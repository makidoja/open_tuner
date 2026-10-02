using System;
using System.Collections.Generic;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;

namespace opentuner
{
    internal static class ModernFullscreenSupport
    {
        private static readonly Dictionary<int, Form> activeWindows = new Dictionary<int, Form>();

        public static void Attach(ModernConceptForm form)
        {
            if (form == null) return;

            HookButtons(form);
            form.Shown += delegate { HookButtons(form); };
        }

        private static void HookButtons(ModernConceptForm form)
        {
            try
            {
                FieldInfo hostsField = typeof(ModernConceptForm).GetField(
                    "videoHosts", BindingFlags.Instance | BindingFlags.NonPublic);
                Panel[] hosts = hostsField == null ? null : hostsField.GetValue(form) as Panel[];
                if (hosts == null) return;

                for (int tuner = 0; tuner < hosts.Length; tuner++)
                {
                    Panel host = hosts[tuner];
                    if (host == null || host.Parent == null) continue;

                    Button button = FindButton(host.Parent, "⛶");
                    if (button == null) continue;
                    if (button.Tag != null && button.Tag.ToString() == "modern-fullscreen-hooked") continue;

                    int capturedTuner = tuner;
                    button.Tag = "modern-fullscreen-hooked";
                    button.Click += delegate { ToggleFullscreen(form, capturedTuner); };
                }
            }
            catch
            {
            }
        }

        private static Button FindButton(Control root, string text)
        {
            foreach (Control control in root.Controls)
            {
                Button button = control as Button;
                if (button != null && button.Text == text)
                    return button;

                Button nested = FindButton(control, text);
                if (nested != null)
                    return nested;
            }
            return null;
        }

        private static void ToggleFullscreen(ModernConceptForm form, int tuner)
        {
            Form existing;
            if (activeWindows.TryGetValue(tuner, out existing) && existing != null && !existing.IsDisposed)
            {
                existing.Close();
                return;
            }

            FieldInfo hostsField = typeof(ModernConceptForm).GetField(
                "videoHosts", BindingFlags.Instance | BindingFlags.NonPublic);
            Panel[] hosts = hostsField == null ? null : hostsField.GetValue(form) as Panel[];
            if (hosts == null || tuner < 0 || tuner >= hosts.Length) return;

            Panel host = hosts[tuner];
            if (host == null || host.Parent == null) return;

            Control originalParent = host.Parent;
            int originalIndex = originalParent.Controls.GetChildIndex(host);
            DockStyle originalDock = host.Dock;
            AnchorStyles originalAnchor = host.Anchor;
            Rectangle originalBounds = host.Bounds;

            Form fullscreen = new Form
            {
                Text = "OpenTuner - Tuner " + (tuner + 1) + " Full Screen",
                BackColor = Color.Black,
                ForeColor = Color.White,
                FormBorderStyle = FormBorderStyle.None,
                WindowState = FormWindowState.Maximized,
                StartPosition = FormStartPosition.Manual,
                KeyPreview = true,
                ShowInTaskbar = true
            };

            Label hint = new Label
            {
                Text = "ESC or double-click to exit full screen",
                AutoSize = true,
                BackColor = Color.FromArgb(150, 0, 0, 0),
                ForeColor = Color.White,
                Padding = new Padding(8, 5, 8, 5),
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };

            fullscreen.Controls.Add(hint);
            fullscreen.Resize += delegate
            {
                hint.Location = new Point(
                    Math.Max(8, fullscreen.ClientSize.Width - hint.Width - 12), 12);
                hint.BringToFront();
            };

            fullscreen.KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Escape)
                {
                    e.Handled = true;
                    fullscreen.Close();
                }
            };
            fullscreen.DoubleClick += delegate { fullscreen.Close(); };
            host.DoubleClick += delegate { if (!fullscreen.IsDisposed) fullscreen.Close(); };

            fullscreen.FormClosed += delegate
            {
                try
                {
                    fullscreen.Controls.Remove(host);
                    originalParent.Controls.Add(host);
                    originalParent.Controls.SetChildIndex(host, Math.Min(originalIndex, originalParent.Controls.Count - 1));
                    host.Dock = originalDock;
                    host.Anchor = originalAnchor;
                    host.Bounds = originalBounds;
                    host.BringToFront();
                }
                catch
                {
                }

                activeWindows.Remove(tuner);
            };

            originalParent.Controls.Remove(host);
            fullscreen.Controls.Add(host);
            host.Dock = DockStyle.Fill;
            host.BringToFront();
            hint.BringToFront();

            activeWindows[tuner] = fullscreen;
            fullscreen.Show(form);
            fullscreen.Activate();
        }
    }
}
