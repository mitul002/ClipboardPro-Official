using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Linq;
using Newtonsoft.Json;
using ClipboardPro.Models;
using System.IO;

namespace ClipboardPro.Services
{
    public class PeerInfo
    {
        public string Name { get; set; } = string.Empty;
        public string IP { get; set; } = string.Empty;
        public int Port { get; set; }
        public DateTime LastSeen { get; set; }
    }

    public class NetworkService : IDisposable
    {
        private const int DiscoveryPort = 50505;
        private const int TransferPortBase = 50506;
        private int _actualTransferPort;
        private readonly string _instanceId;
        private readonly string _deviceName;
        private bool _isRunning;
        
        private UdpClient? _udpDiscovery;
        private TcpListener? _tcpListener;
        
        public event Action<ClipboardItem>? OnItemReceived;
        public event Action<List<PeerInfo>>? OnPeersUpdated;
        
        private readonly Dictionary<string, PeerInfo> _discoveredPeers = new();
        private readonly System.Threading.CancellationTokenSource _cts = new();

        public NetworkService()
        {
            _deviceName = Environment.MachineName;
            _instanceId = Guid.NewGuid().ToString().Substring(0, 8);
        }

        public void Start()
        {
            if (_isRunning) return;
            _isRunning = true;

            StartListener(); 
            StartDiscovery();
            StartBroadcasting();
        }

        private void StartDiscovery()
        {
            Task.Run(async () =>
            {
                try
                {
                    _udpDiscovery = new UdpClient();
                    _udpDiscovery.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                    _udpDiscovery.ExclusiveAddressUse = false;
                    _udpDiscovery.Client.Bind(new IPEndPoint(IPAddress.Any, DiscoveryPort));

                    while (!_cts.Token.IsCancellationRequested)
                    {
                        var result = await _udpDiscovery.ReceiveAsync();
                        var message = Encoding.UTF8.GetString(result.Buffer);
                        if (message.StartsWith("CLIPPRO_DISCOVER:"))
                        {
                            var parts = message.Substring("CLIPPRO_DISCOVER:".Length).Split('|');
                            if (parts.Length < 3) continue;

                            var peerName = parts[0];
                            var peerPort = int.Parse(parts[1]);
                            var peerId   = parts[2];
                            var peerIp   = result.RemoteEndPoint.Address.ToString();

                            var key = $"{peerIp}:{peerPort}";
                            
                            if (peerId != _instanceId)
                            {
                                lock (_discoveredPeers)
                                {
                                    _discoveredPeers[key] = new PeerInfo 
                                    { 
                                        Name = peerName + (peerIp == "127.0.0.1" ? " (Local)" : ""), 
                                        IP = peerIp, 
                                        Port = peerPort,
                                        LastSeen = DateTime.Now 
                                    };
                                }
                                NotifyPeersUpdated();
                            }
                        }
                    }
                }
                catch { }
            });
        }

        private void StartBroadcasting()
        {
            Task.Run(async () =>
            {
                using var udp = new UdpClient();
                udp.EnableBroadcast = true;
                
                var endpoint = new IPEndPoint(IPAddress.Broadcast, DiscoveryPort);
                var loopbackEndpoint = new IPEndPoint(IPAddress.Loopback, DiscoveryPort);

                while (!_cts.Token.IsCancellationRequested)
                {
                    try
                    {
                        if (_actualTransferPort > 0)
                        {
                            var data = Encoding.UTF8.GetBytes($"CLIPPRO_DISCOVER:{_deviceName}|{_actualTransferPort}|{_instanceId}");
                            await udp.SendAsync(data, data.Length, endpoint);
                            await udp.SendAsync(data, data.Length, loopbackEndpoint);
                        }
                        
                        lock (_discoveredPeers)
                        {
                            var timeout = DateTime.Now.AddSeconds(-15);
                            var toRemove = _discoveredPeers.Where(p => p.Value.LastSeen < timeout).Select(p => p.Key).ToList();
                            foreach (var key in toRemove) _discoveredPeers.Remove(key);
                            if (toRemove.Count > 0) NotifyPeersUpdated();
                        }
                    }
                    catch { }
                    await Task.Delay(3000);
                }
            });
        }

        private void StartListener()
        {
            Task.Run(async () =>
            {
                for (int port = TransferPortBase; port < TransferPortBase + 10; port++)
                {
                    try
                    {
                        var listener = new TcpListener(IPAddress.Any, port);
                        listener.Start();
                        _tcpListener = listener;
                        _actualTransferPort = port;
                        NotifyPeersUpdated();
                        break;
                    }
                    catch { }
                }

                if (_tcpListener == null) return;

                while (!_cts.Token.IsCancellationRequested)
                {
                    try
                    {
                        using var client = await _tcpListener.AcceptTcpClientAsync();
                        using var stream = client.GetStream();
                        using var reader = new BinaryReader(stream);

                        var jsonLen = reader.ReadInt32();
                        var jsonBytes = reader.ReadBytes(jsonLen);
                        var json = Encoding.UTF8.GetString(jsonBytes);
                        var item = JsonConvert.DeserializeObject<ClipboardItem>(json);

                        if (item != null)
                        {
                            if (item.Type == ClipboardItemType.Image || item.Type == ClipboardItemType.Path)
                            {
                                var payloadLen = reader.ReadInt64(); // Using long for large files
                                
                                string folder = item.Type == ClipboardItemType.Image ? "Images" : "Received";
                                var appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ClipboardPro", folder);
                                if (!Directory.Exists(appData)) Directory.CreateDirectory(appData);

                                var fileName = item.Type == ClipboardItemType.Image ? $"sync_{Guid.NewGuid()}.png" : item.Content;
                                if (item.Type == ClipboardItemType.Path) fileName = Path.GetFileName(item.Content);
                                
                                var fullPath = Path.Combine(appData, fileName);
                                
                                // Receive payload in chunks to avoid UI freeze and memory spikes
                                using (var fs = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true))
                                {
                                    byte[] buffer = new byte[81920]; // 80KB buffer
                                    long totalRead = 0;
                                    while (totalRead < payloadLen)
                                    {
                                        int toRead = (int)Math.Min(buffer.Length, payloadLen - totalRead);
                                        int read = await stream.ReadAsync(buffer, 0, toRead);
                                        if (read == 0) break;
                                        await fs.WriteAsync(buffer, 0, read);
                                        totalRead += read;
                                    }
                                }
                                
                                if (item.Type == ClipboardItemType.Image) item.ImagePath = fileName;
                                else item.Content = fullPath;
                            }
                            OnItemReceived?.Invoke(item);
                        }
                    }
                    catch { }
                }
            });
        }

        public async Task<bool> SendItemAsync(ClipboardItem item, string targetIp, int targetPort, System.Threading.CancellationToken ct = default, System.Threading.ManualResetEventSlim? pauseEvent = null)
        {
            item.IsSending = true;
            item.SendingPercentage = 0;
            try
            {
                using var client = new TcpClient();
                var connectTask = client.ConnectAsync(targetIp, targetPort);
                if (await Task.WhenAny(connectTask, Task.Delay(5000)) != connectTask) return false;
                
                using var stream = client.GetStream();
                using var writer = new BinaryWriter(stream);

                item.SendingPercentage = 5;

                // 1. Send JSON
                var originalIsSending = item.IsSending;
                var originalPercentage = item.SendingPercentage;
                item.IsSending = false;
                item.SendingPercentage = 0;
                var json = JsonConvert.SerializeObject(item);
                item.IsSending = originalIsSending;
                item.SendingPercentage = originalPercentage;

                var jsonBytes = Encoding.UTF8.GetBytes(json);
                writer.Write(jsonBytes.Length);
                writer.Write(jsonBytes);
                item.SendingPercentage = 15;

                // 2. Send Binary Payload if applicable
                if ((item.Type == ClipboardItemType.Image && !string.IsNullOrEmpty(item.ImagePath)) ||
                    (item.Type == ClipboardItemType.Path && !string.IsNullOrEmpty(item.Content)))
                {
                    string sourcePath = "";
                    if (item.Type == ClipboardItemType.Image)
                    {
                        var appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ClipboardPro", "Images");
                        sourcePath = Path.IsPathRooted(item.ImagePath!) ? item.ImagePath! : Path.Combine(appData, item.ImagePath!);
                    }
                    else sourcePath = item.Content;
                    
                    if (File.Exists(sourcePath))
                    {
                        using (var fs = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true))
                        {
                            long fileLen = fs.Length;
                            item.TotalBytes = fileLen;
                            writer.Write(fileLen); // Send as long
                            
                            byte[] buffer = new byte[81920]; // 80KB buffer
                            long totalSent = 0;
                            while (totalSent < fileLen)
                            {
                                if (pauseEvent != null && !pauseEvent.IsSet)
                                {
                                    await Task.Run(() => pauseEvent.Wait(ct), ct);
                                }

                                int read = await fs.ReadAsync(buffer, 0, buffer.Length, ct);
                                if (read == 0) break;
                                await stream.WriteAsync(buffer, 0, read, ct);
                                totalSent += read;
                                item.BytesSent = totalSent;
                                item.SendingPercentage = 15 + (totalSent * 80.0 / fileLen);
                            }
                        }
                    }
                    else writer.Write((long)0);
                }

                await stream.FlushAsync();
                item.SendingPercentage = 100;
                await Task.Delay(500);
                return true;
            }
            catch (OperationCanceledException) { throw; }
            catch { return false; }
            finally { item.IsSending = false; }
        }

        public List<PeerInfo> GetActivePeers()
        {
            lock (_discoveredPeers)
            {
                return _discoveredPeers.Values.ToList();
            }
        }

        private void NotifyPeersUpdated()
        {
            OnPeersUpdated?.Invoke(GetActivePeers());
        }

        public void Dispose()
        {
            _isRunning = false;
            _cts.Cancel();
            _udpDiscovery?.Dispose();
            _tcpListener?.Stop();
        }
    }
}
