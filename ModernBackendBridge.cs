using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Windows.Forms;
using opentuner.MediaSources;
using opentuner.MediaSources.WinterHill;
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
        private bool backendStartupRetuneDone;

        public void BackendPrepare()
        {
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

            if (source_connected && videoSource != null)
                return true;

            BackendSelectedSourceIndex = sourceIndex;
            backendStartupRetuneDone = false;

            // The visible modern UI owns BATC spectrum/chat/quick-tune. Do not let the
            // hidden legacy MainForm create a second copy of those extras.
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

                MessageBox.Show(message, "OpenTuner - receiver network error", MessageBoxButtons.OK, MessageBoxIcon.Error);
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

            if (source_connected && videoSource != null)
            {
                // On a cold start a stale configured host can point at the Windows PC
                // itself. In that state no PicoTuner status packets arrive, so waiting for
                // status to learn the correct IP can never work. PicoTuner broadcasts its
                // address on UDP 9997, so discover it first and immediately resend the
                // receiver setup to the hardware.
                WinterHillSource winterHill = videoSource as WinterHillSource;
                if (winterHill != null && winterHill.ModernIsPicoTunerEthernet)
                {
                    bool discovered = false;
                    try { discovered = winterHill.ModernDiscoverPicoTuner(1800); }
                    catch (Exception ex) { Log.Warning(ex, "PicoTuner discovery failed"); }

                    Log.Warning(discovered
                        ? "Modern startup: PicoTuner discovery succeeded; reapplying receiver setup"
                        : "Modern startup: PicoTuner discovery unavailable; using configured host and live-status fallback");

                    try { winterHill.ModernReapplyStartupTune(); }
                    catch (Exception ex) { Log.Warning(ex, "PicoTuner startup retune failed"); }
                }

                if (!backendSourceHooked)
                {
                    videoSource.OnSourceData += BackendForwardSourceData;
                    backendSourceHooked = true;
                }
            }

            return source_connected;
        }

        private void BackendForwardSourceData(int videoNr, OTSourceData data, string description)
        {
            // Re-send once more after the first live status packet. At that point
            // WinterHillUDP has also learned the actual sender address directly.
            if (!backendStartupRetuneDone)
            {
                WinterHillSource winterHill = videoSource as WinterHillSource;
                if (winterHill != null && winterHill.ModernIsPicoTunerEthernet)
                {
                    backendStartupRetuneDone = true;
                    try { winterHill.ModernReapplyStartupTune(); }
                    catch (Exception ex) { Log.Warning(ex, "PicoTuner startup retune failed"); }
                }
                else if (videoSource != null)
                {
                    backendStartupRetuneDone = true;
                }
            }

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
            }
        }
    }
}
