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
                g.Clear(Color.Transparent);

                Rectangle outer = new Rectangle(2, 2, 60, 60);
                using (LinearGradientBrush bg = new LinearGradientBrush(
                    outer,
                    Color.FromArgb(28, 139, 253),
                    Color.FromArgb(13, 28, 45),
                    45f))
                {
                    g.FillEllipse(bg, outer);
                }

                using (Pen ring = new Pen(Color.FromArgb(120, 242, 247, 252), 2f))
                    g.DrawEllipse(ring, new Rectangle(7, 7, 50, 50));

                using (Font font = new Font("Segoe UI", 20f, FontStyle.Bold, GraphicsUnit.Pixel))
                using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                using (Brush text = new SolidBrush(Color.White))
                {
                    g.DrawString("OT", font, text, new RectangleF(0, 0, 64, 64), sf);
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
