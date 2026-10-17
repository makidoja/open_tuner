using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Serilog;

namespace opentuner.MediaSources.WinterHill
{
    // All consumers share one socket; the broadcast monitor and modern UI
    // must never compete for UDP 9997.
    public sealed class PicoWHBroadcastListener
    {
        private static readonly object Gate = new object();
        private static readonly Dictionary<string, DateTime> Seen = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
        private static UdpClient socket;
        private static Thread worker;
        private static int users;
        private bool closed;
        public delegate void OnBroadcastDelegate(string data);
        public event OnBroadcastDelegate OnBroadcast;
        private static event OnBroadcastDelegate SharedBroadcast;

        public PicoWHBroadcastListener()
        {
            lock (Gate)
            {
                if (socket == null)
                {
                    socket = new UdpClient(9997);
                    socket.Client.ReceiveTimeout = 1000;
                    worker = new Thread(Listen) { IsBackground = true, Name = "PicoTuner discovery UDP 9997" };
                    worker.Start();
                }
                users++;
                SharedBroadcast += Forward;
            }
        }

        private void Forward(string message)
        {
            try { OnBroadcast?.Invoke(message); }
            catch (Exception ex) { Log.Warning(ex, "PicoTuner broadcast subscriber error"); }
        }

        public static bool RecentlySeen(string host, int seconds = 5)
        {
            if (string.IsNullOrWhiteSpace(host)) return false;
            lock (Gate)
            {
                DateTime time;
                return Seen.TryGetValue(host.Trim(), out time) && DateTime.UtcNow - time < TimeSpan.FromSeconds(seconds);
            }
        }

        public void Close()
        {
            lock (Gate)
            {
                if (closed) return;
                closed = true;
                SharedBroadcast -= Forward;
                if (--users == 0)
                {
                    UdpClient old = socket;
                    socket = null;
                    Seen.Clear();
                    try { old?.Close(); } catch { }
                }
            }
        }

        private static void Listen()
        {
            while (true)
            {
                UdpClient current;
                lock (Gate) current = socket;
                if (current == null) return;
                try
                {
                    IPEndPoint remote = new IPEndPoint(IPAddress.Any, 0);
                    byte[] bytes = current.Receive(ref remote);
                    if (remote == null || bytes == null || bytes.Length == 0) continue;
                    string message = Encoding.ASCII.GetString(bytes);
                    OnBroadcastDelegate subscribers;
                    lock (Gate)
                    {
                        if (current != socket) return;
                        Seen[remote.Address.ToString()] = DateTime.UtcNow;
                        subscribers = SharedBroadcast;
                    }
                    subscribers?.Invoke(message);
                }
                catch (SocketException ex)
                {
                    if (ex.SocketErrorCode != SocketError.TimedOut)
                    {
                        lock (Gate) { if (current != socket) return; }
                        Log.Warning(ex, "PicoTuner broadcast receive failed");
                    }
                }
                catch (ObjectDisposedException) { return; }
                catch (Exception ex) { Log.Warning(ex, "PicoTuner discovery error"); }
            }
        }
    }
}
