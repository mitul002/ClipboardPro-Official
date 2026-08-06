using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media.Imaging;
using ClipboardPro.Models;
using ClipboardPro.Services;
using ClipboardPro.Views;
using System.Threading.Tasks;

namespace ClipboardPro.ViewModels
{
    public class CategoryInfo : INotifyPropertyChanged
    {
        private string _name = string.Empty;
        public string Name { get => _name; set { _name = value; OnPropertyChanged(); } }
        private string _color = "#3498db";
        public string Color { get => _color; set { _color = value; OnPropertyChanged(); } }
        private string _icon = "\uE8EC";
        public string Icon { get => _icon; set { _icon = value; OnPropertyChanged(); } }
        private int _count;
        public int Count { get => _count; set { _count = value; OnPropertyChanged(); } }
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class MainViewModel : INotifyPropertyChanged, IDisposable
    {
        public class UndoBatch : INotifyPropertyChanged
        {
            public List<ClipboardItem> Items { get; set; } = new();
            public bool IsSingle { get; set; }
            public int Index { get; set; }
            public int FilteredIndex { get; set; }
            public CategoryData? Category { get; set; }
            public List<ClipboardItem>? CategoryItems { get; set; }
            public DateTime Expiration { get; set; }
            public string DisplayName { get; set; } = "Item deleted";
            private int _progress;
            public int Progress { get => _progress; set { _progress = value; OnPropertyChanged(); } }
            private int _countdown;
            public int Countdown { get => _countdown; set { _countdown = value; OnPropertyChanged(); } }
            public event PropertyChangedEventHandler? PropertyChanged;
            protected void OnPropertyChanged([CallerMemberName] string name = null!) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        private static readonly System.Text.RegularExpressions.Regex UrlRegex = new(@"^(https?://|www\.)[a-zA-Z0-9.-]+\.[a-zA-Z]{2,15}(\/\S*)?$|^[a-zA-Z0-9.-]+\.(com|net|org|edu|gov|io|ai|me|info|sh|app|dev|xyz|so|online|site|tech)\b(\/\S*)?$", System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Compiled);
        private static readonly System.Net.Http.HttpClient _sharedHttpClient = CreateSharedHttpClient();
        private static System.Net.Http.HttpClient CreateSharedHttpClient()
        {
            var handler = new System.Net.Http.HttpClientHandler { AutomaticDecompression = System.Net.DecompressionMethods.All, AllowAutoRedirect = true, MaxAutomaticRedirections = 12 };
            var client = new System.Net.Http.HttpClient(handler) { Timeout = TimeSpan.FromSeconds(15) };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/121.0.0.0 Safari/537.36");
            client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("text/html"));
            client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/xhtml+xml"));
            client.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.9");
            return client;
        }

        private readonly StorageService _storage;
        private readonly ClipboardMonitorService _monitor;
        private List<ClipboardItem> _allItems;
        private readonly object _collectionLock = new();
        private string _activeFilter = "All Items";
        private string _searchText = string.Empty;
        private string _sortOrder = "Newest First";
        private string _dateFilter = "All Dates";
        private string _typeFilter = "All Types";
        private bool _isGridView = false;
        private System.Threading.CancellationTokenSource? _searchDebounceToken;

        private ObservableCollection<ClipboardItem> _filteredItems = new();
        public ObservableCollection<ClipboardItem> FilteredItems { get => _filteredItems; set { _filteredItems = value; OnPropertyChanged(); } }
        private ObservableCollection<SnippetItem> _filteredSnippets = new();
        public ObservableCollection<SnippetItem> FilteredSnippets { get => _filteredSnippets; set { _filteredSnippets = value; OnPropertyChanged(); } }
        public ObservableCollection<CategoryInfo> CustomCategoryInfos { get; } = new();
        public List<ClipboardItem> AllItems => _allItems;

        private int _totalCount, _favoriteCount, _pinnedCount, _urlCount, _emailCount, _codeCount, _phoneCount, _imageCount, _colorCount, _pathCount, _directoryCount, _privateCount, _snippetCount, _fileCount;
        public int TotalCount => _totalCount;
        public int FavoriteCount => _favoriteCount;
        public int PinnedCount => _pinnedCount;
        public int UrlCount => _urlCount;
        public int EmailCount => _emailCount;
        public int CodeCount => _codeCount;
        public int PhoneCount => _phoneCount;
        public int ImageCount => _imageCount;
        public int ColorCount => _colorCount;
        public int PathCount => _pathCount;
        public int DirectoryCount => _directoryCount;
        public int PrivateCount => _privateCount;
        public int SnippetCount => _snippetCount;
        public int FileCount => _fileCount;

        private AppSettings _settings = new();
        public AppSettings Settings { get => _settings; private set { if (_settings?.CustomCategories != null) _settings.CustomCategories.CollectionChanged -= CustomCategory_CollectionChanged; _settings = value; OnPropertyChanged(); if (_settings?.CustomCategories != null) _settings.CustomCategories.CollectionChanged += CustomCategory_CollectionChanged; UpdateCategoryCounts(); } }

        public ObservableCollection<UndoBatch> ActiveUndos { get; } = new();
        public ObservableCollection<string> ShelfPaths { get; } = new();
        public HashSet<string> SelectedShelfPaths { get; } = new();
        public event Action? ShelfSelectionChanged;
        public void NotifyShelfSelectionChanged()
        {
            if (System.Windows.Application.Current?.Dispatcher != null)
            {
                System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() => ShelfSelectionChanged?.Invoke()));
            }
            else
            {
                ShelfSelectionChanged?.Invoke();
            }
        }
        private System.Windows.Threading.Dispatcher Dispatcher => System.Windows.Application.Current.Dispatcher;
        private bool _isBatchUpdating = false;
        private bool _isUndoMonitorRunning = false;
        private readonly object _undoLock = new();
        private bool _isUndoVisible;
        private bool _isInitializing = true;
        public bool IsUndoVisible { get => _isUndoVisible; set { _isUndoVisible = value; OnPropertyChanged(); } }

        private void CustomCategory_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e) => UpdateCategoryCounts();

        public MainViewModel(StorageService storage, ClipboardMonitorService monitor)
        {
            _storage = storage;
            _monitor = monitor;
            _network = new NetworkService();
            _allItems = new List<ClipboardItem>();
            Settings = _storage.LoadSettings();
            _network.OnItemReceived += OnNetworkItemReceived;
            _network.OnPeersUpdated += peers => { Dispatcher.Invoke(() => { ActivePeers.Clear(); foreach (var p in peers) ActivePeers.Add(p); OnPropertyChanged(nameof(ActivePeers)); OnPropertyChanged(nameof(NearbyDevicesText)); OnPropertyChanged(nameof(NearbyDevicesCount)); }); };
            // Network is NOT started here — deferred to after UI is shown (faster cold boot)
        }

        public async Task InitializeAsync()
        {
            await Task.Run(() => 
            {
                var loadedItems = _storage.LoadItems(); 
                // PerformAutoMaintenance (orphaned image scan) is intentionally NOT called here.
                // It's deferred to 10s after UI shows to avoid cold-boot slowness.
                // It runs automatically via StartDeferredMaintenance(), or via Optimize button.
                
                foreach (var item in loadedItems) RegisterItemEvents(item); 

                Dispatcher.Invoke(() => { 
                    _allItems = loadedItems; 
                    UpdateCategoryCounts(); 
                });
            });
            await ApplyFilter();
            _isInitializing = false;
        }

        // ── Deferred Maintenance: runs 10s after app shows — zero impact on UI ──
        public void StartDeferredMaintenance()
        {
            Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(10000); // Wait 10 seconds after app is shown
                    List<ClipboardItem> snapshot;
                    lock (_collectionLock) { snapshot = _allItems.ToList(); }
                    _storage.PerformAutoMaintenance(snapshot);
                }
                catch { }
            });
        }

        // ── On-Demand Network Start: safe to call multiple times (idempotent) ──
        private bool _networkStarted = false;
        private readonly object _networkLock = new();
        public void EnsureNetworkStarted()
        {
            lock (_networkLock)
            {
                if (_networkStarted) return;
                _networkStarted = true;
            }
            _network.Start();
        }

        public void UpdateCategoryCounts()
        {
            lock (_collectionLock)
            {
                _totalCount = _allItems.Count; _favoriteCount = 0; _pinnedCount = 0; _urlCount = 0; _emailCount = 0; _codeCount = 0; _phoneCount = 0; _imageCount = 0; _colorCount = 0; _pathCount = 0; _directoryCount = 0; _privateCount = 0; _fileCount = 0;
                foreach (var i in _allItems)
                {
                    if (i.IsFavorite) _favoriteCount++; if (i.IsPinned) _pinnedCount++; if (i.IsSensitive) _privateCount++;
                    
                    if (i.Category == "Received") _pathCount++;
                    if (i.Type == ClipboardItemType.Path) _fileCount++;

                    switch (i.Type)
                    {
                        case ClipboardItemType.URL: _urlCount++; break;
                        case ClipboardItemType.Email: _emailCount++; break;
                        case ClipboardItemType.Code: _codeCount++; break;
                        case ClipboardItemType.Phone: _phoneCount++; break;
                        case ClipboardItemType.Image: _imageCount++; break;
                        case ClipboardItemType.Color: _colorCount++; break;
                        case ClipboardItemType.Directory: _directoryCount++; break;
                    }
                }
                _snippetCount = App.TextExpander?.Snippets.Count ?? 0;
            }
            OnPropertyChanged(nameof(TotalCount)); OnPropertyChanged(nameof(FavoriteCount)); OnPropertyChanged(nameof(PinnedCount)); OnPropertyChanged(nameof(UrlCount)); OnPropertyChanged(nameof(EmailCount)); OnPropertyChanged(nameof(CodeCount)); OnPropertyChanged(nameof(PhoneCount)); OnPropertyChanged(nameof(ImageCount)); OnPropertyChanged(nameof(ColorCount)); OnPropertyChanged(nameof(PathCount)); OnPropertyChanged(nameof(DirectoryCount)); OnPropertyChanged(nameof(PrivateCount)); OnPropertyChanged(nameof(SnippetCount)); OnPropertyChanged(nameof(FileCount));

            if (Settings?.CustomCategories != null)
            {
                Dispatcher.Invoke(() => {
                    var existingNames = Settings.CustomCategories.Select(c => c.Name).ToList();
                    for (int i = CustomCategoryInfos.Count - 1; i >= 0; i--) if (!existingNames.Contains(CustomCategoryInfos[i].Name)) CustomCategoryInfos.RemoveAt(i);
                    foreach (var cat in Settings.CustomCategories)
                    {
                        int count = 0;
                        lock (_collectionLock) { count = _allItems?.Count(i => string.Equals(i.Category, cat.Name, StringComparison.OrdinalIgnoreCase)) ?? 0; }
                        var existing = CustomCategoryInfos.FirstOrDefault(c => c.Name == cat.Name);
                        if (existing != null) { if (existing.Count != count) existing.Count = count; if (existing.Color != cat.Color) existing.Color = cat.Color; if (existing.Icon != cat.Icon) existing.Icon = cat.Icon; }
                        else CustomCategoryInfos.Add(new CategoryInfo { Name = cat.Name, Color = cat.Color, Icon = cat.Icon, Count = count });
                    }
                });
            }
            else
            {
                Dispatcher.Invoke(() => CustomCategoryInfos.Clear());
            }
        }

        private void RegisterItemEvents(ClipboardItem item)
        {
            // Unregister first to prevent double-hook leakage
            item.PropertyChanged -= OnItemPropertyChanged;
            item.PropertyChanged += OnItemPropertyChanged;
        }

        private void DeregisterItemEvents(ClipboardItem item)
        {
            item.PropertyChanged -= OnItemPropertyChanged;
        }

        private void OnItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (_isBatchUpdating) return;

            if (sender is ClipboardItem item)
            {
                if (e.PropertyName == nameof(ClipboardItem.IsPinned) || e.PropertyName == nameof(ClipboardItem.IsFavorite) || e.PropertyName == nameof(ClipboardItem.Category) || e.PropertyName == nameof(ClipboardItem.IsSensitive)) 
                {
                    _storage.SaveItem(item); 
                    UpdateCategoryCounts();
                    ApplyFilter();
                } 
                else if (e.PropertyName == nameof(ClipboardItem.Content)) 
                {
                    _storage.SaveItem(item); 
                }
            }
        }

        public string ActiveFilter { get => _activeFilter; set { _activeFilter = value; OnPropertyChanged(); OnPropertyChanged(nameof(FilterTitle)); ApplyFilter(); } }
        public string FilterTitle
        {
            get
            {
                if (_activeFilter == "Snippets") return "Text Expander Snippets";
                if (_activeFilter.StartsWith("cat:")) return _activeFilter.Substring(4);
                return _activeFilter switch
                {
                    "Color" => "Colors",
                    "Path" => "File Received",
                    _ => _activeFilter
                };
            }
        }
        public bool IsGridView { get => _isGridView; set { _isGridView = value; OnPropertyChanged(); } }
        private bool _isSearchLoading;
        public bool IsSearchLoading { get => _isSearchLoading; set { _isSearchLoading = value; OnPropertyChanged(); } }
        public string SearchText
        {
            get => _searchText;
            set
            {
                _searchText = value;
                OnPropertyChanged();
                IsSearchLoading = true;
                // Debounce: wait 300ms after user stops typing before filtering
                _searchDebounceToken?.Cancel();
                var cts = new System.Threading.CancellationTokenSource();
                _searchDebounceToken = cts;
                Task.Delay(300, cts.Token).ContinueWith(t =>
                {
                    if (!t.IsCanceled) ApplyFilter();
                }, System.Threading.Tasks.TaskScheduler.Default);
            }
        }
        public string SortOrder { get => _sortOrder; set { _sortOrder = value; OnPropertyChanged(); ApplyFilter(); } }
        public string DateFilter { get => _dateFilter; set { _dateFilter = value; OnPropertyChanged(); ApplyFilter(); } }
        public string TypeFilter { get => _typeFilter; set { _typeFilter = value; OnPropertyChanged(); ApplyFilter(); } }

        public void AddItem(ClipboardItem item)
        {
            bool isRecentInternal = (DateTime.Now - _lastInternalCopyTime).TotalMilliseconds < 2000;
            if (_isInternalCopy || isRecentInternal) { if (item.Content == _lastInternalCopyContent) return; }
            
            // Bring to front logic ONLY if MergeConsecutiveDuplicates is enabled
            if (Settings.MergeConsecutiveDuplicates)
            {
                ClipboardItem? existing = null;
                lock (_collectionLock) 
                { 
                    existing = _allItems.FirstOrDefault(x => x.Type == item.Type && 
                        (item.Type == ClipboardItemType.Image 
                            ? (x.ImageHash != null && x.ImageHash == item.ImageHash) 
                            : (x.Content == item.Content))); 
                }

                if (existing != null) 
                { 
                    // If it's a duplicate image, we don't need the new file created by the monitor
                    if (item.Type == ClipboardItemType.Image && !string.IsNullOrEmpty(item.ImagePath))
                    {
                        try { System.IO.File.Delete(_storage.GetFullImagePath(item.ImagePath)); } catch { }
                    }

                    existing.Timestamp = DateTime.Now; 
                    lock (_collectionLock) 
                    { 
                        _allItems.Remove(existing); 
                        _allItems.Insert(0, existing); 
                    } 
                    _storage.SaveItem(existing); 
                    ApplyFilter();
                    return; 
                }
            }

            OffloadContent(item);
            if (item.Type != ClipboardItemType.Image) DetectSmartFeatures(item);
            lock (_collectionLock) { _allItems.Insert(0, item); }
            RegisterItemEvents(item);
            lock (_collectionLock) { _storage.EnforceRetentionPolicy(_allItems, Settings); }
            UpdateCategoryCounts();
            
            // To ensure new items are cleanly inserted at top without needing full re-filter 
            // if we are displaying All Items and sorting by Newest First
            if (_activeFilter == "All Items" && _sortOrder == "Newest First" && string.IsNullOrWhiteSpace(_searchText) && _dateFilter == "All Dates" && _typeFilter == "All Types")
            {
                Dispatcher.Invoke(() => {
                    if (_filteredItems != null)
                    {
                        _filteredItems.Insert(0, item);
                    }
                });
            }
            else
            {
                ApplyFilter();
            }
            _storage.SaveItem(item); // Optimized single insert
        }

        private void DetectSmartFeatures(ClipboardItem item)
        {
            var text = item.Content.Trim();
            if (Settings.EnableSensitiveMasking)
            {
                bool isCC = System.Text.RegularExpressions.Regex.IsMatch(text, @"\b(?:\d{4}[ -]?\d{4}[ -]?\d{4}[ -]?\d{4}|\d{4}[ -]?\d{6}[ -]?\d{5}|\d{4}[ -]?\d{6}[ -]?\d{4}|\d{13,16})\b");
                bool isAPI = System.Text.RegularExpressions.Regex.IsMatch(text, @"\b(sk|pk|ak|uk)_(?:live|test|prod)_[a-zA-Z0-9]{20,}\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase) || System.Text.RegularExpressions.Regex.IsMatch(text, @"\b(AKIA|ASIA)[0-9A-Z]{16}\b") || System.Text.RegularExpressions.Regex.IsMatch(text, @"\b(AIza[0-9A-Za-z-_]{35})\b") || System.Text.RegularExpressions.Regex.IsMatch(text, @"\b[a-fA-F0-9]{32,64}\b") || System.Text.RegularExpressions.Regex.IsMatch(text, @"\b((?:sk|pk|secret|key|auth|api|token)[-_a-zA-Z0-9]*[:=][\s]*[a-zA-Z0-9]{12,})\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if (isCC || isAPI)
                {
                    item.IsSensitive = true;
                    item.IsMasked = true; // Critical: Ensure it's hidden by default so eye icon shows up
                }
            }
            var hexMatch = System.Text.RegularExpressions.Regex.Match(text, @"^#([A-Fa-f0-9]{6}|[A-Fa-f0-9]{3})$");
            if (hexMatch.Success) item.DetectedColor = text;
            if ((text.StartsWith("{") && text.EndsWith("}")) || (text.StartsWith("[") && text.EndsWith("]"))) { try { Newtonsoft.Json.Linq.JToken.Parse(text); item.IsJson = true; } catch { } }
            if (item.Type == ClipboardItemType.Text && UrlRegex.IsMatch(text)) item.Type = ClipboardItemType.URL;
            if (item.Type == ClipboardItemType.URL && string.IsNullOrEmpty(item.Title))
            {
                string url = text; if (!url.StartsWith("http", StringComparison.OrdinalIgnoreCase)) url = "https://" + url;
                Task.Run(async () => {
                    try {
                        var response = await _sharedHttpClient.GetAsync(url);
                        if (response.IsSuccessStatusCode) {
                            var bytes = await response.Content.ReadAsByteArrayAsync(); var html = System.Text.Encoding.UTF8.GetString(bytes);
                            var match = System.Text.RegularExpressions.Regex.Match(html, @"<title[^>]*>(.*?)</title>", System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Singleline);
                            if (match.Success) { item.Title = System.Net.WebUtility.HtmlDecode(System.Text.RegularExpressions.Regex.Replace(match.Groups[1].Value, "<.*?>", string.Empty).Trim()); Dispatcher.BeginInvoke(new Action(() => { OnPropertyChanged(nameof(FilteredItems)); _storage.SaveItem(item); })); }
                        }
                    } catch { }
                });
            }
        }

        public void TogglePin(ClipboardItem item) { item.IsPinned = !item.IsPinned; _storage.SaveItem(item); }
        public void ToggleFavorite(ClipboardItem item) { item.IsFavorite = !item.IsFavorite; _storage.SaveItem(item); }
        public void ToggleMask(ClipboardItem item) { item.IsMasked = !item.IsMasked; _storage.SaveItem(item); }
        public void CopyItem(ClipboardItem item) { try { _monitor.InternalCopyCooldown = DateTime.Now.AddSeconds(2); _monitor.InternalCopyContent = item.Content; if (item.Type == ClipboardItemType.Image) { var fullPath = _storage.GetFullImagePath(item.ImagePath); if (File.Exists(fullPath)) { using var img = System.Drawing.Image.FromFile(fullPath); using var bmp = new System.Drawing.Bitmap(img); System.Windows.Clipboard.SetImage(ConvertBitmap(bmp)); } } else { string content = ReloadContent(item); System.Windows.Clipboard.SetText(content); } } catch { } }
        public ClipboardItem? GetItemById(string? id) { if (string.IsNullOrEmpty(id)) return null; lock (_collectionLock) { return _allItems.FirstOrDefault(i => i.Id == id); } }
        
        public void DeleteItem(ClipboardItem item) 
        { 
            int filteredIdx = -1;
            Dispatcher.Invoke(() => { if (FilteredItems != null) filteredIdx = FilteredItems.IndexOf(item); });
            var batch = new UndoBatch { IsSingle = true, Index = _allItems.IndexOf(item), FilteredIndex = filteredIdx, Items = new List<ClipboardItem> { item }, DisplayName = "Item deleted" }; 
            
            Dispatcher.Invoke(() => {
                if (FilteredItems != null && FilteredItems.Contains(item))
                    FilteredItems.Remove(item);
            });

            DeregisterItemEvents(item);
            lock (_collectionLock)
            {
                _allItems.Remove(item);
            }
            _monitor.ResetLastContent();
            PushToUndo(batch);
            
            UpdateCategoryCounts();
        }
        
        public void UndoDelete(UndoBatch? specificBatch = null)
        {
            UndoBatch? batch = specificBatch;
            Dispatcher.Invoke(() => { if (batch == null && ActiveUndos.Count > 0) { batch = ActiveUndos.Last(); ActiveUndos.Remove(batch); } else if (batch != null) ActiveUndos.Remove(batch); });
            if (batch == null) return;
            lock (_collectionLock) {
                if (batch.IsSingle && batch.Items.Count > 0) { var item = batch.Items[0]; if (batch.Index >= 0 && batch.Index <= _allItems.Count) _allItems.Insert(batch.Index, item); else _allItems.Add(item); RegisterItemEvents(item); _storage.SaveItem(item); }
                else if (batch.Category != null && batch.CategoryItems != null) { if (!Settings.CustomCategories.Any(c => c.Name == batch.Category.Name)) Settings.CustomCategories.Add(batch.Category); foreach (var item in batch.CategoryItems) { item.Category = batch.Category.Name; if (!_allItems.Contains(item)) _allItems.Add(item); RegisterItemEvents(item); _storage.SaveItem(item); } SaveSettings(); }
                else { foreach (var item in batch.Items) { if (!_allItems.Contains(item)) { _allItems.Add(item); RegisterItemEvents(item); _storage.SaveItem(item); } } }
            }
            UpdateCategoryCounts();
            
            if (batch.IsSingle && batch.Items.Count > 0)
            {
                var item = batch.Items[0];
                Dispatcher.Invoke(() => {
                    if (FilteredItems != null && !FilteredItems.Contains(item))
                    {
                        if (batch.FilteredIndex >= 0 && batch.FilteredIndex <= FilteredItems.Count)
                            FilteredItems.Insert(batch.FilteredIndex, item);
                        else
                            FilteredItems.Insert(0, item);
                    }
                });
            }
            else
            {
                ApplyFilter();
            }
        }

        private void PushToUndo(UndoBatch batch) { batch.Expiration = DateTime.Now.AddSeconds(10.5); Dispatcher.Invoke(() => ActiveUndos.Add(batch)); StartUndoMonitor(); }
        private void StartUndoMonitor() { lock (_undoLock) { if (_isUndoMonitorRunning) return; _isUndoMonitorRunning = true; } Task.Run(async () => { while (true) { List<UndoBatch> toCommit = new(); Dispatcher.Invoke(() => { var now = DateTime.Now; for (int i = ActiveUndos.Count - 1; i >= 0; i--) { var batch = ActiveUndos[i]; if (now >= batch.Expiration) { toCommit.Add(batch); ActiveUndos.RemoveAt(i); } else { double remainingMs = (batch.Expiration - now).TotalMilliseconds; batch.Progress = (int)((remainingMs / 10000.0) * 100); batch.Countdown = (int)Math.Ceiling(remainingMs / 1000.0); } } IsUndoVisible = ActiveUndos.Count > 0; }); foreach (var batch in toCommit) { foreach (var item in batch.Items) _storage.DeleteItem(item); } int count = 0; Dispatcher.Invoke(() => count = ActiveUndos.Count); lock (_undoLock) { if (count == 0) { _isUndoMonitorRunning = false; break; } } await Task.Delay(100); } }); }

        public Task ApplyFilter()
        {
            IsSearchLoading = true;
            int currentToken = System.Threading.Interlocked.Increment(ref _filterRequestToken);
            
            return Task.Run(() => ApplyFilterCore(currentToken));
        }

        private void ApplyFilterCore(int currentToken)
        {
                // ── Snippets Filter ──
                if (_activeFilter == "Snippets")
                {
                    var snippets = App.TextExpander?.Snippets.ToList() ?? new List<SnippetItem>();
                    var filtered = snippets.AsEnumerable();
                    if (!string.IsNullOrWhiteSpace(_searchText))
                    {
                        var search = _searchText.Trim();
                        filtered = filtered.Where(s => FuzzyMatch(search, s.Trigger) || FuzzyMatch(search, s.Content) || (s.Description != null && FuzzyMatch(search, s.Description)));
                    }

                    // Sorting for snippets
                    filtered = _sortOrder switch
                    {
                        "Oldest First" => filtered.OrderBy(s => s.CreatedAt),
                        "A-Z" => filtered.OrderBy(s => s.Trigger),
                        "Z-A" => filtered.OrderByDescending(s => s.Trigger),
                        _ => filtered.OrderByDescending(s => s.CreatedAt)
                    };

                    var finalSnippets = filtered.ToList();
                    var newCol = new ObservableCollection<SnippetItem>(finalSnippets);
                    var snippetAction = new Action(() => {
                        if (currentToken != _filterRequestToken) return;
                        FilteredSnippets = newCol;
                        _snippetCount = snippets.Count;
                        OnPropertyChanged(nameof(SnippetCount));
                        IsSearchLoading = false;
                    });
                    
                    if (_isInitializing || Dispatcher.CheckAccess())
                        snippetAction();
                    else
                        Dispatcher.BeginInvoke(snippetAction, System.Windows.Threading.DispatcherPriority.Normal);
                    return;
                }

                List<ClipboardItem> itemsCopy; lock (_collectionLock) { itemsCopy = _allItems.ToList(); }
                var result = itemsCopy.AsEnumerable();
                if (!string.IsNullOrEmpty(_activeFilter) && _activeFilter.StartsWith("cat:")) { var catName = _activeFilter.Substring(4); result = result.Where(i => string.Equals(i.Category, catName, StringComparison.OrdinalIgnoreCase)); }
                else { result = _activeFilter switch { "Favorites" => result.Where(i => i.IsFavorite), "Pinned" => result.Where(i => i.IsPinned), "URL" => result.Where(i => i.Type == ClipboardItemType.URL), "Email" => result.Where(i => i.Type == ClipboardItemType.Email), "Code" => result.Where(i => i.Type == ClipboardItemType.Code), "Phone" => result.Where(i => i.Type == ClipboardItemType.Phone), "Image" => result.Where(i => i.Type == ClipboardItemType.Image), "Color" => result.Where(i => i.Type == ClipboardItemType.Color), "Path" => result.Where(i => i.Type == ClipboardItemType.Path), "Directory" => result.Where(i => i.Type == ClipboardItemType.Directory), "File Received" => result.Where(i => i.Category == "Received"), "File" => result.Where(i => i.Type == ClipboardItemType.Path), "Private" => result.Where(i => i.IsSensitive), _ => result }; }
                
                if (!string.IsNullOrWhiteSpace(_searchText)) {
                    var search = _searchText.Trim();
                    if (search.StartsWith("@") && search.Length > 1) {
                        string t = search.Substring(1).ToLower();
                        if (t == "image" || t == "images") result = result.Where(i => i.Type == ClipboardItemType.Image);
                        else if (t == "link" || t == "url" || t == "links") result = result.Where(i => i.Type == ClipboardItemType.URL);
                        else if (t == "text") result = result.Where(i => i.Type == ClipboardItemType.Text);
                        else if (t == "code") result = result.Where(i => i.Type == ClipboardItemType.Code);
                        else if (t == "file" || t == "files" || t == "path") result = result.Where(i => i.Type == ClipboardItemType.Path);
                        else if (t == "color" || t == "colors") result = result.Where(i => i.Type == ClipboardItemType.Color);
                        else if (t == "dir" || t == "directory") result = result.Where(i => i.Type == ClipboardItemType.Directory);
                        else result = result.Where(i => FuzzyMatch(search, i.Content));
                    }
                    else {
                        result = result.Where(i => FuzzyMatch(search, i.Content));
                    }
                }

                if (_typeFilter != "All Types") {
                    result = _typeFilter switch {
                        "Text" => result.Where(i => i.Type == ClipboardItemType.Text),
                        "Images" => result.Where(i => i.Type == ClipboardItemType.Image),
                        "Links" => result.Where(i => i.Type == ClipboardItemType.URL),
                        "Code" => result.Where(i => i.Type == ClipboardItemType.Code),
                        "Email" => result.Where(i => i.Type == ClipboardItemType.Email),
                        "Phone" => result.Where(i => i.Type == ClipboardItemType.Phone),
                        "Colors" => result.Where(i => i.Type == ClipboardItemType.Color),
                        "Files" => result.Where(i => i.Type == ClipboardItemType.Path),
                        "Directory" => result.Where(i => i.Type == ClipboardItemType.Directory),
                        _ => result
                    };
                }

                if (_dateFilter != "All Dates") {
                    var today = DateTime.Today;
                    result = _dateFilter switch {
                        "Today" => result.Where(i => i.Timestamp.Date == today),
                        "Yesterday" => result.Where(i => i.Timestamp.Date == today.AddDays(-1)),
                        "This Week" => result.Where(i => i.Timestamp >= today.AddDays(-7)),
                        "This Month" => result.Where(i => i.Timestamp >= today.AddMonths(-1)),
                        _ => result
                    };
                }

                var ordered = result.OrderByDescending(i => i.IsPinned);
                var finalResults = _sortOrder switch {
                    "Oldest First" => ordered.ThenBy(i => i.Timestamp).ToList(),
                    "A-Z" => ordered.ThenBy(i => i.Title ?? (i.Content != null && i.Content.Length > 50 ? i.Content[..50] : i.Content ?? string.Empty)).ToList(),
                    "Z-A" => ordered.ThenByDescending(i => i.Title ?? (i.Content != null && i.Content.Length > 50 ? i.Content[..50] : i.Content ?? string.Empty)).ToList(),
                    _ => ordered.ThenByDescending(i => i.Timestamp).ToList()
                };

                var action = new Action(() => {
                    if (currentToken != _filterRequestToken) return;

                    // In-place update: avoid replacing the entire collection (prevents full ListBox re-render)
                    // 1. Quick identity check — if nothing changed, skip entirely
                    if (FilteredItems != null && FilteredItems.Count == finalResults.Count)
                    {
                        bool identical = true;
                        for (int j = 0; j < finalResults.Count; j++)
                        {
                            if (FilteredItems[j].Id != finalResults[j].Id) { identical = false; break; }
                        }
                        if (identical) { IsSearchLoading = false; return; }
                    }

                    // Check if there are too many changes (e.g. resorting entirely)
                    int moves = Math.Abs((FilteredItems?.Count ?? 0) - finalResults.Count);
                    if (FilteredItems != null)
                    {
                        int limit = Math.Min(FilteredItems.Count, finalResults.Count);
                        for (int j = 0; j < limit; j++)
                        {
                            if (FilteredItems[j].Id != finalResults[j].Id) moves++;
                        }
                    }

                    if (FilteredItems == null || moves > Math.Max(10, FilteredItems.Count / 3))
                    {
                        FilteredItems = new ObservableCollection<ClipboardItem>(finalResults);
                        OnPropertyChanged(nameof(TotalCount));
                        IsSearchLoading = false;
                        return;
                    }

                    // 2. Remove items that are no longer in the result
                    var resultIds = new HashSet<string>(finalResults.Select(i => i.Id));
                    for (int j = FilteredItems.Count - 1; j >= 0; j--)
                    {
                        if (!resultIds.Contains(FilteredItems[j].Id))
                            FilteredItems.RemoveAt(j);
                    }

                    // 3. Add/move items to match finalResults order
                    for (int j = 0; j < finalResults.Count; j++)
                    {
                        var targetItem = finalResults[j];
                        if (j < FilteredItems.Count)
                        {
                            if (FilteredItems[j].Id != targetItem.Id)
                            {
                                int existingIndex = -1;
                                for (int k = j + 1; k < FilteredItems.Count; k++)
                                {
                                    if (FilteredItems[k].Id == targetItem.Id) { existingIndex = k; break; }
                                }
                                if (existingIndex >= 0)
                                    FilteredItems.Move(existingIndex, j);
                                else
                                    FilteredItems.Insert(j, targetItem);
                            }
                        }
                        else
                        {
                            FilteredItems.Add(targetItem);
                        }
                    }

                    OnPropertyChanged(nameof(TotalCount));
                    IsSearchLoading = false;
                });

                if (_isInitializing || Dispatcher.CheckAccess())
                    action();
                else
                    Dispatcher.BeginInvoke(action, System.Windows.Threading.DispatcherPriority.Normal);
        }

        private bool FuzzyMatch(string query, string content)
        {
            if (string.IsNullOrEmpty(query)) return true;
            if (string.IsNullOrEmpty(content)) return false;
            return content.Contains(query, StringComparison.OrdinalIgnoreCase);
        }

        private int _filterRequestToken = 0;
        public void SaveItems(bool immediate = false)
        {
            // Fully managed via SQLite single records, retained for interface compatibility
            List<ClipboardItem> itemsSnapshot;
            lock (_collectionLock)
            {
                itemsSnapshot = _allItems.ToList();
            }
            Task.Run(() => _storage.SaveItems(itemsSnapshot));
        }

        public void SaveSettings() => _storage.SaveSettings(Settings);
        
        public void AddOrUpdateSnippet(SnippetItem snippet)
        {
            App.TextExpander?.AddOrUpdate(snippet);
            UpdateCategoryCounts();
            ApplyFilter();
        }

        public void DeleteSnippet(SnippetItem snippet)
        {
            App.TextExpander?.Delete(snippet);
            UpdateCategoryCounts();
            ApplyFilter();
        }

        public void ClearAllSnippets()
        {
            App.TextExpander?.ClearAll();
            UpdateCategoryCounts();
            ApplyFilter();
        }

        private void OffloadContent(ClipboardItem item) 
        { 
            if (item.Type == ClipboardItemType.Image || string.IsNullOrEmpty(item.Content) || item.Content.Length < 4096) return; 
            try 
            { 
                var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ClipboardPro", "Cache"); 
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir); 
                item.OffloadedContentPath = Path.Combine(dir, item.Id + ".txt"); 
                
                // Write asynchronously to prevent monitor thread lock
                File.WriteAllTextAsync(item.OffloadedContentPath, item.Content); 
                item.Content = item.Content.Substring(0, 4096) + "... [Full content offloaded to SSD]"; 
            } 
            catch { } 
        }

        private string ReloadContent(ClipboardItem item) { if (item.OffloadedContentPath == null) return item.Content; try { if (File.Exists(item.OffloadedContentPath)) return File.ReadAllText(item.OffloadedContentPath); } catch { } return item.Content; }
        
        private readonly NetworkService _network;
        public ObservableCollection<PeerInfo> ActivePeers { get; } = new();
        public string NearbyDevicesText => ActivePeers.Count == 0 ? "No devices nearby" : $"{ActivePeers.Count} devices nearby";
        public int NearbyDevicesCount => ActivePeers.Count;
        private bool _isInternalCopy = false;
        private DateTime _lastInternalCopyTime = DateTime.MinValue;
        private string _lastInternalCopyContent = string.Empty;

        private void OnNetworkItemReceived(ClipboardItem item)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                bool isDuplicate = false;
                lock (_collectionLock)
                {
                    isDuplicate = _allItems.Any(i => i.Type == item.Type &&
                        (item.Type == ClipboardItemType.Image
                            ? (!string.IsNullOrEmpty(i.ImageHash) && i.ImageHash == item.ImageHash)
                            : (i.Content == item.Content)));
                }

                item.Category = "Received";

                if (!isDuplicate || !Settings.MergeConsecutiveDuplicates)
                {
                    item.Timestamp = DateTime.Now;
                    item.Id = Guid.NewGuid().ToString();
                    AddItem(item);
                }
                else if (isDuplicate && Settings.MergeConsecutiveDuplicates)
                {
                    item.Timestamp = DateTime.Now;
                    AddItem(item);
                }
            }));
        }
        
        public async Task<bool> SendToDevice(ClipboardItem item, PeerInfo peer, System.Threading.CancellationToken ct = default, System.Threading.ManualResetEventSlim? pauseEvent = null) { try { return await _network.SendItemAsync(item, peer.IP, peer.Port, ct, pauseEvent); } catch { return false; } }
        
        public void ClearAll() 
        { 
            List<ClipboardItem> toRemove; 
            lock (_collectionLock) 
            { 
                if (ActiveFilter == "Pinned")
                    toRemove = FilteredItems.ToList();
                else if (ActiveFilter == "Favorites")
                    toRemove = FilteredItems.ToList();
                else if (ActiveFilter == "All Items" || ActiveFilter == "All") 
                    toRemove = _allItems.Where(i => !i.IsPinned).ToList(); 
                else if (ActiveFilter.StartsWith("cat:")) 
                { 
                    var cat = ActiveFilter.Substring(4); 
                    toRemove = _allItems.Where(i => !i.IsPinned && string.Equals(i.Category, cat, StringComparison.OrdinalIgnoreCase)).ToList(); 
                } 
                else 
                    toRemove = FilteredItems.Where(i => !i.IsPinned).ToList(); 

                if (toRemove.Count == 0) return;

                foreach (var item in toRemove) 
                { 
                    DeregisterItemEvents(item); 
                    _allItems.Remove(item); 
                } 
            }

            var batch = new UndoBatch 
            { 
                IsSingle = false, 
                Items = toRemove, 
                DisplayName = toRemove.Count > 1 ? $"{toRemove.Count} items cleared" : "Item cleared" 
            };
            PushToUndo(batch);

            UpdateCategoryCounts();
            ApplyFilter(); 
        }

        public void DeleteCategory(string catName)
        {
            var cat = Settings.CustomCategories.FirstOrDefault(c => c.Name == catName);
            if (cat != null)
            {
                Settings.CustomCategories.Remove(cat);
                _isBatchUpdating = true;
                try
                {
                    lock (_collectionLock)
                    {
                        var itemsToUpdate = _allItems.Where(i => string.Equals(i.Category, catName, StringComparison.OrdinalIgnoreCase)).ToList();
                        foreach (var item in itemsToUpdate)
                        {
                            item.Category = null;
                            _storage.SaveItem(item);
                        }
                    }
                }
                finally
                {
                    _isBatchUpdating = false;
                }
                SaveSettings();
                UpdateCategoryCounts();
                if (ActiveFilter == "cat:" + catName) ActiveFilter = "All Items";
                else ApplyFilter();
            }
        }
        
        public void RenameCategory(string oldName, string newName, string color, string icon)
        {
            var cat = Settings.CustomCategories.FirstOrDefault(c => c.Name == oldName);
            if (cat != null)
            {
                cat.Name = newName;
                cat.Color = color;
                cat.Icon = icon;
                
                _isBatchUpdating = true;
                try
                {
                    lock (_collectionLock)
                    {
                        var itemsToUpdate = _allItems.Where(i => string.Equals(i.Category, oldName, StringComparison.OrdinalIgnoreCase)).ToList();
                        foreach (var item in itemsToUpdate)
                        {
                            item.Category = newName;
                            _storage.SaveItem(item);
                        }
                    }
                }
                finally
                {
                    _isBatchUpdating = false;
                }

                SaveSettings();
                UpdateCategoryCounts();
                if (ActiveFilter == "cat:" + oldName) ActiveFilter = "cat:" + newName;
                else ApplyFilter();
            }
        }

        public void PastePlainText(ClipboardItem item) { string content = ReloadContent(item); if (item.Type == ClipboardItemType.Image) return; _monitor.InternalCopyCooldown = DateTime.Now.AddSeconds(2); _monitor.InternalCopyContent = content; System.Windows.Clipboard.SetText(content); }
        
        public void ExportZip(string path) 
        { 
            try 
            { 
                var tempDir = Path.Combine(Path.GetTempPath(), "ClipboardProExport_" + Guid.NewGuid()); 
                Directory.CreateDirectory(tempDir); 
                
                lock (_collectionLock) 
                { 
                    _storage.SaveSettings(Settings); 
                } 

                // Freeze database and backup cleanly via vacuum command
                var tempDbPath = Path.Combine(tempDir, "clipboard.db");
                _storage.VacuumDatabase(tempDbPath);

                var dataFolder = _storage.GetDataFolder(); 
                if (File.Exists(Path.Combine(dataFolder, "settings.json")))
                    File.Copy(Path.Combine(dataFolder, "settings.json"), Path.Combine(tempDir, "settings.json"), true);
                if (File.Exists(Path.Combine(dataFolder, "snippets.json")))
                    File.Copy(Path.Combine(dataFolder, "snippets.json"), Path.Combine(tempDir, "snippets.json"), true);
                    
                var imgDir = _storage.GetImagesFolder(); 
                if (Directory.Exists(imgDir)) { 
                    var tempImgDir = Path.Combine(tempDir, "Images"); 
                    Directory.CreateDirectory(tempImgDir); 
                    foreach (var file in Directory.GetFiles(imgDir)) 
                        File.Copy(file, Path.Combine(tempImgDir, Path.GetFileName(file))); 
                } 
                if (File.Exists(path)) File.Delete(path); 
                System.IO.Compression.ZipFile.CreateFromDirectory(tempDir, path); 
                Directory.Delete(tempDir, true); 
            } 
            catch (Exception ex) 
            { 
                System.Windows.MessageBox.Show("Export failed: " + ex.Message); 
            } 
        }

        public void ImportZip(string path)
        {
            try
            {
                var choice = new RestoreChoiceWindow();
                if (choice.ShowDialog() != true || choice.SelectedMode == ImportMode.Cancel) return;

                var tempDir = Path.Combine(Path.GetTempPath(), "ClipboardProImport_" + Guid.NewGuid());
                System.IO.Compression.ZipFile.ExtractToDirectory(path, tempDir);
                var dataFile = Path.Combine(tempDir, "data.json");
                var dbFile = Path.Combine(tempDir, "clipboard.db");
                var settingsFile = Path.Combine(tempDir, "settings.json");

                if (choice.SelectedMode == ImportMode.Replace)
                {
                    lock (_collectionLock) 
                    {
                        foreach (var item in _allItems) DeregisterItemEvents(item);
                        _allItems.Clear();
                    }
                    if (File.Exists(settingsFile))
                    {
                        var importedSettings = _storage.LoadSettingsFromFile(settingsFile);
                        if (importedSettings != null) Settings = importedSettings;
                    }
                    App.TextExpander?.ClearAll();
                }

                // Case 1: Legacy JSON Import
                if (File.Exists(dataFile))
                {
                    var importedItems = _storage.ImportJson(dataFile);
                    lock (_collectionLock)
                    {
                        foreach (var item in importedItems)
                        {
                            if (choice.SelectedMode == ImportMode.Replace || !_allItems.Any(i => i.Id == item.Id))
                            {
                                _allItems.Add(item);
                                RegisterItemEvents(item);
                            }
                        }
                    }
                    _storage.SaveItems(_allItems);
                }
                // Case 2: Modern SQLite DB Import
                else if (File.Exists(dbFile))
                {
                    if (choice.SelectedMode == ImportMode.Replace)
                    {
                        _storage.OverwriteDatabase(dbFile);
                        var loaded = _storage.LoadItems();
                        lock (_collectionLock)
                        {
                            _allItems = loaded;
                            foreach (var item in _allItems) RegisterItemEvents(item);
                        }
                    }
                    else // Merge Mode
                    {
                        _storage.ImportDatabaseMerge(dbFile);
                        var loaded = _storage.LoadItems();
                        lock (_collectionLock)
                        {
                            foreach (var item in _allItems) DeregisterItemEvents(item);
                            _allItems = loaded;
                            foreach (var item in _allItems) RegisterItemEvents(item);
                        }
                    }
                    App.TextExpander?.Load();
                }

                // Import Snippets
                var snippetsFile = Path.Combine(tempDir, "snippets.json");
                if (File.Exists(snippetsFile))
                {
                    try
                    {
                        var json = File.ReadAllText(snippetsFile);
                        var importedSnippets = Newtonsoft.Json.JsonConvert.DeserializeObject<List<SnippetItem>>(json);
                        if (importedSnippets != null)
                        {
                            foreach (var s in importedSnippets)
                            {
                                if (choice.SelectedMode == ImportMode.Replace || !(App.TextExpander?.Snippets.Any(x => x.Id == s.Id) ?? false))
                                {
                                    App.TextExpander?.AddOrUpdate(s);
                                }
                            }
                        }
                    }
                    catch { }
                }

                var imgDir = Path.Combine(tempDir, "Images");
                if (Directory.Exists(imgDir))
                {
                    var targetImgDir = _storage.GetImagesFolder();
                    foreach (var file in Directory.GetFiles(imgDir))
                    {
                        var targetFile = Path.Combine(targetImgDir, Path.GetFileName(file));
                        if (!File.Exists(targetFile)) File.Copy(file, targetFile);
                    }
                }

                SaveSettings();
                UpdateCategoryCounts();
                ApplyFilter();
                Directory.Delete(tempDir, true);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show("Import failed: " + ex.Message);
            }
        }

        public void PrettifyJson(ClipboardItem item) { try { var content = ReloadContent(item); var parsed = Newtonsoft.Json.Linq.JToken.Parse(content); item.Content = parsed.ToString(Newtonsoft.Json.Formatting.Indented); _storage.SaveItem(item); } catch { } }
        public void UpdateItemCategory(ClipboardItem item, string category) { item.Category = category; _storage.SaveItem(item); }
        public string GetFullImagePath(string? fileName) => _storage.GetFullImagePath(fileName);
        public void OptimizeDatabase() 
        { 
            // 1. Clean orphaned images
            List<ClipboardItem> snapshot;
            lock (_collectionLock) { snapshot = _allItems.ToList(); }
            _storage.PerformAutoMaintenance(snapshot);
            // 2. Compact the SQLite database file (reclaims deleted row space)
            _storage.VacuumSelf();
        }
        public void OptimizeHistory() { lock (_collectionLock) _storage.EnforceRetentionPolicy(_allItems, Settings); ApplyFilter(); }
        public void ResetSettings() { Settings = new AppSettings(); SaveSettings(); }
        public void ResetTotalApp() {
            lock (_collectionLock) {
                foreach (var item in _allItems) DeregisterItemEvents(item);
                _allItems.Clear();
                Settings = new AppSettings();
                App.TextExpander?.ClearAll();
                _storage.ClearAllClipboardItems();
                
                Dispatcher.Invoke(() => {
                    ActiveUndos.Clear();
                    FilteredItems?.Clear();
                });
            }

            // Force GC and wait to release any potential WPF/GDI file locks
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            lock (_collectionLock) {
                var imagesDir = _storage.GetImagesFolder();
                if (Directory.Exists(imagesDir)) {
                    foreach (var file in Directory.GetFiles(imagesDir)) {
                        for (int i = 0; i < 3; i++) {
                            try { 
                                File.Delete(file); 
                                break; 
                            } 
                            catch { 
                                System.Threading.Thread.Sleep(50); 
                            }
                        }
                    }
                }
                var cacheDir = Path.Combine(_storage.GetDataFolder(), "Cache");
                if (Directory.Exists(cacheDir)) {
                    foreach (var file in Directory.GetFiles(cacheDir)) {
                        try { File.Delete(file); } catch { }
                    }
                }
                var receivedDir = _storage.GetReceivedFolder();
                if (Directory.Exists(receivedDir)) {
                    foreach (var file in Directory.GetFiles(receivedDir)) {
                        try { File.Delete(file); } catch { }
                    }
                }
                var dataDir = _storage.GetDataFolder();
                try { File.Delete(Path.Combine(dataDir, "data.json")); } catch { }
                try { File.Delete(Path.Combine(dataDir, "data.json.migrated")); } catch { }
                try { File.Delete(Path.Combine(dataDir, "snippets.json.migrated")); } catch { }
            }
            SaveSettings();
            UpdateCategoryCounts();
            ApplyFilter();
        }
        public void TrimMemory() { try { GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect(); if (Environment.OSVersion.Platform == PlatformID.Win32NT) SetProcessWorkingSetSize(System.Diagnostics.Process.GetCurrentProcess().Handle, (IntPtr)(-1), (IntPtr)(-1)); } catch { } }
        [DllImport("kernel32.dll")] private static extern bool SetProcessWorkingSetSize(IntPtr process, IntPtr minimumWorkingSetSize, IntPtr maximumWorkingSetSize);

        public void Dispose()
        {
            _network?.Dispose();
            
            // Unregister all hooked events to avoid leaks
            lock (_collectionLock)
            {
                foreach (var item in _allItems) DeregisterItemEvents(item);
            }
        }
        
        [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr hObject);
        private static System.Windows.Media.Imaging.BitmapSource ConvertBitmap(System.Drawing.Bitmap bitmap) { var handle = bitmap.GetHbitmap(); try { var source = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(handle, IntPtr.Zero, Int32Rect.Empty, System.Windows.Media.Imaging.BitmapSizeOptions.FromEmptyOptions()); source.Freeze(); return source; } finally { DeleteObject(handle); } }
        private static string StripFormatting(string text) => System.Text.RegularExpressions.Regex.Replace(text, @"<.*?>", string.Empty).Trim();

        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
