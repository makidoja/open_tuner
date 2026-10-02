using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Windows.Forms;
using opentuner.MediaSources;
using Serilog;

namespace opentuner
{
    // Transitional bridge: keeps the proven receiver/media engine in MainForm while
    // allowing ModernMainForm to be a completely separate visible UI. MainForm is
    // never shown when launched through ModernMainForm.
    public partial class MainForm
    {
        public event Action<int, OTSourceData, string> BackendSourceData;

        private bool backendSourceHooked;

        public void BackendPrepare()
        {
            // Run the normal non-visual initialisation that previously happened on Load,
            // but never allow the hidden legacy form to auto-connect. The standalone
            // ModernMainForm owns the connect action; allowing both layers to connect can
            // bind the same UDP socket twice (WSAEADDRINUSE / 10048).
            bool savedAutoConnect = _settings.auto_connect;
            try
            {
                _settings.auto_connect = false;
                Form1_Load(this, EventArgs.Empty);
            }
            finally
            {
                _settings.auto_connect = savedAutoConnect;
            }
        }

        public string[] BackendSourceNames()
        {
            return _availableSources.Select(s => s.GetName()).ToArray();
        }

        public int BackendSelectedSourceIndex
        {
            get { return comboAvailableSources != null ? comboAvailableSources.SelectedIndex : 0; }
            set
            {
                if (comboAvailableSources != null && value >= 0 && value < comboAvailableSources.Items.Count)
                    comboAvailableSources.SelectedIndex = value;
            }
        }

        public bool BackendConnectSource(int sourceIndex)
        {
            if (sourceIndex < 0 || sourceIndex >= _availableSources.Count)
                return false;

            // Do not initialise the same source twice if a previous connection attempt
            // already succeeded. This is an additional guard against duplicate socket binds.
            if (source_connected && videoSource != null)
                return true;

            BackendSelectedSourceIndex = sourceIndex;

            // The visible modern UI owns BATC spectrum/chat/quick-tune. If the hidden
            // legacy MainForm also creates those extras we end up with two BATCSpectrum
            // instances sharing the legacy static band-plan bitmap. That is what caused
            // the repeated "Object is currently in use elsewhere" GDI+ exceptions.
            bool savedSpectrum = checkBatcSpectrum.Checked;
            bool savedChat = checkBatcChat.Checked;
            bool savedQuickTune = checkQuicktune.Checked;
            bool savedMqtt = checkMqttClient.Checked;
            bool savedReporter = checkDATVReporter.Checked;

            try
            {
                checkBatcSpectrum.Checked = false;
                checkBatcChat.Checked = false;
                checkQuicktune.Checked = false;
                checkMqttClient.Checked = false;
                checkDATVReporter.Checked = false;

                source_connected = ConnectSelectedSource();
            }
            catch (SocketException ex)
            {
                source_connected = false;

                string message;
                if (ex.SocketErrorCode == SocketError.AddressAlreadyInUse)
                {
                    message =
                        "OpenTuner could not open the UDP listener because the required local port is already in use.\r\n\r\n" +
                        "For WinterHill / PicoTuner Ethernet mode, open SOURCE SETTINGS and change the UDP Base Port, " +
                        "or close the application currently using that port.\r\n\r\n" +
                        "Example: a UDP Base Port of 9900 uses local status port 9901.\r\n\r\n" +
                        "Windows error: " + ex.Message;
                }
                else
                {
                    message = "OpenTuner could not open the receiver network socket.\r\n\r\n" + ex.Message;
                }

                MessageBox.Show(
                    message,
                    "OpenTuner - receiver network error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);

                return false;
            }
            catch (Exception ex)
            {
                source_connected = false;
                MessageBox.Show(
                    "OpenTuner could not initialise the selected receiver source.\r\n\r\n" + ex.Message,
                    "OpenTuner - receiver error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return false;
            }
            finally
            {
                checkBatcSpectrum.Checked = savedSpectrum;
                checkBatcChat.Checked = savedChat;
                checkQuicktune.Checked = savedQuickTune;
                checkMqttClient.Checked = savedMqtt;
                checkDATVReporter.Checked = savedReporter;
            }

            if (source_connected && videoSource != null && !backendSourceHooked)
            {
                videoSource.OnSourceData += BackendForwardSourceData;
                backendSourceHooked = true;
            }

            return source_connected;
        }

        private void BackendForwardSourceData(int videoNr, OTSourceData data, string description)
        {
            var handler = BackendSourceData;
            if (handler != null)
                handler(videoNr, data, description);
        }

        public void BackendShowSourceSettings(int sourceIndex)
        {
            if (sourceIndex >= 0 && sourceIndex < _availableSources.Count)
                _availableSources[sourceIndex].ShowSettings();
        }

        public void BackendShowGeneralSettings()
        {
            settingsToolStripMenuItem_Click(this, EventArgs.Empty);
        }

        public void BackendManagePresets()
        {
            if (menuManageFrequencyPresets != null)
                menuManageFrequencyPresets.PerformClick();
        }

        public List<StoredFrequency> BackendPresets()
        {
            return stored_frequencies == null
                ? new List<StoredFrequency>()
                : new List<StoredFrequency>(stored_frequencies);
        }

        public bool BackendConnected
        {
            // Some receiver sources (notably WinterHill/PicoTuner UDP) do not update
            // DeviceConnected reliably even though Initialise succeeded and live source
            // data/video are flowing. For the standalone UI, successful SourceConnect is
            // the correct readiness signal for tuning and connection status.
            get { return source_connected && videoSource != null; }
        }

        public int BackendTunerCount
        {
            get
            {
                if (videoSource == null)
                    return 2;
                try { return videoSource.GetVideoSourceCount(); }
                catch { return 2; }
            }
        }

        public string BackendDeviceName
        {
            get
            {
                if (videoSource == null)
                    return "";
                try { return videoSource.GetDeviceName(); }
                catch { return ""; }
            }
        }

        public void BackendTune(int tuner, uint frequencyKHz, uint symbolRate)
        {
            if (videoSource == null)
                return;

            try
            {
                // The modern UI displays RF/downlink frequency. Reproduce the exact
                // frequency path used by OpenTuner's proven legacy tuner control: convert
                // displayed RF back to the NIM/IF frequency, then call SetFrequency with
                // offset_included=false so the source applies its configured offset.
                // This matters especially for WinterHill/PicoTuner Ethernet.
                long offset = 0;
                try
                {
                    long withOffset = videoSource.GetFrequency(tuner, true);
                    long withoutOffset = videoSource.GetFrequency(tuner, false);
                    offset = withOffset - withoutOffset;
                    if (offset < 0) offset = 0;
                }
                catch { offset = 0; }

                uint sourceFrequency = frequencyKHz;
                bool offsetIncluded = true;

                if (videoSource.GetName() == "WinterHill Variant" && offset > 0 && frequencyKHz > (uint)offset)
                {
                    sourceFrequency = frequencyKHz - (uint)offset;
                    offsetIncluded = false;
                }

                Log.Information(
                    "Modern tune: tuner {Tuner}, displayed RF {Rf} kHz, source freq {SourceFreq} kHz, SR {Sr} kS, offsetIncluded {OffsetIncluded}",
                    tuner, frequencyKHz, sourceFrequency, symbolRate, offsetIncluded);

                videoSource.SetFrequency(tuner, sourceFrequency, symbolRate, offsetIncluded);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Modern tune failed for tuner {Tuner}", tuner);
            }
        }

        public Control[] BackendTakeVideoControls(int tuner)
        {
            if (tuner < 0 || tuner >= video_panels.Length || video_panels[tuner] == null)
                return new Control[0];

            return video_panels[tuner].Controls.Cast<Control>().ToArray();
        }

        public void BackendShutdown()
        {
            try
            {
                Form1_FormClosing(this, new FormClosingEventArgs(CloseReason.ApplicationExitCall, false));
            }
            catch
            {
                // Shutdown is best-effort; the original close path already catches its
                // own receiver/media cleanup exceptions.
            }
        }
    }
}