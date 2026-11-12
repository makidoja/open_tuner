using opentuner.MediaPlayers;
using opentuner.Utilities;
using Serilog;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;

namespace opentuner.MediaSources.WinterHill
{
    public partial class WinterHillSource : OTSource
    {
        private WinterHillSettings _settings;
        private SettingsManager<WinterHillSettings> _settingsManager;

        public override event SourceDataChange OnSourceData;

        private bool _connected = false;
        public override bool DeviceConnected => _connected;

        private VideoChangeCallback VideoChangeCB;

        Thread[] ts_thread_t = null;
        TSThread[] ts_threads;

        // todo: fix double buffer read
        private CircularBuffer[] udp_buffer; // = new CircularBuffer(GlobalDefines.CircularBufferStartingCapacity);
        public CircularBuffer[] ts_data_queue; // = new CircularBuffer(GlobalDefines.CircularBufferStartingCapacity);

        private OTMediaPlayer[] _media_player = new OTMediaPlayer[4];
        private TSRecorder[] _recorders = new TSRecorder[4];
        private TSUdpStreamer[] _streamer = new TSUdpStreamer[4];
        private List<TunerControlForm> _tuner_forms;

        private int ts_devices = 4;

        UDPClient[] udp_clients;

        private string _MediaPath;
        private string _LocalIp;

        private int[] _current_frequency = new int[4] {0, 0, 0, 0};
        private int[] _current_offset = new int[4] { 0, 0, 0, 0 };
        private int[] _current_sr = new int[4] { 0, 0, 0, 0 };

        private string[] last_service_name = new string[4] { "", "", "", "" };
        private string[] last_service_provider = new string[4] { "", "", "", "" };
        private string[] last_dbm = new string[4] { "", "", "", "" };
        private string[] last_mer = new string[4] { "", "", "", "" };

        // Actual transport bytes observed on the local four UDP TS sockets.
        private readonly long[] tsReceivedBytes = new long[4];
        private readonly long[] tsSampleBytes = new long[4];
        private readonly DateTime[] tsSampleAt = new DateTime[4];
        private readonly double[] tsActualMbps = new double[4];
        private readonly object[] tsSampleLocks = new object[] { new object(), new object(), new object(), new object() };
        private readonly string[] receivedVideoCodec = new string[4];
        private readonly string[] receivedVideoResolution = new string[4];
        private readonly string[] receivedAudioCodec = new string[4];
        private readonly string[] receivedAudioRate = new string[4];
        
        private int[] demodstate = new int[4] {0, 0, 0, 0};

        private bool[] playing = new bool[4] {false, false, false, false};

        bool _videoPlayersReady = false;

        int hw_device = 1;

        public WinterHillSource()
        {
            // settings
            _settings = new WinterHillSettings();
            _settingsManager = new SettingsManager<WinterHillSettings>("winterhill_settings");
            _settings = _settingsManager.LoadSettings(_settings);
        }


        public override int Initialize(VideoChangeCallback VideoChangeCB, Control Parent)
        {
            _parent = Parent;

            int udp_port = _settings.WinterHillWSUdpBasePort;

            int defaultInterface = _settings.DefaultInterface;

            if (defaultInterface == 0)
            {
                ChooseWinterHillHardwareInterfaceForm chooseInterfaceForm = new ChooseWinterHillHardwareInterfaceForm();

                if (chooseInterfaceForm.ShowDialog() == DialogResult.OK)
                {
                    defaultInterface = chooseInterfaceForm.comboHardwareInterface.SelectedIndex + 1;
                }
                else
                {
                    return -1;
                }
            }

            for (int c = 0; c < 4; c++)
                _current_offset[c] = (int)_settings.DefaultOffset[c];

            // connect interface
            switch (defaultInterface)
            {
                case 1: // websockets
                    connectWebsockets();
                    udp_port = _settings.WinterHillWSUdpBasePort;
                    ts_devices = 4;
                    hw_device = 1;

                    break;
                case 3: // standard BATC Winter Hill on Raspberry Pi, four UDP receivers
                    udp_port = _settings.WinterHillUdpBasePort;
                    ts_devices = 4;
                    hw_device = 3;
                    break;
                case 2: // PicoTuner Ethernet, optionally two devices
                    udp_port = _settings.WinterHillUdpBasePort;
                    bool dualPico = _settings.SecondPicoTunerEnabled;
                    if (dualPico)
                    {
                        System.Net.IPAddress first, second;
                        if (!System.Net.IPAddress.TryParse(_settings.WinterHillUdpHost, out first) ||
                            !System.Net.IPAddress.TryParse(_settings.SecondPicoTunerUdpHost, out second) ||
                            first.Equals(second) ||
                            _settings.SecondPicoTunerUdpBasePort == udp_port ||
                            System.Math.Abs(_settings.SecondPicoTunerUdpBasePort - udp_port) < 100)
                        {
                            Log.Error("Dual PicoTuner configuration invalid: unique IPv4 addresses and non-overlapping UDP port ranges required");
                            return -1;
                        }
                    }
                    ts_devices = dualPico ? 4 : 2;
                    hw_device = 2;
                    UDPSetVoltage(0, _settings.LNBVoltage[0]);
                    UDPSetVoltage(1, _settings.LNBVoltage[1]);
                    Log.Information("PicoTuner Ethernet mode: {Count} receivers, second unit enabled={Dual}",
                        ts_devices, dualPico);
                    break;
            }

            for (int c = 0; c < ts_devices; c++)
                SetFrequency(c, _settings.DefaultFrequency[c], _settings.DefaultSR[c], true);

            // open udp ts ports
            udp_clients = new UDPClient[4];
            ts_threads = new TSThread[4];
            ts_thread_t = new Thread[4];
            ts_data_queue = new CircularBuffer[4];
            udp_buffer = new CircularBuffer[4];

            for (int c = 0; c < ts_devices; c++)
            {
                int port = _settings.DefaultInterface == 2 && _settings.SecondPicoTunerEnabled && c >= 2
                    ? _settings.SecondPicoTunerUdpBasePort + 41 + (c - 2)
                    : udp_port + 41 + c;

                Log.Information("TS UDP Ports: " + port.ToString());

                udp_buffer[c] = new CircularBuffer(GlobalDefines.CircularBufferStartingCapacity);
                ts_data_queue[c] = new CircularBuffer(GlobalDefines.CircularBufferStartingCapacity);

                udp_clients[c] = new UDPClient(port, c);
                udp_clients[c].ConnectionStatusChanged += WinterHillSource_ConnectionStatusChanged;
                udp_clients[c].DataReceived += WinterHillSource_DataReceived;
                udp_clients[c].Connect();

                FlushTS flush_ts = null;
                ReadTS read_ts = null;

                switch(c)
                {
                    case 0: flush_ts = FlushTS0; read_ts = ReadTS0; break;
                    case 1: flush_ts = FlushTS1; read_ts = ReadTS1; break;
                    case 2: flush_ts = FlushTS2; read_ts = ReadTS2; break;
                    case 3: flush_ts = FlushTS3; read_ts = ReadTS3; break;
                }


                ts_threads[c] = new TSThread(ts_data_queue[c], flush_ts, read_ts, "WH TS" + c.ToString());
                ts_thread_t[c] = new Thread(ts_threads[c].worker_thread);
                ts_thread_t[c].Start();

            }

            BuildSourceProperties();

            this.VideoChangeCB = VideoChangeCB;

            // get local ip
            List<string> detected_ips = CommonFunctions.determineIP();

            if (detected_ips.Count > 0)
                _LocalIp = detected_ips[0];

            if (detected_ips.Count > 1)
            {
                for (int c = 0; c < detected_ips.Count; c++)
                {
                    Log.Warning(detected_ips[c]);
                }
                Log.Warning("Multiple IP's detected, using " + _LocalIp);
            }

            // Previously a live PicoTuner could deliver status during setup.
            if (defaultInterface == 2 || defaultInterface == 3)
                ConnectWinterHillUDP(udp_port + 1);

            return ts_devices;
        }

        public void FlushTS0()
        {
            udp_buffer[0].Clear();
        }
        public void FlushTS1()
        {
            udp_buffer[1].Clear();
        }
        public void FlushTS2()
        {
            udp_buffer[2].Clear();
        }
        public void FlushTS3()
        {
            udp_buffer[3].Clear();
        }

        byte ReadTSGeneric(int id, ref byte[] data, ref uint dataRead)
        {
            int read = udp_buffer[id].Count;
            uint written = 0;

            if (udp_buffer[id].Count > 4000)
            {
                read = 4000;
            }

            for (int c = 0; c < read; c++)
            {
                data[c] = udp_buffer[id].Dequeue();
                written += 1;
            }

            dataRead = written;

            return 0;
        }

        byte ReadTS0(ref byte[] data, ref uint dataRead)
        {
            return ReadTSGeneric(0, ref data, ref dataRead);
        }

        byte ReadTS1(ref byte[] data, ref uint dataRead)
        {
            return ReadTSGeneric(1, ref data, ref dataRead);
        }

        byte ReadTS2(ref byte[] data, ref uint dataRead)
        {
            return ReadTSGeneric(2, ref data, ref dataRead);
        }

        byte ReadTS3(ref byte[] data, ref uint dataRead)
        {
            return ReadTSGeneric(3, ref data, ref dataRead);
        }

        private void WinterHillSource_DataReceived(object sender, byte[] e)
        {
            int device = ((UDPClient)sender).getID();

            if (!playing[device] || e == null || udp_buffer == null || udp_buffer[device] == null)
                return;

            System.Threading.Interlocked.Add(ref tsReceivedBytes[device], e.Length);
            for (int c = 0; c < e.Length; c++)
            {
                udp_buffer[device].Enqueue(e[c]);
            }
            ts_threads[device].NewDataPresent();
        }

        private void WinterHillSource_ConnectionStatusChanged(object sender, bool connection_status)
        {
            Log.Information("Connection Status " + ((UDPClient)sender).getID() + " : " + (connection_status ? "Connected" : "Disconnected"));
        }

        public override void Close()
        {
            Log.Information("Closing WinterHill Source");

            int defaultInterface = _settings.DefaultInterface;
            _settingsManager.SaveSettings(_settings);

            switch (defaultInterface)
            {
                case 1: // websockets
                    DisconnectWebsockets();
                    break;
                case 2: // udp pico wh
                case 3: // standard Winter Hill UDP
                    DisconnectWinterHillUDP();
                    break;
            }
            if (ts_thread_t != null) 
            {
                for (int c = 0; c < ts_thread_t.Length; c++)
                {
                    ts_thread_t[c]?.Abort();
                }
            }

            if (udp_clients != null)
            {
                for (int c = 0; c < udp_clients.Length; c++)
                {
                    udp_clients[c]?.Disconnect();
                }

            }
            //UDPClient[] udp_clients;
        }

        public override void ConfigureMediaPath(string MediaPath)
        {
            _MediaPath = MediaPath;
        }

        public override void ConfigureTSRecorders(List<TSRecorder> TSRecorders)
        {
            for (int c = 0; c  < TSRecorders.Count; c++)
            {
                _recorders[c] = TSRecorders[c];
            }
        }

        public override void ConfigureTSStreamers(List<TSUdpStreamer> TSStreamers)
        {
            for (int c = 0; c < TSStreamers.Count; c++)
            {
                _streamer[c] = TSStreamers[c];
            }
        }

        public override void ConfigureVideoPlayers(List<OTMediaPlayer> MediaPlayers)
        {
            for (int c = 0; c < ts_devices; c++)
            {
                _media_player[c] = MediaPlayers[c];
                _media_player[c].onVideoOut += WinterHillSource_onVideoOut;
                if (_settings.DefaultMuted[c])
                {
                    _media_player[c].SetVolume(0);
                }
                else
                {
                    _media_player[c].SetVolume((int)_settings.DefaultVolume[c]);
                }
            }

            _videoPlayersReady = true;
        }

        private void WinterHillSource_onVideoOut(object sender, MediaStatus e)
        {
            int video_id = ((OTMediaPlayer)sender).getID();

            preMute[video_id] = (int)_settings.DefaultVolume[video_id];
            muted[video_id] = _settings.DefaultMuted[video_id];
            if (muted[video_id] == true)
            {
                _media_player[video_id].SetVolume(0);
            }
            else
            {
                _media_player[video_id].SetVolume(preMute[video_id]);
            }

            UpdateMediaProperties(video_id, e);
        }

        public override string GetDescription()
        {
            return "WinterHill Client, Compatible with:" +
            Environment.NewLine + Environment.NewLine +
            "ZR6TG - WH Variant (websocket)" + Environment.NewLine +
            "G4EWJ - PicoTuner WH (Ethernet)" + Environment.NewLine +
            "BATC - Winter Hill UDP (4 RX)" + Environment.NewLine;

        }

        public override string GetDeviceName()
        {
            switch (hw_device)
            {
                case 1: return "WinterHill (ZR6TG Variant)";
                case 2: return _settings.SecondPicoTunerEnabled ?
                    "Dual PicoTuner Ethernet (4 RX)" : "PicoTuner (G4EWJ Ethernet WH)";
                case 3: return "Winter Hill UDP (BATC 4 RX)";
            }

            return "Unknown";
        }

        public override long GetFrequency(int device, bool offset_included)
        {
            return _current_frequency[device] + (offset_included ? _current_offset[device] : 0);
        }

        public override string GetName()
        {
            return "WinterHill Variant";
        }

        public override void OverrideDefaultMuted(bool Override)
        {
            if (Override)
            {
//                for (int i = 0; i < _settings.DefaultMuted.Count(); i++)
                for (int i = 0; i < hw_device; i++)
                {
                    preMute[i] = (int)_settings.DefaultVolume[i];                           // save DefaultVolume in preMute
                    _tuner_properties[i].UpdateValue("volume_slider_" + i.ToString(), "0"); // side effect: will set DefaultVolume to 0
                    _tuner_properties[i].UpdateMuteButtonColor("media_controls_" + i.ToString(), Color.PaleVioletRed);
                    muted[i] = _settings.DefaultMuted[i] = true;
                    _settings.DefaultVolume[i] = (uint)preMute[i];                          // restore DefaultVolume
                }
            }
        }

        public override CircularBuffer GetVideoDataQueue(int device)
        {
            return ts_data_queue[device];
        }

        public override int GetVideoSourceCount()
        {
            return ts_devices;
        }

        public override void RegisterTSConsumer(int device, CircularBuffer ts_buffer_queue)
        {
            ts_threads[device].RegisterTSConsumer(ts_buffer_queue);
        }

        public void SetRFPort(int device, int port)
        {
            if (device < 0 || device >= ts_devices || (port != 0 && port != 1))
                return;

            _settings.RFPort[device] = (uint)port;
            int frequency = _current_frequency[device];
            int rate = _current_sr[device];
            // A change of RF connector must not send freq=0/srate=0
            // when no status has yet arrived from this receiver.
            if (frequency <= 0 && device < _settings.DefaultFrequency.Length)
                frequency = Math.Max(0, (int)_settings.DefaultFrequency[device] - _current_offset[device]);
            if (rate <= 0 && device < _settings.DefaultSR.Length)
                rate = (int)_settings.DefaultSR[device];

            Log.Information("WinterHill RX{Receiver}: physical RF {Port}, base frequency {Frequency} kHz, SR {SymbolRate} kS",
                device + 1, port == 0 ? "A" : "B", frequency, rate);
            if (frequency > 0 && rate > 0)
                SetFrequency(device, (uint)frequency, (uint)rate, false);
            else
                Log.Warning("WinterHill RX{Receiver}: RF port selected but tune deferred until frequency/SR available", device + 1);
        }


        public override void SetFrequency(int device, uint frequency, uint symbol_rate, bool offset_included)
        {
            if (device < 0 || device >= ts_devices || frequency == 0 || symbol_rate == 0)
            {
                Log.Warning("WinterHill: rejecting invalid tune RX{Receiver}, frequency={Frequency}, SR={Rate}",
                    device + 1, frequency, symbol_rate);
                return;
            }

            // Maintain requested tune state independently of status packets.
            // Status packets can be delayed or absent on RX2/RX4.
            // Internal frequency stores the baseband value without LO offset.
            int baseFrequency = offset_included
                ? (int)frequency - _current_offset[device] : (int)frequency;
            if (baseFrequency >= 0)
                _current_frequency[device] = baseFrequency;
            _current_sr[device] = (int)symbol_rate;
            Log.Information("WinterHill RX{Receiver} tune: frequency={Frequency} kHz, SR={Rate} kS, offsetIncluded={OffsetIncluded}, RF={Rf}",
                device + 1, frequency, symbol_rate, offset_included,
                _settings.RFPort[device] == 0 ? "A" : "B");


            if (offset_included)
            {
                switch (hw_device)
                {
                    case 1: WSSetFrequency(device, (int)frequency, (int)symbol_rate);
                        break;
                    case 2:
                    case 3: UDPSetFrequency(device, (int)frequency, (int)symbol_rate);
                        break;
                }

            }
            else
            {
                switch (hw_device)
                {
                    case 1:  WSSetFrequency(device, (int)frequency + (int)_current_offset[device], (int)symbol_rate);
                        break;
                    case 2:
                    case 3: UDPSetFrequency(device, (int)frequency + (int)_current_offset[device], (int)symbol_rate);
                        break;
                }
            }

        }

        public override void ShowSettings()
        {
            WinterHillSettingsForm settingsForm = new WinterHillSettingsForm(_settings);

            if (settingsForm.ShowDialog() == DialogResult.OK)
            {
                _settingsManager.SaveSettings(_settings);
            }

        }

        public override void StartStreaming(int device)
        {
            if (ts_threads != null)
            {
                if (ts_threads[device] != null)
                {
                    ts_threads[device].start_ts();
                }
            }
        }

        public override void StopStreaming(int device)
        {
            if (ts_threads != null)
            {
                if (ts_threads[device] != null)
                {
                    ts_threads[device].stop_ts();
                }
            }
        }

        public override string GetMoreInfoLink()
        {
            return "https://www.zr6tg.co.za/opentuner-winterhill-source/";
        }
    }
}
