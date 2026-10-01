using System;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using opentuner.ExtraFeatures.BATCSpectrum;

namespace opentuner
{
    internal static class ModernConceptRuntimeFixes
    {
        private static bool spectrumResizeHooked;
        private static bool quickTuneHooked;

        public static void Attach(ModernConceptForm form)
        {
            if (form == null) return;

            // Apply the compact layout before BATCSpectrum is created on Form.Shown so
            // its drawing surface is created at the intended size rather than a stretched
            // 1080p-wide size.
            ApplyCompactSpectrumLayout(form);

            form.Shown += delegate
            {
                ApplyCompactSpectrumLayout(form);
                HookQuickTune(form);
            };

            form.Resize += delegate { ApplyCompactSpectrumLayout(form); };
        }

        private static void ApplyCompactSpectrumLayout(ModernConceptForm form)
        {
            try
            {
                FieldInfo boxField = typeof(ModernConceptForm).GetField("batcSpectrumBox", BindingFlags.Instance | BindingFlags.NonPublic);
                PictureBox box = boxField == null ? null : boxField.GetValue(form) as PictureBox;
                if (box == null || box.Parent == null) return;

                Panel card = box.Parent as Panel;
                if (card == null) return;

                card.Height = 174;

                Action sizeSpectrum = delegate
                {
                    int width = Math.Min(922, Math.Max(200, card.ClientSize.Width - 20));
                    int left = Math.Max(10, (card.ClientSize.Width - width) / 2);
                    box.Location = new Point(left, 34);
                    box.Size = new Size(width, 126);
                    box.Anchor = AnchorStyles.Top;
                    box.SizeMode = PictureBoxSizeMode.Normal;
                };

                sizeSpectrum();

                if (!spectrumResizeHooked)
                {
                    card.Resize += delegate { sizeSpectrum(); };
                    spectrumResizeHooked = true;
                }
            }
            catch
            {
            }
        }

        private static void HookQuickTune(ModernConceptForm form)
        {
            if (quickTuneHooked) return;

            try
            {
                FieldInfo spectrumField = typeof(ModernConceptForm).GetField("batcSpectrum", BindingFlags.Instance | BindingFlags.NonPublic);
                BATCSpectrum spectrum = spectrumField == null ? null : spectrumField.GetValue(form) as BATCSpectrum;
                if (spectrum == null) return;

                FieldInfo backendField = typeof(ModernConceptForm).GetField("backend", BindingFlags.Instance | BindingFlags.NonPublic);
                MainForm backend = backendField == null ? null : backendField.GetValue(form) as MainForm;

                FieldInfo freqField = typeof(ModernConceptForm).GetField("freqInputs", BindingFlags.Instance | BindingFlags.NonPublic);
                NumericUpDown[] freqInputs = freqField == null ? null : freqField.GetValue(form) as NumericUpDown[];

                FieldInfo srField = typeof(ModernConceptForm).GetField("srInputs", BindingFlags.Instance | BindingFlags.NonPublic);
                NumericUpDown[] srInputs = srField == null ? null : srField.GetValue(form) as NumericUpDown[];

                if (backend == null || freqInputs == null || srInputs == null) return;

                BATCSpectrum.SignalSelected handler = delegate(int receiver, uint freq, uint sr)
                {
                    if (form.IsDisposed) return;

                    form.BeginInvoke((MethodInvoker)delegate
                    {
                        int tuner = Math.Max(0, Math.Min(1, receiver));
                        uint srKs = sr > 5000 ? sr / 1000 : sr;

                        decimal mhz = freq / 1000M;
                        if (mhz >= freqInputs[tuner].Minimum && mhz <= freqInputs[tuner].Maximum)
                            freqInputs[tuner].Value = mhz;

                        if (srKs >= srInputs[tuner].Minimum && srKs <= srInputs[tuner].Maximum)
                            srInputs[tuner].Value = srKs;

                        backend.BackendTune(tuner, freq, srKs);
                    });
                };

                // Replace the concept form's original callback. Its symbol-rate handling
                // treated BATC's symbols/sec value as kS, which meant the selected SR was
                // rejected and the receiver could be retuned with the previous SR.
                FieldInfo eventField = typeof(BATCSpectrum).GetField("OnSignalSelected", BindingFlags.Instance | BindingFlags.NonPublic);
                if (eventField != null)
                    eventField.SetValue(spectrum, handler);
                else
                    spectrum.OnSignalSelected += handler;

                quickTuneHooked = true;
            }
            catch
            {
            }
        }
    }
}