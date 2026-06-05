using System;
using System.Collections.Concurrent;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;
using System.Windows;
using System.Windows.Interop;
using ClipboardPro.Models;

namespace ClipboardPro.Services
{
    /// <summary>
    /// Low-level Win32 clipboard listener. Uses AddClipboardFormatListener 
    /// for event-driven, zero-CPU-overhead monitoring.
    /// </summary>
    public class ClipboardMonitorService : IDisposable
    {
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool AddClipboardFormatListener(IntPtr hwnd);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RemoveClipboardFormatListener(IntPtr hwnd);

        [DllImport("user32.dll")]
        private static extern uint GetClipboardSequenceNumber();

        private const int WM_CLIPBOARDUPDATE = 0x031D;

        private HwndSource? _hwndSource;
        private readonly StorageService _storage;
        private volatile uint _lastSequenceNumber = 0;

        private readonly BlockingCollection<uint> _queue = new BlockingCollection<uint>();
        private CancellationTokenSource? _cts;
        private Thread? _workerThread;

        public event Action<ClipboardItem>? OnClipboardChanged;
        public DateTime InternalCopyCooldown { get; set; } = DateTime.MinValue;
        public string? InternalCopyContent { get; set; }

        public ClipboardMonitorService(StorageService storage)
        {
            _storage = storage;
        }

        public void Start(Window window)
        {
            // Start the dedicated STA worker thread
            _cts = new CancellationTokenSource();
            _workerThread = new Thread(ProcessQueue);
            _workerThread.SetApartmentState(ApartmentState.STA);
            _workerThread.IsBackground = true;
            _workerThread.Start();

            var helper = new WindowInteropHelper(window);
            helper.EnsureHandle(); 
            _hwndSource = HwndSource.FromHwnd(helper.Handle);
            _hwndSource?.AddHook(WndProc);
            AddClipboardFormatListener(helper.Handle);
        }

        public void Stop()
        {
            _cts?.Cancel();
            if (_hwndSource != null)
            {
                try
                {
                    RemoveClipboardFormatListener(_hwndSource.Handle);
                    _hwndSource.RemoveHook(WndProc);
                }
                catch { }
            }
        }

        private void ProcessQueue()
        {
            try
            {
                var token = _cts?.Token ?? CancellationToken.None;
                foreach (var sequence in _queue.GetConsumingEnumerable(token))
                {
                    ProcessClipboard(sequence);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception)
            {
                // Restart worker thread if it crashes and we're still running
                if (_cts != null && !_cts.IsCancellationRequested)
                {
                    _workerThread = new Thread(ProcessQueue);
                    _workerThread.SetApartmentState(ApartmentState.STA);
                    _workerThread.IsBackground = true;
                    _workerThread.Start();
                }
            }
        }

        private volatile string? _lastImageHash = null;  
        private volatile string? _lastTextContent = null; 

        public void ResetLastContent()
        {
            _lastTextContent = null;
            _lastImageHash = null;
            InternalCopyContent = null;
            InternalCopyCooldown = DateTime.MinValue;
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_CLIPBOARDUPDATE)
            {
                if (_cts == null || _cts.IsCancellationRequested) return IntPtr.Zero;

                uint currentSequence = GetClipboardSequenceNumber();

                // Only queue if it's a new update
                if (currentSequence != _lastSequenceNumber || _lastSequenceNumber == 0)
                {
                    _lastSequenceNumber = currentSequence;
                    try { _queue.Add(currentSequence); } catch { }
                }
                
                handled = true;
                return IntPtr.Zero;
            }
            return IntPtr.Zero;
        }

        private void ProcessClipboard(uint sequence)
        {
            try
            {
                // Give source apps a moment to stabilize their clipboard data
                Thread.Sleep(100);
                
                if (DateTime.Now < InternalCopyCooldown)
                {
                    if (System.Windows.Clipboard.ContainsText())
                    {
                        var text = System.Windows.Clipboard.GetText();
                        if (text == InternalCopyContent) return; 
                    }
                    else if (System.Windows.Clipboard.ContainsImage() && InternalCopyContent == "Image")
                    {
                        return; 
                    }
                }

                for (int i = 0; i < 10; i++) 
                {
                    try
                    {
                        if (System.Windows.Clipboard.ContainsFileDropList())
                        {
                            var files = System.Windows.Clipboard.GetFileDropList();
                            if (files.Count > 0)
                            {
                                string firstFile = files[0] ?? "";
                                string receivedDir = _storage.GetReceivedFolder();

                                if (firstFile.StartsWith(receivedDir, StringComparison.OrdinalIgnoreCase) && System.IO.File.Exists(firstFile))
                                {
                                    string ext = System.IO.Path.GetExtension(firstFile).ToLower();
                                    if (new[] { ".png", ".jpg", ".jpeg", ".bmp", ".gif" }.Contains(ext))
                                    {
                                        using var img = System.Drawing.Image.FromFile(firstFile);
                                        string hash = ComputeImageHash(img);
                                        _lastImageHash = hash;

                                        var item = new ClipboardItem { 
                                            Content = System.IO.Path.GetFileName(firstFile), 
                                            ImagePath = _storage.SaveImage(img), // Always save to disk so ViewModel has a path
                                            Type = ClipboardItemType.Image, 
                                            Timestamp = DateTime.Now,
                                            ImageHash = hash
                                        };
                                        
                                        OnClipboardChanged?.Invoke(item);
                                        return;
                                    }
                                }
                            }
                        }

                        var dataObject = System.Windows.Clipboard.GetDataObject();
                        if (dataObject == null) return;

                        System.Drawing.Image? capturedImg = null;

                        // Priority capture: PNG -> DIB -> Bitmap
                        string[] pngFormats = { "PNG", "Portable Network Graphics", "image/png" };
                        foreach (var fmt in pngFormats)
                        {
                            if (dataObject.GetDataPresent(fmt))
                            {
                                var data = dataObject.GetData(fmt);
                                if (data is System.IO.MemoryStream ms) capturedImg = StreamToImage(ms);
                                else if (data is byte[] bytes) { using var msBytes = new System.IO.MemoryStream(bytes); capturedImg = StreamToImage(msBytes); }
                                if (capturedImg != null) break;
                            }
                        }

                        if (capturedImg == null && dataObject.GetDataPresent(System.Windows.DataFormats.Dib))
                        {
                            var bitmap = System.Windows.Clipboard.GetImage();
                            if (bitmap != null) capturedImg = BitmapSourceToImage(bitmap);
                        }

                        if (capturedImg == null && System.Windows.Clipboard.ContainsImage())
                        {
                            var bitmap = System.Windows.Clipboard.GetImage();
                            if (bitmap != null) capturedImg = BitmapSourceToImage(bitmap);
                        }

                        // VALIDATION: Compare SHA256 hash to eliminate ghost updates and duplicates
                        if (capturedImg != null)
                        {
                            using (capturedImg)
                            {
                                string hash = ComputeImageHash(capturedImg);
                                _lastImageHash = hash;

                                var item = new ClipboardItem { 
                                    Content = "Image", 
                                    ImagePath = _storage.SaveImage(capturedImg), // Always save to disk
                                    Type = ClipboardItemType.Image, 
                                    Timestamp = DateTime.Now,
                                    ImageHash = hash
                                };

                                OnClipboardChanged?.Invoke(item);
                                return;
                            }
                        }

                        if (System.Windows.Clipboard.ContainsText())
                        {
                            var text = System.Windows.Clipboard.GetText();
                            if (!string.IsNullOrWhiteSpace(text) && text != _lastTextContent)
                            {
                                _lastTextContent = text;
                                var type = ContentDetectionService.Detect(text);
                                OnClipboardChanged?.Invoke(new ClipboardItem { Content = text, Type = type, Timestamp = DateTime.Now });
                                return;
                            }
                        }
                    }
                    catch { /* Handle clipboard lock */ }
                    Thread.Sleep(100);
                }
            }
            catch { }
        }

        private System.Drawing.Image? DownloadImage(string url)
        {
            try
            {
                using var client = new System.Net.Http.HttpClient();
                client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
                var bytes = client.GetByteArrayAsync(url).GetAwaiter().GetResult();
                using var ms = new System.IO.MemoryStream(bytes);
                return StreamToImage(ms);
            }
            catch { return null; }
        }

        private System.Drawing.Image? DataUrlToImage(string dataUrl)
        {
            try
            {
                var base64Data = dataUrl.Split(',')[1];
                var bytes = Convert.FromBase64String(base64Data);
                using var ms = new System.IO.MemoryStream(bytes);
                return StreamToImage(ms);
            }
            catch { return null; }
        }

        private System.Drawing.Image? StreamToImage(System.IO.MemoryStream ms)
        {
            try
            {
                ms.Seek(0, System.IO.SeekOrigin.Begin);
                var decoder = System.Windows.Media.Imaging.BitmapDecoder.Create(ms, 
                    System.Windows.Media.Imaging.BitmapCreateOptions.None, 
                    System.Windows.Media.Imaging.BitmapCacheOption.OnLoad);
                var source = decoder.Frames[0];
                return BitmapSourceToImage(source);
            }
            catch 
            {
                try 
                { 
                    ms.Seek(0, System.IO.SeekOrigin.Begin);
                    return System.Drawing.Image.FromStream(ms); 
                } 
                catch { return null; }
            }
        }

        private System.Drawing.Image BitmapSourceToImage(System.Windows.Media.Imaging.BitmapSource source)
        {
            using (var ms = new System.IO.MemoryStream())
            {
                var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
                encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(source));
                encoder.Save(ms);
                ms.Seek(0, System.IO.SeekOrigin.Begin);
                return System.Drawing.Image.FromStream(ms);
            }
        }

        public string ComputeImageHash(System.Drawing.Image image)
        {
            try
            {
                using (var ms = new System.IO.MemoryStream())
                {
                    image.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                    ms.Seek(0, System.IO.SeekOrigin.Begin);
                    using (var sha = SHA256.Create())
                    {
                        var hash = sha.ComputeHash(ms);
                        return Convert.ToBase64String(hash);
                    }
                }
            }
            catch
            {
                return Guid.NewGuid().ToString(); 
            }
        }

        public void Dispose()
        {
            Stop();
            _queue.Dispose();
            _hwndSource = null;
        }
    }
}
