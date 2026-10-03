using System;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using opentuner.MediaSources;

namespace opentuner
{
    internal static class ModernReceiverStatusPass
    {
        private static bool attached;
        private static readonly Label[] detailLabels = new Label[2];

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
                    Control card = hosts[tuner] == null ? null : hosts[tuner].Parent;
                    if (card != null)
                        detailLabels[tuner] = FindDetailLabel(card);
                }

                backend.BackendSourceData += delegate(int tuner, OTSourceData data, string description)
                {
                    if (data == null || tuner < 0 || tuner > 1 || form.IsDisposed) return;

                    form.BeginInvoke((MethodInvoker)delegate
                    {
                        Label label = detailLabels[tuner];
                        if (label == null) return;

                        string delivery = backend.BackendGetDeliverySystem(tuner);
                        if (string.IsNullOrWhiteSpace(delivery))
                            delivery = data.demod_locked ? "DVB" : "SEARCHING";

                        // DVB-S uses QPSK. DVB-S2 modulation/FEC is only shown if the
                        // receiver actually supplies a value; never show an empty MODCOD placeholder.
                        string mode = delivery;
                        if (string.Equals(delivery, "DVB-S", StringComparison.OrdinalIgnoreCase))
                            mode = "DVB-S QPSK";
                        else if (!string.IsNullOrWhiteSpace(data.modcode))
                            mode = delivery + " " + data.modcode.Trim();

                        string symbolRate = data.symbol_rate > 0 ? data.symbol_rate + " kS" : "SR —";
                        string rf = backend.BackendGetRfInput(tuner);
                        long offset = backend.BackendGetOffset(tuner);
                        string lo = offset > 0 ? (offset / 1000.0).ToString("0.###") + " MHz LO" : "DIRECT";

                        label.Text = mode + "   •   " + symbolRate +
                                     "   •   Tuner " + rf + "   •   " + lo +
                                     (data.streaming ? "   •   TS" : "") +
                                     (data.recording || backend.BackendIsRecording(tuner) ? "   •   REC" : "");
                    });
                };
            };
        }

        private static MainForm GetBackend(ModernConceptForm form)
        {
            FieldInfo f = typeof(ModernConceptForm).GetField("backend", BindingFlags.Instance | BindingFlags.NonPublic);
            return f == null ? null : f.GetValue(form) as MainForm;
        }

        private static Panel[] GetVideoHosts(ModernConceptForm form)
        {
            FieldInfo f = typeof(ModernConceptForm).GetField("videoHosts", BindingFlags.Instance | BindingFlags.NonPublic);
            return f == null ? null : f.GetValue(form) as Panel[];
        }

        private static Label FindDetailLabel(Control root)
        {
            foreach (Control c in root.Controls)
            {
                Label l = c as Label;
                if (l != null)
                {
                    string s = l.Text ?? "";
                    if (s.IndexOf("receiver details", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        s.IndexOf("MODCOD", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        s.IndexOf(" kS", StringComparison.OrdinalIgnoreCase) >= 0)
                        return l;
                }

                if (c.HasChildren)
                {
                    Label nested = FindDetailLabel(c);
                    if (nested != null) return nested;
                }
            }
            return null;
        }
    }
}
