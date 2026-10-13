using System;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using opentuner.MediaSources;

namespace opentuner
{
    // Keep diagnostics out of the native video renderer's host.
    internal static class ModernConstellationPass
    {
        private static readonly ModernConstellationControl[] displays = new ModernConstellationControl[2];
        private static readonly CheckBox[] toggles = new CheckBox[2];
        private static readonly Panel[] statusHosts = new Panel[2];
        private static bool attached;

        public static void Attach(ModernConceptForm form)
        {
            if (attached || form == null) return;
            attached = true;
            form.Shown += delegate
            {
                MainForm backend = GetBackend(form);
                Panel[] videos = GetVideoHosts(form);
                if (backend == null || videos == null) return;

                for (int tuner = 0; tuner < 2; tuner++)
                {
                    int index = tuner;
                    Panel video = videos[tuner];
                    if (video == null || video.Parent == null) continue;
                    Panel status = null;
                    foreach (Control sibling in video.Parent.Controls)
                    {
                        Panel candidate = sibling as Panel;
                        if (candidate != null && candidate != video && candidate.Dock == DockStyle.Bottom)
                        {
                            status = candidate;
                            break;
                        }
                    }
                    if (status == null) continue;
                    statusHosts[tuner] = status;
                    ModernConstellationControl plot = new ModernConstellationControl
                    {
                        Name = "ModernConstellation" + tuner,
                        Size = new Size(108, 108),
                        Visible = false
                    };
                    CheckBox toggle = new CheckBox
                    {
                        Name = "ModernShowConstellation" + tuner,
                        Text = "Constellation",
                        Checked = true,
                        AutoSize = false,
                        Size = new Size(113, 19),
                        ForeColor = Color.FromArgb(220, 230, 242),
                        BackColor = status.BackColor,
                        Font = new Font("Segoe UI", 8f)
                    };
                    status.Controls.Add(plot);
                    status.Controls.Add(toggle);
                    displays[tuner] = plot;
                    toggles[tuner] = toggle;
                    toggle.CheckedChanged += delegate { plot.Visible = toggle.Checked && plot.HasPoints; };
                    status.Resize += delegate { Position(index); };
                    Position(index);
                }

                backend.BackendSourceData += delegate(int tuner, OTSourceData data, string description)
                {
                    if (tuner < 0 || tuner >= 2 || form.IsDisposed) return;
                    byte[,] constellation = data != null && data.demod_locked ? data.constellation : null;
                    string mode = data == null ? "" : data.delivery_system;
                    if (data != null && !string.IsNullOrWhiteSpace(data.modcode))
                        mode = string.IsNullOrWhiteSpace(mode) ? data.modcode : mode + "  " + data.modcode;
                    try
                    {
                        form.BeginInvoke((MethodInvoker)delegate
                        {
                            if (form.IsDisposed) return;
                            ModernConstellationControl plot = displays[tuner];
                            if (plot == null || plot.IsDisposed) return;
                            plot.UpdateData(constellation, mode);
                            plot.Visible = toggles[tuner] != null && toggles[tuner].Checked && plot.HasPoints;
                        });
                    }
                    catch (InvalidOperationException) { }
                };
            };
        }

        private static void Position(int tuner)
        {
            Panel host = statusHosts[tuner];
            ModernConstellationControl plot = displays[tuner];
            CheckBox toggle = toggles[tuner];
            if (host == null || plot == null || toggle == null || host.IsDisposed) return;
            // The metrics begin directly under Record (x=236). Centre the square
            // in the available horizontal space before the tuner control box.
            Control tunerBox = null;
            foreach (Control child in host.Controls)
                if (child.Name == "ModernTunerControlBox" + tuner) { tunerBox = child; break; }
            int metricsRight = 236 + 92;
            int tunerLeft = tunerBox == null ? host.ClientSize.Width - 354 : tunerBox.Left;
            int available = tunerLeft - metricsRight;
            int plotLeft = metricsRight + (available - plot.Width) / 2;
            plot.Location = new Point(Math.Max(metricsRight + 2, plotLeft), 40);
            toggle.Location = new Point(236, 147);
            plot.BringToFront();
            toggle.BringToFront();
        }

        private static MainForm GetBackend(ModernConceptForm form)
        {
            FieldInfo field = typeof(ModernConceptForm).GetField("backend", BindingFlags.Instance | BindingFlags.NonPublic);
            return field == null ? null : field.GetValue(form) as MainForm;
        }

        private static Panel[] GetVideoHosts(ModernConceptForm form)
        {
            FieldInfo field = typeof(ModernConceptForm).GetField("videoHosts", BindingFlags.Instance | BindingFlags.NonPublic);
            return field == null ? null : field.GetValue(form) as Panel[];
        }
    }
}
