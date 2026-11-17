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
            // Offsets are stored in kHz but shown and entered in MHz.
            txtTuner1FreqOffset.Text = KHzToMHzText(_settings.Offset1);
            txtTuner2FreqOffset.Text = KHzToMHzText(_settings.Offset2);
            comboSupplyADefault.SelectedIndex = _settings.DefaultLnbASupply;
            comboSupplyBDefault.SelectedIndex = _settings.DefaultLnbBSupply;
            ComboDefaultRFInput.SelectedIndex = _settings.DefaultRFInput;

            // These are start-up defaults only. While running, LO and LNB supply
            // are changed live from the main window's LO box / L tick and LNB
            // selectors, which do not write back here.
            groupBox1.Text = "Default Tuner Properties";
            label3.Text = "Tuner 1 LO (MHz):";
            label4.Text = "Tuner 2 LO (MHz):";
        }

        private static string KHzToMHzText(uint kHz)
        {
            return (kHz / 1000M).ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) + " MHz";
        }

        // Accepts "9750 MHz", "9750", "9750.0" or "9750,0"; 0 means no LO (direct).
        private static bool TryParseMHzToKHz(string text, out uint kHz)
        {
            kHz = 0;
            decimal mhz;
            string s = (text ?? string.Empty).Trim();
            if (s.EndsWith("MHz", StringComparison.OrdinalIgnoreCase))
                s = s.Substring(0, s.Length - 3).Trim();
            s = s.Replace(',', '.');
            if (!decimal.TryParse(s, System.Globalization.NumberStyles.Number,
                    System.Globalization.CultureInfo.InvariantCulture, out mhz))
                return false;
            if (mhz < 0 || mhz > 15000) return false;
            kHz = (uint)Math.Round(mhz * 1000M);
            return true;
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }

        private void btnSave_Click(object sender, EventArgs e)
        {
            uint offset1 = 0;
            if (!TryParseMHzToKHz(txtTuner1FreqOffset.Text, out offset1))
            {
                MessageBox.Show("Invalid Tuner 1 LO. Enter MHz, e.g. 9750 (or 0 for none).");
                return;
            }

            uint offset2 = 0;
            if (!TryParseMHzToKHz(txtTuner2FreqOffset.Text, out offset2))
            {
                MessageBox.Show("Invalid Tuner 2 LO. Enter MHz, e.g. 9750 (or 0 for none).");
                return;
            }

            _settings.DefaultInterface = (byte)comboHardwareInterface.SelectedIndex;
            _settings.DefaultLnbASupply = (byte)comboSupplyADefault.SelectedIndex;
            _settings.DefaultLnbBSupply = (byte)comboSupplyBDefault.SelectedIndex;
            _settings.DefaultRFInput = (byte)ComboDefaultRFInput.SelectedIndex;

            _settings.Offset1 = offset1;
            _settings.Offset2 = offset2;

            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
