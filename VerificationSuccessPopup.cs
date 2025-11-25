using System;
using System.Drawing;
using System.Windows.Forms;

namespace BiometricsFingerprint
{
    public partial class VerificationSuccessPopup : Form
    {
        public VerificationSuccessPopup(string name, string uid)
        {
            InitializeComponent();
            messageLabel.Text = $"✅ VERIFICATION SUCCESSFUL!\n\nStudent: {name}\nUID: {uid}";
        }

        private void okButton_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(VerificationSuccessPopup));
            this.messageLabel = new System.Windows.Forms.Label();
            this.okButton = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // messageLabel
            // 
            this.messageLabel.Font = new System.Drawing.Font("Poppins", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.messageLabel.Location = new System.Drawing.Point(20, 20);
            this.messageLabel.Name = "messageLabel";
            this.messageLabel.Size = new System.Drawing.Size(465, 227);
            this.messageLabel.TabIndex = 0;
            this.messageLabel.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // okButton
            // 
            this.okButton.BackColor = System.Drawing.Color.DarkGreen;
            this.okButton.Font = new System.Drawing.Font("Sitka Banner", 10.8F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.okButton.ForeColor = System.Drawing.Color.White;
            this.okButton.Location = new System.Drawing.Point(365, 265);
            this.okButton.Name = "okButton";
            this.okButton.Size = new System.Drawing.Size(120, 40);
            this.okButton.TabIndex = 1;
            this.okButton.Text = "OK";
            this.okButton.UseVisualStyleBackColor = false;
            this.okButton.Click += new System.EventHandler(this.okButton_Click);
            // 
            // VerificationSuccessPopup
            // 
            this.BackgroundImage = ((System.Drawing.Image)(resources.GetObject("$this.BackgroundImage")));
            this.ClientSize = new System.Drawing.Size(508, 332);
            this.Controls.Add(this.okButton);
            this.Controls.Add(this.messageLabel);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "VerificationSuccessPopup";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Verification Successful";
            this.Load += new System.EventHandler(this.VerificationSuccessPopup_Load);
            this.ResumeLayout(false);

        }

        private System.Windows.Forms.Label messageLabel;
        private System.Windows.Forms.Button okButton;

        private void VerificationSuccessPopup_Load(object sender, EventArgs e)
        {

        }
    }
}