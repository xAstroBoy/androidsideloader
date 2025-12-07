using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace AndroidSideloader
{
    public partial class DonorsListViewForm : Form
    {
        // Modern theme colors
        private static readonly Color BackgroundColor = Color.FromArgb(20, 24, 29);
        private static readonly Color BorderColor = Color.FromArgb(70, 80, 100);
        private static readonly Color UpdateHighlightColor = Color.FromArgb(0, 79, 97);

        // Shadow and corner settings
        private const int CS_DROPSHADOW = 0x00020000;
        private const int WM_NCLBUTTONDOWN = 0xA1;
        private const int HT_CAPTION = 0x2;
        private const int SHADOW_SIZE = 2;
        private const int CONTENT_RADIUS = 10;

        [DllImport("user32.dll")]
        private static extern int SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ClassStyle |= CS_DROPSHADOW;
                return cp;
            }
        }

        public DonorsListViewForm()
        {
            InitializeComponent();
            ApplyModernTheme();
            CenterToScreen();

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

        private void ApplyModernTheme()
        {
            this.FormBorderStyle = FormBorderStyle.None;
            this.BackColor = Color.FromArgb(25, 25, 30);
            this.Padding = new Padding(5);

            panel1.BackColor = BackgroundColor;
            panel1.Location = new Point(6, 6);
            panel1.Size = new Size(this.ClientSize.Width - 12, this.ClientSize.Height - 12);
            panel1.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;

            this.Paint += Form_Paint;

            // Close button
            var closeButton = new Button
            {
                Text = "✕",
                Font = new Font("Segoe UI", 9F),
                ForeColor = Color.White,
                BackColor = BackgroundColor,
                FlatStyle = FlatStyle.Flat,
                Size = new Size(30, 28),
                Location = new Point(panel1.Width - 35, 5),
                Cursor = Cursors.Hand,
                TabStop = false
            };
            closeButton.FlatAppearance.BorderSize = 0;
            closeButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(200, 60, 60);
            closeButton.Click += (s, e) => Close();
            panel1.Controls.Add(closeButton);
            closeButton.BringToFront();

            // Enable dragging
            panel1.MouseDown += TitleArea_MouseDown;
            foreach (Control ctrl in panel1.Controls)
            {
                if (ctrl is Label)
                {
                    ctrl.MouseDown += TitleArea_MouseDown;
                }
            }
        }

        private void TitleArea_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                ReleaseCapture();
                SendMessage(this.Handle, WM_NCLBUTTONDOWN, (IntPtr)HT_CAPTION, IntPtr.Zero);
            }
        }

        private void Form_Paint(object sender, PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

            int w = this.Width;
            int h = this.Height;

            // Draw shadow layers
            for (int i = SHADOW_SIZE; i >= 1; i--)
            {
                int alpha = (SHADOW_SIZE - i + 1) * 12;
                Rectangle shadowRect = new Rectangle(
                    SHADOW_SIZE - i,
                    SHADOW_SIZE - i,
                    w - (SHADOW_SIZE - i) * 2 - 1,
                    h - (SHADOW_SIZE - i) * 2 - 1);

                using (Pen shadowPen = new Pen(Color.FromArgb(alpha, 0, 0, 0), 1))
                using (GraphicsPath shadowPath = CreateRoundedRectPath(shadowRect, CONTENT_RADIUS + i))
                {
                    e.Graphics.DrawPath(shadowPen, shadowPath);
                }
            }

            // Draw content background
            Rectangle contentRect = new Rectangle(SHADOW_SIZE, SHADOW_SIZE, w - SHADOW_SIZE * 2, h - SHADOW_SIZE * 2);
            using (GraphicsPath contentPath = CreateRoundedRectPath(contentRect, CONTENT_RADIUS))
            {
                using (SolidBrush bgBrush = new SolidBrush(BackgroundColor))
                {
                    e.Graphics.FillPath(bgBrush, contentPath);
                }

                using (Pen borderPen = new Pen(BorderColor, 1f))
                {
                    e.Graphics.DrawPath(borderPen, contentPath);
                }
            }

            // Apply rounded region
            using (GraphicsPath regionPath = CreateRoundedRectPath(new Rectangle(0, 0, w, h), CONTENT_RADIUS + SHADOW_SIZE))
            {
                this.Region = new Region(regionPath);
            }
        }

        private GraphicsPath CreateRoundedRectPath(Rectangle rect, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            if (radius <= 0)
            {
                path.AddRectangle(rect);
                return path;
            }

            int diameter = Math.Min(radius * 2, Math.Min(rect.Width, rect.Height));
            radius = diameter / 2;
            Rectangle arcRect = new Rectangle(rect.Location, new Size(diameter, diameter));

            path.AddArc(arcRect, 180, 90);
            arcRect.X = rect.Right - diameter;
            path.AddArc(arcRect, 270, 90);
            arcRect.Y = rect.Bottom - diameter;
            path.AddArc(arcRect, 0, 90);
            arcRect.X = rect.Left;
            path.AddArc(arcRect, 90, 90);
            path.CloseFigure();
            return path;
        }

        public static string DonorsLocal = MainForm.donorApps;
        public static bool ifuploads = false;
        public static string newAppsForList = "";

        private void DonorsListViewForm_Load(object sender, EventArgs e)
        {
            MainForm.updatesNotified = true;
            bothdet.Visible = MainForm.updates && MainForm.newapps;
            upddet.Visible = MainForm.updates && !MainForm.newapps;
            newdet.Visible = !MainForm.updates;

            foreach (ListViewItem listItem in DonorsListView.Items)
            {
                if (listItem.SubItems[Donors.UpdateOrNew].Text.Contains("Update"))
                    listItem.BackColor = UpdateHighlightColor;
            }
        }

        private async void DonateButton_Click(object sender, EventArgs e)
        {
            if (DonorsListView.CheckedItems.Count > 0)
            {
                bool uncheckednewapps = false;
                foreach (ListViewItem listItem in DonorsListView.Items)
                {
                    if (!listItem.Checked && listItem.SubItems[Donors.UpdateOrNew].Text.Contains("New"))
                    {
                        uncheckednewapps = true;
                        newAppsForList += listItem.SubItems[Donors.GameNameIndex].Text + ";" + listItem.SubItems[Donors.PackageNameIndex].Text + "\n";
                    }
                }

                if (uncheckednewapps)
                {
                    new NewApps().ShowDialog();
                    Hide();
                }
                else
                {
                    Hide();
                }

                for (int i = 0; i < DonorsListView.CheckedItems.Count; i++)
                {
                    ulong vcode = Convert.ToUInt64(DonorsListView.CheckedItems[i].SubItems[Donors.VersionCodeIndex].Text);
                    bool isUpdate = DonorsListView.CheckedItems[i].SubItems[Donors.UpdateOrNew].Text.Contains("Update");
                    await Program.form.extractAndPrepareGameToUploadAsync(
                        DonorsListView.CheckedItems[i].SubItems[Donors.GameNameIndex].Text,
                        DonorsListView.CheckedItems[i].SubItems[Donors.PackageNameIndex].Text,
                        vcode, isUpdate);
                    ifuploads = true;
                }
            }

            if (ifuploads) MainForm.doUpload();
            Close();
        }

        private void DonorsListView_ItemChecked(object sender, ItemCheckedEventArgs e)
        {
            SkipButton.Enabled = DonorsListView.CheckedItems.Count == 0;
            DonateButton.Enabled = !SkipButton.Enabled;
            skip_forever.Enabled = DonorsListView.CheckedItems.Count > 0;
        }

        private void SkipButton_Click(object sender, EventArgs e)
        {
            foreach (ListViewItem listItem in DonorsListView.Items)
            {
                if (!listItem.Checked && listItem.SubItems[Donors.UpdateOrNew].Text.Contains("New"))
                    newAppsForList += listItem.SubItems[Donors.GameNameIndex].Text + ";" + listItem.SubItems[Donors.PackageNameIndex].Text + "\n";
            }

            if (!string.IsNullOrEmpty(newAppsForList))
                new NewApps().ShowDialog();

            Close();
        }

        private void DonorsListViewForm_MouseDown(object sender, MouseEventArgs e) => TitleArea_MouseDown(sender, e);
        private void DonorsListViewForm_MouseMove(object sender, MouseEventArgs e) { }
        private void DonorsListViewForm_MouseUp(object sender, MouseEventArgs e) { }

        private void skip_forever_Click(object sender, EventArgs e)
        {
            var appsToBlacklist = DonorsListView.CheckedItems.Cast<ListViewItem>()
                .Select(item => item.SubItems[Donors.PackageNameIndex].Text).ToList();

            if (appsToBlacklist.Count == 0)
            {
                MessageBox.Show("No apps selected to blacklist.", "Info", MessageBoxButtons.OK);
                return;
            }

            if (MessageBox.Show(
                $"Permanently skip donation requests for {appsToBlacklist.Count} app(s)?",
                "Confirm Blacklist", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;

            string blacklistPath = Path.Combine(Environment.CurrentDirectory, "blacklist.json");
            try
            {
                var existingBlacklist = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                if (File.Exists(blacklistPath))
                {
                    var jsonArray = Newtonsoft.Json.JsonConvert.DeserializeObject<string[]>(File.ReadAllText(blacklistPath));
                    if (jsonArray != null)
                        foreach (string entry in jsonArray.Where(ee => !string.IsNullOrWhiteSpace(ee)))
                            existingBlacklist.Add(entry.Trim());
                }

                foreach (string pkg in appsToBlacklist) existingBlacklist.Add(pkg);

                File.WriteAllText(blacklistPath, Newtonsoft.Json.JsonConvert.SerializeObject(existingBlacklist.ToArray(), Newtonsoft.Json.Formatting.Indented));
                Logger.Log($"Added {appsToBlacklist.Count} apps to local blacklist");
                MessageBox.Show($"{appsToBlacklist.Count} app(s) added to blacklist.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                Close();
            }
            catch (Exception ex)
            {
                Logger.Log($"Error saving blacklist: {ex.Message}", LogLevel.ERROR);
                MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}