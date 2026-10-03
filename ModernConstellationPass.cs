using System;
using System.Reflection;
using System.Windows.Forms;
using opentuner.MediaSources;

namespace opentuner
{
    internal static class ModernConstellationPass
    {
        private static bool attached;
        private static readonly ModernConstellationControl[] displays = new ModernConstellationControl[2];
        private static Panel[] videoHosts;

        public static void Attach(ModernConceptForm form)
        {
            if (attached || form == null) return;
            attached = true;

            form.Shown += delegate
            {
                MainForm backend = GetBackend(form);
                videoHosts = GetVideoHosts(form);
                if (backend == null || videoHosts == null) return;

                // Do not place an empty diagnostics control into the video host at startup.
                // The video ownership pass uses an empty host as the signal that the real
                // VLC/Flyleaf player control still needs to be attached.  Create the
                // constellation overlay lazily only when a source supplies genuine IQ
                // symbol coordinates.
                backend.BackendSourceData += delegate(int tuner, OTSourceData data, string description)
                {
                    if (data == null || tuner < 0 || tuner > 1 || form.IsDisposed) return;
                    if (data.constellation == null || data.constellation.Length == 0) return;

                    try
                    {
                        form.BeginInvoke((MethodInvoker)delegate
                        {
                            if (form.IsDisposed) return;

                            ModernConstellationControl display = EnsureDisplay(tuner);
                            if (display == null || display.IsDisposed) return;

                            string mode = data.delivery_system;
                            if (!string.IsNullOrWhiteSpace(data.modcode))
                                mode = string.IsNullOrWhiteSpace(mode) ? data.modcode : mode + "  " + data.modcode;
                            if (string.IsNullOrWhiteSpace(mode)) mode = "CONSTELLATION";

                            display.UpdateData(data.constellation, mode);
                            display.BringToFront();
                        });
                    }
                    catch
                    {
                    }
                };
            };
        }

        private static ModernConstellationControl EnsureDisplay(int tuner)
        {
            if (videoHosts == null || tuner < 0 || tuner >= videoHosts.Length) return null;
            Panel host = videoHosts[tuner];
            if (host == null || host.IsDisposed) return null;

            ModernConstellationControl existing = displays[tuner];
            if (existing != null && !existing.IsDisposed) return existing;

            ModernConstellationControl display = new ModernConstellationControl
            {
                Name = "ModernConstellation" + tuner,
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Visible = false
            };
            display.Location = new System.Drawing.Point(Math.Max(8, host.ClientSize.Width - display.Width - 10), 10);
            host.Controls.Add(display);
            display.BringToFront();
            displays[tuner] = display;

            int capturedTuner = tuner;
            host.Resize += delegate
            {
                ModernConstellationControl d = displays[capturedTuner];
                if (d != null && !d.IsDisposed)
                    d.Location = new System.Drawing.Point(Math.Max(8, host.ClientSize.Width - d.Width - 10), 10);
            };

            return display;
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
