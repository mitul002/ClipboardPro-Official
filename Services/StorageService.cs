using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
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

        public StorageService()
        {
            _dataDir     = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ClipboardPro");
            _imagesDir   = Path.Combine(_dataDir, "Images");
            _receivedDir = Path.Combine(_dataDir, "Received");
            _dataFile    = Path.Combine(_dataDir, "data.json");
            _settingsFile = Path.Combine(_dataDir, "settings.json");

            Directory.CreateDirectory(_dataDir);
            Directory.CreateDirectory(_imagesDir);
            Directory.CreateDirectory(_receivedDir);
        }

        // ── Items ──────────────────────────────────────────────────────────

        public List<ClipboardItem> LoadItems()
        {
            if (!File.Exists(_dataFile)) return new List<ClipboardItem>();
            try
            {
                using var fs = new FileStream(_dataFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var sr = new StreamReader(fs);
                using var jr = new JsonTextReader(sr);
                var serializer = new JsonSerializer();
                return serializer.Deserialize<List<ClipboardItem>>(jr) ?? new List<ClipboardItem>();
            }
            catch
            {
                BackupCorruptedData();
                return new List<ClipboardItem>();
            }
        }

        private readonly object _fileLock = new();

        public void SaveItems(List<ClipboardItem> items)
        {
            var temp = _dataFile + ".tmp";
            
            lock (_fileLock)
            {
                using (var fs = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
                using (var sw = new StreamWriter(fs))
                using (var jw = new JsonTextWriter(sw) { Formatting = Formatting.Indented })
                {
                    var serializer = new JsonSerializer();
                    serializer.Serialize(jw, items);
                }
                File.Move(temp, _dataFile, overwrite: true);
            }
        }

        // ── Images ─────────────────────────────────────────────────────────

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
            if (Path.IsPathRooted(fileName)) return fileName; // Support legacy absolute paths
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

        // ── Settings ───────────────────────────────────────────────────────

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
            
            lock (_fileLock)
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

        public void ExportJson(List<ClipboardItem> items, string path)
        {
            var json = JsonConvert.SerializeObject(items, Formatting.Indented);
            File.WriteAllText(path, json);
        }

        public List<ClipboardItem> ImportJson(string path)
        {
            if (!File.Exists(path)) return new List<ClipboardItem>();
            var json = File.ReadAllText(path);
            return JsonConvert.DeserializeObject<List<ClipboardItem>>(json) ?? new List<ClipboardItem>();
        }

        public List<ClipboardItem> LoadItemsFromFile(string path) => ImportJson(path);

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
            catch { /* Permission or Registry issues */ }
        }

        // ── Maintenance ────────────────────────────────────────────────────

        public string GetDataFolder() => _dataDir;
        public string GetImagesFolder() => _imagesDir;
        public string GetReceivedFolder() => _receivedDir;

        public void DeleteItem(ClipboardItem item)
        {
            if (!string.IsNullOrEmpty(item.ImagePath))
            {
                var fullPath = GetFullImagePath(item.ImagePath);
                if (File.Exists(fullPath))
                    try { File.Delete(fullPath); } catch { }
            }
        }

        public void EnforceRetentionPolicy(List<ClipboardItem> items, AppSettings settings)
        {
            if (!settings.AutoDeleteOldItems) return;

            var cutoff = DateTime.Now.AddDays(-settings.MaxHistoryDays);
            var toRemove = items
                .Where(i => !i.IsPinned && i.Timestamp < cutoff)
                .ToList();

            foreach (var item in toRemove)
            {
                DeleteItem(item);
                items.Remove(item);
            }

            var unpinned = items.Where(i => !i.IsPinned).OrderByDescending(i => i.Timestamp).ToList();
            if (unpinned.Count > settings.MaxHistoryItems)
            {
                var excess = unpinned.Skip(settings.MaxHistoryItems).ToList();
                foreach (var item in excess)
                {
                    DeleteItem(item);
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

        private void BackupCorruptedData()
        {
            var backup = _dataFile + $".backup_{DateTime.Now:yyyyMMddHHmmss}";
            if (File.Exists(_dataFile))
                File.Copy(_dataFile, backup);
        }
    }
}
