using AndroidSideloader.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Security.Cryptography;

namespace AndroidSideloader
{
    internal class SideloaderRCLONE
    {
        public static List<string> RemotesList = new List<string>();

        public static string RcloneGamesFolder = "Quest Games";

        //This shit sucks but i'll switch to programatically adding indexes from the gamelist txt sometimes maybe

        public static int GameNameIndex = 0;
        public static int ReleaseNameIndex = 1;
        public static int PackageNameIndex = 2;
        public static int VersionCodeIndex = 3;
        public static int ReleaseAPKPathIndex = 4;
        public static int VersionNameIndex = 5;
        public static int DownloadsIndex = 6;

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
                _ = Logger.Log($"Extracting Metadata");

                // Cache commonly used paths to avoid repeated Path.Combine calls
                string currentDir = Environment.CurrentDirectory;
                string metaRoot = Path.Combine(currentDir, "meta");
                string metaArchive = Path.Combine(currentDir, "meta.7z");
                string metaDotMeta = Path.Combine(metaRoot, ".meta");

                Zip.ExtractFile(metaArchive, metaRoot, MainForm.PublicConfigFile.Password);

                _ = Logger.Log($"Updating Metadata");

                // Use a fast directory reset: delete if exists, then move (avoids partial state)
                SafeDeleteDirectory(Nouns);
                SafeDeleteDirectory(ThumbnailsFolder);
                SafeDeleteDirectory(NotesFolder);

                // Avoid throwing if source folders are missing
                MoveIfExists(Path.Combine(metaDotMeta, "nouns"), Nouns);
                MoveIfExists(Path.Combine(metaDotMeta, "thumbnails"), ThumbnailsFolder);
                MoveIfExists(Path.Combine(metaDotMeta, "notes"), NotesFolder);

                _ = Logger.Log($"Initializing Games List");

                // Stream the file line-by-line instead of reading the whole file into memory
                string gameListPath = Path.Combine(metaRoot, "VRP-GameList.txt");
                if (File.Exists(gameListPath))
                {
                    games.Clear();
                    bool isFirstLine = true;
                    foreach (var line in File.ReadLines(gameListPath))
                    {
                        // Skip header line only once
                        if (isFirstLine)
                        {
                            isFirstLine = false;
                            continue;
                        }

                        // Skip empty/whitespace lines without allocating split arrays
                        if (string.IsNullOrWhiteSpace(line))
                        {
                            continue;
                        }

                        // Split with RemoveEmptyEntries to avoid trailing empty fields
                        var splitGame = line.Split(new[] { ';' }, StringSplitOptions.None);
                        // Minimal validation: require at least 2 fields
                        if (splitGame.Length > 1)
                        {
                            games.Add(splitGame);
                        }
                    }
                }
                else
                {
                    _ = Logger.Log("VRP-GameList.txt not found in extracted metadata.", LogLevel.WARNING);
                }

                // Delete meta folder at the end to avoid leaving partial state if something fails earlier
                SafeDeleteDirectory(metaRoot);
            }
            catch (Exception e)
            {
                _ = Logger.Log(e.Message);
                _ = Logger.Log(e.StackTrace);
            }
        }

        public static void RefreshRemotes()
        {
            _ = Logger.Log($"Refresh / List Remotes");
            RemotesList.Clear();

            // Avoid unnecessary ToArray; directly iterate lines
            var output = RCLONE.runRcloneCommand_DownloadConfig("listremotes").Output;
            if (string.IsNullOrEmpty(output))
            {
                _ = Logger.Log("No remotes returned from rclone.");
                return;
            }

            _ = Logger.Log("Loaded following remotes: ");
            foreach (var r in SplitLines(output))
            {
                if (r.Length <= 1)
                {
                    continue;
                }

                // Trim whitespace and trailing colon if present
                var remote = r.TrimEnd();
                if (remote.EndsWith(":"))
                {
                    remote = remote.Substring(0, remote.Length - 1);
                }

                if (remote.IndexOf("mirror", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    _ = Logger.Log(remote);
                    RemotesList.Add(remote);
                }
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

                var getUrl = (HttpWebRequest)WebRequest.Create(configUrl);
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

        // Robust directory delete without throwing if not present
        private static void SafeDeleteDirectory(string path)
        {
            // Avoid exceptions when directory is missing
            if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }
        }

        // Move directory only if source exists
        private static void MoveIfExists(string sourceDir, string destDir)
        {
            if (Directory.Exists(sourceDir))
            {
                // Ensure destination does not exist to prevent IOException
                if (Directory.Exists(destDir))
                {
                    Directory.Delete(destDir, true);
                }
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