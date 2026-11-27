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
        private static readonly Color TextColour = Color.FromArgb(242, 247, 252);
        private static readonly Color MutedColour = Color.FromArgb(142, 165, 190);
        private static readonly Color TrackColour = Color.FromArgb(20, 40, 58);
        private static readonly Color GridColour = Color.FromArgb(40, 70, 95);

        private const int CarrierMeter = 0, TimingMeter = 1, RfMeter = 2, MerMeter = 3;
        private static readonly string[] MeterNames = { "Carrier lock", "SR lock", "RF power", "C/N MER" };
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
                BackColor = Color.FromArgb(7, 19, 31),
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
            int side = Math.Min(Height - 6, (int)(Width * 0.36));
            plot = new Rectangle(Width - side - 4, 3, Math.Max(0, side), Math.Max(0, side));
            return side >= 70 && Width - side >= 200;
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

            int textHeight = TextRenderer.MeasureText("Ag", LedFont).Height;
            int ledRow = textHeight + 6;
            Rectangle plot;
            int right = ConstellationArea(out plot) ? plot.Left - 6 : Width - 6;

            // LEDs: Carrier, SR, Full.
            int x = 8;
            x = DrawLed(g, x, 3, textHeight, carrierLed, "Carrier");
            x = DrawLed(g, x + 12, 3, textHeight, timingLed, "SR");
            DrawLed(g, x + 12, 3, textHeight, fullLed, "Full");

            // Bar meters.
            int labelWidth = Math.Max(TextRenderer.MeasureText("Carrier lock", LabelFont).Width,
                TextRenderer.MeasureText("C/N MER", LabelFont).Width) + 4;
            int valueWidth = TextRenderer.MeasureText("-100 dBm", ValueFont).Width + 4;
            int top = ledRow + 2;
            int rowHeight = Math.Max(textHeight, (Height - top - 3) / 4);
            int barLeft = 8 + labelWidth;
            int barWidth = Math.Max(20, right - barLeft - valueWidth - 6);
            for (int i = 0; i < 4; i++)
            {
                int rowTop = top + i * rowHeight;
                Rectangle labelRect = new Rectangle(8, rowTop, labelWidth, rowHeight);
                TextRenderer.DrawText(g, MeterNames[i], LabelFont, labelRect, MutedColour,
                    TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPadding);
                int barHeight = Math.Max(6, Math.Min(16, rowHeight - 5));
                Rectangle bar = new Rectangle(barLeft, rowTop + (rowHeight - barHeight) / 2, barWidth, barHeight);
                DrawBar(g, bar, fraction[i], peak[i]);
                Rectangle valueRect = new Rectangle(bar.Right + 6, rowTop, valueWidth, rowHeight);
                TextRenderer.DrawText(g, valueText[i], ValueFont, valueRect, TextColour,
                    TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPadding);
            }
        }

        private static int DrawLed(Graphics g, int x, int y, int textHeight, bool on, string label)
        {
            int d = Math.Max(10, textHeight - 2);
            Rectangle led = new Rectangle(x, y + (textHeight - d) / 2 + 1, d, d);
            Color core = on ? Color.FromArgb(60, 220, 90) : Color.FromArgb(200, 40, 40);
            Color dark = on ? Color.FromArgb(20, 110, 40) : Color.FromArgb(95, 15, 15);
            using (LinearGradientBrush fill = new LinearGradientBrush(led, ControlPaint.Light(core), dark, 60f))
                g.FillEllipse(fill, led);
            using (Pen rim = new Pen(Color.FromArgb(10, 20, 30)))
                g.DrawEllipse(rim, led);
            int textLeft = led.Right + 4;
            Size size = TextRenderer.MeasureText(label, LedFont);
            TextRenderer.DrawText(g, label, LedFont, new Point(textLeft, y + 1), on ? TextColour : MutedColour,
                TextFormatFlags.NoPadding);
            return textLeft + size.Width;
        }

        private static void DrawBar(Graphics g, Rectangle bar, double value, double peakValue)
        {
            using (SolidBrush track = new SolidBrush(TrackColour))
                g.FillRectangle(track, bar);
            int filled = (int)Math.Round(bar.Width * value);
            if (filled > 0)
            {
                Rectangle fill = new Rectangle(bar.Left, bar.Top, filled, bar.Height);
                using (SolidBrush brush = new SolidBrush(MeterColour(value)))
                    g.FillRectangle(brush, fill);
            }
            using (Pen grid = new Pen(GridColour))
            {
                for (int i = 1; i < 10; i++)
                {
                    int gx = bar.Left + bar.Width * i / 10;
                    g.DrawLine(grid, gx, bar.Top, gx, bar.Top + 2);
                    g.DrawLine(grid, gx, bar.Bottom - 3, gx, bar.Bottom - 1);
                }
                g.DrawRectangle(grid, bar.Left, bar.Top, bar.Width - 1, bar.Height - 1);
            }
            if (peakValue > 0.005)
            {
                int px = bar.Left + (int)Math.Round((bar.Width - 2) * peakValue);
                using (SolidBrush mark = new SolidBrush(Color.FromArgb(255, 255, 255)))
                    g.FillRectangle(mark, px, bar.Top, 2, bar.Height);
            }
        }

        private static Color MeterColour(double value)
        {
            if (value < 0.33) return Color.FromArgb(220, 60, 50);
            if (value < 0.66) return Color.FromArgb(235, 180, 40);
            return Color.FromArgb(60, 200, 90);
        }
    }
}
