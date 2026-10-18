using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Serilog;

namespace opentuner.MediaSources.WinterHill
{
    // Single process-wide consumer of PicoTuner UDP 9997 broadcasts.
    // Legacy broadcast windows subscribe to the same feed; no competing socket.
    public class PicoWHBroadcastListener
    {
        private static readonly object Sync = new object();
        private static readonly System.Collections.Generic.Dictionary<string, DateTime> Seen =
            new System.Collections.Generic.Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
        private static UdpClient sharedSocket;
        private static Thread sharedThread;
        private static event Action<string> BroadcastReceived;
        private static string lastLoggedSender;
        private static DateTime lastLoggedAt = DateTime.MinValue;
        private bool closed;

        public delegate void OnBroadcastDelegate(string data);
        public event OnBroadcastDelegate OnBroadcast;

        public PicoWHBroadcastListener()
        {
            StartShared();
            lock (Sync) BroadcastReceived += ForwardBroadcast;
        }

        public static void StartShared()
        {
            lock (Sync)
            {
                if (sharedSocket != null) return;
                try
                {
                    sharedSocket = new UdpClient(9997);
                    sharedSocket.Client.ReceiveTimeout = 1500;
                    sharedThread = new Thread(ReceiveLoop) { IsBackground = true, Name = "PicoTuner UDP 9997" };
                    sharedThread.Start();
                    Log.Information("[PICO-DISCOVERY] Shared UDP 9997 listener started");
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "[PICO-DISCOVERY] Could not open shared UDP 9997 listener");
                    if (sharedSocket != null) { sharedSocket.Close(); sharedSocket = null; }
                }
            }
        }

        public static bool RecentlySeen(string host, int maxAgeSeconds = 8)
        {
            if (string.IsNullOrWhiteSpace(host)) return false;
            lock (Sync)
            {
                DateTime seen;
                return Seen.TryGetValue(host.Trim(), out seen) &&
                    (DateTime.UtcNow - seen).TotalSeconds <= maxAgeSeconds;
            }
        }

        public static bool WaitForBroadcast(string host, int timeoutMs)
        {
            StartShared();
            DateTime until = DateTime.UtcNow.AddMilliseconds(Math.Max(0, timeoutMs));
            while (DateTime.UtcNow < until)
            {
                if (RecentlySeen(host)) return true;
                Thread.Sleep(50);
            }
            return RecentlySeen(host);
        }

        private static void ReceiveLoop()
        {
            while (true)
            {
                try
                {
                    IPEndPoint sender = new IPEndPoint(IPAddress.Any, 0);
                    byte[] data = sharedSocket.Receive(ref sender);
                    if (data == null || data.Length == 0 || sender == null) continue;
                    string host = sender.Address.ToString();
                    string message = Encoding.ASCII.GetString(data);
                    Action<string> listeners;
                    lock (Sync)
                    {
                        Seen[host] = DateTime.UtcNow;
                        if (lastLoggedSender != host || (DateTime.UtcNow - lastLoggedAt).TotalSeconds >= 15)
                        {
                            lastLoggedSender = host;
                            lastLoggedAt = DateTime.UtcNow;
                            Log.Information("[PICO-DISCOVERY] Broadcast from {Host}, bytes={Length}", host, data.Length);
                        }
                        listeners = BroadcastReceived;
                    }
                    if (listeners != null)
                    {
                        foreach (Action<string> subscriber in listeners.GetInvocationList())
                        {
                            try { subscriber(message); } catch (Exception ex) { Log.Debug(ex, "Pico broadcast subscriber error"); }
                        }
                    }
                }
                catch (SocketException ex)
                {
                    if (ex.SocketErrorCode != SocketError.TimedOut)
                        Log.Warning(ex, "[PICO-DISCOVERY] UDP receive failed");
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "[PICO-DISCOVERY] Listener error");
                }
            }
        }

        private void ForwardBroadcast(string text)
        {
            if (!closed) OnBroadcast?.Invoke(text);
        }

        public void Close()
        {
            closed = true;
            lock (Sync) BroadcastReceived -= ForwardBroadcast;
        }

        // Compatibility with callers invoking the old method.
        public void ListenerThread() { StartShared(); }
    }
}
