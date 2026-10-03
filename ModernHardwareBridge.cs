using opentuner.MediaSources.WinterHill;

namespace opentuner
{
    public partial class MainForm
    {
        private WinterHillSource BackendWinterHill()
        {
            return videoSource as WinterHillSource;
        }

        public bool BackendSupportsLnbPower
        {
            get
            {
                WinterHillSource wh = BackendWinterHill();
                return wh != null && wh.ModernSupportsLnbPower;
            }
        }

        public int BackendGetLnbVoltage(int output)
        {
            WinterHillSource wh = BackendWinterHill();
            return wh == null ? 0 : wh.ModernGetLnbVoltage(output);
        }

        public void BackendSetLnbVoltage(int output, int volts)
        {
            WinterHillSource wh = BackendWinterHill();
            if (wh != null) wh.ModernSetLnbVoltage(output, volts);
        }

        public string BackendGetRfInput(int tuner)
        {
            WinterHillSource wh = BackendWinterHill();
            return wh == null ? "—" : wh.ModernGetRfInput(tuner);
        }

        public void BackendSetRfInput(int tuner, int input)
        {
            WinterHillSource wh = BackendWinterHill();
            if (wh != null) wh.ModernSetRfInput(tuner, input);
        }

        public long BackendGetOffset(int tuner)
        {
            WinterHillSource wh = BackendWinterHill();
            return wh == null ? 0 : wh.ModernGetOffset(tuner);
        }

        public void BackendSetOffset(int tuner, long offsetKHz)
        {
            WinterHillSource wh = BackendWinterHill();
            if (wh != null) wh.ModernSetOffset(tuner, offsetKHz);
        }

        public string BackendGetDeliverySystem(int tuner)
        {
            WinterHillSource wh = BackendWinterHill();
            return wh == null ? "DVB" : wh.ModernGetDeliverySystem(tuner);
        }

        public string BackendHardwareName
        {
            get
            {
                WinterHillSource wh = BackendWinterHill();
                return wh == null ? BackendDeviceName : wh.ModernHardwareName;
            }
        }
    }
}
