using System;
using System.Drawing;
using System.Globalization;
using System.Reflection;
using System.Windows.Forms;
using opentuner.MediaSources;

namespace opentuner
{
    internal static class ModernHardwareUiPass
    {
        private static bool attached;
        private static Label hardwareLabel;
        private static ComboBox lnbA;
        private static ComboBox lnbB;
        private static readonly Label[] detailLabels = new Label[2];
        private static readonly ComboBox[] rfInputs = new ComboBox[2];
        private static readonly TextBox[] freqText = new TextBox[2];
        private static readonly TextBox[] loText = new TextBox[2];
        private static readonly CheckBox[] loEnabled = new CheckBox[2];
        private static readonly Panel[] tunerControlBoxes = new Panel[2];
        private static readonly decimal[] preferredLo = new decimal[] { 9750M, 9750M };

        private static readonly Color Surface = Color.FromArgb(13, 28, 45);
        private static readonly Color Surface2 = Color.FromArgb(18, 38, 60);
        private static readonly Color Border = Color.FromArgb(37, 67, 94);
        private static readonly Color Text = Color.FromArgb(242, 247, 252);
        private static readonly Color Muted = Color.FromArgb(142, 165, 190);
        private static readonly Color Accent = Color.FromArgb(28, 139, 253);

        public static void Attach(ModernConceptForm form)
        {
            if (attached || form == null) return;
            attached = true;

            form.Shown += delegate
            {
                MainForm backend = GetBackend(form);
                if (backend == null) return;

                AddHardwareControls(form, backend);
                AddPerReceiverControls(form, backend);
                HookReceiverDetails(form, backend);
            };
        }

        private static MainForm GetBackend(ModernConceptForm form)
        {
            FieldInfo f = typeof(ModernConceptForm).GetField("backend", BindingFlags.Instance | BindingFlags.NonPublic);
            return f == null ? null : f.GetValue(form) as MainForm;
        }

        private static Panel[] GetVideoHosts(ModernConceptForm form)
        {
            FieldInfo f = typeof(ModernConceptForm).GetField("videoHosts", BindingFlags.Instance | BindingFlags.NonPublic);
            return f == null ? null : f.GetValue(form) as Panel[];
        }

        private static NumericUpDown[] GetFreqInputs(ModernConceptForm form)
        {
            FieldInfo f = typeof(ModernConceptForm).GetField("freqInputs", BindingFlags.Instance | BindingFlags.NonPublic);
            return f == null ? null : f.GetValue(form) as NumericUpDown[];
        }

        private static NumericUpDown[] GetSrInputs(ModernConceptForm form)
        {
            FieldInfo f = typeof(ModernConceptForm).GetField("srInputs", BindingFlags.Instance | BindingFlags.NonPublic);
            return f == null ? null : f.GetValue(form) as NumericUpDown[];
        }

        private static void AddHardwareControls(ModernConceptForm form, MainForm backend)
        {
            FlowLayoutPanel bar = FindControl<FlowLayoutPanel>(form, "ModernTopToolbar");
            if (bar == null || bar.Controls["ModernLnbA"] != null) return;

            hardwareLabel = new Label
            {
                Name = "ModernHardwareLabel",
                Text = "HW: —",
                AutoSize = true,
                ForeColor = Muted,
                BackColor = Color.Transparent,
                Font = new Font("Segoe UI Semibold", 8.5f),
                Margin = new Padding(12, 8, 4, 0)
            };
            bar.Controls.Add(hardwareLabel);

            lnbA = BuildLnbCombo("ModernLnbA", "LNB A", 0, backend);
            lnbB = BuildLnbCombo("ModernLnbB", "LNB B", 1, backend);
            bar.Controls.Add(lnbA);
            bar.Controls.Add(lnbB);

            Timer refresh = new Timer { Interval = 500 };
            refresh.Tick += delegate
            {
                if (form.IsDisposed) { refresh.Stop(); refresh.Dispose(); return; }

                string hardware = backend.BackendHardwareName;
                hardwareLabel.Text = string.IsNullOrWhiteSpace(hardware) ? "HW: —" : "HW: " + hardware;

                bool enabled = backend.BackendSupportsLnbPower;
                lnbA.Enabled = enabled;
                lnbB.Enabled = enabled;

                if (enabled)
                {
                    SetComboVoltage(lnbA, backend.BackendGetLnbVoltage(0));
                    SetComboVoltage(lnbB, backend.BackendGetLnbVoltage(1));
                }

                NumericUpDown[] hiddenFreq = GetFreqInputs(form);

                for (int tuner = 0; tuner < 2; tuner++)
                {
                    if (rfInputs[tuner] != null && !rfInputs[tuner].DroppedDown)
                    {
                        string rf = backend.BackendGetRfInput(tuner);
                        int index = string.Equals(rf, "B", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
                        if (rfInputs[tuner].SelectedIndex != index)
                            rfInputs[tuner].SelectedIndex = index;
                    }

                    if (hiddenFreq != null && tuner < hiddenFreq.Length && hiddenFreq[tuner] != null &&
                        freqText[tuner] != null && !freqText[tuner].Focused)
                    {
                        string wanted = hiddenFreq[tuner].Value.ToString("0.000", CultureInfo.InvariantCulture);
                        if (!string.Equals(freqText[tuner].Text, wanted, StringComparison.Ordinal))
                            freqText[tuner].Text = wanted;
                    }

                    long backendOffset = backend.BackendGetOffset(tuner);
                    decimal backendLo = backendOffset / 1000M;
                    if (backendOffset > 0)
                    {
                        preferredLo[tuner] = backendLo;
                        if (loText[tuner] != null && !loText[tuner].Focused)
                        {
                            string wantedLo = preferredLo[tuner].ToString("0.###", CultureInfo.InvariantCulture);
                            if (!string.Equals(loText[tuner].Text, wantedLo, StringComparison.Ordinal))
                                loText[tuner].Text = wantedLo;
                        }
                        if (loEnabled[tuner] != null && !loEnabled[tuner].Checked)
                            loEnabled[tuner].Checked = true;
                    }
                    else if (loEnabled[tuner] != null && loEnabled[tuner].Checked)
                    {
                        loEnabled[tuner].Checked = false;
                    }
                }
            };
            refresh.Start();
        }

        private static ComboBox BuildLnbCombo(string name, string prefix, int output, MainForm backend)
        {
            ComboBox combo = new ComboBox
            {
                Name = name,
                DropDownStyle = ComboBoxStyle.DropDownList,
                FlatStyle = FlatStyle.Flat,
                BackColor = Surface2,
                ForeColor = Text,
                Font = new Font("Segoe UI Semibold", 8.5f),
                Width = 112,
                Height = 28,
                Margin = new Padding(3, 4, 3, 0)
            };

            combo.Items.Add(prefix + ": OFF");
            combo.Items.Add(prefix + ": 13V V");
            combo.Items.Add(prefix + ": 18V H");
            combo.SelectedIndex = 0;
            combo.Enabled = false;

            combo.SelectionChangeCommitted += delegate
            {
                int volts = combo.SelectedIndex == 1 ? 13 : combo.SelectedIndex == 2 ? 18 : 0;
                backend.BackendSetLnbVoltage(output, volts);
            };

            return combo;
        }

        private static void SetComboVoltage(ComboBox combo, int volts)
        {
            if (combo == null || combo.DroppedDown) return;
            int index = volts == 13 ? 1 : volts == 18 ? 2 : 0;
            if (combo.SelectedIndex != index) combo.SelectedIndex = index;
        }

        private static void AddPerReceiverControls(ModernConceptForm form, MainForm backend)
        {
            Panel[] hosts = GetVideoHosts(form);
            NumericUpDown[] freq = GetFreqInputs(form);
            NumericUpDown[] sr = GetSrInputs(form);
            if (hosts == null) return;

            if (freq != null)
            {
                for (int i = 0; i < 2 && i < freq.Length; i++)
                {
                    if (freq[i] == null) continue;
                    freq[i].Minimum = 400;
                    freq[i].Maximum = 15000;
                    freq[i].DecimalPlaces = 3;
                    freq[i].Increment = 0.001M;
                }
            }

            for (int tuner = 0; tuner < 2 && tuner < hosts.Length; tuner++)
            {
                int captured = tuner;
                Control card = hosts[tuner] == null ? null : hosts[tuner].Parent;
                if (card == null) continue;

                Panel controls = FindBottomControlsPanel(card);
                if (controls == null || FindControl<Panel>(controls, "ModernTunerControlBox" + tuner) != null) continue;

                foreach (Control c in controls.Controls)
                {
                    Label oldHint = c as Label;
                    if (oldHint != null && (oldHint.Text ?? "").IndexOf("Frequency and SR", StringComparison.OrdinalIgnoreCase) >= 0)
                        oldHint.Visible = false;
                }

                Panel tunerBox = new Panel
                {
                    Name = "ModernTunerControlBox" + tuner,
                    Size = new Size(360, 118),
                    BackColor = Color.FromArgb(10, 24, 39),
                    Anchor = AnchorStyles.Right | AnchorStyles.Bottom
                };
                tunerBox.Paint += delegate(object sender, PaintEventArgs e)
                {
                    using (Pen p = new Pen(Border))
                        e.Graphics.DrawRectangle(p, 0, 0, tunerBox.Width - 1, tunerBox.Height - 1);
                };
                controls.Controls.Add(tunerBox);
                tunerControlBoxes[tuner] = tunerBox;

                Label title = MakeLabel("TUNER CONTROL", 8, 3, 120, 18, 8f, Muted);
                tunerBox.Controls.Add(title);

                Label freqLabel = MakeLabel("FREQ", 8, 25, 38, 28, 8f, Muted);
                tunerBox.Controls.Add(freqLabel);

                if (freq != null && captured < freq.Length && freq[captured] != null)
                {
                    NumericUpDown hidden = freq[captured];
                    Panel field = CreateEntryField("ModernFrequencyBorder" + captured, 48, 24, 120, 30, out TextBox entry);
                    freqText[captured] = entry;
                    entry.Name = "ModernFrequencyText" + captured;
                    entry.Text = hidden.Value.ToString("0.000", CultureInfo.InvariantCulture);
                    entry.Font = new Font("Segoe UI", 9f);
                    tunerBox.Controls.Add(field);
                    hidden.Visible = false;

                    entry.Leave += delegate { CommitFrequency(captured, hidden); };
                    entry.KeyDown += delegate(object sender, KeyEventArgs e)
                    {
                        if (e.KeyCode == Keys.Enter)
                        {
                            CommitFrequency(captured, hidden);
                            Button tuneButton = FindTuneButton(tunerBox);
                            if (tuneButton != null) tuneButton.PerformClick();
                            e.SuppressKeyPress = true;
                        }
                    };
                }

                Label srLabel = MakeLabel("SR", 178, 25, 24, 28, 8f, Muted);
                tunerBox.Controls.Add(srLabel);
                ComboBox srCombo = FindControl<ComboBox>(controls, "ModernSrCombo" + tuner);
                if (srCombo != null)
                {
                    srCombo.Parent = tunerBox;
                    srCombo.Location = new Point(202, 24);
                    srCombo.Size = new Size(90, 30);
                    srCombo.BringToFront();
                }

                Label tunerLabel = MakeLabel("TUNER", 8, 59, 48, 28, 8f, Muted);
                tunerBox.Controls.Add(tunerLabel);
                rfInputs[tuner] = new ComboBox
                {
                    Name = "ModernRfInput" + tuner,
                    DropDownStyle = ComboBoxStyle.DropDownList,
                    FlatStyle = FlatStyle.Flat,
                    BackColor = Surface2,
                    ForeColor = Text,
                    Font = new Font("Segoe UI Semibold", 8.5f),
                    Location = new Point(58, 58),
                    Size = new Size(110, 30)
                };
                rfInputs[tuner].Items.Add("Tuner A");
                rfInputs[tuner].Items.Add("Tuner B");
                rfInputs[tuner].SelectedIndex = string.Equals(backend.BackendGetRfInput(tuner), "B", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
                rfInputs[tuner].SelectionChangeCommitted += delegate
                {
                    backend.BackendSetRfInput(captured, rfInputs[captured].SelectedIndex == 1 ? 1 : 0);
                };
                tunerBox.Controls.Add(rfInputs[tuner]);

                long initialOffset = backend.BackendGetOffset(tuner);
                if (initialOffset > 0)
                    preferredLo[tuner] = initialOffset / 1000M;

                Label loLabel = MakeLabel("LO FREQ", 178, 59, 54, 28, 8f, Muted);
                tunerBox.Controls.Add(loLabel);
                Panel loField = CreateEntryField("ModernLoBorder" + tuner, 235, 58, 82, 30, out TextBox loEntry);
                loText[tuner] = loEntry;
                loEntry.Name = "ModernLoText" + tuner;
                loEntry.Text = preferredLo[tuner].ToString("0.###", CultureInfo.InvariantCulture);
                loEntry.Font = new Font("Segoe UI", 9f);
                tunerBox.Controls.Add(loField);

                loEnabled[tuner] = new CheckBox
                {
                    Name = "ModernLoEnabled" + tuner,
                    Text = "LO",
                    Checked = initialOffset > 0,
                    Location = new Point(322, 61),
                    Size = new Size(36, 24),
                    ForeColor = Text,
                    BackColor = Color.Transparent,
                    Font = new Font("Segoe UI Semibold", 8f),
                    AutoSize = false
                };
                tunerBox.Controls.Add(loEnabled[tuner]);

                Button tune = FindTuneButton(controls);
                if (tune != null)
                {
                    tune.Parent = tunerBox;
                    tune.Location = new Point(260, 88);
                    tune.Size = new Size(92, 26);
                    tune.Text = "TUNE";
                    tune.FlatStyle = FlatStyle.Flat;
                    tune.BackColor = Accent;
                    tune.ForeColor = Color.White;
                    tune.FlatAppearance.BorderColor = Accent;
                    tune.Font = new Font("Segoe UI Semibold", 8.5f);
                    tune.BringToFront();
                }

                loEnabled[tuner].CheckedChanged += delegate
                {
                    if (loEnabled[captured].Checked)
                        CommitLoText(captured, backend, true);
                    else
                        backend.BackendSetOffset(captured, 0);
                };

                loText[tuner].Leave += delegate { CommitLoText(captured, backend, loEnabled[captured].Checked); };
                loText[tuner].KeyDown += delegate(object sender, KeyEventArgs e)
                {
                    if (e.KeyCode == Keys.Enter)
                    {
                        CommitLoText(captured, backend, loEnabled[captured].Checked);
                        e.SuppressKeyPress = true;
                    }
                };

                LayoutTunerBox(controls, tunerBox);
                controls.Resize += delegate { LayoutTunerBox(controls, tunerBox); };
                tunerBox.BringToFront();
            }
        }

        private static Panel FindBottomControlsPanel(Control card)
        {
            foreach (Control c in card.Controls)
            {
                Panel p = c as Panel;
                if (p != null && p.Dock == DockStyle.Bottom)
                    return p;
            }
            return null;
        }

        private static void LayoutTunerBox(Panel controls, Panel box)
        {
            if (controls == null || box == null) return;
            int x = Math.Max(300, controls.ClientSize.Width - box.Width - 4);
            int y = Math.Max(44, controls.ClientSize.Height - box.Height - 4);
            box.Location = new Point(x, y);
        }

        private static Label MakeLabel(string text, int x, int y, int width, int height, float fontSize, Color colour)
        {
            return new Label
            {
                Text = text,
                Location = new Point(x, y),
                Size = new Size(width, height),
                ForeColor = colour,
                BackColor = Color.Transparent,
                Font = new Font("Segoe UI Semibold", fontSize),
                TextAlign = ContentAlignment.MiddleLeft
            };
        }

        private static Panel CreateEntryField(string name, int x, int y, int width, int height, out TextBox textBox)
        {
            Panel border = new Panel
            {
                Name = name,
                Location = new Point(x, y),
                Size = new Size(width, height),
                BackColor = Border
            };
            textBox = new TextBox
            {
                Location = new Point(1, 1),
                Size = new Size(width - 2, height - 2),
                BackColor = Surface2,
                ForeColor = Text,
                BorderStyle = BorderStyle.None,
                TextAlign = HorizontalAlignment.Left,
                Padding = new Padding(4, 0, 2, 0)
            };
            border.Controls.Add(textBox);
            textBox.BringToFront();
            return border;
        }

        private static Button FindTuneButton(Control root)
        {
            if (root == null) return null;
            foreach (Control c in root.Controls)
            {
                Button b = c as Button;
                if (b != null && string.Equals((b.Text ?? string.Empty).Trim(), "TUNE", StringComparison.OrdinalIgnoreCase))
                    return b;
                if (c.HasChildren)
                {
                    Button nested = FindTuneButton(c);
                    if (nested != null) return nested;
                }
            }
            return null;
        }

        private static void CommitFrequency(int tuner, NumericUpDown hidden)
        {
            if (hidden == null || freqText[tuner] == null) return;
            decimal value;
            if (!TryParseMHz(freqText[tuner].Text, out value) || value < hidden.Minimum || value > hidden.Maximum)
            {
                freqText[tuner].Text = hidden.Value.ToString("0.000", CultureInfo.InvariantCulture);
                return;
            }

            hidden.Value = value;
            freqText[tuner].Text = value.ToString("0.000", CultureInfo.InvariantCulture);
        }

        private static void CommitLoText(int tuner, MainForm backend, bool apply)
        {
            if (loText[tuner] == null) return;
            decimal value;
            if (!TryParseMHz(loText[tuner].Text, out value) || value < 0 || value > 15000)
            {
                loText[tuner].Text = preferredLo[tuner].ToString("0.###", CultureInfo.InvariantCulture);
                return;
            }

            preferredLo[tuner] = value;
            loText[tuner].Text = value.ToString("0.###", CultureInfo.InvariantCulture);
            if (apply)
                backend.BackendSetOffset(tuner, (long)Math.Round(value * 1000M));
        }

        private static bool TryParseMHz(string text, out decimal value)
        {
            string s = (text ?? string.Empty).Trim().Replace(',', '.');
            return decimal.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out value);
        }

        private static void HookReceiverDetails(ModernConceptForm form, MainForm backend)
        {
            Panel[] hosts = GetVideoHosts(form);
            if (hosts == null) return;

            for (int tuner = 0; tuner < 2 && tuner < hosts.Length; tuner++)
            {
                Control card = hosts[tuner] == null ? null : hosts[tuner].Parent;
                if (card != null) detailLabels[tuner] = FindDetailLabel(card);
            }

            backend.BackendSourceData += delegate(int tuner, OTSourceData data, string description)
            {
                if (data == null || tuner < 0 || tuner > 1 || form.IsDisposed) return;

                form.BeginInvoke((MethodInvoker)delegate
                {
                    Label label = detailLabels[tuner];
                    if (label == null) return;

                    string mode = string.IsNullOrWhiteSpace(data.demode_state)
                        ? (data.demod_locked ? "LOCKED" : "SEARCHING")
                        : data.demode_state;
                    string mod = string.IsNullOrWhiteSpace(data.modcode) ? "MODCOD —" : data.modcode;
                    string symbolRate = data.symbol_rate > 0 ? data.symbol_rate + " kS" : "SR —";
                    string rf = backend.BackendGetRfInput(tuner);
                    long offset = backend.BackendGetOffset(tuner);
                    string lo = offset > 0 ? (offset / 1000.0).ToString("0.###") + " MHz LO" : "DIRECT";

                    label.Text = mode + "   •   " + symbolRate + "   •   " + mod +
                                 "   •   Tuner " + rf + "   •   " + lo +
                                 (data.streaming ? "   •   TS" : "") +
                                 (data.recording || backend.BackendIsRecording(tuner) ? "   •   REC" : "");
                });
            };
        }

        private static Label FindDetailLabel(Control root)
        {
            foreach (Control c in root.Controls)
            {
                Label l = c as Label;
                if (l != null && ((l.Text ?? "").IndexOf("receiver details", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                  (l.Text ?? "").IndexOf("MODCOD", StringComparison.OrdinalIgnoreCase) >= 0))
                    return l;

                if (c.HasChildren)
                {
                    Label nested = FindDetailLabel(c);
                    if (nested != null) return nested;
                }
            }
            return null;
        }

        private static T FindControl<T>(Control root, string name) where T : Control
        {
            if (root == null) return null;
            if (root.Name == name) return root as T;

            foreach (Control c in root.Controls)
            {
                T found = FindControl<T>(c, name);
                if (found != null) return found;
            }
            return null;
        }
    }
}
