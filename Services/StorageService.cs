using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using Microsoft.Data.Sqlite;
using Newtonsoft.Json;
using ClipboardPro.Models;

namespace ClipboardPro.Services
{
    public class StorageService
    {
        private readonly string _dataDir;
        private readonly string _imagesDir;
        private readonly string _receivedDir;
        private readonly string _dataFile;
        private readonly string _settingsFile;
        private readonly string _dbFile;
        private readonly string _connectionString;
        private readonly object _dbLock = new();

        public StorageService()
        {
            _dataDir      = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ClipboardPro");
            _imagesDir    = Path.Combine(_dataDir, "Images");
            _receivedDir  = Path.Combine(_dataDir, "Received");
            _dataFile     = Path.Combine(_dataDir, "data.json");
            _settingsFile = Path.Combine(_dataDir, "settings.json");
            _dbFile       = Path.Combine(_dataDir, "clipboard.db");

            Directory.CreateDirectory(_dataDir);
            Directory.CreateDirectory(_imagesDir);
            Directory.CreateDirectory(_receivedDir);

            // High-performance WAL mode connection string
            _connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = _dbFile,
                Mode = SqliteOpenMode.ReadWriteCreate,
                Cache = SqliteCacheMode.Shared
            }.ToString();

            InitializeDatabase();
            MigrateLegacyData();
            MigrateLegacySnippets();
        }

        // ── Database Initialization ──────────────────────────────────────────

        private void InitializeDatabase()
        {
            lock (_dbLock)
            {
                using var conn = new SqliteConnection(_connectionString);
                conn.Open();

                // Enable WAL mode for asynchronous safe concurrent reading/writing and set busy timeout
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "PRAGMA journal_mode=WAL; PRAGMA synchronous=NORMAL; PRAGMA busy_timeout=5000;";
                    cmd.ExecuteNonQuery();
                }

                // Table 1: ClipboardItems
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"
                        CREATE TABLE IF NOT EXISTS ClipboardItems (
                            Id TEXT PRIMARY KEY NOT NULL,
                            Content TEXT NOT NULL,
                            OffloadedContentPath TEXT NULL,
                            ImagePath TEXT NULL,
                            Type INTEGER NOT NULL,
                            Timestamp TEXT NOT NULL,
                            IsPinned INTEGER NOT NULL DEFAULT 0,
                            IsFavorite INTEGER NOT NULL DEFAULT 0,
                            Category TEXT NULL,
                            IsSensitive INTEGER NOT NULL DEFAULT 0,
                            IsMasked INTEGER NOT NULL DEFAULT 1,
                            DetectedColor TEXT NULL,
                            IsJson INTEGER NOT NULL DEFAULT 0,
                            Title TEXT NULL,
                            ImageHash TEXT NULL
                        );
                        CREATE INDEX IF NOT EXISTS IX_ClipboardItems_Timestamp ON ClipboardItems (Timestamp DESC);
                        CREATE INDEX IF NOT EXISTS IX_ClipboardItems_Pinned_Timestamp ON ClipboardItems (IsPinned DESC, Timestamp DESC);
                        CREATE INDEX IF NOT EXISTS IX_ClipboardItems_Category ON ClipboardItems (Category);
                        CREATE INDEX IF NOT EXISTS IX_ClipboardItems_Type ON ClipboardItems (Type);
                    ";
                    cmd.ExecuteNonQuery();
                }

                // Table 2: SnippetItems
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"
                        CREATE TABLE IF NOT EXISTS SnippetItems (
                            Id TEXT PRIMARY KEY NOT NULL,
                            Trigger TEXT UNIQUE NOT NULL,
                            Content TEXT NOT NULL,
                            Description TEXT NULL,
                            CreatedAt TEXT NOT NULL
                        );
                        CREATE INDEX IF NOT EXISTS IX_SnippetItems_Trigger ON SnippetItems (Trigger);
                    ";
                    cmd.ExecuteNonQuery();
                }

                // Migration: Add ImageHash column if it doesn't exist
                try
                {
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = "ALTER TABLE ClipboardItems ADD COLUMN ImageHash TEXT NULL;";
                        cmd.ExecuteNonQuery();
                    }
                }
                catch { /* Column already exists */ }
            }
        }

        // ── Legacy Migration ──────────────────────────────────────────────────

        private void MigrateLegacyData()
        {
            if (!File.Exists(_dataFile)) return;

            try
            {
                List<ClipboardItem> legacyItems;
                using (var fs = new FileStream(_dataFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var sr = new StreamReader(fs))
                using (var jr = new JsonTextReader(sr))
                {
                    var serializer = new JsonSerializer();
                    legacyItems = serializer.Deserialize<List<ClipboardItem>>(jr) ?? new List<ClipboardItem>();
                }

                if (legacyItems.Count > 0)
                {
                    SaveItems(legacyItems);
                }

                // Rename legacy file to secure backup instead of deletion
                var backupFile = _dataFile + ".migrated";
                if (File.Exists(backupFile)) File.Delete(backupFile);
                File.Move(_dataFile, backupFile);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Migration failed: {ex.Message}");
            }
        }

        private void MigrateLegacySnippets()
        {
            var legacyFile = Path.Combine(_dataDir, "snippets.json");
            if (!File.Exists(legacyFile)) return;

            try
            {
                List<SnippetItem> legacySnippets;
                using (var fs = new FileStream(legacyFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var sr = new StreamReader(fs))
                using (var jr = new JsonTextReader(sr))
                {
                    var serializer = new JsonSerializer();
                    legacySnippets = serializer.Deserialize<List<SnippetItem>>(jr) ?? new List<SnippetItem>();
                }

                if (legacySnippets.Count > 0)
                {
                    SaveSnippets(legacySnippets);
                }

                var backupFile = legacyFile + ".migrated";
                if (File.Exists(backupFile)) File.Delete(backupFile);
                File.Move(legacyFile, backupFile);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Snippets migration failed: {ex.Message}");
            }
        }

        // ── Items CRUD Operations ─────────────────────────────────────────────

        public List<ClipboardItem> LoadItems()
        {
            var list = new List<ClipboardItem>();
            lock (_dbLock)
            {
                try
                {
                    using var conn = new SqliteConnection(_connectionString);
                    conn.Open();

                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = "SELECT * FROM ClipboardItems ORDER BY IsPinned DESC, Timestamp DESC";

                    using var reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        var item = new ClipboardItem
                        {
                            Id = reader.GetString(0),
                            Content = reader.GetString(1),
                            OffloadedContentPath = reader.IsDBNull(2) ? null : reader.GetString(2),
                            ImagePath = reader.IsDBNull(3) ? null : reader.GetString(3),
                            Type = (ClipboardItemType)reader.GetInt32(4),
                            Timestamp = DateTime.Parse(reader.GetString(5)),
                            IsPinned = reader.GetInt32(6) == 1,
                            IsFavorite = reader.GetInt32(7) == 1,
                            Category = reader.IsDBNull(8) ? null : reader.GetString(8),
                            IsSensitive = reader.GetInt32(9) == 1,
                            IsMasked = reader.GetInt32(10) == 1,
                            DetectedColor = reader.IsDBNull(11) ? null : reader.GetString(11),
                            IsJson = reader.GetInt32(12) == 1,
                            Title = reader.IsDBNull(13) ? null : reader.GetString(13),
                            ImageHash = reader.IsDBNull(14) ? null : reader.GetString(14)
                        };
                        list.Add(item);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Database read failed: {ex.Message}");
                }
            }
            return list;
        }

        public void SaveItems(List<ClipboardItem> items)
        {
            lock (_dbLock)
            {
                try
                {
                    using var conn = new SqliteConnection(_connectionString);
                    conn.Open();

                    using var transaction = conn.BeginTransaction();

                    // Upsert items into database
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = @"
                        INSERT OR REPLACE INTO ClipboardItems (
                            Id, Content, OffloadedContentPath, ImagePath, Type, Timestamp,
                            IsPinned, IsFavorite, Category, IsSensitive, IsMasked, DetectedColor, IsJson, Title, ImageHash
                        ) VALUES (
                            $id, $content, $offloadedContentPath, $imagePath, $type, $timestamp,
                            $isPinned, $isFavorite, $category, $isSensitive, $isMasked, $detectedColor, $isJson, $title, $imageHash
                        )";

                    var pId = cmd.CreateParameter(); pId.ParameterName = "$id"; cmd.Parameters.Add(pId);
                    var pContent = cmd.CreateParameter(); pContent.ParameterName = "$content"; cmd.Parameters.Add(pContent);
                    var pOffPath = cmd.CreateParameter(); pOffPath.ParameterName = "$offloadedContentPath"; cmd.Parameters.Add(pOffPath);
                    var pImgPath = cmd.CreateParameter(); pImgPath.ParameterName = "$imagePath"; cmd.Parameters.Add(pImgPath);
                    var pType = cmd.CreateParameter(); pType.ParameterName = "$type"; cmd.Parameters.Add(pType);
                    var pTime = cmd.CreateParameter(); pTime.ParameterName = "$timestamp"; cmd.Parameters.Add(pTime);
                    var pPinned = cmd.CreateParameter(); pPinned.ParameterName = "$isPinned"; cmd.Parameters.Add(pPinned);
                    var pFav = cmd.CreateParameter(); pFav.ParameterName = "$isFavorite"; cmd.Parameters.Add(pFav);
                    var pCat = cmd.CreateParameter(); pCat.ParameterName = "$category"; cmd.Parameters.Add(pCat);
                    var pSens = cmd.CreateParameter(); pSens.ParameterName = "$isSensitive"; cmd.Parameters.Add(pSens);
                    var pMask = cmd.CreateParameter(); pMask.ParameterName = "$isMasked"; cmd.Parameters.Add(pMask);
                    var pColor = cmd.CreateParameter(); pColor.ParameterName = "$detectedColor"; cmd.Parameters.Add(pColor);
                    var pJson = cmd.CreateParameter(); pJson.ParameterName = "$isJson"; cmd.Parameters.Add(pJson);
                    var pTitle = cmd.CreateParameter(); pTitle.ParameterName = "$title"; cmd.Parameters.Add(pTitle);
                    var pHash = cmd.CreateParameter(); pHash.ParameterName = "$imageHash"; cmd.Parameters.Add(pHash);

                    foreach (var item in items)
                    {
                        pId.Value = item.Id;
                        pContent.Value = item.Content;
                        pOffPath.Value = (object?)item.OffloadedContentPath ?? DBNull.Value;
                        pImgPath.Value = (object?)item.ImagePath ?? DBNull.Value;
                        pType.Value = (int)item.Type;
                        pTime.Value = item.Timestamp.ToString("o"); // Roundtrip ISO 8601
                        pPinned.Value = item.IsPinned ? 1 : 0;
                        pFav.Value = item.IsFavorite ? 1 : 0;
                        pCat.Value = (object?)item.Category ?? DBNull.Value;
                        pSens.Value = item.IsSensitive ? 1 : 0;
                        pMask.Value = item.IsMasked ? 1 : 0;
                        pColor.Value = (object?)item.DetectedColor ?? DBNull.Value;
                        pJson.Value = item.IsJson ? 1 : 0;
                        pTitle.Value = (object?)item.Title ?? DBNull.Value;
                        pHash.Value = (object?)item.ImageHash ?? DBNull.Value;

                        cmd.ExecuteNonQuery();
                    }

                    transaction.Commit();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Database save failed: {ex.Message}");
                }
            }
        }

        // ── Single Item Saving ──────────────────────────────────────────────

        public void SaveItem(ClipboardItem item)
        {
            lock (_dbLock)
            {
                try
                {
                    using var conn = new SqliteConnection(_connectionString);
                    conn.Open();

                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = @"
                        INSERT OR REPLACE INTO ClipboardItems (
                            Id, Content, OffloadedContentPath, ImagePath, Type, Timestamp,
                            IsPinned, IsFavorite, Category, IsSensitive, IsMasked, DetectedColor, IsJson, Title, ImageHash
                        ) VALUES (
                            @id, @content, @offloadedContentPath, @imagePath, @type, @timestamp,
                            @isPinned, @isFavorite, @category, @isSensitive, @isMasked, @detectedColor, @isJson, @title, @imageHash
                        )";

                    cmd.Parameters.AddWithValue("@id", item.Id);
                    cmd.Parameters.AddWithValue("@content", item.Content);
                    cmd.Parameters.AddWithValue("@offloadedContentPath", (object?)item.OffloadedContentPath ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@imagePath", (object?)item.ImagePath ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@type", (int)item.Type);
                    cmd.Parameters.AddWithValue("@timestamp", item.Timestamp.ToString("o"));
                    cmd.Parameters.AddWithValue("@isPinned", item.IsPinned ? 1 : 0);
                    cmd.Parameters.AddWithValue("@isFavorite", item.IsFavorite ? 1 : 0);
                    cmd.Parameters.AddWithValue("@category", (object?)item.Category ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@isSensitive", item.IsSensitive ? 1 : 0);
                    cmd.Parameters.AddWithValue("@isMasked", item.IsMasked ? 1 : 0);
                    cmd.Parameters.AddWithValue("@detectedColor", (object?)item.DetectedColor ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@isJson", item.IsJson ? 1 : 0);
                    cmd.Parameters.AddWithValue("@title", (object?)item.Title ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@imageHash", (object?)item.ImageHash ?? DBNull.Value);

                    cmd.ExecuteNonQuery();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Single item save failed: {ex.Message}");
                }
            }
        }

        public void DeleteItem(ClipboardItem item)
        {
            lock (_dbLock)
            {
                try
                {
                    using var conn = new SqliteConnection(_connectionString);
                    conn.Open();

                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = "DELETE FROM ClipboardItems WHERE Id = @id";
                    cmd.Parameters.AddWithValue("@id", item.Id);
                    cmd.ExecuteNonQuery();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Single delete failed: {ex.Message}");
                }
            }

            // Cleanup local filesystem images
            if (!string.IsNullOrEmpty(item.ImagePath))
            {
                var fullPath = GetFullImagePath(item.ImagePath);
                if (File.Exists(fullPath))
                {
                    try { File.Delete(fullPath); } catch { }
                }
            }

            // Cleanup local offloaded text cache files
            if (!string.IsNullOrEmpty(item.OffloadedContentPath))
            {
                if (File.Exists(item.OffloadedContentPath))
                {
                    try { File.Delete(item.OffloadedContentPath); } catch { }
                }
            }
        }

        // ── Settings ──────────────────────────────────────────────────────────

        public AppSettings LoadSettings()
        {
            if (!File.Exists(_settingsFile)) return new AppSettings();
            try
            {
                var json = File.ReadAllText(_settingsFile);
                return JsonConvert.DeserializeObject<AppSettings>(json) ?? new AppSettings();
            }
            catch { return new AppSettings(); }
        }

        public void SaveSettings(AppSettings settings)
        {
            var json = JsonConvert.SerializeObject(settings, Formatting.Indented);
            var temp = _settingsFile + ".tmp";
            
            lock (_dbLock)
            {
                File.WriteAllText(temp, json);
                File.Move(temp, _settingsFile, overwrite: true);
            }
        }

        public AppSettings? LoadSettingsFromFile(string path)
        {
            if (!File.Exists(path)) return null;
            try
            {
                var json = File.ReadAllText(path);
                return JsonConvert.DeserializeObject<AppSettings>(json);
            }
            catch { return null; }
        }

        // ── Backup Imports / Exports ──────────────────────────────────────────

        public void ExportJson(List<ClipboardItem> items, string path)
        {
            var json = JsonConvert.SerializeObject(items, Formatting.Indented);
            File.WriteAllText(path, json);
        }

        public List<ClipboardItem> ImportJson(string path)
        {
            if (!File.Exists(path)) return new List<ClipboardItem>();
            try
            {
                var json = File.ReadAllText(path);
                return JsonConvert.DeserializeObject<List<ClipboardItem>>(json) ?? new List<ClipboardItem>();
            }
            catch { return new List<ClipboardItem>(); }
        }

        public List<ClipboardItem> LoadItemsFromFile(string path) => ImportJson(path);

        public void VacuumDatabase(string backupPath)
        {
            lock (_dbLock)
            {
                try
                {
                    using var conn = new SqliteConnection(_connectionString);
                    conn.Open();

                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = "VACUUM INTO @backupPath";
                    cmd.Parameters.AddWithValue("@backupPath", backupPath);
                    cmd.ExecuteNonQuery();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Database vacuum failed: {ex.Message}");
                }
            }
        }

        // ── In-Place Vacuum: compacts the live database, reclaims deleted row space ──
        public void VacuumSelf()
        {
            lock (_dbLock)
            {
                try
                {
                    using var conn = new SqliteConnection(_connectionString);
                    conn.Open();
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = "VACUUM;";
                    cmd.ExecuteNonQuery();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"VacuumSelf failed: {ex.Message}");
                }
            }
        }

        public void ImportDatabaseMerge(string importedDbPath)
        {
            lock (_dbLock)
            {
                try
                {
                    using var conn = new SqliteConnection(_connectionString);
                    conn.Open();

                    using var cmd = conn.CreateCommand();
                    // Attach temporary external DB connection
                    cmd.CommandText = "ATTACH DATABASE @importedDbPath AS imported";
                    cmd.Parameters.AddWithValue("@importedDbPath", importedDbPath);
                    cmd.ExecuteNonQuery();

                    // Perform high-speed SQL merge directly inside SQLite engine
                    using var cmdMerge = conn.CreateCommand();
                    cmdMerge.CommandText = "INSERT OR IGNORE INTO main.ClipboardItems SELECT * FROM imported.ClipboardItems";
                    cmdMerge.ExecuteNonQuery();

                    // Also merge SnippetItems if the table exists in the imported database
                    try
                    {
                        using var cmdMergeSnippets = conn.CreateCommand();
                        cmdMergeSnippets.CommandText = "INSERT OR IGNORE INTO main.SnippetItems SELECT * FROM imported.SnippetItems";
                        cmdMergeSnippets.ExecuteNonQuery();
                    }
                    catch { }

                    using var cmdDetach = conn.CreateCommand();
                    cmdDetach.CommandText = "DETACH DATABASE imported";
                    cmdDetach.ExecuteNonQuery();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Database merge failed: {ex.Message}");
                }
            }
        }

        public void OverwriteDatabase(string newDbPath)
        {
            lock (_dbLock)
            {
                // Break current active database file completely
                SqliteConnection.ClearAllPools();
                if (File.Exists(_dbFile)) File.Delete(_dbFile);
                File.Copy(newDbPath, _dbFile);
            }
            InitializeDatabase();
        }

        // ── Single-File Launcher ──────────────────────────────────────────────

        public void SetLaunchOnStartup(bool enable, bool startMinimized = false)
        {
            try
            {
                var exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
                if (string.IsNullOrEmpty(exePath)) return;

                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);
                if (enable)
                {
                    string command = $"\"{exePath}\" --minimized";
                    key?.SetValue("ClipboardPro", command);
                }
                else key?.DeleteValue("ClipboardPro", false);
            }
            catch { /* GPO policy blocks registry writing */ }
        }

        // ── Image Handling Helpers ────────────────────────────────────────────

        public string SaveImage(System.Drawing.Image image, string? hash = null)
        {
            var fileName = !string.IsNullOrEmpty(hash) 
                ? $"img_{hash}.png" 
                : $"img_{DateTime.Now:yyyyMMdd_HHmmss_fff}.png";
            
            var fullPath = Path.Combine(_imagesDir, fileName);
            if (!File.Exists(fullPath))
            {
                image.Save(fullPath, ImageFormat.Png);
            }
            return fileName; 
        }

        public string GetFullImagePath(string? fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return string.Empty;
            if (Path.IsPathRooted(fileName)) return fileName; 
            return Path.Combine(_imagesDir, fileName);
        }

        public string GenerateThumbnailBase64(System.Drawing.Image original)
        {
            int maxSize = 400;
            double ratio = Math.Min((double)maxSize / original.Width, (double)maxSize / original.Height);
            int w = (int)(original.Width  * ratio);
            int h = (int)(original.Height * ratio);

            using var thumb = new Bitmap(w, h);
            using var g = Graphics.FromImage(thumb);
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            g.DrawImage(original, 0, 0, w, h);

            using var ms = new MemoryStream();
            thumb.Save(ms, ImageFormat.Png);
            return Convert.ToBase64String(ms.ToArray());
        }

        public string GetDataFolder() => _dataDir;
        public string GetImagesFolder() => _imagesDir;
        public string GetReceivedFolder() => _receivedDir;
        public string GetDatabasePath() => _dbFile;

        // ── Maintenance & Auto-Trimming ───────────────────────────────────────

        public void EnforceRetentionPolicy(List<ClipboardItem> items, AppSettings settings)
        {
            if (!settings.AutoDeleteOldItems) return;

            lock (_dbLock)
            {
                try
                {
                    using var conn = new SqliteConnection(_connectionString);
                    conn.Open();

                    // Step 1: Remove unpinned database items past max days threshold
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = @"
                            DELETE FROM ClipboardItems 
                            WHERE IsPinned = 0 
                              AND datetime(Timestamp) < datetime('now', '-' || @days || ' days')";
                        cmd.Parameters.AddWithValue("@days", settings.MaxHistoryDays);
                        cmd.ExecuteNonQuery();
                    }

                    // Step 2: Trim count to MaxHistoryItems
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = @"
                            DELETE FROM ClipboardItems 
                            WHERE IsPinned = 0 
                              AND Id NOT IN (
                                  SELECT Id FROM ClipboardItems 
                                  WHERE IsPinned = 1 OR IsPinned = 0 
                                  ORDER BY Timestamp DESC 
                                  LIMIT @limit
                              )";
                        cmd.Parameters.AddWithValue("@limit", settings.MaxHistoryItems);
                        cmd.ExecuteNonQuery();
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Retention trim failed: {ex.Message}");
                }
            }

            // Sync with local memory cache collection
            var cutoff = DateTime.Now.AddDays(-settings.MaxHistoryDays);
            var toRemove = items
                .Where(i => !i.IsPinned && i.Timestamp < cutoff)
                .ToList();

            foreach (var item in toRemove)
            {
                items.Remove(item);
            }

            var unpinned = items.Where(i => !i.IsPinned).OrderByDescending(i => i.Timestamp).ToList();
            if (unpinned.Count > settings.MaxHistoryItems)
            {
                var excess = unpinned.Skip(settings.MaxHistoryItems).ToList();
                foreach (var item in excess)
                {
                    items.Remove(item);
                }
            }
        }

        public void PerformAutoMaintenance(List<ClipboardItem> items)
        {
            try
            {
                if (!Directory.Exists(_imagesDir)) return;

                var referencedImages = new HashSet<string>(
                    items.Where(i => !string.IsNullOrEmpty(i.ImagePath))
                         .Select(i => Path.GetFileName(i.ImagePath)),
                    StringComparer.OrdinalIgnoreCase
                );

                var actualFiles = Directory.GetFiles(_imagesDir);
                foreach (var file in actualFiles)
                {
                    var name = Path.GetFileName(file);
                    if (!referencedImages.Contains(name))
                    {
                        try { File.Delete(file); } catch { }
                    }
                }
            }
            catch { }
        }

        // ── Snippets CRUD Operations ──────────────────────────────────────────

        public List<SnippetItem> LoadSnippets()
        {
            var list = new List<SnippetItem>();
            lock (_dbLock)
            {
                try
                {
                    using var conn = new SqliteConnection(_connectionString);
                    conn.Open();

                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = "SELECT * FROM SnippetItems ORDER BY CreatedAt DESC";

                    using var reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        var item = new SnippetItem
                        {
                            Id = reader.GetString(0),
                            Trigger = reader.GetString(1),
                            Content = reader.GetString(2),
                            Description = reader.IsDBNull(3) ? null : reader.GetString(3),
                            CreatedAt = DateTime.Parse(reader.GetString(4))
                        };
                        list.Add(item);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Snippet read failed: {ex.Message}");
                }
            }
            return list;
        }

        public void SaveSnippets(List<SnippetItem> snippets)
        {
            lock (_dbLock)
            {
                try
                {
                    using var conn = new SqliteConnection(_connectionString);
                    conn.Open();

                    using var transaction = conn.BeginTransaction();

                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = @"
                        INSERT OR REPLACE INTO SnippetItems (
                            Id, Trigger, Content, Description, CreatedAt
                        ) VALUES (
                            $id, $trigger, $content, $description, $createdAt
                        )";

                    var pId = cmd.CreateParameter(); pId.ParameterName = "$id"; cmd.Parameters.Add(pId);
                    var pTrigger = cmd.CreateParameter(); pTrigger.ParameterName = "$trigger"; cmd.Parameters.Add(pTrigger);
                    var pContent = cmd.CreateParameter(); pContent.ParameterName = "$content"; cmd.Parameters.Add(pContent);
                    var pDesc = cmd.CreateParameter(); pDesc.ParameterName = "$description"; cmd.Parameters.Add(pDesc);
                    var pCreated = cmd.CreateParameter(); pCreated.ParameterName = "$createdAt"; cmd.Parameters.Add(pCreated);

                    foreach (var item in snippets)
                    {
                        pId.Value = item.Id;
                        pTrigger.Value = item.Trigger;
                        pContent.Value = item.Content;
                        pDesc.Value = (object?)item.Description ?? DBNull.Value;
                        pCreated.Value = item.CreatedAt.ToString("o");

                        cmd.ExecuteNonQuery();
                    }

                    transaction.Commit();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Snippets save failed: {ex.Message}");
                }
            }
        }

        public void SaveSnippet(SnippetItem snippet)
        {
            lock (_dbLock)
            {
                try
                {
                    using var conn = new SqliteConnection(_connectionString);
                    conn.Open();

                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = @"
                        INSERT OR REPLACE INTO SnippetItems (
                            Id, Trigger, Content, Description, CreatedAt
                        ) VALUES (
                            @id, @trigger, @content, @description, @createdAt
                        )";

                    cmd.Parameters.AddWithValue("@id", snippet.Id);
                    cmd.Parameters.AddWithValue("@trigger", snippet.Trigger);
                    cmd.Parameters.AddWithValue("@content", snippet.Content);
                    cmd.Parameters.AddWithValue("@description", (object?)snippet.Description ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@createdAt", snippet.CreatedAt.ToString("o"));

                    cmd.ExecuteNonQuery();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Snippet save failed: {ex.Message}");
                }
            }
        }

        public void DeleteSnippet(SnippetItem snippet)
        {
            lock (_dbLock)
            {
                try
                {
                    using var conn = new SqliteConnection(_connectionString);
                    conn.Open();

                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = "DELETE FROM SnippetItems WHERE Id = @id";
                    cmd.Parameters.AddWithValue("@id", snippet.Id);
                    cmd.ExecuteNonQuery();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Snippet delete failed: {ex.Message}");
                }
            }
        }

        public void ClearAllSnippets()
        {
            lock (_dbLock)
            {
                try
                {
                    using var conn = new SqliteConnection(_connectionString);
                    conn.Open();

                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = "DELETE FROM SnippetItems";
                    cmd.ExecuteNonQuery();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Snippets clear failed: {ex.Message}");
                }
            }
        }

        public void ClearAllClipboardItems()
        {
            lock (_dbLock)
            {
                try
                {
                    using var conn = new SqliteConnection(_connectionString);
                    conn.Open();

                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = "DELETE FROM ClipboardItems";
                    cmd.ExecuteNonQuery();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"ClipboardItems clear failed: {ex.Message}");
                }
            }
        }
    }
}
