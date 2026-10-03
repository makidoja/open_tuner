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

            lblFreq.Text = stored_frequencies[index].Frequency.ToString() + " kHz";
            lblName.Text = stored_frequencies[index].Name.ToString();
            lblOffset.Text = stored_frequencies[index].Offset.ToString() + " kHz";
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
                editForm.txtName.Text = (stored_frequencies[index].Frequency / 1000M).ToString("0.###", CultureInfo.InvariantCulture);
                editForm.txtFreq.Text = (stored_frequencies[index].Frequency / 1000M).ToString("0.###", CultureInfo.InvariantCulture);
                editForm.txtOffset.Text = (stored_frequencies[index].Offset / 1000M).ToString("0.###", CultureInfo.InvariantCulture);
                editForm.txtSR.Text = stored_frequencies[index].SymbolRate.ToString();
                editForm.comboRFInput.SelectedIndex = stored_frequencies[index].RFInput - 1;
                editForm.txtName.Text = stored_frequencies[index].Name;

                if (editForm.ShowDialog() == DialogResult.OK)
                {
                    uint frequencyKHz;
                    uint offsetKHz;
                    uint symbolRate;
                    if (!TryParsePresetMHz(editForm.txtFreq.Text, false, out frequencyKHz) ||
                        !TryParsePresetMHz(editForm.txtOffset.Text, true, out offsetKHz) ||
                        !uint.TryParse(editForm.txtSR.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out symbolRate))
                    {
                        MessageBox.Show("Please enter frequency and LO offset in MHz (for example 10491.500 and 9750) and SR as a whole number in kS.", "Invalid preset values", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    stored_frequencies[index].Name = editForm.txtName.Text;
                    stored_frequencies[index].Frequency = frequencyKHz;
                    stored_frequencies[index].Offset = offsetKHz;
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
                uint frequencyKHz;
                uint offsetKHz;
                uint symbolRate;
                if (!TryParsePresetMHz(editForm.txtFreq.Text, false, out frequencyKHz) ||
                    !TryParsePresetMHz(editForm.txtOffset.Text, true, out offsetKHz) ||
                    !uint.TryParse(editForm.txtSR.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out symbolRate))
                {
                    MessageBox.Show("Please enter frequency and LO offset in MHz (for example 10491.500 and 9750) and SR as a whole number in kS.", "Invalid preset values", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                StoredFrequency sf = new StoredFrequency();
                sf.Name = editForm.txtName.Text;
                sf.Frequency = frequencyKHz;
                sf.Offset = offsetKHz;
                sf.SymbolRate = symbolRate;
                sf.RFInput = Convert.ToByte(editForm.comboRFInput.SelectedIndex + 1);
                stored_frequencies.Add(sf);
                load_frequencies();
            }
        }

        private static bool TryParsePresetMHz(string text, bool allowZero, out uint valueKHz)
        {
            valueKHz = 0;
            string value = (text ?? string.Empty).Trim().Replace(',', '.');
            decimal mhz;
            if (!decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out mhz))
                return false;
            if (mhz < 0 || (!allowZero && mhz == 0) || mhz > 15000M)
                return false;

            decimal khz = decimal.Round(mhz * 1000M, 0, MidpointRounding.AwayFromZero);
            if (khz > uint.MaxValue)
                return false;
            valueKHz = (uint)khz;
            return true;
        }

        private void frequencyManagerForm_Load(object sender, EventArgs e)
        {
        }
    }
}