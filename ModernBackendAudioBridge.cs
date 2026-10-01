using System;
using opentuner.MediaSources.WinterHill;

namespace opentuner
{
    public partial class MainForm
    {
        public bool BackendSourceInitialised
        {
            get { return source_connected && videoSource != null; }
        }

        public void BackendSetVolume(int tuner, int volume)
        {
            volume = Math.Max(0, Math.Min(100, volume));

            WinterHillSource winterHill = videoSource as WinterHillSource;
            if (winterHill != null)
            {
                winterHill.ModernSetVolume(tuner, volume);
                return;
            }

            if (_mediaPlayers == null || tuner < 0 || tuner >= _mediaPlayers.Count || _mediaPlayers[tuner] == null)
                return;

            try { _mediaPlayers[tuner].SetVolume(volume); }
            catch { }
        }

        public int BackendGetVolume(int tuner)
        {
            WinterHillSource winterHill = videoSource as WinterHillSource;
            if (winterHill != null)
                return winterHill.ModernGetVolume(tuner);

            if (_mediaPlayers == null || tuner < 0 || tuner >= _mediaPlayers.Count || _mediaPlayers[tuner] == null)
                return 0;

            try { return _mediaPlayers[tuner].GetVolume(); }
            catch { return 0; }
        }

        public void BackendEnsureAudible(int tuner)
        {
            WinterHillSource winterHill = videoSource as WinterHillSource;
            if (winterHill != null)
            {
                winterHill.ModernEnsureAudible(tuner, 60);
                return;
            }

            int current = BackendGetVolume(tuner);
            if (current <= 0)
                BackendSetVolume(tuner, 60);
        }
    }
}
