using System.Drawing;
using System.Windows.Forms;

namespace opentuner.ModernUI
{
    /// <summary>
    /// Central colour, typography and spacing definitions for the modern OpenTuner UI.
    /// Keeping these values in one place lets us reskin controls without touching tuner logic.
    /// </summary>
    public static class ModernTheme
    {
        public static readonly Color Background = Color.FromArgb(14, 18, 24);
        public static readonly Color Surface = Color.FromArgb(22, 28, 36);
        public static readonly Color SurfaceRaised = Color.FromArgb(29, 36, 46);
        public static readonly Color SurfaceHover = Color.FromArgb(37, 46, 58);
        public static readonly Color Border = Color.FromArgb(52, 63, 78);

        public static readonly Color TextPrimary = Color.FromArgb(239, 244, 249);
        public static readonly Color TextSecondary = Color.FromArgb(159, 171, 185);
        public static readonly Color TextMuted = Color.FromArgb(108, 121, 136);

        public static readonly Color Accent = Color.FromArgb(45, 140, 255);
        public static readonly Color AccentHover = Color.FromArgb(69, 157, 255);
        public static readonly Color Success = Color.FromArgb(48, 209, 88);
        public static readonly Color Warning = Color.FromArgb(255, 184, 0);
        public static readonly Color Danger = Color.FromArgb(255, 84, 89);

        public const int Radius = 8;
        public const int GapSmall = 8;
        public const int Gap = 12;
        public const int GapLarge = 20;

        public static readonly Font FontBody = new Font("Segoe UI", 9.5f, FontStyle.Regular, GraphicsUnit.Point);
        public static readonly Font FontBodySemibold = new Font("Segoe UI Semibold", 9.5f, FontStyle.Regular, GraphicsUnit.Point);
        public static readonly Font FontSection = new Font("Segoe UI Semibold", 11f, FontStyle.Regular, GraphicsUnit.Point);
        public static readonly Font FontStatus = new Font("Segoe UI Semibold", 12f, FontStyle.Regular, GraphicsUnit.Point);
        public static readonly Font FontFrequency = new Font("Segoe UI Semibold", 28f, FontStyle.Regular, GraphicsUnit.Point);

        public static void ApplyToForm(Form form)
        {
            if (form == null) return;

            form.BackColor = Background;
            form.ForeColor = TextPrimary;
            form.Font = FontBody;
        }

        public static void ApplyToControlTree(Control root)
        {
            if (root == null) return;

            ApplyToControl(root);

            foreach (Control child in root.Controls)
                ApplyToControlTree(child);
        }

        private static void ApplyToControl(Control control)
        {
            if (control is Label)
            {
                control.ForeColor = TextPrimary;
                control.BackColor = Color.Transparent;
            }
            else if (control is Panel || control is SplitterPanel || control is UserControl)
            {
                control.BackColor = Background;
                control.ForeColor = TextPrimary;
            }
            else if (control is Button button)
            {
                button.FlatStyle = FlatStyle.Flat;
                button.FlatAppearance.BorderColor = Border;
                button.FlatAppearance.BorderSize = 1;
                button.BackColor = SurfaceRaised;
                button.ForeColor = TextPrimary;
                button.Font = FontBodySemibold;
            }
            else if (control is TextBox textBox)
            {
                textBox.BackColor = SurfaceRaised;
                textBox.ForeColor = TextPrimary;
                textBox.BorderStyle = BorderStyle.FixedSingle;
            }
            else if (control is ComboBox comboBox)
            {
                comboBox.BackColor = SurfaceRaised;
                comboBox.ForeColor = TextPrimary;
                comboBox.FlatStyle = FlatStyle.Flat;
            }
            else if (control is GroupBox groupBox)
            {
                groupBox.BackColor = Background;
                groupBox.ForeColor = TextPrimary;
                groupBox.Font = FontBodySemibold;
            }
            else if (control is TabControl tabControl)
            {
                tabControl.BackColor = Background;
                tabControl.ForeColor = TextPrimary;
            }
            else
            {
                control.ForeColor = TextPrimary;
                control.Font = FontBody;
            }
        }
    }
}
