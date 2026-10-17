using opentuner.MediaSources.WinterHill;

namespace opentuner
{
    public partial class MainForm
    {
        public bool BackendTransportConnected
        {
            get
            {
                if (!source_connected || videoSource == null)
                    return false;

                WinterHillSource winterHill = videoSource as WinterHillSource;
                if (winterHill != null)
                {
                    // PicoTuner Ethernet is UDP and has no persistent remote socket;
                    // successful source initialisation means the local UDP control/status
                    // paths are ready. The ZR6TG WebSocket transport must report alive.
                    return winterHill.ModernIsPicoTunerEthernet ? winterHill.ModernPicoStatusLive : winterHill.DeviceConnected;
                }

                try { return videoSource.DeviceConnected; }
                catch { return source_connected; }
            }
        }

        // Read-only discovery metadata; does not change the working connect path.
        public bool BackendSelectedPicoEthernet(int selected)
        {
            if (selected < 0 || selected >= _availableSources.Count) return false;
            WinterHillSource wh = _availableSources[selected] as WinterHillSource;
            return wh != null && wh.ModernIsPicoTunerEthernet;
        }

        public string BackendSelectedPicoIp(int selected)
        {
            if (selected < 0 || selected >= _availableSources.Count) return "";
            WinterHillSource wh = _availableSources[selected] as WinterHillSource;
            return wh == null ? "" : wh.ModernConfiguredPicoAddress;
        }

        public bool BackendUsesPicoEthernet
        {
            get { WinterHillSource wh = videoSource as WinterHillSource; return wh != null && wh.ModernIsPicoTunerEthernet; }
        }

        public void BackendRetryPicoEthernet()
        {
            WinterHillSource wh = videoSource as WinterHillSource;
            if (wh != null && wh.ModernIsPicoTunerEthernet) wh.ModernRetryPicoConnection();
        }

        public string BackendInterfaceDescription
        {
            get
            {
                WinterHillSource winterHill = videoSource as WinterHillSource;
                if (winterHill != null)
                    return winterHill.ModernInterfaceDescription;

                try { return videoSource == null ? "" : videoSource.GetDeviceName(); }
                catch { return ""; }
            }
        }
    }
}
