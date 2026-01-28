using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace AndroidSideloader
{
    public partial class NewApps : Form
    {
        // Modern theme colors
        private static readonly Color BackgroundColor = Color.FromArgb(20, 24, 29);
        private static readonly Color BorderColor = Color.FromArgb(70, 80, 100);

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

        public NewApps()
        {
            InitializeComponent();

            // Use same icon as the executable
            this.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);

            ApplyModernTheme();
            CenterToScreen();
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
            label2.MouseDown += TitleArea_MouseDown;
            titleLabel.MouseDown += TitleArea_MouseDown;
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

        private void label2_MouseDown(object sender, MouseEventArgs e) => TitleArea_MouseDown(sender, e);
        private void label2_MouseMove(object sender, MouseEventArgs e) { }
        private void label2_MouseUp(object sender, MouseEventArgs e) { }

        private void DonateButton_Click(object sender, EventArgs e)
        {
            string HWID = SideloaderUtilities.UUID();
            foreach (ListViewItem listItem in NewAppsListView.Items)
            {
                if (listItem.Checked)
                    Properties.Settings.Default.NonAppPackages += listItem.SubItems[Donors.PackageNameIndex].Text + ";" + HWID + "\n";
                else
                    Properties.Settings.Default.AppPackages += listItem.SubItems[Donors.PackageNameIndex].Text + "\n";
                Properties.Settings.Default.Save();
            }
            MainForm.newPackageUpload();
            Close();
        }

        private void NewApps_Load(object sender, EventArgs e)
        {
            NewAppsListView.Items.Clear();
            Donors.initNewApps();
            var NewAppList = new List<ListViewItem>();
            foreach (string[] release in Donors.newApps)
            {
                ListViewItem NGame = new ListViewItem(release);
                if (!NewAppList.Contains(NGame))
                    NewAppList.Add(NGame);
            }
            NewAppsListView.BeginUpdate();
            NewAppsListView.Items.AddRange(NewAppList.ToArray());
            NewAppsListView.EndUpdate();
        }
    }
}