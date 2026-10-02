using System;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using opentuner.MediaSources;

namespace opentuner
{
    internal static class ModernReceiverUiPass
    {
        private static bool attached;
        private static int presetTargetTuner;
        private static Label presetTargetLabel;
        private static Label[] detailLabels = new Label[2];
        private static Button[] recordButtons = new Button[2];
        private static Panel[] merBarFill = new Panel[2];
        private static Panel[] marginBarFill = new Panel[2];
        private static Panel[] merBarBack = new Panel[2];
        private static Panel[] marginBarBack = new Panel[2];

        public static void Attach(ModernConceptForm form)
        {
            if (attached || form == null) return;
            attached = true;

            form.Shown += delegate
            {
                HookTunerControls(form);
                BuildModernPresets(form);
                HookPresetRefresh(form);
                HookTunerSelection(form);
            };
        }

        private static MainForm Backend(ModernConceptForm form)
        {
            FieldInfo f = typeof(ModernConceptForm).GetField("backend", BindingFlags.Instance | BindingFlags.NonPublic);
            return f == null ? null : f.GetValue(form) as MainForm;
        }

        private static Panel[] VideoHosts(ModernConceptForm form)
        {
            FieldInfo f = typeof(ModernConceptForm).GetField("videoHosts", BindingFlags.Instance | BindingFlags.NonPublic);
            return f == null ? null : f.GetValue(form) as Panel[];
        }

        private static NumericUpDown[] FreqInputs(ModernConceptForm form)
        {
            FieldInfo f = typeof(ModernConceptForm).GetField("freqInputs", BindingFlags.Instance | BindingFlags.NonPublic);
            return f == null ? null : f.GetValue(form) as NumericUpDown[];
        }

        private static NumericUpDown[] SrInputs(ModernConceptForm form)
        {
            FieldInfo f = typeof(ModernConceptForm).GetField("srInputs", BindingFlags.Instance | BindingFlags.NonPublic);
            return f == null ? null : f.GetValue(form) as NumericUpDown[];
        }

        private static FlowLayoutPanel PresetList(ModernConceptForm form)
        {
            FieldInfo f = typeof(ModernConceptForm).GetField("presetList", BindingFlags.Instance | BindingFlags.NonPublic);
            return f == null ? null : f.GetValue(form) as FlowLayoutPanel;
        }

        private static void HookTunerControls(ModernConceptForm form)
        {
            MainForm backend = Backend(form);
            Panel[] hosts = VideoHosts(form);
            if (backend == null || hosts == null) return;

            for (int tuner = 0; tuner < 2 && tuner < hosts.Length; tuner++)
            {
                int captured = tuner;
                Control card = hosts[tuner] == null ? null : hosts[tuner].Parent;
                if (card == null) continue;

                Button record = FindButton(card, "RECORD");
                if (record != null)
                {
                    recordButtons[tuner] = record;
                    record.Click += delegate
                    {
                        bool on = backend.BackendToggleRecording(captured);
                        ApplyRecordState(record, on);
                    };
                }

                Button snapshot = FindButton(card, "SNAPSHOT");
                if (snapshot != null)
                {
                    snapshot.Click += delegate
                    {
                        string file = backend.BackendTakeSnapshot(captured);
                        if (!string.IsNullOrWhiteSpace(file))
                        {
                            string old = snapshot.Text;
                            snapshot.Text = "✓ SAVED";
                            Timer t = new Timer { Interval = 1200 };
                            t.Tick += delegate { t.Stop(); t.Dispose(); if (!snapshot.IsDisposed) snapshot.Text = old; };
                            t.Start();
                        }
                    };
                }

                Label detail = FindHintLabel(card);
                if (detail != null)
                {
                    detailLabels[tuner] = detail;
                    detail.Text = "Waiting for receiver details";
                }

                BuildSignalBars(card, tuner);
            }

            backend.BackendSourceData += delegate(int tuner, OTSourceData data, string description)
            {
                if (data == null || tuner < 0 || tuner > 1 || form.IsDisposed) return;
                form.BeginInvoke((MethodInvoker)delegate
                {
                    if (detailLabels[tuner] != null)
                    {
                        string mode = string.IsNullOrWhiteSpace(data.demode_state) ? (data.demod_locked ? "LOCKED" : "SEARCHING") : data.demode_state;
                        string mod = string.IsNullOrWhiteSpace(data.modcode) ? "MODCOD —" : data.modcode;
                        string sr = data.symbol_rate > 0 ? data.symbol_rate + " kS" : "SR —";
                        detailLabels[tuner].Text = mode + "   •   " + sr + "   •   " + mod + (data.streaming ? "   •   TS" : "") + (data.recording || backend.BackendIsRecording(tuner) ? "   •   REC" : "");
                    }
                    if (recordButtons[tuner] != null)
                        ApplyRecordState(recordButtons[tuner], backend.BackendIsRecording(tuner));

                    UpdateBar(merBarBack[tuner], merBarFill[tuner], data.mer, 12.0);
                    UpdateBar(marginBarBack[tuner], marginBarFill[tuner], data.db_margin, 10.0);
                });
            };
        }

        private static void BuildSignalBars(Control card, int tuner)
        {
            Label mer = FindLabel(card, "MER");
            Label margin = FindLabel(card, "Margin");
            if (mer == null || margin == null || mer.Parent == null || margin.Parent == null) return;

            mer.Location = new Point(350, 48);
            mer.Size = new Size(92, 19);
            margin.Location = new Point(445, 48);
            margin.Size = new Size(100, 19);

            merBarBack[tuner] = new Panel
            {
                Location = new Point(350, 69),
                Size = new Size(88, 8),
                BackColor = Color.FromArgb(25, 47, 68)
            };
            merBarFill[tuner] = new Panel
            {
                Dock = DockStyle.Left,
                Width = 0,
                BackColor = Color.FromArgb(38, 148, 255)
            };
            merBarBack[tuner].Controls.Add(merBarFill[tuner]);
            mer.Parent.Controls.Add(merBarBack[tuner]);
            merBarBack[tuner].BringToFront();

            marginBarBack[tuner] = new Panel
            {
                Location = new Point(445, 69),
                Size = new Size(92, 8),
                BackColor = Color.FromArgb(25, 47, 68)
            };
            marginBarFill[tuner] = new Panel
            {
                Dock = DockStyle.Left,
                Width = 0,
                BackColor = Color.FromArgb(34, 197, 94)
            };
            marginBarBack[tuner].Controls.Add(marginBarFill[tuner]);
            margin.Parent.Controls.Add(marginBarBack[tuner]);
            marginBarBack[tuner].BringToFront();
        }

        private static void UpdateBar(Panel back, Panel fill, double value, double max)
        {
            if (back == null || fill == null || max <= 0) return;
            double clamped = Math.Max(0.0, Math.Min(max, value));
            fill.Width = (int)Math.Round(back.ClientSize.Width * (clamped / max));
        }

        private static void ApplyRecordState(Button button, bool on)
        {
            if (button == null) return;
            button.Text = on ? "■ STOP" : "● RECORD";
            button.BackColor = on ? Color.FromArgb(153, 39, 52) : Color.FromArgb(18, 38, 60);
            button.FlatAppearance.BorderColor = on ? Color.FromArgb(236, 84, 99) : Color.FromArgb(37, 67, 94);
        }

        private static void HookTunerSelection(ModernConceptForm form)
        {
            Panel[] hosts = VideoHosts(form);
            if (hosts == null) return;
            for (int tuner = 0; tuner < 2 && tuner < hosts.Length; tuner++)
            {
                int captured = tuner;
                Control card = hosts[tuner] == null ? null : hosts[tuner].Parent;
                if (card == null) continue;
                card.MouseDown += delegate { SetPresetTarget(captured); };
                hosts[tuner].MouseDown += delegate { SetPresetTarget(captured); };
            }
        }

        private static void SetPresetTarget(int tuner)
        {
            presetTargetTuner = Math.Max(0, Math.Min(1, tuner));
            if (presetTargetLabel != null)
                presetTargetLabel.Text = "Preset target: Tuner " + (presetTargetTuner + 1) + "  (click to switch)";
        }

        private static void BuildModernPresets(ModernConceptForm form)
        {
            FlowLayoutPanel list = PresetList(form);
            MainForm backend = Backend(form);
            NumericUpDown[] freq = FreqInputs(form);
            NumericUpDown[] sr = SrInputs(form);
            if (list == null || backend == null || freq == null || sr == null) return;

            list.SuspendLayout();
            list.Controls.Clear();

            presetTargetLabel = new Label
            {
                Text = "Preset target: Tuner " + (presetTargetTuner + 1) + "  (click to switch)",
                Width = 238,
                Height = 28,
                ForeColor = Color.FromArgb(142, 165, 190),
                Cursor = Cursors.Hand,
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = new Padding(3, 2, 3, 8)
            };
            presetTargetLabel.Click += delegate { SetPresetTarget(presetTargetTuner == 0 ? 1 : 0); };
            list.Controls.Add(presetTargetLabel);

            foreach (StoredFrequency preset in backend.BackendPresets())
            {
                StoredFrequency p = preset;
                Button b = new Button
                {
                    Text = "★  " + p.Name + "\r\n    " + (p.Frequency / 1000M).ToString("0.000") + " MHz   •   " + p.SymbolRate + " kS",
                    Width = 238,
                    Height = 50,
                    TextAlign = ContentAlignment.MiddleLeft,
                    FlatStyle = FlatStyle.Flat,
                    BackColor = Color.FromArgb(18, 38, 60),
                    ForeColor = Color.FromArgb(242, 247, 252),
                    Cursor = Cursors.Hand,
                    Margin = new Padding(3, 0, 3, 6)
                };
                b.FlatAppearance.BorderColor = Color.FromArgb(37, 67, 94);
                b.Click += delegate
                {
                    int tuner = presetTargetTuner;
                    decimal mhz = p.Frequency / 1000M;
                    freq[tuner].Value = Math.Min(freq[tuner].Maximum, Math.Max(freq[tuner].Minimum, mhz));
                    sr[tuner].Value = Math.Min(sr[tuner].Maximum, Math.Max(sr[tuner].Minimum, p.SymbolRate));
                    backend.BackendTune(tuner, Convert.ToUInt32(Math.Round(mhz * 1000M, 0)), Convert.ToUInt32(p.SymbolRate));
                };
                list.Controls.Add(b);
            }

            if (list.Controls.Count == 1)
                list.Controls.Add(new Label { Text = "No presets saved yet", AutoSize = true, ForeColor = Color.FromArgb(142,165,190) });

            list.ResumeLayout();
        }

        private static void HookPresetRefresh(ModernConceptForm form)
        {
            foreach (Button b in FindButtons(form))
            {
                if ((b.Text ?? "").IndexOf("MANAGE PRESETS", StringComparison.OrdinalIgnoreCase) >= 0)
                    b.Click += delegate { BuildModernPresets(form); };
            }
        }

        private static Button FindButton(Control root, string contains)
        {
            foreach (Control c in root.Controls)
            {
                Button b = c as Button;
                if (b != null && (b.Text ?? "").IndexOf(contains, StringComparison.OrdinalIgnoreCase) >= 0)
                    return b;
                if (c.HasChildren)
                {
                    Button nested = FindButton(c, contains);
                    if (nested != null) return nested;
                }
            }
            return null;
        }

        private static Label FindLabel(Control root, string startsWith)
        {
            foreach (Control c in root.Controls)
            {
                Label l = c as Label;
                if (l != null && (l.Text ?? "").StartsWith(startsWith, StringComparison.OrdinalIgnoreCase))
                    return l;
                if (c.HasChildren)
                {
                    Label nested = FindLabel(c, startsWith);
                    if (nested != null) return nested;
                }
            }
            return null;
        }

        private static Label FindHintLabel(Control root)
        {
            foreach (Control c in root.Controls)
            {
                Label l = c as Label;
                if (l != null && (l.Text ?? "").IndexOf("Frequency and SR", StringComparison.OrdinalIgnoreCase) >= 0)
                    return l;
                if (c.HasChildren)
                {
                    Label nested = FindHintLabel(c);
                    if (nested != null) return nested;
                }
            }
            return null;
        }

        private static System.Collections.Generic.IEnumerable<Button> FindButtons(Control root)
        {
            foreach (Control c in root.Controls)
            {
                Button b = c as Button;
                if (b != null) yield return b;
                if (c.HasChildren)
                    foreach (Button child in FindButtons(c)) yield return child;
            }
        }
    }
}
