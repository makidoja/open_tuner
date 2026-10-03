using System;
using System.Globalization;
using System.Reflection;
using System.Windows.Forms;

namespace opentuner
{
    internal static class ModernPresetUiSync
    {
        private static bool attached;

        public static void Attach(ModernConceptForm form)
        {
            if (attached || form == null) return;
            attached = true;

            FieldInfo backendField = typeof(ModernConceptForm).GetField("backend", BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo freqField = typeof(ModernConceptForm).GetField("freqInputs", BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo srField = typeof(ModernConceptForm).GetField("srInputs", BindingFlags.Instance | BindingFlags.NonPublic);

            MainForm backend = backendField == null ? null : backendField.GetValue(form) as MainForm;
            NumericUpDown[] freqInputs = freqField == null ? null : freqField.GetValue(form) as NumericUpDown[];
            NumericUpDown[] srInputs = srField == null ? null : srField.GetValue(form) as NumericUpDown[];
            if (backend == null || freqInputs == null || srInputs == null) return;

            backend.BackendPresetLoaded += delegate(int tuner, uint frequencyKHz, uint symbolRateKs)
            {
                if (form.IsDisposed || tuner < 0 || tuner >= freqInputs.Length || tuner >= srInputs.Length) return;

                MethodInvoker apply = delegate
                {
                    if (freqInputs[tuner] != null)
                    {
                        decimal mhz = frequencyKHz / 1000M;
                        freqInputs[tuner].Value = Clamp(freqInputs[tuner], mhz);
                    }

                    if (srInputs[tuner] != null)
                        srInputs[tuner].Value = Clamp(srInputs[tuner], symbolRateKs);

                    // Update the visible modern FREQ box immediately rather than waiting
                    // for the hardware UI refresh timer to mirror the hidden control.
                    Control[] visibleFreq = form.Controls.Find("ModernFrequencyText" + tuner, true);
                    if (visibleFreq.Length > 0 && visibleFreq[0] is TextBox text)
                        text.Text = (frequencyKHz / 1000M).ToString("0.000", CultureInfo.InvariantCulture);

                    // The SR combo is already wired to the hidden NumericUpDown ValueChanged
                    // event, but set it explicitly as well so a preset recall is instant.
                    Control[] visibleSr = form.Controls.Find("ModernSrCombo" + tuner, true);
                    if (visibleSr.Length > 0 && visibleSr[0] is ComboBox combo)
                    {
                        int sr = checked((int)symbolRateKs);
                        for (int i = 0; i < combo.Items.Count; i++)
                        {
                            if (Convert.ToInt32(combo.Items[i]) == sr)
                            {
                                combo.SelectedIndex = i;
                                break;
                            }
                        }
                    }
                };

                if (form.InvokeRequired) form.BeginInvoke(apply);
                else apply();
            };
        }

        private static decimal Clamp(NumericUpDown input, decimal value)
        {
            return Math.Min(input.Maximum, Math.Max(input.Minimum, value));
        }
    }
}
