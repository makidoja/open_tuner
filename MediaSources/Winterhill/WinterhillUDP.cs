using System;
using System.Net;
using System.Text;
using System.Net.Sockets;
using Serilog;
using opentuner.Utilities;

namespace opentuner.MediaSources.WinterHill
{
    public partial class WinterHillSource
    {
        UdpClient WH_Client = new UdpClient();

        UDPClient longmynd_status;
        private string runtimeUdpHost = "";

        private string GetRuntimeUdpHost()
        {
            if (!string.IsNullOrWhiteSpace(runtimeUdpHost))
                return runtimeUdpHost;
            return _settings.WinterHillUdpHost;
        }

        public void ConnectWinterHillUDP(int port)
        {
            runtimeUdpHost = _settings.WinterHillUdpHost;

            longmynd_status = new UDPClient(port);

            longmynd_status.DataReceived += Longmynd_status_DataReceived;
            longmynd_status.ConnectionStatusChanged += Longmynd_status_ConnectionStatusChanged;
            longmynd_status.Connect();
        }

        private void Longmynd_status_ConnectionStatusChanged(object sender, bool connection_status)
        {
            if (connection_status)
            {
                Log.Information("UDP Status Port Connected");
            }
            else
                Log.Warning("UDP Status Port Disconnected");
        }

        public void DisconnectWinterHillUDP()
        {
            longmynd_status?.Close();
        }

        private void Longmynd_status_DataReceived(object sender, byte[] e)
        {
            // Learn the actual PicoTuner address from the sender of valid live status
            // packets. This avoids sending tune commands to a stale address or to the
            // Windows PC's own LAN address. Reception can still work in that situation
            // because status/TS arrive inbound, while control commands silently go nowhere.
            UDPClient statusClient = sender as UDPClient;
            if (statusClient != null && !string.IsNullOrWhiteSpace(statusClient.LastRemoteAddress))
            {
                string detectedHost = statusClient.LastRemoteAddress;
                if (!string.Equals(runtimeUdpHost, detectedHost, StringComparison.OrdinalIgnoreCase))
                {
                    Log.Information("PicoTuner UDP control host learned from status packets: " + detectedHost +
                                    " (configured: " + _settings.WinterHillUdpHost + ")");
                    runtimeUdpHost = detectedHost;
                }
            }

            var data = Encoding.ASCII.GetString(e);

            int udp_base_port = _settings.WinterHillUdpBasePort;

            string[] status_strings = data.Split('\n');

            monitorMessage mm = new monitorMessage();
            mm.type = "RX";
            mm.timestamp = 0;
            mm.rx = new ReceiverMessage[3];
            mm.rx[0] = new ReceiverMessage();
            mm.rx[1] = new ReceiverMessage();
            mm.rx[2] = new ReceiverMessage();

            mm.rx[0].rx = 99;
            mm.rx[1].rx = 99;
            mm.rx[2].rx = 99;

            int receiver = 1;

            for (int c = 0; c <  status_strings.Length; c++)
            {
                string[] dt = status_strings[c].Trim().Split(',');
                if (dt.Length == 0)
                    continue;

                switch(dt[0])
                {
                    case "$0":
                        if (dt.Length < 2) break;
                        int receiver_num = udp_base_port % 100;
                        receiver = Convert.ToInt32(dt[1]) - receiver_num;
                        if (receiver < 1 || receiver > 2 )
                            return;
                        break;
                    case "$1":
                        if (dt.Length < 2) break;
                        switch(dt[1].Trim())
                        {
                            case "DVB-S2": mm.rx[receiver].scanstate = 2; break;
                            case "DVB-S1": mm.rx[receiver].scanstate = 3; break;
                            case "header": mm.rx[receiver].scanstate = 1; break;
                            case "search": mm.rx[receiver].scanstate = 0; break;
                            case "lost": mm.rx[receiver].scanstate = 0; break;
                            default:
                                Log.Warning("WH: Don't know how to decode: " + dt[1]);
                                mm.rx[receiver].scanstate = 1;
                                break;
                        }
                        break;
                    case "$6": if (dt.Length > 1) mm.rx[receiver].frequency = dt[1]; break;
                    case "$9": if (dt.Length > 1) mm.rx[receiver].symbol_rate = dt[1]; break;
                    case "$12": if (dt.Length > 1) mm.rx[receiver].mer = dt[1]; break;
                    case "$13": if (dt.Length > 1) mm.rx[receiver].service_name = dt[1]; break;
                    case "$14": if (dt.Length > 1) mm.rx[receiver].service_provider_name = dt[1]; break;
                    case "$15": if (dt.Length > 1) mm.rx[receiver].null_percentage = dt[1]; break;
                    case "$18": if (dt.Length > 1) mm.rx[receiver].modcod = dt[1]; break;
                    case "$30": if (dt.Length > 1) mm.rx[receiver].dbmargin = dt[1]; break;
                    case "$33":
                        if (dt.Length > 1)
                            mm.rx[receiver].rfport = dt[1].Trim() == "TOP" ? 0 : 1;
                        break;
                    case "$92":
                        if (dt.Length > 1)
                        {
                            string[] ts_data = dt[1].Split(':');
                            if (ts_data.Length > 1)
                            {
                                mm.rx[receiver].ts_addr = ts_data[0];
                                mm.rx[receiver].ts_port = ts_data[1];
                            }
                        }
                        break;
                }
            }

            if (mm.rx[receiver].service_name == null) mm.rx[receiver].service_name = "";
            if (mm.rx[receiver].service_provider_name == null) mm.rx[receiver].service_provider_name = "";
            if (mm.rx[receiver].mer == null) mm.rx[receiver].mer = "";
            if (mm.rx[receiver].modcod == null) mm.rx[receiver].modcod = "";
            if (mm.rx[receiver].dbmargin == null) mm.rx[receiver].dbmargin = "";
            if (mm.rx[receiver].ts_port == null) mm.rx[receiver].ts_port = "";
            if (mm.rx[receiver].ts_addr == null) mm.rx[receiver].ts_addr = "";

            mm.rx[receiver].rx = receiver;
            UpdateInfo(mm);
        }

        public void UDPSetVoltage(int plug, uint voltage)
        {
            int base_port = _settings.WinterHillUdpBasePort;
            string controlHost = GetRuntimeUdpHost();
            IPEndPoint WinterHill_end_point = new IPEndPoint(IPAddress.Parse(controlHost), base_port + 21);

            string command2 = "";
            string vg = plug == 0 ? "vgx" : "vgy";
            switch (voltage)
            {
                case 0: command2 =  ("[to@wh] " + vg + "=OFF"); break;
                case 13: command2 = ("[to@wh] " + vg + "=LO"); break;
                case 18: command2 = ("[to@wh] " + vg + "=HI"); break;
            }

            _settings.LNBVoltage[plug] = voltage;
            Log.Information(command2);

            byte[] outStream = Encoding.ASCII.GetBytes(command2);
            try
            {
                WH_Client.Client.SendTo(outStream, WinterHill_end_point);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Error sending PicoTuner UDP voltage command to " + controlHost);
            }
        }

        public void UDPSetFrequency(int device, int freq, int sr)
        {
            int base_port = _settings.WinterHillUdpBasePort + ( device == 0 ? 21 : 22);
            int receiver_num = (_settings.WinterHillUdpBasePort % 100) + 1 + device;
            string controlHost = GetRuntimeUdpHost();
            IPEndPoint WinterHill_end_point = new IPEndPoint(IPAddress.Parse(controlHost), base_port );

            Log.Information("UDP Set Frequency Device: " + device + " : " + controlHost + " : " + base_port.ToString());

            try
            {
                VideoChangeCB?.Invoke(device + 1, false);
                playing[device] = false;
                demodstate[device] = -1;
            }
            catch { }

            string command = "[to@wh] rcv=" + receiver_num.ToString() +
                             ",freq=" + freq.ToString() +
                             ",offset=" + _current_offset[device].ToString() +
                             ",srate=" + sr.ToString() +
                             ",fplug=" + (_settings.RFPort[device] == 0 ? "A" : "B") + "\n";

            Log.Information(command);

            byte[] outStream = Encoding.ASCII.GetBytes(command);
            try
            {
                Log.Information("Setting WH UDP Frequency: " + freq.ToString() + " via " + controlHost + ":" + base_port.ToString());
                WH_Client.Client.SendTo(outStream, WinterHill_end_point);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Error sending PicoTuner UDP tune command to " + controlHost + ":" + base_port.ToString());
            }
        }
    }
}
