using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ClipboardPro.Models
{
    public class CategoryData : INotifyPropertyChanged
    {
        private string _name = string.Empty;
        private string _color = "#3498db";
        private string _icon = "\uE8EC";

        public string Name { get => _name; set { _name = value; OnPropertyChanged(); } }
        public string Color { get => _color; set { _color = value; OnPropertyChanged(); } }
        public string Icon { get => _icon; set { _icon = value; OnPropertyChanged(); } }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public class AppSettings : INotifyPropertyChanged
    {
        private string _mainWindowHotkey = "Ctrl+Shift+V";
        private string _miniModeHotkey = "Alt+V";
        private string _quickPasteBarHotkey = "Ctrl+Alt+V";
        private int _maxHistoryDays = 7;
        private int _maxHistoryItems = 200;
        private bool _autoDeleteOldItems = true;
        private bool _closeAfterPasting = false;
        private bool _mergeConsecutiveDuplicates = true;
        private bool _launchOnStartup = true;
        private bool _minimizeToTray = true;
        private bool _fuzzySearchEnabled = false;
        private int _themeMode = 2; // 0: Light, 1: Dark, 2: System Default
        private bool _isDarkMode = true;
        private string _imagePreviewSize = "Medium";
        private bool _alwaysOnTop = false;
        private bool _enableTransparency = true;
        private double _windowOpacity = 1.0;

        private bool _enableQuickPasteBar = true;
        private bool _enableSensitiveMasking = true;
        private int _quickDropAction = 0; // 0: Off, 1: Main Window, 2: Mini Mode, 3: Quick Paste Bar, 4: Local Share, 5: Temporary Shelf
        private bool _enableTextExpander = true;

        public string MainWindowHotkey { get => _mainWindowHotkey; set { _mainWindowHotkey = value; OnPropertyChanged(); } }
        public string MiniModeHotkey { get => _miniModeHotkey; set { _miniModeHotkey = value; OnPropertyChanged(); } }
        public string QuickPasteBarHotkey { get => _quickPasteBarHotkey; set { _quickPasteBarHotkey = value; OnPropertyChanged(); } }
        public int MaxHistoryDays { get => _maxHistoryDays; set { _maxHistoryDays = value; OnPropertyChanged(); } }
        public int MaxHistoryItems { get => _maxHistoryItems; set { _maxHistoryItems = value; OnPropertyChanged(); } }
        public bool AutoDeleteOldItems { get => _autoDeleteOldItems; set { _autoDeleteOldItems = value; OnPropertyChanged(); } }
        public bool CloseAfterPasting { get => _closeAfterPasting; set { _closeAfterPasting = value; OnPropertyChanged(); } }
        public bool MergeConsecutiveDuplicates { get => _mergeConsecutiveDuplicates; set { _mergeConsecutiveDuplicates = value; OnPropertyChanged(); } }
        public bool LaunchOnStartup { get => _launchOnStartup; set { _launchOnStartup = value; OnPropertyChanged(); } }
        public bool MinimizeToTray { get => _minimizeToTray; set { _minimizeToTray = value; OnPropertyChanged(); } }
        public bool FuzzySearchEnabled { get => _fuzzySearchEnabled; set { _fuzzySearchEnabled = value; OnPropertyChanged(); } }
        public int ThemeMode { get => _themeMode; set { _themeMode = value; OnPropertyChanged(); } }
        public bool IsDarkMode { get => _isDarkMode; set { _isDarkMode = value; OnPropertyChanged(); } }
        public string ImagePreviewSize { get => _imagePreviewSize; set { _imagePreviewSize = value; OnPropertyChanged(); } }
        public bool AlwaysOnTop { get => _alwaysOnTop; set { _alwaysOnTop = value; OnPropertyChanged(); } }
        public bool EnableTransparency { get => _enableTransparency; set { _enableTransparency = value; OnPropertyChanged(); } }
        public double WindowOpacity { get => _windowOpacity; set { _windowOpacity = value; OnPropertyChanged(); } }
        public bool EnableQuickPasteBar { get => _enableQuickPasteBar; set { _enableQuickPasteBar = value; OnPropertyChanged(); } }
        public bool EnableSensitiveMasking { get => _enableSensitiveMasking; set { _enableSensitiveMasking = value; OnPropertyChanged(); } }
        public int QuickDropAction { get => _quickDropAction; set { _quickDropAction = value; OnPropertyChanged(); } }
        public bool EnableTextExpander { get => _enableTextExpander; set { _enableTextExpander = value; OnPropertyChanged(); } }
        
        private double _quickPasteBarX = -10000;
        private double _quickPasteBarY = -10000;
        public double QuickPasteBarX { get => _quickPasteBarX; set { _quickPasteBarX = value; OnPropertyChanged(); } }
        public double QuickPasteBarY { get => _quickPasteBarY; set { _quickPasteBarY = value; OnPropertyChanged(); } }

        private bool _startMinimized = true;
        public bool StartMinimized { get => _startMinimized; set { _startMinimized = value; OnPropertyChanged(); } }

        public System.Collections.ObjectModel.ObservableCollection<CategoryData> CustomCategories { get; set; } = new();

        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
