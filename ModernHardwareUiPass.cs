using System;
using System.Drawing;
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
        private static NumericUpDown[] loInputs = new NumericUpDown[2];

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

                for (int tuner = 0; tuner < 2; tuner++)
                {
                    if (rfInputs[tuner] != null && !rfInputs[tuner].DroppedDown)
                    {
                        string rf = backend.BackendGetRfInput(tuner);
                        int index = string.Equals(rf, "B", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
                        if (rfInputs[tuner].SelectedIndex != index)
                            rfInputs[tuner].SelectedIndex = index;
                    }

                    if (loInputs[tuner] != null && !loInputs[tuner].Focused)
                    {
                        decimal mhz = backend.BackendGetOffset(tuner) / 1000M;
                        mhz = Math.Min(loInputs[tuner].Maximum, Math.Max(loInputs[tuner].Minimum, mhz));
                        if (loInputs[tuner].Value != mhz)
                            loInputs[tuner].Value = mhz;
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

                // Keep receiver input/LO controls on the main tuning row.  This leaves
                // the bottom status line completely clear for LOCK/SR/MODCOD/LO telemetry.
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

                Label loLabel = new Label
                {
                    Text = "LO",
                    Location = new Point(654, 10),
                    Size = new Size(22, 26),
                    TextAlign = ContentAlignment.MiddleLeft,
                    ForeColor = Color.FromArgb(142, 165, 190),
                    Font = new Font("Segoe UI Semibold", 8f)
                };
                controls.Controls.Add(loLabel);

                decimal initialLo = backend.BackendGetOffset(tuner) / 1000M;
                initialLo = Math.Min(15000M, Math.Max(0M, initialLo));
                loInputs[tuner] = new NumericUpDown
                {
                    Name = "ModernLoOffset" + tuner,
                    DecimalPlaces = 3,
                    Increment = 0.001M,
                    Minimum = 0,
                    Maximum = 15000,
                    Value = initialLo,
                    Location = new Point(678, 10),
                    Size = new Size(92, 26),
                    BackColor = Color.FromArgb(18, 38, 60),
                    ForeColor = Color.FromArgb(242, 247, 252),
                    ThousandsSeparator = false
                };
                loInputs[tuner].Leave += delegate
                {
                    backend.BackendSetOffset(captured, (long)Math.Round(loInputs[captured].Value * 1000M));
                };
                loInputs[tuner].KeyDown += delegate(object sender, KeyEventArgs e)
                {
                    if (e.KeyCode == Keys.Enter)
                    {
                        backend.BackendSetOffset(captured, (long)Math.Round(loInputs[captured].Value * 1000M));
                        e.SuppressKeyPress = true;
                    }
                };
                controls.Controls.Add(loInputs[tuner]);

                Label mhz = new Label
                {
                    Text = "MHz",
                    Location = new Point(772, 10),
                    Size = new Size(34, 26),
                    TextAlign = ContentAlignment.MiddleLeft,
                    ForeColor = Color.FromArgb(142, 165, 190),
                    Font = new Font("Segoe UI", 8f)
                };
                controls.Controls.Add(mhz);
            }
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
