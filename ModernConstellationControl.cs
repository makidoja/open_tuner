using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace opentuner
{
    internal sealed class ModernConstellationControl : Control
    {
        private byte[,] points;
        private string modeText = "CONSTELLATION";

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
            points = constellation == null ? null : (byte[,])constellation.Clone();
            modeText = string.IsNullOrWhiteSpace(mode) ? "CONSTELLATION" : mode.Trim();
            Visible = points != null && points.GetLength(0) > 0;
            if (Visible)
            {
                BringToFront();
                Invalidate();
            }
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

                if (points == null || points.Rank != 2) return;

                int rows = points.GetLength(0);
                int cols = points.GetLength(1);
                if (cols < 2) return;

                for (int i = 0; i < rows; i++)
                {
                    float x = plot.Left + (points[i, 0] / 255f) * plot.Width;
                    float y = plot.Bottom - (points[i, 1] / 255f) * plot.Height;
                    g.FillEllipse(dot, x - 1.5f, y - 1.5f, 3f, 3f);
                }
            }
        }
    }
}
