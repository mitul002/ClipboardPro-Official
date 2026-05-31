using System;

namespace ClipboardPro.Models
{
    public enum ClipboardItemType
    {
        Text,
        Image,
        URL,
        Email,
        Phone,
        Code,
        Color,
        Path,
        Directory
    }

    public enum ImportMode
    {
        Merge,
        Replace,
        Cancel
    }

    public class ClipboardItem : System.ComponentModel.INotifyPropertyChanged
    {
        private bool _isPinned;
        private bool _isFavorite;

        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string? OffloadedContentPath { get; set; }
        private string _content = string.Empty;
        public string Content
        {
            get => _content;
            set 
            { 
                _content = value; 
                OnPropertyChanged(); 
                OnPropertyChanged(nameof(Preview)); 
                OnPropertyChanged(nameof(MaskedContent));
            }
        }
        private string? _imagePath;
        public string? ImagePath 
        { 
            get => _imagePath; 
            set { _imagePath = value; OnPropertyChanged(); } 
        }
        [Newtonsoft.Json.JsonIgnore]
        public string? ThumbnailBase64 { get; set; }
        public ClipboardItemType Type { get; set; } = ClipboardItemType.Text;
        private DateTime _timestamp = DateTime.Now;
        public DateTime Timestamp 
        { 
            get => _timestamp; 
            set 
            { 
                _timestamp = value; 
                OnPropertyChanged(); 
                OnPropertyChanged(nameof(RelativeTime)); 
            } 
        }

        public bool IsPinned
        {
            get => _isPinned;
            set { _isPinned = value; OnPropertyChanged(); }
        }

        public bool IsFavorite
        {
            get => _isFavorite;
            set { _isFavorite = value; OnPropertyChanged(); }
        }

        private string? _category;
        public string? Category
        {
            get => _category;
            set { _category = value; OnPropertyChanged(); }
        }

        public string TypeBadge => Type switch
        {
            ClipboardItemType.URL   => "url",
            ClipboardItemType.Email => "email",
            ClipboardItemType.Code  => "code",
            ClipboardItemType.Phone => "phone",
            ClipboardItemType.Image => "image",
            ClipboardItemType.Color => "color",
            ClipboardItemType.Path  => "file",
            ClipboardItemType.Directory => "folder",
            _                       => "text"
        };

        public string Preview => !string.IsNullOrEmpty(Title) 
            ? Title 
            : (IsSensitive && IsMasked ? "••••••••••••" : (_content.Length > 200 ? _content[..200] + "..." : _content));

        private bool _isSensitive;
        public bool IsSensitive
        {
            get => _isSensitive;
            set { _isSensitive = value; OnPropertyChanged(); OnPropertyChanged(nameof(Preview)); OnPropertyChanged(nameof(MaskedContent)); }
        }

        private bool _isMasked = true;
        public bool IsMasked
        {
            get => _isMasked;
            set { _isMasked = value; OnPropertyChanged(); OnPropertyChanged(nameof(Preview)); OnPropertyChanged(nameof(MaskedContent)); }
        }

        public string MaskedContent => IsSensitive && IsMasked ? "••••••••••••••••" : Content;

        public string RelativeTime
        {
            get
            {
                var span = DateTime.Now - Timestamp;
                if (span.TotalSeconds < 60)  return "Just now";
                if (span.TotalMinutes < 60)  return $"{(int)span.TotalMinutes}m ago";
                if (span.TotalHours   < 24)  return $"{(int)span.TotalHours}h ago";
                return $"{(int)span.TotalDays}d ago";
            }
        }

        private string? _detectedColor;
        public string? DetectedColor
        {
            get => _detectedColor;
            set { _detectedColor = value; OnPropertyChanged(); }
        }

        private bool _isJson;
        public bool IsJson
        {
            get => _isJson;
            set { _isJson = value; OnPropertyChanged(); }
        }

        private string? _title;
        public string? Title
        {
            get => _title;
            set 
            { 
                _title = value; 
                OnPropertyChanged(); 
                OnPropertyChanged(nameof(Preview)); 
            }
        }

        private double _sendingPercentage;
        public double SendingPercentage
        {
            get => _sendingPercentage;
            set { _sendingPercentage = value; OnPropertyChanged(); OnPropertyChanged(nameof(SendingBrush)); }
        }

        private long _totalBytes;
        public long TotalBytes
        {
            get => _totalBytes;
            set { _totalBytes = value; OnPropertyChanged(); }
        }

        private long _bytesSent;
        public long BytesSent
        {
            get => _bytesSent;
            set { _bytesSent = value; OnPropertyChanged(); }
        }

        private bool _isSending;
        public bool IsSending
        {
            get => _isSending;
            set { _isSending = value; OnPropertyChanged(); OnPropertyChanged(nameof(SendingBrush)); }
        }

        private bool _isPaused;
        public bool IsPaused
        {
            get => _isPaused;
            set { _isPaused = value; OnPropertyChanged(); OnPropertyChanged(nameof(SendingBrush)); }
        }

        public System.Windows.Media.Brush SendingBrush => IsSending 
            ? (SendingPercentage >= 100 ? (System.Windows.Media.Brush)new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(39, 174, 96)) // Green
                                       : (System.Windows.Media.Brush)new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(52, 152, 219))) // Blue
            : (System.Windows.Media.Brush)System.Windows.Application.Current.FindResource("TextMuted");

        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
    }
}
