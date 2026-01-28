namespace AndroidSideloader
{
    partial class UpdateForm
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
            this.panel1 = new System.Windows.Forms.Panel();
            this.YesUpdate = new AndroidSideloader.RoundButton();
            this.panel3 = new System.Windows.Forms.Panel();
            this.UpdateTextBox = new System.Windows.Forms.RichTextBox();
            this.UpdateVerLabel = new System.Windows.Forms.Label();
            this.CurVerLabel = new System.Windows.Forms.Label();
            this.SkipUpdate = new System.Windows.Forms.Label();
            this.panel1.SuspendLayout();
            this.panel3.SuspendLayout();
            this.SuspendLayout();
            // 
            // panel1
            // 
            this.panel1.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(20)))), ((int)(((byte)(24)))), ((int)(((byte)(29)))));
            this.panel1.Controls.Add(this.YesUpdate);
            this.panel1.Controls.Add(this.panel3);
            this.panel1.Controls.Add(this.UpdateVerLabel);
            this.panel1.Controls.Add(this.CurVerLabel);
            this.panel1.Controls.Add(this.SkipUpdate);
            this.panel1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panel1.Location = new System.Drawing.Point(0, 0);
            this.panel1.Name = "panel1";
            this.panel1.Padding = new System.Windows.Forms.Padding(20, 50, 20, 20);
            this.panel1.Size = new System.Drawing.Size(480, 320);
            this.panel1.TabIndex = 5;
            this.panel1.MouseDown += new System.Windows.Forms.MouseEventHandler(this.UpdateForm_MouseDown);
            this.panel1.MouseMove += new System.Windows.Forms.MouseEventHandler(this.UpdateForm_MouseMove);
            this.panel1.MouseUp += new System.Windows.Forms.MouseEventHandler(this.UpdateForm_MouseUp);
            // 
            // YesUpdate
            // 
            this.YesUpdate.Active1 = System.Drawing.Color.FromArgb(((int)(((byte)(113)))), ((int)(((byte)(223)))), ((int)(((byte)(193)))));
            this.YesUpdate.Active2 = System.Drawing.Color.FromArgb(((int)(((byte)(93)))), ((int)(((byte)(203)))), ((int)(((byte)(173)))));
            this.YesUpdate.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.YesUpdate.BackColor = System.Drawing.Color.Transparent;
            this.YesUpdate.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.YesUpdate.Disabled1 = System.Drawing.Color.FromArgb(((int)(((byte)(32)))), ((int)(((byte)(35)))), ((int)(((byte)(45)))));
            this.YesUpdate.Disabled2 = System.Drawing.Color.FromArgb(((int)(((byte)(25)))), ((int)(((byte)(28)))), ((int)(((byte)(35)))));
            this.YesUpdate.DisabledStrokeColor = System.Drawing.Color.FromArgb(((int)(((byte)(50)))), ((int)(((byte)(55)))), ((int)(((byte)(65)))));
            this.YesUpdate.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold);
            this.YesUpdate.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(20)))), ((int)(((byte)(20)))), ((int)(((byte)(20)))));
            this.YesUpdate.Inactive1 = System.Drawing.Color.FromArgb(((int)(((byte)(93)))), ((int)(((byte)(203)))), ((int)(((byte)(173)))));
            this.YesUpdate.Inactive2 = System.Drawing.Color.FromArgb(((int)(((byte)(73)))), ((int)(((byte)(183)))), ((int)(((byte)(153)))));
            this.YesUpdate.Location = new System.Drawing.Point(340, 259);
            this.YesUpdate.Name = "YesUpdate";
            this.YesUpdate.Radius = 6;
            this.YesUpdate.Size = new System.Drawing.Size(120, 36);
            this.YesUpdate.Stroke = false;
            this.YesUpdate.StrokeColor = System.Drawing.Color.FromArgb(((int)(((byte)(74)))), ((int)(((byte)(74)))), ((int)(((byte)(74)))));
            this.YesUpdate.TabIndex = 2;
            this.YesUpdate.Text = "Update Now";
            this.YesUpdate.Transparency = false;
            this.YesUpdate.Click += new System.EventHandler(this.YesUpdate_Click);
            // 
            // panel3
            // 
            this.panel3.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.panel3.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(28)))), ((int)(((byte)(32)))), ((int)(((byte)(38)))));
            this.panel3.Controls.Add(this.UpdateTextBox);
            this.panel3.Location = new System.Drawing.Point(20, 50);
            this.panel3.Name = "panel3";
            this.panel3.Padding = new System.Windows.Forms.Padding(12, 10, 12, 10);
            this.panel3.Size = new System.Drawing.Size(440, 200);
            this.panel3.TabIndex = 0;
            // 
            // UpdateTextBox
            // 
            this.UpdateTextBox.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(28)))), ((int)(((byte)(32)))), ((int)(((byte)(38)))));
            this.UpdateTextBox.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.UpdateTextBox.Dock = System.Windows.Forms.DockStyle.Fill;
            this.UpdateTextBox.Font = new System.Drawing.Font("Segoe UI", 9.5F);
            this.UpdateTextBox.ForeColor = System.Drawing.Color.White;
            this.UpdateTextBox.Location = new System.Drawing.Point(12, 10);
            this.UpdateTextBox.Margin = new System.Windows.Forms.Padding(6);
            this.UpdateTextBox.Name = "UpdateTextBox";
            this.UpdateTextBox.ReadOnly = true;
            this.UpdateTextBox.ScrollBars = System.Windows.Forms.RichTextBoxScrollBars.Vertical;
            this.UpdateTextBox.Size = new System.Drawing.Size(416, 180);
            this.UpdateTextBox.TabIndex = 1;
            this.UpdateTextBox.Text = "";
            this.UpdateTextBox.TextChanged += new System.EventHandler(this.UpdateTextBox_TextChanged);
            this.UpdateTextBox.MouseDown += new System.Windows.Forms.MouseEventHandler(this.UpdateForm_MouseDown);
            this.UpdateTextBox.MouseMove += new System.Windows.Forms.MouseEventHandler(this.UpdateForm_MouseMove);
            this.UpdateTextBox.MouseUp += new System.Windows.Forms.MouseEventHandler(this.UpdateForm_MouseUp);
            // 
            // UpdateVerLabel
            // 
            this.UpdateVerLabel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.UpdateVerLabel.AutoSize = true;
            this.UpdateVerLabel.BackColor = System.Drawing.Color.Transparent;
            this.UpdateVerLabel.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);
            this.UpdateVerLabel.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(93)))), ((int)(((byte)(203)))), ((int)(((byte)(173)))));
            this.UpdateVerLabel.Location = new System.Drawing.Point(20, 285);
            this.UpdateVerLabel.Name = "UpdateVerLabel";
            this.UpdateVerLabel.Size = new System.Drawing.Size(95, 15);
            this.UpdateVerLabel.TabIndex = 3;
            this.UpdateVerLabel.Text = "Update Version:";
            // 
            // CurVerLabel
            // 
            this.CurVerLabel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.CurVerLabel.AutoSize = true;
            this.CurVerLabel.BackColor = System.Drawing.Color.Transparent;
            this.CurVerLabel.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.CurVerLabel.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(160)))), ((int)(((byte)(165)))), ((int)(((byte)(175)))));
            this.CurVerLabel.Location = new System.Drawing.Point(20, 266);
            this.CurVerLabel.Name = "CurVerLabel";
            this.CurVerLabel.Size = new System.Drawing.Size(91, 15);
            this.CurVerLabel.TabIndex = 2;
            this.CurVerLabel.Text = "Current Version:";
            // 
            // SkipUpdate
            // 
            this.SkipUpdate.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.SkipUpdate.AutoSize = true;
            this.SkipUpdate.BackColor = System.Drawing.Color.Transparent;
            this.SkipUpdate.Cursor = System.Windows.Forms.Cursors.Hand;
            this.SkipUpdate.Font = new System.Drawing.Font("Segoe UI", 8.5F);
            this.SkipUpdate.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(160)))), ((int)(((byte)(165)))), ((int)(((byte)(175)))));
            this.SkipUpdate.Location = new System.Drawing.Point(380, 297);
            this.SkipUpdate.Name = "SkipUpdate";
            this.SkipUpdate.Size = new System.Drawing.Size(73, 15);
            this.SkipUpdate.TabIndex = 4;
            this.SkipUpdate.Text = "Skip for now";
            this.SkipUpdate.Click += new System.EventHandler(this.SkipUpdate_Click);
            // 
            // UpdateForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(20)))), ((int)(((byte)(24)))), ((int)(((byte)(29)))));
            this.ClientSize = new System.Drawing.Size(480, 320);
            this.ControlBox = false;
            this.Controls.Add(this.panel1);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
            this.Name = "UpdateForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.MouseDown += new System.Windows.Forms.MouseEventHandler(this.UpdateForm_MouseDown);
            this.MouseMove += new System.Windows.Forms.MouseEventHandler(this.UpdateForm_MouseMove);
            this.MouseUp += new System.Windows.Forms.MouseEventHandler(this.UpdateForm_MouseUp);
            this.panel1.ResumeLayout(false);
            this.panel1.PerformLayout();
            this.panel3.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Label SkipUpdate;
        private System.Windows.Forms.Label CurVerLabel;
        private System.Windows.Forms.Label UpdateVerLabel;
        private System.Windows.Forms.Panel panel3;
        private System.Windows.Forms.RichTextBox UpdateTextBox;
        private System.Windows.Forms.Panel panel1;
        private RoundButton YesUpdate;
    }
}