namespace opentuner.MediaSources.WinterHill
{
    public partial class WinterHillSource
    {
        public bool ModernIsPicoTunerEthernet
        {
            get { return _settings != null && _settings.DefaultInterface == 2; }
        }

        public string ModernInterfaceDescription
        {
            get
            {
                if (_settings == null) return "WinterHill";
                switch (_settings.DefaultInterface)
                {
                    case 1: return "WebSocket (ZR6TG)";
                    case 2: return "PicoTuner Ethernet (G4EWJ)";
                    default: return "Always Ask";
                }
            }
        }
    }
}
