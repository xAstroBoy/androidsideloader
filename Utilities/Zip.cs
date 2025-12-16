using JR.Utils.GUI.Forms;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;

namespace AndroidSideloader.Utilities
{
    public class ExtractionException : Exception
    {
        public ExtractionException(string message) : base(message) { }
    }

    internal class Zip
    {
        private static readonly SettingsManager settings = SettingsManager.Instance;

        // Progress callback: (percent, eta)
        public static Action<int, TimeSpan?> ExtractionProgressCallback { get; set; }
        public static Action<string> ExtractionStatusCallback { get; set; }

        public static void ExtractFile(string sourceArchive, string destination)
        {
            string args = $"x \"{sourceArchive}\" -y -o\"{destination}\" -bsp1";
            DoExtract(args);
        }

        public static void ExtractFile(string sourceArchive, string destination, string password)
        {
            string args = $"x \"{sourceArchive}\" -y -o\"{destination}\" -p\"{password}\" -bsp1";
            DoExtract(args);
        }

        private static string extractionError = null;
        private static bool errorMessageShown = false;
        private static void DoExtract(string args)
        {
            if (!File.Exists(Path.Combine(Environment.CurrentDirectory, "7z.exe")) || !File.Exists(Path.Combine(Environment.CurrentDirectory, "7z.dll")))
            {
                _ = Logger.Log("Begin download 7-zip");
                string architecture = Environment.Is64BitOperatingSystem ? "64" : "";
                try
                {
                    // Use DNS fallback download method from GetDependencies
                    GetDependencies.DownloadFileWithDnsFallback($"https://github.com/VRPirates/rookie/raw/master/7z{architecture}.exe", "7z.exe");
                    GetDependencies.DownloadFileWithDnsFallback($"https://github.com/VRPirates/rookie/raw/master/7z{architecture}.dll", "7z.dll");
                }
                catch (Exception ex)
                {
                    _ = FlexibleMessageBox.Show(Program.form, $"You are unable to access the GitHub page with the Exception: {ex.Message}\nSome files may be missing (7z)");
                    _ = FlexibleMessageBox.Show(Program.form, "7z was unable to be downloaded\nRookie will now close");
                    Application.Exit();
                }
                _ = Logger.Log("Complete download 7-zip");
            }

            ProcessStartInfo pro = new ProcessStartInfo
            {
                WindowStyle = ProcessWindowStyle.Hidden,
                FileName = "7z.exe",
                Arguments = args,
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true
            };

            _ = Logger.Log($"Extract: 7z {string.Join(" ", args.Split(' ').Where(a => !a.StartsWith("-p")))}");

            // ETA tracking
            DateTime extractStart = DateTime.UtcNow;
            int etaLastPercent = 0;
            DateTime etaLastPercentTime = DateTime.UtcNow;
            double smoothedSecondsPerPercent = 0;
            TimeSpan? lastReportedEta = null;
            int lastReportedPercent = -1;
            const double SmoothingAlpha = 0.15;
            const double EtaChangeThreshold = 0.10;

            using (Process x = new Process())
            {
                x.StartInfo = pro;

                if (MainForm.isInDownloadExtract && x != null)
                {
                    x.OutputDataReceived += (sender, e) =>
                    {
                        if (e.Data != null)
                        {
                            // Parse 7-Zip progress output (e.g., " 45% - filename")
                            var match = Regex.Match(e.Data, @"^\s*(\d+)%");
                            if (match.Success && int.TryParse(match.Groups[1].Value, out int percent))
                            {
                                TimeSpan? eta = null;

                                // Calculate ETA
                                if (percent > etaLastPercent && percent < 100)
                                {
                                    var now = DateTime.UtcNow;
                                    double secondsForThisChunk = (now - etaLastPercentTime).TotalSeconds;
                                    int percentGained = percent - etaLastPercent;

                                    if (percentGained > 0 && secondsForThisChunk > 0)
                                    {
                                        double secondsPerPercent = secondsForThisChunk / percentGained;

                                        if (smoothedSecondsPerPercent == 0)
                                            smoothedSecondsPerPercent = secondsPerPercent;
                                        else
                                            smoothedSecondsPerPercent = SmoothingAlpha * secondsPerPercent + (1 - SmoothingAlpha) * smoothedSecondsPerPercent;

                                        int remainingPercent = 100 - percent;
                                        double etaSeconds = remainingPercent * smoothedSecondsPerPercent;
                                        var newEta = TimeSpan.FromSeconds(Math.Max(0, etaSeconds));

                                        // Only update if significant change
                                        if (!lastReportedEta.HasValue ||
                                            Math.Abs(newEta.TotalSeconds - lastReportedEta.Value.TotalSeconds) / Math.Max(1, lastReportedEta.Value.TotalSeconds) > EtaChangeThreshold)
                                        {
                                            eta = newEta;
                                            lastReportedEta = eta;
                                        }
                                        else
                                        {
                                            eta = lastReportedEta;
                                        }

                                        etaLastPercent = percent;
                                        etaLastPercentTime = now;
                                    }
                                }
                                else
                                {
                                    eta = lastReportedEta;
                                }

                                // Only report if percent changed
                                if (percent != lastReportedPercent)
                                {
                                    lastReportedPercent = percent;

                                    MainForm mainForm = (MainForm)Application.OpenForms[0];
                                    if (mainForm != null)
                                    {
                                        mainForm.Invoke((Action)(() => mainForm.SetProgress(percent)));
                                    }

                                    ExtractionProgressCallback?.Invoke(percent, eta);
                                }
                            }

                            // Extract filename from output
                            var fileMatch = Regex.Match(e.Data, @"- (.+)$");
                            if (fileMatch.Success)
                            {
                                string fileName = Path.GetFileName(fileMatch.Groups[1].Value.Trim());
                                if (!string.IsNullOrEmpty(fileName))
                                {
                                    ExtractionStatusCallback?.Invoke(fileName);
                                }
                            }
                        }
                    };
                }

                x.ErrorDataReceived += (sender, e) =>
                {
                    if (e.Data != null)
                    {
                        var error = e.Data;
                        if (error.Contains("There is not enough space on the disk") && !errorMessageShown)
                        {
                            errorMessageShown = true;
                            Program.form.Invoke(new Action(() =>
                            {
                                _ = FlexibleMessageBox.Show(Program.form, $"Not enough space to extract archive.\r\nMake sure your {Path.GetPathRoot(settings.DownloadDir)} drive has at least double the space of the game, then try again.",
                                   "NOT ENOUGH SPACE",
                                   MessageBoxButtons.OK,
                                   MessageBoxIcon.Error);
                                return;
                            }));
                        }
                        _ = Logger.Log(error, LogLevel.ERROR);
                        extractionError = $"Extracting failed: {error}"; // Store the error message directly
                        return;
                    }
                };

                x.Start();
                x.BeginOutputReadLine();
                x.BeginErrorReadLine();
                x.WaitForExit();

                // Clear callbacks
                ExtractionProgressCallback?.Invoke(100, null);
                ExtractionStatusCallback?.Invoke("");

                errorMessageShown = false;

                if (!string.IsNullOrEmpty(extractionError))
                {
                    string errorMessage = extractionError;
                    extractionError = null; // Reset the error message
                    throw new ExtractionException(errorMessage);
                }
            }
        }
    }
}
