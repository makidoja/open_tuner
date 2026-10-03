using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using FlyleafLib;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace opentuner
{
    internal static class Program
    {
        public static bool FFmpegEngineAvailable = false;
        public static string FFmpegStartupError = "";
        public static LoggingLevelSwitch levelSwitch = new LoggingLevelSwitch();

        [STAThread]
        static void Main(string[] args)
        {
            int i = 0;

            levelSwitch.MinimumLevel = LogEventLevel.Debug;

            string logPath = AppDomain.CurrentDomain.BaseDirectory + "logs\\opentuner_.txt";

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.ControlledBy(levelSwitch)
                .WriteTo.Console()
                .WriteTo.File(logPath, rollingInterval: RollingInterval.Day, retainedFileCountLimit: 10)
                .CreateLogger();

            LogEventLevel lastMinimumLevel = levelSwitch.MinimumLevel;
            levelSwitch.MinimumLevel = LogEventLevel.Information;
            Log.Information("Starting OpenTuner Modern concept UI");
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
                            try { File.Delete(file.FullName); }
                            catch { Log.Warning("Log file for deletion not found: " + file.Name); }
                        }
                        i++;
                    }
                }
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            ModernWindowTheme.EnableGlobalStyling();

            try
            {
                Engine.Start(new EngineConfig()
                {
                    FFmpegPath = @"ffmpeg\\",
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
                ModernConceptForm mainForm = new ModernConceptForm(args);
                ModernBranding.Apply(mainForm);
                ModernConceptRuntimeFixes.Attach(mainForm);
                ModernFullscreenSupport.Attach(mainForm);
                ModernReceiverUiPass.Attach(mainForm);
                ModernCompactUiPass.Attach(mainForm);
                ModernHardwareUiPass.Attach(mainForm);
                ModernPresetUiSync.Attach(mainForm);
                ModernSelectorFixPass.Attach(mainForm);
                ModernFinalPolishPass.Attach(mainForm);
                ModernReceiverBottomLayoutPass.Attach(mainForm);
                ModernReceiverStatusPass.Attach(mainForm);
                ModernBatcSpectrumPass.Attach(mainForm);

                if (!FFmpegEngineAvailable)
                {
                    mainForm.Shown += delegate
                    {
                        MessageBox.Show(
                            "The FFmpeg runtime was not found.\r\n\r\n" +
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
