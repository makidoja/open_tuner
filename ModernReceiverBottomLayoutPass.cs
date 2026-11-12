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
        private static readonly Color Text = Color.FromArgb(242, 247, 252);
        private static readonly Color Muted = Color.FromArgb(142, 165, 190);
        private static readonly Panel[] infoTabs = new Panel[2];
        private static readonly Panel[,] infoPages = new Panel[2, 3];
        private static readonly Button[,] tabButtons = new Button[2, 3];
        private static readonly TsBitrateGraph[] tsGraphs = new TsBitrateGraph[2];
        private static readonly DateTime[] lastTsSample = new DateTime[2];
        private static readonly Label[] currentFreq = new Label[2];
        private static readonly Label[] currentMer = new Label[2];
        private static readonly Label[] currentMargin = new Label[2];
        private static readonly Panel[] currentMerBar = new Panel[2];
        private static readonly Panel[] currentMarginBar = new Panel[2];
        private static readonly Label[,] dataLabels = new Label[2, 4];

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
            if (form == null || form.IsDisposed) return;

            for (int tuner = 0; tuner < 2; tuner++)
            {
                Panel tunerBox = FindByName<Panel>(form, "ModernTunerControlBox" + tuner);
                if (tunerBox == null || tunerBox.Parent == null) continue;

                Panel controls = tunerBox.Parent as Panel;
                if (controls == null) continue;

                tunerBox.Size = new Size(350, 140);
                tunerBox.Location = new Point(
                    Math.Max(445, controls.ClientSize.Width - tunerBox.Width - 4),
                    Math.Max(42, controls.ClientSize.Height - tunerBox.Height - 4));
                tunerBox.Anchor = AnchorStyles.Right | AnchorStyles.Bottom;

                LayoutTunerBox(tunerBox, tuner);
                LayoutReceiverStatusAndButtons(controls, tunerBox, tuner);
                tunerBox.BringToFront();
            }
        }

        private static void LayoutTunerBox(Panel box, int tuner)
        {
            MoveLabel(box, "TUNER CONTROL", 10, 2, 130, 17, 8.5f, Muted);
            MoveLabel(box, "FREQ", 10, 20, 150, 16, 8.5f, Muted);
            MoveLabel(box, "SR", 175, 20, 80, 16, 8.5f, Muted);
            MoveLabel(box, "TUNER", 10, 72, 150, 16, 8.5f, Muted);
            MoveLabel(box, "LO FREQ", 175, 72, 90, 16, 8.5f, Muted);

            Panel freqBorder = FindByName<Panel>(box, "ModernFrequencyBorder" + tuner);
            if (freqBorder != null)
            {
                freqBorder.Location = new Point(10, 37);
                freqBorder.Size = new Size(150, 32);
                StyleEntry(freqBorder, 10.5f);
            }

            ComboBox sr = FindByName<ComboBox>(box, "ModernSrCombo" + tuner);
            if (sr != null)
            {
                sr.Location = new Point(175, 37);
                sr.Size = new Size(80, 32);
                sr.Font = new Font("Segoe UI Semibold", 10f);
            }

            ComboBox rf = FindByName<ComboBox>(box, "ModernRfInput" + tuner);
            if (rf != null)
            {
                rf.Location = new Point(10, 89);
                rf.Size = new Size(150, 32);
                rf.Font = new Font("Segoe UI Semibold", 10f);
            }

            Panel loBorder = FindByName<Panel>(box, "ModernLoBorder" + tuner);
            if (loBorder != null)
            {
                loBorder.Location = new Point(175, 89);
                loBorder.Size = new Size(90, 32);
                StyleEntry(loBorder, 10.5f);
            }

            CheckBox lo = FindByName<CheckBox>(box, "ModernLoEnabled" + tuner);
            if (lo != null)
            {
                lo.Text = "LO";
                lo.Location = new Point(274, 93);
                lo.Size = new Size(50, 24);
                lo.Font = new Font("Segoe UI Semibold", 9f);
            }

            Button tune = FindButtonExact(box, "TUNE");
            if (tune != null)
            {
                tune.Location = new Point(265, 37);
                tune.Size = new Size(75, 32);
                tune.Font = new Font("Segoe UI Semibold", 9f);
            }
        }

        private static void LayoutReceiverStatusAndButtons(Panel controls, Panel tunerBox, int tuner)
        {
            Label volIcon = FindLabelExact(controls, "🔊");
            if (volIcon != null) { volIcon.Location = new Point(4, 4); volIcon.Size = new Size(24, 30); }

            TrackBar volume = FindFirst<TrackBar>(controls);
            if (volume != null)
            {
                volume.Location = new Point(28, 3);
                volume.Size = new Size(124, 32);
            }

            Button mute = FindButtonContains(controls, "MUTE");
            Button record = FindButtonContains(controls, "RECORD");
            if (record == null) record = FindButtonContains(controls, "STOP");
            Button snapshot = FindButtonContains(controls, "SNAPSHOT");
            Button fullscreen = FindButtonExact(controls, "⛶");

            if (mute != null) { mute.Location = new Point(158, 4); mute.Size = new Size(72, 30); }
            if (record != null) { record.Location = new Point(236, 4); record.Size = new Size(88, 30); }
            if (snapshot != null) { snapshot.Location = new Point(330, 4); snapshot.Size = new Size(100, 30); }
            if (fullscreen != null) { fullscreen.Location = new Point(436, 4); fullscreen.Size = new Size(42, 30); }

            // Centre receiver state directly above the tuner control panel.
            Label locked = FindLabelContains(controls, "LOCK");
            Label service = FindServiceLabel(controls, locked);
            int stateWidth = Math.Min(tunerBox.Width, 340);
            int stateLeft = tunerBox.Left + Math.Max(0, (tunerBox.Width - stateWidth) / 2);
            int lockWidth = 88;
            int serviceWidth = Math.Max(120, stateWidth - lockWidth - 8);

            if (locked != null)
            {
                locked.Location = new Point(stateLeft, 4);
                locked.Size = new Size(lockWidth, 30);
                locked.TextAlign = ContentAlignment.MiddleCenter;
            }

            if (service != null)
            {
                service.Location = new Point(stateLeft + lockWidth + 8, 4);
                service.Size = new Size(serviceWidth, 30);
                service.Font = new Font("Segoe UI Semibold", 11.5f);
                service.ForeColor = Color.White;
                service.TextAlign = ContentAlignment.MiddleCenter;
            }

            LayoutInfoTabs(controls, tunerBox, tuner);
        }

        // The left-hand bottom section is selectable. Reparent existing live
        // telemetry controls, rather than creating duplicates or changing any
        // receiver event handlers.
        private static void LayoutInfoTabs(Panel controls, Panel tunerBox, int tuner)
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

                for (int page = 1; page <= 2; page++)
                {
                    for (int col = 0; col < 2; col++)
                    {
                        Label label = new Label
                        {
                            AutoSize = false,
                            Font = new Font("Segoe UI", 8.5f),
                            ForeColor = Text,
                            BackColor = Color.Transparent,
                            Text = "Waiting for receiver data...",
                            TextAlign = ContentAlignment.TopLeft
                        };
                        dataLabels[tuner, (page - 1) * 2 + col] = label;
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

            int available = Math.Max(355, tunerBox.Left - 9);
            tabs.Bounds = new Rectangle(4, 39, Math.Min(438, available),
                Math.Max(120, controls.ClientSize.Height - 43));
            int tabWidth = Math.Max(80, (tabs.ClientSize.Width - 2) / 3);
            for (int page = 0; page < 3; page++)
            {
                tabButtons[tuner, page].Bounds = new Rectangle(page * tabWidth, 0,
                    page == 2 ? tabs.ClientSize.Width - page * tabWidth : tabWidth, 23);
                infoPages[tuner, page].Bounds = new Rectangle(0, 23,
                    tabs.ClientSize.Width, Math.Max(30, tabs.ClientSize.Height - 23));
                infoPages[tuner, page].SendToBack();
            }
            Panel pageCurrent = infoPages[tuner, 0];
            if (currentFreq[tuner] != null)
            {
                currentFreq[tuner].Location = new Point(5, 1);
                currentFreq[tuner].Size = new Size(245, 35);
                currentFreq[tuner].Font = new Font("Segoe UI Semibold", 17f);
            }
            // Restore the original vertical metric order under the live
            // frequency, while leaving the right side for Constellation.
            if (currentMer[tuner] != null)
            {
                currentMer[tuner].AutoSize = false;
                currentMer[tuner].Bounds = new Rectangle(5, 38, 128, 21);
                currentMer[tuner].Font = new Font("Segoe UI Semibold", 9.5f);
            }
            if (currentMerBar[tuner] != null)
                currentMerBar[tuner].Bounds = new Rectangle(142, 43, 113, 11);
            if (currentMargin[tuner] != null)
            {
                currentMargin[tuner].AutoSize = false;
                currentMargin[tuner].Bounds = new Rectangle(5, 68, 133, 21);
                currentMargin[tuner].Font = new Font("Segoe UI Semibold", 9.5f);
            }
            if (currentMarginBar[tuner] != null)
                currentMarginBar[tuner].Bounds = new Rectangle(142, 73, 113, 11);
            for (int page = 1; page <= 2; page++)
                for (int col = 0; col < 2; col++)
                {
                    Label label = dataLabels[tuner, (page - 1) * 2 + col];
                    if (label == null) continue;
                    int left = col == 0 ? 7 : pageCurrent.ClientSize.Width / 2 + 2;
                    if (page == 1)
                    {
                        label.Font = new Font("Segoe UI", 7.5f);
                        label.Bounds = new Rectangle(left, 68,
                            Math.Max(125, pageCurrent.ClientSize.Width / 2 - 8), 45);
                    }
                    else
                    {
                        label.Font = new Font("Segoe UI", 8f);
                        label.Bounds = new Rectangle(left, 7,
                            Math.Max(125, pageCurrent.ClientSize.Width / 2 - 8), 84);
                    }
                }
            if (tsGraphs[tuner] != null && !tsGraphs[tuner].IsDisposed)
                tsGraphs[tuner].Bounds = new Rectangle(6, 3,
                    Math.Max(200, infoPages[tuner, 1].ClientSize.Width - 12), 61);
            Label details = FindDetailsLabel(infoPages[tuner, 2]);
            if (details != null)
            {
                details.Bounds = new Rectangle(7, 88, Math.Max(250, pageCurrent.ClientSize.Width - 14), 18);
                details.AutoSize = false;
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
                if (tuner < 0 || tuner >= 2 || data == null ||
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
                            tsGraphs[tuner].PushSample(data.streaming ? data.ts_bitrate_mbps : 0);
                            lastTsSample[tuner] = DateTime.UtcNow;
                        }
                        string mbps = data.streaming && data.ts_bitrate_mbps > 0 ?
                            data.ts_bitrate_mbps.ToString("0.00", CultureInfo.InvariantCulture) + " Mbps" : "—";
                        string nulls = data.streaming ?
                            data.null_packets_percent.ToString("0.0", CultureInfo.InvariantCulture) + "%" : "—";
                        dataLabels[tuner, 0].Text = "TS bitrate:  " + mbps + "\nNull packets:  " + nulls +
                            "\nStreaming:  " + (data.streaming ? "Yes" : "No");
                        dataLabels[tuner, 1].Text = "Video:  " + ValueOrDash(data.video_codec) +
                            "\nResolution:  " + ValueOrDash(data.video_resolution) +
                            "\nAudio:  " + ValueOrDash(data.audio_codec) +
                            "\nAudio rate:  " + ValueOrDash(data.audio_rate);
                        dataLabels[tuner, 2].Text = "Demod:  " + ValueOrDash(data.demode_state) +
                            "\nSystem:  " + ValueOrDash(data.delivery_system) +
                            "\nModcod:  " + ValueOrDash(data.modcode);
                        dataLabels[tuner, 3].Text = "Symbol rate:  " +
                            (data.symbol_rate > 0 ? data.symbol_rate + " kS" : "—") +
                            "\nService:  " + ValueOrDash(data.service_name) +
                            "\nProvider:  " + ValueOrDash(data.service_provider);
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
