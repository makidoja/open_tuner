using System;

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
            if (_mediaPlayers == null || tuner < 0 || tuner >= _mediaPlayers.Count || _mediaPlayers[tuner] == null)
                return;

            volume = Math.Max(0, Math.Min(100, volume));
            try { _mediaPlayers[tuner].SetVolume(volume); }
            catch { }
        }

        public int BackendGetVolume(int tuner)
        {
            if (_mediaPlayers == null || tuner < 0 || tuner >= _mediaPlayers.Count || _mediaPlayers[tuner] == null)
                return 0;

            try { return _mediaPlayers[tuner].GetVolume(); }
            catch { return 0; }
        }

        public void BackendEnsureAudible(int tuner)
        {
            int current = BackendGetVolume(tuner);
            if (current <= 0)
                BackendSetVolume(tuner, 60);
        }
    }
}
