using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using opentuner.MediaSources;

namespace opentuner
{
    public sealed class ModernMainForm : Form
    {
        private readonly MainForm backend;
        private readonly OTSourceData[] lastData = new OTSourceData[4];
        private readonly Panel[] videoHosts = new Panel[4];
        private readonly Button[] tunerButtons = new Button[4];

        private ComboBox sourceCombo;
        private ComboBox tunerViewCombo;
        private Label connectionPill;
        private Label receiverTitle;
        private Label serviceLabel;
        private Label modcodeLabel;
        private Label lockPill;
        private Label merLabel;
        private Label marginLabel;
        private Label frequencyReadout;
        private NumericUpDown frequencyInput;
        private NumericUpDown symbolRateInput;
        private FlowLayoutPanel presetList;
        private Label videoPlaceholder;
        private Timer uiTimer;
        private int activeTuner;
        private bool connectRequested;

        public ModernMainForm(string[] args)
        {
            Text = "OpenTuner Modern - " + GlobalDefines.Version;
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(1180, 720);
            Size = new Size(1540, 900);
            BackColor = ModernTheme.Background;
            ForeColor = ModernTheme.TextPrimary;
            Font = ModernTheme.FontBody;

            backend = new MainForm(args);
            backend.ShowInTaskbar = false;
            backend.Opacity = 0;
            backend.BackendPrepare();
            backend.BackendSourceData += BackendSourceData;

            BuildInterface();
            LoadSources();
            LoadPresets();
            ApplyTunerView();

            uiTimer = new Timer { Interval = 350 };
            uiTimer.Tick += delegate
            {
                AdoptVideoControls();
                RefreshConnectionState();
                ApplyTunerView();
            };
            uiTimer.Start();
        }

        private void BuildInterface()
        {
            Panel root = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = ModernTheme.Background,
                Padding = new Padding(18)
            };
            Controls.Add(root);

            Panel header = BuildHeader();
            header.Dock = DockStyle.Top;
            header.Height = 64;
            root.Controls.Add(header);

            Panel status = BuildStatusBar();
            status.Dock = DockStyle.Bottom;
            status.Height = 34;
            root.Controls.Add(status);

            Panel body = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = ModernTheme.Background,
                Padding = new Padding(0, 14, 0, 12)
            };
            root.Controls.Add(body);
            body.BringToFront();

            Panel left = BuildTuningPanel();
            left.Dock = DockStyle.Left;
            left.Width = 300;
            body.Controls.Add(left);

            Panel right = BuildPresetPanel();
            right.Dock = DockStyle.Right;
            right.Width = 250;
            body.Controls.Add(right);

            Panel center = BuildVideoPanel();
            center.Dock = DockStyle.Fill;
            center.Padding = new Padding(14, 0, 14, 0);
            body.Controls.Add(center);
            center.BringToFront();
        }

        private Panel BuildHeader()
        {
            Panel header = CardPanel();

            Label brand = new Label
            {
                Text = "OpenTuner",
                AutoSize = true,
                Font = new Font("Segoe UI Semibold", 18f),
                ForeColor = ModernTheme.TextPrimary,
                Location = new Point(18, 16)
            };
            header.Controls.Add(brand);

            Label sub = new Label
            {
                Text = "DATV RECEIVER CONSOLE",
                AutoSize = true,
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = ModernTheme.TextMuted,
                Location = new Point(145, 25)
            };
            header.Controls.Add(sub);

            sourceCombo = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Size = new Size(190, 30),
                FlatStyle = FlatStyle.Flat,
                BackColor = ModernTheme.SurfaceRaised,
                ForeColor = ModernTheme.TextPrimary,
                Font = ModernTheme.FontBody
            };
            header.Controls.Add(sourceCombo);

            Button sourceSettings = ModernTheme.CreateButton("SOURCE SETTINGS", false);
            sourceSettings.Size = new Size(128, 32);
            sourceSettings.Click += delegate
            {
                if (sourceCombo.SelectedIndex >= 0)
                    backend.BackendShowSourceSettings(sourceCombo.SelectedIndex);
            };
            header.Controls.Add(sourceSettings);

            Button connect = ModernTheme.CreateButton("CONNECT", true);
            connect.Size = new Size(92, 32);
            connect.Click += delegate { ConnectSelectedSource(); };
            header.Controls.Add(connect);

            Button settings = ModernTheme.CreateButton("SETTINGS", false);
            settings.Size = new Size(88, 32);
            settings.Click += delegate { backend.BackendShowGeneralSettings(); };
            header.Controls.Add(settings);

            connectionPill = MakePill("READY", ModernTheme.TextSecondary, ModernTheme.SurfaceRaised, 110);
            header.Controls.Add(connectionPill);

            header.Resize += delegate
            {
                int x = header.ClientSize.Width - 16;
                connectionPill.Location = new Point(x - connectionPill.Width, 17);
                x -= connectionPill.Width + 8;
                settings.Location = new Point(x - settings.Width, 16);
                x -= settings.Width + 8;
                connect.Location = new Point(x - connect.Width, 16);
                x -= connect.Width + 8;
                sourceSettings.Location = new Point(x - sourceSettings.Width, 16);
                x -= sourceSettings.Width + 8;
                sourceCombo.Location = new Point(x - sourceCombo.Width, 17);
            };

            return header;
        }

        private Panel BuildTuningPanel()
        {
            Panel panel = CardPanel();
            panel.Padding = new Padding(18);

            Label heading = SectionTitle("TUNING");
            heading.Location = new Point(18, 18);
            panel.Controls.Add(heading);

            FlowLayoutPanel tunerRow = new FlowLayoutPanel
            {
                Location = new Point(18, 49),
                Size = new Size(264, 36),
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                BackColor = Color.Transparent
            };
            for (int i = 0; i < 4; i++)
            {
                int tuner = i;
                Button b = ModernTheme.CreateButton("T" + (i + 1), i == 0);
                b.Size = new Size(54, 30);
                b.Margin = new Padding(0, 0, 8, 0);
                b.Click += delegate { SelectTuner(tuner); };
                tunerButtons[i] = b;
                tunerRow.Controls.Add(b);
            }
            panel.Controls.Add(tunerRow);

            Label viewCaption = SmallCaption("VIEW");
            viewCaption.Location = new Point(18, 99);
            panel.Controls.Add(viewCaption);

            tunerViewCombo = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Location = new Point(74, 94),
                Size = new Size(126, 28),
                FlatStyle = FlatStyle.Flat,
                BackColor = ModernTheme.SurfaceRaised,
                ForeColor = ModernTheme.TextPrimary
            };
            tunerViewCombo.Items.AddRange(new object[] { "AUTO", "2 TUNERS", "4 TUNERS" });
            tunerViewCombo.SelectedIndex = 0;
            tunerViewCombo.SelectedIndexChanged += delegate { ApplyTunerView(); };
            panel.Controls.Add(tunerViewCombo);

            Label freqCaption = SmallCaption("FREQUENCY");
            freqCaption.Location = new Point(18, 143);
            panel.Controls.Add(freqCaption);

            frequencyReadout = new Label
            {
                Location = new Point(18, 164),
                Size = new Size(264, 48),
                Text = "10491.500 MHz",
                Font = new Font("Segoe UI Semibold", 23f),
                ForeColor = ModernTheme.TextPrimary,
                TextAlign = ContentAlignment.MiddleLeft
            };
            panel.Controls.Add(frequencyReadout);

            frequencyInput = new NumericUpDown
            {
                Location = new Point(18, 217),
                Size = new Size(264, 32),
                DecimalPlaces = 3,
                Increment = 0.001M,
                Minimum = 0,
                Maximum = 15000,
                Value = 10491.500M,
                BackColor = ModernTheme.SurfaceRaised,
                ForeColor = ModernTheme.TextPrimary,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 11f)
            };
            frequencyInput.ValueChanged += delegate
            {
                frequencyReadout.Text = frequencyInput.Value.ToString("0.000") + " MHz";
            };
            panel.Controls.Add(frequencyInput);

            Label srCaption = SmallCaption("SYMBOL RATE (KS/s)");
            srCaption.Location = new Point(18, 266);
            panel.Controls.Add(srCaption);

            symbolRateInput = new NumericUpDown
            {
                Location = new Point(18, 287),
                Size = new Size(264, 32),
                Minimum = 1,
                Maximum = 5000,
                Value = 333,
                BackColor = ModernTheme.SurfaceRaised,
                ForeColor = ModernTheme.TextPrimary,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 11f)
            };
            panel.Controls.Add(symbolRateInput);

            Button tune = ModernTheme.CreateButton("TUNE", true);
            tune.Location = new Point(18, 336);
            tune.Size = new Size(264, 42);
            tune.Font = new Font("Segoe UI Semibold", 11f);
            tune.Click += delegate { Tune(); };
            panel.Controls.Add(tune);

            Label signalHeading = SectionTitle("SIGNAL");
            signalHeading.Location = new Point(18, 410);
            panel.Controls.Add(signalHeading);

            lockPill = MakePill("NO LOCK", ModernTheme.Danger, Color.FromArgb(55, 28, 31), 112);
            lockPill.Location = new Point(18, 443);
            panel.Controls.Add(lockPill);

            merLabel = MetricLabel("MER", "— dB");
            merLabel.Location = new Point(18, 493);
            panel.Controls.Add(merLabel);

            marginLabel = MetricLabel("MARGIN", "— dB");
            marginLabel.Location = new Point(18, 539);
            panel.Controls.Add(marginLabel);

            return panel;
        }

        private Panel BuildVideoPanel()
        {
            Panel outer = new Panel { BackColor = ModernTheme.Background };
            Panel frame = CardPanel();
            frame.Dock = DockStyle.Fill;
            frame.Padding = new Padding(2);
            outer.Controls.Add(frame);

            Panel title = new Panel
            {
                Dock = DockStyle.Top,
                Height = 52,
                BackColor = ModernTheme.Surface
            };
            frame.Controls.Add(title);

            receiverTitle = new Label
            {
                Text = "RECEIVER 1",
                Location = new Point(16, 8),
                AutoSize = true,
                Font = ModernTheme.FontSection,
                ForeColor = ModernTheme.TextPrimary
            };
            title.Controls.Add(receiverTitle);

            serviceLabel = new Label
            {
                Text = "Waiting for signal",
                Location = new Point(16, 30),
                AutoSize = true,
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = ModernTheme.TextMuted
            };
            title.Controls.Add(serviceLabel);

            modcodeLabel = new Label
            {
                Size = new Size(310, 34),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                TextAlign = ContentAlignment.MiddleRight,
                Font = ModernTheme.FontBodySemibold,
                ForeColor = ModernTheme.TextSecondary
            };
            title.Controls.Add(modcodeLabel);
            title.Resize += delegate { modcodeLabel.Location = new Point(title.Width - modcodeLabel.Width - 16, 9); };

            Panel viewport = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Black
            };
            frame.Controls.Add(viewport);
            viewport.BringToFront();

            for (int i = 0; i < 4; i++)
            {
                videoHosts[i] = new Panel
                {
                    Dock = DockStyle.Fill,
                    BackColor = Color.Black,
                    Visible = i == 0
                };
                viewport.Controls.Add(videoHosts[i]);
            }

            videoPlaceholder = new Label
            {
                Dock = DockStyle.Fill,
                Text = "NO SIGNAL\r\n\r\nSelect a receiver source and tune to a DATV signal",
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 11f),
                ForeColor = ModernTheme.TextMuted,
                BackColor = Color.Black
            };
            viewport.Controls.Add(videoPlaceholder);
            videoPlaceholder.BringToFront();

            return outer;
        }

        private Panel BuildPresetPanel()
        {
            Panel panel = CardPanel();
            panel.Padding = new Padding(16);

            Label heading = SectionTitle("PRESETS");
            heading.Location = new Point(16, 18);
            panel.Controls.Add(heading);

            presetList = new FlowLayoutPanel
            {
                Location = new Point(16, 52),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                Size = new Size(218, 500),
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = true,
                BackColor = Color.Transparent
            };
            panel.Controls.Add(presetList);

            Button manage = ModernTheme.CreateButton("MANAGE PRESETS", false);
            manage.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;
            manage.Size = new Size(218, 38);
            manage.Click += delegate
            {
                backend.BackendManagePresets();
                LoadPresets();
            };
            panel.Controls.Add(manage);

            panel.Resize += delegate
            {
                presetList.Width = panel.Width - 32;
                presetList.Height = Math.Max(120, panel.Height - 112);
                manage.Location = new Point(16, panel.Height - 52);
                manage.Width = panel.Width - 32;
            };

            return panel;
        }

        private Panel BuildStatusBar()
        {
            Panel bar = new Panel { BackColor = ModernTheme.Surface };
            Label left = new Label
            {
                Dock = DockStyle.Left,
                Width = 620,
                Text = "OpenTuner modern-ui • standalone interface",
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(10, 0, 0, 0),
                ForeColor = ModernTheme.TextMuted
            };
            bar.Controls.Add(left);

            Label right = new Label
            {
                Dock = DockStyle.Right,
                Width = 260,
                Text = "Receiver engine: OpenTuner",
                TextAlign = ContentAlignment.MiddleRight,
                Padding = new Padding(0, 0, 10, 0),
                ForeColor = ModernTheme.TextMuted
            };
            bar.Controls.Add(right);
            return bar;
        }

        private void LoadSources()
        {
            sourceCombo.Items.Clear();
            foreach (string name in backend.BackendSourceNames())
                sourceCombo.Items.Add(name);

            int selected = backend.BackendSelectedSourceIndex;
            if (selected < 0 || selected >= sourceCombo.Items.Count)
                selected = 0;
            if (sourceCombo.Items.Count > 0)
                sourceCombo.SelectedIndex = selected;
        }

        private void ConnectSelectedSource()
        {
            if (sourceCombo.SelectedIndex < 0)
                return;

            connectRequested = true;
            connectionPill.Text = "CONNECTING";
            connectionPill.ForeColor = ModernTheme.TextSecondary;
            connectionPill.BackColor = ModernTheme.SurfaceRaised;

            bool started = backend.BackendConnectSource(sourceCombo.SelectedIndex);
            if (!started)
            {
                connectRequested = false;
                connectionPill.Text = "ERROR";
                connectionPill.ForeColor = ModernTheme.Danger;
                MessageBox.Show("OpenTuner could not initialise the selected receiver source.", "OpenTuner", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            ApplyTunerView();
        }

        private void Tune()
        {
            if (!backend.BackendConnected)
                return;

            uint freq = Convert.ToUInt32(decimal.Round(frequencyInput.Value * 1000M, 0));
            uint sr = Convert.ToUInt32(symbolRateInput.Value);
            backend.BackendTune(activeTuner, freq, sr);
        }

        private void BackendSourceData(int tuner, OTSourceData data, string description)
        {
            if (data == null || tuner < 0 || tuner >= lastData.Length)
                return;

            lastData[tuner] = data;
            if (IsDisposed)
                return;

            if (InvokeRequired)
            {
                BeginInvoke((MethodInvoker)delegate
                {
                    if (tuner == activeTuner)
                        ApplyData(tuner, data);
                });
            }
            else if (tuner == activeTuner)
            {
                ApplyData(tuner, data);
            }
        }

        private void ApplyData(int tuner, OTSourceData data)
        {
            receiverTitle.Text = "RECEIVER " + (tuner + 1);
            serviceLabel.Text = string.IsNullOrWhiteSpace(data.service_name) ? "Waiting for service information" : data.service_name;
            modcodeLabel.Text = data.modcode ?? "";

            lockPill.Text = data.demod_locked ? "● LOCKED" : "NO LOCK";
            lockPill.ForeColor = data.demod_locked ? ModernTheme.Success : ModernTheme.Danger;
            lockPill.BackColor = data.demod_locked ? Color.FromArgb(22, 48, 37) : Color.FromArgb(55, 28, 31);
            merLabel.Text = "MER      " + data.mer.ToString("0.0", CultureInfo.InvariantCulture) + " dB";
            marginLabel.Text = "MARGIN   " + data.db_margin.ToString("0.0", CultureInfo.InvariantCulture) + " dB";

            if (data.frequency > 0)
            {
                decimal mhz = data.frequency / 1000M;
                if (mhz >= frequencyInput.Minimum && mhz <= frequencyInput.Maximum)
                    frequencyInput.Value = mhz;
                frequencyReadout.Text = mhz.ToString("0.000") + " MHz";
            }
            if (data.symbol_rate > 0 && data.symbol_rate <= symbolRateInput.Maximum)
                symbolRateInput.Value = data.symbol_rate;

            videoPlaceholder.Visible = !data.demod_locked;
            if (data.demod_locked)
                videoHosts[activeTuner].BringToFront();
        }

        private void AdoptVideoControls()
        {
            for (int i = 0; i < 4; i++)
            {
                Control[] controls = backend.BackendTakeVideoControls(i);
                if (controls == null || controls.Length == 0)
                    continue;

                foreach (Control control in controls)
                {
                    if (control.Parent == videoHosts[i])
                        continue;
                    videoHosts[i].Controls.Add(control);
                    if (!(control is Label) && !(control is TrackBar) && !(control is Button))
                        control.Dock = DockStyle.Fill;
                    control.BringToFront();
                }
            }
        }

        private void SelectTuner(int tuner)
        {
            if (tuner < 0 || tuner > 3)
                return;
            activeTuner = tuner;

            for (int i = 0; i < 4; i++)
            {
                bool active = i == tuner;
                tunerButtons[i].BackColor = active ? ModernTheme.Accent : ModernTheme.SurfaceRaised;
                tunerButtons[i].FlatAppearance.BorderColor = active ? ModernTheme.Accent : ModernTheme.Border;
                videoHosts[i].Visible = active;
            }

            receiverTitle.Text = "RECEIVER " + (tuner + 1);
            if (lastData[tuner] != null)
                ApplyData(tuner, lastData[tuner]);
            else
            {
                serviceLabel.Text = "Waiting for signal";
                modcodeLabel.Text = "";
                lockPill.Text = "NO LOCK";
                lockPill.ForeColor = ModernTheme.Danger;
                merLabel.Text = "MER      — dB";
                marginLabel.Text = "MARGIN   — dB";
                videoPlaceholder.Visible = true;
                videoPlaceholder.BringToFront();
            }
        }

        private int RequestedTunerCount()
        {
            if (tunerViewCombo != null)
            {
                if (tunerViewCombo.SelectedIndex == 1) return 2;
                if (tunerViewCombo.SelectedIndex == 2) return 4;
            }
            if (backend.BackendConnected)
                return backend.BackendTunerCount >= 4 ? 4 : 2;
            return 2;
        }

        private void ApplyTunerView()
        {
            int count = RequestedTunerCount();
            for (int i = 0; i < tunerButtons.Length; i++)
                tunerButtons[i].Visible = i < count;
            if (activeTuner >= count)
                SelectTuner(0);
        }

        private void RefreshConnectionState()
        {
            bool connected = backend.BackendConnected;
            if (connected)
            {
                connectRequested = false;
                connectionPill.Text = "● CONNECTED";
                connectionPill.ForeColor = ModernTheme.Success;
                connectionPill.BackColor = Color.FromArgb(22, 48, 37);
                string device = backend.BackendDeviceName;
                if (!string.IsNullOrWhiteSpace(device))
                    Text = "OpenTuner Modern - " + device;
            }
            else if (connectRequested)
            {
                connectionPill.Text = "CONNECTING";
                connectionPill.ForeColor = ModernTheme.TextSecondary;
                connectionPill.BackColor = ModernTheme.SurfaceRaised;
            }
            else
            {
                connectionPill.Text = "READY";
                connectionPill.ForeColor = ModernTheme.TextSecondary;
                connectionPill.BackColor = ModernTheme.SurfaceRaised;
            }
        }

        private void LoadPresets()
        {
            if (presetList == null)
                return;

            presetList.Controls.Clear();
            List<StoredFrequency> presets = backend.BackendPresets();
            foreach (StoredFrequency preset in presets.Take(16))
            {
                StoredFrequency p = preset;
                Button b = ModernTheme.CreateButton(p.Name, false);
                b.Width = 205;
                b.Height = 42;
                b.Margin = new Padding(0, 0, 0, 7);
                b.TextAlign = ContentAlignment.MiddleLeft;
                b.Padding = new Padding(9, 0, 4, 0);
                b.Click += delegate
                {
                    frequencyInput.Value = Math.Min(frequencyInput.Maximum, Math.Max(frequencyInput.Minimum, p.Frequency / 1000M));
                    symbolRateInput.Value = Math.Min(symbolRateInput.Maximum, Math.Max(symbolRateInput.Minimum, p.SymbolRate));
                    Tune();
                };
                presetList.Controls.Add(b);
            }

            if (presetList.Controls.Count == 0)
            {
                presetList.Controls.Add(new Label
                {
                    Text = "No presets saved yet",
                    AutoSize = true,
                    ForeColor = ModernTheme.TextMuted,
                    Margin = new Padding(4, 8, 0, 0)
                });
            }
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            if (uiTimer != null)
                uiTimer.Stop();
            backend.BackendSourceData -= BackendSourceData;
            backend.BackendShutdown();
            backend.Dispose();
            base.OnFormClosed(e);
        }

        private static Panel CardPanel()
        {
            return new Panel { BackColor = ModernTheme.Surface, ForeColor = ModernTheme.TextPrimary };
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
                Size = new Size(264, 36),
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font("Segoe UI Semibold", 12f),
                ForeColor = ModernTheme.TextPrimary
            };
        }
    }
}
