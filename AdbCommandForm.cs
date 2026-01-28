using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace AndroidSideloader
{
    public partial class AdbCommandForm : Form
    {
        public string Command { get; private set; }
        public bool ToggleUpdatesClicked { get; private set; }

        public AdbCommandForm()
        {
            InitializeComponent();

            // Use same icon as the executable
            this.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            this.ShowIcon = true; // Enable icon
        }

        private void InitializeComponent()
        {
            this.lblTitle = new Label();
            this.txtCommand = new TextBox();
            this.btnSend = new RoundButton();
            this.btnToggleUpdates = new RoundButton();
            this.btnClose = new RoundButton();
            this.separator = new Panel();
            this.lblHint = new Label();
            this.SuspendLayout();
            // 
            // lblTitle
            // 
            this.lblTitle.AutoSize = true;
            this.lblTitle.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
            this.lblTitle.ForeColor = Color.FromArgb(93, 203, 173);
            this.lblTitle.Location = new Point(20, 15);
            this.lblTitle.Name = "lblTitle";
            this.lblTitle.Size = new Size(140, 20);
            this.lblTitle.TabIndex = 0;
            this.lblTitle.Text = "Run ADB Command";
            // 
            // txtCommand
            // 
            this.txtCommand.BackColor = Color.FromArgb(40, 44, 52);
            this.txtCommand.BorderStyle = BorderStyle.FixedSingle;
            this.txtCommand.Font = new Font("Consolas", 10F);
            this.txtCommand.ForeColor = Color.White;
            this.txtCommand.Location = new Point(24, 50);
            this.txtCommand.Name = "txtCommand";
            this.txtCommand.Size = new Size(292, 23);
            this.txtCommand.TabIndex = 1;
            this.txtCommand.KeyPress += TxtCommand_KeyPress;
            // 
            // lblHint
            // 
            this.lblHint.AutoSize = true;
            this.lblHint.Font = new Font("Segoe UI", 8F);
            this.lblHint.ForeColor = Color.FromArgb(120, 120, 120);
            this.lblHint.Location = new Point(24, 78);
            this.lblHint.Name = "lblHint";
            this.lblHint.Size = new Size(200, 13);
            this.lblHint.TabIndex = 2;
            this.lblHint.Text = "Enter command without \"adb\" prefix";
            // 
            // separator
            // 
            this.separator.BackColor = Color.FromArgb(50, 55, 65);
            this.separator.Location = new Point(20, 105);
            this.separator.Name = "separator";
            this.separator.Size = new Size(300, 1);
            this.separator.TabIndex = 3;
            // 
            // btnSend
            // 
            this.btnSend.Active1 = Color.FromArgb(113, 223, 193);
            this.btnSend.Active2 = Color.FromArgb(113, 223, 193);
            this.btnSend.BackColor = Color.Transparent;
            this.btnSend.Cursor = Cursors.Hand;
            this.btnSend.DialogResult = DialogResult.OK;
            this.btnSend.Disabled1 = Color.FromArgb(32, 35, 45);
            this.btnSend.Disabled2 = Color.FromArgb(25, 28, 35);
            this.btnSend.DisabledStrokeColor = Color.FromArgb(50, 55, 65);
            this.btnSend.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            this.btnSend.ForeColor = Color.FromArgb(20, 24, 29);
            this.btnSend.Inactive1 = Color.FromArgb(93, 203, 173);
            this.btnSend.Inactive2 = Color.FromArgb(93, 203, 173);
            this.btnSend.Location = new Point(24, 120);
            this.btnSend.Name = "btnSend";
            this.btnSend.Radius = 5;
            this.btnSend.Size = new Size(140, 30);
            this.btnSend.Stroke = false;
            this.btnSend.StrokeColor = Color.FromArgb(93, 203, 173);
            this.btnSend.TabIndex = 4;
            this.btnSend.Text = "SEND COMMAND";
            this.btnSend.Transparency = false;
            this.btnSend.Click += BtnSend_Click;
            // 
            // btnToggleUpdates
            // 
            this.btnToggleUpdates.Active1 = Color.FromArgb(50, 55, 65);
            this.btnToggleUpdates.Active2 = Color.FromArgb(50, 55, 65);
            this.btnToggleUpdates.BackColor = Color.Transparent;
            this.btnToggleUpdates.Cursor = Cursors.Hand;
            this.btnToggleUpdates.DialogResult = DialogResult.None;
            this.btnToggleUpdates.Disabled1 = Color.FromArgb(32, 35, 45);
            this.btnToggleUpdates.Disabled2 = Color.FromArgb(25, 28, 35);
            this.btnToggleUpdates.DisabledStrokeColor = Color.FromArgb(50, 55, 65);
            this.btnToggleUpdates.Font = new Font("Segoe UI", 9F);
            this.btnToggleUpdates.ForeColor = Color.White;
            this.btnToggleUpdates.Inactive1 = Color.FromArgb(40, 44, 52);
            this.btnToggleUpdates.Inactive2 = Color.FromArgb(40, 44, 52);
            this.btnToggleUpdates.Location = new Point(176, 120);
            this.btnToggleUpdates.Name = "btnToggleUpdates";
            this.btnToggleUpdates.Radius = 5;
            this.btnToggleUpdates.Size = new Size(140, 30);
            this.btnToggleUpdates.Stroke = true;
            this.btnToggleUpdates.StrokeColor = Color.FromArgb(60, 65, 75);
            this.btnToggleUpdates.TabIndex = 5;
            this.btnToggleUpdates.Text = "Toggle OS Updates";
            this.btnToggleUpdates.Transparency = false;
            this.btnToggleUpdates.Click += BtnToggleUpdates_Click;
            // 
            // btnClose
            // 
            this.btnClose.Active1 = Color.FromArgb(60, 65, 75);
            this.btnClose.Active2 = Color.FromArgb(60, 65, 75);
            this.btnClose.BackColor = Color.Transparent;
            this.btnClose.Cursor = Cursors.Hand;
            this.btnClose.DialogResult = DialogResult.Cancel;
            this.btnClose.Disabled1 = Color.FromArgb(32, 35, 45);
            this.btnClose.Disabled2 = Color.FromArgb(25, 28, 35);
            this.btnClose.DisabledStrokeColor = Color.FromArgb(50, 55, 65);
            this.btnClose.Font = new Font("Segoe UI", 9F);
            this.btnClose.ForeColor = Color.White;
            this.btnClose.Inactive1 = Color.FromArgb(50, 55, 65);
            this.btnClose.Inactive2 = Color.FromArgb(50, 55, 65);
            this.btnClose.Location = new Point(24, 160);
            this.btnClose.Name = "btnClose";
            this.btnClose.Radius = 5;
            this.btnClose.Size = new Size(292, 30);
            this.btnClose.Stroke = true;
            this.btnClose.StrokeColor = Color.FromArgb(74, 74, 74);
            this.btnClose.TabIndex = 6;
            this.btnClose.Text = "Close";
            this.btnClose.Transparency = false;
            this.btnClose.Click += BtnClose_Click;
            // 
            // AdbCommandForm
            // 
            this.AcceptButton = this.btnSend;
            this.AutoScaleDimensions = new SizeF(6F, 13F);
            this.AutoScaleMode = AutoScaleMode.Font;
            this.BackColor = Color.FromArgb(20, 24, 29);
            this.CancelButton = this.btnClose;
            this.ClientSize = new Size(340, 210);
            this.Controls.Add(this.lblTitle);
            this.Controls.Add(this.txtCommand);
            this.Controls.Add(this.lblHint);
            this.Controls.Add(this.separator);
            this.Controls.Add(this.btnSend);
            this.Controls.Add(this.btnToggleUpdates);
            this.Controls.Add(this.btnClose);
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "AdbCommandForm";
            this.ShowIcon = false;
            this.StartPosition = FormStartPosition.CenterParent;
            this.Text = "ADB Command";
            this.Load += AdbCommandForm_Load;
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        private void AdbCommandForm_Load(object sender, EventArgs e)
        {
            txtCommand.Focus();
        }

        private void TxtCommand_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == (char)Keys.Enter)
            {
                e.Handled = true;
                BtnSend_Click(sender, e);
            }
            else if (e.KeyChar == (char)Keys.Escape)
            {
                e.Handled = true;
                this.DialogResult = DialogResult.Cancel;
                this.Close();
            }
        }

        private void BtnSend_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtCommand.Text))
            {
                return;
            }

            Command = txtCommand.Text;
            ToggleUpdatesClicked = false;
            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        private void BtnToggleUpdates_Click(object sender, EventArgs e)
        {
            // Check current state and set the appropriate command
            string adbResult = ADB.RunAdbCommandToString("shell pm list packages -d").Output;
            bool isUpdatesDisabled = adbResult.Contains("com.oculus.updater");

            if (isUpdatesDisabled)
            {
                Command = "shell pm enable com.oculus.updater";
            }
            else
            {
                Command = "shell pm disable-user --user 0 com.oculus.updater";
            }

            ToggleUpdatesClicked = true;
            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        private void BtnClose_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.Cancel;
            this.Close();
        }

        private Label lblTitle;
        private TextBox txtCommand;
        private Label lblHint;
        private Panel separator;
        private RoundButton btnSend;
        private RoundButton btnToggleUpdates;
        private RoundButton btnClose;
    }
}