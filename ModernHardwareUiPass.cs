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

        public static void Attach(ModernConceptForm form)
        {
            if (attached || form == null) return;
            attached = true;

            form.Shown += delegate
            {
                MainForm backend = GetBackend(form);
                if (backend == null) return;

                AddHardwareControls(form, backend);
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
                    string lo = offset > 0 ? (offset / 1000.0).ToString("0.###") + " MHz LO" : "LO —";

                    label.Text = mode + "   •   " + sr + "   •   " + mod +
                                 "   •   RF " + rf + "   •   " + lo +
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
