using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace opentuner
{
    internal static class ModernWindowTheme
    {
        private static readonly Color Bg = Color.FromArgb(7, 17, 29);
        private static readonly Color Surface = Color.FromArgb(13, 28, 45);
        private static readonly Color Surface2 = Color.FromArgb(18, 38, 60);
        private static readonly Color Border = Color.FromArgb(37, 67, 94);
        private static readonly Color Accent = Color.FromArgb(28, 139, 253);
        private static readonly Color Text = Color.FromArgb(242, 247, 252);
        private static readonly Color Muted = Color.FromArgb(142, 165, 190);

        private static readonly HashSet<IntPtr> StyledForms = new HashSet<IntPtr>();
        private static readonly HashSet<IntPtr> StyledTabs = new HashSet<IntPtr>();
        private static readonly ToolStripRenderer DarkRenderer = new ToolStripProfessionalRenderer(new DarkColorTable());
        private static bool enabled;

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        public static void EnableGlobalStyling()
        {
            if (enabled) return;
            enabled = true;

            Application.Idle += delegate
            {
                try
                {
                    foreach (Form form in Application.OpenForms)
                    {
                        if (form == null || form.IsDisposed || !form.IsHandleCreated)
                            continue;

                        // The main receiver already has its own purpose-built styling.
                        if (form is ModernConceptForm)
                        {
                            ApplyDarkTitleBar(form);
                            continue;
                        }

                        if (!StyledForms.Contains(form.Handle))
                        {
                            Apply(form);
                            StyledForms.Add(form.Handle);
                        }
                    }
                }
                catch
                {
                }
            };
        }

        public static void Apply(Form form)
        {
            if (form == null) return;

            form.BackColor = Bg;
            form.ForeColor = Text;
            form.Font = new Font("Segoe UI", 9f);
            ApplyDarkTitleBar(form);
            StyleChildren(form.Controls);
        }

        private static void ApplyDarkTitleBar(Form form)
        {
            try
            {
                int useDark = 1;
                int result = DwmSetWindowAttribute(form.Handle, 20, ref useDark, sizeof(int));
                if (result != 0)
                    DwmSetWindowAttribute(form.Handle, 19, ref useDark, sizeof(int));
            }
            catch
            {
            }
        }

        private static void StyleChildren(Control.ControlCollection controls)
        {
            foreach (Control control in controls)
            {
                StyleControl(control);
                if (control.HasChildren)
                    StyleChildren(control.Controls);
            }
        }

        private static void StyleControl(Control control)
        {
            if (control == null) return;

            if (control is Button)
            {
                Button b = (Button)control;
                b.UseVisualStyleBackColor = false;
                b.FlatStyle = FlatStyle.Flat;
                b.BackColor = Surface2;
                b.ForeColor = Text;
                b.FlatAppearance.BorderColor = Border;
                b.FlatAppearance.BorderSize = 1;
                return;
            }

            if (control is TextBoxBase)
            {
                TextBoxBase tb = (TextBoxBase)control;
                tb.BackColor = Surface2;
                tb.ForeColor = Text;
                tb.BorderStyle = BorderStyle.FixedSingle;
                return;
            }

            if (control is ComboBox)
            {
                ComboBox cb = (ComboBox)control;
                cb.BackColor = Surface2;
                cb.ForeColor = Text;
                cb.FlatStyle = FlatStyle.Flat;
                return;
            }

            if (control is NumericUpDown)
            {
                NumericUpDown n = (NumericUpDown)control;
                n.BackColor = Surface2;
                n.ForeColor = Text;
                n.BorderStyle = BorderStyle.FixedSingle;
                return;
            }

            if (control is ListBox)
            {
                ListBox lb = (ListBox)control;
                lb.BackColor = Surface2;
                lb.ForeColor = Text;
                lb.BorderStyle = BorderStyle.FixedSingle;
                return;
            }

            if (control is CheckedListBox)
            {
                CheckedListBox clb = (CheckedListBox)control;
                clb.BackColor = Surface2;
                clb.ForeColor = Text;
                clb.BorderStyle = BorderStyle.FixedSingle;
                return;
            }

            if (control is ListView)
            {
                ListView lv = (ListView)control;
                lv.BackColor = Surface2;
                lv.ForeColor = Text;
                lv.BorderStyle = BorderStyle.FixedSingle;
                return;
            }

            if (control is TreeView)
            {
                TreeView tv = (TreeView)control;
                tv.BackColor = Surface2;
                tv.ForeColor = Text;
                tv.BorderStyle = BorderStyle.FixedSingle;
                return;
            }

            if (control is DataGridView)
            {
                DataGridView grid = (DataGridView)control;
                grid.BackgroundColor = Surface;
                grid.BorderStyle = BorderStyle.None;
                grid.GridColor = Border;
                grid.EnableHeadersVisualStyles = false;
                grid.DefaultCellStyle.BackColor = Surface2;
                grid.DefaultCellStyle.ForeColor = Text;
                grid.DefaultCellStyle.SelectionBackColor = Accent;
                grid.DefaultCellStyle.SelectionForeColor = Color.White;
                grid.ColumnHeadersDefaultCellStyle.BackColor = Surface;
                grid.ColumnHeadersDefaultCellStyle.ForeColor = Text;
                grid.RowHeadersDefaultCellStyle.BackColor = Surface;
                grid.RowHeadersDefaultCellStyle.ForeColor = Text;
                return;
            }

            if (control is TabControl)
            {
                TabControl tabs = (TabControl)control;
                tabs.BackColor = Bg;
                tabs.ForeColor = Text;
                tabs.DrawMode = TabDrawMode.OwnerDrawFixed;
                if (tabs.IsHandleCreated && !StyledTabs.Contains(tabs.Handle))
                {
                    tabs.DrawItem += DrawTab;
                    StyledTabs.Add(tabs.Handle);
                }
                return;
            }

            if (control is TabPage)
            {
                control.BackColor = Bg;
                control.ForeColor = Text;
                return;
            }

            if (control is GroupBox)
            {
                control.BackColor = Surface;
                control.ForeColor = Text;
                return;
            }

            if (control is Panel || control is SplitContainer || control is TableLayoutPanel || control is FlowLayoutPanel)
            {
                control.BackColor = Bg;
                control.ForeColor = Text;
                return;
            }

            if (control is ToolStrip)
            {
                ToolStrip strip = (ToolStrip)control;
                strip.Renderer = DarkRenderer;
                strip.BackColor = Surface;
                strip.ForeColor = Text;
                foreach (ToolStripItem item in strip.Items)
                    item.ForeColor = Text;
                return;
            }

            if (control is Label)
            {
                Label label = (Label)control;
                label.BackColor = Color.Transparent;
                bool looksLikeLink = label.Cursor == Cursors.Hand || label.ForeColor == Color.Blue || label.ForeColor == Color.RoyalBlue;
                label.ForeColor = looksLikeLink ? Accent : Text;
                return;
            }

            if (control is LinkLabel)
            {
                LinkLabel link = (LinkLabel)control;
                link.BackColor = Color.Transparent;
                link.LinkColor = Accent;
                link.ActiveLinkColor = Color.White;
                link.VisitedLinkColor = Accent;
                return;
            }

            if (control is CheckBox || control is RadioButton)
            {
                control.BackColor = Color.Transparent;
                control.ForeColor = Text;
                return;
            }

            if (control is TrackBar)
            {
                control.BackColor = Surface;
                control.ForeColor = Text;
                return;
            }

            // Leave video/spectrum PictureBoxes alone. Everything else receives the
            // common text colour so old setup dialogs no longer look like a separate app.
            if (!(control is PictureBox))
                control.ForeColor = Text;
        }

        private static void DrawTab(object sender, DrawItemEventArgs e)
        {
            try
            {
                TabControl tabs = sender as TabControl;
                if (tabs == null || e.Index < 0 || e.Index >= tabs.TabPages.Count) return;

                Rectangle r = e.Bounds;
                bool selected = e.Index == tabs.SelectedIndex;
                using (Brush back = new SolidBrush(selected ? Surface2 : Surface))
                    e.Graphics.FillRectangle(back, r);
                using (Pen pen = new Pen(Border))
                    e.Graphics.DrawRectangle(pen, r.X, r.Y, r.Width - 1, r.Height - 1);
                TextRenderer.DrawText(e.Graphics, tabs.TabPages[e.Index].Text, tabs.Font, r,
                    selected ? Color.White : Muted,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            }
            catch
            {
            }
        }

        private sealed class DarkColorTable : ProfessionalColorTable
        {
            public override Color ToolStripGradientBegin { get { return Surface; } }
            public override Color ToolStripGradientMiddle { get { return Surface; } }
            public override Color ToolStripGradientEnd { get { return Surface; } }
            public override Color MenuStripGradientBegin { get { return Surface; } }
            public override Color MenuStripGradientEnd { get { return Surface; } }
            public override Color ToolStripDropDownBackground { get { return Surface; } }
            public override Color ImageMarginGradientBegin { get { return Surface; } }
            public override Color ImageMarginGradientMiddle { get { return Surface; } }
            public override Color ImageMarginGradientEnd { get { return Surface; } }
            public override Color MenuItemSelected { get { return Surface2; } }
            public override Color MenuItemBorder { get { return Border; } }
            public override Color MenuItemSelectedGradientBegin { get { return Surface2; } }
            public override Color MenuItemSelectedGradientEnd { get { return Surface2; } }
            public override Color MenuItemPressedGradientBegin { get { return Surface2; } }
            public override Color MenuItemPressedGradientMiddle { get { return Surface2; } }
            public override Color MenuItemPressedGradientEnd { get { return Surface2; } }
            public override Color SeparatorDark { get { return Border; } }
            public override Color SeparatorLight { get { return Border; } }
        }
    }
}
