using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace opentuner.Utilities
{
    public static class CommonFunctions
    {
        public static string GenerateTimestampFilename()
        {
            return DateTime.Now.ToString("yyyy-dd-M--HH-mm-ss");
        }

        public static List<string> determineIP()
        {
            List<IPAddress> detected = new List<IPAddress>();

            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                    detected.Add(ip);
            }

            // Prefer ordinary LAN interfaces. Tailscale commonly uses 100.64.0.0/10
            // and Windows link-local interfaces use 169.254.0.0/16; both are poor
            // defaults for a receiver on the local Ethernet/Wi-Fi LAN.
            return detected
                .OrderBy(ip => GetIPv4Preference(ip))
                .ThenBy(ip => ip.ToString())
                .Select(ip => ip.ToString())
                .ToList();
        }

        private static int GetIPv4Preference(IPAddress ip)
        {
            byte[] b = ip.GetAddressBytes();
            if (b.Length != 4)
                return 99;

            // 192.168.0.0/16
            if (b[0] == 192 && b[1] == 168)
                return 0;

            // 10.0.0.0/8
            if (b[0] == 10)
                return 1;

            // 172.16.0.0/12
            if (b[0] == 172 && b[1] >= 16 && b[1] <= 31)
                return 2;

            // 100.64.0.0/10 (CGNAT / commonly Tailscale)
            if (b[0] == 100 && b[1] >= 64 && b[1] <= 127)
                return 20;

            // 169.254.0.0/16 link-local
            if (b[0] == 169 && b[1] == 254)
                return 30;

            return 10;
        }
    }
}
