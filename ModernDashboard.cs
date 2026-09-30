using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using opentuner.MediaSources;

namespace opentuner
{
    /// <summary>
    /// Primary operating UI for the modern branch. The original WinForms controls are
    /// left alive behind this view so all existing source/player functionality remains
    /// available, but normal operation happens here.
    /// </summary>
    public partial class MainForm
    {
        private Panel mdRoot;
        private Panel mdVideoFrame;
        private Panel mdVideoViewport;
        private Label mdVideoPlaceholder;
        private FlowLayoutPanel mdPresetList;
        private FlowLayoutPanel mdTunerSelector;
        private Label mdConnection;
        private Label mdDevice;
        private Label mdLock;
        private Label mdMer;
        private Label mdMargin;
        private Label mdService;
        private Label mdModcode;
        private Label mdFrequencyReadout;
        private NumericUpDown mdFrequency;
        private NumericUpDown mdSymbolRate;
        private ComboBox mdSource;
        private Timer mdDashboardTimer;
        private int mdActiveTuner;
        private OTSource mdSubscribedSource;
        private readonly OTSourceData[] mdLastData = new OTSourceData[4];
        private readonly Panel[] mdVideoHosts = new Panel[4];
        private readonly Button[] mdTunerButtons = new Button[4];

        private void BuildModernDashboard()
        {
            // Hide the legacy operating surface, but do not dispose it. Existing menu
            // commands can still expose settings and tools when required.
            if (splitContainer1 != null)
                splitContainer1.Visible = false;
            if (tabControl1 != null)
                tabControl1.Visible = false;
            if (menuStrip1 != null)
                menuStrip1.Visible = false;

            mdRoot = new Panel
            {
                Name = "ModernDashboard",
                Dock = DockStyle.Fill,
                BackColor = ModernTheme.Background,
                Padding = new Padding(18)
            };
            Controls.Add(mdRoot);
            mdRoot.BringToFront();

            Panel top = BuildDashboardHeader();
            top.Dock = DockStyle.Top;
            top.Height = 62;
            mdRoot.Controls.Add(top);

            Panel bottom = BuildStatusBar();
            bottom.Dock = DockStyle.Bottom;
            bottom.Height = 42;
            mdRoot.Controls.Add(bottom);

            Panel body = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = ModernTheme.Background,
                Padding = new Padding(0, 14, 0, 12)
            };
            mdRoot.Controls.Add(body);
            body.BringToFront();

            Panel left = BuildTuningPanel();
            left.Dock = DockStyle.Left;
            left.Width = 285;
            body.Controls.Add(left);

            Panel right = BuildPresetPanel();
            right.Dock = DockStyle.Right;
            right.Width = 235;
            body.Controls.Add(right);

            Panel center = BuildVideoPanel();
            center.Dock = DockStyle.Fill;
            center.Padding = new Padding(14, 0, 14, 0);
            body.Controls.Add(center);
            center.BringToFront();

            mdDashboardTimer = new Timer { Interval = 350 };
            mdDashboardTimer.Tick += delegate
            {
                HookDashboardSource();
                AdoptVideoControls();
                RefreshDashboardStatus();
            };
            mdDashboardTimer.Start();

            RefreshDashboardStatus();
        }

        private Panel BuildDashboardHeader()
        {
            Panel header = new Panel
            {
                BackColor = ModernTheme.Surface,
                Padding = new Padding(18, 10, 18, 10)
            };

            Label brand = new Label
            {
                Text = "OpenTuner",
                AutoSize = true,
                Font = new Font("Segoe UI Semibold", 18f),
                ForeColor = ModernTheme.TextPrimary,
                Location = new Point(16, 14)
            };
            header.Controls.Add(brand);

            Label sub = new Label
            {
                Text = "DATV RECEIVER",
                AutoSize = true,
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = ModernTheme.TextMuted,
                Location = new Point(142, 23)
            };
            header.Controls.Add(sub);

            mdConnection = MakePill("READY", ModernTheme.TextSecondary, ModernTheme.SurfaceRaised, 110);
            mdConnection.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            header.Controls.Add(mdConnection);

            mdSource = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = 185,
                Height = 32,
                BackColor = ModernTheme.SurfaceRaised,
                ForeColor = ModernTheme.TextPrimary,
                FlatStyle = FlatStyle.Flat,
                Font = ModernTheme.FontBody
            };
            if (comboAvailableSources != null)
            {
                foreach (object item in comboAvailableSources.Items)
                    mdSource.Items.Add(item);
                if (comboAvailableSources.SelectedIndex >= 0)
                    mdSource.SelectedIndex = comboAvailableSources.SelectedIndex;
            }
            mdSource.SelectedIndexChanged += delegate
            {
                if (comboAvailableSources != null && mdSource.SelectedIndex >= 0)
                    comboAvailableSources.SelectedIndex = mdSource.SelectedIndex;
            };
            header.Controls.Add(mdSource);

            Button connect = ModernTheme.CreateButton("CONNECT", true);
            connect.Width = 92;
            connect.Click += delegate { if (btnSourceConnect != null) btnSourceConnect.PerformClick(); };
            header.Controls.Add(connect);

            Button settings = ModernTheme.CreateButton("SETTINGS", false);
            settings.Width = 92;
            settings.Click += delegate
            {
                if (settingsToolStripMenuItem != null)
                    settingsToolStripMenuItem.PerformClick();
            };
            header.Controls.Add(settings);

            header.Resize += delegate
            {
                int x = header.ClientSize.Width - 18;
                mdConnection.Location = new Point(x - mdConnection.Width, 15);
                x -= mdConnection.Width + 10;
                settings.Location = new Point(x - settings.Width, 13);
                x -= settings.Width + 8;
                connect.Location = new Point(x - connect.Width, 13);
                x -= connect.Width + 10;
                mdSource.Location = new Point(x - mdSource.Width, 15);
            };

            return header;
        }

        private Panel BuildTuningPanel()
        {
            Panel panel = CardPanel();
            panel.Padding = new Padding(18);

            Label heading = SectionTitle("TUNING");
            heading.Location = new Point(18, 16);
            panel.Controls.Add(heading);

            mdTunerSelector = new FlowLayoutPanel
            {
                Location = new Point(18, 49),
                Size = new Size(245, 36),
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                BackColor = Color.Transparent
            };
            for (int i = 0; i < 4; i++)
            {
                int tuner = i;
                Button b = ModernTheme.CreateButton("T" + (i + 1), i == 0);
                b.Width = 52;
                b.Height = 30;
                b.Margin = new Padding(0, 0, 7, 0);
                b.Click += delegate { SelectDashboardTuner(tuner); };
                mdTunerButtons[i] = b;
                mdTunerSelector.Controls.Add(b);
            }
            panel.Controls.Add(mdTunerSelector);

            Label freqLabel = SmallCaption("FREQUENCY");
            freqLabel.Location = new Point(18, 102);
            panel.Controls.Add(freqLabel);

            mdFrequencyReadout = new Label
            {
                Location = new Point(18, 123),
                Size = new Size(245, 52),
                Text = "10491.500 MHz",
                Font = new Font("Segoe UI Semibold", 24f),
                ForeColor = ModernTheme.TextPrimary,
                TextAlign = ContentAlignment.MiddleLeft
            };
            panel.Controls.Add(mdFrequencyReadout);

            mdFrequency = new NumericUpDown
            {
                Location = new Point(18, 178),
                Size = new Size(245, 32),
                DecimalPlaces = 3,
                Increment = 0.001M,
                Minimum = 0,
                Maximum = 15000,
                Value = 10491.500M,
                ThousandsSeparator = false,
                BackColor = ModernTheme.SurfaceRaised,
                ForeColor = ModernTheme.TextPrimary,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 11f)
            };
            mdFrequency.ValueChanged += delegate { mdFrequencyReadout.Text = mdFrequency.Value.ToString("0.000") + " MHz"; };
            panel.Controls.Add(mdFrequency);

            Label srLabel = SmallCaption("SYMBOL RATE");
            srLabel.Location = new Point(18, 226);
            panel.Controls.Add(srLabel);

            mdSymbolRate = new NumericUpDown
            {
                Location = new Point(18, 247),
                Size = new Size(245, 32),
                Minimum = 1,
                Maximum = 5000,
                Value = 333,
                BackColor = ModernTheme.SurfaceRaised,
                ForeColor = ModernTheme.TextPrimary,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 11f)
            };
            panel.Controls.Add(mdSymbolRate);

            Button tune = ModernTheme.CreateButton("TUNE", true);
            tune.Location = new Point(18, 296);
            tune.Size = new Size(245, 40);
            tune.Font = new Font("Segoe UI Semibold", 11f);
            tune.Click += delegate { TuneFromDashboard(); };
            panel.Controls.Add(tune);

            Label signalHeading = SectionTitle("SIGNAL");
            signalHeading.Location = new Point(18, 365);
            panel.Controls.Add(signalHeading);

            mdLock = MakePill("NO LOCK", ModernTheme.Danger, Color.FromArgb(55, 28, 31), 108);
            mdLock.Location = new Point(18, 397);
            panel.Controls.Add(mdLock);

            mdMer = MetricLabel("MER", "— dB");
            mdMer.Location = new Point(18, 445);
            panel.Controls.Add(mdMer);

            mdMargin = MetricLabel("MARGIN", "— dB");
            mdMargin.Location = new Point(18, 491);
            panel.Controls.Add(mdMargin);

            return panel;
        }

        private Panel BuildVideoPanel()
        {
            Panel outer = new Panel { BackColor = ModernTheme.Background };

            mdVideoFrame = CardPanel();
            mdVideoFrame.Dock = DockStyle.Fill;
            mdVideoFrame.Padding = new Padding(2);
            outer.Controls.Add(mdVideoFrame);

            Panel titlebar = new Panel
            {
                Dock = DockStyle.Top,
                Height = 48,
                BackColor = ModernTheme.Surface
            };
            mdVideoFrame.Controls.Add(titlebar);

            mdDevice = new Label
            {
                Text = "RECEIVER 1",
                Location = new Point(16, 8),
                AutoSize = true,
                Font = ModernTheme.FontSection,
                ForeColor = ModernTheme.TextPrimary
            };
            titlebar.Controls.Add(mdDevice);

            mdService = new Label
            {
                Text = "Waiting for signal",
                Location = new Point(16, 28),
                AutoSize = true,
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = ModernTheme.TextMuted
            };
            titlebar.Controls.Add(mdService);

            mdModcode = new Label
            {
                Text = "",
                AutoSize = false,
                Size = new Size(260, 32),
                TextAlign = ContentAlignment.MiddleRight,
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Font = ModernTheme.FontBodySemibold,
                ForeColor = ModernTheme.TextSecondary
            };
            titlebar.Controls.Add(mdModcode);
            titlebar.Resize += delegate { mdModcode.Location = new Point(titlebar.Width - mdModcode.Width - 16, 8); };

            mdVideoViewport = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Black
            };
            mdVideoFrame.Controls.Add(mdVideoViewport);
            mdVideoViewport.BringToFront();

            mdVideoPlaceholder = new Label
            {
                Dock = DockStyle.Fill,
                Text = "NO SIGNAL\r\n\r\nSelect a receiver and tune to a DATV signal",
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 11f),
                ForeColor = ModernTheme.TextMuted,
                BackColor = Color.Black
            };
            mdVideoViewport.Controls.Add(mdVideoPlaceholder);

            for (int i = 0; i < 4; i++)
            {
                mdVideoHosts[i] = new Panel
                {
                    Dock = DockStyle.Fill,
                    BackColor = Color.Black,
                    Visible = i == 0
                };
                mdVideoViewport.Controls.Add(mdVideoHosts[i]);
            }
            mdVideoPlaceholder.BringToFront();

            return outer;
        }

        private Panel BuildPresetPanel()
        {
            Panel panel = CardPanel();
            panel.Padding = new Padding(16);

            Label heading = SectionTitle("PRESETS");
            heading.Location = new Point(16, 16);
            panel.Controls.Add(heading);

            mdPresetList = new FlowLayoutPanel
            {
                Location = new Point(16, 48),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                Size = new Size(203, 500),
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = true,
                BackColor = Color.Transparent
            };
            panel.Controls.Add(mdPresetList);

            PopulateDashboardPresets();

            Button manage = ModernTheme.CreateButton("MANAGE PRESETS", false);
            manage.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;
            manage.Location = new Point(16, 560);
            manage.Size = new Size(203, 36);
            manage.Click += delegate
            {
                if (menuManageFrequencyPresets != null)
                    menuManageFrequencyPresets.PerformClick();
                PopulateDashboardPresets();
            };
            panel.Controls.Add(manage);
            panel.Resize += delegate
            {
                mdPresetList.Height = Math.Max(120, panel.Height - 112);
                manage.Top = panel.Height - 52;
                manage.Width = panel.Width - 32;
                mdPresetList.Width = panel.Width - 32;
            };

            return panel;
        }

        private Panel BuildStatusBar()
        {
            Panel bar = new Panel { BackColor = ModernTheme.Surface };
            Label left = new Label
            {
                Dock = DockStyle.Left,
                Width = 430,
                Text = "OpenTuner modern-ui",
                TextAlign = ContentAlignment.MiddleLeft,
                Font = ModernTheme.FontBody,
                ForeColor = ModernTheme.TextMuted,
                Padding = new Padding(12, 0, 0, 0)
            };
            bar.Controls.Add(left);

            Button legacy = ModernTheme.CreateButton("ADVANCED / LEGACY", false);
            legacy.Dock = DockStyle.Right;
            legacy.Width = 160;
            legacy.Click += delegate { ShowLegacyView(); };
            bar.Controls.Add(legacy);
            return bar;
        }

        private void ShowLegacyView()
        {
            if (mdRoot != null)
                mdRoot.Visible = false;
            if (menuStrip1 != null)
                menuStrip1.Visible = true;
            if (splitContainer1 != null)
                splitContainer1.Visible = true;
            if (tabControl1 != null)
                tabControl1.Visible = true;
        }

        private void SelectDashboardTuner(int tuner)
        {
            if (tuner < 0 || tuner > 3)
                return;
            mdActiveTuner = tuner;
            for (int i = 0; i < 4; i++)
            {
                bool active = i == tuner;
                if (mdTunerButtons[i] != null)
                {
                    mdTunerButtons[i].BackColor = active ? ModernTheme.Accent : ModernTheme.SurfaceRaised;
                    mdTunerButtons[i].FlatAppearance.BorderColor = active ? ModernTheme.Accent : ModernTheme.Border;
                }
                if (mdVideoHosts[i] != null)
                    mdVideoHosts[i].Visible = active;
            }
            OTSourceData data = mdLastData[tuner];
            if (data != null)
                ApplyDashboardData(tuner, data);
            AdoptVideoControls();
        }

        private void TuneFromDashboard()
        {
            if (!source_connected || videoSource == null)
                return;

            uint freqKHz = Convert.ToUInt32(decimal.Round(mdFrequency.Value * 1000M, 0));
            uint sr = Convert.ToUInt32(mdSymbolRate.Value);
            videoSource.SetFrequency(mdActiveTuner, freqKHz, sr, false);
        }

        private void PopulateDashboardPresets()
        {
            if (mdPresetList == null)
                return;
            mdPresetList.Controls.Clear();

            IEnumerable<StoredFrequency> presets = stored_frequencies ?? new List<StoredFrequency>();
            foreach (StoredFrequency preset in presets.Take(14))
            {
                StoredFrequency p = preset;
                Button b = ModernTheme.CreateButton(p.Name, false);
                b.Width = Math.Max(170, mdPresetList.ClientSize.Width - 8);
                b.Height = 42;
                b.Margin = new Padding(0, 0, 0, 7);
                b.TextAlign = ContentAlignment.MiddleLeft;
                b.Padding = new Padding(9, 0, 4, 0);
                b.Click += delegate
                {
                    mdFrequency.Value = Math.Min(mdFrequency.Maximum, Math.Max(mdFrequency.Minimum, p.Frequency / 1000M));
                    mdSymbolRate.Value = Math.Min(mdSymbolRate.Maximum, Math.Max(mdSymbolRate.Minimum, p.SymbolRate));
                    TuneFromDashboard();
                };
                mdPresetList.Controls.Add(b);
            }

            if (mdPresetList.Controls.Count == 0)
            {
                Label none = new Label
                {
                    Text = "No presets saved yet",
                    AutoSize = true,
                    ForeColor = ModernTheme.TextMuted,
                    Font = ModernTheme.FontBody,
                    Margin = new Padding(4, 8, 0, 0)
                };
                mdPresetList.Controls.Add(none);
            }
        }

        private void HookDashboardSource()
        {
            if (!source_connected || videoSource == null || mdSubscribedSource == videoSource)
                return;

            if (mdSubscribedSource != null)
                mdSubscribedSource.OnSourceData -= DashboardSourceData;
            mdSubscribedSource = videoSource;
            mdSubscribedSource.OnSourceData += DashboardSourceData;
        }

        private void DashboardSourceData(int videoNr, OTSourceData data, string description)
        {
            if (data == null || videoNr < 0 || videoNr >= 4)
                return;
            mdLastData[videoNr] = data;

            if (InvokeRequired)
            {
                BeginInvoke((MethodInvoker)delegate
                {
                    if (videoNr == mdActiveTuner)
                        ApplyDashboardData(videoNr, data);
                });
            }
            else if (videoNr == mdActiveTuner)
            {
                ApplyDashboardData(videoNr, data);
            }
        }

        private void ApplyDashboardData(int tuner, OTSourceData data)
        {
            mdDevice.Text = "RECEIVER " + (tuner + 1);
            mdService.Text = string.IsNullOrWhiteSpace(data.service_name) ? "Waiting for service information" : data.service_name;
            mdModcode.Text = data.modcode ?? "";
            mdLock.Text = data.demod_locked ? "● LOCKED" : "NO LOCK";
            mdLock.ForeColor = data.demod_locked ? ModernTheme.Success : ModernTheme.Danger;
            mdLock.BackColor = data.demod_locked ? Color.FromArgb(22, 48, 37) : Color.FromArgb(55, 28, 31);
            mdMer.Text = "MER      " + data.mer.ToString("0.0", CultureInfo.InvariantCulture) + " dB";
            mdMargin.Text = "MARGIN   " + data.db_margin.ToString("0.0", CultureInfo.InvariantCulture) + " dB";

            if (data.frequency > 0)
            {
                decimal mhz = data.frequency / 1000M;
                if (mhz >= mdFrequency.Minimum && mhz <= mdFrequency.Maximum)
                    mdFrequency.Value = mhz;
                mdFrequencyReadout.Text = mhz.ToString("0.000") + " MHz";
            }
            if (data.symbol_rate > 0 && data.symbol_rate <= mdSymbolRate.Maximum)
                mdSymbolRate.Value = data.symbol_rate;

            mdVideoPlaceholder.Visible = !data.demod_locked;
            if (!mdVideoPlaceholder.Visible)
                mdVideoHosts[mdActiveTuner].BringToFront();
        }

        private void AdoptVideoControls()
        {
            for (int i = 0; i < 4; i++)
            {
                if (video_panels == null || video_panels[i] == null || mdVideoHosts[i] == null)
                    continue;

                List<Control> controls = video_panels[i].Controls.Cast<Control>().ToList();
                foreach (Control c in controls)
                {
                    video_panels[i].Controls.Remove(c);
                    mdVideoHosts[i].Controls.Add(c);
                    if (!(c is Label) && !(c is TrackBar) && !(c is Button))
                        c.Dock = DockStyle.Fill;
                    c.BringToFront();
                }
            }
        }

        private void RefreshDashboardStatus()
        {
            if (mdConnection == null)
                return;
            bool connected = source_connected && videoSource != null;
            mdConnection.Text = connected ? "● CONNECTED" : "READY";
            mdConnection.ForeColor = connected ? ModernTheme.Success : ModernTheme.TextSecondary;
            mdConnection.BackColor = connected ? Color.FromArgb(22, 48, 37) : ModernTheme.SurfaceRaised;
        }

        private static Panel CardPanel()
        {
            return new Panel
            {
                BackColor = ModernTheme.Surface,
                ForeColor = ModernTheme.TextPrimary
            };
        }

        private static Label SectionTitle(string text)
        {
            return new Label
            {
                Text = text,
                AutoSize = true,
                Font = new Font("Segoe UI Semibold", 10f),
                ForeColor = ModernTheme.TextSecondary
            };
        }

        private static Label SmallCaption(string text)
        {
            return new Label
            {
                Text = text,
                AutoSize = true,
                Font = new Font("Segoe UI Semibold", 8f),
                ForeColor = ModernTheme.TextMuted
            };
        }

        private static Label MakePill(string text, Color fore, Color back, int width)
        {
            return new Label
            {
                Text = text,
                Size = new Size(width, 30),
                TextAlign = ContentAlignment.MiddleCenter,
                Font = ModernTheme.FontBodySemibold,
                ForeColor = fore,
                BackColor = back
            };
        }

        private static Label MetricLabel(string name, string value)
        {
            return new Label
            {
                Text = name + "      " + value,
                Size = new Size(245, 36),
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font("Segoe UI Semibold", 12f),
                ForeColor = ModernTheme.TextPrimary
            };
        }
    }
}