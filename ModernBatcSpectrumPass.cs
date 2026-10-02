using System;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using opentuner.ExtraFeatures.BATCSpectrum;

namespace opentuner
{
    internal static class ModernBatcSpectrumPass
    {
        private static bool attached;
        private static ModernBatcSpectrumControl display;

        public static void Attach(ModernConceptForm form)
        {
            if (attached || form == null) return;
            attached = true;
            form.Shown += delegate { ReplaceLegacySpectrum(form); };
        }

        private static T GetField<T>(ModernConceptForm form, string name) where T : class
        {
            FieldInfo field = typeof(ModernConceptForm).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            return field == null ? null : field.GetValue(form) as T;
        }

        private static void ReplaceLegacySpectrum(ModernConceptForm form)
        {
            FieldInfo boxField = typeof(ModernConceptForm).GetField("batcSpectrumBox", BindingFlags.Instance | BindingFlags.NonPublic);
            PictureBox legacyBox = boxField == null ? null : boxField.GetValue(form) as PictureBox;
            if (legacyBox == null || legacyBox.Parent == null) return;

            Panel card = legacyBox.Parent as Panel;
            if (card == null) return;

            FieldInfo oldSpectrumField = typeof(ModernConceptForm).GetField("batcSpectrum", BindingFlags.Instance | BindingFlags.NonPublic);
            BATCSpectrum oldSpectrum = oldSpectrumField == null ? null : oldSpectrumField.GetValue(form) as BATCSpectrum;

            try { oldSpectrum?.Close(); } catch { }
            try { if (oldSpectrumField != null) oldSpectrumField.SetValue(form, null); } catch { }

            try
            {
                Image oldImage = legacyBox.Image;
                legacyBox.Image = null;
                oldImage?.Dispose();
            }
            catch { }

            try { card.Controls.Remove(legacyBox); } catch { }
            try { legacyBox.Dispose(); } catch { }

            PictureBox compatibilityBox = new PictureBox
            {
                Name = "LegacySpectrumCompatibilityPlaceholder",
                Visible = false,
                Enabled = false,
                Size = new Size(1, 1),
                Location = new Point(0, 0)
            };
            try { boxField?.SetValue(form, compatibilityBox); } catch { }

            MainForm backend = GetField<MainForm>(form, "backend");
            NumericUpDown[] freqInputs = GetField<NumericUpDown[]>(form, "freqInputs");
            NumericUpDown[] srInputs = GetField<NumericUpDown[]>(form, "srInputs");
            if (backend == null || freqInputs == null || srInputs == null) return;

            display = new ModernBatcSpectrumControl
            {
                Name = "ModernBatcSpectrum",
                BackColor = Color.FromArgb(13, 28, 45),
                Location = new Point(12, 31),
                Size = new Size(Math.Min(1140, Math.Max(300, card.ClientSize.Width - 24)), 108),
                Anchor = AnchorStyles.Top
            };

            display.OnSignalSelected += delegate(int receiver, uint freqKHz, uint srKs)
            {
                if (form.IsDisposed) return;
                form.BeginInvoke((MethodInvoker)delegate
                {
                    int tuner = Math.Max(0, Math.Min(1, receiver));
                    decimal mhz = freqKHz / 1000M;
                    if (mhz >= freqInputs[tuner].Minimum && mhz <= freqInputs[tuner].Maximum)
                        freqInputs[tuner].Value = mhz;
                    if (srKs >= srInputs[tuner].Minimum && srKs <= srInputs[tuner].Maximum)
                        srInputs[tuner].Value = srKs;
                    backend.BackendTune(tuner, freqKHz, srKs);
                });
            };

            card.Controls.Add(display);
            LayoutDisplay(card);
            display.BringToFront();

            card.Resize += delegate { LayoutDisplay(card); };
            form.FormClosed += delegate
            {
                try { display?.Close(); } catch { }
            };
        }

        private static void LayoutDisplay(Panel card)
        {
            if (display == null || display.IsDisposed || card == null) return;

            // Keep the entire BATC section only as tall as the title plus the spectrum.
            // This preserves vertical space for the two video panes.
            if (card.Visible && card.Height != 145)
                card.Height = 145;

            int available = Math.Max(300, card.ClientSize.Width - 24);
            int width = Math.Min(1140, available);
            int height = 108;

            display.Size = new Size(width, height);
            display.Location = new Point(Math.Max(12, (card.ClientSize.Width - width) / 2), 31);
        }
    }
}
