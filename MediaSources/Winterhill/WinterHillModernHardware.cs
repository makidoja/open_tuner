using System;

namespace opentuner.MediaSources.WinterHill
{
    public partial class WinterHillSource
    {
        public bool ModernSupportsLnbPower
        {
            get { return ModernIsPicoTunerEthernet && _settings != null; }
        }

        public int ModernGetLnbVoltage(int output)
        {
            if (_settings == null || _settings.LNBVoltage == null || output < 0 || output >= _settings.LNBVoltage.Length)
                return 0;
            return (int)_settings.LNBVoltage[output];
        }

        public void ModernSetLnbVoltage(int output, int volts)
        {
            if (!ModernSupportsLnbPower || output < 0 || output > 1)
                return;

            if (volts != 0 && volts != 13 && volts != 18)
                return;

            UDPSetVoltage(output, (uint)volts);

            try
            {
                _settings.LNBVoltage[output] = (uint)volts;
                _settingsManager.SaveSettings(_settings);
            }
            catch { }
        }

        public string ModernGetRfInput(int tuner)
        {
            if (_settings == null || _settings.RFPort == null || tuner < 0 || tuner >= _settings.RFPort.Length)
                return "—";
            return _settings.RFPort[tuner] == 0 ? "A" : "B";
        }

        public void ModernSetRfInput(int tuner, int input)
        {
            if (_settings == null || _settings.RFPort == null || tuner < 0 || tuner >= _settings.RFPort.Length)
                return;
            if (input != 0 && input != 1)
                return;

            SetRFPort(tuner, input);
            try { _settingsManager.SaveSettings(_settings); } catch { }
        }

        public long ModernGetOffset(int tuner)
        {
            if (_current_offset == null || tuner < 0 || tuner >= _current_offset.Length)
                return 0;
            return _current_offset[tuner];
        }

        public void ModernSetOffset(int tuner, long offsetKHz)
        {
            if (_settings == null || _settings.DefaultOffset == null || _current_offset == null ||
                tuner < 0 || tuner >= _current_offset.Length || tuner >= _settings.DefaultOffset.Length)
                return;

            if (offsetKHz < 0 || offsetKHz > 15000000)
                return;

            _current_offset[tuner] = (int)offsetKHz;
            _settings.DefaultOffset[tuner] = (uint)offsetKHz;
            try { _settingsManager.SaveSettings(_settings); } catch { }
        }

        public string ModernHardwareName
        {
            get
            {
                if (ModernIsPicoTunerEthernet) return "PicoTuner (ETH)";
                return "WinterHill";
            }
        }
    }
}
