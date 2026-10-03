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

        public static void Attach(ModernConceptForm form)
        {
            if (attached || form == null) return;
            attached = true;

            form.Shown += delegate
            {
                MainForm backend = GetBackend(form);
                Panel[] hosts = GetVideoHosts(form);
                if (backend == null || hosts == null) return;

                for (int tuner = 0; tuner < 2 && tuner < hosts.Length; tuner++)
                {
                    Panel host = hosts[tuner];
                    if (host == null) continue;

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
                }

                backend.BackendSourceData += delegate(int tuner, OTSourceData data, string description)
                {
                    if (data == null || tuner < 0 || tuner > 1 || form.IsDisposed) return;
                    form.BeginInvoke((MethodInvoker)delegate
                    {
                        ModernConstellationControl display = displays[tuner];
                        if (display == null || display.IsDisposed) return;

                        string mode = data.delivery_system;
                        if (!string.IsNullOrWhiteSpace(data.modcode))
                            mode = string.IsNullOrWhiteSpace(mode) ? data.modcode : mode + "  " + data.modcode;
                        if (string.IsNullOrWhiteSpace(mode)) mode = "CONSTELLATION";

                        display.UpdateData(data.constellation, mode);
                    });
                };
            };
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
