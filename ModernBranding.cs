using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace opentuner
{
    internal static class ModernBranding
    {
        public const string ProductTitle = "Open Tuner (ZR6TG) Version M0CKE 0.C.21026";

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool DestroyIcon(IntPtr handle);

        public static void Apply(Form form)
        {
            if (form == null) return;

            form.Text = ProductTitle;
            form.ShowIcon = true;

            try
            {
                form.Icon = BuildIcon();
            }
            catch
            {
                // Branding must never prevent the receiver from starting.
            }
        }

        private static Icon BuildIcon()
        {
            using (Bitmap bitmap = new Bitmap(64, 64))
            using (Graphics g = Graphics.FromImage(bitmap))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.Clear(Color.Transparent);

                Rectangle outer = new Rectangle(2, 2, 60, 60);
                using (LinearGradientBrush bg = new LinearGradientBrush(
                    outer,
                    Color.FromArgb(17, 72, 126),
                    Color.FromArgb(5, 22, 38),
                    55f))
                {
                    g.FillEllipse(bg, outer);
                }

                using (Pen rim = new Pen(Color.FromArgb(70, 170, 255), 2.5f))
                    g.DrawEllipse(rim, new Rectangle(3, 3, 58, 58));

                // Satellite dish reflector.
                using (GraphicsPath dish = new GraphicsPath())
                {
                    dish.AddBezier(new PointF(15, 21), new PointF(20, 39), new PointF(34, 47), new PointF(45, 47));
                    dish.AddBezier(new PointF(45, 47), new PointF(31, 52), new PointF(18, 43), new PointF(15, 21));
                    using (Brush fill = new SolidBrush(Color.White))
                        g.FillPath(fill, dish);
                }

                // Feed arm and support.
                using (Pen white = new Pen(Color.White, 3f))
                {
                    white.StartCap = LineCap.Round;
                    white.EndCap = LineCap.Round;
                    g.DrawLine(white, 28, 39, 43, 26);
                    g.DrawLine(white, 28, 43, 22, 55);
                    g.DrawLine(white, 17, 55, 35, 55);
                }

                using (Brush feed = new SolidBrush(Color.FromArgb(103, 215, 255)))
                    g.FillEllipse(feed, new RectangleF(40, 23, 6, 6));

                // Three RF wave arcs.
                using (Pen wave = new Pen(Color.FromArgb(36, 214, 239), 3f))
                {
                    wave.StartCap = LineCap.Round;
                    wave.EndCap = LineCap.Round;
                    g.DrawArc(wave, 34, 10, 18, 18, 285, 72);
                    g.DrawArc(wave, 31, 6, 27, 27, 286, 72);
                    g.DrawArc(wave, 28, 2, 36, 36, 287, 72);
                }

                IntPtr hIcon = bitmap.GetHicon();
                try
                {
                    using (Icon temp = Icon.FromHandle(hIcon))
                        return (Icon)temp.Clone();
                }
                finally
                {
                    DestroyIcon(hIcon);
                }
            }
        }
    }
}
