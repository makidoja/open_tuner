using System;
using System.Collections.Generic;
using System.Linq;
using System.Drawing;
using System.Globalization;
using System.Reflection;
using System.Windows.Forms;
using opentuner.MediaSources;

namespace opentuner
{
    internal static class ModernReceiverBottomLayoutPass
    {
        private static bool attached;
        private static readonly HashSet<Panel> observedReceiverPanels = new HashSet<Panel>();
        private static bool applyingLayout;
        private static readonly Color Text = Color.FromArgb(242, 247, 252);
        private static readonly Color Muted = Color.FromArgb(142, 165, 190);
        private static readonly Panel[] infoTabs = new Panel[4];
        private static readonly Panel[,] infoPages = new Panel[4, 3];
        private static readonly Button[,] tabButtons = new Button[4, 3];
        private static readonly TsBitrateGraph[] tsGraphs = new TsBitrateGraph[4];
        private static readonly DateTime[] lastTsSample = new DateTime[4];
        private static readonly Label[] currentFreq = new Label[4];
        private static readonly Label[] currentMer = new Label[4];
        private static readonly Label[] currentMargin = new Label[4];
        private static readonly Panel[] currentMerBar = new Panel[4];
        private static readonly Panel[] currentMarginBar = new Panel[4];
        private static readonly Label[,] dataLabels = new Label[4, 8];

        public static void Attach(ModernConceptForm form)
        {
            if (attached || form == null) return;
            attached = true;

            form.Shown += delegate
            {
                Apply(form);
                HookInfoTelemetry(form);
                form.Resize += delegate { Apply(form); };
            };
        }

        private static void Apply(ModernConceptForm form)
        {
            if (form == null || form.IsDisposed || applyingLayout) return;
            applyingLayout = true;
            try
            {
            for (int tuner = 0; tuner < 4; tuner++)
            {
                Panel tunerBox = FindByName<Panel>(form, "ModernTunerControlBox" + tuner);
                if (tunerBox == null || tunerBox.Parent == null) continue;

                Panel controls = tunerBox.Parent as Panel;
                if (controls == null) continue;
                if (observedReceiverPanels.Add(controls))
                    controls.Resize += delegate { Apply(form); };

                bool compact4 = form.ModernReceiverCount >= 4;
                tunerBox.Size = compact4
                    ? new Size(Math.Max(185, controls.ClientSize.Width - 8), 105)
                    : new Size(188, Math.Max(150, controls.ClientSize.Height - 8));
                tunerBox.Location = compact4
                    ? new Point(4, Math.Max(4, controls.ClientSize.Height - tunerBox.Height - 4))
                    : new Point(Math.Max(260, controls.ClientSize.Width - tunerBox.Width - 4), 4);
                tunerBox.Anchor = compact4 ? (AnchorStyles.Left | AnchorStyles.Bottom) :
                    (AnchorStyles.Right | AnchorStyles.Bottom);

                LayoutTunerBox(tunerBox, tuner, compact4);
                LayoutReceiverStatusAndButtons(controls, tunerBox, tuner, compact4);
                tunerBox.BringToFront();
            }
            }
            finally { applyingLayout = false; }
        }

        private static void LayoutTunerBox(Panel box, int tuner, bool compact4)
        {
            // Compact, label-free vertical tuner stack. Keep the four
            // existing interactive inputs and their change handlers.
            foreach (Control child in box.Controls)
            {
                Label caption = child as Label;
                if (caption != null) caption.Visible = false;
            }

            Panel freq = FindByName<Panel>(box, "ModernFrequencyBorder" + tuner);
            ComboBox sr = FindByName<ComboBox>(box, "ModernSrCombo" + tuner);
            ComboBox rf = FindByName<ComboBox>(box, "ModernRfInput" + tuner);
            Panel lo = FindByName<Panel>(box, "ModernLoBorder" + tuner);
            CheckBox loEnabled = FindByName<CheckBox>(box, "ModernLoEnabled" + tuner);
            Button tune = FindButtonExact(box, "TUNE");

            int left = compact4 ? 5 : 9;
            int full = box.ClientSize.Width - left * 2;
            if (compact4)
            {
                // Two rows, two columns and one full-width TUNE button.
                // All five original controls retain their event handlers.
                int gap = 5;
                int half = (full - gap) / 2;
                if (freq != null) { freq.Bounds = new Rectangle(left, 3, half, 24); StyleEntry(freq, 8.5f); }
                if (rf != null) { rf.Bounds = new Rectangle(left + half + gap, 3, half, 24); rf.Font = new Font("Segoe UI Semibold", 8f); }
                if (sr != null) { sr.Bounds = new Rectangle(left, 31, half, 24); sr.Font = new Font("Segoe UI Semibold", 8f); }
                if (lo != null) { lo.Bounds = new Rectangle(left + half + gap, 31, Math.Max(40, half - 34), 24); StyleEntry(lo, 8f); }
                if (loEnabled != null) { loEnabled.Text = "LO"; loEnabled.Bounds = new Rectangle(left + full - 33, 32, 33, 22); }
                if (tune != null) { tune.Bounds = new Rectangle(left, 62, full, 30); tune.Font = new Font("Segoe UI Semibold", 8.5f); }
                return;
            }
            if (freq != null) { freq.Bounds = new Rectangle(left, 8, full, 27); StyleEntry(freq, 10f); }
            if (sr != null) { sr.Bounds = new Rectangle(left, 39, full, 27); sr.Font = new Font("Segoe UI Semibold", 9.5f); }
            if (rf != null) { rf.Bounds = new Rectangle(left, 70, full, 27); rf.Font = new Font("Segoe UI Semibold", 9.5f); }
            if (lo != null) { lo.Bounds = new Rectangle(left, 101, full - 50, 27); StyleEntry(lo, 9.5f); }
            if (loEnabled != null) { loEnabled.Text = "LO"; loEnabled.Bounds = new Rectangle(box.ClientSize.Width - 47, 103, 43, 22); }
            if (tune != null)
            {
                tune.Bounds = new Rectangle(left, Math.Max(119, box.ClientSize.Height - 35), full, 30);
                tune.Font = new Font("Segoe UI Semibold", 9f);
            }
        }

        private static void LayoutReceiverStatusAndButtons(Panel controls, Panel tunerBox, int tuner, bool compact4)
        {
            Panel header = FindByName<Panel>(controls.Parent, "ModernReceiverHeader" + tuner);
            if (header != null)
            {
                // Preserve the controls' existing event handlers and state:
                // only their visual parent and bounds change.
                // On subsequent layouts the controls already live inside
                // the header. Search there first so window resize keeps all
                // controls aligned without moving them back to the old strip.
                // Search the entire card by stable control identity: after
                // switching layouts some controls have already been moved
                // into the header while others remain in the sidebar.
                Control root = controls.Parent;
                Label volIcon = FindByName<Label>(root, "ModernReceiverSpeaker" + tuner);
                TrackBar volume = FindByName<TrackBar>(root, "ModernReceiverVolume" + tuner);
                Button mute = FindByName<Button>(root, "ModernReceiverMute" + tuner);
                Button record = FindByName<Button>(root, "ModernReceiverRecord" + tuner);
                Button snapshot = FindByName<Button>(root, "ModernReceiverSnapshot" + tuner);
                Button fullscreen = FindByName<Button>(root, "ModernReceiverFullscreen" + tuner);
                Label locked = FindByName<Label>(root, "ModernReceiverLock" + tuner);
                Label service = FindByName<Label>(root, "ModernReceiverService" + tuner);
                foreach (Control item in new Control[] {
                    volIcon, volume, mute, record, snapshot, fullscreen, locked, service })
                {
                    if (item != null && item.Parent != header)
                        header.Controls.Add(item);
                }

                // RX title occupies the first 55px. Reserve at least 65px
                // for service status; the volume slider flexes with width.
                if (compact4)
                {
                    foreach (Control item in header.Controls)
                    {
                        Button btn = item as Button;
                        if (btn != null) btn.Font = new Font("Segoe UI Semibold", 7f);
                        Label lbl = item as Label;
                        if (lbl != null && lbl.Name == "ModernReceiverHeaderTitle" + tuner)
                        {
                            lbl.Font = new Font("Segoe UI Semibold", 9f);
                            lbl.Size = new Size(44, 23);
                        }
                    }
                }
                int x = compact4 ? 48 : 58;
                if (volIcon != null) volIcon.Bounds = new Rectangle(x, 8, compact4 ? 15 : 20, 24);
                x += compact4 ? 16 : 22;
                int volumeWidth = compact4 ? 55 : Math.Max(65, Math.Min(115, header.ClientSize.Width - 620));
                if (volume != null) volume.Bounds = new Rectangle(x, 4, volumeWidth, 30);
                x += volumeWidth + (compact4 ? 2 : 5);
                if (mute != null) { mute.Bounds = new Rectangle(x, 5, compact4 ? 48 : 64, 27); x += compact4 ? 50 : 68; }
                if (record != null) { record.Bounds = new Rectangle(x, 5, compact4 ? 72 : 89, 27); x += compact4 ? 74 : 93; }
                if (snapshot != null) { snapshot.Bounds = new Rectangle(x, 5, compact4 ? 78 : 99, 27); x += compact4 ? 80 : 103; }
                if (fullscreen != null) { fullscreen.Bounds = new Rectangle(x, 5, compact4 ? 29 : 34, 27); x += compact4 ? 31 : 39; }
                if (locked != null)
                {
                    locked.Bounds = new Rectangle(x, 5, compact4 ? 65 : 86, 27);
                    x += compact4 ? 67 : 90;
                }
                if (service != null)
                {
                    service.Bounds = new Rectangle(x, 5,
                        Math.Max(38, header.ClientSize.Width - x - 6), 27);
                    service.TextAlign = ContentAlignment.MiddleLeft;
                }
            }

            LayoutInfoTabs(controls, tunerBox, tuner, compact4);
        }

        // The left-hand bottom section is selectable. Reparent existing live
        // telemetry controls, rather than creating duplicates or changing any
        // receiver event handlers.
        private static void LayoutInfoTabs(Panel controls, Panel tunerBox, int tuner, bool compact4)
        {
            Panel tabs = infoTabs[tuner];
            if (tabs == null || tabs.IsDisposed)
            {
                Label freq = FindLargestFrequencyLabel(controls);
                Label mer = FindLabelStarts(controls, "MER");
                Label margin = FindLabelStarts(controls, "Margin");
                if (freq == null || mer == null || margin == null) return;

                currentFreq[tuner] = freq;
                currentMer[tuner] = mer;
                currentMargin[tuner] = margin;
                foreach (Control child in controls.Controls)
                {
                    Panel bar = child as Panel;
                    if (bar == null || bar == tunerBox || bar.Height != 11) continue;
                    if (IsMerBar(bar)) currentMerBar[tuner] = bar;
                    else if (IsMarginBar(bar)) currentMarginBar[tuner] = bar;
                }

                tabs = new Panel
                {
                    Name = "ModernReceiverInfoTabs" + tuner,
                    BackColor = Color.FromArgb(21, 42, 62),
                    BorderStyle = BorderStyle.FixedSingle
                };
                string[] names = { "Current", "TS Info", "Tuner Data" };
                for (int page = 0; page < names.Length; page++)
                {
                    int index = page;
                    Button button = new Button
                    {
                        Name = "ModernReceiverTabButton" + tuner + "_" + page,
                        Text = names[page],
                        FlatStyle = FlatStyle.Flat,
                        TabStop = false,
                        BackColor = page == 0 ? Color.FromArgb(34, 68, 98) :
                            Color.FromArgb(17, 35, 52),
                        ForeColor = Text,
                        Font = new Font("Segoe UI Semibold", 8f),
                        UseVisualStyleBackColor = false
                    };
                    button.FlatAppearance.BorderSize = 0;
                    button.Click += delegate { SelectInfoPage(tuner, index); };
                    Panel pagePanel = new Panel
                    {
                        Name = "ModernReceiverInfoPage" + page + "_" + tuner,
                        BackColor = Color.FromArgb(13, 28, 45),
                        Visible = page == 0
                    };
                    tabButtons[tuner, page] = button;
                    infoPages[tuner, page] = pagePanel;
                    tabs.Controls.Add(pagePanel);
                    tabs.Controls.Add(button);
                    button.BringToFront();
                }
                Panel current = infoPages[tuner, 0];
                TsBitrateGraph graph = new TsBitrateGraph
                {
                    Name = "ModernTsBitrateGraph" + tuner,
                    BackColor = Color.FromArgb(7, 19, 31)
                };
                tsGraphs[tuner] = graph;
                infoPages[tuner, 1].Controls.Add(graph);
                current.Controls.Add(freq);
                current.Controls.Add(mer);
                current.Controls.Add(margin);
                if (currentMerBar[tuner] != null) current.Controls.Add(currentMerBar[tuner]);
                if (currentMarginBar[tuner] != null) current.Controls.Add(currentMarginBar[tuner]);

                // Four narrow columns use the available horizontal space
                // rather than reserving height for long stacks of metrics.
                for (int page = 1; page <= 2; page++)
                {
                    for (int col = 0; col < 4; col++)
                    {
                        Label label = new Label
                        {
                            AutoSize = false,
                            Font = new Font("Segoe UI", 8f),
                            ForeColor = Text,
                            BackColor = Color.Transparent,
                            Text = "—",
                            TextAlign = ContentAlignment.TopLeft
                        };
                        dataLabels[tuner, (page - 1) * 4 + col] = label;
                        infoPages[tuner, page].Controls.Add(label);
                    }
                }

                // The original live details string continues updating, but
                // belongs with tuner diagnostics rather than overlapping the
                // new information box.
                Label detail = FindDetailsLabel(controls);
                if (detail != null)
                {
                    infoPages[tuner, 2].Controls.Add(detail);
                    detail.Font = new Font("Segoe UI", 8f);
                    detail.ForeColor = Muted;
                    detail.Anchor = AnchorStyles.Left | AnchorStyles.Top;
                }

                controls.Controls.Add(tabs);
                tabs.BringToFront();
                infoTabs[tuner] = tabs;
                SelectInfoPage(tuner, 0);
            }

            // Fill the space between the left edge and Tuner Control,
            // retaining a consistent 9px visual gap instead of capping the
            // information panel at 438px.
            int infoLeft = 4;
            int gapToTuner = 9;
            int available = Math.Max(200, tunerBox.Left - gapToTuner - infoLeft);
            tabs.Bounds = new Rectangle(infoLeft, 4, available,
                Math.Max(72, controls.ClientSize.Height - 8));
            int tabWidth = Math.Max(55, (tabs.ClientSize.Width - 2) / 3);
            for (int page = 0; page < 3; page++)
            {
                tabButtons[tuner, page].Bounds = new Rectangle(page * tabWidth, 0,
                    page == 2 ? tabs.ClientSize.Width - page * tabWidth : tabWidth, compact4 ? 19 : 23);
                infoPages[tuner, page].Bounds = new Rectangle(0, compact4 ? 19 : 23,
                    tabs.ClientSize.Width, Math.Max(30, tabs.ClientSize.Height - (compact4 ? 19 : 23)));
                infoPages[tuner, page].SendToBack();
            }
            Panel pageCurrent = infoPages[tuner, 0];
            if (currentFreq[tuner] != null)
            {
                currentFreq[tuner].Location = new Point(5, 1);
                currentFreq[tuner].Size = new Size(compact4 ? 200 : 270, compact4 ? 23 : 37);
                currentFreq[tuner].Font = new Font("Segoe UI Semibold", compact4 ? 11f : 17f);
            }
            // Restore the original vertical metric order under the live
            // frequency, while leaving the right side for Constellation.
            if (currentMer[tuner] != null)
            {
                currentMer[tuner].AutoSize = false;
                currentMer[tuner].Bounds = new Rectangle(8, compact4 ? 27 : 51, compact4 ? 90 : 130, compact4 ? 18 : 26);
                currentMer[tuner].Font = new Font("Segoe UI Semibold", compact4 ? 8f : 11f);
            }
            if (currentMerBar[tuner] != null)
                currentMerBar[tuner].Bounds = compact4 ? new Rectangle(97, 30, Math.Max(45, pageCurrent.ClientSize.Width - 215), 11) : new Rectangle(147, 55, 180, 19);
            if (currentMargin[tuner] != null)
            {
                currentMargin[tuner].AutoSize = false;
                currentMargin[tuner].Bounds = new Rectangle(8, compact4 ? 49 : 103, compact4 ? 90 : 133, compact4 ? 18 : 26);
                currentMargin[tuner].Font = new Font("Segoe UI Semibold", compact4 ? 8f : 11f);
            }
            if (currentMarginBar[tuner] != null)
                currentMarginBar[tuner].Bounds = compact4 ? new Rectangle(97, 52, Math.Max(45, pageCurrent.ClientSize.Width - 215), 11) : new Rectangle(147, 107, 180, 19);
            // TS Info: four two-line columns on top, real bitrate trace below.
            // Tuner Data: four compact columns, at most four measurements each.
            for (int page = 1; page <= 2; page++)
                for (int col = 0; col < 4; col++)
                {
                    Label label = dataLabels[tuner, (page - 1) * 4 + col];
                    if (label == null) continue;
                    Panel pagePanel = infoPages[tuner, page];
                    // In 4-RX mode the TS tab stays at four columns, but
                    // the longer tuner diagnostics need two columns to avoid
                    // severe truncation on a half-width receiver panel.
                    bool twoColumnTuner = compact4 && page == 2;
                    label.Visible = !twoColumnTuner || col == 0 || col == 2;
                    int columns = twoColumnTuner ? 2 : 4;
                    int position = twoColumnTuner ? col / 2 : col;
                    int cellWidth = Math.Max(70, (pagePanel.ClientSize.Width - 12) / columns);
                    int left = 6 + position * cellWidth;
                    label.Font = new Font("Segoe UI", twoColumnTuner ? 7.3f : (compact4 ? 7.1f : 8f));
                    label.Bounds = page == 1
                        ? new Rectangle(left, 3, cellWidth - 5, compact4 ? 31 : 43)
                        : new Rectangle(left, 4, cellWidth - 5, Math.Max(38, pagePanel.ClientSize.Height - 6));
                }
            if (tsGraphs[tuner] != null && !tsGraphs[tuner].IsDisposed)
            {
                Panel tsPage = infoPages[tuner, 1];
                int graphTop = compact4 ? 36 : 48;
                tsGraphs[tuner].Bounds = new Rectangle(6, graphTop,
                    Math.Max(110, tsPage.ClientSize.Width - 12),
                    Math.Max(25, tsPage.ClientSize.Height - graphTop - 5));
            }
            Label details = FindDetailsLabel(infoPages[tuner, 2]);
            if (details != null)
            {
                // Tuner Data's two full-height columns now include everything
                // previously compressed into the small detail footer.
                details.Visible = false;
            }
        }

        private static void SelectInfoPage(int tuner, int selected)
        {
            for (int page = 0; page < 3; page++)
            {
                Panel panel = infoPages[tuner, page];
                Button button = tabButtons[tuner, page];
                if (panel == null || button == null) continue;
                panel.Visible = page == selected;
                button.BackColor = page == selected ? Color.FromArgb(34, 68, 98) :
                    Color.FromArgb(17, 35, 52);
                button.ForeColor = page == selected ? Text : Muted;
                if (page == selected) panel.BringToFront();
                button.BringToFront();
            }
        }

        private static string ValueOrDash(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "—" : value.Trim();
        }

        private static void HookInfoTelemetry(ModernConceptForm form)
        {
            FieldInfo field = typeof(ModernConceptForm).GetField("backend",
                BindingFlags.Instance | BindingFlags.NonPublic);
            MainForm backend = field == null ? null : field.GetValue(form) as MainForm;
            if (backend == null) return;
            backend.BackendSourceData += delegate(int tuner, OTSourceData data, string description)
            {
                if (tuner < 0 || tuner >= 4 || data == null ||
                    form.IsDisposed || !form.IsHandleCreated) return;
                try
                {
                    form.BeginInvoke((MethodInvoker)delegate
                    {
                        if (form.IsDisposed || dataLabels[tuner, 0] == null ||
                            dataLabels[tuner, 0].IsDisposed) return;
                        if (tsGraphs[tuner] != null && !tsGraphs[tuner].IsDisposed &&
                            (DateTime.UtcNow - lastTsSample[tuner]).TotalMilliseconds >= 900)
                        {
                            tsGraphs[tuner].PushSample(data.ts_metrics_available ? data.ts_bitrate_mbps : 0);
                            lastTsSample[tuner] = DateTime.UtcNow;
                        }
                        string mbps = data.ts_metrics_available && data.ts_bitrate_mbps > 0 ?
                            data.ts_bitrate_mbps.ToString("0.00", CultureInfo.InvariantCulture) + " Mbps" : "—";
                        string nulls = data.ts_metrics_available ?
                            data.null_packets_percent.ToString("0.0", CultureInfo.InvariantCulture) + "%" : "—";
                        dataLabels[tuner, 0].Text = "TS bitrate: " + mbps +
                            "\nNull: " + nulls;
                        dataLabels[tuner, 1].Text = "TS: " + (data.streaming ? "Receiving" : "No stream") +
                            "\nRecord: " + (data.recording ? "Yes" : "No");
                        dataLabels[tuner, 2].Text = "Video: " + ValueOrDash(data.video_codec) +
                            "\nSize: " + ValueOrDash(data.video_resolution);
                        dataLabels[tuner, 3].Text = "Audio: " + ValueOrDash(data.audio_codec) +
                            "\nRate: " + ValueOrDash(data.audio_rate);
                        string f = data.frequency > 0 ?
                            (data.frequency / 1000.0).ToString("0.000", CultureInfo.InvariantCulture) + " MHz" : "—";
                        dataLabels[tuner, 4].Text =
                            "Demod: " + ValueOrDash(data.demode_state) +
                            "\nSystem: " + ValueOrDash(data.delivery_system) +
                            "\nModcod: " + ValueOrDash(data.modcode) +
                            "\nRF input: " + ValueOrDash(data.rf_input);
                        dataLabels[tuner, 5].Text =
                            "MER: " + (data.demod_locked ? data.mer.ToString("0.0", CultureInfo.InvariantCulture) + " dB" : "—") +
                            "\nMargin: " + (data.demod_locked ? data.db_margin.ToString("0.0", CultureInfo.InvariantCulture) + " dB" : "—") +
                            "\nRF level: " + (double.IsNaN(data.rf_input_level) ? "—" :
                                data.rf_input_level.ToString("0", CultureInfo.InvariantCulture) + " dB") +
                            "\nBER: " + (data.tuner_metrics_available ? data.ber.ToString() : "—");
                        dataLabels[tuner, 6].Text =
                            "Freq: " + f +
                            "\nNIM: " + (data.nim_frequency > 0 ? data.nim_frequency + " kHz" : "—") +
                            "\nLO: " + (data.frequency_offset > 0 ? (data.frequency_offset / 1000.0).ToString("0.###", CultureInfo.InvariantCulture) + " MHz" : "Off") +
                            "\nSR: " + (data.symbol_rate > 0 ? data.symbol_rate + " kS" : "—");
                        dataLabels[tuner, 7].Text =
                            "Carrier: " + (data.tuner_metrics_available ? data.carrier_offset + " Hz" : "—") +
                            "\nService: " + ValueOrDash(data.service_name) +
                            "\nProvider: " + ValueOrDash(data.service_provider) +
                            "\nLDPC: " + (data.tuner_metrics_available ? data.ldpc_errors.ToString() : "—");
                        // Preserve all readings while stacking them into
                        // two readable columns in the 4-receiver layout.
                        bool compactTuner = form.ModernReceiverCount >= 4;
                        if (compactTuner)
                        {
                            dataLabels[tuner, 4].Text += "\n" + dataLabels[tuner, 5].Text;
                            dataLabels[tuner, 6].Text += "\n" + dataLabels[tuner, 7].Text;
                        }
                    });
                }
                catch (InvalidOperationException) { }
            };
        }

        private static bool IsMerBar(Panel p)
        {
            foreach (Control c in p.Controls)
            {
                Panel fill = c as Panel;
                if (fill != null && fill.BackColor.R > fill.BackColor.G && fill.BackColor.B > fill.BackColor.G)
                    return true;
                if (fill != null && fill.BackColor.B > 180 && fill.BackColor.G > 90)
                    return true;
            }
            return p.Width == 124;
        }

        private static bool IsMarginBar(Panel p)
        {
            foreach (Control c in p.Controls)
            {
                Panel fill = c as Panel;
                if (fill != null && fill.BackColor.G > fill.BackColor.R && fill.BackColor.G > fill.BackColor.B)
                    return true;
            }
            return p.Width == 134;
        }

        private static void StyleEntry(Panel border, float fontSize)
        {
            foreach (Control c in border.Controls)
            {
                TextBox t = c as TextBox;
                if (t == null) continue;
                t.AutoSize = false;
                t.Location = new Point(5, 3);
                t.Size = new Size(Math.Max(20, border.Width - 10), border.Height - 6);
                t.Font = new Font("Segoe UI Semibold", fontSize);
                t.TextAlign = HorizontalAlignment.Center;
                t.ForeColor = Text;
            }
        }

        private static void MoveLabel(Control root, string text, int x, int y, int w, int h, float size, Color colour)
        {
            Label l = FindLabelExact(root, text);
            if (l == null) return;
            l.Location = new Point(x, y);
            l.Size = new Size(w, h);
            l.Font = new Font("Segoe UI Semibold", size);
            l.ForeColor = colour;
            l.TextAlign = ContentAlignment.MiddleLeft;
        }

        private static Label FindLargestFrequencyLabel(Control root)
        {
            Label best = null;
            foreach (Control c in root.Controls)
            {
                Label l = c as Label;
                if (l != null && l.Font != null && l.Font.Size >= 18f)
                {
                    if (best == null || l.Font.Size > best.Font.Size) best = l;
                }
            }
            return best;
        }

        private static Label FindServiceLabel(Control root, Label locked)
        {
            foreach (Control c in root.Controls)
            {
                Label l = c as Label;
                if (l == null || l == locked) continue;
                string s = (l.Text ?? "").Trim();
                if (s.Length > 0 && !s.StartsWith("MER", StringComparison.OrdinalIgnoreCase) &&
                    !s.StartsWith("Margin", StringComparison.OrdinalIgnoreCase) &&
                    s.IndexOf("receiver details", StringComparison.OrdinalIgnoreCase) < 0 &&
                    s.IndexOf("MODCOD", StringComparison.OrdinalIgnoreCase) < 0 &&
                    l.Font != null && l.Font.Size >= 10f && l.Font.Size < 18f)
                    return l;
            }
            return null;
        }

        private static Label FindDetailsLabel(Control root)
        {
            foreach (Control c in root.Controls)
            {
                Label l = c as Label;
                if (l == null) continue;
                string s = l.Text ?? "";
                if (s.IndexOf("receiver details", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    s.IndexOf("MODCOD", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    s.IndexOf("kS", StringComparison.OrdinalIgnoreCase) >= 0)
                    return l;
            }
            return null;
        }

        private static Label FindLabelStarts(Control root, string text)
        {
            foreach (Control c in root.Controls)
            {
                Label l = c as Label;
                if (l != null && (l.Text ?? "").StartsWith(text, StringComparison.OrdinalIgnoreCase)) return l;
            }
            return null;
        }

        private static Label FindLabelContains(Control root, string text)
        {
            foreach (Control c in root.Controls)
            {
                Label l = c as Label;
                if (l != null && (l.Text ?? "").IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0) return l;
            }
            return null;
        }

        private static Label FindLabelExact(Control root, string text)
        {
            foreach (Control c in root.Controls)
            {
                Label l = c as Label;
                if (l != null && string.Equals((l.Text ?? "").Trim(), text, StringComparison.Ordinal)) return l;
                if (c.HasChildren)
                {
                    Label nested = FindLabelExact(c, text);
                    if (nested != null) return nested;
                }
            }
            return null;
        }

        private static Button FindButtonContains(Control root, string text)
        {
            foreach (Control c in root.Controls)
            {
                Button b = c as Button;
                if (b != null && (b.Text ?? "").IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0) return b;
                if (c.HasChildren)
                {
                    Button nested = FindButtonContains(c, text);
                    if (nested != null) return nested;
                }
            }
            return null;
        }

        private static Button FindButtonExact(Control root, string text)
        {
            foreach (Control c in root.Controls)
            {
                Button b = c as Button;
                if (b != null && string.Equals((b.Text ?? "").Trim(), text, StringComparison.OrdinalIgnoreCase)) return b;
                if (c.HasChildren)
                {
                    Button nested = FindButtonExact(c, text);
                    if (nested != null) return nested;
                }
            }
            return null;
        }

        private static T FindByName<T>(Control root, string name) where T : Control
        {
            if (root == null) return null;
            Control[] found = root.Controls.Find(name, true);
            if (found.Length == 0) return null;
            return found[0] as T;
        }

        private static T FindFirst<T>(Control root) where T : Control
        {
            foreach (Control c in root.Controls)
            {
                T match = c as T;
                if (match != null) return match;
                if (c.HasChildren)
                {
                    T nested = FindFirst<T>(c);
                    if (nested != null) return nested;
                }
            }
            return null;
        }
    }
    // A lightweight, locally drawn bitrate history using actual receiver
    // telemetry. The backend does not expose per-PID TS composition here,
    // so the graph must never synthesize video/audio/overhead percentages.
    internal sealed class TsBitrateGraph : Control
    {
        private readonly Queue<float> history = new Queue<float>();
        private const int MaxSamples = 120;

        public TsBitrateGraph()
        {
            DoubleBuffered = true;
            Font = new Font("Segoe UI", 7f);
        }

        public void PushSample(double mbps)
        {
            float value = (float)Math.Max(0.0, Math.Min(100.0, mbps));
            history.Enqueue(value);
            while (history.Count > MaxSamples) history.Dequeue();
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.Clear(BackColor);
            Rectangle area = new Rectangle(7, 14, Math.Max(10, Width - 15),
                Math.Max(12, Height - 19));
            using (Pen grid = new Pen(Color.FromArgb(35, 85, 100)))
            using (Pen trace = new Pen(Color.FromArgb(235, 216, 45), 1.3f))
            using (Brush text = new SolidBrush(Color.FromArgb(190, 216, 230)))
            {
                g.DrawString("TS BITRATE · LAST 120 S", Font, text, 7, 1);
                for (int i = 1; i < 4; i++)
                    g.DrawLine(grid, area.Left, area.Top + area.Height * i / 4,
                        area.Right, area.Top + area.Height * i / 4);
                for (int i = 1; i < 8; i++)
                    g.DrawLine(grid, area.Left + area.Width * i / 8, area.Top,
                        area.Left + area.Width * i / 8, area.Bottom);
                float[] values = history.ToArray();
                if (values.Length < 2) return;
                float max = Math.Max(1.0f, values.Max() * 1.15f);
                PointF[] pts = new PointF[values.Length];
                for (int i = 0; i < values.Length; i++)
                    pts[i] = new PointF(area.Right -
                        (values.Length - 1 - i) * area.Width / (float)(MaxSamples - 1),
                        area.Bottom - values[i] / max * area.Height);
                g.DrawLines(trace, pts);
            }
        }
    }

}
