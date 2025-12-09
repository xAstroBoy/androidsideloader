
namespace AndroidSideloader
{
    partial class NewApps
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(NewApps));
            this.NewAppsListView = new System.Windows.Forms.ListView();
            this.GameNameIndex = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.PackageNameIndex = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.panel1 = new System.Windows.Forms.Panel();
            this.NewAppsButton = new AndroidSideloader.RoundButton();
            this.label2 = new System.Windows.Forms.Label();
            this.titleLabel = new System.Windows.Forms.Label();
            this.panel2 = new System.Windows.Forms.Panel();
            this.panel1.SuspendLayout();
            this.panel2.SuspendLayout();
            this.SuspendLayout();
            // 
            // NewAppsListView
            // 
            this.NewAppsListView.AccessibleRole = System.Windows.Forms.AccessibleRole.None;
            this.NewAppsListView.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(28)))), ((int)(((byte)(32)))), ((int)(((byte)(38)))));
            this.NewAppsListView.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.NewAppsListView.CausesValidation = false;
            this.NewAppsListView.CheckBoxes = true;
            this.NewAppsListView.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.GameNameIndex,
            this.PackageNameIndex});
            this.NewAppsListView.Dock = System.Windows.Forms.DockStyle.Fill;
            this.NewAppsListView.Font = new System.Drawing.Font("Segoe UI", 9.5F);
            this.NewAppsListView.ForeColor = System.Drawing.Color.White;
            this.NewAppsListView.FullRowSelect = true;
            this.NewAppsListView.HeaderStyle = System.Windows.Forms.ColumnHeaderStyle.Nonclickable;
            this.NewAppsListView.HideSelection = false;
            this.NewAppsListView.Location = new System.Drawing.Point(1, 1);
            this.NewAppsListView.Name = "NewAppsListView";
            this.NewAppsListView.Size = new System.Drawing.Size(298, 168);
            this.NewAppsListView.TabIndex = 1;
            this.NewAppsListView.UseCompatibleStateImageBehavior = false;
            this.NewAppsListView.View = System.Windows.Forms.View.Details;
            this.NewAppsListView.MouseDown += new System.Windows.Forms.MouseEventHandler(this.label2_MouseDown);
            this.NewAppsListView.MouseMove += new System.Windows.Forms.MouseEventHandler(this.label2_MouseMove);
            this.NewAppsListView.MouseUp += new System.Windows.Forms.MouseEventHandler(this.label2_MouseUp);
            // 
            // GameNameIndex
            // 
            this.GameNameIndex.Text = "App Name";
            this.GameNameIndex.Width = 280;
            // 
            // PackageNameIndex
            // 
            this.PackageNameIndex.Width = 0;
            // 
            // panel1
            // 
            this.panel1.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(20)))), ((int)(((byte)(24)))), ((int)(((byte)(29)))));
            this.panel1.Controls.Add(this.NewAppsButton);
            this.panel1.Controls.Add(this.label2);
            this.panel1.Controls.Add(this.titleLabel);
            this.panel1.Controls.Add(this.panel2);
            this.panel1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panel1.Location = new System.Drawing.Point(0, 0);
            this.panel1.Name = "panel1";
            this.panel1.Padding = new System.Windows.Forms.Padding(16);
            this.panel1.Size = new System.Drawing.Size(340, 300);
            this.panel1.TabIndex = 10;
            this.panel1.MouseDown += new System.Windows.Forms.MouseEventHandler(this.label2_MouseDown);
            this.panel1.MouseMove += new System.Windows.Forms.MouseEventHandler(this.label2_MouseMove);
            this.panel1.MouseUp += new System.Windows.Forms.MouseEventHandler(this.label2_MouseUp);
            // 
            // NewAppsButton
            // 
            this.NewAppsButton.Active1 = System.Drawing.Color.FromArgb(((int)(((byte)(50)))), ((int)(((byte)(140)))), ((int)(((byte)(115)))));
            this.NewAppsButton.Active2 = System.Drawing.Color.FromArgb(((int)(((byte)(45)))), ((int)(((byte)(125)))), ((int)(((byte)(105)))));
            this.NewAppsButton.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.NewAppsButton.BackColor = System.Drawing.Color.Transparent;
            this.NewAppsButton.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.NewAppsButton.Disabled1 = System.Drawing.Color.FromArgb(((int)(((byte)(32)))), ((int)(((byte)(35)))), ((int)(((byte)(45)))));
            this.NewAppsButton.Disabled2 = System.Drawing.Color.FromArgb(((int)(((byte)(25)))), ((int)(((byte)(28)))), ((int)(((byte)(35)))));
            this.NewAppsButton.DisabledStrokeColor = System.Drawing.Color.FromArgb(((int)(((byte)(50)))), ((int)(((byte)(55)))), ((int)(((byte)(65)))));
            this.NewAppsButton.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold);
            this.NewAppsButton.ForeColor = System.Drawing.Color.White;
            this.NewAppsButton.Inactive1 = System.Drawing.Color.FromArgb(((int)(((byte)(40)))), ((int)(((byte)(120)))), ((int)(((byte)(100)))));
            this.NewAppsButton.Inactive2 = System.Drawing.Color.FromArgb(((int)(((byte)(35)))), ((int)(((byte)(100)))), ((int)(((byte)(85)))));
            this.NewAppsButton.Location = new System.Drawing.Point(20, 252);
            this.NewAppsButton.Name = "NewAppsButton";
            this.NewAppsButton.Radius = 4;
            this.NewAppsButton.Size = new System.Drawing.Size(300, 36);
            this.NewAppsButton.Stroke = true;
            this.NewAppsButton.StrokeColor = System.Drawing.Color.FromArgb(((int)(((byte)(60)))), ((int)(((byte)(150)))), ((int)(((byte)(125)))));
            this.NewAppsButton.TabIndex = 2;
            this.NewAppsButton.Text = "Continue";
            this.NewAppsButton.Transparency = false;
            this.NewAppsButton.Click += new System.EventHandler(this.DonateButton_Click);
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.BackColor = System.Drawing.Color.Transparent;
            this.label2.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.label2.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(160)))), ((int)(((byte)(165)))), ((int)(((byte)(175)))));
            this.label2.Location = new System.Drawing.Point(20, 42);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(223, 15);
            this.label2.TabIndex = 9;
            this.label2.Text = "Check the box for all free or non-VR apps";
            this.label2.MouseDown += new System.Windows.Forms.MouseEventHandler(this.label2_MouseDown);
            this.label2.MouseMove += new System.Windows.Forms.MouseEventHandler(this.label2_MouseMove);
            this.label2.MouseUp += new System.Windows.Forms.MouseEventHandler(this.label2_MouseUp);
            // 
            // titleLabel
            // 
            this.titleLabel.AutoSize = true;
            this.titleLabel.BackColor = System.Drawing.Color.Transparent;
            this.titleLabel.Font = new System.Drawing.Font("Segoe UI", 11F, System.Drawing.FontStyle.Bold);
            this.titleLabel.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(93)))), ((int)(((byte)(203)))), ((int)(((byte)(173)))));
            this.titleLabel.Location = new System.Drawing.Point(20, 15);
            this.titleLabel.Name = "titleLabel";
            this.titleLabel.Size = new System.Drawing.Size(129, 20);
            this.titleLabel.TabIndex = 11;
            this.titleLabel.Text = "New Apps Found";
            this.titleLabel.MouseDown += new System.Windows.Forms.MouseEventHandler(this.label2_MouseDown);
            this.titleLabel.MouseMove += new System.Windows.Forms.MouseEventHandler(this.label2_MouseMove);
            this.titleLabel.MouseUp += new System.Windows.Forms.MouseEventHandler(this.label2_MouseUp);
            // 
            // panel2
            // 
            this.panel2.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.panel2.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(28)))), ((int)(((byte)(32)))), ((int)(((byte)(38)))));
            this.panel2.Controls.Add(this.NewAppsListView);
            this.panel2.Location = new System.Drawing.Point(20, 70);
            this.panel2.Name = "panel2";
            this.panel2.Padding = new System.Windows.Forms.Padding(1);
            this.panel2.Size = new System.Drawing.Size(300, 170);
            this.panel2.TabIndex = 8;
            // 
            // NewApps
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(20)))), ((int)(((byte)(24)))), ((int)(((byte)(29)))));
            this.ClientSize = new System.Drawing.Size(340, 300);
            this.ControlBox = false;
            this.Controls.Add(this.panel1);
            this.ForeColor = System.Drawing.Color.White;
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Name = "NewApps";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Load += new System.EventHandler(this.NewApps_Load);
            this.MouseDown += new System.Windows.Forms.MouseEventHandler(this.label2_MouseDown);
            this.MouseMove += new System.Windows.Forms.MouseEventHandler(this.label2_MouseMove);
            this.MouseUp += new System.Windows.Forms.MouseEventHandler(this.label2_MouseUp);
            this.panel1.ResumeLayout(false);
            this.panel1.PerformLayout();
            this.panel2.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.ListView NewAppsListView;
        private System.Windows.Forms.ColumnHeader GameNameIndex;
        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.Panel panel2;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label titleLabel;
        private System.Windows.Forms.ColumnHeader PackageNameIndex;
        private RoundButton NewAppsButton;
    }
}