using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using Serilog;

namespace opentuner
{
    internal sealed class ModernBatcSpectrumControl : Control
    {
        public delegate void SignalSelected(int receiver, uint frequencyKHz, uint symbolRateKs);
        public event SignalSelected OnSignalSelected;

        private readonly object sync = new object();
        private readonly object signalLock = new object();
        private readonly socket fftSocket;
        private readonly signal detector;
        private ushort[] fft;
        private List<signal.Sig> signals = new List<signal.Sig>();
        private readonly signal.Sig?[] tuned = new signal.Sig?[2];
        private bool connected;
        private DateTime lastFrame = DateTime.MinValue;
        private readonly Timer watchdog;

        private const double StartMHz = 10490.5;
        private const double SpanMHz = 9.0;

        private static readonly Color Surface = Color.FromArgb(13, 28, 45);
        private static readonly Color Surface2 = Color.FromArgb(18, 38, 60);
        private static readonly Color Border = Color.FromArgb(37, 67, 94);
        private static readonly Color Accent = Color.FromArgb(28, 139, 253);
        private static readonly Color Accent2 = Color.FromArgb(84, 163, 255);
        private static readonly Color Text = Color.FromArgb(242, 247, 252);
        private static readonly Color Muted = Color.FromArgb(142, 165, 190);
        private static readonly Color Green = Color.FromArgb(40, 222, 126);

        public ModernBatcSpectrumControl()
        {
            DoubleBuffered = true;
            ResizeRedraw = true;
            BackColor = Surface;
            ForeColor = Text;
            Cursor = Cursors.Hand;
            TabStop = false;

            detector = new signal(signalLock);
            detector.set_num_rx(2);
            detector.set_num_rx_scan(2);
            detector.set_avoidbeacon(true);
            detector.debug += delegate(string s) { Log.Information(s); };

            fftSocket = new socket();
            fftSocket.callback += OnFft;
            fftSocket.ConnectionStatusChanged += delegate(object sender, bool state)
            {
                connected = state;
                SafeInvalidate();
            };
            fftSocket.start();

            watchdog = new Timer { Interval = 2000 };
            watchdog.Tick += delegate
            {
                if ((DateTime.Now - lastFrame).TotalSeconds > 4)
                {
                    connected = false;
                    try { fftSocket.stop(); fftSocket.start(); } catch { }
                    Invalidate();
                }
            };
            watchdog.Start();
        }

        private void OnFft(ushort[] data)
        {
            if (data == null || data.Length < 8) return;

            ushort[] copy = new ushort[data.Length];
            Array.Copy(data, copy, data.Length);
            List<signal.Sig> detected;
            try
            {
                detected = detector.detect_signals(copy).ToList();
                detector.updateSignalList();
            }
            catch
            {
                detected = new List<signal.Sig>();
            }

            lock (sync)
            {
                fft = copy;
                signals = detected;
                lastFrame = DateTime.Now;
                connected = true;
            }
            SafeInvalidate();
        }

        private void SafeInvalidate()
        {
            try
            {
                if (IsDisposed || !IsHandleCreated) return;
                if (InvokeRequired) BeginInvoke((MethodInvoker)Invalidate);
                else Invalidate();
            }
            catch { }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            g.Clear(Surface);

            Rectangle plot = new Rectangle(12, 8, Math.Max(20, Width - 24), Math.Max(30, Height - 34));
            using (Pen border = new Pen(Border))
                g.DrawRectangle(border, plot);

            ushort[] localFft;
            List<signal.Sig> localSignals;
            lock (sync)
            {
                localFft = fft == null ? null : (ushort[])fft.Clone();
                localSignals = new List<signal.Sig>(signals);
            }

            if (!connected || localFft == null)
            {
                using (Font f = new Font("Segoe UI Semibold", 9f))
                using (Brush b = new SolidBrush(Muted))
                    g.DrawString("BATC wideband spectrum — connecting…", f, b, plot.Left + 10, plot.Top + 10);
                DrawAxis(g, plot);
                return;
            }

            DrawGrid(g, plot);
            DrawSpectrum(g, plot, localFft);
            DrawSignals(g, plot, localSignals);
            DrawTunedMarkers(g, plot);
            DrawAxis(g, plot);
        }

        private void DrawGrid(Graphics g, Rectangle plot)
        {
            using (Pen p = new Pen(Color.FromArgb(55, 142, 165, 190)))
            {
                p.DashStyle = DashStyle.Dot;
                for (int i = 1; i < 4; i++)
                {
                    int y = plot.Top + (plot.Height * i / 4);
                    g.DrawLine(p, plot.Left, y, plot.Right, y);
                }
                for (int i = 1; i < 9; i++)
                {
                    int x = plot.Left + (plot.Width * i / 9);
                    g.DrawLine(p, x, plot.Top, x, plot.Bottom);
                }
            }
        }

        private void DrawSpectrum(Graphics g, Rectangle plot, ushort[] data)
        {
            PointF[] points = new PointF[data.Length + 2];
            points[0] = new PointF(plot.Left, plot.Bottom);
            float max = 65535f;
            for (int i = 0; i < data.Length; i++)
            {
                float x = plot.Left + (i / (float)Math.Max(1, data.Length - 1)) * plot.Width;
                float n = Math.Max(0f, Math.Min(1f, data[i] / max));
                float y = plot.Bottom - (n * plot.Height * 1.55f);
                y = Math.Max(plot.Top, Math.Min(plot.Bottom, y));
                points[i + 1] = new PointF(x, y);
            }
            points[points.Length - 1] = new PointF(plot.Right, plot.Bottom);

            using (LinearGradientBrush fill = new LinearGradientBrush(
                new Point(plot.Left, plot.Top), new Point(plot.Left, plot.Bottom),
                Color.FromArgb(185, Accent2), Color.FromArgb(55, Accent)))
                g.FillPolygon(fill, points);

            using (Pen edge = new Pen(Accent2, 1.4f))
                g.DrawLines(edge, points.Skip(1).Take(points.Length - 2).ToArray());
        }

        private void DrawSignals(Graphics g, Rectangle plot, List<signal.Sig> localSignals)
        {
            using (Font labelFont = new Font("Segoe UI Semibold", 8f))
            using (Brush labelBrush = new SolidBrush(Text))
            using (Brush blockBrush = new SolidBrush(Color.FromArgb(35, Accent2)))
            using (Pen blockPen = new Pen(Color.FromArgb(135, Accent2)))
            {
                foreach (signal.Sig s in localSignals)
                {
                    double rel = (s.frequency - StartMHz) / SpanMHz;
                    if (rel < 0 || rel > 1) continue;
                    float x = plot.Left + (float)(rel * plot.Width);
                    float width = Math.Max(5f, (s.sr / (float)SpanMHz) * plot.Width);
                    RectangleF block = new RectangleF(x - width / 2f, plot.Top + 2, width, plot.Height - 4);
                    g.FillRectangle(blockBrush, block);
                    g.DrawRectangle(blockPen, block.X, block.Y, block.Width, block.Height);

                    string callsign = string.IsNullOrWhiteSpace(s.callsign) ? "" : s.callsign + "  ";
                    string text = callsign + s.frequency.ToString("0.000") + "  " + Math.Round(s.sr * 1000) + "kS";
                    SizeF size = g.MeasureString(text, labelFont);
                    float tx = Math.Max(plot.Left + 3, Math.Min(plot.Right - size.Width - 3, x - size.Width / 2f));
                    g.DrawString(text, labelFont, labelBrush, tx, plot.Top + 5);
                }
            }
        }

        private void DrawTunedMarkers(Graphics g, Rectangle plot)
        {
            for (int rx = 0; rx < 2; rx++)
            {
                signal.Sig? item = tuned[rx];
                if (!item.HasValue) continue;
                signal.Sig s = item.Value;
                float x = plot.Left + (float)(((s.frequency - StartMHz) / SpanMHz) * plot.Width);
                Color c = rx == 0 ? Green : Color.Gold;
                using (Pen p = new Pen(c, 2f))
                {
                    g.DrawLine(p, x, plot.Top, x, plot.Bottom);
                    g.DrawLine(p, x - 4, plot.Top + 4, x, plot.Top);
                    g.DrawLine(p, x + 4, plot.Top + 4, x, plot.Top);
                }
                using (Font f = new Font("Segoe UI Semibold", 7.5f))
                using (Brush b = new SolidBrush(c))
                    g.DrawString(rx == 0 ? "RX1" : "RX2", f, b, x + 4, plot.Bottom - 17);
            }
        }

        private void DrawAxis(Graphics g, Rectangle plot)
        {
            using (Font f = new Font("Segoe UI", 7.5f))
            using (Brush b = new SolidBrush(Muted))
            {
                for (int i = 0; i <= 9; i++)
                {
                    double mhz = StartMHz + i;
                    string text = mhz.ToString("0.0");
                    SizeF s = g.MeasureString(text, f);
                    float x = plot.Left + (plot.Width * i / 9f) - s.Width / 2f;
                    x = Math.Max(0, Math.Min(Width - s.Width, x));
                    g.DrawString(text, f, b, x, plot.Bottom + 4);
                }
            }
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            if (e.Button != MouseButtons.Left) return;

            Rectangle plot = new Rectangle(12, 8, Math.Max(20, Width - 24), Math.Max(30, Height - 34));
            if (!plot.Contains(e.Location)) return;

            List<signal.Sig> local;
            lock (sync) local = new List<signal.Sig>(signals);
            if (local.Count == 0) return;

            double clickedMHz = StartMHz + ((e.X - plot.Left) / (double)Math.Max(1, plot.Width)) * SpanMHz;
            signal.Sig? best = null;
            double bestDelta = double.MaxValue;
            foreach (signal.Sig s in local)
            {
                double half = Math.Max(0.035, s.sr / 2.0);
                double delta = Math.Abs(s.frequency - clickedMHz);
                if (delta <= half && delta < bestDelta)
                {
                    best = s;
                    bestDelta = delta;
                }
            }
            if (!best.HasValue) return;

            int receiver = e.Y < plot.Top + plot.Height / 2 ? 0 : 1;
            signal.Sig selected = best.Value;
            tuned[receiver] = selected;
            detector.set_tuned(selected, receiver);
            Invalidate();

            uint freqKHz = Convert.ToUInt32(Math.Round(selected.frequency * 1000.0));
            uint srKs = Convert.ToUInt32(Math.Round(selected.sr * 1000.0));
            OnSignalSelected?.Invoke(receiver, freqKHz, srKs);
        }

        public void UpdateSignalCallsign(string callsign, double freqMHz, float srMs)
        {
            try { detector.updateCurrentSignal(callsign, freqMHz, srMs); } catch { }
        }

        public void Close()
        {
            try { watchdog.Stop(); watchdog.Dispose(); } catch { }
            try { fftSocket.stop(); } catch { }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) Close();
            base.Dispose(disposing);
        }
    }
}
