using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
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
        private Button connectionPill;
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
        private int visibleTuners = 2;
        private bool connectRequested;
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
            LoadSources();
            LoadPresets();

            Shown += delegate { StartBatcSpectrum(); };

            uiTimer = new Timer { Interval = 300 };
            uiTimer.Tick += delegate
            {
                // Scan must run independently of video adoption and status UI.
                // An exception in those paths previously left Auto Scan ARMED.
                try { CheckQuickTuneAutoScan(); }
                catch (Exception ex)
                {
                    Serilog.Log.Warning(ex, "QO100 Auto Scan tick failed");
                    for (int rx = 0; rx < 2; rx++)
                        if (autoScanEnabled[rx] != null && autoScanEnabled[rx].Checked)
                            SetAutoScanState(rx, "SCAN ERROR");
                }
                try { AdoptVideoControls(); }
                catch (Exception ex) { Serilog.Log.Warning(ex, "Video control adoption failed"); }
                try { RefreshConnectionState(); }
                catch (Exception ex) { Serilog.Log.Warning(ex, "Connection status refresh failed"); }
            };
            uiTimer.Start();
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
            work.Controls.Add(spectrumCard);

            TableLayoutPanel tunerGrid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                BackColor = Bg,
                Padding = new Padding(0, 10, 0, 0)
            };
            tunerGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            tunerGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            tunerGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            work.Controls.Add(tunerGrid);
            tunerGrid.BringToFront();

            tunerGrid.Controls.Add(BuildTunerCard(0), 0, 0);
            tunerGrid.Controls.Add(BuildTunerCard(1), 1, 0);
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

            Button source = FlatButton("SOURCE SETTINGS", false); source.Width = 126; source.Height = 32;
            source.Click += delegate { if (sourceCombo.SelectedIndex >= 0) backend.BackendShowSourceSettings(sourceCombo.SelectedIndex); };
            p.Controls.Add(source);

            Button connect = FlatButton("CONNECT", true); connect.Width = 86; connect.Height = 32; connect.Click += delegate { ToggleConnection(); };
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
                sourceCombo.Location = new Point(x - sourceCombo.Width, 14);
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
            Panel card = Card(); card.Dock = DockStyle.Fill; card.Margin = tuner == 0 ? new Padding(0,0,6,0) : new Padding(6,0,0,0); card.Padding = new Padding(10);

            Label title = new Label { Text = "▣  Tuner " + (tuner + 1), Dock = DockStyle.Top, Height = 30, ForeColor = Text, Font = new Font("Segoe UI Semibold", 11f) };
            card.Controls.Add(title);

            Panel controls = new Panel { Dock = DockStyle.Bottom, Height = 190, BackColor = Surface };
            card.Controls.Add(controls);

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
            if (switchingConnection) return;
            switchingConnection = true;
            try
            {
                if (backend.BackendSourceInitialised)
                {
                    connectionPill.Text = "DISCONNECTING";
                    connectionPill.Enabled = false;
                    bool resetOk = backend.BackendDisconnectSource(backend.BackendUsesPicoEthernet);
                    connectRequested = false;
                    audioInitialised = false;
                    for (int i = 0; i < 2; i++)
                    {
                        lastData[i] = null;
                        lastSignalStatus[i] = DateTime.MinValue;
                        noLockSince[i] = DateTime.MinValue;
                        spectrumMissingSince[i] = DateTime.MinValue;
                    }
                    if (!resetOk)
                        MessageBox.Show("Disconnected locally, but the PicoTuner RESET could not be confirmed. Check the device address and connection.", "OpenTuner", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                else Connect();
            }
            finally
            {
                switchingConnection = false;
                connectionPill.Enabled = true;
                RefreshConnectionState();
            }
        }

        private void Connect()
        {
            if (sourceCombo.SelectedIndex < 0) return;
            connectRequested = true; connectionPill.Text = "CONNECTING";

            // Give the backend the final visible video destinations before it creates
            // the media players. This avoids native renderer HWND re-parenting.
            backend.BackendSetVideoHosts(videoHosts.Cast<Control>().ToArray());
            bool ok = backend.BackendConnectSource(sourceCombo.SelectedIndex);
            if (!ok) { connectRequested = false; connectionPill.Text = "ERROR"; return; }
            visibleTuners = Math.Max(1, Math.Min(2, backend.BackendTunerCount));
            if (!audioInitialised)
            {
                for (int i = 0; i < visibleTuners; i++) { backend.BackendEnsureAudible(i); int v = backend.BackendGetVolume(i); if (v <= 0) v = 60; volumeBars[i].Value = Math.Max(0, Math.Min(100, v)); savedVolume[i] = volumeBars[i].Value; }
                audioInitialised = true;
            }
        }

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
            if (tuner > 1) return;
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
                for (int rx = 0; rx < 2; rx++) SetAutoScanState(rx, "NO SIGNALS");
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
                if (lastData[rx] != null && lastData[rx].demod_locked)
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
                    // Do not collide with another receiver's valid lock.
                    !(lastData[other] != null && lastData[other].demod_locked &&
                      Math.Abs(sig.frequency * 1000.0 - lastData[other].frequency) < 75) &&
                    // Nor choose the same signal while the other receiver is
                    // actively attempting to acquire it.
                    !((now - lastAutoTune[other]).TotalSeconds < 18 &&
                      Math.Abs(sig.frequency - lastAutoFrequency[other]) < 0.075))
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
            if (enabled) lastAutoScanCheck = DateTime.MinValue;
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
            for (int i = 0; i < 2; i++)
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
            bool initialised = backend.BackendSourceInitialised;
            bool live = backend.BackendTransportConnected;
            if (initialised && live)
            {
                connectRequested = false;
                connectionPill.Text = "● CONNECTED";
                connectionPill.ForeColor = Green;
                connectionPill.BackColor = Color.FromArgb(17, 56, 44);
            }
            else if (initialised && backend.BackendUsesPicoEthernet)
            {
                connectRequested = false;
                connectionPill.Text = "● OFFLINE";
                connectionPill.ForeColor = Color.FromArgb(255, 190, 100);
                connectionPill.BackColor = Color.FromArgb(65, 42, 27);
            }
            else if (connectRequested)
            {
                connectionPill.Text = "CONNECTING";
                connectionPill.ForeColor = Muted;
                connectionPill.BackColor = Surface2;
            }
            else
            {
                connectionPill.Text = "READY";
                connectionPill.ForeColor = Muted;
                connectionPill.BackColor = Surface2;
            }
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            if (uiTimer != null) uiTimer.Stop();
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