using AndroidSideloader.Utilities;
using System;
using System.IO;
using System.Windows.Forms;

namespace AndroidSideloader
{
    public partial class QuestForm : Form
    {
        private static readonly SettingsManager settings = SettingsManager.Instance;
        public static int length = 0;
        public static string[] result;
        public bool settingsexist = false;
        private bool delsh = false;

        public QuestForm()
        {
            InitializeComponent();
        }

        private void btnApplyTempSettings_Click(object sender, EventArgs e)
        {
            bool ChangesMade = false;

            if (RefreshRateComboBox.SelectedIndex != -1)
            {
                string refreshRate = RefreshRateComboBox.SelectedItem.ToString().Replace(" Hz", "");
                _ = ADB.RunAdbCommandToString($"shell setprop debug.oculus.refreshRate {refreshRate}");
                _ = ADB.RunAdbCommandToString($"shell settings put global 90hz_global {RefreshRateComboBox.SelectedIndex}");
                _ = ADB.RunAdbCommandToString($"shell settings put global 90hzglobal {RefreshRateComboBox.SelectedIndex}");
                ChangesMade = true;
            }

            if (TextureResTextBox.Text.Length > 0 && TextureResTextBox.Text != "0")
            {
                if (int.TryParse(TextureResTextBox.Text, out _))
                {
                    _ = ADB.RunAdbCommandToString($"shell settings put global texture_size_Global {TextureResTextBox.Text}");
                    _ = ADB.RunAdbCommandToString($"shell setprop debug.oculus.textureWidth {TextureResTextBox.Text}");
                    _ = ADB.RunAdbCommandToString($"shell setprop debug.oculus.textureHeight {TextureResTextBox.Text}");
                    ChangesMade = true;
                }
            }

            if (CPUComboBox.SelectedIndex != -1)
            {
                _ = ADB.RunAdbCommandToString($"shell setprop debug.oculus.cpuLevel {CPUComboBox.SelectedIndex}");
                ChangesMade = true;
            }

            if (GPUComboBox.SelectedIndex != -1)
            {
                _ = ADB.RunAdbCommandToString($"shell setprop debug.oculus.gpuLevel {GPUComboBox.SelectedIndex}");
                ChangesMade = true;
            }

            if (ChangesMade)
            {
                _ = MessageBox.Show("Settings applied!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        public static void setLength(int value)
        {
            result = new string[value];
        }

        private void toggleDeleteAfterTransfer_CheckedChanged(object sender, EventArgs e)
        {
            delsh = toggleDeleteAfterTransfer.Checked;
        }

        private static readonly Random random = new Random();
        private static readonly object syncLock = new object();

        public static int RandomNumber(int min, int max)
        {
            lock (syncLock)
            {
                return random.Next(min, max);
            }
        }

        private void QuestForm_FormClosed(object sender, FormClosedEventArgs e)
        {
            settings.Delsh = toggleDeleteAfterTransfer.Checked;
            settings.Save();
        }

        private void QuestForm_Load(object sender, EventArgs e)
        {
            CenterToParent();
            toggleDeleteAfterTransfer.SetCheckedSilent(settings.Delsh);
            delsh = settings.Delsh;
            GlobalUsername.Text = settings.GlobalUsername;
        }

        private void questPics_Click(object sender, EventArgs e)
        {
            string path = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);

            if (!Directory.Exists($"{path}\\Quest Screenshots"))
            {
                _ = Directory.CreateDirectory($"{path}\\Quest Screenshots");
            }

            _ = MessageBox.Show("Please wait until you get the message that the transfer has finished.",
                "Transfer Starting", MessageBoxButtons.OK, MessageBoxIcon.Information);

            Program.form.changeTitle("Pulling files...");
            _ = ADB.RunAdbCommandToString($"pull \"/sdcard/Oculus/Screenshots\" \"{path}\\Quest Screenshots\"");

            if (delsh)
            {
                DialogResult dialogResult = MessageBox.Show(
                    "You have chosen to delete files from headset after transferring.\n\nMake sure to move them from your desktop to somewhere safe!",
                    "Warning", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning);

                if (dialogResult == DialogResult.OK)
                {
                    _ = ADB.RunAdbCommandToString("shell rm -r /sdcard/Oculus/Screenshots");
                    _ = ADB.RunAdbCommandToString("shell mkdir /sdcard/Oculus/Screenshots");
                }
            }

            _ = MessageBox.Show("Transfer finished!\n\nScreenshots can be found in:\nDesktop\\Quest Screenshots",
                "Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
            Program.form.changeTitle("Done!");
        }

        private void questVids_Click(object sender, EventArgs e)
        {
            string path = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);

            if (!Directory.Exists($"{path}\\Quest Recordings"))
            {
                _ = Directory.CreateDirectory($"{path}\\Quest Recordings");
            }

            _ = MessageBox.Show("Please wait until you get the message that the transfer has finished.",
                "Transfer Starting", MessageBoxButtons.OK, MessageBoxIcon.Information);

            Program.form.changeTitle("Pulling files...");
            _ = ADB.RunAdbCommandToString($"pull \"/sdcard/Oculus/Videoshots\" \"{path}\\Quest Recordings\"");

            if (delsh)
            {
                DialogResult dialogResult = MessageBox.Show(
                    "You have chosen to delete files from headset after transferring.\n\nMake sure to move them from your desktop to somewhere safe!",
                    "Warning", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning);

                if (dialogResult == DialogResult.OK)
                {
                    _ = ADB.RunAdbCommandToString("shell rm -r /sdcard/Oculus/Videoshots");
                    _ = ADB.RunAdbCommandToString("shell mkdir /sdcard/Oculus/Videoshots");
                }
            }

            _ = MessageBox.Show("Transfer finished!\n\nRecordings can be found in:\nDesktop\\Quest Recordings",
                "Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
            Program.form.changeTitle("Done!");
        }

        private void btnApplyUsername_Click(object sender, EventArgs e)
        {
            _ = ADB.RunAdbCommandToString($"shell settings put global username {GlobalUsername.Text}");
            _ = MessageBox.Show($"Username set to: {GlobalUsername.Text}", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void btnClose_Click(object sender, EventArgs e)
        {
            Close();
        }

        protected override bool ProcessDialogKey(Keys keyData)
        {
            if (Form.ModifierKeys == Keys.None && keyData == Keys.Escape)
            {
                Close();
                return true;
            }
            return base.ProcessDialogKey(keyData);
        }

        private void GlobalUsername_TextChanged(object sender, EventArgs e)
        {
            btnApplyUsername.Enabled = GlobalUsername.TextLength > 0;
            btnApplyUsername.ForeColor = System.Drawing.Color.FromArgb(
                ((int)(((byte)(btnApplyUsername.Enabled ? 30 : 80)))), 
                ((int)(((byte)(btnApplyUsername.Enabled ? 24 : 80)))), 
                ((int)(((byte)(btnApplyUsername.Enabled ? 29 : 80)))));

            settings.GlobalUsername = GlobalUsername.Text;
        }
    }
}