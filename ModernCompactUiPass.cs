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
        private static Button connectButton;
        private static Button sourceSettingsButton;
        private static Timer connectionTimer;

        public static void Attach(ModernConceptForm form)
        {
            if (attached || form == null) return;
            attached = true;
            form.Shown += delegate { Apply(form); };
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
                CompactHeader(header, sourceCombo);
                BuildSingleTopBar(header, backend, sourceCombo);
            }

            if (srInputs != null) ReplaceSymbolRateControls(srInputs);
            if (videoHosts != null) TightenTunerCards(videoHosts);

            ConfigureSpectrum(nativeSpectrum);
            StartConnectionStateTimer(backend);
        }

        private static T GetField<T>(ModernConceptForm form, string name) where T : class
        {
            FieldInfo f = typeof(ModernConceptForm).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            return f == null ? null : f.GetValue(form) as T;
        }

        private static void HideLeftNavigation(Form form)
        {
            foreach (Control c in form.Controls) HideLeftNavigationRecursive(c);
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

        private static void CompactHeader(Panel header, ComboBox sourceCombo)
        {
            header.Height = 48;
            sourceCombo.Visible = false;

            foreach (Control c in header.Controls)
            {
                Label l = c as Label;
                if (l != null)
                {
                    if ((l.Text ?? "").IndexOf("MODERN RECEIVER", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        l.Text = "OpenTuner";
                        l.Font = new Font("Segoe UI Semibold", 11f);
                    }
                    else if (string.Equals((l.Text ?? "").Trim(), "READY", StringComparison.OrdinalIgnoreCase) ||
                             string.Equals((l.Text ?? "").Trim(), "CONNECTED", StringComparison.OrdinalIgnoreCase))
                    {
                        l.Visible = false;
                    }
                }

                Button b = c as Button;
                if (b != null)
                {
                    string text = (b.Text ?? "").Trim();
                    if (string.Equals(text, "SETTINGS", StringComparison.OrdinalIgnoreCase) ||
                        text.IndexOf("SOURCE SETTINGS", StringComparison.OrdinalIgnoreCase) >= 0)
                        b.Visible = false;
                    else if (string.Equals(text, "CONNECT", StringComparison.OrdinalIgnoreCase))
                        connectButton = b;
                }
            }
        }

        private static void BuildSingleTopBar(Panel header, MainForm backend, ComboBox sourceCombo)
        {
            if (header.Controls["ModernSingleTopBar"] != null) return;

            Panel bar = new Panel
            {
                Name = "ModernSingleTopBar",
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(13, 28, 45),
                Padding = new Padding(12, 7, 12, 7)
            };

            FlowLayoutPanel left = new FlowLayoutPanel
            {
                Name = "ModernTopToolbar",
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                BackColor = Color.Transparent,
                Padding = new Padding(0),
                Margin = new Padding(0)
            };

            Label brand = new Label
            {
                Text = "OpenTuner",
                AutoSize = false,
                Width = 92,
                Height = 30,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = Color.FromArgb(242, 247, 252),
                Font = new Font("Segoe UI Semibold", 11f),
                Margin = new Padding(0, 0, 12, 0)
            };
            left.Controls.Add(brand);

            left.Controls.Add(ToolButton("PRESETS", delegate { backend.BackendShowPresets(); }));
            left.Controls.Add(ToolButton("RECORDINGS", delegate { backend.BackendOpenRecordingsFolder(); }));
            left.Controls.Add(ToolButton("SNAPSHOTS", delegate { backend.BackendOpenSnapshotsFolder(); }));
            left.Controls.Add(ToolButton("BATC CHAT", delegate { backend.BackendShowBatcChat(); }));
            left.Controls.Add(ToolButton("SETTINGS", delegate { backend.BackendShowGeneralSettings(); }));

            sourceSettingsButton = ToolButton("SOURCE SETTINGS ▼", delegate { ShowSourceMenu(backend, sourceCombo); });
            left.Controls.Add(sourceSettingsButton);

            CheckBox qo100 = new CheckBox
            {
                Text = "QO-100 spectrum",
                AutoSize = true,
                Checked = false,
                ForeColor = Color.FromArgb(242, 247, 252),
                BackColor = Color.Transparent,
                Font = new Font("Segoe UI Semibold", 8.5f),
                Margin = new Padding(10, 7, 0, 0),
                Cursor = Cursors.Hand
            };
            qo100.CheckedChanged += delegate { SetSpectrumVisible(qo100.Checked); };
            left.Controls.Add(qo100);

            Panel right = new Panel { Dock = DockStyle.Right, Width = 118, BackColor = Color.Transparent };

            if (connectButton == null)
                connectButton = ToolButton("CONNECT", delegate { });
            else
            {
                connectButton.Visible = true;
                connectButton.AutoSize = false;
            }

            connectButton.Dock = DockStyle.Fill;
            connectButton.Margin = new Padding(0);
            connectButton.Font = new Font("Segoe UI Semibold", 9f);
            connectButton.FlatStyle = FlatStyle.Flat;
            connectButton.BackColor = Color.FromArgb(28, 139, 253);
            connectButton.ForeColor = Color.White;
            connectButton.FlatAppearance.BorderColor = Color.FromArgb(28, 139, 253);
            right.Controls.Add(connectButton);

            bar.Controls.Add(left);
            bar.Controls.Add(right);
            header.Controls.Add(bar);
            bar.BringToFront();
        }

        private static void ShowSourceMenu(MainForm backend, ComboBox hiddenSourceCombo)
        {
            string[] names = backend.BackendSourceNames();
            if (names == null || names.Length == 0) return;

            ContextMenuStrip menu = new ContextMenuStrip();
            menu.Font = new Font("Segoe UI", 9f);
            menu.ShowImageMargin = false;
            menu.BackColor = Color.FromArgb(13, 28, 45);
            menu.ForeColor = Color.FromArgb(242, 247, 252);

            for (int i = 0; i < names.Length; i++)
            {
                int index = i;
                ToolStripMenuItem item = new ToolStripMenuItem(names[i]);
                item.ForeColor = Color.FromArgb(242, 247, 252);
                item.BackColor = Color.FromArgb(13, 28, 45);
                item.Checked = backend.BackendSelectedSourceIndex == i;
                item.Click += delegate
                {
                    backend.BackendSelectedSourceIndex = index;
                    if (hiddenSourceCombo != null && index >= 0 && index < hiddenSourceCombo.Items.Count)
                        hiddenSourceCombo.SelectedIndex = index;
                    backend.BackendShowSourceSettings(index);
                };
                menu.Items.Add(item);
            }

            ModernWindowTheme.ThemeContextMenu(menu);

            // Deliberately do not Dispose() from the Closed event. WinForms can still
            // be completing OnItemClicked at that point; disposing there causes the
            // ObjectDisposedException seen when selecting a source.
            if (sourceSettingsButton != null)
                menu.Show(sourceSettingsButton, new Point(0, sourceSettingsButton.Height));
        }

        private static void StartConnectionStateTimer(MainForm backend)
        {
            if (connectionTimer != null) return;

            connectionTimer = new Timer { Interval = 250 };
            connectionTimer.Tick += delegate
            {
                if (connectButton == null || connectButton.IsDisposed) return;

                if (backend.BackendConnected)
                {
                    connectButton.Text = "CONNECTED";
                    connectButton.BackColor = Color.FromArgb(22, 140, 85);
                    connectButton.FlatAppearance.BorderColor = Color.FromArgb(40, 222, 126);
                    connectButton.ForeColor = Color.White;
                }
                else
                {
                    connectButton.Text = "CONNECT";
                    connectButton.BackColor = Color.FromArgb(28, 139, 253);
                    connectButton.FlatAppearance.BorderColor = Color.FromArgb(28, 139, 253);
                    connectButton.ForeColor = Color.White;
                }
            };
            connectionTimer.Start();
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
                Font = new Font("Segoe UI Semibold", 8.25f),
                Cursor = Cursors.Hand,
                Margin = new Padding(0, 0, 5, 0),
                Padding = new Padding(6, 0, 6, 0)
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

            spectrumCard.Resize += delegate { LayoutSpectrumCompact(); };
            SetSpectrumVisible(false);
        }

        private static void SetSpectrumVisible(bool visible)
        {
            if (spectrumCard == null) return;
            spectrumCard.Visible = visible;
            spectrumCard.Height = visible ? 168 : 0;
            if (visible) LayoutSpectrumCompact();
        }

        private static void LayoutSpectrumCompact()
        {
            if (spectrumCard == null || spectrumProxy == null || !spectrumCard.Visible) return;

            int available = Math.Max(300, spectrumCard.ClientSize.Width - 24);
            int width = Math.Min(760, available);
            int height = 126;

            spectrumProxy.SizeMode = PictureBoxSizeMode.Zoom;
            spectrumProxy.Size = new Size(width, height);
            spectrumProxy.Location = new Point(Math.Max(12, (spectrumCard.ClientSize.Width - width) / 2), 32);
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
