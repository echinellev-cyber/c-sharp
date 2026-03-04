using DPFP.Processing;
using System;
using System.Windows.Forms;
using System.Drawing.Text;
using MySql.Data.MySqlClient;
using System.Drawing;
using System.Threading.Tasks;

namespace BiometricsFingerprint
{
    delegate void Function();
    public partial class main : Form
    {
        private DPFP.Template Template;
        private Label lblDbStatus;

        public main()
        {
            InitializeComponent();
            this.Resize += main_Resize; // Handle resizing
            
            // Initialize Status Label
            lblDbStatus = new Label();
            lblDbStatus.AutoSize = true;
            lblDbStatus.Font = new Font("Segoe UI", 12F, FontStyle.Bold);
            lblDbStatus.Location = new Point(20, 20);
            lblDbStatus.Text = "Checking Database Connection...";
            lblDbStatus.ForeColor = Color.Orange;
            this.Controls.Add(lblDbStatus);
        }

        private void OnTemplate(DPFP.Template template)
        {
            this.Invoke(new Function(delegate ()
            {
                Template = template;
                if (Template != null)
                {
                    MessageBox.Show(this, "The fingerprint template is ready for fingerprint verification", "Fingerprint Registration");
                }
                else
                {
                    MessageBox.Show(this, "The fingerprint template is not valid. Repeat fingerprint scanning", "Fingerprint Registration");
                }
            }));
        }

        private void register_btn_Click(object sender, EventArgs e)
        {
            register EnFrm = new register();
            EnFrm.OnTemplate += this.OnTemplate;
            EnFrm.Show(); // Removed 'this' to allow independent window navigation
        }

        private void verify_btn_Click(object sender, EventArgs e)
        {
            try
            {
                verify verifyForm = new verify();
                verifyForm.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Error opening verification form: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void main_Load(object sender, EventArgs e)
        {
            // Set form to maximized window (full screen but with title bar and close button)
            this.WindowState = FormWindowState.Maximized;

            PositionButtonsRight();
            this.Resize += main_Resize;
            
            // Check DB Connection asynchronously
            CheckDatabaseConnection();
        }

        private async void CheckDatabaseConnection()
        {
            lblDbStatus.Text = "Connecting to Database...";
            lblDbStatus.ForeColor = Color.Orange;

            await Task.Run(() =>
            {
                try
                {
                    using (MySqlConnection conn = new MySqlConnection(DatabaseConfig.ConnectionString))
                    {
                        conn.Open();
                        this.Invoke(new Action(() =>
                        {
                            lblDbStatus.Text = "✅ Database Connected";
                            lblDbStatus.ForeColor = Color.Green;
                        }));
                    }
                }
                catch (MySqlException myEx)
                {
                    this.Invoke(new Action(() =>
                    {
                        lblDbStatus.Text = $"❌ Error {myEx.Number}";
                        lblDbStatus.ForeColor = Color.Red;

                        // Show EXACT error from the engine without speculation
                        string msg = $"Database Error Code: {myEx.Number}\n\nSystem Message:\n{myEx.Message}";
                        
                        MessageBox.Show(msg, "Connection Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);

                        ToolTip tt = new ToolTip();
                        tt.SetToolTip(lblDbStatus, myEx.Message);
                        lblDbStatus.Cursor = Cursors.Hand;
                        lblDbStatus.Click += (s, e) => MessageBox.Show(msg, "Database Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }));
                }
                catch (Exception ex)
                {
                    this.Invoke(new Action(() =>
                    {
                        lblDbStatus.Text = "❌ Connection Failed";
                        lblDbStatus.ForeColor = Color.Red;
                        
                        MessageBox.Show($"System Error:\n{ex.Message}", "Connection Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        
                        // Optional: Add tooltip or click event to see error
                        ToolTip tt = new ToolTip();
                        tt.SetToolTip(lblDbStatus, ex.Message);
                        lblDbStatus.Cursor = Cursors.Hand;
                        lblDbStatus.Click += (s, e) => MessageBox.Show($"Connection Error:\n{ex.Message}", "Database Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }));
                }
            });
        }

        private void main_Resize(object sender, EventArgs e)
        {
            PositionButtonsRight();
        }

        private void PositionButtonsRight()
        {
            int marginRight = 150; // distance from right edge
            int marginTop = 250;  // distance from top edge for first button
            int spaceBetween = 40; // gap between buttons

            // Calculate the right position for all buttons
            int rightPosition = this.ClientSize.Width - register_btn.Width - marginRight;

            // Position Fingerprint Registration button
            register_btn.Left = rightPosition;
            register_btn.Top = marginTop;

            // Position Verify Student button (below first)
            verify_btn.Left = rightPosition;
            verify_btn.Top = register_btn.Bottom + spaceBetween;

            // Position Student Attendance button (below second)
            attendance_btn.Left = rightPosition;
            attendance_btn.Top = verify_btn.Bottom + spaceBetween;
        }

        private void attendance_btn_Click(object sender, EventArgs e)
        {
            try
            {
                events attendanceForm = new events();
                attendanceForm.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Error opening verification form: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}