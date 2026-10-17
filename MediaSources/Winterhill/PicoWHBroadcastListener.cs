using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Serilog;

namespace opentuner.MediaSources.WinterHill
{
    // One shared listener for the hardware monitor and the optional broadcast window.
    // Never allocate two sockets on PicoTuner discovery port 9997.
    public class PicoWHBroadcastListener
    {
        private static readonly object Sync = new object();
        private static UdpClient socket;
        private static Thread worker;
        private static event Action<string> BroadcastReceived;
        private static long lastSeenTicks;
        private static string lastSeenAddress = "";

        public delegate void OnBroadcastDelegate(string data);
        public event OnBroadcastDelegate OnBroadcast;

        public static void EnsureStarted()
        {
            lock (Sync)
            {
                if (socket != null) return;
                socket = new UdpClient(9997);
                socket.Client.ReceiveTimeout = 1000;
                worker = new Thread(Listen) { IsBackground = true, Name = "PicoTuner discovery" };
                worker.Start();
            }
        }

        public static bool RecentlySeen(string expectedAddress, int maxAgeSeconds)
        {
            long ticks = Interlocked.Read(ref lastSeenTicks);
            if (ticks == 0 || DateTime.UtcNow.Ticks - ticks > TimeSpan.FromSeconds(maxAgeSeconds).Ticks)
                return false;
            lock (Sync)
                return string.IsNullOrWhiteSpace(expectedAddress) ||
                    string.Equals(lastSeenAddress, expectedAddress, StringComparison.OrdinalIgnoreCase);
        }

        public static bool SeenAfter(string expectedAddress, DateTime thresholdUtc)
        {
            long ticks = Interlocked.Read(ref lastSeenTicks);
            return ticks > thresholdUtc.Ticks &&
                RecentlySeen(expectedAddress, 6);
        }

        public static string LastAddress { get { lock (Sync) return lastSeenAddress; } }

        private static void Listen()
        {
            while (true)
            {
                try
                {
                    IPEndPoint remote = new IPEndPoint(IPAddress.Any, 0);
                    byte[] packet = socket.Receive(ref remote);
                    if (packet == null || packet.Length == 0 || remote == null) continue;
                    string data = Encoding.ASCII.GetString(packet);
                    // Prefer a recognisable PicoTuner broadcast over arbitrary LAN UDP.
                    if (data.IndexOf("IP address", StringComparison.OrdinalIgnoreCase) < 0 &&
                        data.IndexOf("Base IP port", StringComparison.OrdinalIgnoreCase) < 0) continue;
                    lock (Sync) lastSeenAddress = remote.Address.ToString();
                    Interlocked.Exchange(ref lastSeenTicks, DateTime.UtcNow.Ticks);
                    Action<string> handler = BroadcastReceived;
                    if (handler != null) handler(data);
                }
                catch (SocketException ex)
                {
                    if (ex.SocketErrorCode != SocketError.TimedOut)
                        Log.Warning(ex, "PicoTuner broadcast receive failed");
                }
                catch (Exception ex) { Log.Warning(ex, "PicoTuner broadcast handler failed"); }
            }
        }

        public PicoWHBroadcastListener()
        {
            EnsureStarted();
            BroadcastReceived += Forward;
        }

        private void Forward(string data)
        {
            OnBroadcastDelegate handler = OnBroadcast;
            if (handler != null) handler(data);
        }

        public void Close()
        {
            BroadcastReceived -= Forward;
        }
    }
}
