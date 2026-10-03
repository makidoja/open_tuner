using System;
using System.Drawing;
using System.Windows.Forms;

namespace opentuner
{
    internal static class ModernReceiverBottomLayoutPass
    {
        private static bool attached;
        private static readonly Color Text = Color.FromArgb(242, 247, 252);
        private static readonly Color Muted = Color.FromArgb(142, 165, 190);

        public static void Attach(ModernConceptForm form)
        {
            if (attached || form == null) return;
            attached = true;

            form.Shown += delegate
            {
                Apply(form);
                form.Resize += delegate { Apply(form); };
            };
        }

        private static void Apply(ModernConceptForm form)
        {
            if (form == null || form.IsDisposed) return;

            for (int tuner = 0; tuner < 2; tuner++)
            {
                Panel tunerBox = FindByName<Panel>(form, "ModernTunerControlBox" + tuner);
                if (tunerBox == null || tunerBox.Parent == null) continue;

                Panel controls = tunerBox.Parent as Panel;
                if (controls == null) continue;

                // Keep the tuner controls as a tidy block on the bottom-right.
                tunerBox.Size = new Size(390, 122);
                tunerBox.Location = new Point(
                    Math.Max(525, controls.ClientSize.Width - tunerBox.Width - 4),
                    Math.Max(42, controls.ClientSize.Height - tunerBox.Height - 4));
                tunerBox.Anchor = AnchorStyles.Right | AnchorStyles.Bottom;

                LayoutTunerBox(tunerBox, tuner);
                LayoutReceiverStatusAndButtons(controls, tunerBox);
                tunerBox.BringToFront();
            }
        }

        private static void LayoutTunerBox(Panel box, int tuner)
        {
            MoveLabel(box, "TUNER CONTROL", 8, 2, 130, 18, 8.5f, Muted);
            MoveLabel(box, "FREQ", 8, 24, 42, 32, 8.5f, Muted);
            MoveLabel(box, "SR", 194, 24, 24, 32, 8.5f, Muted);
            MoveLabel(box, "TUNER", 8, 60, 48, 32, 8.5f, Muted);
            MoveLabel(box, "LO FREQ", 194, 60, 58, 32, 8.5f, Muted);

            Panel freqBorder = FindByName<Panel>(box, "ModernFrequencyBorder" + tuner);
            if (freqBorder != null)
            {
                freqBorder.Location = new Point(52, 24);
                freqBorder.Size = new Size(130, 32);
                StyleEntry(freqBorder, 10.5f);
            }

            ComboBox sr = FindByName<ComboBox>(box, "ModernSrCombo" + tuner);
            if (sr != null)
            {
                sr.Location = new Point(220, 24);
                sr.Size = new Size(100, 32);
                sr.Font = new Font("Segoe UI Semibold", 10f);
            }

            ComboBox rf = FindByName<ComboBox>(box, "ModernRfInput" + tuner);
            if (rf != null)
            {
                rf.Location = new Point(58, 60);
                rf.Size = new Size(124, 32);
                rf.Font = new Font("Segoe UI Semibold", 10f);
            }

            Panel loBorder = FindByName<Panel>(box, "ModernLoBorder" + tuner);
            if (loBorder != null)
            {
                loBorder.Location = new Point(254, 60);
                loBorder.Size = new Size(90, 32);
                StyleEntry(loBorder, 10.5f);
            }

            CheckBox lo = FindByName<CheckBox>(box, "ModernLoEnabled" + tuner);
            if (lo != null)
            {
                lo.Location = new Point(350, 64);
                lo.Size = new Size(38, 24);
                lo.Font = new Font("Segoe UI Semibold", 8.5f);
            }

            Button tune = FindButtonExact(box, "TUNE");
            if (tune != null)
            {
                tune.Location = new Point(280, 94);
                tune.Size = new Size(102, 25);
                tune.Font = new Font("Segoe UI Semibold", 9f);
            }
        }

        private static void LayoutReceiverStatusAndButtons(Panel controls, Panel tunerBox)
        {
            // One clean media-control row immediately below the RX picture.
            Label volIcon = FindLabelExact(controls, "🔊");
            if (volIcon != null) { volIcon.Location = new Point(4, 5); volIcon.Size = new Size(24, 30); }

            TrackBar volume = FindFirst<TrackBar>(controls);
            if (volume != null)
            {
                volume.Location = new Point(28, 4);
                volume.Size = new Size(124, 32);
            }

            Button mute = FindButtonContains(controls, "MUTE");
            Button record = FindButtonContains(controls, "RECORD");
            if (record == null) record = FindButtonContains(controls, "STOP");
            Button snapshot = FindButtonContains(controls, "SNAPSHOT");
            Button fullscreen = FindButtonExact(controls, "⛶");

            if (mute != null) { mute.Location = new Point(158, 5); mute.Size = new Size(72, 30); }
            if (record != null) { record.Location = new Point(236, 5); record.Size = new Size(88, 30); }
            if (snapshot != null) { snapshot.Location = new Point(330, 5); snapshot.Size = new Size(100, 30); }
            if (fullscreen != null) { fullscreen.Location = new Point(436, 5); fullscreen.Size = new Size(42, 30); }

            // Drop the main readout, MER and Margin below the media-control row.
            Label freq = FindLargestFrequencyLabel(controls);
            if (freq != null)
            {
                freq.Location = new Point(4, 43);
                freq.Size = new Size(225, 42);
                freq.Font = new Font("Segoe UI Semibold", 21f);
            }

            Label mer = FindLabelStarts(controls, "MER");
            Label margin = FindLabelStarts(controls, "Margin");
            if (mer != null)
            {
                mer.Location = new Point(235, 45);
                mer.Size = new Size(130, 24);
                mer.Font = new Font("Segoe UI Semibold", 11.5f);
                mer.ForeColor = Text;
            }
            if (margin != null)
            {
                margin.Location = new Point(375, 45);
                margin.Size = new Size(145, 24);
                margin.Font = new Font("Segoe UI Semibold", 11.5f);
                margin.ForeColor = Text;
            }

            // Existing MER/Margin bargraph panels created by ModernReceiverUiPass.
            foreach (Control c in controls.Controls)
            {
                Panel p = c as Panel;
                if (p == null || p == tunerBox || p.Height != 11) continue;
                if (p.Width == 124) p.Location = new Point(235, 72);
                else if (p.Width == 134) p.Location = new Point(375, 72);
            }

            Label locked = FindLabelContains(controls, "LOCK");
            if (locked != null)
            {
                locked.Location = new Point(4, 88);
                locked.Size = new Size(100, 28);
            }

            Label service = FindServiceLabel(controls, locked);
            if (service != null)
            {
                service.Location = new Point(114, 88);
                service.Size = new Size(200, 28);
                service.Font = new Font("Segoe UI Semibold", 11.5f);
                service.ForeColor = Color.White;
            }

            Label details = FindDetailsLabel(controls);
            if (details != null)
            {
                details.Location = new Point(4, 119);
                details.Size = new Size(Math.Max(290, tunerBox.Left - 12), 20);
                details.Font = new Font("Segoe UI Semibold", 9.25f);
            }
        }

        private static void StyleEntry(Panel border, float fontSize)
        {
            foreach (Control c in border.Controls)
            {
                TextBox t = c as TextBox;
                if (t == null) continue;
                t.AutoSize = false;
                t.Location = new Point(5, 3);
                t.Size = new Size(Math.Max(20, border.Width - 10), border.Height - 6);
                t.Font = new Font("Segoe UI Semibold", fontSize);
                t.TextAlign = HorizontalAlignment.Center;
                t.ForeColor = Text;
            }
        }

        private static void MoveLabel(Control root, string text, int x, int y, int w, int h, float size, Color colour)
        {
            Label l = FindLabelExact(root, text);
            if (l == null) return;
            l.Location = new Point(x, y);
            l.Size = new Size(w, h);
            l.Font = new Font("Segoe UI Semibold", size);
            l.ForeColor = colour;
        }

        private static Label FindLargestFrequencyLabel(Control root)
        {
            Label best = null;
            foreach (Control c in root.Controls)
            {
                Label l = c as Label;
                if (l != null && l.Font != null && l.Font.Size >= 18f)
                {
                    if (best == null || l.Font.Size > best.Font.Size) best = l;
                }
            }
            return best;
        }

        private static Label FindServiceLabel(Control root, Label locked)
        {
            foreach (Control c in root.Controls)
            {
                Label l = c as Label;
                if (l == null || l == locked) continue;
                string s = (l.Text ?? "").Trim();
                if (s.Length > 0 && !s.StartsWith("MER", StringComparison.OrdinalIgnoreCase) &&
                    !s.StartsWith("Margin", StringComparison.OrdinalIgnoreCase) &&
                    s.IndexOf("receiver details", StringComparison.OrdinalIgnoreCase) < 0 &&
                    s.IndexOf("MODCOD", StringComparison.OrdinalIgnoreCase) < 0 &&
                    l.Font != null && l.Font.Size >= 10f && l.Font.Size < 18f)
                    return l;
            }
            return null;
        }

        private static Label FindDetailsLabel(Control root)
        {
            foreach (Control c in root.Controls)
            {
                Label l = c as Label;
                if (l == null) continue;
                string s = l.Text ?? "";
                if (s.IndexOf("receiver details", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    s.IndexOf("MODCOD", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    s.IndexOf("kS", StringComparison.OrdinalIgnoreCase) >= 0)
                    return l;
            }
            return null;
        }

        private static Label FindLabelStarts(Control root, string text)
        {
            foreach (Control c in root.Controls)
            {
                Label l = c as Label;
                if (l != null && (l.Text ?? "").StartsWith(text, StringComparison.OrdinalIgnoreCase)) return l;
            }
            return null;
        }

        private static Label FindLabelContains(Control root, string text)
        {
            foreach (Control c in root.Controls)
            {
                Label l = c as Label;
                if (l != null && (l.Text ?? "").IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0) return l;
            }
            return null;
        }

        private static Label FindLabelExact(Control root, string text)
        {
            foreach (Control c in root.Controls)
            {
                Label l = c as Label;
                if (l != null && string.Equals((l.Text ?? "").Trim(), text, StringComparison.Ordinal)) return l;
                if (c.HasChildren)
                {
                    Label nested = FindLabelExact(c, text);
                    if (nested != null) return nested;
                }
            }
            return null;
        }

        private static Button FindButtonContains(Control root, string text)
        {
            foreach (Control c in root.Controls)
            {
                Button b = c as Button;
                if (b != null && (b.Text ?? "").IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0) return b;
                if (c.HasChildren)
                {
                    Button nested = FindButtonContains(c, text);
                    if (nested != null) return nested;
                }
            }
            return null;
        }

        private static Button FindButtonExact(Control root, string text)
        {
            foreach (Control c in root.Controls)
            {
                Button b = c as Button;
                if (b != null && string.Equals((b.Text ?? "").Trim(), text, StringComparison.OrdinalIgnoreCase)) return b;
                if (c.HasChildren)
                {
                    Button nested = FindButtonExact(c, text);
                    if (nested != null) return nested;
                }
            }
            return null;
        }

        private static T FindByName<T>(Control root, string name) where T : Control
        {
            if (root == null) return null;
            Control[] found = root.Controls.Find(name, true);
            if (found.Length == 0) return null;
            return found[0] as T;
        }

        private static T FindFirst<T>(Control root) where T : Control
        {
            foreach (Control c in root.Controls)
            {
                T match = c as T;
                if (match != null) return match;
                if (c.HasChildren)
                {
                    T nested = FindFirst<T>(c);
                    if (nested != null) return nested;
                }
            }
            return null;
        }
    }
}
