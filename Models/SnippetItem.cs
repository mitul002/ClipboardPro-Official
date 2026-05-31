using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ClipboardPro.Models
{
    public class SnippetItem : INotifyPropertyChanged
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();

        private string _trigger = string.Empty;
        /// <summary>The short abbreviation the user types (e.g. ;em)</summary>
        public string Trigger
        {
            get => _trigger;
            set { _trigger = value; OnPropertyChanged(); OnPropertyChanged(nameof(Preview)); }
        }

        private string _content = string.Empty;
        /// <summary>The full text that replaces the trigger (e.g. example@email.com)</summary>
        public string Content
        {
            get => _content;
            set { _content = value; OnPropertyChanged(); OnPropertyChanged(nameof(Preview)); }
        }

        private string? _description;
        /// <summary>Optional user-friendly label shown in the list</summary>
        public string? Description
        {
            get => _description;
            set { _description = value; OnPropertyChanged(); }
        }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public string RelativeTime
        {
            get
            {
                var span = DateTime.Now - CreatedAt;
                if (span.TotalMinutes < 1) return "Just now";
                if (span.TotalHours < 1) return $"{(int)span.TotalMinutes}m ago";
                if (span.TotalDays < 1) return $"{(int)span.TotalHours}h ago";
                if (span.TotalDays < 7) return $"{(int)span.TotalDays}d ago";
                return CreatedAt.ToString("MMM dd");
            }
        }

        /// <summary>Preview shown in the snippet list card</summary>
        public string Preview => !string.IsNullOrEmpty(_content)
            ? (_content.Length > 80 ? _content[..80] + "…" : _content)
            : "(empty)";

        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
