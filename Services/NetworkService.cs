using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Net.NetworkInformation;
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
                        try
                        {
                            var result = await _udpDiscovery.ReceiveAsync();
                            try
                            {
                                var message = Encoding.UTF8.GetString(result.Buffer);
                                if (message.StartsWith("CLIPPRO_DISCOVER:"))
                                {
                                    var parts = message.Substring("CLIPPRO_DISCOVER:".Length).Split('|');
                                    if (parts.Length < 3) continue;

                                    var peerName = parts[0];
                                    if (!int.TryParse(parts[1], out int peerPort)) continue;
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
                            catch { }
                        }
                        catch (Exception ex)
                        {
                            if (_cts.Token.IsCancellationRequested) break;
                            await Task.Delay(1000);
                        }
                    }
                }
                catch { }
            });
        }

        private static IEnumerable<IPAddress> GetBroadcastAddresses()
        {
            var addresses = new List<IPAddress>();
            try
            {
                foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus != OperationalStatus.Up) continue;
                    if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;

                    var ipProps = ni.GetIPProperties();
                    foreach (var unicast in ipProps.UnicastAddresses)
                    {
                        if (unicast.Address.AddressFamily == AddressFamily.InterNetwork)
                        {
                            var ip = unicast.Address;
                            var mask = unicast.IPv4Mask;
                            if (mask != null)
                            {
                                byte[] ipBytes = ip.GetAddressBytes();
                                byte[] maskBytes = mask.GetAddressBytes();
                                byte[] broadcastBytes = new byte[ipBytes.Length];
                                for (int i = 0; i < ipBytes.Length; i++)
                                {
                                    broadcastBytes[i] = (byte)(ipBytes[i] | ~maskBytes[i]);
                                }
                                addresses.Add(new IPAddress(broadcastBytes));
                            }
                        }
                    }
                }
            }
            catch { }
            
            if (!addresses.Contains(IPAddress.Broadcast)) addresses.Add(IPAddress.Broadcast);
            return addresses.Distinct();
        }

        private void StartBroadcasting()
        {
            Task.Run(async () =>
            {
                using var udp = new UdpClient();
                udp.EnableBroadcast = true;
                
                var loopbackEndpoint = new IPEndPoint(IPAddress.Loopback, DiscoveryPort);

                while (!_cts.Token.IsCancellationRequested)
                {
                    try
                    {
                        if (_actualTransferPort > 0)
                        {
                            var data = Encoding.UTF8.GetBytes($"CLIPPRO_DISCOVER:{_deviceName}|{_actualTransferPort}|{_instanceId}");
                            
                            // Broadcast to all active subnet broadcast endpoints
                            foreach (var bcastIp in GetBroadcastAddresses())
                            {
                                try
                                {
                                    await udp.SendAsync(data, data.Length, new IPEndPoint(bcastIp, DiscoveryPort));
                                }
                                catch { }
                            }
                            
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
                        if (jsonLen <= 0 || jsonLen > 100 * 1024 * 1024) continue; // 100MB limit for JSON metadata safety

                        var jsonBytes = reader.ReadBytes(jsonLen);
                        var json = Encoding.UTF8.GetString(jsonBytes);
                        var item = JsonConvert.DeserializeObject<ClipboardItem>(json);

                        if (item != null)
                        {
                            if (item.Type == ClipboardItemType.Image || item.Type == ClipboardItemType.Path)
                            {
                                var payloadLen = reader.ReadInt64(); // Using long for large files
                                if (payloadLen < 0) continue; // Safety validation
                                
                                string folder = item.Type == ClipboardItemType.Image ? "Images" : "Received";
                                var appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ClipboardPro", folder);
                                if (!Directory.Exists(appData)) Directory.CreateDirectory(appData);

                                var fileName = item.Type == ClipboardItemType.Image ? $"sync_{Guid.NewGuid()}.png" : item.Content;
                                if (item.Type == ClipboardItemType.Path) fileName = Path.GetFileName(item.Content);
                                
                                // Robust security sanitization: enforce filename only and sanitize illegal chars
                                fileName = Path.GetFileName(fileName);
                                fileName = fileName.Replace("..", "").Replace("/", "").Replace("\\", "");
                                foreach (char c in Path.GetInvalidFileNameChars())
                                {
                                    fileName = fileName.Replace(c, '_');
                                }
                                if (string.IsNullOrWhiteSpace(fileName)) fileName = $"sync_{Guid.NewGuid()}.dat";
                                
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
                using (ct.Register(() => { try { client.Close(); } catch { } }))
                {
                    var connectTask = client.ConnectAsync(targetIp, targetPort);
                    if (await Task.WhenAny(connectTask, Task.Delay(5000)) != connectTask)
                    {
                        try { client.Close(); } catch { }
                        return false;
                    }
                    if (!client.Connected) return false;
                }
                
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
