using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace opentuner
{
    public partial class frequencyManagerForm : Form
    {
        private List<StoredFrequency> stored_frequencies = null;

        public frequencyManagerForm(List<StoredFrequency> _stored_frequencies)
        {
            InitializeComponent();
            ApplyModernPresetStyle();

            stored_frequencies = _stored_frequencies;
            load_frequencies();
        }

        private void ApplyModernPresetStyle()
        {
            ModernTheme.ApplyToForm(this);
            Text = "OpenTuner Frequency Presets";
            BackColor = ModernTheme.Background;
            ForeColor = ModernTheme.TextPrimary;
            Font = ModernTheme.FontBody;
            MinimumSize = new Size(560, 400);

            listFreq.BackColor = ModernTheme.Surface;
            listFreq.ForeColor = ModernTheme.TextPrimary;
            listFreq.BorderStyle = BorderStyle.FixedSingle;
            listFreq.Font = ModernTheme.FontBodySemibold;

            Label[] values = { lblFreq, lblName, lblOffset, lblSymbolRate, lblRFInput };
            foreach (Label value in values)
            {
                value.ForeColor = ModernTheme.TextPrimary;
                value.Font = ModernTheme.FontStatus;
            }

            btnAdd.BackColor = ModernTheme.Accent;
            btnAdd.FlatAppearance.BorderColor = ModernTheme.Accent;
            btnAdd.FlatAppearance.MouseOverBackColor = ModernTheme.AccentHover;
            btnAdd.ForeColor = ModernTheme.TextPrimary;
            btnAdd.Font = ModernTheme.FontBodySemibold;

            btnDelete.ForeColor = ModernTheme.Danger;
        }

        public void load_frequencies()
        {
            listFreq.Items.Clear();

            lblFreq.Text = "";
            lblName.Text = "";
            lblOffset.Text = "";
            lblSymbolRate.Text = "";
            lblRFInput.Text = "";

            for (int c = 0; c < stored_frequencies.Count; c++)
                listFreq.Items.Add(stored_frequencies[c].Name);

            if (listFreq.Items.Count > 0)
                listFreq.SelectedIndex = 0;
        }

        public void show_frequency(int index)
        {
            if (index < 0 || index >= stored_frequencies.Count)
                return;

            lblFreq.Text = FormatStoredMHz(stored_frequencies[index].Frequency) + " MHz";
            lblName.Text = stored_frequencies[index].Name.ToString();
            lblOffset.Text = FormatStoredMHz(stored_frequencies[index].Offset) + " MHz";
            lblSymbolRate.Text = stored_frequencies[index].SymbolRate.ToString() + " kS";
            lblRFInput.Text = stored_frequencies[index].RFInput == 1 ? "A" : "B";
        }

        private void listFreq_SelectedIndexChanged(object sender, EventArgs e)
        {
            show_frequency(listFreq.SelectedIndex);
        }

        private void btnClose_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void btnDelete_Click(object sender, EventArgs e)
        {
            if (listFreq.SelectedIndex > -1)
            {
                if (MessageBox.Show("Are you sure you want to delete '" + stored_frequencies[listFreq.SelectedIndex].Name + "'?", "Confirmation", MessageBoxButtons.YesNo) == DialogResult.Yes)
                {
                    stored_frequencies.RemoveAt(listFreq.SelectedIndex);
                    load_frequencies();
                }
            }
        }

        private void btnEdit_Click(object sender, EventArgs e)
        {
            if (listFreq.SelectedIndex > -1)
            {
                int index = listFreq.SelectedIndex;

                editStoredFrequencyForm editForm = new editStoredFrequencyForm();
                ModernTheme.ApplyToForm(editForm);
                editForm.txtName.Text = stored_frequencies[index].Name;
                editForm.txtFreq.Text = FormatStoredMHz(stored_frequencies[index].Frequency);
                editForm.txtOffset.Text = FormatStoredMHz(stored_frequencies[index].Offset);
                editForm.txtSR.Text = stored_frequencies[index].SymbolRate.ToString();
                editForm.comboRFInput.SelectedIndex = stored_frequencies[index].RFInput - 1;

                if (editForm.ShowDialog() == DialogResult.OK)
                {
                    uint frequencyValue;
                    uint offsetValue;
                    uint symbolRate;
                    if (!TryParsePresetValue(editForm.txtFreq.Text, false, out frequencyValue) ||
                        !TryParsePresetValue(editForm.txtOffset.Text, true, out offsetValue) ||
                        !uint.TryParse(editForm.txtSR.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out symbolRate))
                    {
                        MessageBox.Show("Please enter frequency/LO in MHz. Whole values such as 10491 or decimal values such as 10491.500 are both accepted. SR must be a whole number in kS.", "Invalid preset values", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    stored_frequencies[index].Name = editForm.txtName.Text;
                    stored_frequencies[index].Frequency = frequencyValue;
                    stored_frequencies[index].Offset = offsetValue;
                    stored_frequencies[index].SymbolRate = symbolRate;
                    stored_frequencies[index].RFInput = Convert.ToByte(editForm.comboRFInput.SelectedIndex + 1);
                    load_frequencies();
                }
            }
        }

        private void btnAdd_Click(object sender, EventArgs e)
        {
            editStoredFrequencyForm editForm = new editStoredFrequencyForm();
            ModernTheme.ApplyToForm(editForm);

            if (editForm.ShowDialog() == DialogResult.OK)
            {
                uint frequencyValue;
                uint offsetValue;
                uint symbolRate;
                if (!TryParsePresetValue(editForm.txtFreq.Text, false, out frequencyValue) ||
                    !TryParsePresetValue(editForm.txtOffset.Text, true, out offsetValue) ||
                    !uint.TryParse(editForm.txtSR.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out symbolRate))
                {
                    MessageBox.Show("Please enter frequency/LO in MHz. Whole values such as 10491 or decimal values such as 10491.500 are both accepted. SR must be a whole number in kS.", "Invalid preset values", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                StoredFrequency sf = new StoredFrequency();
                sf.Name = editForm.txtName.Text;
                sf.Frequency = frequencyValue;
                sf.Offset = offsetValue;
                sf.SymbolRate = symbolRate;
                sf.RFInput = Convert.ToByte(editForm.comboRFInput.SelectedIndex + 1);
                stored_frequencies.Add(sf);
                load_frequencies();
            }
        }

        // Preserve the original whole-number preset convention (for example 10491),
        // while also accepting decimal MHz entries such as 10491.500. Decimal entries
        // are stored in kHz so no precision is lost; the modern preset loader already
        // understands both representations.
        private static bool TryParsePresetValue(string text, bool allowZero, out uint storedValue)
        {
            storedValue = 0;
            string raw = (text ?? string.Empty).Trim();
            string normalised = raw.Replace(',', '.');
            decimal mhz;
            if (!decimal.TryParse(normalised, NumberStyles.Number, CultureInfo.InvariantCulture, out mhz))
                return false;
            if (mhz < 0 || (!allowZero && mhz == 0) || mhz > 15000M)
                return false;

            bool hasDecimal = normalised.IndexOf('.') >= 0;
            decimal value = hasDecimal ? decimal.Round(mhz * 1000M, 0, MidpointRounding.AwayFromZero) : mhz;
            if (value < 0 || value > uint.MaxValue)
                return false;

            storedValue = (uint)value;
            return true;
        }

        private static string FormatStoredMHz(uint storedValue)
        {
            if (storedValue == 0)
                return "0";
            if (storedValue <= 15000)
                return storedValue.ToString(CultureInfo.InvariantCulture);
            return (storedValue / 1000M).ToString("0.###", CultureInfo.InvariantCulture);
        }

        private void frequencyManagerForm_Load(object sender, EventArgs e)
        {
        }
    }
}