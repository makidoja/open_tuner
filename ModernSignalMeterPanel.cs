using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Windows.Forms;

namespace opentuner
{
    // "Signal" info tab for USB MiniTiouner / PicoTuner: lock LEDs, bar meters
    // and the constellation, for peaking up weak signals. Values come from the
    // NIM thread's fast meter reads (ModernTunerDiagnostics), which only run
    // while one of these panels is on screen.
    internal sealed class ModernSignalMeterPanel : Control
    {
        private static readonly Font LabelFont = new Font("Segoe UI", 8f);
        private static readonly Font ValueFont = new Font("Segoe UI Semibold", 8f);
        private static readonly Font LedFont = new Font("Segoe UI Semibold", 8f);
        private static readonly Font SmallFont = new Font("Segoe UI", 6.5f);
        private static readonly Color TextColour = Color.FromArgb(242, 247, 252);
        private static readonly Color MutedColour = Color.FromArgb(142, 165, 190);
        private static readonly Color GridColour = Color.FromArgb(40, 70, 95);

        private const int CarrierMeter = 0, TimingMeter = 1, RfMeter = 2, MerMeter = 3;
        private static readonly string[] DialNames = { "Carrier Lock", "SR Lock", "RF Power", "C/N MER" };
        private static readonly string[,] ScaleEnds = { { "0", "100" }, { "0", "100" }, { "-100", "-30" }, { "-3", "20" } };

        private const double RfMin = -100, RfMax = -30;
        private const double MerMin = -3, MerMax = 20;
        private const double TimingDefaultScale = 2048;

        private readonly int tuner;
        private readonly Timer timer;
        private readonly ModernConstellationControl constellation;
        private readonly double[] fraction = new double[4];
        private readonly double[] peak = new double[4];
        private readonly DateTime[] peakTime = new DateTime[4];
        private readonly string[] valueText = { "—", "—", "—", "—" };
        private bool carrierLed, timingLed, fullLed, hasData, lastFullLock;
        private long lastSequence = -1;
        private byte[,] lastConstellation;
        private double timingScale;

        public ModernSignalMeterPanel(int tuner)
        {
            this.tuner = tuner;
            DoubleBuffered = true;
            SetStyle(ControlStyles.ResizeRedraw | ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
            BackColor = Color.FromArgb(13, 28, 45);

            constellation = new ModernConstellationControl
            {
                BackColor = BackColor,
                Visible = false
            };
            Controls.Add(constellation);

            // 40 ms UI refresh: the NIM thread produces new readings about every
            // 40-60 ms while the panel is visible, so each one is drawn promptly.
            timer = new Timer { Interval = 40 };
            timer.Tick += delegate { Poll(); };
            timer.Start();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) timer.Dispose();
            base.Dispose(disposing);
        }

        private void Poll()
        {
            // Control.Visible is false when any parent is hidden, so this is
            // only true while this receiver's Signal tab is actually showing.
            if (IsDisposed || !IsHandleCreated || !Visible) return;
            ModernTunerDiagnostics.RequestFastMeters();

            ModernTunerDiagnosticSnapshot s = tuner < 2 ? ModernTunerDiagnostics.GetSnapshot(tuner) : null;
            bool fresh = s != null && s.MetersValid &&
                (DateTime.UtcNow - s.MeterUpdatedUtc).TotalSeconds < 2.5;
            if (!fresh)
            {
                if (hasData)
                {
                    hasData = false;
                    PlaceConstellation();
                    Invalidate();
                }
                return;
            }
            if (s.MeterSequence == lastSequence && hasData) return;
            lastSequence = s.MeterSequence;
            hasData = true;

            carrierLed = s.CarrierLock;
            timingLed = s.TimingLock;
            fullLed = s.FullLock;

            // Carrier lock detector integrator: signed 8-bit, saturates near +127 when locked.
            SetMeter(CarrierMeter, Clamp(s.CarrierLevel / 127.0),
                Math.Round(Clamp(s.CarrierLevel / 127.0) * 100).ToString(CultureInfo.InvariantCulture) + "%");

            // Timing lock level has no fixed full scale across symbol rates, so
            // the scale follows the level seen while fully locked.
            if (s.FullLock && s.TimingLevel > timingScale) timingScale = s.TimingLevel;
            else if (s.FullLock) timingScale = Math.Max(s.TimingLevel, timingScale * 0.998);
            double scale = timingScale >= 64 ? timingScale : TimingDefaultScale;
            SetMeter(TimingMeter, Clamp(s.TimingLevel / scale),
                Math.Round(Clamp(s.TimingLevel / scale) * 100).ToString(CultureInfo.InvariantCulture) + "%");

            SetMeter(RfMeter, Clamp((s.RfDbm - RfMin) / (RfMax - RfMin)),
                s.RfDbm.ToString(CultureInfo.InvariantCulture) + " dBm");

            bool demodLocked = s.DemodStatus == 2 || s.DemodStatus == 3;
            double mer = s.MerTenths / 10.0;
            if (demodLocked)
                SetMeter(MerMeter, Clamp((mer - MerMin) / (MerMax - MerMin)),
                    mer.ToString("0.0", CultureInfo.InvariantCulture) + " dB");
            else
                SetMeter(MerMeter, 0, "—");

            if (s.FullLock != lastFullLock)
            {
                lastFullLock = s.FullLock;
                Serilog.Log.Information("Signal meters RX{Rx}: full lock {Lock} (LDI {Ldi}, TMG level {Tmg}, RF {Rf} dBm, MER {Mer} dB)",
                    tuner + 1, s.FullLock, s.CarrierLevel, s.TimingLevel, s.RfDbm,
                    mer.ToString("0.0", CultureInfo.InvariantCulture));
            }

            UpdateConstellation(demodLocked ? s.Constellation : null, s.DemodStatus);
            PlaceConstellation();
            Invalidate();
        }

        private void UpdateConstellation(byte[,] points, byte demodStatus)
        {
            if (points == null)
            {
                if (lastConstellation != null) constellation.UpdateData(null, "CONSTELLATION");
                lastConstellation = null;
                return;
            }
            if (SamePoints(points, lastConstellation)) return;
            lastConstellation = points;
            constellation.UpdateData(points, demodStatus == 3 ? "DVB-S" : "DVB-S2");
        }

        private static bool SamePoints(byte[,] a, byte[,] b)
        {
            if (a == null || b == null || a.GetLength(0) != b.GetLength(0) || a.GetLength(1) != b.GetLength(1))
                return false;
            for (int i = 0; i < a.GetLength(0); i++)
                for (int j = 0; j < a.GetLength(1); j++)
                    if (a[i, j] != b[i, j]) return false;
            return true;
        }

        private void SetMeter(int index, double value, string text)
        {
            fraction[index] = value;
            valueText[index] = text;
            DateTime now = DateTime.UtcNow;
            // Peak marker: jumps up instantly, holds 2 s, then falls slowly.
            if (value >= peak[index]) { peak[index] = value; peakTime[index] = now; }
            else if ((now - peakTime[index]).TotalSeconds > 2)
                peak[index] = Math.Max(value, peak[index] - 0.02);
        }

        private static double Clamp(double v)
        {
            if (double.IsNaN(v)) return 0;
            return v < 0 ? 0 : v > 1 ? 1 : v;
        }

        // Constellation on the right when there is room for a usable square.
        private bool ConstellationArea(out Rectangle plot)
        {
            // Box 14 px narrower than tall gives a square plot inside it.
            int height = Math.Min(Height - 6, (int)(Width * 0.36) + 14);
            int width = height - 14;
            plot = new Rectangle(Width - width - 4, 3, Math.Max(0, width), Math.Max(0, height));
            return height >= 70 && Width - width >= 200;
        }

        private void PlaceConstellation()
        {
            Rectangle plot = Rectangle.Empty;
            bool show = hasData && ConstellationArea(out plot);
            if (show && constellation.Bounds != plot) constellation.Bounds = plot;
            if (constellation.Visible != show) constellation.Visible = show;
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            PlaceConstellation();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(BackColor);
            g.SmoothingMode = SmoothingMode.AntiAlias;

            if (!hasData)
            {
                TextRenderer.DrawText(g,
                    "Signal meters are available with a MiniTiouner or PicoTuner connected by USB.",
                    LabelFont, new Rectangle(8, 4, Math.Max(10, Width - 16), Math.Max(10, Height - 8)),
                    MutedColour, TextFormatFlags.WordBreak | TextFormatFlags.Left | TextFormatFlags.Top);
                return;
            }

            int textHeight = TextRenderer.MeasureText("Ag", LabelFont).Height;
            Rectangle plot;
            int right = ConstellationArea(out plot) ? plot.Left - 6 : Width - 6;

            // Four dial meters with the Carrier, SR and Full LEDs in the gaps.
            Rectangle area = new Rectangle(4, 3, Math.Max(20, right - 4), Math.Max(20, Height - 6));
            Rectangle[] slots;
            Point[] leds;
            int ledSize;
            ComputeLayout(area, out slots, out leds, out ledSize);
            for (int i = 0; i < 4; i++)
                DrawDial(g, slots[i], textHeight, i);
            DrawLed(g, leds[0], ledSize, CarrierLedColour(), "Carrier");
            DrawLed(g, leds[1], ledSize, TimingLedColour(), "SR");
            DrawLed(g, leds[2], ledSize, FullLedColour(), "Full");
        }

        // LED colours follow the meters: green locked, amber close, red not locked.
        private Color CarrierLedColour()
        {
            if (carrierLed) return MeterColour(1);
            return fraction[CarrierMeter] >= 0.33 ? MeterColour(0.5) : MeterColour(0);
        }

        private Color TimingLedColour()
        {
            if (timingLed) return MeterColour(1);
            return fraction[TimingMeter] >= 0.33 ? MeterColour(0.5) : MeterColour(0);
        }

        private Color FullLedColour()
        {
            if (fullLed) return MeterColour(1);
            return carrierLed && timingLed ? MeterColour(0.5) : MeterColour(0);
        }

        // Four meter slots (a dial face plus its name underneath) and the three
        // LED centres. Wide panels: one row, LEDs in the three gaps at dial
        // centre height. Narrow, tall panels (4 RX): 2 x 2 grid with the LEDs in
        // a column between the two columns.
        private void ComputeLayout(Rectangle area, out Rectangle[] slots, out Point[] leds, out int ledSize)
        {
            int nameHeight = TextRenderer.MeasureText("Ag", LabelFont).Height;
            ledSize = 2 * Math.Max(10, TextRenderer.MeasureText("Ag", LedFont).Height - 2);
            int gap = Math.Max(ledSize, TextRenderer.MeasureText("Carrier", LedFont).Width) + 10;

            int rowSize = Math.Min(area.Height - nameHeight - 3, (area.Width - 3 * gap) / 4);
            int gridSize = Math.Min((area.Width - gap) / 2, (area.Height - 2 * nameHeight - 8) / 2);
            slots = new Rectangle[4];
            leds = new Point[3];
            if (rowSize >= gridSize)
            {
                int size = Math.Max(20, rowSize);
                int total = 4 * size + 3 * gap;
                int left = area.Left + Math.Max(0, (area.Width - total) / 2);
                for (int i = 0; i < 4; i++)
                    slots[i] = new Rectangle(left + i * (size + gap), area.Top, size, size + nameHeight + 2);
                int ledY = area.Top + size / 2;
                for (int i = 0; i < 3; i++)
                    leds[i] = new Point(slots[i].Right + gap / 2, ledY);
            }
            else
            {
                int size = Math.Max(20, gridSize);
                int total = 2 * size + gap;
                int left = area.Left + Math.Max(0, (area.Width - total) / 2);
                int rowHeight = size + nameHeight + 6;
                for (int i = 0; i < 4; i++)
                    slots[i] = new Rectangle(left + (i % 2) * (size + gap), area.Top + (i / 2) * rowHeight,
                        size, size + nameHeight + 2);
                int ledX = left + size + gap / 2;
                leds[0] = new Point(ledX, area.Top + size / 2);
                leds[1] = new Point(ledX, area.Top + rowHeight - 3);
                leds[2] = new Point(ledX, area.Top + rowHeight + size / 2);
            }
        }

        private void DrawDial(Graphics g, Rectangle cell, int nameHeight, int index)
        {
            int size = cell.Width;
            if (size < 30) return;
            Rectangle face = new Rectangle(cell.Left, cell.Top + 1, size, size);
            float cx = face.Left + size / 2f, cy = face.Top + size / 2f;
            float radius = size / 2f;

            using (SolidBrush faceBrush = new SolidBrush(Color.FromArgb(7, 19, 31)))
                g.FillEllipse(faceBrush, face);
            using (Pen rim = new Pen(GridColour, 1.5f))
                g.DrawEllipse(rim, face);

            // 270 degree scale from bottom-left (135) clockwise to bottom-right (45),
            // with red / amber / green zones.
            float arcRadius = radius - Math.Max(4, size / 12f);
            RectangleF arc = new RectangleF(cx - arcRadius, cy - arcRadius, arcRadius * 2, arcRadius * 2);
            float zoneWidth = Math.Max(2f, size / 22f);
            DrawZone(g, arc, 0, 0.33, Color.FromArgb(220, 60, 50), zoneWidth);
            DrawZone(g, arc, 0.33, 0.66, Color.FromArgb(235, 180, 40), zoneWidth);
            DrawZone(g, arc, 0.66, 1.0, Color.FromArgb(60, 200, 90), zoneWidth);
            using (Pen tick = new Pen(MutedColour, 1f))
            {
                for (int t = 0; t <= 10; t++)
                {
                    double a = Angle(t / 10.0);
                    float inner = arcRadius - zoneWidth - (t % 5 == 0 ? size / 10f : size / 18f);
                    float outer = arcRadius - zoneWidth;
                    g.DrawLine(tick, cx + (float)Math.Cos(a) * inner, cy + (float)Math.Sin(a) * inner,
                        cx + (float)Math.Cos(a) * outer, cy + (float)Math.Sin(a) * outer);
                }
            }

            // Scale end labels, inside the dial near the start and end of the arc.
            Font small = size >= 90 ? LabelFont : SmallFont;
            DrawAt(g, ScaleEnds[index, 0], small, cx, cy, Angle(0), arcRadius * 0.62f, MutedColour);
            DrawAt(g, ScaleEnds[index, 1], small, cx, cy, Angle(1), arcRadius * 0.62f, MutedColour);

            // Peak marker on the scale, then the needle.
            if (peak[index] > 0.005)
            {
                double a = Angle(peak[index]);
                using (Pen mark = new Pen(Color.White, Math.Max(2f, size / 40f)))
                    g.DrawLine(mark, cx + (float)Math.Cos(a) * (arcRadius - zoneWidth * 2.5f),
                        cy + (float)Math.Sin(a) * (arcRadius - zoneWidth * 2.5f),
                        cx + (float)Math.Cos(a) * (arcRadius + 1), cy + (float)Math.Sin(a) * (arcRadius + 1));
            }
            double needle = Angle(fraction[index]);
            using (Pen needlePen = new Pen(Color.FromArgb(235, 50, 40), Math.Max(2f, size / 30f)))
            {
                needlePen.StartCap = LineCap.Round;
                needlePen.EndCap = LineCap.Round;
                g.DrawLine(needlePen, cx, cy, cx + (float)Math.Cos(needle) * (arcRadius - 2),
                    cy + (float)Math.Sin(needle) * (arcRadius - 2));
            }
            float hub = Math.Max(5f, size / 9f);
            using (SolidBrush hubBrush = new SolidBrush(Color.FromArgb(70, 85, 100)))
                g.FillEllipse(hubBrush, cx - hub / 2, cy - hub / 2, hub, hub);

            // Value in the gap at the bottom of the dial; name underneath.
            Rectangle valueRect = new Rectangle(face.Left, (int)(cy + radius * 0.35f), face.Width,
                (int)(radius * 0.6f));
            TextRenderer.DrawText(g, valueText[index], size >= 90 ? ValueFont : SmallFont, valueRect, TextColour,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            Rectangle nameRect = new Rectangle(cell.Left - 8, face.Bottom + 1, cell.Width + 16, nameHeight);
            TextRenderer.DrawText(g, DialNames[index], LabelFont, nameRect, MutedColour,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.Top | TextFormatFlags.NoPadding |
                TextFormatFlags.EndEllipsis);
        }

        private static double Angle(double value)
        {
            return (135 + 270 * Clamp(value)) * Math.PI / 180;
        }

        private static void DrawZone(Graphics g, RectangleF arc, double from, double to, Color colour, float width)
        {
            using (Pen pen = new Pen(Color.FromArgb(170, colour), width))
                g.DrawArc(pen, arc, (float)(135 + 270 * from), (float)(270 * (to - from)));
        }

        private static void DrawAt(Graphics g, string text, Font font, float cx, float cy, double angle,
            float distance, Color colour)
        {
            Size size = TextRenderer.MeasureText(text, font);
            int x = (int)(cx + Math.Cos(angle) * distance - size.Width / 2f);
            int y = (int)(cy + Math.Sin(angle) * distance - size.Height / 2f);
            TextRenderer.DrawText(g, text, font, new Point(x, y), colour, TextFormatFlags.NoPadding);
        }

        // Large LED centred on a point, label underneath.
        private static void DrawLed(Graphics g, Point centre, int d, Color core, string label)
        {
            Rectangle led = new Rectangle(centre.X - d / 2, centre.Y - d / 2, d, d);
            Color dark = Color.FromArgb(core.R / 2, core.G / 2, core.B / 2);
            using (LinearGradientBrush fill = new LinearGradientBrush(led, ControlPaint.Light(core), dark, 60f))
                g.FillEllipse(fill, led);
            using (Pen rim = new Pen(Color.FromArgb(10, 20, 30), 1.5f))
                g.DrawEllipse(rim, led);
            // Small highlight so it reads as a lamp.
            int h = Math.Max(3, d / 4);
            using (SolidBrush shine = new SolidBrush(Color.FromArgb(120, 255, 255, 255)))
                g.FillEllipse(shine, led.Left + d / 4, led.Top + d / 6, h, h);
            Size size = TextRenderer.MeasureText(label, LedFont);
            TextRenderer.DrawText(g, label, LedFont, new Point(centre.X - size.Width / 2, led.Bottom + 2),
                TextColour, TextFormatFlags.NoPadding);
        }

        private static Color MeterColour(double value)
        {
            if (value < 0.33) return Color.FromArgb(220, 60, 50);
            if (value < 0.66) return Color.FromArgb(235, 180, 40);
            return Color.FromArgb(60, 200, 90);
        }
    }
}

