using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace opentuner
{
    internal static class ModernFullscreenSupport
    {
        private static readonly Dictionary<int, Form> activeWindows = new Dictionary<int, Form>();

        // Screen last used for full screen this session (by device name), so the
        // next full-screen window opens where the user moved the previous one.
        private static string lastScreenName;

        private static Screen InitialScreen(Form owner)
        {
            if (!string.IsNullOrEmpty(lastScreenName))
                foreach (Screen sc in Screen.AllScreens)
                    if (sc.DeviceName == lastScreenName) return sc;
            return Screen.FromControl(owner);
        }

        // A borderless maximised window maximises on the monitor that holds its
        // normal bounds, so restore, move the bounds, then maximise again.
        private static void PlaceOnScreen(Form window, Screen screen)
        {
            window.WindowState = FormWindowState.Normal;
            window.Bounds = screen.Bounds;
            window.WindowState = FormWindowState.Maximized;
            lastScreenName = screen.DeviceName;
        }

        private static void MoveToAdjacentScreen(Form window, int direction)
        {
            Screen[] screens = Screen.AllScreens;
            if (screens.Length < 2) return;
            // Order left-to-right (then top-to-bottom) so arrows match the layout.
            Array.Sort(screens, (a, b) => a.Bounds.X != b.Bounds.X
                ? a.Bounds.X.CompareTo(b.Bounds.X) : a.Bounds.Y.CompareTo(b.Bounds.Y));
            Screen current = Screen.FromControl(window);
            int index = Array.FindIndex(screens, sc => sc.DeviceName == current.DeviceName);
            if (index < 0) index = 0;
            int next = (index + direction + screens.Length) % screens.Length;
            PlaceOnScreen(window, screens[next]);
        }

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
                Panel[] hosts = form.VideoHosts;
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

            Panel[] hosts = form.VideoHosts;
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
                Text = Screen.AllScreens.Length > 1
                    ? "Press ESC to exit full screen  •  ← → move to another screen"
                    : "Press ESC to exit full screen",
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
                else if (e.KeyCode == Keys.Left || e.KeyCode == Keys.Right)
                {
                    e.Handled = true;
                    MoveToAdjacentScreen(fullscreen, e.KeyCode == Keys.Right ? 1 : -1);
                }
            };
            // ESC only: double-click is used by the player to toggle its info bar.
            // (The host handler was also added again on every open and never removed.)

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
            // Open on the main window's screen, or the screen last used for full screen.
            Screen target = InitialScreen(form);
            fullscreen.Bounds = target.Bounds;
            lastScreenName = target.DeviceName;
            fullscreen.Show(form);
            fullscreen.Activate();
        }
    }
}
