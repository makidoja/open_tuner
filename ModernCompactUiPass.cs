using System;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;

namespace opentuner
{
    internal static class ModernCompactUiPass
    {
        private static bool attached;
        private static readonly int[] SymbolRates = { 66, 125, 250, 333, 500, 1000, 1500, 2000 };
        private static Panel spectrumCard;
        private static PictureBox spectrumProxy;

        public static void Attach(ModernConceptForm form)
        {
            if (attached || form == null) return;
            attached = true;

            form.Shown += delegate
            {
                Apply(form);
            };
        }

        private static void Apply(ModernConceptForm form)
        {
            MainForm backend = GetField<MainForm>(form, "backend");
            ComboBox sourceCombo = GetField<ComboBox>(form, "sourceCombo");
            FlowLayoutPanel presetList = GetField<FlowLayoutPanel>(form, "presetList");
            PictureBox nativeSpectrum = GetField<PictureBox>(form, "batcSpectrumBox");
            NumericUpDown[] srInputs = GetField<NumericUpDown[]>(form, "srInputs");
            Panel[] videoHosts = GetField<Panel[]>(form, "videoHosts");

            if (backend == null || sourceCombo == null) return;

            HideLeftNavigation(form);
            HidePresetSidebar(presetList);

            Panel header = sourceCombo.Parent as Panel;
            if (header != null)
            {
                CompactHeader(header);
                BuildTopToolbar(header, backend, nativeSpectrum);
            }

            if (srInputs != null)
                ReplaceSymbolRateControls(srInputs);

            if (videoHosts != null)
                TightenTunerCards(videoHosts);

            ConfigureSpectrum(nativeSpectrum);
        }

        private static T GetField<T>(ModernConceptForm form, string name) where T : class
        {
            FieldInfo f = typeof(ModernConceptForm).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            return f == null ? null : f.GetValue(form) as T;
        }

        private static void HideLeftNavigation(Form form)
        {
            foreach (Control c in form.Controls)
            {
                HideLeftNavigationRecursive(c);
            }
        }

        private static void HideLeftNavigationRecursive(Control root)
        {
            foreach (Control c in root.Controls)
            {
                Panel p = c as Panel;
                if (p != null && p.Dock == DockStyle.Left && p.Width >= 130 && p.Width <= 180 && ContainsText(p, "OpenTuner"))
                {
                    p.Visible = false;
                    p.Width = 0;
                    return;
                }
                if (c.HasChildren) HideLeftNavigationRecursive(c);
            }
        }

        private static void HidePresetSidebar(FlowLayoutPanel presetList)
        {
            if (presetList == null || presetList.Parent == null) return;
            Control side = presetList.Parent;
            side.Visible = false;
            side.Width = 0;
        }

        private static void CompactHeader(Panel header)
        {
            header.Height = 98;

            foreach (Control c in header.Controls)
            {
                Label l = c as Label;
                if (l != null && (l.Text ?? "").IndexOf("MODERN RECEIVER", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    l.Text = "OpenTuner";
                    l.Font = new Font("Segoe UI Semibold", 12f);
                }

                Button b = c as Button;
                if (b != null && string.Equals((b.Text ?? "").Trim(), "SETTINGS", StringComparison.OrdinalIgnoreCase))
                    b.Visible = false;
            }
        }

        private static void BuildTopToolbar(Panel header, MainForm backend, PictureBox nativeSpectrum)
        {
            if (header.Controls["ModernTopToolbar"] != null) return;

            FlowLayoutPanel bar = new FlowLayoutPanel
            {
                Name = "ModernTopToolbar",
                Dock = DockStyle.Bottom,
                Height = 40,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Padding = new Padding(12, 4, 8, 3),
                BackColor = Color.FromArgb(9, 24, 39)
            };

            bar.Controls.Add(ToolButton("PRESETS", delegate { backend.BackendShowPresets(); }));
            bar.Controls.Add(ToolButton("RECORDINGS", delegate { backend.BackendOpenRecordingsFolder(); }));
            bar.Controls.Add(ToolButton("SNAPSHOTS", delegate { backend.BackendOpenSnapshotsFolder(); }));
            bar.Controls.Add(ToolButton("BATC CHAT", delegate { backend.BackendShowBatcChat(); }));
            bar.Controls.Add(ToolButton("EXTERNAL TOOLS", delegate { backend.BackendShowExternalTools(); }));
            bar.Controls.Add(ToolButton("SETTINGS", delegate { backend.BackendShowGeneralSettings(); }));

            CheckBox qo100 = new CheckBox
            {
                Text = "QO-100 spectrum",
                AutoSize = true,
                Checked = false,
                ForeColor = Color.FromArgb(242, 247, 252),
                BackColor = Color.Transparent,
                Font = new Font("Segoe UI Semibold", 9f),
                Margin = new Padding(18, 7, 0, 0),
                Cursor = Cursors.Hand
            };
            qo100.CheckedChanged += delegate { SetSpectrumVisible(qo100.Checked); };
            bar.Controls.Add(qo100);

            header.Controls.Add(bar);
            bar.BringToFront();
        }

        private static Button ToolButton(string text, Action click)
        {
            Button b = new Button
            {
                Text = text,
                AutoSize = true,
                Height = 30,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(18, 38, 60),
                ForeColor = Color.FromArgb(242, 247, 252),
                Font = new Font("Segoe UI Semibold", 8.5f),
                Cursor = Cursors.Hand,
                Margin = new Padding(0, 0, 6, 0),
                Padding = new Padding(7, 0, 7, 0)
            };
            b.FlatAppearance.BorderColor = Color.FromArgb(37, 67, 94);
            b.Click += delegate { click(); };
            return b;
        }

        private static void ReplaceSymbolRateControls(NumericUpDown[] inputs)
        {
            for (int tuner = 0; tuner < 2 && tuner < inputs.Length; tuner++)
            {
                NumericUpDown input = inputs[tuner];
                if (input == null || input.Parent == null) continue;
                if (input.Parent.Controls["ModernSrCombo" + tuner] != null) continue;

                int captured = tuner;
                ComboBox combo = new ComboBox
                {
                    Name = "ModernSrCombo" + tuner,
                    DropDownStyle = ComboBoxStyle.DropDownList,
                    FlatStyle = FlatStyle.Flat,
                    BackColor = Color.FromArgb(18, 38, 60),
                    ForeColor = Color.FromArgb(242, 247, 252),
                    Font = new Font("Segoe UI", 9f),
                    Bounds = input.Bounds
                };

                foreach (int sr in SymbolRates) combo.Items.Add(sr);
                int initial = (int)input.Value;
                combo.SelectedItem = Array.IndexOf(SymbolRates, initial) >= 0 ? (object)initial : 333;

                combo.SelectedIndexChanged += delegate
                {
                    if (combo.SelectedItem == null) return;
                    decimal value = Convert.ToDecimal((int)combo.SelectedItem);
                    input.Value = Math.Min(input.Maximum, Math.Max(input.Minimum, value));
                };

                input.ValueChanged += delegate
                {
                    int value = (int)input.Value;
                    if (Array.IndexOf(SymbolRates, value) >= 0 && !Equals(combo.SelectedItem, value))
                        combo.SelectedItem = value;
                };

                input.Parent.Controls.Add(combo);
                combo.BringToFront();
                input.Visible = false;
            }
        }

        private static void TightenTunerCards(Panel[] hosts)
        {
            for (int tuner = 0; tuner < 2 && tuner < hosts.Length; tuner++)
            {
                Panel host = hosts[tuner];
                if (host == null || host.Parent == null) continue;
                foreach (Control c in host.Parent.Controls)
                {
                    Panel p = c as Panel;
                    if (p != null && p.Dock == DockStyle.Bottom && p.Height >= 180)
                    {
                        p.Height = 170;
                        break;
                    }
                }
            }
        }

        private static void ConfigureSpectrum(PictureBox nativeSpectrum)
        {
            if (nativeSpectrum == null || nativeSpectrum.Parent == null) return;
            spectrumCard = nativeSpectrum.Parent as Panel;
            if (spectrumCard == null) return;

            foreach (Control c in spectrumCard.Controls)
            {
                PictureBox pb = c as PictureBox;
                if (pb != null && !ReferenceEquals(pb, nativeSpectrum))
                {
                    spectrumProxy = pb;
                    break;
                }
            }

            spectrumCard.Resize += delegate { LayoutSpectrumNativeSize(); };
            SetSpectrumVisible(false);
        }

        private static void SetSpectrumVisible(bool visible)
        {
            if (spectrumCard == null) return;
            spectrumCard.Visible = visible;
            spectrumCard.Height = visible ? 319 : 0;
            if (visible) LayoutSpectrumNativeSize();
        }

        private static void LayoutSpectrumNativeSize()
        {
            if (spectrumCard == null || spectrumProxy == null || !spectrumCard.Visible) return;
            spectrumProxy.SizeMode = PictureBoxSizeMode.StretchImage;
            spectrumProxy.Size = new Size(922, 275);
            spectrumProxy.Location = new Point(Math.Max(10, (spectrumCard.ClientSize.Width - 922) / 2), 34);
            spectrumProxy.BringToFront();
        }

        private static bool ContainsText(Control root, string text)
        {
            foreach (Control c in root.Controls)
            {
                if ((c.Text ?? "").IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0) return true;
                if (c.HasChildren && ContainsText(c, text)) return true;
            }
            return false;
        }
    }
}
