using AdvancedSharpAdbClient;
using AdvancedSharpAdbClient.DeviceCommands;
using AdvancedSharpAdbClient.Models;
using AdvancedSharpAdbClient.Receivers;
using AndroidSideloader.Utilities;
using JR.Utils.GUI.Forms;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace AndroidSideloader
{
    internal class ADB
    {
        private static readonly SettingsManager settings = SettingsManager.Instance;
        public static string adbFolderPath = Path.Combine(Environment.CurrentDirectory, "platform-tools");
        public static string adbFilePath = Path.Combine(adbFolderPath, "adb.exe");
        public static string DeviceID = "";
        public static string package = "";
        public static bool wirelessadbON;

        // AdbClient for direct protocol communication
        private static AdbClient _adbClient;
        private static DeviceData _currentDevice;

        // Gets or initializes the AdbClient instance
        private static AdbClient GetAdbClient()
        {
            if (_adbClient == null)
            {
                // Ensure ADB server is started
                if (!AdbServer.Instance.GetStatus().IsRunning)
                {
                    var server = new AdbServer();
                    var result = server.StartServer(adbFilePath, false);
                    Logger.Log($"ADB server start result: {result}");
                }

                _adbClient = new AdbClient();
            }
            return _adbClient;
        }

        // Gets the current device for AdbClient operations
        private static DeviceData GetCurrentDevice()
        {
            var client = GetAdbClient();
            var devices = client.GetDevices();

            if (devices == null || !devices.Any())
            {
                Logger.Log("No devices found via AdbClient", LogLevel.WARNING);
                return default;
            }

            // If DeviceID is set, find that specific device
            if (!string.IsNullOrEmpty(DeviceID) && DeviceID.Length > 1)
            {
                var device = devices.FirstOrDefault(d => d.Serial == DeviceID || d.Serial.StartsWith(DeviceID));
                if (device.Serial != null)
                {
                    _currentDevice = device;
                    return device;
                }
            }

            // Otherwise return the first available device
            _currentDevice = devices.First();
            return _currentDevice;
        }

        public static ProcessOutput RunAdbCommandToString(string command)
        {
            command = command.Replace("adb", "");

            settings.ADBFolder = adbFolderPath;
            settings.ADBPath = adbFilePath;
            settings.Save();

            if (DeviceID.Length > 1)
            {
                command = $" -s {DeviceID} {command}";
            }

            if (!command.Contains("dumpsys") && !command.Contains("shell pm list packages") && !command.Contains("KEYCODE_WAKEUP"))
            {
                string logcmd = command;
                if (logcmd.Contains(Environment.CurrentDirectory))
                {
                    logcmd = logcmd.Replace($"{Environment.CurrentDirectory}", $"CurrentDirectory");
                }
                _ = Logger.Log($"Running command: {logcmd}");
            }

            using (Process adb = new Process())
            {
                adb.StartInfo.FileName = adbFilePath;
                adb.StartInfo.Arguments = command;
                adb.StartInfo.RedirectStandardError = true;
                adb.StartInfo.RedirectStandardOutput = true;
                adb.StartInfo.CreateNoWindow = true;
                adb.StartInfo.UseShellExecute = false;
                adb.StartInfo.WorkingDirectory = adbFolderPath;
                _ = adb.Start();

                string output = "";
                string error = "";

                try
                {
                    output = adb.StandardOutput.ReadToEnd();
                    error = adb.StandardError.ReadToEnd();
                }
                catch { }

                if (command.Contains("connect"))
                {
                    bool graceful = adb.WaitForExit(3000);
                    if (!graceful)
                    {
                        adb.Kill();
                        adb.WaitForExit();
                    }
                }

                if (error.Contains("ADB_VENDOR_KEYS") && !settings.AdbDebugWarned)
                {
                    ADBDebugWarning();
                }
                if (error.Contains("not enough storage space"))
                {
                    _ = FlexibleMessageBox.Show(Program.form, "There is not enough room on your device to install this package. Please clear AT LEAST 2x the amount of the app you are trying to install.");
                }
                if (!output.Contains("version") && !output.Contains("KEYCODE_WAKEUP") && !output.Contains("Filesystem") && !output.Contains("package:") && !output.Equals(null))
                {
                    _ = Logger.Log(output);
                }

                _ = Logger.Log(error, LogLevel.ERROR);
                return new ProcessOutput(output, error);
            }
        }

        // Executes a shell command on the device.
        private static void ExecuteShellCommand(AdbClient client, DeviceData device, string command)
        {
            var receiver = new ConsoleOutputReceiver();
            client.ExecuteRemoteCommand(command, device, receiver);
        }

        // Copies and installs an APK with real-time progress reporting using AdvancedSharpAdbClient
        public static async Task<ProcessOutput> SideloadWithProgressAsync(
            string path,
            Action<int> progressCallback = null,
            Action<string> statusCallback = null,
            string packagename = "",
            string gameName = "")
        {
            statusCallback?.Invoke("Installing APK...");
            progressCallback?.Invoke(0);

            try
            {
                var device = GetCurrentDevice();
                if (device.Serial == null)
                {
                    return new ProcessOutput("", "No device connected");
                }

                var client = GetAdbClient();
                var packageManager = new PackageManager(client, device);

                statusCallback?.Invoke("Installing APK...");

                // Create install progress handler
                Action<InstallProgressEventArgs> installProgress = (args) =>
                {
                    // Map PackageInstallProgressState to percentage
                    int percent = 0;
                    switch (args.State)
                    {
                        case PackageInstallProgressState.Preparing:
                            percent = 0;
                            statusCallback?.Invoke("Preparing...");
                            break;
                        case PackageInstallProgressState.Uploading:
                            percent = (int)Math.Round(args.UploadProgress);
                            statusCallback?.Invoke($"Installing · {args.UploadProgress:F0}%");
                            break;
                        case PackageInstallProgressState.Installing:
                            percent = 100;
                            statusCallback?.Invoke("Completing Installation...");
                            break;
                        case PackageInstallProgressState.Finished:
                            percent = 100;
                            statusCallback?.Invoke("");
                            break;
                        default:
                            percent = 50;
                            break;
                    }
                    progressCallback?.Invoke(percent);
                };

                // Install the package with progress
                await Task.Run(() =>
                {
                    packageManager.InstallPackage(path, installProgress);
                });

                progressCallback?.Invoke(100);
                statusCallback?.Invoke("");

                return new ProcessOutput($"{gameName}: Success\n");
            }
            catch (Exception ex)
            {
                Logger.Log($"SideloadWithProgressAsync error: {ex.Message}", LogLevel.ERROR);

                // Check for signature mismatch errors
                if (ex.Message.Contains("INSTALL_FAILED") ||
                    ex.Message.Contains("signatures do not match"))
                {
                    bool cancelClicked = false;

                    if (!settings.AutoReinstall)
                    {
                        Program.form.Invoke(() =>
                        {
                            DialogResult dialogResult1 = FlexibleMessageBox.Show(Program.form,
                                "In place upgrade has failed. Rookie can attempt to backup your save data and reinstall the game automatically, however some games do not store their saves in an accessible location (less than 5%). Continue with reinstall?",
                                "In place upgrade failed.", MessageBoxButtons.OKCancel);
                            if (dialogResult1 == DialogResult.Cancel)
                                cancelClicked = true;
                        });
                    }

                    if (cancelClicked)
                        return new ProcessOutput("", "Installation cancelled by user");

                    // Perform reinstall
                    statusCallback?.Invoke("Performing reinstall...");

                    try
                    {
                        var device = GetCurrentDevice();
                        var client = GetAdbClient();
                        var packageManager = new PackageManager(client, device);

                        // Backup save data
                        statusCallback?.Invoke("Backing up save data...");
                        _ = RunAdbCommandToString($"pull \"/sdcard/Android/data/{MainForm.CurrPCKG}\" \"{Environment.CurrentDirectory}\"");

                        // Uninstall
                        statusCallback?.Invoke("Uninstalling old version...");
                        packageManager.UninstallPackage(packagename);

                        // Reinstall with progress
                        statusCallback?.Invoke("Reinstalling game...");
                        Action<InstallProgressEventArgs> reinstallProgress = (args) =>
                        {
                            if (args.State == PackageInstallProgressState.Uploading)
                            {
                                progressCallback?.Invoke((int)Math.Round(args.UploadProgress));
                            }
                        };
                        packageManager.InstallPackage(path, reinstallProgress);

                        // Restore save data
                        statusCallback?.Invoke("Restoring save data...");
                        _ = RunAdbCommandToString($"push \"{Environment.CurrentDirectory}\\{MainForm.CurrPCKG}\" /sdcard/Android/data/");

                        string directoryToDelete = Path.Combine(Environment.CurrentDirectory, MainForm.CurrPCKG);
                        if (Directory.Exists(directoryToDelete) && directoryToDelete != Environment.CurrentDirectory)
                        {
                            Directory.Delete(directoryToDelete, true);
                        }

                        progressCallback?.Invoke(100);
                        return new ProcessOutput($"{gameName}: Reinstall: Success\n", "");
                    }
                    catch (Exception reinstallEx)
                    {
                        return new ProcessOutput($"{gameName}: Reinstall: Failed: {reinstallEx.Message}\n");
                    }
                }

                return new ProcessOutput("", ex.Message);
            }
        }

        // Copies OBB folder with real-time progress reporting using AdvancedSharpAdbClient
        public static async Task<ProcessOutput> CopyOBBWithProgressAsync(
            string localPath,
            Action<int> progressCallback = null,
            Action<string> statusCallback = null,
            string gameName = "")
        {
            string folderName = Path.GetFileName(localPath);

            if (!folderName.Contains("."))
            {
                return new ProcessOutput("No OBB Folder found");
            }

            try
            {
                var device = GetCurrentDevice();
                if (device.Serial == null)
                {
                    return new ProcessOutput("", "No device connected");
                }

                var client = GetAdbClient();
                string remotePath = $"/sdcard/Android/obb/{folderName}";

                statusCallback?.Invoke($"Preparing: {folderName}");
                progressCallback?.Invoke(0);

                // Delete existing OBB folder and create new one
                ExecuteShellCommand(client, device, $"rm -rf \"{remotePath}\"");
                ExecuteShellCommand(client, device, $"mkdir -p \"{remotePath}\"");

                // Get all files to push and calculate total size
                var files = Directory.GetFiles(localPath, "*", SearchOption.AllDirectories);
                long totalBytes = files.Sum(f => new FileInfo(f).Length);
                long transferredBytes = 0;

                statusCallback?.Invoke($"Copying: {folderName}");

                using (var syncService = new SyncService(client, device))
                {
                    foreach (var file in files)
                    {
                        string relativePath = file.Substring(localPath.Length)
                                                  .TrimStart('\\', '/')
                                                  .Replace('\\', '/');
                        string remoteFilePath = $"{remotePath}/{relativePath}";
                        string fileName = Path.GetFileName(file);

                        // Let UI know which file we're currently on
                        statusCallback?.Invoke(fileName);

                        // Ensure remote directory exists
                        string remoteDir = remoteFilePath.Substring(0, remoteFilePath.LastIndexOf('/'));
                        ExecuteShellCommand(client, device, $"mkdir -p \"{remoteDir}\"");

                        var fileInfo = new FileInfo(file);
                        long fileSize = fileInfo.Length;
                        long capturedTransferredBytes = transferredBytes;

                        // Progress handler for this file
                        Action<SyncProgressChangedEventArgs> progressHandler = (args) =>
                        {
                            long totalProgressBytes = capturedTransferredBytes + args.ReceivedBytesSize;

                            double overallPercent = totalBytes > 0
                                ? (totalProgressBytes * 100.0) / totalBytes
                                : 0.0;

                            int overallPercentInt = (int)Math.Round(overallPercent);
                            overallPercentInt = Math.Max(0, Math.Min(100, overallPercentInt));

                            // Single source of truth for UI (bar + label + text)
                            progressCallback?.Invoke(overallPercentInt);
                        };

                        // Push the file with progress
                        using (var stream = File.OpenRead(file))
                        {
                            await Task.Run(() =>
                            {
                                syncService.Push(
                                    stream,
                                    remoteFilePath,
                                    UnixFileStatus.DefaultFileMode,
                                    DateTime.Now,
                                    progressHandler,
                                    false);
                            });
                        }

                        // Mark this file as fully transferred
                        transferredBytes += fileSize;
                    }
                }

                // Ensure final 100% and clear status
                progressCallback?.Invoke(100);
                statusCallback?.Invoke("");

                return new ProcessOutput($"{gameName}: OBB transfer: Success\n", "");
            }
            catch (Exception ex)
            {
                Logger.Log($"CopyOBBWithProgressAsync error: {ex.Message}", LogLevel.ERROR);

                return new ProcessOutput("", $"{gameName}: OBB transfer: Failed: {ex.Message}\n");
            }
        }

        public static ProcessOutput RunAdbCommandToStringWOADB(string result, string path)
        {
            string command = result;
            string logcmd = command;
            if (logcmd.Contains(Environment.CurrentDirectory))
            {
                logcmd = logcmd.Replace($"{Environment.CurrentDirectory}", $"CurrentDirectory");
            }

            _ = Logger.Log($"Running command: {logcmd}");

            using (var adb = new Process())
            {
                adb.StartInfo.FileName = "cmd.exe";
                adb.StartInfo.RedirectStandardError = true;
                adb.StartInfo.RedirectStandardInput = true;
                adb.StartInfo.RedirectStandardOutput = true;
                adb.StartInfo.CreateNoWindow = true;
                adb.StartInfo.UseShellExecute = false;
                adb.StartInfo.WorkingDirectory = Path.GetDirectoryName(path);
                _ = adb.Start();
                adb.StandardInput.WriteLine(command);
                adb.StandardInput.Flush();
                adb.StandardInput.Close();

                string output = "";
                string error = "";

                try
                {
                    output += adb.StandardOutput.ReadToEnd();
                    error += adb.StandardError.ReadToEnd();
                }
                catch { }

                if (command.Contains("connect"))
                {
                    bool graceful = adb.WaitForExit(3000);
                    if (!graceful)
                    {
                        adb.Kill();
                        adb.WaitForExit();
                    }
                }

                if (error.Contains("ADB_VENDOR_KEYS") && settings.AdbDebugWarned)
                {
                    ADBDebugWarning();
                }

                _ = Logger.Log(output);
                _ = Logger.Log(error, LogLevel.ERROR);
                return new ProcessOutput(output, error);
            }
        }

        public static ProcessOutput RunCommandToString(string result, string path = "")
        {
            string command = result;
            string logcmd = command;
            if (logcmd.Contains(Environment.CurrentDirectory))
            {
                logcmd = logcmd.Replace($"{Environment.CurrentDirectory}", $"CurrentDirectory");
            }

            Logger.Log($"Running command: {logcmd}");

            try
            {
                using (var proc = new Process())
                {
                    proc.StartInfo.FileName = $@"{Path.GetPathRoot(Environment.SystemDirectory)}\Windows\System32\cmd.exe";
                    proc.StartInfo.Arguments = command;
                    proc.StartInfo.RedirectStandardError = true;
                    proc.StartInfo.RedirectStandardInput = true;
                    proc.StartInfo.RedirectStandardOutput = true;
                    proc.StartInfo.CreateNoWindow = true;
                    proc.StartInfo.UseShellExecute = false;
                    proc.StartInfo.WorkingDirectory = Path.GetDirectoryName(path);

                    proc.Start();
                    proc.StandardInput.WriteLine(command);
                    proc.StandardInput.Flush();
                    proc.StandardInput.Close();

                    string output = proc.StandardOutput.ReadToEnd();
                    string error = proc.StandardError.ReadToEnd();

                    if (command.Contains("connect"))
                    {
                        bool graceful = proc.WaitForExit(3000);
                        if (!graceful)
                        {
                            proc.Kill();
                            proc.WaitForExit();
                        }
                    }
                    else
                    {
                        proc.WaitForExit();
                    }

                    if (error.Contains("ADB_VENDOR_KEYS") && settings.AdbDebugWarned)
                    {
                        ADBDebugWarning();
                    }

                    Logger.Log(output);
                    Logger.Log(error, LogLevel.ERROR);

                    return new ProcessOutput(output, error);
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"Error in RunCommandToString: {ex.Message}", LogLevel.ERROR);
                return new ProcessOutput("", $"Exception occurred: {ex.Message}");
            }
        }

        public static void ADBDebugWarning()
        {
            Program.form.Invoke(() =>
            {
                DialogResult dialogResult = FlexibleMessageBox.Show(Program.form,
                    "On your headset, click on the Notifications Bell, and then select the USB Detected notification to enable Connections.",
                    "ADB Debugging not enabled.", MessageBoxButtons.OKCancel);
                if (dialogResult == DialogResult.Cancel)
                {
                    settings.Save();
                }
            });
        }

        public static ProcessOutput UninstallPackage(string package)
        {
            ProcessOutput output = new ProcessOutput("", "");
            output += RunAdbCommandToString($"shell pm uninstall {package}");

            // Prefix the output with the simple game name
            string label = Sideloader.gameNameToSimpleName(Sideloader.PackageNametoGameName(package));

            if (!string.IsNullOrEmpty(output.Output))
            {
                output.Output = $"{label}: {output.Output}";
            }

            if (!string.IsNullOrEmpty(output.Error))
            {
                output.Error = $"{label}: {output.Error}";
            }

            return output;
        }

        public static string GetAvailableSpace()
        {
            long totalSize = 0;
            long usedSize = 0;
            long freeSize = 0;

            string[] output = RunAdbCommandToString("shell df").Output.Split('\n');

            foreach (string currLine in output)
            {
                if (currLine.StartsWith("/dev/fuse") || currLine.StartsWith("/data/media"))
                {
                    string[] foo = currLine.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (foo.Length >= 4)
                    {
                        totalSize = long.Parse(foo[1]) / 1000;
                        usedSize = long.Parse(foo[2]) / 1000;
                        freeSize = long.Parse(foo[3]) / 1000;
                        break;
                    }
                }
            }

            return $"Total space: {string.Format("{0:0.00}", (double)totalSize / 1000)}GB\nUsed space: {string.Format("{0:0.00}", (double)usedSize / 1000)}GB\nFree space: {string.Format("{0:0.00}", (double)freeSize / 1000)}GB";
        }

        public static ProcessOutput Sideload(string path, string packagename = "")
        {
            ProcessOutput ret = new ProcessOutput();
            ret += RunAdbCommandToString($"install -g \"{path}\"");
            string out2 = ret.Output + ret.Error;

            if (out2.Contains("failed"))
            {
                _ = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), $"Rookie Backups");
                _ = Logger.Log(out2);

                if (out2.Contains("offline") && !settings.NodeviceMode)
                {
                    DialogResult dialogResult2 = FlexibleMessageBox.Show(Program.form, "Device is offline. Press Yes to reconnect, or if you don't wish to connect and just want to download the game (requires unchecking \"Delete games after install\" from settings menu) then press No.", "Device offline.", MessageBoxButtons.YesNoCancel);
                }

                if (out2.Contains($"signatures do not match previously") || out2.Contains("INSTALL_FAILED_VERSION_DOWNGRADE") || out2.Contains("signatures do not match") || out2.Contains("failed to install"))
                {
                    ret.Error = string.Empty;
                    ret.Output = string.Empty;

                    bool cancelClicked = false;

                    if (!settings.AutoReinstall)
                    {
                        Program.form.Invoke((MethodInvoker)(() =>
                        {
                            DialogResult dialogResult1 = FlexibleMessageBox.Show(Program.form, "In place upgrade has failed. Rookie can attempt to backup your save data and reinstall the game automatically, however some games do not store their saves in an accessible location (less than 5%). Continue with reinstall?", "In place upgrade failed.", MessageBoxButtons.OKCancel);
                            if (dialogResult1 == DialogResult.Cancel)
                                cancelClicked = true;
                        }));
                    }

                    if (cancelClicked)
                        return ret;

                    Program.form.changeTitle("Performing reinstall, please wait...");
                    _ = RunAdbCommandToString("kill-server");
                    _ = RunAdbCommandToString("devices");
                    _ = RunAdbCommandToString($"pull \"/sdcard/Android/data/{MainForm.CurrPCKG}\" \"{Environment.CurrentDirectory}\"");
                    Program.form.changeTitle("Uninstalling game...");
                    _ = Sideloader.UninstallGame(MainForm.CurrPCKG);
                    Program.form.changeTitle("Reinstalling game...");
                    ret += RunAdbCommandToString($"install -g \"{path}\"");
                    _ = RunAdbCommandToString($"push \"{Environment.CurrentDirectory}\\{MainForm.CurrPCKG}\" /sdcard/Android/data/");

                    string directoryToDelete = Path.Combine(Environment.CurrentDirectory, MainForm.CurrPCKG);
                    if (Directory.Exists(directoryToDelete))
                    {
                        if (directoryToDelete != Environment.CurrentDirectory)
                        {
                            Directory.Delete(directoryToDelete, true);
                        }
                    }

                    Program.form.changeTitle("");
                    return ret;
                }
            }

            Program.form.changeTitle("");
            return ret;
        }

        public static ProcessOutput CopyOBB(string path)
        {
            string folder = Path.GetFileName(path);
            string lastFolder = Path.GetFileName(path);
            return folder.Contains(".")
                ? RunAdbCommandToString($"shell rm -rf \"/sdcard/Android/obb/{lastFolder}\" && mkdir \"/sdcard/Android/obb/{lastFolder}\"") + RunAdbCommandToString($"push \"{path}\" \"/sdcard/Android/obb\"")
                : new ProcessOutput("No OBB Folder found");
        }
    }
}