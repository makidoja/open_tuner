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
                    return winterHill.ModernIsPicoTunerEthernet || winterHill.DeviceConnected;
                }

                try { return videoSource.DeviceConnected; }
                catch { return source_connected; }
            }
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
