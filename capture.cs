using DPFP;
using DPFP.Capture;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using MySql.Data.MySqlClient;

namespace BiometricsFingerprint
{
    public partial class capture : Form, DPFP.Capture.EventHandler
    {
        private DPFP.Capture.Capture Capturer;
        public string FullName = "";

        public string StudentUid
        {
            get { return uid != null ? uid.Text.Trim() : string.Empty; }
        }

        public string SelectedCourse
        {
            get
            {
                if (comboBox1 != null && comboBox1.SelectedItem != null)
                {
                    return comboBox1.SelectedItem.ToString();
                }
                return string.Empty;
            }
        }

        public string SelectedCourseCode
        {
            get
            {
                if (comboBox1 != null && comboBox1.SelectedItem != null)
                {
                    string selected = comboBox1.SelectedItem.ToString();
                    // Since we removed acronyms, return empty or the first few characters if needed
                    return string.Empty;
                }
                return string.Empty;
            }
        }

        public string SelectedYearLevel
        {
            get
            {
                if (comboBox2 != null && comboBox2.SelectedItem != null)
                {
                    return MapYearLevel(comboBox2.SelectedItem.ToString());
                }
                return string.Empty;
            }
        }

        public string SelectedYearLevelDisplay
        {
            get
            {
                if (comboBox2 != null && comboBox2.SelectedItem != null)
                {
                    return comboBox2.SelectedItem.ToString();
                }
                return string.Empty;
            }
        }

        public string SelectedDepartment
        {
            get
            {
                if (comboBox3 != null && comboBox3.SelectedItem != null)
                {
                    return comboBox3.SelectedItem.ToString();
                }
                return string.Empty;
            }
        }

        public capture()
        {
            InitializeComponent();
            // Add event handlers
            comboBox1.SelectedIndexChanged += comboBox1_SelectedIndexChanged;
            comboBox2.SelectedIndexChanged += comboBox2_SelectedIndexChanged;
            comboBox3.SelectedIndexChanged += comboBox3_SelectedIndexChanged;

            // Initialize search functionality
            InitializeSearchFunctionality();
        }

        private void InitializeSearchFunctionality()
        {
            // Set up search button click event
            if (searchButton != null)
            {
                searchButton.Click += SearchButton_Click;
            }

            // Set up add button click event
            if (addButton != null)
            {
                addButton.Click += AddButton_Click;
            }

            // Set up enter key press for search textbox
            if (searchTextBox != null)
            {
                searchTextBox.KeyPress += SearchTextBox_KeyPress;
            }
        }

        private void SearchTextBox_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == (char)Keys.Enter)
            {
                SearchButton_Click(sender, e);
                e.Handled = true;
            }
        }

        private void SearchButton_Click(object sender, EventArgs e)
        {
            string searchTerm = searchTextBox.Text.Trim();
            if (string.IsNullOrEmpty(searchTerm))
            {
                MessageBox.Show("Please enter a UID or Name to search.", "Information",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            SearchStudent(searchTerm);
        }

        private void AddButton_Click(object sender, EventArgs e)
        {
            AddStudentWithoutFingerprint();
        }

        private void SearchStudent(string searchTerm)
        {
            try
            {
                string connectionString = DatabaseConfig.ConnectionString;
                string query = @"SELECT uid, student_name, course, year_level, department, 
                                        fingerprint_data, LENGTH(fingerprint_data) as template_size
                                 FROM register_student 
                                 WHERE uid = @searchTerm OR student_name LIKE @searchName";

                using (MySqlConnection connection = new MySqlConnection(connectionString))
                using (MySqlCommand command = new MySqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@searchTerm", searchTerm);
                    command.Parameters.AddWithValue("@searchName", "%" + searchTerm + "%");

                    connection.Open();

                    using (MySqlDataReader reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            // Student found - populate the fields
                            string foundUid = reader["uid"].ToString();
                            string name = reader["student_name"].ToString();
                            string course = reader["course"].ToString();
                            string year = reader["year_level"].ToString();
                            string department = reader["department"].ToString();

                            // Check if student has fingerprint data
                            bool hasFingerprint = !reader.IsDBNull(reader.GetOrdinal("fingerprint_data"));
                            int templateSize = hasFingerprint ? Convert.ToInt32(reader["template_size"]) : 0;

                            // Populate the form fields
                            if (uid != null) uid.Text = foundUid;
                            if (fname != null) fname.Text = name;

                            // Set course
                            if (!string.IsNullOrEmpty(course) && comboBox1 != null)
                            {
                                var courseItem = comboBox1.Items.Cast<string>()
                                    .FirstOrDefault(item => item.Equals(course, StringComparison.OrdinalIgnoreCase));
                                if (courseItem != null)
                                {
                                    comboBox1.SelectedItem = courseItem;
                                }
                            }

                            // Set year level
                            if (!string.IsNullOrEmpty(year) && comboBox2 != null)
                            {
                                string yearDisplay = GetYearLevelDisplay(year);
                                var yearItem = comboBox2.Items.Cast<string>()
                                    .FirstOrDefault(item => item.Equals(yearDisplay, StringComparison.OrdinalIgnoreCase));
                                if (yearItem != null)
                                {
                                    comboBox2.SelectedItem = yearItem;
                                }
                            }

                            // Set department
                            if (!string.IsNullOrEmpty(department) && comboBox3 != null)
                            {
                                var deptItem = comboBox3.Items.Cast<string>()
                                    .FirstOrDefault(item => item.Equals(department, StringComparison.OrdinalIgnoreCase));
                                if (deptItem != null)
                                {
                                    comboBox3.SelectedItem = deptItem;
                                }
                            }

                            // Show appropriate message based on fingerprint status
                            if (hasFingerprint && templateSize > 0)
                            {
                                ShowSuccessMessage(
                                    "Search Successful",
                                    $"Student found: {name} (UID: {foundUid})",
                                    $"Fingerprint: Registered ({templateSize} bytes)"
                                );
                            }
                            else
                            {
                                ShowSuccessMessage(
                                    "Search Successful",
                                    $"Student found: {name} (UID: {foundUid})",
                                    $"Fingerprint: Not Registered\nYou can now register their fingerprint by clicking 'Start Scan'"
                                );
                            }
                        }
                        else
                        {
                            DialogResult result = MessageBox.Show("No student found with the provided UID or Name.", "Not Found",
                                MessageBoxButtons.OK, MessageBoxIcon.Information);

                            // Clear search textbox when OK is clicked
                            if (result == DialogResult.OK)
                            {
                                searchTextBox.Text = "";
                                searchTextBox.Focus();
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                DialogResult result = MessageBox.Show($"Error searching student: {ex.Message}", "Search Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);

                // Clear search textbox when OK is clicked
                if (result == DialogResult.OK)
                {
                    searchTextBox.Text = "";
                    searchTextBox.Focus();
                }
            }
        }

        private void AddStudentWithoutFingerprint()
        {
            try
            {
                string studentUid = StudentUid;
                string fullName = FullName;
                string selectedCourse = SelectedCourse;
                string selectedYear = SelectedYearLevel;
                string selectedDepartment = SelectedDepartment;

                // FIX: Auto-determine department if not manually selected
                if (string.IsNullOrEmpty(selectedDepartment) && !string.IsNullOrEmpty(selectedCourse))
                {
                    selectedDepartment = GetDepartmentByCourse(selectedCourse);
                    SetStatus($"Auto-selected department: {selectedDepartment}");
                }

                // Validate required fields
                if (string.IsNullOrEmpty(studentUid) || string.IsNullOrEmpty(fullName))
                {
                    MessageBox.Show("Please fill in UID and Name fields.", "Validation Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                // Save to database without fingerprint (use NULL instead of empty bytes)
                string connectionString = DatabaseConfig.ConnectionString;

                // Check if student already exists
                string checkQuery = "SELECT COUNT(*) FROM register_student WHERE uid = @uid";

                // Use NULL for fingerprint_data instead of empty bytes
                string insertQuery = @"INSERT INTO register_student 
                                    (uid, student_name, course, year_level, department, fingerprint_data) 
                                    VALUES (@uid, @name, @course, @year, @department, NULL)";

                using (MySqlConnection connection = new MySqlConnection(connectionString))
                {
                    connection.Open();

                    // Check for existing student
                    using (MySqlCommand checkCommand = new MySqlCommand(checkQuery, connection))
                    {
                        checkCommand.Parameters.AddWithValue("@uid", studentUid);
                        int existingCount = Convert.ToInt32(checkCommand.ExecuteScalar());

                        if (existingCount > 0)
                        {
                            MessageBox.Show("Student UID already exists in database.", "Error",
                                MessageBoxButtons.OK, MessageBoxIcon.Error);
                            return;
                        }
                    }

                    // Insert new student with NULL fingerprint_data
                    using (MySqlCommand insertCommand = new MySqlCommand(insertQuery, connection))
                    {
                        insertCommand.Parameters.AddWithValue("@uid", studentUid);
                        insertCommand.Parameters.AddWithValue("@name", fullName);
                        insertCommand.Parameters.AddWithValue("@course", selectedCourse);
                        insertCommand.Parameters.AddWithValue("@year", selectedYear);
                        insertCommand.Parameters.AddWithValue("@department", selectedDepartment); // This should now have a value

                        int rowsAffected = insertCommand.ExecuteNonQuery();

                        if (rowsAffected > 0)
                        {
                            ShowSuccessMessage(
                                "Success",
                                $"Student {fullName} added successfully!",
                                $"UID: {studentUid}\nCourse: {selectedCourse}\nYear: {SelectedYearLevelDisplay}\nDepartment: {selectedDepartment}"
                            );

                            // DON'T clear the fields - keep the information displayed for fingerprint registration
                            // Only clear the search textbox
                            if (searchTextBox != null)
                            {
                                searchTextBox.Text = "";
                                searchTextBox.Focus();
                            }

                            SetStatus($"Student {fullName} added successfully - ready for fingerprint registration");
                        }
                        else
                        {
                            MessageBox.Show("Failed to add student.", "Error",
                                MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error adding student: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // New method to show big success messages with original content
        private void ShowSuccessMessage(string title, string message, string details = "")
        {
            using (var successForm = new Form())
            {
                successForm.Text = title;
                successForm.Size = new Size(550, 350); // Made bigger
                successForm.StartPosition = FormStartPosition.CenterParent;
                successForm.BackColor = Color.FromArgb(240, 245, 240);
                successForm.FormBorderStyle = FormBorderStyle.FixedDialog;
                successForm.MaximizeBox = false;
                successForm.MinimizeBox = false;

                // Create Poppins font (fallback to Segoe UI if not available)
                Font poppinsFont = new Font("Poppins", 12, FontStyle.Regular);
                Font poppinsBold = new Font("Poppins", 12, FontStyle.Bold);
                Font poppinsTitle = new Font("Poppins", 16, FontStyle.Bold);

                // If Poppins is not installed, use Segoe UI
                if (poppinsFont.Name != "Poppins")
                {
                    poppinsFont = new Font("Segoe UI", 12, FontStyle.Regular);
                    poppinsBold = new Font("Segoe UI", 12, FontStyle.Bold);
                    poppinsTitle = new Font("Segoe UI", 16, FontStyle.Bold);
                }

                // Title label with checkmark
                Label titleLabel = new Label();
                titleLabel.Text = "✓ " + title;
                titleLabel.Font = poppinsTitle;
                titleLabel.ForeColor = Color.FromArgb(0, 100, 0);
                titleLabel.AutoSize = true;
                titleLabel.Location = new Point(20, 20);
                successForm.Controls.Add(titleLabel);

                // Message label (main content)
                Label messageLabel = new Label();
                messageLabel.Text = message;
                messageLabel.Font = poppinsBold;
                messageLabel.ForeColor = Color.Black;
                messageLabel.AutoSize = false;
                messageLabel.Size = new Size(500, 40);
                messageLabel.Location = new Point(20, 60);
                messageLabel.TextAlign = ContentAlignment.MiddleLeft;
                successForm.Controls.Add(messageLabel);

                // Details label (if provided)
                if (!string.IsNullOrEmpty(details))
                {
                    Label detailsLabel = new Label();
                    detailsLabel.Text = details;
                    detailsLabel.Font = poppinsFont;
                    detailsLabel.ForeColor = Color.Black;
                    detailsLabel.AutoSize = false;
                    detailsLabel.Size = new Size(500, 120); // Made taller
                    detailsLabel.Location = new Point(20, 110);
                    detailsLabel.TextAlign = ContentAlignment.TopLeft;
                    successForm.Controls.Add(detailsLabel);
                }

                // OK button
                Button okButton = new Button();
                okButton.Text = "OK";
                okButton.Size = new Size(100, 35);
                okButton.Location = new Point(215, 250); // Adjusted position
                okButton.Font = poppinsBold;
                okButton.BackColor = Color.FromArgb(0, 120, 0);
                okButton.ForeColor = Color.White;
                okButton.FlatStyle = FlatStyle.Flat;
                okButton.FlatAppearance.BorderSize = 0;
                okButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(0, 150, 0);
                okButton.FlatAppearance.MouseDownBackColor = Color.FromArgb(0, 100, 0);
                okButton.Cursor = Cursors.Hand;
                okButton.Click += (s, e) => {
                    successForm.DialogResult = DialogResult.OK;
                    successForm.Close();
                };
                successForm.Controls.Add(okButton);

                // Add some styling
                Panel headerPanel = new Panel();
                headerPanel.BackColor = Color.FromArgb(220, 240, 220);
                headerPanel.Size = new Size(550, 60);
                headerPanel.Location = new Point(0, 0);
                successForm.Controls.Add(headerPanel);
                headerPanel.SendToBack();

                // Make sure title is on top
                titleLabel.BringToFront();

                // Set the AcceptButton so Enter key works
                successForm.AcceptButton = okButton;

                // Show the form
                successForm.ShowDialog();

                // Clear search textbox when OK is clicked BUT KEEP OTHER FIELDS
                if (searchTextBox != null)
                {
                    searchTextBox.Text = "";
                    searchTextBox.Focus();
                }

                // Clean up fonts
                poppinsFont.Dispose();
                poppinsBold.Dispose();
                poppinsTitle.Dispose();
            }
        }

        // Updated method to show fingerprint registration success with search message styling
        public void ShowFingerprintSuccess(string name, string uid, string course, string year, int templateSize)
        {
            using (var successForm = new Form())
            {
                successForm.Text = "ACCOUNT CREATED SUCCESSFULLY";
                successForm.Size = new Size(550, 350);
                successForm.StartPosition = FormStartPosition.CenterParent;
                successForm.BackColor = Color.FromArgb(240, 245, 240);
                successForm.FormBorderStyle = FormBorderStyle.FixedDialog;
                successForm.MaximizeBox = false;
                successForm.MinimizeBox = false;

                // Create fonts (Poppins fallback to Segoe UI)
                Font poppinsFont = new Font("Poppins", 11, FontStyle.Regular);
                Font poppinsBold = new Font("Poppins", 11, FontStyle.Bold);
                Font poppinsTitle = new Font("Poppins", 14, FontStyle.Bold);

                // Fallback to Segoe UI if Poppins not available
                if (poppinsFont.Name != "Poppins")
                {
                    poppinsFont = new Font("Segoe UI", 11, FontStyle.Regular);
                    poppinsBold = new Font("Segoe UI", 11, FontStyle.Bold);
                    poppinsTitle = new Font("Segoe UI", 14, FontStyle.Bold);
                }

                // Header panel with green background
                Panel headerPanel = new Panel();
                headerPanel.BackColor = Color.FromArgb(220, 240, 220);
                headerPanel.Size = new Size(550, 60);
                headerPanel.Location = new Point(0, 0);
                successForm.Controls.Add(headerPanel);

                // Title label with checkmark (on header) - EXACTLY LIKE SEARCH SUCCESS
                Label titleLabel = new Label();
                titleLabel.Text = "✓ ACCOUNT CREATED SUCCESSFULLY";
                titleLabel.Font = poppinsTitle;
                titleLabel.ForeColor = Color.FromArgb(0, 100, 0);
                titleLabel.AutoSize = true;
                titleLabel.Location = new Point(20, 20);
                successForm.Controls.Add(titleLabel);
                titleLabel.BringToFront();

                // Main message label - Student found style
                Label messageLabel = new Label();
                messageLabel.Text = $"Student: {name} (UID: {uid})";
                messageLabel.Font = poppinsBold;
                messageLabel.ForeColor = Color.Black;
                messageLabel.AutoSize = false;
                messageLabel.Size = new Size(500, 30);
                messageLabel.Location = new Point(20, 80);
                messageLabel.TextAlign = ContentAlignment.MiddleLeft;
                successForm.Controls.Add(messageLabel);

                // Details section - Course and Year information
                Label detailsLabel = new Label();
                detailsLabel.Text = $"Course: {course}\nYear: {year}\n\nFingerprint: Registered ({templateSize} bytes)";
                detailsLabel.Font = poppinsFont;
                detailsLabel.ForeColor = Color.Black;
                detailsLabel.AutoSize = false;
                detailsLabel.Size = new Size(500, 100);
                detailsLabel.Location = new Point(20, 120);
                detailsLabel.TextAlign = ContentAlignment.TopLeft;
                successForm.Controls.Add(detailsLabel);

                // OK button - Same style as search success
                Button okButton = new Button();
                okButton.Text = "OK";
                okButton.Size = new Size(100, 35);
                okButton.Location = new Point(215, 250);
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

                // Set AcceptButton for Enter key
                successForm.AcceptButton = okButton;

                // Show the form
                successForm.ShowDialog();

                // Clean up fonts
                poppinsFont.Dispose();
                poppinsBold.Dispose();
                poppinsTitle.Dispose();
            }
        }

        private void ClearAllStudentInfo()
        {
            try
            {
                // Clear all student information fields
                if (uid != null) uid.Text = "";
                if (fname != null) fname.Text = "";
                if (comboBox1 != null) comboBox1.SelectedIndex = -1;
                if (comboBox2 != null) comboBox2.SelectedIndex = -1;
                if (comboBox3 != null) comboBox3.SelectedIndex = -1;
                if (searchTextBox != null) searchTextBox.Text = "";

                // Clear fingerprint image
                ClearFingerprintImage();

                // Reset status
                SetStatus("Ready for new student registration");

                // Focus on first field
                if (uid != null) uid.Focus();
            }
            catch (Exception ex)
            {
                SetStatus($"Error clearing form: {ex.Message}");
            }
        }

        private void ClearFingerprintImage()
        {
            try
            {
                if (fImage != null && !fImage.IsDisposed)
                {
                    if (fImage.Image != null)
                    {
                        fImage.Image.Dispose();
                        fImage.Image = null;
                    }
                }
            }
            catch (Exception ex)
            {
                SetStatus($"Error clearing fingerprint image: {ex.Message}");
            }
        }

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

        private void TestMySQLConnection()
        {
            try
            {
                string connectionString = DatabaseConfig.ConnectionString;

                using (MySqlConnection connection = new MySqlConnection(connectionString))
                {
                    connection.Open();
                    MessageBox.Show("✅ Connected to MySQL successfully!");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"❌ Connection failed: {ex.Message}");
            }
        }

        private DataSet LoadMySQLData()
        {
            DataSet dataSet = new DataSet();

            try
            {
                string connectionString = DatabaseConfig.ConnectionString;

                using (MySqlConnection connection = new MySqlConnection(connectionString))
                {
                    string query = "SELECT * FROM register_student";
                    MySqlDataAdapter adapter = new MySqlDataAdapter(query, connection);

                    adapter.Fill(dataSet, "Students");
                    MessageBox.Show($"Loaded {dataSet.Tables[0].Rows.Count} records!");
                }

                return dataSet;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"MySQL Error: {ex.Message}");
                return null;
            }
        }

        private void LoadCoursesIntoComboBox()
        {
            try
            {
                string connectionString = DatabaseConfig.ConnectionString;
                using (MySqlConnection connection = new MySqlConnection(connectionString))
                {
                    connection.Open();

                    // First try to load from admin_courses table
                    string adminCoursesQuery = @"
                SELECT course_code, course_name 
                FROM admin_courses 
                WHERE course_code IS NOT NULL AND course_code != '' 
                ORDER BY course_name";

                    using (MySqlCommand command = new MySqlCommand(adminCoursesQuery, connection))
                    using (MySqlDataReader reader = command.ExecuteReader())
                    {
                        if (reader.HasRows)
                        {
                            comboBox1.Items.Clear();
                            int count = 0;
                            while (reader.Read())
                            {
                                string courseCode = reader["course_code"].ToString();
                                string courseName = reader["course_name"].ToString();

                                // Only add courses that are in the specified list
                                if (IsCourseInAllowedList(courseName))
                                {
                                    comboBox1.Items.Add(courseName);
                                    count++;
                                }
                            }
                            SetStatus($"Loaded {count} courses from admin_courses table");
                            return; // Successfully loaded from admin_courses
                        }
                        else
                        {
                            SetStatus("admin_courses table is empty or has no valid data");
                        }
                    }

                    // If admin_courses is empty, try register_student table
                    string fallbackQuery = "SELECT DISTINCT course FROM register_student WHERE course IS NOT NULL AND course != '' ORDER BY course";
                    using (MySqlCommand fallbackCmd = new MySqlCommand(fallbackQuery, connection))
                    using (MySqlDataReader reader = fallbackCmd.ExecuteReader())
                    {
                        comboBox1.Items.Clear();
                        int count = 0;
                        while (reader.Read())
                        {
                            string courseName = reader["course"].ToString();
                            // Only add courses that are in the specified list
                            if (IsCourseInAllowedList(courseName))
                            {
                                comboBox1.Items.Add(courseName);
                                count++;
                            }
                        }
                        if (count > 0)
                        {
                            SetStatus($"Loaded {count} courses from student records");
                            return;
                        }
                        else
                        {
                            SetStatus("No courses found in any table");
                        }
                    }

                    // Final fallback: Use ONLY the specified courses
                    comboBox1.Items.Clear();
                    string[] allowedCourses = {
                        // College of Arts, Sciences, Education & Information Technology (CASETIT)
                        "BS in Information Technology",
                        "BS in Elementary Education",
                        "BS in Secondary Education",
                        
                        // College of Business, Management and Accountancy
                        "BS in Accountancy",
                        "BS in Business Administration",
                        "BS in Business Administration - Marketing Management",
                        "BS in Business Administration - Financial Management",
                        "BS in Hospitality Management",
                        "BS in Tourism Management",
                        
                        // Medical Sciences (Nursing only)
                        "BS in Nursing",
                        
                        // Engineering (Civil and Electrical only)
                        "BS in Civil Engineering",
                        "BS in Electrical Engineering",
                        
                        // Criminology
                        "BS in Criminology"
                    };

                    foreach (string course in allowedCourses)
                    {
                        comboBox1.Items.Add(course);
                    }

                    SetStatus($"Using specified course list ({allowedCourses.Length} courses)");
                }
            }
            catch (Exception ex)
            {
                // If all fails, use ONLY the specified courses
                comboBox1.Items.Clear();
                string[] allowedCourses = {
                    // College of Arts, Sciences, Education & Information Technology (CASETIT)
                    "BS in Information Technology",
                    "BS in Elementary Education",
                    "BS in Secondary Education",
                    
                    // College of Business, Management and Accountancy
                    "BS in Accountancy",
                    "BS in Business Administration",
                    "BS in Business Administration - Marketing Management",
                    "BS in Business Administration - Financial Management",
                    "BS in Hospitality Management",
                    "BS in Tourism Management",
                    
                    // Medical Sciences (Nursing only)
                    "BS in Nursing",
                    
                    // Engineering (Civil and Electrical only)
                    "BS in Civil Engineering",
                    "BS in Electrical Engineering",
                    
                    // Criminology
                    "BS in Criminology"
                };

                foreach (string course in allowedCourses)
                {
                    comboBox1.Items.Add(course);
                }

                SetStatus($"Error: {ex.Message} - Using specified course list");

                // For debugging
                MessageBox.Show($"Error loading courses: {ex.Message}",
                                "Course Loading Error",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Warning);
            }
        }

        private bool IsCourseInAllowedList(string courseName)
        {
            if (string.IsNullOrEmpty(courseName)) return false;

            courseName = courseName.ToUpper();

            // College of Arts, Sciences, Education & Information Technology (CASETIT)
            if (courseName.Contains("INFORMATION TECHNOLOGY") ||
                courseName.Contains("ELEMENTARY EDUCATION") ||
                courseName.Contains("SECONDARY EDUCATION"))
            {
                return true;
            }

            // College of Business, Management and Accountancy
            if (courseName.Contains("ACCOUNTANCY") ||
                courseName.Contains("BUSINESS ADMINISTRATION") ||
                courseName.Contains("MARKETING MANAGEMENT") ||
                courseName.Contains("FINANCIAL MANAGEMENT") ||
                courseName.Contains("HOSPITALITY MANAGEMENT") ||
                courseName.Contains("TOURISM MANAGEMENT"))
            {
                return true;
            }

            // Medical Sciences (Nursing only)
            if (courseName.Contains("NURSING"))
            {
                return true;
            }

            // Engineering (Civil and Electrical only)
            if (courseName.Contains("CIVIL ENGINEERING") ||
                courseName.Contains("ELECTRICAL ENGINEERING"))
            {
                return true;
            }

            // Criminology
            if (courseName.Contains("CRIMINOLOGY"))
            {
                return true;
            }

            return false;
        }

        private void LoadYearLevelsIntoComboBox()
        {
            try
            {
                comboBox2.Items.Clear();

                // Add year levels with proper display text
                comboBox2.Items.Add("1st Year");
                comboBox2.Items.Add("2nd Year");
                comboBox2.Items.Add("3rd Year");
                comboBox2.Items.Add("4th Year");
                comboBox2.Items.Add("5th Year"); // For courses that take 5 years

                SetStatus("Year levels loaded");
            }
            catch (Exception ex)
            {
                SetStatus($"Error loading year levels: {ex.Message}");
            }
        }

        private void LoadDepartmentsIntoComboBox()
        {
            try
            {
                comboBox3.Items.Clear();

                // Add ALL departments initially
                string[] allDepartments = {
                    "Arts, Science, Education & Information Technology",
                    "Business, Management & Accountancy",
                    "Criminology",
                    "Engineering & Technology",
                    "Medical Sciences"
                };

                comboBox3.Items.AddRange(allDepartments);
                SetStatus("Departments loaded successfully");
            }
            catch (Exception ex)
            {
                SetStatus($"Error loading departments: {ex.Message}");
                MessageBox.Show($"Error loading departments: {ex.Message}",
                                "Department Loading Error",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Warning);
            }
        }

        private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (comboBox1.SelectedItem != null)
            {
                string selectedCourse = comboBox1.SelectedItem.ToString();
                SetStatus($"Selected course: {selectedCourse}");

                // Auto-select department based on course
                string department = GetDepartmentByCourse(selectedCourse);
                if (!string.IsNullOrEmpty(department) && comboBox3 != null)
                {
                    var deptItem = comboBox3.Items.Cast<string>()
                        .FirstOrDefault(item => item.Equals(department, StringComparison.OrdinalIgnoreCase));
                    if (deptItem != null)
                    {
                        comboBox3.SelectedItem = deptItem;
                        SetStatus($"Auto-selected department: {department}");
                    }
                }
            }
        }

        private string GetDepartmentByCourse(string course)
        {
            if (string.IsNullOrEmpty(course)) return string.Empty;

            course = course.ToUpper();

            // Arts, Science, Education & Information Technology (CASETIT)
            if (course.Contains("INFORMATION TECHNOLOGY") ||
                course.Contains("ELEMENTARY EDUCATION") ||
                course.Contains("SECONDARY EDUCATION"))
            {
                return "Arts, Science, Education & Information Technology";
            }

            // Business, Management & Accountancy
            if (course.Contains("ACCOUNTANCY") ||
                course.Contains("BUSINESS ADMINISTRATION") ||
                course.Contains("MARKETING MANAGEMENT") ||
                course.Contains("FINANCIAL MANAGEMENT") ||
                course.Contains("TOURISM") ||
                course.Contains("HOSPITALITY"))
            {
                return "Business, Management & Accountancy";
            }

            // Criminology
            if (course.Contains("CRIMINOLOGY"))
            {
                return "Criminology";
            }

            // Engineering & Technology
            if (course.Contains("CIVIL ENGINEERING") ||
                course.Contains("ELECTRICAL ENGINEERING"))
            {
                return "Engineering & Technology";
            }

            // Medical Sciences
            if (course.Contains("NURSING") || course.Contains("MEDICAL"))
            {
                return "Medical Sciences";
            }

            return "Arts, Science, Education & Information Technology"; // Default department
        }

        private void comboBox2_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (comboBox2.SelectedItem != null)
            {
                string selectedYear = comboBox2.SelectedItem.ToString();
                SetStatus($"Selected year: {selectedYear}");
            }
        }

        private void comboBox3_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (comboBox3.SelectedItem != null)
            {
                string selectedDepartment = comboBox3.SelectedItem.ToString();
                SetStatus($"Selected department: {selectedDepartment}");
            }
        }

        protected void SetPrompt(string prompt)
        {
            if (IsDisposed || !IsHandleCreated) return;
            try
            {
                this.BeginInvoke(new Action(delegate ()
                {
                    if (Prompt != null && !Prompt.IsDisposed)
                        Prompt.Text = prompt;
                }));
            }
            catch (ObjectDisposedException) { }
        }

        protected void SetStatus(string status)
        {
            if (IsDisposed || !IsHandleCreated) return;
            try
            {
                this.BeginInvoke(new Action(delegate ()
                {
                    if (StatusLabel != null && !StatusLabel.IsDisposed)
                        StatusLabel.Text = status;
                }));
            }
            catch (ObjectDisposedException) { }
        }

        private void DrawPicture(Bitmap bitmap)
        {
            if (IsDisposed || !IsHandleCreated) return;
            try
            {
                this.BeginInvoke(new Action(delegate ()
                {
                    if (fImage != null && !fImage.IsDisposed)
                    {
                        if (fImage.Image != null)
                        {
                            fImage.Image.Dispose();
                        }
                        fImage.Image = new Bitmap(bitmap, fImage.Size);
                    }
                }));
            }
            catch (ObjectDisposedException) { }
        }

        protected void Setfname(string value)
        {
            if (IsDisposed || !IsHandleCreated) return;
            try
            {
                this.BeginInvoke(new Action(delegate ()
                {
                    if (fname != null && !fname.IsDisposed)
                        fname.Text = value;
                }));
            }
            catch (ObjectDisposedException) { }
        }

        protected virtual void Init()
        {
            try
            {
                Capturer = new DPFP.Capture.Capture();
                if (null != Capturer)
                    Capturer.EventHandler = this;
                else
                    SetPrompt("Can't initiate capture operation.");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Can't initiate capture operation: {ex.Message}");
            }
        }

        protected virtual void Process(DPFP.Sample Sample)
        {
            DrawPicture(ConvertSampleToBitmap(Sample));
        }

        protected Bitmap ConvertSampleToBitmap(DPFP.Sample Sample)
        {
            DPFP.Capture.SampleConversion Convertor = new DPFP.Capture.SampleConversion();
            Bitmap bitmap = null;
            Convertor.ConvertToPicture(Sample, ref bitmap);
            return bitmap;
        }

        protected void Start()
        {
            if (null != Capturer)
            {
                try
                {
                    Capturer.StartCapture();
                    SetPrompt("Using the fingerprint reader, scan your fingerprint.");
                }
                catch (Exception ex)
                {
                    SetPrompt("Can't initiate capture.");
                    MakeReport($"Start error: {ex.Message}");
                }
            }
        }

        protected void Stop()
        {
            if (null != Capturer)
            {
                try
                {
                    Capturer.StopCapture();
                }
                catch (Exception ex)
                {
                    SetPrompt("Can't terminate capture.");
                    MakeReport($"Stop error: {ex.Message}");
                }
            }
        }

        protected void MakeReport(string message)
        {
            if (IsDisposed || !IsHandleCreated) return;
            try
            {
                this.BeginInvoke(new Action(delegate ()
                {
                    if (StatusText != null && !StatusText.IsDisposed)
                        StatusText.AppendText(message + "\r\n");
                }));
            }
            catch (ObjectDisposedException) { }
        }

        protected DPFP.FeatureSet ExtractFeatures(DPFP.Sample Sample, DPFP.Processing.DataPurpose Purpose)
        {
            try
            {
                DPFP.Processing.FeatureExtraction Extractor = new DPFP.Processing.FeatureExtraction();
                DPFP.Capture.CaptureFeedback feedback = DPFP.Capture.CaptureFeedback.None;
                DPFP.FeatureSet features = new DPFP.FeatureSet();
                Extractor.CreateFeatureSet(Sample, Purpose, ref feedback, ref features);

                if (feedback == DPFP.Capture.CaptureFeedback.Good)
                    return features;
                else
                    return null;
            }
            catch (Exception ex)
            {
                MakeReport($"Extract error: {ex.Message}");
                return null;
            }
        }

        public void OnComplete(object Capture, string ReaderSerialNumber, DPFP.Sample Sample)
        {
            MakeReport("The fingerprint sample was captured.");
            SetPrompt("Scan the same fingerprint again.");
            Process(Sample);
        }

        public void OnFingerGone(object Capture, string ReaderSerialNumber)
        {
            MakeReport("The fingerprint was removed from the fingerprint reader");
        }

        public void OnFingerTouch(object Capture, string ReaderSerialNumber)
        {
            MakeReport("The fingerprint reader was touched.");
        }

        public void OnReaderConnect(object Capture, string ReaderSerialNumber)
        {
            MakeReport("The fingerprint reader was connected.");
        }

        public void OnReaderDisconnect(object Capture, string ReaderSerialNumber)
        {
            MakeReport("The fingerprint reader was disconnected");
        }

        public void OnSampleQuality(object Capture, string ReaderSerialNumber, DPFP.Capture.CaptureFeedback CaptureFeedback)
        {
            if (CaptureFeedback == DPFP.Capture.CaptureFeedback.Good)
                MakeReport("The quality of the fingerprint sample is good.");
            else
                MakeReport("The quality of the fingerprint sample is poor");
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            Stop();
            if (Capturer != null)
            {
                Capturer.Dispose();
                Capturer = null;
            }
            base.OnFormClosed(e);
        }

        private void start_scan_Click(object sender, EventArgs e)
        {
            Start();
        }

        private void capture_FormClosed(object sender, FormClosedEventArgs e)
        {
            Stop();
        }

        private void capture_Load(object sender, EventArgs e)
        {
            Init();

            // Set the dropdown style before loading
            comboBox1.DropDownStyle = ComboBoxStyle.DropDownList;
            comboBox1.DropDownHeight = 200;
            comboBox2.DropDownStyle = ComboBoxStyle.DropDownList;
            comboBox3.DropDownStyle = ComboBoxStyle.DropDownList;

            LoadCoursesIntoComboBox();     // Load courses when form loads
            LoadYearLevelsIntoComboBox();  // Load year levels
            LoadDepartmentsIntoComboBox(); // Load departments
        }

        private void fname_TextChanged(object sender, EventArgs e)
        {
            FullName = fname.Text;
        }

        private string MapYearLevel(string display)
        {
            if (string.IsNullOrWhiteSpace(display)) return string.Empty;

            // Convert from display text to database value
            display = display.ToLowerInvariant();
            if (display.Contains("1st") || display == "1") return "1";
            if (display.Contains("2nd") || display == "2") return "2";
            if (display.Contains("3rd") || display == "3") return "3";
            if (display.Contains("4th") || display == "4") return "4";
            if (display.Contains("5th") || display == "5") return "5";
            return display;
        }

        private void button1_Click(object sender, EventArgs e)
        {
            TestMySQLConnection();
        }

        private void searchButton_Click_1(object sender, EventArgs e)
        {

        }

        // ADDED: Fingerprint duplicate check method
        private bool IsFingerprintDuplicate(byte[] newTemplate)
        {
            try
            {
                string connectionString = DatabaseConfig.ConnectionString;
                string query = "SELECT student_name, uid, fingerprint_data FROM register_student WHERE fingerprint_data IS NOT NULL AND LENGTH(fingerprint_data) > 0";

                using (MySqlConnection connection = new MySqlConnection(connectionString))
                using (MySqlCommand command = new MySqlCommand(query, connection))
                {
                    connection.Open();

                    using (MySqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            if (!reader.IsDBNull(reader.GetOrdinal("fingerprint_data")))
                            {
                                byte[] existingTemplate = (byte[])reader["fingerprint_data"];

                                // Simple length comparison first (quick check)
                                if (existingTemplate.Length == newTemplate.Length)
                                {
                                    // More detailed comparison
                                    if (AreTemplatesIdentical(newTemplate, existingTemplate))
                                    {
                                        string existingStudentName = reader["student_name"].ToString();
                                        string existingStudentUid = reader["uid"].ToString();

                                        SafeMakeReport($"✗ Fingerprint already registered to: {existingStudentName} (UID: {existingStudentUid})");
                                        return true;
                                    }
                                }
                            }
                        }
                    }
                }
                return false;
            }
            catch (Exception ex)
            {
                SafeMakeReport($"✗ Error checking fingerprint duplicate: {ex.Message}");
                return false;
            }
        }

        // ADDED: Template comparison method
        private bool AreTemplatesIdentical(byte[] template1, byte[] template2)
        {
            // Simple byte-by-byte comparison
            if (template1.Length != template2.Length)
                return false;

            for (int i = 0; i < template1.Length; i++)
            {
                if (template1[i] != template2[i])
                    return false;
            }

            return true;
        }

        // ADDED: Thread-safe report method
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
    }
}