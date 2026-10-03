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
        private static Label[] detailLabels = new Label[2];
        private static ComboBox[] rfInputs = new ComboBox[2];
        private static TextBox[] freqText = new TextBox[2];
        private static TextBox[] loText = new TextBox[2];
        private static CheckBox[] loEnabled = new CheckBox[2];
        private static decimal[] preferredLo = new decimal[] { 9750M, 9750M };

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

        private static void AddHardwareControls(ModernConceptForm form, MainForm backend)
        {
            FlowLayoutPanel bar = FindControl<FlowLayoutPanel>(form, "ModernTopToolbar");
            if (bar == null || bar.Controls["ModernLnbA"] != null) return;

            hardwareLabel = new Label
            {
                Name = "ModernHardwareLabel",
                Text = "HW: —",
                AutoSize = true,
                ForeColor = Color.FromArgb(142, 165, 190),
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
                BackColor = Color.FromArgb(18, 38, 60),
                ForeColor = Color.FromArgb(242, 247, 252),
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

                Panel controls = null;
                foreach (Control c in card.Controls)
                {
                    Panel p = c as Panel;
                    if (p != null && p.Dock == DockStyle.Bottom)
                    {
                        controls = p;
                        break;
                    }
                }
                if (controls == null || controls.Controls["ModernRfInput" + tuner] != null) continue;

                foreach (Control c in controls.Controls)
                {
                    Label oldHint = c as Label;
                    if (oldHint != null && (oldHint.Text ?? "").IndexOf("Frequency and SR", StringComparison.OrdinalIgnoreCase) >= 0)
                        oldHint.Visible = false;
                }

                if (freq != null && captured < freq.Length && freq[captured] != null)
                {
                    NumericUpDown hidden = freq[captured];
                    freqText[captured] = new TextBox
                    {
                        Name = "ModernFrequencyText" + captured,
                        Text = hidden.Value.ToString("0.000", CultureInfo.InvariantCulture),
                        Location = hidden.Location,
                        Size = hidden.Size,
                        BackColor = Color.FromArgb(18, 38, 60),
                        ForeColor = Color.FromArgb(242, 247, 252),
                        BorderStyle = BorderStyle.FixedSingle,
                        Font = hidden.Font,
                        TextAlign = HorizontalAlignment.Left
                    };
                    controls.Controls.Add(freqText[captured]);
                    freqText[captured].BringToFront();
                    hidden.Visible = false;

                    freqText[captured].Leave += delegate { CommitFrequency(captured, hidden); };
                    freqText[captured].KeyDown += delegate(object sender, KeyEventArgs e)
                    {
                        if (e.KeyCode == Keys.Enter)
                        {
                            CommitFrequency(captured, hidden);
                            e.SuppressKeyPress = true;
                        }
                    };
                }

                Label inputLabel = new Label
                {
                    Text = "RF",
                    Location = new Point(540, 10),
                    Size = new Size(24, 26),
                    TextAlign = ContentAlignment.MiddleLeft,
                    ForeColor = Color.FromArgb(142, 165, 190),
                    Font = new Font("Segoe UI Semibold", 8f)
                };
                controls.Controls.Add(inputLabel);

                rfInputs[tuner] = new ComboBox
                {
                    Name = "ModernRfInput" + tuner,
                    DropDownStyle = ComboBoxStyle.DropDownList,
                    FlatStyle = FlatStyle.Flat,
                    BackColor = Color.FromArgb(18, 38, 60),
                    ForeColor = Color.FromArgb(242, 247, 252),
                    Font = new Font("Segoe UI Semibold", 8.5f),
                    Location = new Point(566, 10),
                    Size = new Size(78, 26)
                };
                rfInputs[tuner].Items.Add("Tuner A");
                rfInputs[tuner].Items.Add("Tuner B");
                rfInputs[tuner].SelectedIndex = string.Equals(backend.BackendGetRfInput(tuner), "B", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
                rfInputs[tuner].SelectionChangeCommitted += delegate
                {
                    backend.BackendSetRfInput(captured, rfInputs[captured].SelectedIndex == 1 ? 1 : 0);
                };
                controls.Controls.Add(rfInputs[tuner]);

                long initialOffset = backend.BackendGetOffset(tuner);
                if (initialOffset > 0)
                    preferredLo[tuner] = initialOffset / 1000M;

                loEnabled[tuner] = new CheckBox
                {
                    Name = "ModernLoEnabled" + tuner,
                    Text = "LO",
                    Checked = initialOffset > 0,
                    Location = new Point(654, 11),
                    Size = new Size(42, 24),
                    ForeColor = Color.FromArgb(242, 247, 252),
                    BackColor = Color.Transparent,
                    Font = new Font("Segoe UI Semibold", 8f),
                    AutoSize = false
                };
                controls.Controls.Add(loEnabled[tuner]);

                loText[tuner] = new TextBox
                {
                    Name = "ModernLoText" + tuner,
                    Text = preferredLo[tuner].ToString("0.###", CultureInfo.InvariantCulture),
                    Location = new Point(698, 10),
                    Size = new Size(72, 26),
                    BackColor = Color.FromArgb(18, 38, 60),
                    ForeColor = Color.FromArgb(242, 247, 252),
                    BorderStyle = BorderStyle.FixedSingle,
                    Font = new Font("Segoe UI", 8.5f),
                    TextAlign = HorizontalAlignment.Left
                };
                controls.Controls.Add(loText[tuner]);

                Label mhz = new Label
                {
                    Text = "MHz",
                    Location = new Point(774, 10),
                    Size = new Size(34, 26),
                    TextAlign = ContentAlignment.MiddleLeft,
                    ForeColor = Color.FromArgb(142, 165, 190),
                    Font = new Font("Segoe UI", 8f)
                };
                controls.Controls.Add(mhz);

                loEnabled[tuner].CheckedChanged += delegate
                {
                    if (loEnabled[captured].Checked)
                    {
                        CommitLoText(captured, backend, true);
                    }
                    else
                    {
                        backend.BackendSetOffset(captured, 0);
                    }
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
            }
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
                    string sr = data.symbol_rate > 0 ? data.symbol_rate + " kS" : "SR —";
                    string rf = backend.BackendGetRfInput(tuner);
                    long offset = backend.BackendGetOffset(tuner);
                    string lo = offset > 0 ? (offset / 1000.0).ToString("0.###") + " MHz LO" : "DIRECT";

                    label.Text = mode + "   •   " + sr + "   •   " + mod +
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
