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

            // Flyleaf/FFmpeg is optional at startup. The packaged build normally carries
            // the runtime, but a missing native folder must not stop the receiver opening.
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

                // MainForm's existing OnShown code completes the legacy dynamic-control
                // setup. Queue our dashboard until the next message-loop turn so the new
                // view always wins the final z-order and becomes the operating surface.
                mainForm.Shown += delegate
                {
                    mainForm.BeginInvoke((MethodInvoker)delegate
                    {
                        try
                        {
                            var method = typeof(MainForm).GetMethod(
                                "BuildModernDashboard",
                                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

                            if (method == null)
                                throw new MissingMethodException("Modern dashboard entry point not found.");

                            method.Invoke(mainForm, null);
                        }
                        catch (Exception ex)
                        {
                            Log.Error(ex, "Could not create modern dashboard; leaving legacy view available.");
                            MessageBox.Show(
                                "The modern dashboard could not be created. The original OpenTuner controls will remain available.\r\n\r\n" + ex.Message,
                                "OpenTuner modern UI",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Warning);
                        }
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
}
