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

        private static void FixTopToolbar(ModernConceptForm form)
        {
            FlowLayoutPanel bar = FindControl<FlowLayoutPanel>(form, "ModernTopToolbar");
            if (bar == null) return;

            ComboBox lnbA = FindControl<ComboBox>(form, "ModernLnbA");
            ComboBox lnbB = FindControl<ComboBox>(form, "ModernLnbB");
            Label hw = FindControl<Label>(form, "ModernHardwareLabel");

            Button a = ReplaceSelector(lnbA, "ModernLnbAButton", true);
            Button b = ReplaceSelector(lnbB, "ModernLnbBButton", true);

            if (a != null) a.Width = Math.Max(a.Width, 122);
            if (b != null) b.Width = Math.Max(b.Width, 112);

            HideStaleLnbButtons(bar, a, b);

            if (hw != null)
            {
                int hwIndex = bar.Controls.GetChildIndex(hw, false);
                if (a != null)
                {
                    bar.Controls.SetChildIndex(a, Math.Min(hwIndex + 1, bar.Controls.Count - 1));
                    a.Margin = new Padding(4, 4, 3, 0);
                }
                if (b != null)
                {
                    int aIndex = a == null ? hwIndex : bar.Controls.GetChildIndex(a, false);
                    bar.Controls.SetChildIndex(b, Math.Min(aIndex + 1, bar.Controls.Count - 1));
                    b.Margin = new Padding(3, 4, 3, 0);
                }
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
            for (int tuner = 0; tuner < 2; tuner++)
            {
                ComboBox sr = FindControl<ComboBox>(form, "ModernSrCombo" + tuner);
                ComboBox rf = FindControl<ComboBox>(form, "ModernRfInput" + tuner);

                Button srButton = ReplaceSelector(sr, "ModernSrButton" + tuner, false);
                Button rfButton = ReplaceSelector(rf, "ModernRfButton" + tuner, true);

                if (srButton != null)
                {
                    srButton.Height = 30;
                    srButton.Top = Math.Max(6, srButton.Top - 2);
                }

                if (rfButton != null)
                {
                    rfButton.Height = 30;
                    rfButton.Width = Math.Max(rfButton.Width, 90);
                    rfButton.Top = Math.Max(6, rfButton.Top - 2);
                }
            }
        }

        private static Button ReplaceSelector(ComboBox combo, string buttonName, bool raiseCommit)
        {
            if (combo == null || combo.Parent == null) return null;

            Control existing = combo.Parent.Controls[buttonName];
            if (existing is Button) return (Button)existing;

            HideOldProxy(combo);

            Button button = new Button
            {
                Name = buttonName,
                Location = combo.Location,
                Size = new Size(combo.Width, Math.Max(30, combo.Height + 4)),
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

            combo.Parent.Controls.Add(button);
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
