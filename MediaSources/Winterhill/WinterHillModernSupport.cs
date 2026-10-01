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
    }
}
