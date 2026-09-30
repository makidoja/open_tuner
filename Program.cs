using FlyleafLib;
using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace opentuner
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        public static LoggingLevelSwitch levelSwitch;
        public static bool FFmpegEngineAvailable { get; private set; } = false;
        public static string FFmpegStartupError { get; private set; } = "";

        [DllImport("user32.dll")]
        private static extern bool ShowWindow([In] IntPtr hWnd, [In] int nCmdShow);

        [DllImport("kernel32.dll")]
        static extern IntPtr GetConsoleWindow();

        [STAThread]
        static void Main(string[] args)
        {
            int i = 0;
            int debugLevel = 3; // Warning
            levelSwitch = new LoggingLevelSwitch();

            while (i < args.Length)
            {
                switch (args[i])
                {
                    case "--debuglevel":
                        int new_debug_level = -1;

                        if (i + 1 < args.Length && int.TryParse(args[i + 1], out new_debug_level))
                        {
                            if (new_debug_level < 6 && new_debug_level >= 0)
                                debugLevel = new_debug_level;
                            i += 1;
                        }
                        break;

                    case "--hideconsolewindow":
                        IntPtr handle = GetConsoleWindow();
                        if (handle != IntPtr.Zero)
                            ShowWindow(handle, 0);
                        break;

                    default:
                        break;
                }
                i += 1;
            }

            switch (debugLevel)
            {
                case 0: levelSwitch.MinimumLevel = LogEventLevel.Verbose; break;
                case 1: levelSwitch.MinimumLevel = LogEventLevel.Debug; break;
                case 2: levelSwitch.MinimumLevel = LogEventLevel.Information; break;
                case 3: levelSwitch.MinimumLevel = LogEventLevel.Warning; break;
                case 4: levelSwitch.MinimumLevel = LogEventLevel.Error; break;
                case 5: levelSwitch.MinimumLevel = LogEventLevel.Fatal; break;
                default: levelSwitch.MinimumLevel = LogEventLevel.Warning; break;
            }

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.ControlledBy(levelSwitch)
                .WriteTo.Console()
                .WriteTo.File("logs\\ot_log_" + DateTime.Now.ToString("yyyy-dd-M--HH-mm-ss") + ".txt")
                .CreateLogger();

            LogEventLevel lastMinimumLevel = levelSwitch.MinimumLevel;
            levelSwitch.MinimumLevel = LogEventLevel.Information;
            Log.Information("Starting OpenTuner");
            levelSwitch.MinimumLevel = lastMinimumLevel;

            string logDirectory = AppDomain.CurrentDomain.BaseDirectory + "logs\\";

            if (Directory.Exists(logDirectory))
            {
                var logFiles = Directory.GetFiles(logDirectory, "*.txt")
                    .Select(f => new FileInfo(f))
                    .OrderByDescending(f => f.CreationTime);
                int fileCount = logFiles.Count();
                if (fileCount > 10)
                {
                    i = 0;
                    foreach (var file in logFiles)
                    {
                        if (i > 9)
                        {
                            try
                            {
                                File.Delete(file.FullName);
                                Log.Debug("Log file deleted: " + file.Name);
                            }
                            catch
                            {
                                Log.Warning("Log file for deletion not found: " + file.Name);
                            }
                        }
                        i++;
                    }
                }
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // Flyleaf/FFmpeg is an optional media engine. The application must still
            // start when its native runtime folder is absent; VLC remains available.
            try
            {
                Engine.Start(new EngineConfig()
                {
                    FFmpegPath = @"ffmpeg\",
                    FFmpegDevices = false,
                });
                FFmpegEngineAvailable = true;
            }
            catch (Exception ex)
            {
                FFmpegEngineAvailable = false;
                FFmpegStartupError = ex.Message;
                Log.Error(ex, "FFmpeg/Flyleaf engine unavailable. Continuing with remaining media engines.");
            }

            try
            {
                MainForm mainForm = new MainForm(args);

                // The modern controls are created by MainForm.OnShown. Queue the final
                // layout pass after that code has completed so docking/z-order is stable.
                mainForm.Shown += delegate
                {
                    mainForm.BeginInvoke((MethodInvoker)delegate
                    {
                        ModernRuntimeLayout.Apply(mainForm);
                    });
                };

                if (!FFmpegEngineAvailable)
                {
                    mainForm.Shown += delegate
                    {
                        MessageBox.Show(
                            "The FFmpeg runtime was not found in this test package.\r\n\r\n" +
                            "OpenTuner will continue to run, but the FFmpeg/Flyleaf media-player option is unavailable. " +
                            "Use VLC for testing this build.\r\n\r\n" +
                            "Details: " + FFmpegStartupError,
                            "OpenTuner - FFmpeg runtime missing",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Warning);
                    };
                }

                Application.Run(mainForm);
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Program.Main: Uncaught Exception");
                MessageBox.Show(
                    "OpenTuner could not start.\r\n\r\n" + ex,
                    "OpenTuner startup error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }
    }

    /// <summary>
    /// Final layout pass for the modern shell. This deliberately changes presentation
    /// only; the existing receiver controls remain available through the original menus.
    /// </summary>
    internal static class ModernRuntimeLayout
    {
        public static void Apply(Form form)
        {
            if (form == null || form.IsDisposed)
                return;

            form.SuspendLayout();

            Control header = FindControl(form, "modernHeader");
            Control receiverStrip = FindControl(form, "modernReceiverStrip");

            // WinForms docks controls according to z-order. In the first test build the
            // header was being laid out below the receiver strip. Making the strip the
            // front-most top-docked control places the header above it on screen.
            if (receiverStrip != null)
                receiverStrip.BringToFront();

            if (header != null)
                header.Height = 60;

            if (receiverStrip != null)
                receiverStrip.Height = 88;

            // The modern tuner cards replace the always-visible legacy property column.
            // Users can still restore it with the existing Show/Hide Properties command.
            SplitContainer mainSplit = FindControl(form, "splitContainer1") as SplitContainer;
            if (mainSplit != null)
            {
                mainSplit.BorderStyle = BorderStyle.None;
                mainSplit.SplitterWidth = 2;
                mainSplit.BackColor = ModernTheme.Border;

                if (!mainSplit.Panel1Collapsed)
                    mainSplit.Panel1Collapsed = true;
            }

            foreach (Control control in GetAllControls(form))
            {
                SplitContainer split = control as SplitContainer;
                if (split != null)
                {
                    split.BorderStyle = BorderStyle.None;
                    split.SplitterWidth = 2;
                    split.BackColor = ModernTheme.Border;
                    split.Panel1.BackColor = ModernTheme.Background;
                    split.Panel2.BackColor = ModernTheme.Background;
                }
            }

            form.ResumeLayout(true);
            form.PerformLayout();
        }

        private static Control FindControl(Control root, string name)
        {
            if (root == null)
                return null;

            Control[] matches = root.Controls.Find(name, true);
            return matches.Length > 0 ? matches[0] : null;
        }

        private static System.Collections.Generic.IEnumerable<Control> GetAllControls(Control root)
        {
            foreach (Control child in root.Controls)
            {
                yield return child;

                foreach (Control descendant in GetAllControls(child))
                    yield return descendant;
            }
        }
    }
}
