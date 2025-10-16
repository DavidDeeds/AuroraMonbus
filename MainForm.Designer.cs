namespace AuroraMonbus
{
    partial class MainForm
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
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
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            btnConnect = new Button();
            btnDisconnect = new Button();
            txtOutput = new RichTextBox();
            lblStatus = new Label();
            label1 = new Label();
            chkShowRaw = new CheckBox();
            btnClear = new Button();
            btnSysInfo = new Button();
            SuspendLayout();
            // 
            // btnConnect
            // 
            btnConnect.ForeColor = SystemColors.ControlText;
            btnConnect.Location = new Point(12, 12);
            btnConnect.Name = "btnConnect";
            btnConnect.Size = new Size(94, 29);
            btnConnect.TabIndex = 0;
            btnConnect.Text = "Connect";
            btnConnect.UseVisualStyleBackColor = true;
            btnConnect.Click += btnConnect_Click;
            // 
            // btnDisconnect
            // 
            btnDisconnect.Location = new Point(423, 67);
            btnDisconnect.Name = "btnDisconnect";
            btnDisconnect.Size = new Size(94, 29);
            btnDisconnect.TabIndex = 1;
            btnDisconnect.Text = "Disconnect";
            btnDisconnect.UseVisualStyleBackColor = true;
            btnDisconnect.Click += btnDisconnect_Click;
            // 
            // txtOutput
            // 
            txtOutput.Font = new Font("Consolas", 9F, FontStyle.Regular, GraphicsUnit.Point, 0);
            txtOutput.Location = new Point(12, 102);
            txtOutput.Name = "txtOutput";
            txtOutput.ReadOnly = true;
            txtOutput.ScrollBars = RichTextBoxScrollBars.Vertical;
            txtOutput.Size = new Size(505, 574);
            txtOutput.TabIndex = 38;
            txtOutput.Text = "";
            txtOutput.WordWrap = false;
            // 
            // lblStatus
            // 
            lblStatus.AutoSize = true;
            lblStatus.Location = new Point(70, 44);
            lblStatus.Name = "lblStatus";
            lblStatus.Size = new Size(15, 20);
            lblStatus.TabIndex = 39;
            lblStatus.Text = "..";
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Location = new Point(12, 44);
            label1.Name = "label1";
            label1.Size = new Size(52, 20);
            label1.TabIndex = 40;
            label1.Text = "Status:";
            // 
            // chkShowRaw
            // 
            chkShowRaw.AutoSize = true;
            chkShowRaw.Location = new Point(112, 15);
            chkShowRaw.Name = "chkShowRaw";
            chkShowRaw.Size = new Size(135, 24);
            chkShowRaw.TabIndex = 41;
            chkShowRaw.Text = "Show Raw Data";
            chkShowRaw.UseVisualStyleBackColor = true;
            // 
            // btnClear
            // 
            btnClear.Location = new Point(12, 67);
            btnClear.Name = "btnClear";
            btnClear.Size = new Size(94, 29);
            btnClear.TabIndex = 42;
            btnClear.Text = "Clear";
            btnClear.UseVisualStyleBackColor = true;
            // 
            // btnSysInfo
            // 
            btnSysInfo.Location = new Point(423, 12);
            btnSysInfo.Name = "btnSysInfo";
            btnSysInfo.Size = new Size(94, 29);
            btnSysInfo.TabIndex = 43;
            btnSysInfo.Text = "System Info";
            btnSysInfo.UseVisualStyleBackColor = true;
            btnSysInfo.Click += btnSysInfo_Click;
            // 
            // MainForm
            // 
            AutoScaleDimensions = new SizeF(8F, 20F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(529, 688);
            Controls.Add(btnSysInfo);
            Controls.Add(btnClear);
            Controls.Add(chkShowRaw);
            Controls.Add(label1);
            Controls.Add(lblStatus);
            Controls.Add(txtOutput);
            Controls.Add(btnDisconnect);
            Controls.Add(btnConnect);
            MaximizeBox = false;
            Name = "MainForm";
            Text = "AuroraMonbus";
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private Button btnConnect;
        private Button btnDisconnect;
        private RichTextBox txtOutput;
        private Label lblStatus;
        private Label label1;
        private CheckBox chkShowRaw;
        private Button btnClear;
        private Button btnSysInfo;
    }
}
