using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AndroidSideloader.Utilities
{
    // Provides DNS fallback functionality using Cloudflare DNS (1.1.1.1, 1.0.0.1) if system DNS fails to resolve critical hostnames
    // Also provides a proxy for rclone that handles DNS resolution
    public static class DnsHelper
    {
        private static readonly string[] FallbackDnsServers = { "1.1.1.1", "1.0.0.1" };
        private static readonly string[] CriticalHostnames =
        {
            "raw.githubusercontent.com",
            "downloads.rclone.org",
            "vrpirates.wiki",
            "go.vrpyourself.online",
            "github.com"
        };

        private static readonly ConcurrentDictionary<string, IPAddress> _dnsCache =
            new ConcurrentDictionary<string, IPAddress>(StringComparer.OrdinalIgnoreCase);

        private static bool _initialized;
        private static bool _useFallbackDns;
        private static readonly object _lock = new object();

        // Local proxy for rclone
        private static TcpListener _proxyListener;
        private static CancellationTokenSource _proxyCts;
        private static int _proxyPort;
        private static bool _proxyRunning;

        public static bool UseFallbackDns
        {
            get { if (!_initialized) Initialize(); return _useFallbackDns; }
        }

        // Gets the proxy URL for rclone to use, or empty string if not needed
        public static string ProxyUrl => _proxyRunning ? $"http://127.0.0.1:{_proxyPort}" : string.Empty;

        public static void Initialize()
        {
            lock (_lock)
            {
                if (_initialized) return;
                Logger.Log("Testing DNS resolution for critical hostnames...");

                if (!TestSystemDns())
                {
                    Logger.Log("System DNS failed. Testing Cloudflare DNS fallback...", LogLevel.WARNING);
                    if (TestFallbackDns())
                    {
                        _useFallbackDns = true;
                        Logger.Log("Using Cloudflare DNS fallback.", LogLevel.INFO);
                        PreResolveHostnames();
                        ServicePointManager.DnsRefreshTimeout = 0;

                        // Start local proxy for rclone
                        StartProxy();
                    }
                    else
                    {
                        Logger.Log("Both system and fallback DNS failed.", LogLevel.ERROR);
                    }
                }
                else
                {
                    Logger.Log("System DNS is working correctly.");
                }
                _initialized = true;
            }
        }

        // Cleans up resources. Called on application exit
        public static void Cleanup()
        {
            StopProxy();
        }

        private static void PreResolveHostnames()
        {
            foreach (string hostname in CriticalHostnames)
            {
                try
                {
                    var ip = ResolveWithFallbackDns(hostname);
                    if (ip != null)
                    {
                        _dnsCache[hostname] = ip;
                        Logger.Log($"Pre-resolved {hostname} -> {ip}");
                    }
                }
                catch (Exception ex)
                {
                    Logger.Log($"Failed to pre-resolve {hostname}: {ex.Message}", LogLevel.WARNING);
                }
            }
        }

        private static bool TestSystemDns()
        {
            foreach (string hostname in CriticalHostnames)
            {
                try
                {
                    var addresses = Dns.GetHostAddresses(hostname);
                    if (addresses == null || addresses.Length == 0) return false;
                }
                catch { return false; }
            }
            return true;
        }

        private static bool TestFallbackDns()
        {
            foreach (string dnsServer in FallbackDnsServers)
            {
                try
                {
                    var addresses = ResolveWithDns(CriticalHostnames[0], dnsServer);
                    if (addresses != null && addresses.Count > 0) return true;
                }
                catch { }
            }
            return false;
        }

        private static IPAddress ResolveWithFallbackDns(string hostname)
        {
            foreach (string dnsServer in FallbackDnsServers)
            {
                try
                {
                    var addresses = ResolveWithDns(hostname, dnsServer);
                    if (addresses != null && addresses.Count > 0)
                        return addresses[0];
                }
                catch { }
            }
            return null;
        }

        private static List<IPAddress> ResolveWithDns(string hostname, string dnsServer, int timeoutMs = 5000)
        {
            byte[] query = BuildDnsQuery(hostname);
            using (var udp = new UdpClient())
            {
                udp.Client.ReceiveTimeout = timeoutMs;
                udp.Client.SendTimeout = timeoutMs;
                udp.Send(query, query.Length, new IPEndPoint(IPAddress.Parse(dnsServer), 53));
                IPEndPoint remoteEp = null;
                byte[] response = udp.Receive(ref remoteEp);
                return ParseDnsResponse(response);
            }
        }

        private static byte[] BuildDnsQuery(string hostname)
        {
            var ms = new MemoryStream();
            var writer = new BinaryWriter(ms);
            writer.Write(IPAddress.HostToNetworkOrder((short)new Random().Next(0, ushort.MaxValue)));
            writer.Write(IPAddress.HostToNetworkOrder((short)0x0100));
            writer.Write(IPAddress.HostToNetworkOrder((short)1));
            writer.Write(IPAddress.HostToNetworkOrder((short)0));
            writer.Write(IPAddress.HostToNetworkOrder((short)0));
            writer.Write(IPAddress.HostToNetworkOrder((short)0));
            foreach (string label in hostname.Split('.'))
            {
                writer.Write((byte)label.Length);
                writer.Write(Encoding.ASCII.GetBytes(label));
            }
            writer.Write((byte)0);
            writer.Write(IPAddress.HostToNetworkOrder((short)1));
            writer.Write(IPAddress.HostToNetworkOrder((short)1));
            return ms.ToArray();
        }

        private static List<IPAddress> ParseDnsResponse(byte[] response)
        {
            var addresses = new List<IPAddress>();
            if (response.Length < 12) return addresses;
            int pos = 12;
            while (pos < response.Length && response[pos] != 0) pos += response[pos] + 1;
            pos += 5;
            int answerCount = (response[6] << 8) | response[7];
            for (int i = 0; i < answerCount && pos + 12 <= response.Length; i++)
            {
                if ((response[pos] & 0xC0) == 0xC0) pos += 2;
                else { while (pos < response.Length && response[pos] != 0) pos += response[pos] + 1; pos++; }
                if (pos + 10 > response.Length) break;
                ushort type = (ushort)((response[pos] << 8) | response[pos + 1]);
                pos += 8;
                ushort rdLength = (ushort)((response[pos] << 8) | response[pos + 1]);
                pos += 2;
                if (pos + rdLength > response.Length) break;
                if (type == 1 && rdLength == 4)
                    addresses.Add(new IPAddress(new[] { response[pos], response[pos + 1], response[pos + 2], response[pos + 3] }));
                pos += rdLength;
            }
            return addresses;
        }

        #region Local HTTP CONNECT Proxy for rclone

        private static void StartProxy()
        {
            try
            {
                // Find an available port
                _proxyListener = new TcpListener(IPAddress.Loopback, 0);
                _proxyListener.Start();
                _proxyPort = ((IPEndPoint)_proxyListener.LocalEndpoint).Port;
                _proxyCts = new CancellationTokenSource();
                _proxyRunning = true;

                Logger.Log($"Started DNS proxy on port {_proxyPort}");

                // Accept connections in background
                Task.Run(() => ProxyAcceptLoop(_proxyCts.Token));
            }
            catch (Exception ex)
            {
                Logger.Log($"Failed to start DNS proxy: {ex.Message}", LogLevel.WARNING);
                _proxyRunning = false;
            }
        }

        private static void StopProxy()
        {
            _proxyRunning = false;
            _proxyCts?.Cancel();
            try { _proxyListener?.Stop(); } catch { }
        }

        private static async Task ProxyAcceptLoop(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested && _proxyRunning)
            {
                try
                {
                    var client = await _proxyListener.AcceptTcpClientAsync();
                    _ = Task.Run(() => HandleProxyClient(client, ct));
                }
                catch (ObjectDisposedException) { break; }
                catch (Exception ex)
                {
                    if (!ct.IsCancellationRequested)
                        Logger.Log($"Proxy accept error: {ex.Message}", LogLevel.WARNING);
                }
            }
        }

        private static async Task HandleProxyClient(TcpClient client, CancellationToken ct)
        {
            try
            {
                using (client)
                using (var stream = client.GetStream())
                {
                    client.ReceiveTimeout = 30000;
                    client.SendTimeout = 30000;

                    // Read the HTTP request
                    var buffer = new byte[8192];
                    int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, ct);
                    if (bytesRead == 0) return;

                    string request = Encoding.ASCII.GetString(buffer, 0, bytesRead);
                    string[] lines = request.Split(new[] { "\r\n" }, StringSplitOptions.None);
                    if (lines.Length == 0) return;

                    string[] requestLine = lines[0].Split(' ');
                    if (requestLine.Length < 2) return;

                    string method = requestLine[0];
                    string target = requestLine[1];

                    if (method == "CONNECT")
                    {
                        // HTTPS proxy - tunnel mode
                        await HandleConnectRequest(stream, target, ct);
                    }
                    else
                    {
                        // HTTP proxy - forward mode
                        await HandleHttpRequest(stream, request, target, ct);
                    }
                }
            }
            catch (Exception ex)
            {
                if (!ct.IsCancellationRequested)
                    Logger.Log($"Proxy client error: {ex.Message}", LogLevel.WARNING);
            }
        }

        private static async Task HandleConnectRequest(NetworkStream clientStream, string target, CancellationToken ct)
        {
            // Parse host:port
            string[] parts = target.Split(':');
            string host = parts[0];
            int port = parts.Length > 1 ? int.Parse(parts[1]) : 443;

            // Resolve hostname using our DNS
            IPAddress ip = ResolveAnyHostname(host);
            if (ip == null)
            {
                byte[] errorResponse = Encoding.ASCII.GetBytes("HTTP/1.1 502 Bad Gateway\r\n\r\n");
                await clientStream.WriteAsync(errorResponse, 0, errorResponse.Length, ct);
                return;
            }

            try
            {
                // Connect to target
                using (var targetClient = new TcpClient())
                {
                    await targetClient.ConnectAsync(ip, port);
                    using (var targetStream = targetClient.GetStream())
                    {
                        // Send 200 OK to client
                        byte[] okResponse = Encoding.ASCII.GetBytes("HTTP/1.1 200 Connection Established\r\n\r\n");
                        await clientStream.WriteAsync(okResponse, 0, okResponse.Length, ct);

                        // Tunnel data bidirectionally
                        var clientToTarget = RelayData(clientStream, targetStream, ct);
                        var targetToClient = RelayData(targetStream, clientStream, ct);
                        await Task.WhenAny(clientToTarget, targetToClient);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"CONNECT tunnel error to {host}: {ex.Message}", LogLevel.WARNING);
                byte[] errorResponse = Encoding.ASCII.GetBytes("HTTP/1.1 502 Bad Gateway\r\n\r\n");
                try { await clientStream.WriteAsync(errorResponse, 0, errorResponse.Length, ct); } catch { }
            }
        }

        private static async Task HandleHttpRequest(NetworkStream clientStream, string request, string url, CancellationToken ct)
        {
            try
            {
                var uri = new Uri(url);
                IPAddress ip = ResolveAnyHostname(uri.Host);
                if (ip == null)
                {
                    byte[] errorResponse = Encoding.ASCII.GetBytes("HTTP/1.1 502 Bad Gateway\r\n\r\n");
                    await clientStream.WriteAsync(errorResponse, 0, errorResponse.Length, ct);
                    return;
                }

                int port = uri.Port > 0 ? uri.Port : 80;

                using (var targetClient = new TcpClient())
                {
                    await targetClient.ConnectAsync(ip, port);
                    using (var targetStream = targetClient.GetStream())
                    {
                        // Modify request to use relative path
                        string modifiedRequest = request.Replace(url, uri.PathAndQuery);
                        byte[] requestBytes = Encoding.ASCII.GetBytes(modifiedRequest);
                        await targetStream.WriteAsync(requestBytes, 0, requestBytes.Length, ct);

                        // Relay response
                        await RelayData(targetStream, clientStream, ct);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"HTTP proxy error: {ex.Message}", LogLevel.WARNING);
            }
        }

        private static async Task RelayData(NetworkStream from, NetworkStream to, CancellationToken ct)
        {
            byte[] buffer = new byte[8192];
            try
            {
                int bytesRead;
                while ((bytesRead = await from.ReadAsync(buffer, 0, buffer.Length, ct)) > 0)
                {
                    await to.WriteAsync(buffer, 0, bytesRead, ct);
                }
            }
            catch { }
        }

        #endregion

        public static IPAddress ResolveHostname(string hostname)
        {
            if (_dnsCache.TryGetValue(hostname, out IPAddress cached))
                return cached;

            try
            {
                var addresses = Dns.GetHostAddresses(hostname);
                if (addresses != null && addresses.Length > 0)
                {
                    _dnsCache[hostname] = addresses[0];
                    return addresses[0];
                }
            }
            catch { }

            if (_useFallbackDns || !_initialized)
            {
                var ip = ResolveWithFallbackDns(hostname);
                if (ip != null)
                {
                    _dnsCache[hostname] = ip;
                    return ip;
                }
            }

            return null;
        }

        public static IPAddress ResolveAnyHostname(string hostname)
        {
            if (_dnsCache.TryGetValue(hostname, out IPAddress cached))
                return cached;

            try
            {
                var addresses = Dns.GetHostAddresses(hostname);
                if (addresses != null && addresses.Length > 0)
                {
                    _dnsCache[hostname] = addresses[0];
                    return addresses[0];
                }
            }
            catch { }

            var ip = ResolveWithFallbackDns(hostname);
            if (ip != null)
            {
                _dnsCache[hostname] = ip;
                return ip;
            }

            return null;
        }

        public static HttpWebRequest CreateWebRequest(string url)
        {
            var uri = new Uri(url);

            if (!_useFallbackDns)
            {
                try
                {
                    Dns.GetHostAddresses(uri.Host);
                    return (HttpWebRequest)WebRequest.Create(url);
                }
                catch
                {
                    if (!_initialized) Initialize();
                }
            }

            if (_useFallbackDns)
            {
                var ip = ResolveHostname(uri.Host);
                if (ip == null)
                {
                    ip = ResolveAnyHostname(uri.Host);
                }

                if (ip != null)
                {
                    var builder = new UriBuilder(uri) { Host = ip.ToString() };
                    var request = (HttpWebRequest)WebRequest.Create(builder.Uri);
                    request.Host = uri.Host;
                    return request;
                }
            }

            return (HttpWebRequest)WebRequest.Create(url);
        }
    }
}