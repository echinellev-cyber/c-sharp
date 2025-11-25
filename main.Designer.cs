namespace BiometricsFingerprint
{
    partial class main
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(main));
            this.register_btn = new System.Windows.Forms.Button();
            this.verify_btn = new System.Windows.Forms.Button();
            this.attendance_btn = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // register_btn
            // 
            this.register_btn.AutoSize = true;
            this.register_btn.BackColor = System.Drawing.Color.DarkGreen;
            this.register_btn.BackgroundImage = ((System.Drawing.Image)(resources.GetObject("register_btn.BackgroundImage")));
            this.register_btn.BackgroundImageLayout = System.Windows.Forms.ImageLayout.None;
            this.register_btn.Font = new System.Drawing.Font("Poppins", 16.2F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.register_btn.ForeColor = System.Drawing.SystemColors.Info;
            this.register_btn.Location = new System.Drawing.Point(846, 270);
            this.register_btn.Name = "register_btn";
            this.register_btn.Size = new System.Drawing.Size(500, 145);
            this.register_btn.TabIndex = 0;
            this.register_btn.Text = "REGISTER STUDENT";
            this.register_btn.UseVisualStyleBackColor = false;
            this.register_btn.Click += new System.EventHandler(this.register_btn_Click);
            // 
            // verify_btn
            // 
            this.verify_btn.AutoSize = true;
            this.verify_btn.BackColor = System.Drawing.Color.DarkGreen;
            this.verify_btn.BackgroundImage = ((System.Drawing.Image)(resources.GetObject("verify_btn.BackgroundImage")));
            this.verify_btn.BackgroundImageLayout = System.Windows.Forms.ImageLayout.None;
            this.verify_btn.Font = new System.Drawing.Font("Poppins", 16.2F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.verify_btn.ForeColor = System.Drawing.SystemColors.ControlLight;
            this.verify_btn.Location = new System.Drawing.Point(846, 463);
            this.verify_btn.Name = "verify_btn";
            this.verify_btn.Size = new System.Drawing.Size(500, 151);
            this.verify_btn.TabIndex = 1;
            this.verify_btn.Text = "VERIFY STUDENT";
            this.verify_btn.UseVisualStyleBackColor = false;
            this.verify_btn.Click += new System.EventHandler(this.verify_btn_Click);
            // 
            // attendance_btn
            // 
            this.attendance_btn.AutoSize = true;
            this.attendance_btn.BackColor = System.Drawing.Color.DarkGreen;
            this.attendance_btn.BackgroundImage = ((System.Drawing.Image)(resources.GetObject("attendance_btn.BackgroundImage")));
            this.attendance_btn.BackgroundImageLayout = System.Windows.Forms.ImageLayout.None;
            this.attendance_btn.Font = new System.Drawing.Font("Poppins", 16.2F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.attendance_btn.ForeColor = System.Drawing.SystemColors.ControlLight;
            this.attendance_btn.Location = new System.Drawing.Point(846, 654);
            this.attendance_btn.Name = "attendance_btn";
            this.attendance_btn.Size = new System.Drawing.Size(500, 147);
            this.attendance_btn.TabIndex = 2;
            this.attendance_btn.Text = "STUDENT ATTENDANCE\r\n";
            this.attendance_btn.UseVisualStyleBackColor = false;
            this.attendance_btn.Click += new System.EventHandler(this.attendance_btn_Click);
            // 
            // main
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.Ivory;
            this.BackgroundImage = ((System.Drawing.Image)(resources.GetObject("$this.BackgroundImage")));
            this.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Stretch;
            this.ClientSize = new System.Drawing.Size(1536, 857);
            this.Controls.Add(this.attendance_btn);
            this.Controls.Add(this.verify_btn);
            this.Controls.Add(this.register_btn);
            this.Cursor = System.Windows.Forms.Cursors.Hand;
            this.DoubleBuffered = true;
            this.Font = new System.Drawing.Font("Rockwell", 7.8F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Name = "main";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "ACI Student Fingerprint";
            this.Load += new System.EventHandler(this.main_Load);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button register_btn;
        private System.Windows.Forms.Button verify_btn;
        private System.Windows.Forms.Button attendance_btn;
    }
}

