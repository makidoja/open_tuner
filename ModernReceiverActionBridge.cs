using System;
using System.IO;
using System.Windows.Forms;

namespace opentuner
{
    public partial class MainForm
    {
        public bool BackendToggleRecording(int tuner)
        {
            if (_ts_recorders == null || tuner < 0 || tuner >= _ts_recorders.Count || _ts_recorders[tuner] == null)
                return false;

            _ts_recorders[tuner].record = !_ts_recorders[tuner].record;
            return _ts_recorders[tuner].record;
        }

        public bool BackendIsRecording(int tuner)
        {
            return _ts_recorders != null && tuner >= 0 && tuner < _ts_recorders.Count &&
                   _ts_recorders[tuner] != null && _ts_recorders[tuner].record;
        }

        public string BackendTakeSnapshot(int tuner)
        {
            if (_mediaPlayers == null || tuner < 0 || tuner >= _mediaPlayers.Count || _mediaPlayers[tuner] == null)
                return null;

            string folder = _settings == null ? null : _settings.media_path;
            if (string.IsNullOrWhiteSpace(folder) && _settings != null)
                folder = _settings.media_video_path;
            if (string.IsNullOrWhiteSpace(folder))
                folder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "snapshots");

            Directory.CreateDirectory(folder);
            string file = Path.Combine(folder, DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss") + "_T" + (tuner + 1) + ".png");
            _mediaPlayers[tuner].SnapShot(file);
            return file;
        }
    }
}
