using AndroidSideloader.Utilities;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Security.Cryptography;

namespace AndroidSideloader
{
    internal class SideloaderRCLONE
    {
        public static List<string> RemotesList = new List<string>();

        public static string RcloneGamesFolder = "Quest Games";

        public static int GameNameIndex = 0;
        public static int ReleaseNameIndex = 1;
        public static int PackageNameIndex = 2;
        public static int VersionCodeIndex = 3;
        public static int ReleaseAPKPathIndex = 4;
        public static int VersionNameIndex = 5;
        public static int DownloadsIndex = 6;
        public static int InstalledVersion = 7;

        public static List<string> gameProperties = new List<string>();
        /* Game Name
         * Release Name
         * Release APK Path
         * Package Name
         * Version Code
         * Version Name
         */
        public static List<string[]> games = new List<string[]>();

        public static string Nouns = Path.Combine(Environment.CurrentDirectory, "nouns");
        public static string ThumbnailsFolder = Path.Combine(Environment.CurrentDirectory, "thumbnails");
        public static string NotesFolder = Path.Combine(Environment.CurrentDirectory, "notes");

        public static void UpdateNouns(string remote)
        {
            _ = Logger.Log($"Updating Nouns");
            _ = RCLONE.runRcloneCommand_DownloadConfig($"sync \"{remote}:{RcloneGamesFolder}/.meta/nouns\" \"{Nouns}\"");
        }

        public static void UpdateGamePhotos(string remote)
        {
            _ = Logger.Log($"Updating Thumbnails");
            _ = RCLONE.runRcloneCommand_DownloadConfig($"sync \"{remote}:{RcloneGamesFolder}/.meta/thumbnails\" \"{ThumbnailsFolder}\" --transfers 10");
        }

        public static void UpdateGameNotes(string remote)
        {
            _ = Logger.Log($"Updating Game Notes");
            _ = RCLONE.runRcloneCommand_DownloadConfig($"sync \"{remote}:{RcloneGamesFolder}/.meta/notes\" \"{NotesFolder}\"");
        }

        public static void UpdateMetadataFromPublic()
        {
            _ = Logger.Log($"Downloading Metadata");
            string rclonecommand =
                $"sync \":http:/meta.7z\" \"{Environment.CurrentDirectory}\"";
            _ = RCLONE.runRcloneCommand_PublicConfig(rclonecommand);
        }

        public static void ProcessMetadataFromPublic()
        {
            try
            {
                var sw = Stopwatch.StartNew();

                string currentDir = Environment.CurrentDirectory;
                string metaRoot = Path.Combine(currentDir, "meta");
                string metaArchive = Path.Combine(currentDir, "meta.7z");
                string metaDotMeta = Path.Combine(metaRoot, ".meta");

                // Check if archive exists and is newer than existing metadata
                if (!File.Exists(metaArchive))
                {
                    Logger.Log("meta.7z not found, skipping extraction", LogLevel.WARNING);
                    return;
                }

                // Skip extraction if metadata is already up-to-date (based on file timestamps)
                string gameListPath = Path.Combine(metaRoot, "VRP-GameList.txt");
                if (File.Exists(gameListPath))
                {
                    var archiveTime = File.GetLastWriteTimeUtc(metaArchive);
                    var gameListTime = File.GetLastWriteTimeUtc(gameListPath);

                    // If game list is newer than archive, skip extraction
                    if (gameListTime > archiveTime && games.Count > 0)
                    {
                        Logger.Log($"Metadata already up-to-date, skipping extraction");
                        return;
                    }
                }

                _ = Logger.Log($"Extracting Metadata");
                Zip.ExtractFile(metaArchive, metaRoot, MainForm.PublicConfigFile.Password);
                Logger.Log($"Extraction completed in {sw.ElapsedMilliseconds}ms");
                sw.Restart();

                _ = Logger.Log($"Updating Metadata");

                // Use Parallel.Invoke for independent directory operations
                System.Threading.Tasks.Parallel.Invoke(
                    () => SafeDeleteDirectory(Nouns),
                    () => SafeDeleteDirectory(ThumbnailsFolder),
                    () => SafeDeleteDirectory(NotesFolder)
                );
                Logger.Log($"Directory cleanup in {sw.ElapsedMilliseconds}ms");
                sw.Restart();

                // Move directories
                MoveIfExists(Path.Combine(metaDotMeta, "nouns"), Nouns);
                MoveIfExists(Path.Combine(metaDotMeta, "thumbnails"), ThumbnailsFolder);
                MoveIfExists(Path.Combine(metaDotMeta, "notes"), NotesFolder);
                Logger.Log($"Directory moves in {sw.ElapsedMilliseconds}ms");
                sw.Restart();

                _ = Logger.Log($"Initializing Games List");

                gameListPath = Path.Combine(metaRoot, "VRP-GameList.txt");
                if (File.Exists(gameListPath))
                {
                    // Read all lines at once - faster for files that fit in memory
                    var lines = File.ReadAllLines(gameListPath);
                    var newGames = new List<string[]>(lines.Length);

                    for (int i = 1; i < lines.Length; i++) // Skip header
                    {
                        var line = lines[i];
                        if (string.IsNullOrWhiteSpace(line))
                            continue;

                        var splitGame = line.Split(';');
                        if (splitGame.Length > 1)
                        {
                            newGames.Add(splitGame);
                        }
                    }

                    // Atomic swap
                    games.Clear();
                    games.AddRange(newGames);
                    Logger.Log($"Parsed {games.Count} games in {sw.ElapsedMilliseconds}ms");
                }
                else
                {
                    _ = Logger.Log("VRP-GameList.txt not found in extracted metadata.", LogLevel.WARNING);
                }

                SafeDeleteDirectory(metaRoot);
            }
            catch (Exception e)
            {
                _ = Logger.Log(e.Message);
                _ = Logger.Log(e.StackTrace);
            }
        }

        public static void initGames(string remote)
        {
            _ = Logger.Log($"Initializing Games List");

            gameProperties.Clear();
            games.Clear();

            // Fetch once, then process as lines
            string tempGameList = RCLONE.runRcloneCommand_DownloadConfig($"cat \"{remote}:{RcloneGamesFolder}/VRP-GameList.txt\"").Output;
            if (MainForm.debugMode)
            {
                // Avoid redundant disk I/O: write only if non-empty
                if (!string.IsNullOrEmpty(tempGameList))
                {
                    File.WriteAllText("VRP-GamesList.txt", tempGameList);
                }
            }

            if (!string.IsNullOrEmpty(tempGameList))
            {
                bool isFirstLine = true;
                foreach (var line in SplitLines(tempGameList))
                {
                    if (isFirstLine)
                    {
                        isFirstLine = false; // skip header
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(line))
                    {
                        continue;
                    }

                    var splitGame = line.Split(new[] { ';' }, StringSplitOptions.None);
                    if (splitGame.Length > 1)
                    {
                        games.Add(splitGame);
                    }
                }
            }
        }

        public static void updateUploadConfig()
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls
                                                 | SecurityProtocolType.Tls11
                                                 | SecurityProtocolType.Tls12
                                                 | SecurityProtocolType.Ssl3;
            _ = Logger.Log($"Attempting to Update Upload Config");
            try
            {
                string configUrl = "https://vrpirates.wiki/downloads/vrp.upload.config";

                // Use DnsHelper for fallback DNS support
                var getUrl = DnsHelper.CreateWebRequest(configUrl);
                using (var response = getUrl.GetResponse())
                using (var stream = response.GetResponseStream())
                using (var responseReader = new StreamReader(stream))
                {
                    string resultString = responseReader.ReadToEnd();

                    _ = Logger.Log($"Retrieved updated config from: {configUrl}");

                    // Avoid multiple combines; write once
                    string uploadConfigPath = Path.Combine(Environment.CurrentDirectory, "rclone", "vrp.upload.config");
                    File.WriteAllText(uploadConfigPath, resultString);

                    _ = Logger.Log("Upload config updated successfully.");
                }
            }
            catch (Exception e)
            {
                _ = Logger.Log($"Failed to update Upload config: {e.Message}", LogLevel.ERROR);
            }
        }

        // Fast directory delete using Windows cmd - faster than .NET's Directory.Delete
        // for large directories with many files (e.g., thumbnails folder with 1000+ images)
        private static void SafeDeleteDirectory(string path)
        {
            // Avoid exceptions when directory is missing
            if (!Directory.Exists(path))
                return;

            try
            {
                // Use Windows rd command which is ~10x faster than .NET's recursive delete
                var psi = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c rd /s /q \"{path}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using (var process = Process.Start(psi))
                {
                    // Wait with timeout to prevent hanging
                    if (!process.WaitForExit(30000)) // 30 second timeout
                    {
                        try { process.Kill(); } catch { }
                        Logger.Log($"Directory delete timed out for: {path}", LogLevel.WARNING);
                        // Fallback to .NET delete
                        FallbackDelete(path);
                    }
                    else if (process.ExitCode != 0 && Directory.Exists(path))
                    {
                        // Command failed, try fallback
                        FallbackDelete(path);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"Fast delete failed for {path}: {ex.Message}", LogLevel.WARNING);
                // Fallback to standard .NET delete
                FallbackDelete(path);
            }
        }

        // Fallback delete method using standard .NET
        private static void FallbackDelete(string path)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    Directory.Delete(path, true);
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"Fallback delete also failed for {path}: {ex.Message}", LogLevel.ERROR);
            }
        }

        // Move directory only if source exists
        private static void MoveIfExists(string sourceDir, string destDir)
        {
            if (Directory.Exists(sourceDir))
            {
                // Ensure destination does not exist to prevent IOException
                // Use fast delete method
                SafeDeleteDirectory(destDir);
                Directory.Move(sourceDir, destDir);
            }
            else
            {
                _ = Logger.Log($"Source directory not found: {sourceDir}", LogLevel.WARNING);
            }
        }

        // Efficient, cross-platform line splitting for string buffers
        private static IEnumerable<string> SplitLines(string s)
        {
            // Handle both \r\n and \n without allocating intermediate arrays
            using (var reader = new StringReader(s))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    yield return line;
                }
            }
        }
    }
}