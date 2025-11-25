using DPFP;
using System;
using System.Windows.Forms;
using MySql.Data.MySqlClient;
using System.IO;
using System.Linq;
using System.Drawing;

namespace BiometricsFingerprint
{
    public partial class verify : capture
    {
        public verify()
        {
            InitializeComponent();

            // Set form to maximize when shown
            this.Load += Verify_Load;
            this.Shown += Verify_Shown;
        }

        private void Verify_Load(object sender, EventArgs e)
        {
            // Set form properties for full screen but KEEP the border for X button
            this.WindowState = FormWindowState.Normal; // First set to normal
            this.FormBorderStyle = FormBorderStyle.Sizable; // Keep borders for X button
            this.Bounds = Screen.PrimaryScreen.Bounds; // Set to primary screen bounds
        }

        private void Verify_Shown(object sender, EventArgs e)
        {
            // Ensure it's truly full screen after showing
            this.WindowState = FormWindowState.Maximized;
            this.TopMost = true; // Bring to front
            this.TopMost = false; // Release topmost but keep focused
            this.Focus();
        }

        protected override void Init()
        {
            base.Init();
            this.Text = "Fingerprint Verification";

            // Ensure full screen but KEEP the border
            this.WindowState = FormWindowState.Maximized;
            this.FormBorderStyle = FormBorderStyle.Sizable; // Keep borders for X button
            this.Bounds = Screen.PrimaryScreen.Bounds;

            SetStatus("Ready to scan fingerprint");
            SafeMakeReport("Place your finger on the scanner");
        }

        // Add a method to handle exit from full screen if needed
        private void ExitFullScreen()
        {
            this.FormBorderStyle = FormBorderStyle.Sizable;
            this.WindowState = FormWindowState.Normal;
        }

        // You might want to add a key handler to exit full screen with ESC
        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Escape)
            {
                ExitFullScreen();
            }
        }

        // The rest of your existing code remains the same...
        protected override void Process(Sample sample)
        {
            base.Process(sample);
            SafeMakeReport("Processing fingerprint...");

            try
            {
                // Extract features for VERIFICATION
                var features = ExtractFeatures(sample, DPFP.Processing.DataPurpose.Verification);

                if (features != null)
                {
                    SafeMakeReport("✓ Features extracted");
                    CheckForMatch(features);
                }
                else
                {
                    SafeMakeReport("✗ Failed to extract features");
                }
            }
            catch (Exception ex)
            {
                SafeMakeReport($"Error: {ex.Message}");
            }
        }

        private void CheckForMatch(DPFP.FeatureSet features)
        {
            string connectionString = DatabaseConfig.ConnectionString;
            string query = "SELECT uid, student_name, course, year_level, fingerprint_data FROM register_student";

            try
            {
                using (var connection = new MySqlConnection(connectionString))
                using (var command = new MySqlCommand(query, connection))
                {
                    connection.Open();

                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string uid = reader["uid"].ToString();
                            string name = reader["student_name"].ToString();
                            string course = reader["course"].ToString();
                            string year = reader["year_level"].ToString();

                            // Convert numeric year to display format
                            string yearDisplay = GetYearLevelDisplay(year);

                            if (reader["fingerprint_data"] is byte[] templateData && templateData.Length > 0)
                            {
                                if (VerifyTemplate(features, templateData))
                                {
                                    // SHOW SUCCESS ON UI THREAD - PASS COURSE AND YEAR DISPLAY
                                    if (this.InvokeRequired)
                                    {
                                        this.Invoke(new Action(() =>
                                        {
                                            OnMatchFound(name, uid, course, yearDisplay);
                                        }));
                                    }
                                    else
                                    {
                                        OnMatchFound(name, uid, course, yearDisplay);
                                    }
                                    return;
                                }
                            }
                        }
                    }
                }

                // No match found - SHOW ON UI THREAD
                if (this.InvokeRequired)
                {
                    this.Invoke(new Action(() =>
                    {
                        SafeMakeReport("✗ No matching fingerprint found");
                        MessageBox.Show(this, "Verification failed. No match found.", "Access Denied",
                                      MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }));
                }
                else
                {
                    SafeMakeReport("✗ No matching fingerprint found");
                    MessageBox.Show(this, "Verification failed. No match found.", "Access Denied",
                                  MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            catch (Exception ex)
            {
                SafeMakeReport($"Database error: {ex.Message}");
            }
        }

        private bool VerifyTemplate(DPFP.FeatureSet features, byte[] templateData)
        {
            try
            {
                using (var stream = new MemoryStream(templateData))
                {
                    var storedTemplate = new DPFP.Template();
                    storedTemplate.DeSerialize(stream);

                    var result = new DPFP.Verification.Verification.Result();
                    var verificator = new DPFP.Verification.Verification();

                    verificator.Verify(features, storedTemplate, ref result);

                    SafeMakeReport($"Verification score: {result.FARAchieved}");
                    return result.Verified;
                }
            }
            catch (Exception ex)
            {
                SafeMakeReport($"Verification error: {ex.Message}");
                return false;
            }
        }

        private void OnMatchFound(string name, string uid, string course, string yearDisplay)
        {
            SafeMakeReport($"✓ VERIFICATION SUCCESSFUL!");
            SafeMakeReport($"Student: {name}");
            SafeMakeReport($"UID: {uid}");
            SafeMakeReport($"Course: {course}");
            SafeMakeReport($"Year: {yearDisplay}");

            // Update UI fields
            SetVerifiedUID(uid);
            SetVerifiedName(name);
            SetVerifiedCourse(course);
            SetVerifiedYear(yearDisplay);

            // CREATE CUSTOM GREEN DIALOG
            ShowGreenSuccessDialog(name, uid, course, yearDisplay);
        }

        private void ShowGreenSuccessDialog(string name, string uid, string course, string yearDisplay)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(() => ShowGreenSuccessDialog(name, uid, course, yearDisplay)));
                return;
            }

            // Create custom form for green dialog
            Form greenDialog = new Form()
            {
                Text = "Verification Successful",
                Size = new System.Drawing.Size(450, 300),
                StartPosition = FormStartPosition.CenterScreen,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false,
                BackColor = System.Drawing.Color.DarkGreen, // Green background
                ForeColor = System.Drawing.Color.White // White text
            };

            // Add title label
            Label titleLabel = new Label()
            {
                Text = "VERIFICATION SUCCESSFUL:",
                Font = new System.Drawing.Font("Arial", 14, System.Drawing.FontStyle.Bold),
                ForeColor = System.Drawing.Color.White,
                AutoSize = true,
                Location = new System.Drawing.Point(20, 20)
            };

            // Add details labels
            Label nameLabel = new Label()
            {
                Text = $"Student: {name}",
                Font = new System.Drawing.Font("Arial", 11),
                ForeColor = System.Drawing.Color.White,
                AutoSize = true,
                Location = new System.Drawing.Point(20, 60)
            };

            Label uidLabel = new Label()
            {
                Text = $"UID: {uid}",
                Font = new System.Drawing.Font("Arial", 11),
                ForeColor = System.Drawing.Color.White,
                AutoSize = true,
                Location = new System.Drawing.Point(20, 90)
            };

            Label courseLabel = new Label()
            {
                Text = $"Course: {course}",
                Font = new System.Drawing.Font("Arial", 11),
                ForeColor = System.Drawing.Color.White,
                AutoSize = true,
                Location = new System.Drawing.Point(20, 120)
            };

            Label yearLabel = new Label()
            {
                Text = $"Year: {yearDisplay}",
                Font = new System.Drawing.Font("Arial", 11),
                ForeColor = System.Drawing.Color.White,
                AutoSize = true,
                Location = new System.Drawing.Point(20, 150)
            };

            // Add OK button
            Button okButton = new Button()
            {
                Text = "OK",
                Size = new System.Drawing.Size(100, 35),
                Location = new System.Drawing.Point(165, 190),
                BackColor = System.Drawing.Color.White,
                ForeColor = System.Drawing.Color.LimeGreen,
                Font = new System.Drawing.Font("Arial", 10, System.Drawing.FontStyle.Bold),
                DialogResult = DialogResult.OK
            };

            okButton.Click += (sender, e) =>
            {
                greenDialog.DialogResult = DialogResult.OK;
                greenDialog.Close();
            };

            // Add controls to form
            greenDialog.Controls.Add(titleLabel);
            greenDialog.Controls.Add(nameLabel);
            greenDialog.Controls.Add(uidLabel);
            greenDialog.Controls.Add(courseLabel);
            greenDialog.Controls.Add(yearLabel);
            greenDialog.Controls.Add(okButton);

            // Accept button for Enter key
            greenDialog.AcceptButton = okButton;

            // Show dialog and close form when OK is clicked
            if (greenDialog.ShowDialog() == DialogResult.OK)
            {
                this.DialogResult = DialogResult.OK;
                this.Close();
            }
        }

        private void SetVerifiedUID(string uid)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(() =>
                {
                    var textBox = this.Controls.Find("uid", true).FirstOrDefault() as TextBox;
                    if (textBox != null) textBox.Text = uid;
                }));
            }
            else
            {
                var textBox = this.Controls.Find("uid", true).FirstOrDefault() as TextBox;
                if (textBox != null) textBox.Text = uid;
            }
        }

        private void SetVerifiedName(string name)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(() =>
                {
                    var textBox = this.Controls.Find("fname", true).FirstOrDefault() as TextBox;
                    if (textBox != null) textBox.Text = name;
                }));
            }
            else
            {
                var textBox = this.Controls.Find("fname", true).FirstOrDefault() as TextBox;
                if (textBox != null) textBox.Text = name;
            }
        }

        private void SetVerifiedCourse(string course)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(() =>
                {
                    var courseControl = this.Controls.Find("listBox1", true).FirstOrDefault();
                    if (courseControl is ListBox listBox)
                    {
                        listBox.SelectedItem = course;
                    }
                }));
            }
            else
            {
                var courseControl = this.Controls.Find("listBox1", true).FirstOrDefault();
                if (courseControl is ListBox listBox)
                {
                    listBox.SelectedItem = course;
                }
            }
        }

        private void SetVerifiedYear(string yearDisplay)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(() =>
                {
                    var yearControl = this.Controls.Find("listBox2", true).FirstOrDefault();
                    if (yearControl is ListBox listBox)
                    {
                        listBox.SelectedItem = yearDisplay;
                    }
                }));
            }
            else
            {
                var yearControl = this.Controls.Find("listBox2", true).FirstOrDefault();
                if (yearControl is ListBox listBox)
                {
                    listBox.SelectedItem = yearDisplay;
                }
            }
        }

        private void SafeMakeReport(string message)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(() => MakeReport(message)));
            }
            else
            {
                MakeReport(message);
            }
        }

        // Add the GetYearLevelDisplay method to convert numeric years to display format
        private string GetYearLevelDisplay(string yearLevel)
        {
            switch (yearLevel)
            {
                case "1": return "1st Year";
                case "2": return "2nd Year";
                case "3": return "3rd Year";
                case "4": return "4th Year";
                case "5": return "5th Year";
                default: return yearLevel;
            }
        }

        private void verify_Load_1(object sender, EventArgs e)
        {

        }
    }
}