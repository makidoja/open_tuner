using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace opentuner.MediaSources.Minitiouner
{
    public partial class MinitiounerSettingsForm : Form
    {
        private MinitiounerSettings _settings;
        public MinitiounerSettingsForm(ref MinitiounerSettings Settings)
        {
            InitializeComponent();
            _settings = Settings;

            comboHardwareInterface.SelectedIndex = _settings.DefaultInterface;
            txtTuner1FreqOffset.Text = _settings.Offset1.ToString();
            txtTuner2FreqOffset.Text = _settings.Offset2.ToString();
            comboSupplyADefault.SelectedIndex = _settings.DefaultLnbASupply;
            comboSupplyBDefault.SelectedIndex = _settings.DefaultLnbBSupply;
            ComboDefaultRFInput.SelectedIndex = _settings.DefaultRFInput;

            HideMainWindowSettings();
        }

        // LO offsets and LNB supply are set in one place: the main window's
        // LO box / L tick and LNB selectors, which save to these same settings.
        // Hide the duplicate fields here and close up the gap they leave.
        private void HideMainWindowSettings()
        {
            Control[] hidden =
            {
                label3, txtTuner1FreqOffset, label4, txtTuner2FreqOffset,
                label5, comboSupplyADefault, label6, comboSupplyBDefault
            };
            foreach (Control c in hidden) c.Visible = false;

            int shift = ComboDefaultRFInput.Top - txtTuner1FreqOffset.Top;
            label7.Top -= shift;
            ComboDefaultRFInput.Top -= shift;
            groupBox1.Height -= shift;
            btnSave.Top -= shift;
            btnCancel.Top -= shift;
            ClientSize = new System.Drawing.Size(ClientSize.Width, ClientSize.Height - shift);
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }

        private void btnSave_Click(object sender, EventArgs e)
        {
            // Offsets and LNB supply defaults are owned by the main window
            // (see HideMainWindowSettings); leave the saved values untouched.
            _settings.DefaultInterface = (byte)comboHardwareInterface.SelectedIndex;
            _settings.DefaultRFInput = (byte)ComboDefaultRFInput.SelectedIndex;

            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
