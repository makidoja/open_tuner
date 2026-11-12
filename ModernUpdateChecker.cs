using System;
using System.Diagnostics;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Newtonsoft.Json.Linq;

namespace opentuner
{
    /// <summary>
    /// Optional startup notice for published, non-prerelease GitHub releases.
    /// Never blocks receiver startup or downloads/executes installers.
    /// </summary>
    internal static class ModernUpdateChecker
    {
        private const string LatestReleaseApi =
            "https://api.github.com/repos/makidoja/open_tuner/releases/latest";
        private const string ReleasesUrl =
            "https://github.com/makidoja/open_tuner/releases";
        private static int started;

        internal static void CheckOnStartup(Form owner)
        {
            if (owner == null || Interlocked.Exchange(ref started, 1) != 0)
                return;
            Task.Run(delegate
            {
                try
                {
                    Version installed = ParseVersion(GlobalDefines.PublicReleaseVersion);
                    if (installed == null) return;
                    HttpWebRequest request = (HttpWebRequest)WebRequest.Create(LatestReleaseApi);
                    request.UserAgent = "OpenTuner-UpdateChecker";
                    request.Accept = "application/vnd.github+json";
                    request.Timeout = 7000;
                    request.ReadWriteTimeout = 7000;
                    string json;
                    using (WebResponse response = request.GetResponse())
                    using (var reader = new System.IO.StreamReader(response.GetResponseStream()))
                        json = reader.ReadToEnd();
                    JObject release = JObject.Parse(json);
                    if ((bool?)release["draft"] == true || (bool?)release["prerelease"] == true)
                        return;
                    string tag = (string)release["tag_name"];
                    Version latest = ParseVersion(tag);
                    if (latest == null || latest.CompareTo(installed) <= 0) return;
                    string url = (string)release["html_url"];
                    if (string.IsNullOrWhiteSpace(url) ||
                        !url.StartsWith(ReleasesUrl, StringComparison.OrdinalIgnoreCase))
                        url = ReleasesUrl;

                    if (!owner.IsDisposed && owner.IsHandleCreated)
                        owner.BeginInvoke((MethodInvoker)delegate
                        {
                            if (owner.IsDisposed) return;
                            DialogResult answer = MessageBox.Show(owner,
                                "A newer OpenTuner release is available.\n\n" +
                                "Installed: " + installed + "\n" +
                                "Available: " + latest + "\n\n" +
                                "Open the GitHub release page to download it?",
                                "OpenTuner — Update available",
                                MessageBoxButtons.YesNo, MessageBoxIcon.Information);
                            if (answer == DialogResult.Yes)
                            {
                                try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
                                catch (Exception ex) { Serilog.Log.Warning(ex, "Unable to open OpenTuner release page"); }
                            }
                        });
                }
                catch (Exception ex)
                {
                    // An unavailable GitHub API or offline connection never
                    // interferes with receiving or generates a nuisance dialog.
                    Serilog.Log.Debug(ex, "OpenTuner update check skipped");
                }
            });
        }

        private static Version ParseVersion(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            Match m = Regex.Match(text.Trim(), @"(?:^|[^0-9])(\d+)\.(\d+)\.(\d+)$");
            if (!m.Success) return null;
            Version parsed;
            return Version.TryParse(m.Groups[1].Value + "." + m.Groups[2].Value +
                "." + m.Groups[3].Value, out parsed) ? parsed : null;
        }
    }
}
