using System;
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
                {
                    ExtraToolsTab.SelectedTab = ExtraSpectrumTab;
                }
            }
            catch { }
        }

        public void BackendShowExternalTools()
        {
            try { ToggleExtraToolPanel(false); } catch { }
        }
    }
}
