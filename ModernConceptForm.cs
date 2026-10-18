using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Windows.Forms;
using opentuner.ExtraFeatures.BATCSpectrum;
using opentuner.MediaSources;

namespace opentuner
{
    public sealed class ModernConceptForm : Form
    {
        private readonly MainForm backend;
        private readonly OTSourceData[] lastData = new OTSourceData[4];
        private readonly Panel[] videoHosts = new Panel[4];
        private readonly NumericUpDown[] freqInputs = new NumericUpDown[4];
        private readonly NumericUpDown[] srInputs = new NumericUpDown[4];
        private readonly Label[] liveFreq = new Label[4];
        private readonly Label[] serviceLabels = new Label[4];
        private readonly Label[] lockLabels = new Label[4];
        private readonly Label[] merLabels = new Label[4];
        private readonly Label[] marginLabels = new Label[4];
        private readonly TrackBar[] volumeBars = new TrackBar[4];
        private readonly Button[] muteButtons = new Button[4];
        private readonly int[] savedVolume = new int[] { 60, 60, 60, 60 };

        private ComboBox sourceCombo;
        private ComboBox layoutCombo;
        private Button connectionPill;
        private Button connectButton;
        private volatile bool picoResetPending;
        private int picoResetGeneration;
        private FlowLayoutPanel presetList;
        private PictureBox batcSpectrumBox;
        private BATCSpectrum batcSpectrum;
        private ModernBatcSpectrumControl activeQuickTuneSpectrum;
        private readonly CheckBox[] autoScanEnabled = new CheckBox[2];
        private readonly DateTime[] noLockSince = new DateTime[2];
        private readonly DateTime[] lastAutoTune = new DateTime[2];
        private readonly DateTime[] lastSignalStatus = new DateTime[2];
        private readonly double[] lastAutoFrequency = new double[2];
        private readonly string[] autoScanState = new string[] { "OFF", "OFF" };
        private readonly Label[] autoScanStatusLabels = new Label[2];
        private readonly DateTime[] spectrumMissingSince = new DateTime[2];
        private DateTime lastAutoScanCheck = DateTime.MinValue;
        private Timer uiTimer;
        private Timer autoScanTimer;
        private int visibleTuners = 2;
        private TableLayoutPanel receiverGrid;
        private Panel spectrumCardPanel;
        private readonly Panel[] receiverCards = new Panel[4];
        private readonly Panel[] receiverControls = new Panel[4];
        private readonly Dictionary<Control, Rectangle> originalReceiverBounds = new Dictionary<Control, Rectangle>();
        private readonly Dictionary<Control, Font> originalReceiverFonts = new Dictionary<Control, Font>();
        private bool connectRequested;
        private string lastConnectionDiagnostic;
        private bool audioInitialised;

        private static readonly Color Bg = Color.FromArgb(7, 17, 29);
        private static readonly Color Surface = Color.FromArgb(13, 28, 45);
        private static readonly Color Surface2 = Color.FromArgb(18, 38, 60);
        private static readonly Color Border = Color.FromArgb(37, 67, 94);
        private static readonly Color Accent = Color.FromArgb(28, 139, 253);
        private static readonly Color Green = Color.FromArgb(40, 222, 126);
        private static readonly Color Text = Color.FromArgb(242, 247, 252);
        private static readonly Color Muted = Color.FromArgb(142, 165, 190);

        public ModernConceptForm(string[] args)
        {
            base.Text = ModernBranding.ProductTitle;
            StartPosition = FormStartPosition.CenterScreen;
            WindowState = FormWindowState.Maximized;
            MinimumSize = new Size(1280, 760);
            BackColor = Bg;
            ForeColor = Text;
            Font = new Font("Segoe UI", 9f);

            backend = new MainForm(args);
            backend.ShowInTaskbar = false;
            backend.Opacity = 0;
            backend.BackendPrepare();
            backend.BackendSourceData += BackendSourceData;

            BuildInterface();
            ApplyReceiverCount();
            LoadSources();
            Serilog.Log.Information("[CONNECT-DIAG] Startup sources={Sources}, selected={Index}, name={Name}", sourceCombo.Items.Count, sourceCombo.SelectedIndex, sourceCombo.SelectedItem);
            LoadPresets();

            Shown += delegate { StartBatcSpectrum(); };

            uiTimer = new Timer { Interval = 300 };
            uiTimer.Tick += delegate
            {
                // Scan must run independently of video adoption and status UI.
                // An exception in those paths previously left Auto Scan ARMED.
                try { AdoptVideoControls(); }
                catch (Exception ex) { Serilog.Log.Warning(ex, "Video control adoption failed"); }
                try { RefreshConnectionState(); }
                catch (Exception ex) { Serilog.Log.Warning(ex, "Connection status refresh failed"); }
            };
            uiTimer.Start();

            // Independent periodic scanner: video/control UI updates must never
            // starve the QO-100 scan state machine.
            autoScanTimer = new Timer { Interval = 1000 };
            autoScanTimer.Tick += delegate { AutoScanTick(); };
            Shown += delegate { autoScanTimer.Start(); AutoScanTick(); };
        }

        private void BuildInterface()
        {
            Panel root = new Panel { Dock = DockStyle.Fill, BackColor = Bg };
            Controls.Add(root);

            Panel nav = BuildNav();
            nav.Dock = DockStyle.Left;
            nav.Width = 150;
            root.Controls.Add(nav);

            Panel content = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12), BackColor = Bg };
            root.Controls.Add(content);
            content.BringToFront();

            Panel header = BuildHeader();
            header.Dock = DockStyle.Top;
            header.Height = 56;
            content.Controls.Add(header);

            Panel footer = BuildFooter();
            footer.Dock = DockStyle.Bottom;
            footer.Height = 34;
            content.Controls.Add(footer);

            Panel main = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, 10, 0, 8), BackColor = Bg };
            content.Controls.Add(main);
            main.BringToFront();

            Panel right = BuildRightPanel();
            right.Dock = DockStyle.Right;
            right.Width = 270;
            main.Controls.Add(right);

            Panel work = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, 0, 10, 0), BackColor = Bg };
            main.Controls.Add(work);
            work.BringToFront();

            Panel spectrumCard = BuildSpectrumCard();
            spectrumCard.Dock = DockStyle.Top;
            spectrumCard.Height = 319;
            spectrumCardPanel = spectrumCard;
            work.Controls.Add(spectrumCard);

            TableLayoutPanel tunerGrid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 2,
                BackColor = Bg,
                Padding = new Padding(0, 10, 0, 0)
            };
            tunerGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            tunerGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            tunerGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
            tunerGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
            work.Controls.Add(tunerGrid);
            tunerGrid.BringToFront();

            tunerGrid.Controls.Add(BuildTunerCard(0), 0, 0);
            tunerGrid.Controls.Add(BuildTunerCard(1), 1, 0);
            tunerGrid.Controls.Add(BuildTunerCard(2), 0, 1);
            tunerGrid.Controls.Add(BuildTunerCard(3), 1, 1);
            receiverGrid = tunerGrid;
        }

        private Panel BuildNav()
        {
            Panel p = new Panel { BackColor = Color.FromArgb(8, 22, 37), Padding = new Padding(10, 14, 10, 10) };
            Label brand = new Label { Text = "◉  OpenTuner", Dock = DockStyle.Top, Height = 46, ForeColor = Text, Font = new Font("Segoe UI Semibold", 14f), TextAlign = ContentAlignment.MiddleLeft };
            p.Controls.Add(brand);

            string[] items = { "▣  Receive", "☆  Presets", "▥  Spectrum", "⌕  Scan", "●  Recordings", "▣  Snapshots", "◉  Band Profiles", "○  Chat (BATC)", "🔧  External Tools", "⚙  Settings" };
            FlowLayoutPanel list = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 500, FlowDirection = FlowDirection.TopDown, WrapContents = false, BackColor = Color.Transparent };
            foreach (string item in items)
            {
                Button b = FlatButton(item, item.Contains("Receive"));
                b.Width = 128; b.Height = 40; b.TextAlign = ContentAlignment.MiddleLeft;
                if (item.Contains("Settings")) b.Click += delegate { backend.BackendShowGeneralSettings(); };
                list.Controls.Add(b);
            }
            p.Controls.Add(list);
            list.BringToFront();
            return p;
        }

        private Panel BuildHeader()
        {
            Panel p = Card();
            Label title = new Label { Text = ModernBranding.ProductTitle, AutoSize = true, Location = new Point(16, 17), ForeColor = Text, Font = new Font("Segoe UI Semibold", 12f) };
            p.Controls.Add(title);

            sourceCombo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 190, Height = 30, BackColor = Surface2, ForeColor = Text, FlatStyle = FlatStyle.Flat };
            p.Controls.Add(sourceCombo);
            layoutCombo = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = 83, Height = 30, BackColor = Surface2,
                ForeColor = Text, FlatStyle = FlatStyle.Flat
            };
            layoutCombo.Items.Add("AUTO");
            layoutCombo.SelectedIndex = 0;
            layoutCombo.Enabled = false;
            layoutCombo.SelectedIndexChanged += delegate { ApplyReceiverCount(); };
            p.Controls.Add(layoutCombo);

            Button source = FlatButton("SOURCE SETTINGS", false); source.Width = 126; source.Height = 32;
            source.Click += delegate { if (sourceCombo.SelectedIndex >= 0) backend.BackendShowSourceSettings(sourceCombo.SelectedIndex); };
            p.Controls.Add(source);

            Button connect = FlatButton("CONNECT", true); connect.Width = 86; connect.Height = 32; connect.Click += delegate { ToggleConnection(); };
            connectButton = connect;
            p.Controls.Add(connect);

            Button settings = FlatButton("SETTINGS", false); settings.Width = 82; settings.Height = 32; settings.Click += delegate { backend.BackendShowGeneralSettings(); };
            p.Controls.Add(settings);

            connectionPill = FlatButton("READY", false);
            connectionPill.Size = new Size(112, 30);
            connectionPill.Click += delegate { ToggleConnection(); };
            p.Controls.Add(connectionPill);

            p.Resize += delegate
            {
                int x = p.ClientSize.Width - 12;
                connectionPill.Location = new Point(x - connectionPill.Width, 13); x -= connectionPill.Width + 7;
                settings.Location = new Point(x - settings.Width, 12); x -= settings.Width + 7;
                connect.Location = new Point(x - connect.Width, 12); x -= connect.Width + 7;
                source.Location = new Point(x - source.Width, 12); x -= source.Width + 7;
                sourceCombo.Location = new Point(x - sourceCombo.Width, 14); x -= sourceCombo.Width + 7;
                layoutCombo.Location = new Point(x - layoutCombo.Width, 14);
            };
            return p;
        }

        private Panel BuildSpectrumCard()
        {
            Panel p = Card();
            Label t = new Label { Text = "BATC WIDEBAND QUICK TUNE   •   click a signal to tune", AutoSize = true, Location = new Point(14, 9), ForeColor = Text, Font = new Font("Segoe UI Semibold", 9.5f) };
            p.Controls.Add(t);
            batcSpectrumBox = new PictureBox
            {
                Location = new Point(10, 34),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                BackColor = Color.Black,
                SizeMode = PictureBoxSizeMode.Normal,
                Size = new Size(922, 275)
            };
            p.Controls.Add(batcSpectrumBox);
            // Each receiver gets an independent opt-in auto scan checkbox in
            // its own half of the BATC spectrum (above/below the RX divider).
            for (int rx = 0; rx < 2; rx++)
            {
                int index = rx;
                CheckBox check = new CheckBox
                {
                    Text = "RX" + (rx + 1) + " Auto Scan",
                    Checked = false,
                    AutoSize = false,
                    Size = new Size(132, 24),
                    Location = new Point(Math.Max(4, batcSpectrumBox.Width - 145), rx == 0 ? 65 : 205),
                    BackColor = Surface2,
                    ForeColor = Text,
                    Font = new Font("Segoe UI Semibold", 8.5f),
                    Cursor = Cursors.Hand
                };
                check.CheckedChanged += delegate
                {
                    noLockSince[index] = DateTime.MinValue;
                    spectrumMissingSince[index] = DateTime.MinValue;
                    lastAutoTune[index] = DateTime.MinValue;
                    lastAutoFrequency[index] = 0;
                };
                batcSpectrumBox.Controls.Add(check);
                check.BringToFront();
                autoScanEnabled[rx] = check;
            }

            p.Resize += delegate
            {
                batcSpectrumBox.Size = new Size(Math.Max(10, p.Width - 20), 275);
                for (int rx = 0; rx < 2; rx++)
                    if (autoScanEnabled[rx] != null)
                        autoScanEnabled[rx].Left = Math.Max(4, batcSpectrumBox.Width - autoScanEnabled[rx].Width - 12);
            };
            return p;
        }

        private Panel BuildTunerCard(int tuner)
        {
            Panel card = Card(); card.Dock = DockStyle.Fill; card.Margin = tuner % 2 == 0 ? new Padding(0,0,6,4) : new Padding(6,0,0,4); card.Padding = new Padding(10);
            receiverCards[tuner] = card;

            Label title = new Label { Text = "▣  RX" + (tuner + 1), Dock = DockStyle.Top, Height = 30, ForeColor = Text, Font = new Font("Segoe UI Semibold", 11f) };
            card.Controls.Add(title);

            Panel controls = new Panel { Dock = DockStyle.Bottom, Height = 190, BackColor = Surface };
            card.Controls.Add(controls);
            receiverControls[tuner] = controls;

            videoHosts[tuner] = new Panel { Dock = DockStyle.Fill, BackColor = Color.Black };
            card.Controls.Add(videoHosts[tuner]); videoHosts[tuner].BringToFront();

            liveFreq[tuner] = new Label { Text = "— MHz", Location = new Point(4, 6), Size = new Size(230, 38), ForeColor = Text, Font = new Font("Segoe UI Semibold", 21f), TextAlign = ContentAlignment.MiddleLeft };
            controls.Controls.Add(liveFreq[tuner]);

            freqInputs[tuner] = new NumericUpDown { DecimalPlaces = 3, Increment = .001M, Minimum = 0, Maximum = 15000, Value = 10491.500M, Location = new Point(238, 10), Width = 120, BackColor = Surface2, ForeColor = Text };
            controls.Controls.Add(freqInputs[tuner]);
            srInputs[tuner] = new NumericUpDown { Minimum = 1, Maximum = 5000, Value = 333, Location = new Point(364, 10), Width = 82, BackColor = Surface2, ForeColor = Text };
            controls.Controls.Add(srInputs[tuner]);
            Button tune = FlatButton("TUNE", true); tune.Location = new Point(452, 7); tune.Size = new Size(76, 32); tune.Click += delegate { Tune(tuner); };
            controls.Controls.Add(tune);

            lockLabels[tuner] = new Label { Text = "NO LOCK", Location = new Point(4, 51), Size = new Size(105, 28), TextAlign = ContentAlignment.MiddleCenter, ForeColor = Muted, BackColor = Surface2, Font = new Font("Segoe UI Semibold", 9f) };
            controls.Controls.Add(lockLabels[tuner]);
            serviceLabels[tuner] = new Label { Text = "Waiting for service", Location = new Point(120, 55), Size = new Size(220, 24), ForeColor = Muted };
            controls.Controls.Add(serviceLabels[tuner]);
            merLabels[tuner] = new Label { Text = "MER  — dB", Location = new Point(350, 55), Size = new Size(95, 24), ForeColor = Text };
            controls.Controls.Add(merLabels[tuner]);
            marginLabels[tuner] = new Label { Text = "Margin  — dB", Location = new Point(445, 55), Size = new Size(100, 24), ForeColor = Text };
            controls.Controls.Add(marginLabels[tuner]);

            Label volIcon = new Label { Text = "🔊", Location = new Point(4, 99), Size = new Size(28, 26), ForeColor = Text, TextAlign = ContentAlignment.MiddleCenter };
            controls.Controls.Add(volIcon);
            volumeBars[tuner] = new TrackBar { Minimum = 0, Maximum = 100, Value = 60, TickStyle = TickStyle.None, Location = new Point(34, 96), Width = 160, Height = 30, BackColor = Surface };
            volumeBars[tuner].Scroll += delegate { savedVolume[tuner] = volumeBars[tuner].Value; backend.BackendSetVolume(tuner, volumeBars[tuner].Value); muteButtons[tuner].Text = volumeBars[tuner].Value == 0 ? "UNMUTE" : "MUTE"; };
            controls.Controls.Add(volumeBars[tuner]);
            muteButtons[tuner] = FlatButton("MUTE", false); muteButtons[tuner].Location = new Point(200, 95); muteButtons[tuner].Size = new Size(74, 32);
            muteButtons[tuner].Click += delegate
            {
                int current = backend.BackendGetVolume(tuner);
                if (current > 0) { savedVolume[tuner] = current; backend.BackendSetVolume(tuner, 0); volumeBars[tuner].Value = 0; muteButtons[tuner].Text = "UNMUTE"; }
                else { int v = Math.Max(20, savedVolume[tuner]); backend.BackendSetVolume(tuner, v); volumeBars[tuner].Value = v; muteButtons[tuner].Text = "MUTE"; }
            };
            controls.Controls.Add(muteButtons[tuner]);

            Button record = FlatButton("● RECORD", false); record.Location = new Point(282, 95); record.Size = new Size(88, 32); controls.Controls.Add(record);
            Button snap = FlatButton("▣ SNAPSHOT", false); snap.Location = new Point(378, 95); snap.Size = new Size(96, 32); controls.Controls.Add(snap);
            Button full = FlatButton("⛶", false); full.Location = new Point(482, 95); full.Size = new Size(46, 32); controls.Controls.Add(full);

            Label hint = new Label { Text = "Frequency and SR edit boxes are independent from live telemetry", Location = new Point(4, 142), AutoSize = true, ForeColor = Muted, Font = new Font("Segoe UI", 8.5f) };
            controls.Controls.Add(hint);
            return card;
        }

        private Panel BuildRightPanel()
        {
            Panel p = Card(); p.Padding = new Padding(12);
            Label title = new Label { Text = "PRESETS", Dock = DockStyle.Top, Height = 32, ForeColor = Text, Font = new Font("Segoe UI Semibold", 10f) };
            p.Controls.Add(title);
            presetList = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, BackColor = Color.Transparent, Padding = new Padding(0,8,0,0) };
            p.Controls.Add(presetList); presetList.BringToFront();
            Button manage = FlatButton("+  MANAGE PRESETS", true); manage.Dock = DockStyle.Bottom; manage.Height = 38; manage.Click += delegate { backend.BackendManagePresets(); LoadPresets(); };
            p.Controls.Add(manage);
            return p;
        }

        private Panel BuildFooter()
        {
            Panel p = new Panel { BackColor = Color.FromArgb(9, 24, 39) };
            Label l = new Label { Dock = DockStyle.Fill, Text = "●  OpenTuner   •   BATC Wideband Quick Tune   •   Receiver engine: OpenTuner", ForeColor = Muted, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(10,0,0,0) };
            p.Controls.Add(l); return p;
        }

        private void LoadSources()
        {
            sourceCombo.Items.Clear();
            foreach (string s in backend.BackendSourceNames()) sourceCombo.Items.Add(s);
            int idx = backend.BackendSelectedSourceIndex;
            if (idx < 0 || idx >= sourceCombo.Items.Count) idx = 0;
            if (sourceCombo.Items.Count > 0) sourceCombo.SelectedIndex = idx;
            Serilog.Log.Information("[CONNECT-DIAG] Source list loaded: UI index={UiIndex}, backend index={BackendIndex}, source={Name}, options={Options}",
                sourceCombo.SelectedIndex, backend.BackendSelectedSourceIndex, sourceCombo.SelectedItem,
                string.Join(", ", sourceCombo.Items.Cast<object>().Select(x => x.ToString())));
        }

        private void LoadPresets()
        {
            presetList.Controls.Clear();
            foreach (StoredFrequency p in backend.BackendPresets().Take(18))
            {
                StoredFrequency preset = p;
                Button b = FlatButton("★  " + preset.Name + "   " + (preset.Frequency / 1000M).ToString("0.000") + " MHz   " + preset.SymbolRate + " kS", false);
                b.Width = 238; b.Height = 42; b.TextAlign = ContentAlignment.MiddleLeft;
                b.Click += delegate { freqInputs[0].Value = Clamp(freqInputs[0], preset.Frequency / 1000M); srInputs[0].Value = Clamp(srInputs[0], preset.SymbolRate); Tune(0); };
                presetList.Controls.Add(b);
            }
            if (presetList.Controls.Count == 0) presetList.Controls.Add(new Label { Text = "No presets saved yet", AutoSize = true, ForeColor = Muted, Margin = new Padding(4,8,0,0) });
        }

        private decimal Clamp(NumericUpDown n, decimal value) { return Math.Min(n.Maximum, Math.Max(n.Minimum, value)); }

        private bool switchingConnection;

        private void ToggleConnection()
        {
            Serilog.Log.Information("[CONNECT-DIAG] Click: switching={Switching}, reset={Reset}, selected={Index}, source={Source}, initialised={Initialised}, buttonEnabled={Enabled}", switchingConnection, picoResetPending, sourceCombo.SelectedIndex, sourceCombo.SelectedItem, backend.BackendSourceInitialised, connectButton != null && connectButton.Enabled);
            if (switchingConnection || picoResetPending) { Serilog.Log.Warning("[CONNECT-DIAG] Click ignored: switching={Switching}, reset={Reset}", switchingConnection, picoResetPending); return; }
            switchingConnection = true;
            try
            {
                if (backend.BackendSourceInitialised)
                {
                    bool resetPico = backend.BackendUsesPicoEthernet;
                    if (resetPico) picoResetPending = true;
                    connectionPill.Text = "DISCONNECTING";
                    connectionPill.Enabled = false;
                    if (connectButton != null) connectButton.Enabled = false;
                    Serilog.Log.Information("[CONNECT-DIAG] Disconnect requested; resetPico={ResetPico}", resetPico);
                    bool resetOk = backend.BackendDisconnectSource(resetPico);
                    Serilog.Log.Information("[CONNECT-DIAG] Disconnect returned={Result}", resetOk);
                    connectRequested = false;
                    audioInitialised = false;
                    visibleTuners = 2;
                    RefreshLayoutOptions();
                    ApplyReceiverCount();
                    for (int i = 0; i < 2; i++)
                    {
                        lastData[i] = null;
                        lastSignalStatus[i] = DateTime.MinValue;
                        noLockSince[i] = DateTime.MinValue;
                        spectrumMissingSince[i] = DateTime.MinValue;
                    }
                    if (resetPico) WaitForPicoReturn();
                    if (!resetOk)
                        MessageBox.Show("Disconnected locally, but the PicoTuner RESET could not be confirmed. Check the device address and connection.", "OpenTuner", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                else Connect();
            }
            finally
            {
                switchingConnection = false;
                connectionPill.Enabled = !picoResetPending;
                if (connectButton != null) connectButton.Enabled = !picoResetPending;
                RefreshConnectionState();
            }
        }

        // The device drops its control/status transport during a hardware reset.
        // A fresh discovery broadcast proves the PicoTuner is back online.
        private void WaitForPicoReturn()
        {
            int generation = System.Threading.Interlocked.Increment(ref picoResetGeneration);
            Task.Run(delegate
            {
                // Allow the old firmware session to shut down before trusting
                // any fresh discovery advertisement.
                System.Threading.Thread.Sleep(3000);
                bool returned = false;
                try
                {
                    using (UdpClient listener = new UdpClient(9997))
                    {
                        listener.Client.ReceiveTimeout = 1500;
                        while (generation == picoResetGeneration && !IsDisposed)
                        {
                            try
                            {
                                IPEndPoint remote = new IPEndPoint(IPAddress.Any, 0);
                                byte[] message = listener.Receive(ref remote);
                                if (message != null && message.Length > 0)
                                {
                                    returned = true;
                                    break;
                                }
                            }
                            catch (SocketException ex)
                            {
                                if (ex.SocketErrorCode != SocketError.TimedOut) break;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Serilog.Log.Warning(ex, "PicoTuner reset discovery listener unavailable");
                }

                if (generation != picoResetGeneration || IsDisposed || !IsHandleCreated)
                    return;
                try
                {
                    BeginInvoke((MethodInvoker)delegate
                    {
                        if (generation != picoResetGeneration || IsDisposed) return;
                        if (returned)
                        {
                            picoResetPending = false;
                            connectionPill.Enabled = true;
                            if (connectButton != null) connectButton.Enabled = true;
                            RefreshConnectionState();
                        }
                        else
                        {
                            // Remain disconnected if discovery is unavailable;
                            // never permit an unsafe immediate reconnect.
                            connectionPill.Text = "PICO OFFLINE";
                            Serilog.Log.Warning("PicoTuner did not advertise readiness after reset");
                        }
                    });
                }
                catch (InvalidOperationException) { }
            });
        }

        private void Connect()
        {
            if (picoResetPending) { Serilog.Log.Warning("[CONNECT-DIAG] Connect ignored: reset pending"); return; }
            if (sourceCombo.SelectedIndex < 0) { Serilog.Log.Warning("[CONNECT-DIAG] Connect ignored: no source selected"); return; }
            Serilog.Log.Information("[CONNECT-DIAG] Connecting source={Index} name={Name}", sourceCombo.SelectedIndex, sourceCombo.SelectedItem);
            connectRequested = true; connectionPill.Text = "CONNECTING";

            // Give the backend the final visible video destinations before it creates
            // the media players. This avoids native renderer HWND re-parenting.
            backend.BackendSetVideoHosts(videoHosts.Cast<Control>().ToArray());
            bool ok = backend.BackendConnectSource(sourceCombo.SelectedIndex);
            Serilog.Log.Information("[CONNECT-DIAG] BackendConnectSource result={Result}, initialised={Initialised}, transport={Transport}", ok, backend.BackendSourceInitialised, backend.BackendTransportConnected);
            if (!ok) { connectRequested = false; connectionPill.Text = "ERROR"; return; }
            visibleTuners = Math.Max(1, Math.Min(4, backend.BackendTunerCount));
            RefreshLayoutOptions();
            ApplyReceiverCount();
            if (!audioInitialised)
            {
                for (int i = 0; i < visibleTuners; i++) { backend.BackendEnsureAudible(i); int v = backend.BackendGetVolume(i); if (v <= 0) v = 60; volumeBars[i].Value = Math.Max(0, Math.Min(100, v)); savedVolume[i] = volumeBars[i].Value; }
                audioInitialised = true;
            }
        }

        private void RefreshLayoutOptions()
        {
            if (layoutCombo == null) return;
            // Only advertise layouts which the currently connected backend
            // can genuinely provide; never create fictional RX channels.
            int count = backend.BackendSourceInitialised ? visibleTuners : 0;
            layoutCombo.BeginUpdate();
            try
            {
                layoutCombo.Items.Clear();
                layoutCombo.Items.Add("AUTO");
                if (count == 1) layoutCombo.Items.Add("1 RX");
                if (count >= 2) layoutCombo.Items.Add("2 RX");
                if (count >= 4) layoutCombo.Items.Add("4 RX");
                layoutCombo.SelectedIndex = 0;
                layoutCombo.Enabled = count > 0;
            }
            finally { layoutCombo.EndUpdate(); }
        }

        private void ApplyReceiverCount()
        {
            int displayed = visibleTuners;
            if (layoutCombo != null && layoutCombo.Enabled)
            {
                string selection = layoutCombo.SelectedItem as string;
                if (selection == "1 RX") displayed = Math.Min(1, visibleTuners);
                else if (selection == "2 RX") displayed = Math.Min(2, visibleTuners);
                else if (selection == "4 RX") displayed = Math.Min(4, visibleTuners);
            }
            bool dual = displayed > 1;
            bool four = displayed > 2;
            if (receiverGrid != null)
            {
                receiverGrid.SuspendLayout();
                for (int i = 0; i < receiverCards.Length; i++)
                    if (receiverCards[i] != null) receiverCards[i].Visible = i < displayed;
                receiverGrid.ColumnStyles[0].Width = dual ? 50f : 100f;
                receiverGrid.ColumnStyles[1].Width = dual ? 50f : 0f;
                receiverGrid.RowStyles[0].Height = four ? 50f : 100f;
                receiverGrid.RowStyles[1].Height = four ? 50f : 0f;
                receiverGrid.ResumeLayout(true);
            }
            if (spectrumCardPanel != null) spectrumCardPanel.Height = four ? 200 : 319;
            for (int i = 0; i < receiverControls.Length; i++)
                if (receiverControls[i] != null) ApplyCompactReceiverLayout(i, four);
            if (activeQuickTuneSpectrum != null)
                activeQuickTuneSpectrum.SetReceiverCount(Math.Min(2, visibleTuners));
            if (autoScanEnabled[1] != null) autoScanEnabled[1].Visible = visibleTuners > 1;
            if (autoScanStatusLabels[1] != null) autoScanStatusLabels[1].Visible = visibleTuners > 1;
        }

        private void ApplyCompactReceiverLayout(int rx, bool compact)
        {
            Panel panel = receiverControls[rx];
            if (panel == null) return;
            panel.Height = compact ? 117 : 190;
            if (!compact)
            {
                foreach (Control control in panel.Controls)
                {
                    Rectangle original;
                    if (originalReceiverBounds.TryGetValue(control, out original))
                        control.Bounds = original;
                    Font originalFont;
                    if (originalReceiverFonts.TryGetValue(control, out originalFont))
                        control.Font = originalFont;
                    if (control is Label && control.Text.StartsWith("Frequency and SR"))
                        control.Visible = true;
                }
                return;
            }

            panel.SuspendLayout();
            foreach (Control control in panel.Controls)
            {
                if (!originalReceiverBounds.ContainsKey(control))
                {
                    originalReceiverBounds[control] = control.Bounds;
                    originalReceiverFonts[control] = control.Font;
                }
            }
            panel.Font = new Font("Segoe UI", 7.5f);
            liveFreq[rx].Location = new Point(2, 2);
            liveFreq[rx].Size = new Size(130, 27);
            liveFreq[rx].Font = new Font("Segoe UI Semibold", 13f);
            freqInputs[rx].Location = new Point(135, 4);
            freqInputs[rx].Width = 94;
            srInputs[rx].Location = new Point(233, 4);
            srInputs[rx].Width = 63;
            lockLabels[rx].Location = new Point(2, 33);
            lockLabels[rx].Size = new Size(75, 22);
            lockLabels[rx].Font = new Font("Segoe UI Semibold", 7.5f);
            serviceLabels[rx].Location = new Point(82, 34);
            serviceLabels[rx].Size = new Size(132, 19);
            merLabels[rx].Location = new Point(218, 34);
            merLabels[rx].Size = new Size(90, 19);
            marginLabels[rx].Location = new Point(218, 55);
            marginLabels[rx].Size = new Size(100, 18);
            // Reflow the existing buttons without replacing their Click handlers.
            Button tune = panel.Controls.OfType<Button>().FirstOrDefault(x => x.Text == "TUNE");
            if (tune != null) { tune.Location = new Point(300, 3); tune.Size = new Size(57, 25); tune.Font = new Font("Segoe UI Semibold", 7f); }
            Label speaker = panel.Controls.OfType<Label>().FirstOrDefault(x => x.Text == "🔊");
            if (speaker != null) { speaker.Location = new Point(2, 73); speaker.Size = new Size(20, 22); }
            volumeBars[rx].Location = new Point(22, 72);
            volumeBars[rx].Width = 85;
            muteButtons[rx].Location = new Point(108, 72);
            muteButtons[rx].Size = new Size(53, 24);
            muteButtons[rx].Font = new Font("Segoe UI Semibold", 7f);
            Button record = panel.Controls.OfType<Button>().FirstOrDefault(x => x.Text.Contains("RECORD"));
            if (record != null) { record.Location = new Point(165, 72); record.Size = new Size(66, 24); record.Font = new Font("Segoe UI", 7f); }
            Button snap = panel.Controls.OfType<Button>().FirstOrDefault(x => x.Text.Contains("SNAPSHOT"));
            if (snap != null) { snap.Location = new Point(235, 72); snap.Size = new Size(75, 24); snap.Font = new Font("Segoe UI", 7f); }
            Button full = panel.Controls.OfType<Button>().FirstOrDefault(x => x.Text == "⛶");
            if (full != null) { full.Location = new Point(314, 72); full.Size = new Size(35, 24); }
            Label hint = panel.Controls.OfType<Label>().FirstOrDefault(x => x.Text.StartsWith("Frequency and SR"));
            if (hint != null) hint.Visible = false;
            panel.ResumeLayout(true);
        }

        internal int ModernReceiverCount { get { return visibleTuners; } }

        private void Tune(int tuner)
        {
            if (!backend.BackendSourceInitialised) return;
            uint freq = Convert.ToUInt32(decimal.Round(freqInputs[tuner].Value * 1000M, 0));
            uint sr = Convert.ToUInt32(srInputs[tuner].Value);
            backend.BackendTune(tuner, freq, sr);
        }

        private void StartBatcSpectrum()
        {
            try
            {
                if (batcSpectrum != null || batcSpectrumBox.Width < 50) return;
                batcSpectrum = new BATCSpectrum(batcSpectrumBox, 2);
                batcSpectrum.OnSignalSelected += delegate(int receiver, uint freq, uint sr)
                {
                    if (IsDisposed) return;
                    BeginInvoke((MethodInvoker)delegate
                    {
                        int tuner = Math.Max(0, Math.Min(1, receiver));
                        decimal mhz = freq / 1000M;
                        if (mhz >= freqInputs[tuner].Minimum && mhz <= freqInputs[tuner].Maximum) freqInputs[tuner].Value = mhz;
                        if (sr >= srInputs[tuner].Minimum && sr <= srInputs[tuner].Maximum) srInputs[tuner].Value = sr;
                        Tune(tuner);
                    });
                };
            }
            catch (Exception ex)
            {
                batcSpectrumBox.BackColor = Color.Black;
                batcSpectrumBox.Paint += delegate(object sender, PaintEventArgs e) { e.Graphics.DrawString("BATC spectrum unavailable: " + ex.Message, Font, Brushes.OrangeRed, 8, 8); };
            }
        }

        private void BackendSourceData(int tuner, OTSourceData data, string description)
        {
            if (data == null || tuner < 0 || tuner >= lastData.Length) return;
            lastData[tuner] = data;
            if (IsDisposed) return;
            if (InvokeRequired) BeginInvoke((MethodInvoker)delegate { ApplyData(tuner, data); }); else ApplyData(tuner, data);
        }

        private void ApplyData(int tuner, OTSourceData data)
        {
            if (tuner >= visibleTuners || tuner < 0) return;
            serviceLabels[tuner].Text = string.IsNullOrWhiteSpace(data.service_name) ? "Waiting for service" : data.service_name;
            lastSignalStatus[tuner] = DateTime.UtcNow;
            if (data.demod_locked && activeQuickTuneSpectrum != null && data.frequency > 0)
            {
                activeQuickTuneSpectrum.UpdateLockedReceiverMarker(
                    tuner, data.frequency / 1000.0, data.symbol_rate / 1000f);
            }
            lockLabels[tuner].Text = data.demod_locked ? "● LOCKED" : "NO LOCK";
            lockLabels[tuner].ForeColor = data.demod_locked ? Green : Muted;
            lockLabels[tuner].BackColor = data.demod_locked ? Color.FromArgb(17, 56, 44) : Surface2;
            merLabels[tuner].Text = "MER  " + data.mer.ToString("0.0", CultureInfo.InvariantCulture) + " dB";
            marginLabels[tuner].Text = "Margin  " + data.db_margin.ToString("0.0", CultureInfo.InvariantCulture) + " dB";
            if (data.frequency > 0) liveFreq[tuner].Text = (data.frequency / 1000M).ToString("0.000") + " MHz";
        }

        private void AutoScanTick()
        {
            if (IsDisposed || Disposing) return;
            for (int rx = 0; rx < 2; rx++)
            {
                if (autoScanEnabled[rx] != null && autoScanEnabled[rx].Checked &&
                    autoScanState[rx] == "ARMED")
                    SetAutoScanState(rx, "CHECKING");
            }
            try { CheckQuickTuneAutoScan(); }
            catch (Exception ex)
            {
                Serilog.Log.Warning(ex, "QO100 Auto Scan tick failed");
                for (int rx = 0; rx < 2; rx++)
                    if (autoScanEnabled[rx] != null && autoScanEnabled[rx].Checked)
                        SetAutoScanState(rx, "SCAN ERROR");
            }
        }

        // Deliberately opt-in; do not chase short fades or interrupt a lock.
        // Scan candidates from the BATC Quick Tune signal detector, not presets.
        private void CheckQuickTuneAutoScan()
        {
            DateTime now = DateTime.UtcNow;
            if ((now - lastAutoScanCheck).TotalSeconds < 2) return;
            lastAutoScanCheck = now;

            if (!backend.BackendSourceInitialised || !backend.BackendTransportConnected || activeQuickTuneSpectrum == null)
            {
                for (int rx = 0; rx < 2; rx++) SetAutoScanState(rx, "WAITING FOR RX");
                return;
            }

            bool any = (autoScanEnabled[0] != null && autoScanEnabled[0].Checked) ||
                       (autoScanEnabled[1] != null && autoScanEnabled[1].Checked);
            if (!any)
            {
                for (int rx = 0; rx < 2; rx++) SetAutoScanState(rx, "OFF");
                return;
            }

            List<signal.Sig> detected;
            if (!activeQuickTuneSpectrum.TryDetectedSignalsSnapshot(out detected))
            {
                for (int rx = 0; rx < 2; rx++) SetAutoScanState(rx, "NO BATC DATA");
                return;
            }
            var candidates = detected
                .Where(sig => sig.frequency > 10490.5 && sig.frequency < 10500.0 &&
                              Math.Abs(sig.frequency - 10491.5) > 0.10 &&
                              sig.sr >= 0.065f)
                .OrderBy(sig => sig.frequency).ToList();
            if (candidates.Count == 0)
            {
                for (int rx = 0; rx < Math.Min(2, visibleTuners); rx++)
                {
                    bool enabled = autoScanEnabled[rx] != null && autoScanEnabled[rx].Checked;
                    bool beacon = lastData[rx] != null && lastData[rx].frequency > 0 &&
                        Math.Abs(lastData[rx].frequency / 1000.0 - 10491.5) < 0.15;
                    SetAutoScanState(rx, !enabled ? "OFF" : beacon ? "BEACON STANDBY" : "NO SIGNALS");
                }
                return;
            }

            for (int rx = 0; rx < Math.Min(2, visibleTuners); rx++)
            {
                if (autoScanEnabled[rx] == null || !autoScanEnabled[rx].Checked)
                {
                    SetAutoScanState(rx, "OFF");
                    continue;
                }
                // Do not act on stale lock information or while the receiver
                // is being initialised and awaiting its first status message.
                // Some PicoTuner firmware versions do not send periodic status
                // for an idle/unlocked RX. A live connection with no RX status
                // should still be eligible for scanning. Never override a
                // receiver known to be locked, even if its status goes stale.
                bool onBeacon = lastData[rx] != null && lastData[rx].frequency > 0 &&
                    Math.Abs(lastData[rx].frequency / 1000.0 - 10491.5) < 0.15;
                // Beacon lock is a standby condition, not a programme to protect.
                // The normal 8-second scan dwell still avoids rapid retuning.
                if (lastData[rx] != null && lastData[rx].demod_locked && !onBeacon)
                {
                    // Some receiver firmware keeps reporting the previous lock
                    // after the transmission moves. Check the current spectrum
                    // before deciding that the old lock is still usable.
                    double currentMHz = lastData[rx].frequency / 1000.0;
                    bool present = detected.Any(sig =>
                        Math.Abs(sig.frequency - currentMHz) <
                        Math.Max(0.10, sig.sr / 2.0 + 0.045));
                    if (present || currentMHz < 10490.5 || currentMHz > 10499.5)
                    {
                        spectrumMissingSince[rx] = DateTime.MinValue;
                        noLockSince[rx] = DateTime.MinValue;
                        SetAutoScanState(rx, "LOCKED");
                        continue;
                    }
                    if (spectrumMissingSince[rx] == DateTime.MinValue)
                        spectrumMissingSince[rx] = now;
                    if ((now - spectrumMissingSince[rx]).TotalSeconds < 12)
                    {
                        SetAutoScanState(rx, "CHECK SIGNAL");
                        continue;
                    }
                }
                else spectrumMissingSince[rx] = DateTime.MinValue;
                if (noLockSince[rx] == DateTime.MinValue)
                {
                    noLockSince[rx] = now;
                    SetAutoScanState(rx, "WAIT");
                    continue;
                }
                if ((now - noLockSince[rx]).TotalSeconds < 8 ||
                    (now - lastAutoTune[rx]).TotalSeconds < 10)
                {
                    SetAutoScanState(rx, "WAIT");
                    continue;
                }

                // Avoid retuning onto the same frequency as an already locked
                // receiver, and cycle onward after an unsuccessful trial.
                int other = 1 - rx;
                var available = candidates.Where(sig =>
                    // Never take the beacon, including wide detected blocks that
                    // overlap its centre even when their measured centre differs.
                    Math.Abs(sig.frequency - 10491.5) > (0.10 + sig.sr / 2.0) &&
                    // Reserve the whole occupied signal bandwidth, not merely a
                    // fixed 75 kHz around the other tuner. This covers 333/500/
                    // 1500 kS transmissions, slight frequency offsets, and
                    // the case where the other receiver is still acquiring lock.
                    !AutoScanCandidateOccupiedByOther(rx, sig))
                    .ToList();
                if (available.Count == 0) { SetAutoScanState(rx, "NO FREE SIGNAL"); continue; }
                var selected = available.FirstOrDefault(sig => sig.frequency > lastAutoFrequency[rx] + 0.05);
                if (selected.frequency <= 0) selected = available[0];
                uint freqKHz = Convert.ToUInt32(Math.Round(selected.frequency * 1000.0));
                uint srKs = Convert.ToUInt32(Math.Round(selected.sr * 1000.0));
                decimal mhz = freqKHz / 1000M;
                if (mhz < freqInputs[rx].Minimum || mhz > freqInputs[rx].Maximum ||
                    srKs < srInputs[rx].Minimum || srKs > srInputs[rx].Maximum)
                { SetAutoScanState(rx, "INVALID SR"); continue; }

                Serilog.Log.Information("QO100 Auto Scan RX{Receiver}: trying {FreqMHz:F3} MHz / {SymbolRate} kS", rx + 1, selected.frequency, srKs);
                lastAutoFrequency[rx] = selected.frequency;
                lastAutoTune[rx] = now;
                freqInputs[rx].Value = mhz;
                srInputs[rx].Value = srKs;
                SetAutoScanState(rx, "TUNING " + selected.frequency.ToString("0.000", CultureInfo.InvariantCulture));
                Tune(rx);
            }
        }

        private bool AutoScanCandidateOccupiedByOther(int receiver, signal.Sig candidate)
        {
            int other = 1 - receiver;
            if (other >= visibleTuners) return false;

            // Prefer the confirmed hardware lock, since the backend can report
            // a slightly different centre frequency from the BATC detector.
            if (lastData[other] != null && lastData[other].demod_locked &&
                lastData[other].frequency > 0)
            {
                double lockedMHz = lastData[other].frequency / 1000.0;
                double lockedSrMHz = lastData[other].symbol_rate / 1000.0;
                double guardMHz = Math.Max(0.10, (candidate.sr + lockedSrMHz) / 2.0 + 0.05);
                if (Math.Abs(candidate.frequency - lockedMHz) < guardMHz)
                    return true;
            }

            // Once Auto Scan has assigned a signal to the other receiver, keep
            // it reserved for as long as it remains tuned there, including
            // while waiting to acquire lock. Do not expire this reservation
            // after an arbitrary number of seconds.
            if (autoScanEnabled[other] != null && autoScanEnabled[other].Checked &&
                lastAutoTune[other] != DateTime.MinValue && lastAutoFrequency[other] > 0)
            {
                double otherSrMHz = (double)srInputs[other].Value / 1000.0;
                double guardMHz = Math.Max(0.10, (candidate.sr + otherSrMHz) / 2.0 + 0.05);
                if (Math.Abs(candidate.frequency - lastAutoFrequency[other]) < guardMHz)
                    return true;
            }

            return false;
        }

        internal void ModernAutoScanChanged(int receiver, bool enabled)
        {
            if (receiver < 0 || receiver >= 2) return;
            noLockSince[receiver] = DateTime.MinValue;
            spectrumMissingSince[receiver] = DateTime.MinValue;
            lastAutoTune[receiver] = DateTime.MinValue;
            lastAutoFrequency[receiver] = 0;
            // Force refresh even if the previous cached state was identical.
            autoScanState[receiver] = "";
            SetAutoScanState(receiver, enabled ? "ARMED" : "OFF");
            if (enabled)
            {
                lastAutoScanCheck = DateTime.MinValue;
                if (autoScanTimer != null && !autoScanTimer.Enabled) autoScanTimer.Start();
                AutoScanTick();
            }
        }

        private void SetAutoScanState(int receiver, string state)
        {
            if (receiver < 0 || receiver > 1) return;
            if (autoScanState[receiver] == state) return;
            autoScanState[receiver] = state;
            CheckBox cb = autoScanEnabled[receiver];
            Label label = autoScanStatusLabels[receiver];
            if (label != null && !label.IsDisposed)
                label.Text = state;
            Serilog.Log.Information("QO100 scan RX{Receiver}: {State}", receiver + 1, state);
        }

        private static bool IsVideoRenderer(Control control)
        {
            return control is LibVLCSharp.WinForms.VideoView ||
                   control is FlyleafLib.Controls.WinForms.FlyleafHost ||
                   control is PictureBox;
        }

        private void AdoptVideoControls()
        {
            for (int i = 0; i < Math.Min(4, visibleTuners); i++)
            {
                Control[] controls = backend.BackendTakeVideoControls(i);
                if (controls == null) continue;

                Control renderer = null;
                List<Control> overlays = new List<Control>();

                foreach (Control c in controls)
                {
                    if (c == null || c.IsDisposed) continue;

                    if (c.Parent != videoHosts[i])
                        videoHosts[i].Controls.Add(c);

                    if (IsVideoRenderer(c))
                    {
                        renderer = c;
                        c.Dock = DockStyle.Fill;
                        c.Visible = true;
                    }
                    else
                    {
                        // StreamInfoContainer and VolumeInfoContainer are overlays from the
                        // legacy player. They must retain their own size/location. Docking
                        // them Fill was covering the entire video surface while audio played.
                        if (c.Dock == DockStyle.Fill)
                            c.Dock = DockStyle.None;
                        overlays.Add(c);
                    }
                }

                if (renderer != null)
                    renderer.SendToBack();

                foreach (Control overlay in overlays)
                    overlay.BringToFront();
            }
        }

        private void RefreshConnectionState()
        {
            bool resetting = picoResetPending;
            bool initialised = backend.BackendSourceInitialised;
            bool live = initialised && backend.BackendTransportConnected;
            bool connected = initialised && live;
            bool offline = initialised && backend.BackendUsesPicoEthernet && !live;
            bool ready = !resetting && !switchingConnection && !connectRequested && !initialised;
            string diagnostic = string.Format("initialised={0} transport={1} reset={2} switching={3} request={4} ready={5} source={6}", initialised, live, resetting, switchingConnection, connectRequested, ready, sourceCombo != null ? sourceCombo.SelectedIndex : -1);
            if (diagnostic != lastConnectionDiagnostic)
            {
                lastConnectionDiagnostic = diagnostic;
                Serilog.Log.Information("[CONNECT-DIAG] State changed: {State}", diagnostic);
            }

            // Set the colours explicitly; a FlatStyle.Flat button does not
            // reliably change its background when Enabled becomes false.
            Color grey = Color.FromArgb(75, 84, 95);
            Color greyBorder = Color.FromArgb(108, 119, 131);
            if (connectButton != null)
            {
                connectButton.Enabled = (ready || connected || offline) && !switchingConnection && !resetting;
                connectButton.Text = resetting ? "RESETTING" : connected ? "CONNECTED" :
                    offline ? "OFFLINE" : connectRequested ? "CONNECTING" : "CONNECT";
                connectButton.BackColor = connected ? Color.FromArgb(17, 91, 59) : ready ? Accent : grey;
                connectButton.ForeColor = (ready || connected) ? Text : Color.FromArgb(210, 216, 223);
                connectButton.FlatAppearance.BorderColor = connected ? Color.FromArgb(38, 140, 86) : ready ? Accent : greyBorder;
                connectButton.Cursor = connectButton.Enabled ? Cursors.Hand : Cursors.Default;
            }

            connectionPill.Enabled = !resetting && !switchingConnection;
            connectionPill.Cursor = connectionPill.Enabled ? Cursors.Hand : Cursors.Default;
            if (resetting)
            {
                connectionPill.Text = "RESETTING";
                connectionPill.ForeColor = Color.FromArgb(210, 216, 223);
                connectionPill.BackColor = grey;
                connectionPill.FlatAppearance.BorderColor = greyBorder;
            }
            else if (connected)
            {
                connectRequested = false;
                connectionPill.Text = "● CONNECTED";
                connectionPill.ForeColor = Green;
                connectionPill.BackColor = Color.FromArgb(17, 56, 44);
                connectionPill.FlatAppearance.BorderColor = Color.FromArgb(38, 112, 83);
            }
            else if (offline)
            {
                connectionPill.Text = "● OFFLINE";
                connectionPill.ForeColor = Color.FromArgb(210, 216, 223);
                connectionPill.BackColor = grey;
                connectionPill.FlatAppearance.BorderColor = greyBorder;
            }
            else if (connectRequested)
            {
                connectionPill.Text = "CONNECTING";
                connectionPill.ForeColor = Muted;
                connectionPill.BackColor = grey;
                connectionPill.FlatAppearance.BorderColor = greyBorder;
            }
            else
            {
                connectionPill.Text = "READY";
                connectionPill.ForeColor = Muted;
                connectionPill.BackColor = Surface2;
                connectionPill.FlatAppearance.BorderColor = Border;
            }
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            if (uiTimer != null) uiTimer.Stop();
            if (autoScanTimer != null) { autoScanTimer.Stop(); autoScanTimer.Dispose(); }
            try { batcSpectrum?.Close(); } catch { }
            backend.BackendSourceData -= BackendSourceData;
            backend.BackendShutdown();
            backend.Dispose();
            base.OnFormClosed(e);
        }

        private Panel Card() { return new Panel { BackColor = Surface, ForeColor = Text }; }
        private Button FlatButton(string text, bool active)
        {
            Button b = new Button { Text = text, FlatStyle = FlatStyle.Flat, BackColor = active ? Accent : Surface2, ForeColor = Text, Font = new Font("Segoe UI Semibold", 9f), Cursor = Cursors.Hand };
            b.FlatAppearance.BorderColor = active ? Accent : Border; b.FlatAppearance.BorderSize = 1; return b;
        }
    }
}