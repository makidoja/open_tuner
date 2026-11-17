using System;
using System.Collections.Generic;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using opentuner.ExtraFeatures.BATCSpectrum;
using opentuner.MediaSources;

namespace opentuner
{
    internal static class ModernConceptRuntimeFixes
    {
        private static bool spectrumResizeHooked;
        private static bool quickTuneHooked;
        private static bool navigationHooked;
        private static bool videoOwnershipHooked;

        public static void Attach(ModernConceptForm form)
        {
            if (form == null) return;

            ApplyCompactSpectrumLayout(form);
            HookNavigation(form);

            form.Shown += delegate
            {
                ApplyCompactSpectrumLayout(form);
                HookQuickTune(form);
                HookNavigation(form);
                StabiliseVideoOwnership(form);
            };

            form.Resize += delegate { ApplyCompactSpectrumLayout(form); };
        }

        private static MainForm GetBackend(ModernConceptForm form)
        {
            return form.Backend;
        }

        private static PictureBox GetSpectrumBox(ModernConceptForm form)
        {
            return form.BatcSpectrumBox;
        }

        private static Panel[] GetVideoHosts(ModernConceptForm form)
        {
            return form.VideoHosts;
        }

        private static IEnumerable<Button> FindButtons(Control root)
        {
            foreach (Control c in root.Controls)
            {
                Button b = c as Button;
                if (b != null)
                    yield return b;

                if (c.HasChildren)
                {
                    foreach (Button child in FindButtons(c))
                        yield return child;
                }
            }
        }

        private static void HookNavigation(ModernConceptForm form)
        {
            if (navigationHooked || form == null) return;

            MainForm backend = GetBackend(form);
            if (backend == null) return;

            foreach (Button b in FindButtons(form))
            {
                string text = (b.Text ?? "").Trim();

                if (text.IndexOf("Chat (BATC)", StringComparison.OrdinalIgnoreCase) >= 0)
                    b.Click += delegate { backend.BackendShowBatcChat(); };
                else if (text.IndexOf("Presets", StringComparison.OrdinalIgnoreCase) >= 0 && text.IndexOf("Manage", StringComparison.OrdinalIgnoreCase) < 0)
                    b.Click += delegate { backend.BackendShowPresets(); };
                else if (text.IndexOf("Spectrum", StringComparison.OrdinalIgnoreCase) >= 0)
                    b.Click += delegate
                    {
                        PictureBox box = GetSpectrumBox(form);
                        if (box != null && !box.IsDisposed)
                        {
                            box.Focus();
                            box.BringToFront();
                        }
                        backend.BackendShowSpectrum();
                    };
                else if (text.IndexOf("Scan", StringComparison.OrdinalIgnoreCase) >= 0)
                    b.Click += delegate
                    {
                        PictureBox box = GetSpectrumBox(form);
                        if (box != null && !box.IsDisposed)
                            box.Focus();
                    };
                else if (text.IndexOf("Recordings", StringComparison.OrdinalIgnoreCase) >= 0)
                    b.Click += delegate { backend.BackendOpenRecordingsFolder(); };
                else if (text.IndexOf("Snapshots", StringComparison.OrdinalIgnoreCase) >= 0)
                    b.Click += delegate { backend.BackendOpenSnapshotsFolder(); };
                else if (text.IndexOf("Band Profiles", StringComparison.OrdinalIgnoreCase) >= 0)
                    b.Click += delegate { backend.BackendShowPresets(); };
                else if (text.IndexOf("External Tools", StringComparison.OrdinalIgnoreCase) >= 0)
                    b.Click += delegate { backend.BackendShowExternalTools(); };
            }

            navigationHooked = true;
        }

        private static void StabiliseVideoOwnership(ModernConceptForm form)
        {
            if (videoOwnershipHooked || form == null) return;

            MainForm backend = GetBackend(form);
            Panel[] hosts = GetVideoHosts(form);
            if (backend == null || hosts == null) return;

            // Video controls are attached only from the UI thread when the backend
            // actually starts producing receiver data. ModernConceptForm.uiTimer no
            // longer adopts video, so it stays running for connection-state refresh.

            Action attachIfNeeded = delegate
            {
                if (form.IsDisposed) return;
                bool needsAttach = false;
                for (int i = 0; i < Math.Min(2, hosts.Length); i++)
                {
                    if (hosts[i] != null && hosts[i].Controls.Count == 0)
                    {
                        needsAttach = true;
                        break;
                    }
                }

                if (!needsAttach) return;
                try { form.AdoptVideoControls(); }
                catch (Exception ex) { Serilog.Log.Warning(ex, "Video control adoption failed"); }
            };

            attachIfNeeded();

            backend.BackendSourceData += delegate(int tuner, OTSourceData data, string description)
            {
                if (form.IsDisposed) return;
                try
                {
                    form.BeginInvoke((MethodInvoker)delegate { attachIfNeeded(); });
                }
                catch { }
            };

            videoOwnershipHooked = true;
        }

        private static void ApplyCompactSpectrumLayout(ModernConceptForm form)
        {
            try
            {
                PictureBox nativeBox = GetSpectrumBox(form);
                if (nativeBox == null || nativeBox.Parent == null) return;

                Panel card = nativeBox.Parent as Panel;
                if (card == null) return;

                card.Height = 174;
                card.BackColor = Color.FromArgb(13, 28, 45);

                Action layout = delegate
                {
                    int available = Math.Max(300, card.ClientSize.Width - 24);
                    int width = Math.Min(760, available);
                    int height = 126;
                    int left = Math.Max(12, (card.ClientSize.Width - width) / 2);

                    nativeBox.Visible = true;
                    nativeBox.Anchor = AnchorStyles.Top;
                    nativeBox.SizeMode = PictureBoxSizeMode.Normal;
                    nativeBox.BackColor = Color.FromArgb(13, 28, 45);
                    nativeBox.Size = new Size(width, height);
                    nativeBox.Location = new Point(left, 32);
                    nativeBox.Cursor = Cursors.Hand;
                    nativeBox.BringToFront();
                };

                layout();

                if (!spectrumResizeHooked)
                {
                    card.Resize += delegate { layout(); };
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
                BATCSpectrum spectrum = form.BatcSpectrum;
                if (spectrum == null) return;

                MainForm backend = GetBackend(form);

                NumericUpDown[] freqInputs = form.FreqInputs;
                NumericUpDown[] srInputs = form.SrInputs;

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
