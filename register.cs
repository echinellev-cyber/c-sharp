using System;
using System.Data;
using System.IO;
using System.Windows.Forms;
using MySql.Data.MySqlClient;
using DPFP;
using DPFP.Processing;
using System.Configuration;
using System.Net.Http;
using System.Threading.Tasks;
using System.Drawing;

namespace BiometricsFingerprint
{
    public partial class register : capture
    {
        public delegate void OnTemplateEventHandler(DPFP.Template template);
        public event OnTemplateEventHandler OnTemplate;
        private DPFP.Processing.Enrollment Enroller;

        public register()
        {
            InitializeComponent();
            this.WindowState = FormWindowState.Maximized;
            this.Load += (sender, e) => { this.WindowState = FormWindowState.Maximized; };
        }

        protected override void Init()
        {
            try
            {
                base.Init();
                this.Text = "Fingerprint Registration";
                this.WindowState = FormWindowState.Maximized;
                Enroller = new DPFP.Processing.Enrollment();
                UpdateStatus();
                SafeMakeReport("Ready for fingerprint registration");
            }
            catch (System.DllNotFoundException ex)
            {
                MessageBox.Show(this, $"SDK Error: {ex.Message}\n\nPlease install the fingerprint SDK correctly.");
                this.Close();
            }
        }

        protected override void Process(DPFP.Sample Sample)
        {
            base.Process(Sample);

            DPFP.FeatureSet features = ExtractFeatures(Sample, DPFP.Processing.DataPurpose.Enrollment);

            if (features != null)
            {
                try
                {
                    SafeMakeReport("✓ Fingerprint features extracted");
                    Enroller.AddFeatures(features);
                }
                finally
                {
                    UpdateStatus();
                    HandleEnrollmentResult();
                }
            }
        }

        private void HandleEnrollmentResult()
        {
            switch (Enroller.TemplateStatus)
            {
                case DPFP.Processing.Enrollment.Status.Ready:
                    HandleSuccessfulEnrollment();
                    break;

                case DPFP.Processing.Enrollment.Status.Failed:
                    HandleFailedEnrollment();
                    break;
            }
        }

        private void HandleSuccessfulEnrollment()
        {
            try
            {
                using (MemoryStream fingerprintData = new MemoryStream())
                {
                    Enroller.Template.Serialize(fingerprintData);
                    byte[] templateBytes = fingerprintData.ToArray();

                    if (!IsTemplateValid(templateBytes))
                    {
                        SafeMakeReport("✗ Template validation failed. Please scan again.");
                        ResetCapture();
                        return;
                    }

                    SafeMakeReport($"✓ Template created: {templateBytes.Length} bytes");

                    // **STRICT DUPLICATE CHECK - WILL BLOCK ANY SIMILAR FINGERPRINTS**
                    string duplicateInfo = CheckFingerprintDuplicate(templateBytes);
                    if (!string.IsNullOrEmpty(duplicateInfo))
                    {
                        SafeMakeReport("✗ FINGERPRINT DUPLICATE DETECTED!");

                        // Show blocking error message
                        if (this.InvokeRequired)
                        {
                            this.Invoke(new Action(() =>
                            {
                                using (var errorForm = new Form())
                                {
                                    errorForm.Text = "DUPLICATE FINGERPRINT DETECTED";
                                    errorForm.Size = new Size(600, 400);
                                    errorForm.StartPosition = FormStartPosition.CenterParent;
                                    errorForm.BackColor = Color.FromArgb(255, 240, 240);
                                    errorForm.FormBorderStyle = FormBorderStyle.FixedDialog;
                                    errorForm.MaximizeBox = false;
                                    errorForm.MinimizeBox = false;

                                    // Create fonts
                                    Font poppinsFont = new Font("Segoe UI", 11, FontStyle.Regular);
                                    Font poppinsBold = new Font("Segoe UI", 11, FontStyle.Bold);
                                    Font poppinsTitle = new Font("Segoe UI", 14, FontStyle.Bold);

                                    // Header panel with background
                                    Panel headerPanel = new Panel();
                                    headerPanel.BackColor = Color.FromArgb(255, 200, 200);
                                    headerPanel.Size = new Size(600, 70);
                                    headerPanel.Location = new Point(0, 0);
                                    errorForm.Controls.Add(headerPanel);

                                    // Title label
                                    Label titleLabel = new Label();
                                    titleLabel.Text = "✗ DUPLICATE FINGERPRINT DETECTED";
                                    titleLabel.Font = poppinsTitle;
                                    titleLabel.ForeColor = Color.FromArgb(200, 0, 0);
                                    titleLabel.AutoSize = true;
                                    titleLabel.Location = new Point(20, 25);
                                    errorForm.Controls.Add(titleLabel);
                                    titleLabel.BringToFront();

                                    // Main message label
                                    Label messageLabel = new Label();
                                    messageLabel.Text = "This fingerprint is already registered to another student!";
                                    messageLabel.Font = poppinsBold;
                                    messageLabel.ForeColor = Color.Black;
                                    messageLabel.AutoSize = false;
                                    messageLabel.Size = new Size(560, 30);
                                    messageLabel.Location = new Point(20, 90);
                                    messageLabel.TextAlign = ContentAlignment.MiddleLeft;
                                    errorForm.Controls.Add(messageLabel);

                                    // Details section with the duplicate info
                                    Label detailsLabel = new Label();
                                    detailsLabel.Text = $"Duplicate Found:\n{duplicateInfo}\n\nThis fingerprint cannot be registered to multiple students.\nRegistration blocked.";
                                    detailsLabel.Font = poppinsFont;
                                    detailsLabel.ForeColor = Color.Black;
                                    detailsLabel.AutoSize = false;
                                    detailsLabel.Size = new Size(560, 180);
                                    detailsLabel.Location = new Point(20, 130);
                                    detailsLabel.TextAlign = ContentAlignment.TopLeft;
                                    errorForm.Controls.Add(detailsLabel);

                                    // OK button
                                    Button okButton = new Button();
                                    okButton.Text = "OK";
                                    okButton.Size = new Size(120, 40);
                                    okButton.Location = new Point(240, 320);
                                    okButton.Font = poppinsBold;
                                    okButton.BackColor = Color.FromArgb(200, 0, 0);
                                    okButton.ForeColor = Color.White;
                                    okButton.FlatStyle = FlatStyle.Flat;
                                    okButton.FlatAppearance.BorderSize = 0;
                                    okButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(220, 0, 0);
                                    okButton.FlatAppearance.MouseDownBackColor = Color.FromArgb(180, 0, 0);
                                    okButton.Cursor = Cursors.Hand;
                                    okButton.DialogResult = DialogResult.OK;
                                    errorForm.Controls.Add(okButton);

                                    errorForm.AcceptButton = okButton;
                                    errorForm.ShowDialog();
                                }
                            }));
                        }
                        else
                        {
                            using (var errorForm = new Form())
                            {
                                errorForm.Text = "DUPLICATE FINGERPRINT DETECTED";
                                errorForm.Size = new Size(600, 400);
                                errorForm.StartPosition = FormStartPosition.CenterParent;
                                errorForm.BackColor = Color.FromArgb(255, 240, 240);
                                errorForm.FormBorderStyle = FormBorderStyle.FixedDialog;
                                errorForm.MaximizeBox = false;
                                errorForm.MinimizeBox = false;

                                Font poppinsFont = new Font("Segoe UI", 11, FontStyle.Regular);
                                Font poppinsBold = new Font("Segoe UI", 11, FontStyle.Bold);
                                Font poppinsTitle = new Font("Segoe UI", 14, FontStyle.Bold);

                                Panel headerPanel = new Panel();
                                headerPanel.BackColor = Color.FromArgb(255, 200, 200);
                                headerPanel.Size = new Size(600, 70);
                                headerPanel.Location = new Point(0, 0);
                                errorForm.Controls.Add(headerPanel);

                                Label titleLabel = new Label();
                                titleLabel.Text = "✗ DUPLICATE FINGERPRINT DETECTED";
                                titleLabel.Font = poppinsTitle;
                                titleLabel.ForeColor = Color.FromArgb(200, 0, 0);
                                titleLabel.AutoSize = true;
                                titleLabel.Location = new Point(20, 25);
                                errorForm.Controls.Add(titleLabel);
                                titleLabel.BringToFront();

                                Label messageLabel = new Label();
                                messageLabel.Text = "This fingerprint is already registered to another student!";
                                messageLabel.Font = poppinsBold;
                                messageLabel.ForeColor = Color.Black;
                                messageLabel.AutoSize = false;
                                messageLabel.Size = new Size(560, 30);
                                messageLabel.Location = new Point(20, 90);
                                messageLabel.TextAlign = ContentAlignment.MiddleLeft;
                                errorForm.Controls.Add(messageLabel);

                                Label detailsLabel = new Label();
                                detailsLabel.Text = $"Duplicate Found:\n{duplicateInfo}\n\nThis fingerprint cannot be registered to multiple students.\nRegistration blocked.";
                                detailsLabel.Font = poppinsFont;
                                detailsLabel.ForeColor = Color.Black;
                                detailsLabel.AutoSize = false;
                                detailsLabel.Size = new Size(560, 180);
                                detailsLabel.Location = new Point(20, 130);
                                detailsLabel.TextAlign = ContentAlignment.TopLeft;
                                errorForm.Controls.Add(detailsLabel);

                                Button okButton = new Button();
                                okButton.Text = "OK";
                                okButton.Size = new Size(120, 40);
                                okButton.Location = new Point(240, 320);
                                okButton.Font = poppinsBold;
                                okButton.BackColor = Color.FromArgb(200, 0, 0);
                                okButton.ForeColor = Color.White;
                                okButton.FlatStyle = FlatStyle.Flat;
                                okButton.FlatAppearance.BorderSize = 0;
                                okButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(220, 0, 0);
                                okButton.FlatAppearance.MouseDownBackColor = Color.FromArgb(180, 0, 0);
                                okButton.Cursor = Cursors.Hand;
                                okButton.DialogResult = DialogResult.OK;
                                errorForm.Controls.Add(okButton);

                                errorForm.AcceptButton = okButton;
                                errorForm.ShowDialog();
                            }
                        }

                        ResetCapture();
                        Enroller.Clear();
                        return;
                    }

                    // **ONLY REACH THIS POINT IF NO DUPLICATE FOUND**
                    string studentUid = "";
                    string fullName = "";
                    string selectedCourse = "";
                    string selectedYear = "";

                    if (this.InvokeRequired)
                    {
                        this.Invoke(new Action(() =>
                        {
                            studentUid = StudentUid;
                            fullName = FullName;
                            selectedCourse = SelectedCourse;
                            selectedYear = SelectedYearLevel;
                        }));
                    }
                    else
                    {
                        studentUid = StudentUid;
                        fullName = FullName;
                        selectedCourse = SelectedCourse;
                        selectedYear = SelectedYearLevel;
                    }

                    if (string.IsNullOrEmpty(studentUid) || string.IsNullOrEmpty(fullName))
                    {
                        SafeMakeReport("✗ Please fill in all required fields (UID and Name)");
                        return;
                    }

                    // **SAVE TO DATABASE ONLY IF NO DUPLICATE**
                    SaveToDatabase(studentUid, fullName, selectedCourse, selectedYear, templateBytes);
                    SafeMakeReport($"✓ {fullName} registered successfully!");

                    Stop();

                    if (this.InvokeRequired)
                    {
                        this.Invoke(new Action(() =>
                        {
                            ShowSuccessMessage(fullName, studentUid, selectedCourse, selectedYear);
                        }));
                    }
                    else
                    {
                        ShowSuccessMessage(fullName, studentUid, selectedCourse, selectedYear);
                    }
                }
            }
            catch (Exception ex)
            {
                SafeMakeReport($"✗ Enrollment error: {ex.Message}");

                if (this.InvokeRequired)
                {
                    this.Invoke(new Action(() =>
                    {
                        MessageBox.Show(this, $"Registration failed: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }));
                }
                else
                {
                    MessageBox.Show(this, $"Registration failed: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        // **STRICT DUPLICATE CHECK - DETECTS ANY SIMILAR FINGERPRINTS**
        private string CheckFingerprintDuplicate(byte[] newTemplate)
        {
            try
            {
                string connectionString = "server=localhost;user id=root;password=;database=biometric;SslMode=None;";

                string byteQuery = @"SELECT student_name, uid, fingerprint_data, course
                           FROM register_student 
                           WHERE fingerprint_data IS NOT NULL 
                           AND LENGTH(fingerprint_data) > 0";

                using (MySqlConnection connection = new MySqlConnection(connectionString))
                {
                    connection.Open();

                    using (MySqlCommand byteCommand = new MySqlCommand(byteQuery, connection))
                    using (MySqlDataReader reader = byteCommand.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            if (!reader.IsDBNull(reader.GetOrdinal("fingerprint_data")))
                            {
                                byte[] existingTemplate = (byte[])reader["fingerprint_data"];
                                string existingStudentName = reader["student_name"].ToString();
                                string existingStudentUid = reader["uid"].ToString();
                                string existingCourse = reader["course"]?.ToString() ?? "N/A";

                                // **STRICT COMPARISON - CATCHES ANY SIMILARITY**
                                if (IsFingerprintDuplicateStrict(newTemplate, existingTemplate))
                                {
                                    SafeMakeReport($"✗ STRICT DUPLICATE: Fingerprint matches {existingStudentName} (UID: {existingStudentUid})");
                                    return $"STUDENT: {existingStudentName}\nUID: {existingStudentUid}\nCOURSE: {existingCourse}";
                                }
                            }
                        }
                    }
                }

                SafeMakeReport("✓ No duplicate fingerprints found");
                return null;
            }
            catch (Exception ex)
            {
                SafeMakeReport($"⚠ Error checking duplicate: {ex.Message}");
                // If we can't check, BLOCK registration for safety
                return "Error checking duplicates. Registration blocked for security.";
            }
        }

        // **STRICT FINGERPRINT COMPARISON - CATCHES ALL DUPLICATES**
        private bool IsFingerprintDuplicateStrict(byte[] template1, byte[] template2)
        {
            // 1. First check exact match (fastest)
            if (AreTemplatesIdentical(template1, template2))
            {
                SafeMakeReport("✗ Exact template match found!");
                return true;
            }

            // 2. Check if templates are very similar in size (similar fingerprints usually have similar sizes)
            double sizeDifference = Math.Abs((double)template1.Length - template2.Length) / Math.Max(template1.Length, template2.Length);
            if (sizeDifference > 0.3) // More than 30% size difference
            {
                return false;
            }

            // 3. Check multiple sections of the template for similarity
            int checkSections = 5;
            int sectionSize = Math.Min(template1.Length, template2.Length) / checkSections;
            int matchingSections = 0;

            for (int section = 0; section < checkSections; section++)
            {
                int start = section * sectionSize;
                int end = Math.Min(start + sectionSize, Math.Min(template1.Length, template2.Length));
                int matchesInSection = 0;

                for (int i = start; i < end; i++)
                {
                    if (template1[i] == template2[i])
                        matchesInSection++;
                }

                double sectionSimilarity = (double)matchesInSection / (end - start);
                if (sectionSimilarity >= 0.70) // 70% similarity in this section
                {
                    matchingSections++;
                }
            }

            // 4. Check overall similarity
            int minLength = Math.Min(template1.Length, template2.Length);
            int totalMatches = 0;

            for (int i = 0; i < minLength; i++)
            {
                if (template1[i] == template2[i])
                    totalMatches++;
            }

            double overallSimilarity = (double)totalMatches / minLength;

            SafeMakeReport($"Similarity Analysis:\n- Size Difference: {sizeDifference:P2}\n- Matching Sections: {matchingSections}/{checkSections}\n- Overall Similarity: {overallSimilarity:P2}");

            // **STRICT CONDITIONS FOR DUPLICATE DETECTION**
            bool isDuplicate = (overallSimilarity >= 0.75) || // 75% overall similarity
                              (matchingSections >= 3) ||      // 3+ matching sections
                              (sizeDifference <= 0.1 && overallSimilarity >= 0.65); // Small size difference + decent similarity

            if (isDuplicate)
            {
                SafeMakeReport($"✗ STRICT DUPLICATE DETECTED! Similarity: {overallSimilarity:P2}");
            }

            return isDuplicate;
        }

        // **EXACT MATCH CHECK**
        private bool AreTemplatesIdentical(byte[] template1, byte[] template2)
        {
            if (template1.Length != template2.Length)
                return false;

            for (int i = 0; i < template1.Length; i++)
            {
                if (template1[i] != template2[i])
                    return false;
            }

            return true;
        }

        private void ShowSuccessMessage(string fullName, string studentUid, string course, string year)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(() => ShowSuccessMessage(fullName, studentUid, course, year)));
                return;
            }

            using (var successForm = new Form())
            {
                successForm.Text = "ACCOUNT CREATED SUCCESSFULLY";
                successForm.Size = new Size(550, 400);
                successForm.StartPosition = FormStartPosition.CenterParent;
                successForm.BackColor = Color.FromArgb(240, 245, 240);
                successForm.FormBorderStyle = FormBorderStyle.FixedDialog;
                successForm.MaximizeBox = false;
                successForm.MinimizeBox = false;

                Font poppinsFont = new Font("Segoe UI", 11, FontStyle.Regular);
                Font poppinsBold = new Font("Segoe UI", 11, FontStyle.Bold);
                Font poppinsTitle = new Font("Segoe UI", 14, FontStyle.Bold);

                Panel headerPanel = new Panel();
                headerPanel.BackColor = Color.FromArgb(220, 240, 220);
                headerPanel.Size = new Size(550, 60);
                headerPanel.Location = new Point(0, 0);
                successForm.Controls.Add(headerPanel);

                Label titleLabel = new Label();
                titleLabel.Text = "✓ ACCOUNT CREATED SUCCESSFULLY";
                titleLabel.Font = poppinsTitle;
                titleLabel.ForeColor = Color.FromArgb(0, 100, 0);
                titleLabel.AutoSize = true;
                titleLabel.Location = new Point(20, 20);
                successForm.Controls.Add(titleLabel);
                titleLabel.BringToFront();

                string department = SelectedDepartment;
                string yearDisplay = SelectedYearLevelDisplay;

                int yPos = 80;
                int lineHeight = 25;

                Label uidLabel = CreateInfoLabel("ID:", studentUid, yPos, poppinsBold, poppinsFont);
                successForm.Controls.Add(uidLabel);
                yPos += lineHeight;

                Label nameLabel = CreateInfoLabel("NAME:", fullName, yPos, poppinsBold, poppinsFont);
                successForm.Controls.Add(nameLabel);
                yPos += lineHeight;

                Label departmentLabel = CreateInfoLabel("DEPARTMENT:", department, yPos, poppinsBold, poppinsFont);
                successForm.Controls.Add(departmentLabel);
                yPos += lineHeight;

                Label courseLabel = CreateInfoLabel("COURSE:", course, yPos, poppinsBold, poppinsFont);
                successForm.Controls.Add(courseLabel);
                yPos += lineHeight;

                Label yearLabel = CreateInfoLabel("YEAR:", yearDisplay, yPos, poppinsBold, poppinsFont);
                successForm.Controls.Add(yearLabel);
                yPos += lineHeight;

                Label fingerprintLabel = new Label();
                fingerprintLabel.Text = "Fingerprint: Registered successfully";
                fingerprintLabel.Font = poppinsBold;
                fingerprintLabel.ForeColor = Color.FromArgb(0, 100, 0);
                fingerprintLabel.AutoSize = true;
                fingerprintLabel.Location = new Point(20, yPos + 10);
                successForm.Controls.Add(fingerprintLabel);

                Button okButton = new Button();
                okButton.Text = "OK";
                okButton.Size = new Size(100, 35);
                okButton.Location = new Point(215, yPos + 50);
                okButton.Font = poppinsBold;
                okButton.BackColor = Color.FromArgb(0, 120, 0);
                okButton.ForeColor = Color.White;
                okButton.FlatStyle = FlatStyle.Flat;
                okButton.FlatAppearance.BorderSize = 0;
                okButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(0, 150, 0);
                okButton.FlatAppearance.MouseDownBackColor = Color.FromArgb(0, 100, 0);
                okButton.Cursor = Cursors.Hand;
                okButton.DialogResult = DialogResult.OK;
                successForm.Controls.Add(okButton);

                successForm.AcceptButton = okButton;

                if (successForm.ShowDialog() == DialogResult.OK)
                {
                    ClearAllFormFields();
                }

                poppinsFont.Dispose();
                poppinsBold.Dispose();
                poppinsTitle.Dispose();
            }
        }

        private Label CreateInfoLabel(string label, string value, int yPos, Font boldFont, Font regularFont)
        {
            var combinedLabel = new Label();
            combinedLabel.Text = $"{label} {value}";
            combinedLabel.Font = regularFont;
            combinedLabel.ForeColor = Color.Black;
            combinedLabel.AutoSize = true;
            combinedLabel.Location = new Point(20, yPos);
            return combinedLabel;
        }

        private void ClearAllFormFields()
        {
            try
            {
                if (this.InvokeRequired)
                {
                    this.Invoke(new Action(ClearAllFormFields));
                    return;
                }

                Enroller = new DPFP.Processing.Enrollment();
                UpdateStatus();
                SafeMakeReport("Ready for new fingerprint registration");
                ResetCapture();
                ClearAllControls(this);
                SafeMakeReport("Form cleared. Ready for new student registration.");
            }
            catch (Exception ex)
            {
                SafeMakeReport($"Error clearing form: {ex.Message}");
            }
        }

        private void ClearAllControls(Control control)
        {
            foreach (Control c in control.Controls)
            {
                if (c is TextBox) ((TextBox)c).Text = "";
                else if (c is ComboBox) ((ComboBox)c).SelectedIndex = -1;
                else if (c is CheckBox) ((CheckBox)c).Checked = false;

                if (c.HasChildren) ClearAllControls(c);
            }
        }

        private bool IsTemplateValid(byte[] templateData)
        {
            try
            {
                if (templateData.Length < 500 || templateData.Length > 5000)
                {
                    SafeMakeReport($"Invalid template size: {templateData.Length} bytes");
                    return false;
                }

                using (MemoryStream testStream = new MemoryStream(templateData))
                {
                    DPFP.Template testTemplate = new DPFP.Template();
                    testTemplate.DeSerialize(testStream);

                    if (testTemplate.Size == 0)
                    {
                        SafeMakeReport("Template deserialized but has zero size");
                        return false;
                    }

                    SafeMakeReport($"✓ Template validated: {testTemplate.Size} bytes");
                    return true;
                }
            }
            catch (Exception ex)
            {
                SafeMakeReport($"Template validation failed: {ex.Message}");
                return false;
            }
        }

        private void SaveToDatabase(string uid, string fullName, string course, string year, byte[] fingerprintData)
        {
            try
            {
                string connectionString = "server=localhost;user id=root;password=;database=biometric;SslMode=None;";

                // Check if student exists AND check fingerprint status
                string checkQuery = @"SELECT COUNT(*) as count, 
                             fingerprint_data 
                             FROM register_student 
                             WHERE uid = @uid";

                string updateQuery = @"UPDATE register_student 
                              SET student_name = @name, 
                                  course = @course, 
                                  year_level = @year, 
                                  fingerprint_data = @fingerprint,
                                  last_updated = NOW()
                              WHERE uid = @uid";

                string insertQuery = @"INSERT INTO register_student 
                            (uid, student_name, course, year_level, fingerprint_data) 
                            VALUES (@uid, @name, @course, @year, @fingerprint)";

                using (MySqlConnection connection = new MySqlConnection(connectionString))
                {
                    connection.Open();

                    using (MySqlCommand checkCommand = new MySqlCommand(checkQuery, connection))
                    {
                        checkCommand.Parameters.AddWithValue("@uid", uid);

                        using (MySqlDataReader reader = checkCommand.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                int existingCount = reader.GetInt32(0);
                                bool hasFingerprint = !reader.IsDBNull(reader.GetOrdinal("fingerprint_data"));

                                reader.Close(); // Close reader before executing other commands

                                if (existingCount > 0)
                                {
                                    // Student exists - check if they have fingerprint data
                                    if (hasFingerprint)
                                    {
                                        SafeMakeReport($"✗ Student ID {uid} already has fingerprint registered!");

                                        if (this.InvokeRequired)
                                        {
                                            this.Invoke(new Action(() =>
                                            {
                                                MessageBox.Show(this,
                                                    $"Student ID {uid} already has fingerprint registered!\n\nCannot register duplicate fingerprint.",
                                                    "Fingerprint Already Exists",
                                                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                                            }));
                                        }
                                        throw new Exception($"Student ID {uid} already has fingerprint registered.");
                                    }
                                    else
                                    {
                                        // Student exists but has no fingerprint - UPDATE their record
                                        using (MySqlCommand updateCommand = new MySqlCommand(updateQuery, connection))
                                        {
                                            updateCommand.Parameters.AddWithValue("@uid", uid);
                                            updateCommand.Parameters.AddWithValue("@name", fullName);
                                            updateCommand.Parameters.AddWithValue("@course", course);
                                            updateCommand.Parameters.AddWithValue("@year", year);
                                            updateCommand.Parameters.AddWithValue("@fingerprint", fingerprintData);

                                            int rowsUpdated = updateCommand.ExecuteNonQuery();

                                            if (rowsUpdated > 0)
                                            {
                                                SafeMakeReport($"✓ Updated fingerprint for existing student: {fullName}");
                                            }
                                            else
                                            {
                                                throw new Exception("Failed to update student fingerprint.");
                                            }
                                        }
                                    }
                                }
                                else
                                {
                                    // Student doesn't exist - INSERT new record
                                    using (MySqlCommand insertCommand = new MySqlCommand(insertQuery, connection))
                                    {
                                        insertCommand.Parameters.AddWithValue("@uid", uid);
                                        insertCommand.Parameters.AddWithValue("@name", fullName);
                                        insertCommand.Parameters.AddWithValue("@course", course);
                                        insertCommand.Parameters.AddWithValue("@year", year);
                                        insertCommand.Parameters.AddWithValue("@fingerprint", fingerprintData);

                                        insertCommand.ExecuteNonQuery();
                                        SafeMakeReport($"✓ Added new student with fingerprint: {fullName}");
                                    }
                                }
                            }
                            else
                            {
                                // No record found - INSERT new record
                                reader.Close();

                                using (MySqlCommand insertCommand = new MySqlCommand(insertQuery, connection))
                                {
                                    insertCommand.Parameters.AddWithValue("@uid", uid);
                                    insertCommand.Parameters.AddWithValue("@name", fullName);
                                    insertCommand.Parameters.AddWithValue("@course", course);
                                    insertCommand.Parameters.AddWithValue("@year", year);
                                    insertCommand.Parameters.AddWithValue("@fingerprint", fingerprintData);

                                    insertCommand.ExecuteNonQuery();
                                    SafeMakeReport($"✓ Added new student with fingerprint: {fullName}");
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                SafeMakeReport($"✗ Database error: {ex.Message}");
                throw;
            }
        }

        private void HandleFailedEnrollment()
        {
            Enroller.Clear();
            SafeMakeReport("✗ Enrollment failed. Please try again.");
            ResetCapture();
        }

        private void UpdateStatus()
        {
            string statusText = $"Samples needed: {Enroller.FeaturesNeeded}";
            SetStatus(statusText);
        }

        private void ResetCapture()
        {
            Stop();
            Start();
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

        private void register_Load(object sender, EventArgs e)
        {
            // Initialization
        }
    }
}