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

        public void ModernSetVolume(int tuner, int volume)
        {
            if (_settings == null || tuner < 0 || tuner >= _settings.DefaultVolume.Length)
                return;

            volume = System.Math.Max(0, System.Math.Min(100, volume));

            if (volume <= 0)
            {
                _settings.DefaultMuted[tuner] = true;
                muted[tuner] = true;
            }
            else
            {
                _settings.DefaultMuted[tuner] = false;
                _settings.DefaultVolume[tuner] = (uint)volume;
                preMute[tuner] = volume;
                muted[tuner] = false;
            }

            if (_media_player != null && tuner < _media_player.Length && _media_player[tuner] != null)
            {
                try { _media_player[tuner].SetVolume(volume); } catch { }
            }

            try { _settingsManager.SaveSettings(_settings); } catch { }
        }

        public int ModernGetVolume(int tuner)
        {
            if (_settings == null || tuner < 0 || tuner >= _settings.DefaultVolume.Length)
                return 0;

            if (_media_player != null && tuner < _media_player.Length && _media_player[tuner] != null)
            {
                try { return _media_player[tuner].GetVolume(); } catch { }
            }

            return _settings.DefaultMuted[tuner] ? 0 : (int)_settings.DefaultVolume[tuner];
        }

        public void ModernEnsureAudible(int tuner, int fallbackVolume)
        {
            if (tuner < 0 || tuner >= _settings.DefaultVolume.Length)
                return;

            int volume = ModernGetVolume(tuner);
            if (volume <= 0)
                volume = fallbackVolume;

            ModernSetVolume(tuner, volume);
        }

        // PicoTuner status packets teach the modern build the receiver's real IP address.
        // The source's normal Initialize() sends its default tune commands before that first
        // status packet can arrive, so those initial commands can go to a stale configured
        // address. Once status is flowing, send the exact same current tuner setup again.
        public void ModernReapplyStartupTune()
        {
            if (!ModernIsPicoTunerEthernet)
                return;

            int count = System.Math.Min(2, _current_frequency.Length);
            for (int tuner = 0; tuner < count; tuner++)
            {
                if (_current_frequency[tuner] <= 0 || _current_sr[tuner] <= 0)
                    continue;

                Serilog.Log.Information(
                    "Reapplying PicoTuner startup tune T{Tuner}: {Frequency} kHz / {SymbolRate} kS",
                    tuner + 1, _current_frequency[tuner], _current_sr[tuner]);

                UDPSetFrequency(tuner, _current_frequency[tuner], _current_sr[tuner]);
            }
        }
    }
}
