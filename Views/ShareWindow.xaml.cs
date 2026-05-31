using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using ClipboardPro.ViewModels;
using ClipboardPro.Models;

namespace ClipboardPro.Views
{
    public partial class ShareWindow : Window
    {
        private readonly MainViewModel _vm;
        private ObservableCollection<TransferModel> _transfers = new();

        public ShareWindow(MainViewModel vm)
        {
            InitializeComponent();
            _vm = vm;
            DataContext = _vm;
            
            DevicesList.ItemsSource = _vm.ActivePeers;
            TransfersList.ItemsSource = _transfers;
            
            // Listen for count changes to update status text
            _vm.ActivePeers.CollectionChanged += (s, e) => UpdateStatus();
            UpdateStatus();
        }

        private void UpdateStatus()
        {
            TxtStatus.Text = _vm.NearbyDevicesText;
            ScanningState.Visibility = _vm.NearbyDevicesCount > 0 ? Visibility.Collapsed : Visibility.Visible;
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left) DragMove();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            this.Hide(); // Hide instead of close to keep it persistent if needed
        }

        private void BtnMinimize_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        private void BtnBack_Click(object sender, RoutedEventArgs e)
        {
            DiscoveryView.Visibility = Visibility.Visible;
            SessionView.Visibility = Visibility.Collapsed;
            BtnBack.Visibility = Visibility.Collapsed;
            TxtTitle.Text = "Local Share";
        }

        private ClipboardPro.Services.PeerInfo? _selectedPeer;

        private void Device_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is ClipboardPro.Services.PeerInfo peer)
            {
                _selectedPeer = peer;
                DiscoveryView.Visibility = Visibility.Collapsed;
                SessionView.Visibility = Visibility.Visible;
                BtnBack.Visibility = Visibility.Visible;
                TxtTitle.Text = peer.Name;
            }
        }

        private void DropZone_DragOver(object sender, System.Windows.DragEventArgs e)
        {
            if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
            {
                e.Effects = System.Windows.DragDropEffects.Copy;
                IconDrop.Foreground = (System.Windows.Media.Brush)FindResource("AccentPrimary");
            }
        }

        private void DropZone_DragLeave(object sender, System.Windows.DragEventArgs e)
        {
            IconDrop.Foreground = (System.Windows.Media.Brush)FindResource("TextMuted");
        }

        private void DropZone_Drop(object sender, System.Windows.DragEventArgs e)
        {
            if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(System.Windows.DataFormats.FileDrop);
                ProcessFiles(files);
                IconDrop.Foreground = (System.Windows.Media.Brush)FindResource("TextMuted");
            }
        }

        private void BtnBrowse_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog { Multiselect = true };
            if (dialog.ShowDialog() == true)
            {
                ProcessFiles(dialog.FileNames);
            }
        }

        private async void ProcessFiles(string[] files)
        {
            if (_selectedPeer == null) return;
            var peer = _selectedPeer; // Capture local

            foreach (string file in files)
            {
                var transfer = new TransferModel 
                { 
                    FileName = System.IO.Path.GetFileName(file), 
                    Progress = 0, 
                    Status = "Sending..." 
                };
                _transfers.Insert(0, transfer);

                try 
                {
                    var item = new ClipboardItem 
                    { 
                        Type = ClipboardItemType.Path, 
                        Content = file,
                        Timestamp = DateTime.Now
                    };

                    // Simple progress bridge
                    item.PropertyChanged += (s, ev) => 
                    {
                        Dispatcher.BeginInvoke(new Action(() => 
                        {
                            if (ev.PropertyName == nameof(ClipboardItem.SendingPercentage))
                                transfer.Progress = (int)item.SendingPercentage;
                            if (ev.PropertyName == nameof(ClipboardItem.BytesSent))
                                transfer.BytesTransferred = item.BytesSent;
                            if (ev.PropertyName == nameof(ClipboardItem.TotalBytes))
                                transfer.TotalBytes = item.TotalBytes;
                        }));
                    };

                    bool success = await _vm.SendToDevice(item, peer, transfer.Cts.Token, transfer.PauseEvent);
                    
                    transfer.Status = success ? "Completed" : "Failed";
                    if (success) transfer.Progress = 100;
                }
                catch (OperationCanceledException)
                {
                    transfer.Status = "Cancelled";
                }
                catch 
                {
                    transfer.Status = "Error";
                }
                finally
                {
                    transfer.IsActive = false;
                }
            }
        }

        private void BtnPause_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is TransferModel tm)
                tm.TogglePause();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is TransferModel tm)
                tm.Cancel();
        }

        private void BtnOpenReceived_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var path = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ClipboardPro", "Received");
                if (!System.IO.Directory.Exists(path)) System.IO.Directory.CreateDirectory(path);
                System.Diagnostics.Process.Start("explorer.exe", path);
            }
            catch { }
        }

        private void BtnViewHistory_Click(object sender, RoutedEventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(() => {
                if (System.Windows.Application.Current is App app)
                {
                    app.ShowMainWindow("Received"); // Navigate to "File Received"
                }
            }));
        }
    }

    public class TransferModel : System.ComponentModel.INotifyPropertyChanged
    {
        private int _progress;
        private long _bytesTransferred;
        private long _totalBytes;
        private string _status = "Waiting...";
        private bool _isPaused;
        private bool _isActive = true;

        public bool IsActive
        {
            get => _isActive;
            set { _isActive = value; OnPropertyChanged(); OnPropertyChanged(nameof(CanAction)); }
        }

        public string FileName { get; set; } = string.Empty;
        public System.Threading.CancellationTokenSource Cts { get; } = new();
        public System.Threading.ManualResetEventSlim PauseEvent { get; } = new(true);

        public int Progress
        {
            get => _progress;
            set { _progress = value; OnPropertyChanged(); OnPropertyChanged(nameof(CanAction)); }
        }

        public long BytesTransferred
        {
            get => _bytesTransferred;
            set { _bytesTransferred = value; OnPropertyChanged(); OnPropertyChanged(nameof(SizeDisplay)); }
        }

        public long TotalBytes
        {
            get => _totalBytes;
            set { _totalBytes = value; OnPropertyChanged(); OnPropertyChanged(nameof(SizeDisplay)); }
        }

        public string SizeDisplay => TotalBytes > 0 ? $"{FormatSize(BytesTransferred)} / {FormatSize(TotalBytes)}" : "";

        public string Status
        {
            get => _status;
            set { _status = value; OnPropertyChanged(); }
        }

        public bool IsPaused
        {
            get => _isPaused;
            set { _isPaused = value; OnPropertyChanged(); OnPropertyChanged(nameof(PauseIcon)); }
        }

        public string PauseIcon => IsPaused ? "\uE768" : "\uE769"; // Play : Pause
        public Visibility CanAction => (IsActive && Progress < 100) ? Visibility.Visible : Visibility.Collapsed;

        public void TogglePause()
        {
            if (IsPaused)
            {
                PauseEvent.Set();
                IsPaused = false;
                Status = "Sending...";
            }
            else
            {
                PauseEvent.Reset();
                IsPaused = true;
                Status = "Paused";
            }
        }

        public void Cancel()
        {
            Cts.Cancel();
            PauseEvent.Set();
            Status = "Cancelled";
            IsActive = false;
        }

        private string FormatSize(long bytes)
        {
            string[] units = { "B", "KB", "MB", "GB" };
            double size = bytes;
            int unitIndex = 0;
            while (size >= 1024 && unitIndex < units.Length - 1)
            {
                size /= 1024;
                unitIndex++;
            }
            return $"{size:0.#} {units[unitIndex]}";
        }

        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(name));
        }
    }
}
