using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;
using opentuner.ExtraFeatures.BATCWebchat;

namespace opentuner
{
    public partial class MainForm
    {
        public void BackendShowBatcChat()
        {
            if (videoSource == null)
            {
                MessageBox.Show("Connect a receiver source before opening BATC Chat.", "OpenTuner - BATC Chat", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                if (batc_chat == null)
                    batc_chat = new BATCChat(videoSource);

                batc_chat.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show("BATC Chat could not be opened.\r\n\r\n" + ex.Message, "OpenTuner - BATC Chat", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        public void BackendShowPresets()
        {
            BackendManagePresets();
        }

        public void BackendShowSpectrum()
        {
            try
            {
                if (ExtraToolsTab != null && ExtraSpectrumTab != null)
                    ExtraToolsTab.SelectedTab = ExtraSpectrumTab;
            }
            catch { }
        }

        public void BackendShowExternalTools()
        {
            try { ToggleExtraToolPanel(false); } catch { }
        }

        public void BackendOpenRecordingsFolder()
        {
            OpenMediaFolder(_settings == null ? null : _settings.media_video_path, "recordings");
        }

        public void BackendOpenSnapshotsFolder()
        {
            string path = _settings == null ? null : _settings.media_path;
            if (string.IsNullOrWhiteSpace(path) && _settings != null)
                path = _settings.media_video_path;
            OpenMediaFolder(path, "snapshots");
        }

        private void OpenMediaFolder(string path, string description)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
                {
                    MessageBox.Show("The " + description + " folder is not configured or does not exist. Set the media paths in SETTINGS.", "OpenTuner", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                Process.Start(new ProcessStartInfo("explorer.exe", "\"" + path + "\"") { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not open the " + description + " folder.\r\n\r\n" + ex.Message, "OpenTuner", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
