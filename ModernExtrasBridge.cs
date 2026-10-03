using System;
using System.Diagnostics;
using System.Drawing;
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
            var presets = BackendPresets();
            if (presets == null || presets.Count == 0)
            {
                if (MessageBox.Show("There are no saved presets yet.\r\n\r\nOpen the preset manager now?", "OpenTuner - Presets", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
                    BackendManagePresets();
                return;
            }

            using (Form dialog = new Form())
            {
                dialog.Text = "Load Preset";
                dialog.StartPosition = FormStartPosition.CenterParent;
                dialog.FormBorderStyle = FormBorderStyle.FixedDialog;
                dialog.MinimizeBox = false;
                dialog.MaximizeBox = false;
                dialog.ShowInTaskbar = false;
                dialog.ClientSize = new Size(560, 410);
                dialog.BackColor = Color.FromArgb(13, 28, 45);
                dialog.ForeColor = Color.FromArgb(242, 247, 252);
                dialog.Font = new Font("Segoe UI", 9f);

                Label heading = new Label
                {
                    Text = "Select a preset and the receiver window to load it into",
                    Location = new Point(16, 14),
                    AutoSize = true,
                    Font = new Font("Segoe UI Semibold", 10f),
                    ForeColor = dialog.ForeColor
                };
                dialog.Controls.Add(heading);

                RadioButton tunerA = new RadioButton
                {
                    Text = "Receiver 1 / Tuner A",
                    Location = new Point(18, 45),
                    AutoSize = true,
                    Checked = true,
                    ForeColor = dialog.ForeColor
                };
                RadioButton tunerB = new RadioButton
                {
                    Text = "Receiver 2 / Tuner B",
                    Location = new Point(180, 45),
                    AutoSize = true,
                    ForeColor = dialog.ForeColor
                };
                dialog.Controls.Add(tunerA);
                dialog.Controls.Add(tunerB);

                ListBox list = new ListBox
                {
                    Location = new Point(18, 76),
                    Size = new Size(524, 262),
                    BackColor = Color.FromArgb(18, 38, 60),
                    ForeColor = dialog.ForeColor,
                    BorderStyle = BorderStyle.FixedSingle,
                    Font = new Font("Consolas", 9f)
                };

                foreach (StoredFrequency p in presets)
                {
                    uint displayFrequencyKHz = NormalisePresetFrequencyKHz(p.Frequency);
                    string rf = displayFrequencyKHz >= 10000000
                        ? (displayFrequencyKHz / 1000M).ToString("0.000") + " MHz RF"
                        : (displayFrequencyKHz / 1000M).ToString("0.000") + " MHz";
                    string input = p.RFInput > 0 ? "  IN " + (p.RFInput == 1 ? "A" : "B") : "";
                    list.Items.Add(p.Name + "   |   " + rf + "   |   " + p.SymbolRate + " kS" + input);
                }
                if (list.Items.Count > 0) list.SelectedIndex = 0;
                dialog.Controls.Add(list);

                Button manage = new Button
                {
                    Text = "MANAGE PRESETS",
                    Location = new Point(18, 355),
                    Size = new Size(132, 34),
                    FlatStyle = FlatStyle.Flat,
                    BackColor = Color.FromArgb(18, 38, 60),
                    ForeColor = dialog.ForeColor
                };
                manage.FlatAppearance.BorderColor = Color.FromArgb(37, 67, 94);
                manage.Click += delegate
                {
                    dialog.Close();
                    BackendManagePresets();
                };
                dialog.Controls.Add(manage);

                Button load = new Button
                {
                    Text = "LOAD PRESET",
                    DialogResult = DialogResult.OK,
                    Location = new Point(406, 355),
                    Size = new Size(136, 34),
                    FlatStyle = FlatStyle.Flat,
                    BackColor = Color.FromArgb(28, 139, 253),
                    ForeColor = Color.White
                };
                load.FlatAppearance.BorderColor = Color.FromArgb(28, 139, 253);
                dialog.Controls.Add(load);
                dialog.AcceptButton = load;

                list.DoubleClick += delegate
                {
                    if (list.SelectedIndex >= 0)
                    {
                        dialog.DialogResult = DialogResult.OK;
                        dialog.Close();
                    }
                };

                if (dialog.ShowDialog() != DialogResult.OK || list.SelectedIndex < 0)
                    return;

                int tuner = tunerB.Checked ? 1 : 0;
                StoredFrequency preset = presets[list.SelectedIndex];

                uint frequencyKHz = NormalisePresetFrequencyKHz(preset.Frequency);
                uint offsetKHz = NormalisePresetOffsetKHz(preset.Offset);

                if (offsetKHz <= 15000000)
                    BackendSetOffset(tuner, offsetKHz);
                if (preset.RFInput == 1 || preset.RFInput == 2)
                    BackendSetRfInput(tuner, preset.RFInput - 1);

                BackendTune(tuner, frequencyKHz, preset.SymbolRate);
            }
        }

        // Older preset dialogs store values in kHz, but the modern UI naturally
        // encourages users to type values such as 1310 and 9750 as MHz. Accept
        // both forms so existing presets and newly-entered modern presets work.
        private static uint NormalisePresetFrequencyKHz(uint value)
        {
            if (value >= 400 && value <= 15000)
                return checked(value * 1000U);
            return value;
        }

        private static uint NormalisePresetOffsetKHz(uint value)
        {
            if (value > 0 && value <= 15000)
                return checked(value * 1000U);
            return value;
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
