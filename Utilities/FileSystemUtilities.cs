using System;
using System.IO;
using System.Threading;

namespace AndroidSideloader.Utilities
{
    internal static class FileSystemUtilities
    {
        public static bool TryDeleteDirectory(string directoryPath, int maxRetries = 3, int delayMs = 150) // 3x 150ms = 450ms total
        {
            if (string.IsNullOrWhiteSpace(directoryPath))
                return true;

            if (!Directory.Exists(directoryPath))
                return true;

            Exception lastError = null;

            // Retry deletion several times in case of lock ups
            for (int attempt = 0; attempt <= maxRetries; attempt++)
            {
                try
                {
                    StripReadOnlyAttributes(directoryPath);
                    Directory.Delete(directoryPath, true);
                    return true;
                }
                catch (DirectoryNotFoundException)
                {
                    return true;
                }
                catch (Exception ex) when (ex is UnauthorizedAccessException || ex is IOException)
                {
                    lastError = ex;

                    if (attempt < maxRetries)
                    {
                        Thread.Sleep(delayMs);
                        continue;
                    }

                    break;
                }
                catch (Exception ex)
                {
                    // Non-retryable error
                    lastError = ex;
                    break;
                }
            }

            // Last resort: rename then delete
            try
            {
                string renamedPath = directoryPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                    + ".deleting." + DateTime.UtcNow.Ticks;

                Directory.Move(directoryPath, renamedPath);

                StripReadOnlyAttributes(renamedPath);
                Directory.Delete(renamedPath, true);
                return true;
            }
            catch (Exception ex)
            {
                lastError = ex;
            }

            Logger.Log($"Failed to delete directory: {directoryPath}. Error: {lastError}", LogLevel.WARNING);
            return false;
        }

        private static void StripReadOnlyAttributes(string directoryPath)
        {
            var root = new DirectoryInfo(directoryPath);
            if (!root.Exists) return;

            root.Attributes &= ~FileAttributes.ReadOnly;

            foreach (var dir in root.EnumerateDirectories("*", SearchOption.AllDirectories))
            {
                dir.Attributes &= ~FileAttributes.ReadOnly;
            }

            foreach (var file in root.EnumerateFiles("*", SearchOption.AllDirectories))
            {
                file.Attributes &= ~FileAttributes.ReadOnly;
            }
        }
    }
}