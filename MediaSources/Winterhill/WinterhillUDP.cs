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
        private UDPClient[] winterHillStatusClients;
        private string runtimeUdpHost = "";

        private bool DualPicoEnabled
        {
            get { return _settings != null && _settings.DefaultInterface == 2 &&
                _settings.SecondPicoTunerEnabled; }
        }

        private int GetPicoBasePort(int receiver)
        {
            return DualPicoEnabled && receiver >= 2 ?
                _settings.SecondPicoTunerUdpBasePort : _settings.WinterHillUdpBasePort;
        }

        private int GetPicoLocalReceiver(int receiver)
        {
            return DualPicoEnabled ? receiver % 2 : receiver;
        }

        private string GetRuntimeUdpHost(int receiver = 0)
        {
            if (DualPicoEnabled)
                return receiver >= 2 ? _settings.SecondPicoTunerUdpHost : _settings.WinterHillUdpHost;
            if (!string.IsNullOrWhiteSpace(runtimeUdpHost))
                return runtimeUdpHost;
            return _settings.WinterHillUdpHost;
        }

        public void ConnectWinterHillUDP(int port)
        {
            runtimeUdpHost = _settings.WinterHillUdpHost;

            // Each PicoTuner sends two receiver status sections on its own
            // base+1 port. Keep their local IDs separate, then map into
            // global RX A/B (unit 1) and C/D (unit 2).
            if (DualPicoEnabled)
            {
                winterHillStatusClients = new UDPClient[2];
                int[] statusPorts = new int[]
                {
                    _settings.WinterHillUdpBasePort + 1,
                    _settings.SecondPicoTunerUdpBasePort + 1
                };
                for (int unit = 0; unit < 2; unit++)
                {
                    UDPClient listener = new UDPClient(statusPorts[unit], unit);
                    listener.DataReceived += Longmynd_status_DataReceived;
                    listener.ConnectionStatusChanged += Longmynd_status_ConnectionStatusChanged;
                    winterHillStatusClients[unit] = listener;
                    listener.Connect();
                    Log.Information("PicoTuner {Unit} status listening on UDP {Port}",
                        unit + 1, statusPorts[unit]);
                }
            }
            else
            {
                // BATC WinterHill BIP+01 carries extended status for all RX.
                longmynd_status = new UDPClient(port);
                longmynd_status.DataReceived += Longmynd_status_DataReceived;
                longmynd_status.ConnectionStatusChanged += Longmynd_status_ConnectionStatusChanged;
                longmynd_status.Connect();
                Log.Information("WinterHill status: listening on UDP {Port}", port);
            }
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
            System.Threading.Interlocked.Exchange(ref lastPicoStatusTicks, 0);
            longmynd_status?.Close();
            longmynd_status = null;
            if (winterHillStatusClients != null)
            {
                foreach (var client in winterHillStatusClients)
                    client?.Close();
                winterHillStatusClients = null;
            }
        }

        private void Longmynd_status_DataReceived(object sender, byte[] e)
        {
            // Learn the actual PicoTuner address from the sender of valid live status
            // packets. This avoids sending tune commands to a stale address or to the
            // Windows PC's own LAN address. Reception can still work in that situation
            // because status/TS arrive inbound, while control commands silently go nowhere.
            UDPClient statusClient = sender as UDPClient;
            if (!DualPicoEnabled && statusClient != null &&
                !string.IsNullOrWhiteSpace(statusClient.LastRemoteAddress))
            {
                string detectedHost = statusClient.LastRemoteAddress;
                if (!string.Equals(runtimeUdpHost, detectedHost, StringComparison.OrdinalIgnoreCase))
                {
                    Log.Information("PicoTuner UDP control host learned from status packets: " + detectedHost +
                                    " (configured: " + _settings.WinterHillUdpHost + ")");
                    runtimeUdpHost = detectedHost;
                }
            }

            if (e == null || e.Length == 0) return;
            try
            {
            var data = Encoding.ASCII.GetString(e);

            int sourceUnit = DualPicoEnabled && statusClient != null ? statusClient.getID() : 0;
            if (DualPicoEnabled)
            {
                if (statusClient == null || sourceUnit < 0 || sourceUnit > 1)
                    return;
                // The local destination UDP port identifies the device.
                // PicoTuner firmware can report a different source address
                // when network interfaces, DHCP or NAT are involved. A strict
                // equality check here silently discarded every RX C/D packet.
                // Keep distinct local ports and receiver ID ranges as the
                // routing boundary; log any sender-IP discrepancy for diagnosis.
                string expectedHost = sourceUnit == 0 ? _settings.WinterHillUdpHost :
                    _settings.SecondPicoTunerUdpHost;
                if (!string.IsNullOrWhiteSpace(statusClient.LastRemoteAddress) &&
                    !string.Equals(statusClient.LastRemoteAddress, expectedHost,
                    StringComparison.OrdinalIgnoreCase))
                {
                    Log.Debug("Dual PicoTuner {Unit} status received from {Sender}, configured IP {Configured}; mapping by local UDP port and RX ID",
                        sourceUnit + 1, statusClient.LastRemoteAddress, expectedHost);
                }
            }
            int udp_base_port = DualPicoEnabled && sourceUnit == 1 ?
                _settings.SecondPicoTunerUdpBasePort : _settings.WinterHillUdpBasePort;

            string[] status_strings = data.Split('\n');

            monitorMessage mm = new monitorMessage();
            mm.type = "RX";
            mm.timestamp = 0;
            int receiverCount = _settings.DefaultInterface == 3 || DualPicoEnabled ? 4 : 2;
            mm.rx = new ReceiverMessage[receiverCount + 1];
            for (int i = 0; i < mm.rx.Length; i++)
            {
                mm.rx[i] = new ReceiverMessage();
                mm.rx[i].rx = 99;
            }

            // Multiple $0 receiver sections can arrive in ONE packet.
            // No assumptions about receiver identity based on UDP port.
            int receiver = 0;
            bool receiverIdentified = false;

            for (int c = 0; c < status_strings.Length; c++)
            {
                string[] dt = status_strings[c].Trim().Split(',');
                if (dt.Length == 0)
                    continue;

                if (dt[0] != "$0" && receiver == 0)
                    continue;
                switch (dt[0])
                {
                    case "$0":
                        if (dt.Length < 2) break;
                        int receiver_num = udp_base_port % 100;
                        int reportedReceiver;
                        if (!int.TryParse(dt[1].Trim(), out reportedReceiver))
                        {
                            Log.Warning("Ignoring malformed WinterHill receiver ID: '{Id}'", dt[1]);
                            receiver = 0;
                            break;
                        }
                        int localReceiver = reportedReceiver - receiver_num;
                        receiver = DualPicoEnabled ? sourceUnit * 2 + localReceiver : localReceiver;
                        if (localReceiver < 1 || (DualPicoEnabled && localReceiver > 2) ||
                            receiver < 1 || receiver > receiverCount)
                        {
                            Log.Warning("WinterHill status rejected: reported RX={Reported}, base={Base}, decoded={Decoded}, expected 1..{Count}",
                                reportedReceiver, receiver_num, receiver, receiverCount);
                            receiver = 0;
                            break;
                        }
                        receiverIdentified = true;
                        mm.rx[receiver].rx = receiver;
                        if (DualPicoEnabled && sourceUnit == 1)
                            Log.Debug("PicoTuner 2 status mapped local RX{Local} to OpenTuner RX{Global}",
                                localReceiver, receiver);
                        // Some firmware sends status in partial batches. Do not
                        // treat an omitted $1 demod state as a loss of lock.
                        mm.rx[receiver].scanstate = demodstate[receiver - 1];
                        break;

                    case "$1":
                        if (dt.Length < 2) break;

                        // PicoTuner/WinterHill firmware variants have used several labels
                        // for DVB-S and DVB-S2 over time. Normalise the text and accept
                        // descriptive suffixes (for example "DVB-S QPSK" or "DVB-S1 LOCK")
                        // rather than requiring one exact literal string.
                        string demodState = (dt[1] ?? "").Trim().ToUpperInvariant();
                        string compactState = demodState.Replace("_", "-").Replace(" ", "");

                        if (compactState.StartsWith("DVB-S2") || compactState.StartsWith("DVBS2"))
                        {
                            mm.rx[receiver].scanstate = 2;
                        }
                        else if (compactState.StartsWith("DVB-S1") ||
                                 compactState.StartsWith("DVBS1") ||
                                 compactState.StartsWith("DVB-S") ||
                                 compactState.StartsWith("DVBS"))
                        {
                            mm.rx[receiver].scanstate = 3;
                        }
                        else if (compactState == "HEADER")
                        {
                            mm.rx[receiver].scanstate = 1;
                        }
                        else if (compactState == "SEARCH" || compactState == "SEARCHING" || compactState == "LOST")
                        {
                            mm.rx[receiver].scanstate = 0;
                        }
                        else
                        {
                            Log.Warning("WH: Don't know how to decode demod state: '" + dt[1] + "'");
                            mm.rx[receiver].scanstate = 1;
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

            // Deliver all identified receivers from the batch. Earlier
            // code only marked the last receiver, discarding RX2/RX4.
            if (!receiverIdentified) return;
            for (int id = 1; id <= receiverCount; id++)
            {
                ReceiverMessage rx = mm.rx[id];
                if (rx.rx == 99) continue;
                if (rx.service_name == null) rx.service_name = "";
                if (rx.service_provider_name == null) rx.service_provider_name = "";
                if (rx.mer == null) rx.mer = "";
                if (rx.modcod == null) rx.modcod = "";
                if (rx.dbmargin == null) rx.dbmargin = "";
                if (rx.ts_port == null) rx.ts_port = "";
                if (rx.ts_addr == null) rx.ts_addr = "";
            }
            UpdateInfo(mm);
            if (ModernIsPicoTunerEthernet || ModernIsStandardWinterHillUdp)
                System.Threading.Interlocked.Exchange(ref lastPicoStatusTicks, DateTime.UtcNow.Ticks);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Ignoring invalid PicoTuner UDP status packet during startup/reception");
            }
        }

        // Same remote RESET command and port calculation as WH Broadcast Listener.
        // RESET is not REBOOT: it resets the receiver without a power cycle.
        public bool ModernSendPicoReset()
        {
            if (!ModernIsPicoTunerEthernet) return false;
            IPAddress address;
            string host = GetRuntimeUdpHost();
            if (!IPAddress.TryParse(host, out address))
            {
                Log.Warning("PicoTuner reset skipped: invalid control host '{Host}'", host);
                return false;
            }

            int port = (_settings.WinterHillUdpBasePort / 100) * 100 + 20;
            try
            {
                byte[] command = Encoding.ASCII.GetBytes("[to@wh] reset=147");
                using (UdpClient client = new UdpClient())
                    client.Send(command, command.Length, new IPEndPoint(address, port));
                Log.Information("PicoTuner RESET sent to {Host}:{Port}", host, port);
                return true;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "PicoTuner RESET failed");
                return false;
            }
        }

        public void UDPSetVoltage(int plug, uint voltage)
        {
            int base_port = _settings.WinterHillUdpBasePort;
            string controlHost = GetRuntimeUdpHost();
            IPEndPoint WinterHill_end_point = null;
            IPAddress hostAddress;
            if (!IPAddress.TryParse(controlHost, out hostAddress))
            {
                Log.Warning("PicoTuner UDP voltage command deferred: invalid/unresolved host '{Host}'", controlHost);
                return;
            }
            WinterHill_end_point = new IPEndPoint(hostAddress, base_port + 21);

            string command2 = "";
            string vg = plug == 0 ? "vgx" : "vgy";
            switch (voltage)
            {
                case 0: command2 = ("[to@wh] " + vg + "=OFF"); break;
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
            int physicalDevice = GetPicoLocalReceiver(device);
            int physicalBase = GetPicoBasePort(device);
            // PicoTunerWH accepts [to@wh] WinterHill-format control commands
            // at base+20. Base+21/+22 are MiniTioune-format receiver ports.
            // Preserve the proven PicoTuner 1 path while fixing PicoTuner 2,
            // whose TS targets remain 0.0.0.0 after commands to 9821/9822.
            int base_port = DualPicoEnabled && device >= 2 ?
                physicalBase + 20 : physicalBase + 21 + physicalDevice;
            int receiver_num = (physicalBase % 100) + 1 + physicalDevice;
            string controlHost = GetRuntimeUdpHost(device);
            IPAddress hostAddress;
            if (!IPAddress.TryParse(controlHost, out hostAddress))
            {
                Log.Warning("PicoTuner UDP tuning command deferred: invalid/unresolved host '{Host}'", controlHost);
                return;
            }
            IPEndPoint WinterHill_end_point = new IPEndPoint(hostAddress, base_port);

            Log.Information("WinterHill tune RX{Receiver}: destination={Host}:{Port}, receiver ID={ReceiverId}, freq={Frequency}, SR={Rate}, RF={Rf}",
                device + 1, controlHost, base_port, receiver_num, freq, sr,
                _settings.RFPort[device] == 0 ? "A" : "B");

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
