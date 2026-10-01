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
        private Label connectionPill;
        private FlowLayoutPanel presetList;
        private PictureBox batcSpectrumBox;
        private BATCSpectrum batcSpectrum;
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
            Text = "OpenTuner Modern - " + GlobalDefines.Version;
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
                AdoptVideoControls();
                RefreshConnectionState();
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
            spectrumCard.Height = 185;
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
            Label title = new Label { Text = "OpenTuner   0.C.2   MODERN RECEIVER", AutoSize = true, Location = new Point(16, 17), ForeColor = Text, Font = new Font("Segoe UI Semibold", 12f) };
            p.Controls.Add(title);

            sourceCombo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 190, Height = 30, BackColor = Surface2, ForeColor = Text, FlatStyle = FlatStyle.Flat };
            p.Controls.Add(sourceCombo);

            Button source = FlatButton("SOURCE SETTINGS", false); source.Width = 126; source.Height = 32;
            source.Click += delegate { if (sourceCombo.SelectedIndex >= 0) backend.BackendShowSourceSettings(sourceCombo.SelectedIndex); };
            p.Controls.Add(source);

            Button connect = FlatButton("CONNECT", true); connect.Width = 86; connect.Height = 32; connect.Click += delegate { Connect(); };
            p.Controls.Add(connect);

            Button settings = FlatButton("SETTINGS", false); settings.Width = 82; settings.Height = 32; settings.Click += delegate { backend.BackendShowGeneralSettings(); };
            p.Controls.Add(settings);

            connectionPill = new Label { Text = "READY", Size = new Size(106, 30), TextAlign = ContentAlignment.MiddleCenter, BackColor = Surface2, ForeColor = Muted, Font = new Font("Segoe UI Semibold", 9f) };
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
            batcSpectrumBox = new PictureBox { Location = new Point(10, 34), Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right, BackColor = Color.Black, SizeMode = PictureBoxSizeMode.StretchImage };
            p.Controls.Add(batcSpectrumBox);
            p.Resize += delegate { batcSpectrumBox.Size = new Size(Math.Max(10, p.Width - 20), Math.Max(10, p.Height - 44)); };
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

        private void Connect()
        {
            if (sourceCombo.SelectedIndex < 0) return;
            connectRequested = true; connectionPill.Text = "CONNECTING";
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
            lockLabels[tuner].Text = data.demod_locked ? "● LOCKED" : "NO LOCK";
            lockLabels[tuner].ForeColor = data.demod_locked ? Green : Muted;
            lockLabels[tuner].BackColor = data.demod_locked ? Color.FromArgb(17, 56, 44) : Surface2;
            merLabels[tuner].Text = "MER  " + data.mer.ToString("0.0", CultureInfo.InvariantCulture) + " dB";
            marginLabels[tuner].Text = "Margin  " + data.db_margin.ToString("0.0", CultureInfo.InvariantCulture) + " dB";
            if (data.frequency > 0) liveFreq[tuner].Text = (data.frequency / 1000M).ToString("0.000") + " MHz";
        }

        private void AdoptVideoControls()
        {
            for (int i = 0; i < 2; i++)
            {
                Control[] controls = backend.BackendTakeVideoControls(i);
                if (controls == null) continue;
                foreach (Control c in controls)
                {
                    if (c.Parent != videoHosts[i]) videoHosts[i].Controls.Add(c);
                    if (!(c is Label) && !(c is TrackBar) && !(c is Button)) c.Dock = DockStyle.Fill;
                    c.BringToFront();
                }
            }
        }

        private void RefreshConnectionState()
        {
            if (backend.BackendSourceInitialised)
            {
                connectRequested = false; connectionPill.Text = "● CONNECTED"; connectionPill.ForeColor = Green; connectionPill.BackColor = Color.FromArgb(17,56,44);
            }
            else if (connectRequested) { connectionPill.Text = "CONNECTING"; connectionPill.ForeColor = Muted; }
            else { connectionPill.Text = "READY"; connectionPill.ForeColor = Muted; }
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
