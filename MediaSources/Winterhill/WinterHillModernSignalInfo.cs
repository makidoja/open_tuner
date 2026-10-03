namespace opentuner.MediaSources.WinterHill
{
    public partial class WinterHillSource
    {
        public string ModernGetDeliverySystem(int tuner)
        {
            if (tuner < 0 || tuner >= demodstate.Length)
                return "DVB";

            switch (demodstate[tuner])
            {
                case 2: return "DVB-S2";
                case 3: return "DVB-S";
                case 1: return "DVB HEADER";
                case 0: return "SEARCHING";
                case 0x80: return "LOST";
                case 0x81: return "TIMEOUT";
                case 0x82: return "IDLE";
                default: return "DVB";
            }
        }
    }
}
