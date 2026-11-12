using System;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;

namespace opentuner
{
    internal static class ModernSelectorFixPass
    {
        private static bool attached;
        private static readonly Color Surface2 = Color.FromArgb(18, 38, 60);
        private static readonly Color Border = Color.FromArgb(37, 67, 94);
        private static readonly Color Text = Color.FromArgb(242, 247, 252);
        private static readonly Color Surface = Color.FromArgb(13, 28, 45);

        public static void Attach(ModernConceptForm form)
        {
            if (attached || form == null) return;
            attached = true;

            form.Shown += delegate
            {
                form.BeginInvoke((MethodInvoker)delegate
                {
                    FixTopToolbar(form);
                    FixTunerSelectors(form);
                });
            };
        }

        internal static void StyleDialogSelectors(Form form)
        {
            if (form == null || form.IsDisposed) return;
            StyleDialogSelectorsRecursive(form.Controls);
        }

        private static void StyleDialogSelectorsRecursive(Control.ControlCollection controls)
        {
            foreach (Control c in controls)
            {
                ComboBox combo = c as ComboBox;
                if (combo != null && combo.Visible)
                {
                    string safeName = string.IsNullOrWhiteSpace(combo.Name)
                        ? "Combo" + combo.GetHashCode().ToString("X")
                        : combo.Name;
                    Button button = ReplaceSelector(combo, "ModernDialog_" + safeName, false);
                    if (button != null)
                    {
                        button.Height = Math.Max(30, combo.Height + 4);
                        button.Width = combo.Width;
                    }
                }

                if (c.HasChildren)
                    StyleDialogSelectorsRecursive(c.Controls);
            }
        }

        private static void FixTopToolbar(ModernConceptForm form)
        {
            FlowLayoutPanel bar = FindControl<FlowLayoutPanel>(form, "ModernTopToolbar");
            if (bar == null) return;

            ComboBox lnbA = FindControl<ComboBox>(form, "ModernLnbA");
            ComboBox lnbB = FindControl<ComboBox>(form, "ModernLnbB");
            Label hw = FindControl<Label>(form, "ModernHardwareLabel");

            Button a = ReplaceSelector(lnbA, "ModernLnbAButton", true);
            Button b = ReplaceSelector(lnbB, "ModernLnbBButton", true);

            // Match the SOURCE SETTINGS toolbar button (30px tall).
            // A and B must sit together, in that order, after HW.
            foreach (Button button in new Button[] { a, b })
            {
                if (button == null) continue;
                button.AutoSize = false;
                button.Size = new Size(122, 30);
                button.Font = new Font("Segoe UI Semibold", 8.25f);
                button.Padding = new Padding(6, 0, 6, 0);
                button.Margin = new Padding(0, 0, 5, 0);
                button.FlatStyle = FlatStyle.Flat;
                button.FlatAppearance.BorderColor = Border;
                button.BackColor = Surface2;
                button.ForeColor = Text;
                button.TextAlign = ContentAlignment.MiddleLeft;
            }

            HideStaleLnbButtons(bar, a, b);

            if (hw != null && a != null && b != null)
            {
                // Rebuild the pair's order explicitly. Moving B with
                // SetChildIndex against a previously calculated A index
                // was sending B to the far-left of the FlowLayoutPanel.
                bar.SuspendLayout();
                try
                {
                    bar.Controls.Remove(a);
                    bar.Controls.Remove(b);
                    int afterHardware = bar.Controls.GetChildIndex(hw, false) + 1;
                    bar.Controls.Add(a);
                    bar.Controls.Add(b);
                    bar.Controls.SetChildIndex(a, afterHardware);
                    bar.Controls.SetChildIndex(b, afterHardware + 1);
                }
                finally { bar.ResumeLayout(true); }
            }
        }

        private static void HideStaleLnbButtons(FlowLayoutPanel bar, Button keepA, Button keepB)
        {
            foreach (Control c in bar.Controls)
            {
                Button button = c as Button;
                if (button == null || button == keepA || button == keepB) continue;

                string text = button.Text ?? string.Empty;
                if (text.StartsWith("LNB A:", StringComparison.OrdinalIgnoreCase) ||
                    text.StartsWith("LNB B:", StringComparison.OrdinalIgnoreCase))
                    button.Visible = false;
            }
        }

        private static void FixTunerSelectors(ModernConceptForm form)
        {
            // Native WinForms dropdowns use a bright OS-themed arrow and
            // selection area. Present both SR and RF connector as the same
            // dark menu-backed button used elsewhere in the modern theme.
            // Unlike the old RX1/2-only pass, apply to all four receivers
            // and keep proxies exactly in sync with the real control bounds.
            for (int tuner = 0; tuner < 4; tuner++)
            {
                ComboBox sr = FindControl<ComboBox>(form, "ModernSrCombo" + tuner);
                ComboBox rf = FindControl<ComboBox>(form, "ModernRfInput" + tuner);
                if (sr != null)
                    ReplaceSelector(sr, "ModernSrButton" + tuner, false);
                if (rf != null)
                    ReplaceSelector(rf, "ModernRfButton" + tuner, true);
            }
        }

        private static Button ReplaceSelector(ComboBox combo, string buttonName, bool raiseCommit)
        {
            if (combo == null || combo.Parent == null) return null;

            Control existing = combo.Parent.Controls[buttonName];
            int desiredHeight = buttonName.StartsWith("ModernLnb", StringComparison.Ordinal)
                ? 30 : buttonName.StartsWith("ModernSrButton", StringComparison.Ordinal) ||
                  buttonName.StartsWith("ModernRfButton", StringComparison.Ordinal)
                ? 29 : Math.Max(30, combo.Height);
            if (existing is Button)
            {
                existing.Bounds = new Rectangle(combo.Left, combo.Top, combo.Width, desiredHeight);
                existing.Visible = true;
                combo.Visible = false;
                return (Button)existing;
            }

            HideOldProxy(combo);

            Button button = new Button
            {
                Name = buttonName,
                Location = combo.Location,
                Size = new Size(combo.Width, desiredHeight),
                Anchor = combo.Anchor,
                FlatStyle = FlatStyle.Flat,
                BackColor = Surface2,
                ForeColor = Text,
                Font = combo.Font,
                TextAlign = ContentAlignment.MiddleLeft,
                Cursor = Cursors.Hand,
                Padding = new Padding(6, 0, 4, 0),
                TabStop = combo.TabStop,
                Enabled = combo.Enabled,
                Margin = combo.Margin,
                UseVisualStyleBackColor = false
            };
            button.FlatAppearance.BorderColor = Border;
            button.FlatAppearance.BorderSize = 1;

            Action refresh = delegate
            {
                if (button.IsDisposed) return;
                string value = combo.SelectedItem != null ? combo.GetItemText(combo.SelectedItem) : combo.Text;
                button.Text = (value ?? string.Empty) + "  ▼";
            };
            refresh();

            button.Click += delegate
            {
                if (combo.Items.Count == 0) return;

                ContextMenuStrip menu = new ContextMenuStrip
                {
                    Font = combo.Font,
                    ShowImageMargin = false,
                    MinimumSize = new Size(button.Width, 0),
                    BackColor = Surface,
                    ForeColor = Text
                };
                ModernWindowTheme.ThemeContextMenu(menu);

                for (int i = 0; i < combo.Items.Count; i++)
                {
                    int index = i;
                    ToolStripMenuItem item = new ToolStripMenuItem(combo.GetItemText(combo.Items[i]))
                    {
                        ForeColor = Text,
                        BackColor = Surface,
                        Checked = i == combo.SelectedIndex
                    };
                    item.Click += delegate
                    {
                        combo.SelectedIndex = index;
                        if (raiseCommit)
                            RaiseSelectionChangeCommitted(combo);
                        refresh();
                    };
                    menu.Items.Add(item);
                }

                menu.Show(button, new Point(0, button.Height));
            };

            combo.SelectedIndexChanged += delegate { refresh(); };
            combo.TextChanged += delegate { refresh(); };
            combo.EnabledChanged += delegate { if (!button.IsDisposed) button.Enabled = combo.Enabled; };
            // The bottom layout pass owns actual dropdown positions. Follow
            // every bounds change so proxies cannot drift over FREQ or LO.
            EventHandler syncBounds = delegate
            {
                if (!button.IsDisposed && !combo.IsDisposed)
                {
                    // Native WinForms DropDownList controls clamp their height.
                    // Never transfer that clamped height to the themed button.
                    // In a FlowLayoutPanel, BringToFront changes the toolbar
                    // *order*, so never call it for LNB controls.
                    button.Bounds = new Rectangle(combo.Left, combo.Top,
                        combo.Width, desiredHeight);
                    button.Anchor = combo.Anchor;
                    if (!(button.Parent is FlowLayoutPanel))
                        button.BringToFront();
                }
            };
            combo.LocationChanged += syncBounds;
            combo.SizeChanged += syncBounds;

            combo.Parent.Controls.Add(button);
            if (!(button.Parent is FlowLayoutPanel))
                button.BringToFront();
            combo.Visible = false;
            return button;
        }

        private static void HideOldProxy(ComboBox combo)
        {
            if (combo == null || combo.Parent == null) return;

            string selected = combo.SelectedItem != null ? combo.GetItemText(combo.SelectedItem) : combo.Text;
            foreach (Control c in combo.Parent.Controls)
            {
                Button b = c as Button;
                if (b == null || b.Name.StartsWith("Modern", StringComparison.OrdinalIgnoreCase)) continue;

                bool overlaps = Math.Abs(b.Left - combo.Left) <= 4 && Math.Abs(b.Top - combo.Top) <= 6;
                bool sameText = !string.IsNullOrEmpty(selected) && (b.Text ?? "").StartsWith(selected, StringComparison.OrdinalIgnoreCase);
                if (overlaps && sameText)
                    b.Visible = false;
            }
        }

        private static void RaiseSelectionChangeCommitted(ComboBox combo)
        {
            try
            {
                MethodInfo method = typeof(ComboBox).GetMethod("OnSelectionChangeCommitted", BindingFlags.Instance | BindingFlags.NonPublic);
                if (method != null) method.Invoke(combo, new object[] { EventArgs.Empty });
            }
            catch
            {
            }
        }

        private static T FindControl<T>(Control root, string name) where T : Control
        {
            if (root == null) return null;
            if (string.Equals(root.Name, name, StringComparison.Ordinal)) return root as T;

            foreach (Control c in root.Controls)
            {
                T found = FindControl<T>(c, name);
                if (found != null) return found;
            }
            return null;
        }
    }
}
