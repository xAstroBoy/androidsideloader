using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace AndroidSideloader
{
    public partial class DonorsListViewForm : Form
    {

        private bool mouseDown;
        private Point lastLocation;

        public DonorsListViewForm()
        {
            InitializeComponent();
            Donors.initDonorGames();

            var seen = new HashSet<string>();
            var DGameList = new List<ListViewItem>();

            foreach (string[] release in Donors.donorGames)
            {
                if (release.Length == 0) continue;
                string key = release[0];
                if (seen.Add(key))
                {
                    DGameList.Add(new ListViewItem(release));
                }
            }

            ListViewItem[] arr = DGameList.ToArray();
            DonorsListView.BeginUpdate();
            DonorsListView.Items.Clear();
            DonorsListView.Items.AddRange(arr);
            DonorsListView.EndUpdate();
        }

        public static string DonorsLocal = MainForm.donorApps;
        public static bool ifuploads = false;
        public static string newAppsForList = "";


        private void DonorsListViewForm_Load(object sender, EventArgs e)
        {
            MainForm.updatesNotified = true;
            if (MainForm.updates && MainForm.newapps)
            {
                bothdet.Visible = true;
            }
            else if (MainForm.updates && !MainForm.newapps)
            {
                upddet.Visible = true;
            }
            else
            {
                newdet.Visible = true;
            }

            foreach (ListViewItem listItem in DonorsListView.Items)
            {
                if (listItem.SubItems[Donors.UpdateOrNew].Text.Contains("Update"))
                {
                    listItem.BackColor = Color.FromArgb(0, 79, 97);
                }
            }

        }

        private async void DonateButton_Click(object sender, EventArgs e)
        {
            if (DonorsListView.CheckedItems.Count > 0)
            {
                bool uncheckednewapps = false;
                foreach (ListViewItem listItem in DonorsListView.Items)
                {
                    if (!listItem.Checked)
                    {
                        if (listItem.SubItems[Donors.UpdateOrNew].Text.Contains("New"))
                        {
                            uncheckednewapps = true;
                            newAppsForList += listItem.SubItems[Donors.GameNameIndex].Text + ";" + listItem.SubItems[Donors.PackageNameIndex].Text + "\n";
                        }
                    }
                }
                if (uncheckednewapps)
                {

                    NewApps NewAppForm = new NewApps();
                    _ = NewAppForm.ShowDialog();
                    Hide();
                }
                else
                {
                    Hide();
                }
                int count = DonorsListView.CheckedItems.Count;
                _ = new string[count];
                for (int i = 0; i < count; i++)
                {
                    ulong vcode = Convert.ToUInt64(DonorsListView.CheckedItems[i].SubItems[Donors.VersionCodeIndex].Text);
                    if (DonorsListView.CheckedItems[i].SubItems[Donors.UpdateOrNew].Text.Contains("Update"))
                    {
                        await Program.form.extractAndPrepareGameToUploadAsync(DonorsListView.CheckedItems[i].SubItems[Donors.GameNameIndex].Text, DonorsListView.CheckedItems[i].SubItems[Donors.PackageNameIndex].Text, vcode, true);
                    }
                    else
                    {
                        await Program.form.extractAndPrepareGameToUploadAsync(DonorsListView.CheckedItems[i].SubItems[Donors.GameNameIndex].Text, DonorsListView.CheckedItems[i].SubItems[Donors.PackageNameIndex].Text, vcode, false);
                    }

                    ifuploads = true;
                }
            }

            if (ifuploads)
            {
                MainForm.doUpload();
            }
            Close();
        }

        private void DonorsListView_ItemChecked(object sender, ItemCheckedEventArgs e)
        {
            SkipButton.Enabled = DonorsListView.CheckedItems.Count == 0;
            DonateButton.Enabled = !SkipButton.Enabled;

            // Enable skip_forever button only when items are checked
            skip_forever.Enabled = DonorsListView.CheckedItems.Count > 0;
        }

        private void SkipButton_Click(object sender, EventArgs e)
        {
            bool uncheckednewapps = false;
            foreach (ListViewItem listItem in DonorsListView.Items)
            {
                if (!listItem.Checked)
                {
                    if (listItem.SubItems[Donors.UpdateOrNew].Text.Contains("New"))
                    {
                        uncheckednewapps = true;
                        newAppsForList += listItem.SubItems[Donors.GameNameIndex].Text + ";" + listItem.SubItems[Donors.PackageNameIndex].Text + "\n";
                    }
                }
            }
            if (uncheckednewapps)
            {
                NewApps NewAppForm = new NewApps();
                _ = NewAppForm.ShowDialog();
            }
            Close();
        }

        private void DonorsListViewForm_MouseDown(object sender, MouseEventArgs e)
        {
            mouseDown = true;
            lastLocation = e.Location;
        }

        private void DonorsListViewForm_MouseMove(object sender, MouseEventArgs e)
        {
            if (mouseDown)
            {
                Location = new Point(
                    Location.X - lastLocation.X + e.X, Location.Y - lastLocation.Y + e.Y);
                Update();
            }
        }

        private void DonorsListViewForm_MouseUp(object sender, MouseEventArgs e)
        {
            mouseDown = false;
        }

        private void skip_forever_Click(object sender, EventArgs e)
        {
            // Collect selected items from the list
            List<string> appsToBlacklist = new List<string>();

            foreach (ListViewItem listItem in DonorsListView.CheckedItems)
            {
                // Get the package name from the checked list item
                string packageName = listItem.SubItems[Donors.PackageNameIndex].Text;
                appsToBlacklist.Add(packageName);
            }

            if (appsToBlacklist.Count == 0)
            {
                MessageBox.Show("No apps selected to blacklist.", "Info", MessageBoxButtons.OK);
                return;
            }

            // Confirm with user
            DialogResult result = MessageBox.Show(
                $"Are you sure you want to permanently skip donation requests for {appsToBlacklist.Count} selected app(s)?\n\nThese apps will not be requested for donation again.",
                "Confirm Blacklist",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (result != DialogResult.Yes)
            {
                return;
            }

            // Path to local blacklist.json in the main directory
            string blacklistPath = Path.Combine(Environment.CurrentDirectory, "blacklist.json");

            try
            {
                // Read existing blacklist entries if file exists
                HashSet<string> existingBlacklist = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                if (File.Exists(blacklistPath))
                {
                    try
                    {
                        string jsonContent = File.ReadAllText(blacklistPath);
                        // Try to parse as JSON array
                        var jsonArray = Newtonsoft.Json.JsonConvert.DeserializeObject<string[]>(jsonContent);
                        if (jsonArray != null)
                        {
                            foreach (string entry in jsonArray)
                            {
                                if (!string.IsNullOrWhiteSpace(entry))
                                {
                                    existingBlacklist.Add(entry.Trim());
                                }
                            }
                        }
                    }
                    catch
                    {
                        // If JSON parsing fails, file might be corrupted, start fresh
                        Logger.Log("Existing blacklist.json is corrupted, creating new file", LogLevel.WARNING);
                    }
                }

                // Add new package names to blacklist
                foreach (string packageName in appsToBlacklist)
                {
                    existingBlacklist.Add(packageName);
                }

                // Write back to file as JSON array
                string jsonOutput = Newtonsoft.Json.JsonConvert.SerializeObject(existingBlacklist.ToArray(), Newtonsoft.Json.Formatting.Indented);
                File.WriteAllText(blacklistPath, jsonOutput);

                Logger.Log($"Added {appsToBlacklist.Count} apps to local blacklist");

                MessageBox.Show(
                    $"{appsToBlacklist.Count} app(s) have been added to the blacklist.\n\nYou will not be asked to donate these apps again.",
                    "Success",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);

                // Close the form
                Close();
            }
            catch (Exception ex)
            {
                Logger.Log($"Error saving blacklist: {ex.Message}", LogLevel.ERROR);
                MessageBox.Show(
                    $"Error saving blacklist: {ex.Message}",
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }
    }
}
