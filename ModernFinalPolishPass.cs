using System;
using System.Drawing;
using System.Windows.Forms;

namespace opentuner
{
    internal static class ModernFinalPolishPass
    {
        private static bool attached;

        public static void Attach(ModernConceptForm form)
        {
            if (attached || form == null) return;
            attached = true;

            form.Shown += delegate
            {
                // Keep enough horizontal room for CONNECTED, both LNB selectors,
                // receiver RF selection and LO fields without controls colliding.
                if (form.MinimumSize.Width < 1380 || form.MinimumSize.Height < 760)
                    form.MinimumSize = new Size(Math.Max(1380, form.MinimumSize.Width), Math.Max(760, form.MinimumSize.Height));

                PolishControls(form);
            };

            form.Resize += delegate { PolishControls(form); };
        }

        private static void PolishControls(Control root)
        {
            if (root == null || root.IsDisposed) return;

            foreach (Control c in root.Controls)
            {
                ComboBox combo = c as ComboBox;
                if (combo != null)
                    ModernWindowTheme.ThemeComboBox(combo);

                Label label = c as Label;
                if (label != null)
                {
                    string text = label.Text ?? "";

                    // Bottom receiver telemetry: make it easier to read without making
                    // it dominate the tuning controls. The station/callsign strip is left
                    // at its original size as requested.
                    if (text.IndexOf("MODCOD", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        text.IndexOf("receiver details", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        label.Font = new Font("Segoe UI Semibold", 10.25f);
                        label.ForeColor = Color.FromArgb(215, 228, 240);
                        label.AutoSize = true;
                    }
                }

                if (c.HasChildren)
                    PolishControls(c);
            }
        }
    }
}
