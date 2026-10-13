using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace opentuner
{
    internal sealed class ModernConstellationControl : Control
    {
        private readonly System.Collections.Generic.Queue<PointF> history = new System.Collections.Generic.Queue<PointF>();
        private const int MaxHistory = 512;
        private bool hasPoints;
        private string modeText = "CONSTELLATION";

        public bool HasPoints { get { return hasPoints; } }

        public ModernConstellationControl()
        {
            DoubleBuffered = true;
            BackColor = Color.FromArgb(10, 20, 32);
            ForeColor = Color.White;
            Font = new Font("Segoe UI Semibold", 8f);
            Size = new Size(150, 150);
        }

        public void UpdateData(byte[,] constellation, string mode)
        {
            if (constellation == null || constellation.Rank != 2 || constellation.GetLength(1) < 2 || constellation.GetLength(0) == 0)
            {
                history.Clear();
                hasPoints = false;
            }
            else
            {
                // STV0910 ISYMB/QSYMB are signed 8-bit values. Convert to signed
                // coordinates before plotting so the axes cross at zero.
                for (int index = 0; index < constellation.GetLength(0); index++)
                {
                    float x = unchecked((sbyte)constellation[index, 0]);
                    float y = unchecked((sbyte)constellation[index, 1]);
                    history.Enqueue(new PointF(x, y));
                    while (history.Count > MaxHistory) history.Dequeue();
                }
                hasPoints = history.Count > 0;
            }
            modeText = string.IsNullOrWhiteSpace(mode) ? "CONSTELLATION" : mode.Trim();
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(BackColor);

            Rectangle plot = new Rectangle(10, 24, Math.Max(10, Width - 20), Math.Max(10, Height - 34));
            using (Pen border = new Pen(Color.FromArgb(65, 95, 120)))
            using (Pen axis = new Pen(Color.FromArgb(55, 110, 135)))
            using (Brush dot = new SolidBrush(Color.FromArgb(80, 220, 255)))
            using (Brush text = new SolidBrush(Color.FromArgb(225, 235, 245)))
            {
                g.DrawRectangle(border, plot);
                int cx = plot.Left + plot.Width / 2;
                int cy = plot.Top + plot.Height / 2;
                g.DrawLine(axis, cx, plot.Top, cx, plot.Bottom);
                g.DrawLine(axis, plot.Left, cy, plot.Right, cy);
                g.DrawString(modeText, Font, text, 8, 5);

                foreach (PointF sample in history)
                {
                    float x = cx + (sample.X / 128f) * (plot.Width / 2f);
                    float y = cy - (sample.Y / 128f) * (plot.Height / 2f);
                    g.FillEllipse(dot, x - 1.1f, y - 1.1f, 2.2f, 2.2f);
                }
            }
        }
    }
}
