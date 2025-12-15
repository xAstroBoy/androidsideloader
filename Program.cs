using AndroidSideloader.Utilities;
using System;
using System.IO;
using System.Security.Permissions;
using System.Windows.Forms;

namespace AndroidSideloader
{
    internal static class Program
    {
        private static SettingsManager settings;
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        [SecurityPermission(SecurityAction.Demand, Flags = SecurityPermissionFlag.ControlAppDomain)]
        private static void Main()
        {
            // Handle corrupted user.config files
            bool configFixed = false;
            Exception configException = null;

            try
            {
                // Force settings initialization to trigger any config errors early
                var test = AndroidSideloader.Properties.Settings.Default.FontStyle;
            }
            catch (Exception ex)
            {
                configException = ex;
                // Delete the corrupted config file and retry
                try
                {
                    string configPath = GetUserConfigPath();
                    if (!string.IsNullOrEmpty(configPath) && File.Exists(configPath))
                    {
                        File.Delete(configPath);
                        configFixed = true;
                    }
                }
                catch
                {
                    // If we can't delete it, try to continue anyway
                }
            }

            if (configFixed)
            {
                // Restart the application after fixing config
                Application.Restart();
                return;
            }

            if (configException != null)
            {
                MessageBox.Show(
                    "Settings file is corrupted and could not be repaired automatically.\n\n" +
                    "Please delete this folder and restart the application:\n" +
                    Path.GetDirectoryName(GetUserConfigPath()),
                    "Configuration Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return;
            }

            settings = SettingsManager.Instance;
            AppDomain currentDomain = AppDomain.CurrentDomain;
            currentDomain.UnhandledException += new UnhandledExceptionEventHandler(CrashHandler);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            form = new MainForm();
            Application.Run(form);
            //form.Show();
        }

        private static string GetUserConfigPath()
        {
            try
            {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string companyName = "Rookie.AndroidSideloader";
                string exeName = "AndroidSideloader.exe_Url_dkp0unsd4fjaabhwwafgfxvvbrerf10b";
                string version = "2.0.0.0";
                return Path.Combine(appData, companyName, exeName, version, "user.config");
            }
            catch
            {
                return null;
            }
        }

        public static MainForm form;

        private static void CrashHandler(object sender, UnhandledExceptionEventArgs args)
        {
            // Capture unhandled exceptions and write to file.
            Exception e = (Exception)args.ExceptionObject;
            string innerExceptionMessage = (e.InnerException != null)
                ? e.InnerException.Message
                : "None";
            string date_time = DateTime.Now.ToString("dddd, MMMM dd @ hh:mmtt (UTC)");
            File.WriteAllText(Sideloader.CrashLogPath, $"Date/Time of crash: {date_time}\nMessage: {e.Message}\nInner Message: {innerExceptionMessage}\nData: {e.Data}\nSource: {e.Source}\nTargetSite: {e.TargetSite}\nStack Trace: \n{e.StackTrace}\n\n\nDebuglog: \n\n\n");
            // If a debuglog exists we append it to the crashlog.
            if (settings != null && File.Exists(settings.CurrentLogPath))
            {
                File.AppendAllText(Sideloader.CrashLogPath, File.ReadAllText($"{settings.CurrentLogPath}"));
            }
        }
    }
}