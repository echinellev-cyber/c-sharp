using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using DPFP;
using DPFP.Capture;
using DPFP.Processing;
using DPFP.Verification;

namespace BiometricsFingerprint
{
    public partial class events : Form
    {
        private readonly string connectionString = DatabaseConfig.ConnectionString;
        private ListView attendanceListView;

        // Multiple scanner support
        private Dictionary<string, DPFP.Capture.Capture> scanners;
        private Dictionary<string, ScannerHandler> scannerHandlers;

        // UPDATED: Remove hardcoded fine amounts - get from event instead
        private int lateThresholdMinutes = 15; // From your settings table
        private int minimumSessionMinutes = 30; // NEW: Minimum time before allowed to time out

        public events()
        {
            InitializeComponent();

            // Set form properties for full screen BUT keep the title bar with X button
            this.WindowState = FormWindowState.Maximized;
            this.FormBorderStyle = FormBorderStyle.Sizable;
            this.ControlBox = true;
            this.MaximizeBox = true;
            this.MinimizeBox = true;

            LoadFineSettings(); // Load fine settings from database

            FindListViewControl();
            SetupListView();
            LoadEvents();
            InitMultipleScanners(); // Initialize multiple fingerprint scanners
        }

        // SIMPLIFIED LOGGING SYSTEM - REPLACED THE OLD MakeReport METHOD
        protected void MakeReport(string message)
        {
            if (IsDisposed || !IsHandleCreated) return;

            try
            {
                // FILTER OUT VERBOSE MESSAGES
                if (ShouldFilterMessage(message))
                    return;

                this.BeginInvoke(new Action(delegate ()
                {
                    if (StatusText != null && !StatusText.IsDisposed)
                    {
                        StatusText.AppendText(DateTime.Now.ToString("HH:mm:ss") + " - " + message + "\r\n");

                        // Auto-scroll to bottom
                        StatusText.SelectionStart = StatusText.TextLength;
                        StatusText.ScrollToCaret();
                    }
                }));
            }
            catch (ObjectDisposedException) { }
        }

        private bool ShouldFilterMessage(string message)
        {
            string[] filteredPatterns = {
                "Checked.*students.*had fingerprint templates",
                "No matching fingerprint found",
                "Possible reasons:",
                "Fingerprint not enrolled in database",
                "Poor fingerprint scan quality",
                "Fingerprint scanner issue",
                "Finger removed",
                "Finger detected",
                "Good sample quality",
                "Poor sample quality",
                "Processing fingerprint...",
                "Searching database for matches...",
                "Checking student:",
                "No fingerprint template for:",
                "Verification score:",
                "Features extracted",
                "Failed to extract features"
            };

            // KEEP important messages
            string[] importantPatterns = {
                "VERIFICATION SUCCESSFUL!",
                "MATCH FOUND:",
                "TIMED IN successfully",
                "TIMED OUT successfully",
                "Error:",
                "Failed to",
                "✗",
                "✓",
                "⚠",
                "cannot time out yet",
                "Generated.*fine",
                "DEBUG:",
                "Event Access Restricted",
                "year.*restricted"
            };

            // Always show important messages
            foreach (string pattern in importantPatterns)
            {
                if (message.Contains(pattern))
                    return false;
            }

            // Filter out verbose messages
            foreach (string pattern in filteredPatterns)
            {
                if (message.Contains(pattern))
                    return true;
            }

            return false;
        }

        // Scanner Handler Class for multiple scanners
        public class ScannerHandler : DPFP.Capture.EventHandler
        {
            private events parentForm;
            private string scannerId;
            private PictureBox fingerprintImage;
            private Label promptLabel;
            private Label statusLabel;
            private TextBox statusText; // CHANGED: From RichTextBox to TextBox

            public ScannerHandler(events parent, string id, PictureBox image, Label prompt, Label status, TextBox text) // CHANGED: TextBox parameter
            {
                parentForm = parent;
                scannerId = id;
                fingerprintImage = image;
                promptLabel = prompt;
                statusLabel = status;
                statusText = text;
            }

            public void OnComplete(object Capture, string ReaderSerialNumber, DPFP.Sample Sample)
            {
                parentForm.MakeReport($"[{scannerId}] Fingerprint captured");
                parentForm.SetPrompt(scannerId, "Processing...");
                parentForm.Process(scannerId, Sample);
            }

            public void OnFingerGone(object Capture, string ReaderSerialNumber)
            {
                parentForm.MakeReport($"[{scannerId}] Finger removed");
                parentForm.SetPrompt(scannerId, "Place your finger on the scanner");
            }

            public void OnFingerTouch(object Capture, string ReaderSerialNumber)
            {
                parentForm.MakeReport($"[{scannerId}] Finger detected");
            }

            public void OnReaderConnect(object Capture, string ReaderSerialNumber)
            {
                parentForm.MakeReport($"[{scannerId}] Reader connected: {ReaderSerialNumber}");
            }

            public void OnReaderDisconnect(object Capture, string ReaderSerialNumber)
            {
                parentForm.MakeReport($"[{scannerId}] Reader disconnected: {ReaderSerialNumber}");
            }

            public void OnSampleQuality(object Capture, string ReaderSerialNumber, DPFP.Capture.CaptureFeedback CaptureFeedback)
            {
                if (CaptureFeedback == DPFP.Capture.CaptureFeedback.Good)
                    parentForm.MakeReport($"[{scannerId}] Good sample quality");
                else
                    parentForm.MakeReport($"[{scannerId}] Poor sample quality");
            }

            public void DrawPicture(Bitmap bitmap)
            {
                if (fingerprintImage != null && !fingerprintImage.IsDisposed && fingerprintImage.InvokeRequired)
                {
                    fingerprintImage.Invoke(new Action<Bitmap>(DrawPicture), bitmap);
                }
                else if (fingerprintImage != null && !fingerprintImage.IsDisposed)
                {
                    fingerprintImage.Image = new Bitmap(bitmap, fingerprintImage.Size);
                }
            }

            public void SetPrompt(string prompt)
            {
                if (promptLabel != null && !promptLabel.IsDisposed && promptLabel.InvokeRequired)
                {
                    promptLabel.Invoke(new Action<string>(SetPrompt), prompt);
                }
                else if (promptLabel != null && !promptLabel.IsDisposed)
                {
                    promptLabel.Text = prompt;
                }
            }

            public void SetStatus(string status)
            {
                if (statusLabel != null && !statusLabel.IsDisposed && statusLabel.InvokeRequired)
                {
                    statusLabel.Invoke(new Action<string>(SetStatus), status);
                }
                else if (statusLabel != null && !statusLabel.IsDisposed)
                {
                    statusLabel.Text = status;
                }
            }
        }

        // Multiple Scanner Initialization
        protected virtual void InitMultipleScanners()
        {
            try
            {
                scanners = new Dictionary<string, DPFP.Capture.Capture>();
                scannerHandlers = new Dictionary<string, ScannerHandler>();

                // Get available readers - FIXED: Use different approach since EnumerateReaders doesn't exist
                MakeReport("Initializing fingerprint scanners...");

                // Try to initialize multiple scanners by creating multiple Capture instances
                // They will automatically bind to available hardware
                string[] scannerIds = { "Scanner_1", "Scanner_2" }; // You can add more if needed

                foreach (string scannerId in scannerIds)
                {
                    try
                    {
                        var capturer = new DPFP.Capture.Capture();

                        // Create handler for this scanner
                        var handler = new ScannerHandler(
                            this,
                            scannerId,
                            GetScannerImageControl(scannerId),
                            GetScannerPromptControl(scannerId),
                            GetScannerStatusControl(scannerId),
                            StatusText // This should be your TextBox control
                        );

                        capturer.EventHandler = handler;
                        scanners.Add(scannerId, capturer);
                        scannerHandlers.Add(scannerId, handler);

                        MakeReport($"Initialized {scannerId}");
                    }
                    catch (Exception ex)
                    {
                        MakeReport($"Failed to initialize {scannerId}: {ex.Message}");
                    }
                }

                if (scanners.Count == 0)
                {
                    MakeReport("No fingerprint scanners initialized");
                }
                else
                {
                    MakeReport($"Successfully initialized {scanners.Count} scanner(s)");
                }
            }
            catch (Exception ex)
            {
                MakeReport($"Error initializing scanners: {ex.Message}");
            }
        }

        // Helper methods to get UI controls for each scanner - FIXED: Removed recursive patterns
        private PictureBox GetScannerImageControl(string scannerId)
        {
            string controlName;
            if (scannerId == "Scanner_1")
            {
                controlName = "fImage";  // Use your existing fImage control for first scanner
            }
            else if (scannerId == "Scanner_2")
            {
                controlName = "fImage2"; // You'll need to add fImage2 to your form
            }
            else
            {
                controlName = "fImage";
            }

            var control = this.Controls.Find(controlName, true).FirstOrDefault();
            if (control == null)
            {
                // Fallback to main fImage if specific control not found
                control = this.Controls.Find("fImage", true).FirstOrDefault();
            }
            return control as PictureBox;
        }

        private Label GetScannerPromptControl(string scannerId)
        {
            string controlName;
            if (scannerId == "Scanner_1")
            {
                controlName = "Prompt";   // Use your existing Prompt control
            }
            else if (scannerId == "Scanner_2")
            {
                controlName = "Prompt2";  // You'll need to add Prompt2 to your form
            }
            else
            {
                controlName = "Prompt";
            }

            var control = this.Controls.Find(controlName, true).FirstOrDefault();
            if (control == null)
            {
                // Fallback to main Prompt if specific control not found
                control = this.Controls.Find("Prompt", true).FirstOrDefault();
            }
            return control as Label;
        }

        private Label GetScannerStatusControl(string scannerId)
        {
            string controlName;
            if (scannerId == "Scanner_1")
            {
                controlName = "StatusLabel";   // Use your existing StatusLabel control
            }
            else if (scannerId == "Scanner_2")
            {
                controlName = "StatusLabel2";  // You'll need to add StatusLabel2 to your form
            }
            else
            {
                controlName = "StatusLabel";
            }

            var control = this.Controls.Find(controlName, true).FirstOrDefault();
            if (control == null)
            {
                // Fallback to main StatusLabel if specific control not found
                control = this.Controls.Find("StatusLabel", true).FirstOrDefault();
            }
            return control as Label;
        }

        // Start all scanners
        protected void StartAllScanners()
        {
            if (scanners == null || scanners.Count == 0)
            {
                MakeReport("No scanners available to start");
                return;
            }

            foreach (var scanner in scanners)
            {
                try
                {
                    scanner.Value.StartCapture();
                    SetPrompt(scanner.Key, "Scan your fingerprint");
                    SetStatus(scanner.Key, "Scanning...");
                    MakeReport($"[{scanner.Key}] Scanning started");
                }
                catch (Exception ex)
                {
                    MakeReport($"[{scanner.Key}] Start error: {ex.Message}");
                }
            }
        }

        // Stop all scanners
        protected void StopAllScanners()
        {
            if (scanners == null) return;

            foreach (var scanner in scanners)
            {
                try
                {
                    scanner.Value.StopCapture();
                    SetStatus(scanner.Key, "Ready");
                    MakeReport($"[{scanner.Key}] Scanning stopped");
                }
                catch (Exception ex)
                {
                    MakeReport($"[{scanner.Key}] Stop error: {ex.Message}");
                }
            }
        }

        // Start specific scanner
        protected void StartScanner(string scannerId)
        {
            if (scanners != null && scanners.ContainsKey(scannerId))
            {
                try
                {
                    scanners[scannerId].StartCapture();
                    SetPrompt(scannerId, "Scan your fingerprint");
                    SetStatus(scannerId, "Scanning...");
                    MakeReport($"[{scannerId}] Scanning started");
                }
                catch (Exception ex)
                {
                    MakeReport($"[{scannerId}] Start error: {ex.Message}");
                }
            }
            else
            {
                MakeReport($"[{scannerId}] Scanner not found");
            }
        }

        // Stop specific scanner
        protected void StopScanner(string scannerId)
        {
            if (scanners != null && scanners.ContainsKey(scannerId))
            {
                try
                {
                    scanners[scannerId].StopCapture();
                    SetStatus(scannerId, "Ready");
                    MakeReport($"[{scannerId}] Scanning stopped");
                }
                catch (Exception ex)
                {
                    MakeReport($"[{scannerId}] Stop error: {ex.Message}");
                }
            }
            else
            {
                MakeReport($"[{scannerId}] Scanner not found");
            }
        }

        // Process fingerprint sample from specific scanner
        protected void Process(string scannerId, DPFP.Sample Sample)
        {
            if (scannerHandlers.ContainsKey(scannerId))
            {
                scannerHandlers[scannerId].DrawPicture(ConvertSampleToBitmap(Sample));
            }

            MakeReport($"[{scannerId}] Processing fingerprint...");

            try
            {
                var features = ExtractFeatures(Sample, DPFP.Processing.DataPurpose.Verification);

                if (features != null)
                {
                    MakeReport($"[{scannerId}] ✓ Features extracted");
                    CheckForMatch(scannerId, features);
                }
                else
                {
                    MakeReport($"[{scannerId}] ✗ Failed to extract features");
                }
            }
            catch (Exception ex)
            {
                MakeReport($"[{scannerId}] Error: {ex.Message}");
            }
        }

        // UI Update methods for specific scanners
        protected void SetPrompt(string scannerId, string prompt)
        {
            if (scannerHandlers.ContainsKey(scannerId))
            {
                scannerHandlers[scannerId].SetPrompt(prompt);
            }
        }

        protected void SetStatus(string scannerId, string status)
        {
            if (scannerHandlers.ContainsKey(scannerId))
            {
                scannerHandlers[scannerId].SetStatus(status);
            }
        }

        protected void DrawPicture(string scannerId, Bitmap bitmap)
        {
            if (scannerHandlers.ContainsKey(scannerId))
            {
                scannerHandlers[scannerId].DrawPicture(bitmap);
            }
        }

        // Load fine settings from database
        private void LoadFineSettings()
        {
            try
            {
                string query = "SELECT setting_name, setting_value FROM settings WHERE setting_name IN ('late_threshold_minutes', 'minimum_session_minutes')";

                using (var connection = new MySqlConnection(connectionString))
                using (var command = new MySqlCommand(query, connection))
                {
                    connection.Open();
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string settingName = reader["setting_name"].ToString();
                            string settingValue = reader["setting_value"].ToString();

                            switch (settingName)
                            {
                                case "late_threshold_minutes":
                                    lateThresholdMinutes = int.Parse(settingValue);
                                    break;
                                case "minimum_session_minutes":
                                    minimumSessionMinutes = int.Parse(settingValue);
                                    break;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading fine settings: {ex.Message}. Using default values.", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        // NEW METHOD: Get fine amount from event
        private decimal GetEventFineAmount(int eventId)
        {
            try
            {
                string query = "SELECT fine_amount FROM admin_event WHERE event_id = @eventId";
                using (var connection = new MySqlConnection(connectionString))
                using (var command = new MySqlCommand(query, connection))
                {
                    connection.Open();
                    command.Parameters.AddWithValue("@eventId", eventId);
                    object result = command.ExecuteScalar();

                    return result != null && result != DBNull.Value ? Convert.ToDecimal(result) : 0m;
                }
            }
            catch (Exception ex)
            {
                MakeReport($"Error getting event fine amount: {ex.Message}");
                return 0m;
            }
        }

        // Method to update fines display for a specific event
        private void UpdateFinesDisplay(int eventId)
        {
            try
            {
                string query = @"
                    SELECT 
                        rs.student_name,
                        rs.uid,
                        SUM(CASE WHEN af.status = 'unpaid' THEN af.amount ELSE 0 END) as total_unpaid_fines,
                        COUNT(CASE WHEN af.status = 'unpaid' THEN 1 END) as unpaid_count
                    FROM register_student rs
                    LEFT JOIN admin_fines af ON rs.id = af.student_id AND af.event_id = @eventId
                    WHERE rs.id IN (SELECT student_id FROM students_events WHERE event_id = @eventId)
                    GROUP BY rs.id, rs.student_name, rs.uid
                    HAVING total_unpaid_fines > 0
                    ORDER BY total_unpaid_fines DESC";

                using (var connection = new MySqlConnection(connectionString))
                using (var command = new MySqlCommand(query, connection))
                {
                    connection.Open();
                    command.Parameters.AddWithValue("@eventId", eventId);

                    using (var reader = command.ExecuteReader())
                    {
                        string finesInfo = $"Fines Summary for Event ID: {eventId}\r\n";
                        finesInfo += "===================================\r\n";

                        int studentCount = 0;
                        decimal totalFines = 0;

                        while (reader.Read())
                        {
                            studentCount++;
                            string studentName = reader["student_name"].ToString();
                            string uid = reader["uid"].ToString();
                            decimal unpaidFines = Convert.ToDecimal(reader["total_unpaid_fines"]);
                            int unpaidCount = Convert.ToInt32(reader["unpaid_count"]);

                            finesInfo += $"{studentName} ({uid}): ₱{unpaidFines} ({unpaidCount} fines)\r\n";
                            totalFines += unpaidFines;
                        }

                        if (studentCount == 0)
                        {
                            finesInfo += "No unpaid fines for this event.\r\n";
                        }
                        else
                        {
                            finesInfo += $"\r\nTotal: {studentCount} students with ₱{totalFines} in unpaid fines\r\n";
                        }

                        // Get event fine amount for display
                        decimal eventFineAmount = GetEventFineAmount(eventId);
                        finesInfo += $"\r\nFine Rates:\r\n- Late: ₱50 (after {lateThresholdMinutes} mins)\r\n- Absent: ₱{eventFineAmount}";

                        // Update the fines textbox on UI thread
                        //if (finesTextBox.InvokeRequired)
                        //{
                        //    finesTextBox.Invoke(new Action(() => finesTextBox.Text = finesInfo));
                        //}
                        //else
                        //{
                        //    finesTextBox.Text = finesInfo;
                        //}
                    }
                }
            }
            catch (Exception ex)
            {
                MakeReport($"Error updating fines display: {ex.Message}");
            }
        }

        // UPDATED: Method to automatically generate fines for absent students - now uses event fine amount
        private void GenerateFinesForAbsentStudents(int eventId)
        {
            try
            {
                // Get event details and fine amount
                string eventQuery = "SELECT event_name, date, fine_amount FROM admin_event WHERE event_id = @eventId";
                string eventName = "";
                DateTime eventDate = DateTime.Now;
                decimal eventFineAmount = 0m;

                using (var connection = new MySqlConnection(connectionString))
                {
                    connection.Open();

                    // Get event details
                    using (var eventCmd = new MySqlCommand(eventQuery, connection))
                    {
                        eventCmd.Parameters.AddWithValue("@eventId", eventId);
                        using (var eventReader = eventCmd.ExecuteReader())
                        {
                            if (eventReader.Read())
                            {
                                eventName = eventReader["event_name"].ToString();
                                eventDate = Convert.ToDateTime(eventReader["date"]);
                                eventFineAmount = eventReader["fine_amount"] != DBNull.Value ? Convert.ToDecimal(eventReader["fine_amount"]) : 0m;
                            }
                        }
                    }

                    // Check if fines are enabled for this event
                    if (eventFineAmount <= 0)
                    {
                        MakeReport("Fines are not enabled for this event");
                        return;
                    }

                    // Find absent students and generate fines
                    string absentQuery = @"
                        SELECT rs.id as student_id, rs.student_name, rs.uid, se.id as attendance_id
                        FROM students_events se 
                        INNER JOIN register_student rs ON se.student_id = rs.id 
                        WHERE se.event_id = @eventId 
                        AND se.attendance_status = 'absent'
                        AND se.student_id NOT IN (
                            SELECT student_id FROM admin_fines 
                            WHERE event_id = @eventId AND fine_type = 'absent'
                        )";

                    using (var absentCmd = new MySqlCommand(absentQuery, connection))
                    {
                        absentCmd.Parameters.AddWithValue("@eventId", eventId);

                        using (var reader = absentCmd.ExecuteReader())
                        {
                            int finesGenerated = 0;

                            while (reader.Read())
                            {
                                int studentId = Convert.ToInt32(reader["student_id"]);
                                string studentName = reader["student_name"].ToString();
                                string uid = reader["uid"].ToString();
                                int attendanceId = Convert.ToInt32(reader["attendance_id"]);

                                // Generate fine for absent student USING EVENT FINE AMOUNT
                                GenerateFine(studentId, eventId, attendanceId, "absent", eventFineAmount,
                                    $"Unexcused absence from {eventName} on {eventDate:MMM dd, yyyy}");

                                finesGenerated++;
                                MakeReport($"Generated ₱{eventFineAmount} fine for absent student: {studentName}");
                            }

                            if (finesGenerated > 0)
                            {
                                MakeReport($"Generated fines for {finesGenerated} absent students");
                                UpdateFinesDisplay(eventId); // Refresh fines display
                            }
                            else
                            {
                                MakeReport("No new absent students found to generate fines for");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MakeReport($"Error generating fines: {ex.Message}");
            }
        }

        // Method to generate a fine
        private void GenerateFine(int studentId, int eventId, int attendanceId, string fineType, decimal amount, string description)
        {
            try
            {
                string query = @"
                    INSERT INTO admin_fines 
                    (student_id, event_id, attendance_id, fine_type, amount, description, date_issued, due_date, status, issued_by)
                    VALUES 
                    (@studentId, @eventId, @attendanceId, @fineType, @amount, @description, CURDATE(), DATE_ADD(CURDATE(), INTERVAL 30 DAY), 'unpaid', @issuedBy)";

                using (var connection = new MySqlConnection(connectionString))
                using (var command = new MySqlCommand(query, connection))
                {
                    connection.Open();
                    command.Parameters.AddWithValue("@studentId", studentId);
                    command.Parameters.AddWithValue("@eventId", eventId);
                    command.Parameters.AddWithValue("@attendanceId", attendanceId);
                    command.Parameters.AddWithValue("@fineType", fineType);
                    command.Parameters.AddWithValue("@amount", amount);
                    command.Parameters.AddWithValue("@description", description);
                    command.Parameters.AddWithValue("@issuedBy", 56); // Default admin ID

                    command.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                MakeReport($"Error generating fine: {ex.Message}");
            }
        }

        // Method to check if student is late and generate fine if needed
        private void CheckAndGenerateLateFine(int studentId, int eventId, int attendanceId, DateTime timeIn)
        {
            try
            {
                // Get event start time
                string eventQuery = "SELECT start_time, event_name, date FROM admin_event WHERE event_id = @eventId";

                using (var connection = new MySqlConnection(connectionString))
                {
                    connection.Open();

                    TimeSpan eventStartTime = TimeSpan.Zero;
                    string eventName = "";
                    DateTime eventDate = DateTime.Now;

                    using (var eventCmd = new MySqlCommand(eventQuery, connection))
                    {
                        eventCmd.Parameters.AddWithValue("@eventId", eventId);
                        using (var eventReader = eventCmd.ExecuteReader())
                        {
                            if (eventReader.Read())
                            {
                                eventStartTime = (TimeSpan)eventReader["start_time"];
                                eventName = eventReader["event_name"].ToString();
                                eventDate = Convert.ToDateTime(eventReader["date"]);
                            }
                        }
                    }

                    // Calculate if student is late
                    DateTime eventDateTime = eventDate.Date + eventStartTime;
                    TimeSpan lateDuration = timeIn - eventDateTime;

                    if (lateDuration.TotalMinutes > lateThresholdMinutes)
                    {
                        // Check if late fine already exists
                        string checkFineQuery = @"
                            SELECT COUNT(*) FROM admin_fines 
                            WHERE student_id = @studentId AND event_id = @eventId AND fine_type = 'late'";

                        using (var checkCmd = new MySqlCommand(checkFineQuery, connection))
                        {
                            checkCmd.Parameters.AddWithValue("@studentId", studentId);
                            checkCmd.Parameters.AddWithValue("@eventId", eventId);

                            int existingFines = Convert.ToInt32(checkCmd.ExecuteScalar());

                            if (existingFines == 0)
                            {
                                // Generate late fine (keeping ₱50 for late fines as per your original)
                                GenerateFine(studentId, eventId, attendanceId, "late", 50.00m,
                                    $"Late arrival to {eventName} on {eventDate:MMM dd, yyyy} ({lateDuration.TotalMinutes:F0} minutes late)");

                                MakeReport($"Generated ₱50.00 late fine for {lateDuration.TotalMinutes:F0} minutes late");
                                UpdateFinesDisplay(eventId); // Refresh fines display
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MakeReport($"Error checking late fine: {ex.Message}");
            }
        }

        //***************************************************

        // Add a method to mark event as completed
        private void MarkEventAsCompleted(int eventId)
        {
            try
            {
                string query = "UPDATE admin_event SET status = 'Completed' WHERE event_id = @eventId";

                using (MySqlConnection connection = new MySqlConnection(connectionString))
                using (MySqlCommand command = new MySqlCommand(query, connection))
                {
                    connection.Open();
                    command.Parameters.AddWithValue("@eventId", eventId);
                    command.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error marking event as completed: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // Modify the LoadEvents method to only show active events
        private void LoadEvents()
        {
            try
            {
                // Only load events that are not completed
                string query = "SELECT event_id, event_name, date, start_time, end_time, location FROM admin_event WHERE status != 'Completed' OR status IS NULL ORDER BY date DESC, start_time DESC";

                using (MySqlConnection connection = new MySqlConnection(connectionString))
                {
                    connection.Open();

                    using (MySqlCommand command = new MySqlCommand(query, connection))
                    using (MySqlDataReader reader = command.ExecuteReader())
                    {
                        comboBox1.Items.Clear();

                        while (reader.Read())
                        {
                            string eventName = reader["event_name"].ToString();
                            DateTime eventDate = Convert.ToDateTime(reader["date"]);
                            TimeSpan startTime = (TimeSpan)reader["start_time"];
                            TimeSpan endTime = (TimeSpan)reader["end_time"];
                            string location = reader["location"].ToString();

                            string displayText = $"{eventName} - {eventDate:MMM dd, yyyy} ({startTime:hh\\:mm} - {endTime:hh\\:mm}) - {location}";

                            comboBox1.Items.Add(new EventItem(
                                Convert.ToInt32(reader["event_id"]),
                                displayText
                            ));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading events: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // Add a button to mark event as finished manually
        private void btnFinishEvent_Click(object sender, EventArgs e)
        {
            int selectedEventId = GetSelectedEventId();
            if (selectedEventId == -1)
            {
                MessageBox.Show("Please select an event first.", "Information", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            DialogResult result = MessageBox.Show(
                "Are you sure you want to mark this event as finished? This will hide it from future attendance sessions.",
                "Confirm Finish Event",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (result == DialogResult.Yes)
            {
                MarkEventAsCompleted(selectedEventId);
                MessageBox.Show("Event marked as completed successfully!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                LoadEvents(); // Refresh the event list
            }
        }

        // Modify the form closing event to ask if user wants to mark event as completed
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            int selectedEventId = GetSelectedEventId();
            if (selectedEventId != -1)
            {
                DialogResult result = MessageBox.Show(
                    "Would you like to mark this event as completed? This will hide it from future attendance sessions.",
                    "Finish Event",
                    MessageBoxButtons.YesNoCancel,
                    MessageBoxIcon.Question);

                if (result == DialogResult.Yes)
                {
                    MarkEventAsCompleted(selectedEventId);
                }
                else if (result == DialogResult.Cancel)
                {
                    e.Cancel = true; // Don't close the form if user cancels
                    return;
                }
            }

            StopAllScanners();
            if (scanners != null)
            {
                foreach (var scanner in scanners)
                {
                    scanner.Value.Dispose();
                }
                scanners.Clear();
            }
            base.OnFormClosing(e);
        }

        // Fingerprint processing methods
        protected Bitmap ConvertSampleToBitmap(DPFP.Sample Sample)
        {
            DPFP.Capture.SampleConversion Convertor = new DPFP.Capture.SampleConversion();
            Bitmap bitmap = null;
            Convertor.ConvertToPicture(Sample, ref bitmap);
            return bitmap;
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

        private void CheckForMatch(string scannerId, DPFP.FeatureSet features)
        {
            // Get the selected event ID on the UI thread first
            int selectedEventId = -1;
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(() =>
                {
                    selectedEventId = GetSelectedEventId();
                }));
            }
            else
            {
                selectedEventId = GetSelectedEventId();
            }

            if (selectedEventId == -1)
            {
                MakeReport($"[{scannerId}] Please select an event first");
                return;
            }

            string query = "SELECT id, uid, student_name, course, year_level, fingerprint_data FROM register_student";

            try
            {
                using (var connection = new MySqlConnection(connectionString))
                using (var command = new MySqlCommand(query, connection))
                {
                    connection.Open();
                    MakeReport($"[{scannerId}] Searching database for matches...");

                    using (var reader = command.ExecuteReader())
                    {
                        bool found = false;
                        int studentCount = 0;
                        int templateCount = 0;

                        while (reader.Read())
                        {
                            studentCount++;
                            string uid = reader["uid"].ToString();
                            string name = reader["student_name"].ToString();
                            string course = reader["course"].ToString();
                            string year = reader["year_level"].ToString();

                            if (reader["fingerprint_data"] is byte[] templateData && templateData.Length > 0)
                            {
                                templateCount++;
                                MakeReport($"[{scannerId}] Checking student: {name} (UID: {uid})");

                                if (VerifyTemplate(features, templateData))
                                {
                                    found = true;
                                    MakeReport($"[{scannerId}] ✓ MATCH FOUND: {name}");
                                    // Always invoke to ensure we're on the UI thread
                                    if (this.InvokeRequired)
                                    {
                                        this.Invoke(new Action(() =>
                                        {
                                            OnMatchFound(scannerId, name, uid, course, year, selectedEventId);
                                        }));
                                    }
                                    else
                                    {
                                        OnMatchFound(scannerId, name, uid, course, year, selectedEventId);
                                    }
                                    break;
                                }
                            }
                            else
                            {
                                MakeReport($"[{scannerId}] No fingerprint template for: {name}");
                            }
                        }

                        MakeReport($"[{scannerId}] Checked {studentCount} students, {templateCount} had fingerprint templates");

                        if (!found)
                        {
                            MakeReport($"[{scannerId}] ✗ No matching fingerprint found");
                            MakeReport($"[{scannerId}] Possible reasons:");
                            MakeReport($"[{scannerId}] - Fingerprint not enrolled in database");
                            MakeReport($"[{scannerId}] - Poor fingerprint scan quality");
                            MakeReport($"[{scannerId}] - Fingerprint scanner issue");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MakeReport($"[{scannerId}] Database error: {ex.Message}");
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
                    MakeReport($"Verification score: {result.FARAchieved}");
                    return result.Verified;
                }
            }
            catch (Exception ex)
            {
                MakeReport($"Verification error: {ex.Message}");
                return false;
            }
        }

        private void OnMatchFound(string scannerId, string name, string uid, string course, string year, int eventId)
        {
            MakeReport($"[{scannerId}] ✓ VERIFICATION SUCCESSFUL!");
            MakeReport($"[{scannerId}] Student: {name}");
            MakeReport($"[{scannerId}] UID: {uid}");
            MakeReport($"[{scannerId}] Course: {course}");
            MakeReport($"[{scannerId}] Year: {year}");

            RecordAttendance(scannerId, eventId, uid, name);
        }

        // UPDATED: RecordAttendance method with year-level checking and minimum session duration
        private void RecordAttendance(string scannerId, int eventId, string uid, string studentName)
        {
            try
            {
                using (var connection = new MySqlConnection(connectionString))
                {
                    connection.Open();

                    // Get student ID and year level first
                    string studentQuery = "SELECT id, year_level FROM register_student WHERE uid = @uid";
                    int studentId = -1;
                    string studentYearLevel = "";

                    using (var studentCmd = new MySqlCommand(studentQuery, connection))
                    {
                        studentCmd.Parameters.AddWithValue("@uid", uid);
                        using (var reader = studentCmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                studentId = Convert.ToInt32(reader["id"]);
                                studentYearLevel = reader["year_level"]?.ToString() ?? "";
                            }
                            else
                            {
                                MakeReport($"[{scannerId}] ✗ Student not found in database");
                                return;
                            }
                        }
                    }

                    // Check if event has year level restriction
                    string eventQuery = "SELECT event_name, year_level FROM admin_event WHERE event_id = @eventId";
                    string eventName = "";
                    string eventYearLevel = "";

                    using (var eventCmd = new MySqlCommand(eventQuery, connection))
                    {
                        eventCmd.Parameters.AddWithValue("@eventId", eventId);
                        using (var reader = eventCmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                eventName = reader["event_name"]?.ToString() ?? "";
                                eventYearLevel = reader["year_level"]?.ToString() ?? "";
                            }
                        }
                    }

                    // Check year level restriction
                    if (!string.IsNullOrEmpty(eventYearLevel) && studentYearLevel != eventYearLevel)
                    {
                        // Show friendly restriction message
                        string message = $"📚 Event Access Notice\n\n" +
                                       $"This event is specifically for {eventYearLevel} students.\n" +
                                       $"You are currently enrolled as a {studentYearLevel} student.\n\n" +
                                       $"If you believe this is a mistake or have special permission,\n" +
                                       $"please see the event organizer for assistance.";

                        // Show message box on UI thread
                        if (this.InvokeRequired)
                        {
                            this.Invoke(new Action(() =>
                            {
                                MessageBox.Show(message, "Event Access Restricted",
                                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                            }));
                        }
                        else
                        {
                            MessageBox.Show(message, "Event Access Restricted",
                                MessageBoxButtons.OK, MessageBoxIcon.Information);
                        }

                        MakeReport($"[{scannerId}] ✗ {studentName} ({studentYearLevel}) restricted from {eventName} (for {eventYearLevel} only)");
                        return; // Don't proceed with attendance recording
                    }

                    // Continue with normal attendance recording...
                    // Check current attendance status for this student and event
                    string checkQuery = @"SELECT time_in, time_out, attendance_status 
                              FROM students_events 
                              WHERE event_id = @eventId 
                              AND student_id = @studentId";

                    using (var checkCmd = new MySqlCommand(checkQuery, connection))
                    {
                        checkCmd.Parameters.AddWithValue("@eventId", eventId);
                        checkCmd.Parameters.AddWithValue("@studentId", studentId);

                        using (var reader = checkCmd.ExecuteReader())
                        {
                            if (reader.HasRows && reader.Read())
                            {
                                bool hasTimeIn = reader["time_in"] != DBNull.Value;
                                bool hasTimeOut = reader["time_out"] != DBNull.Value;
                                string currentStatus = reader["attendance_status"]?.ToString() ?? "absent";

                                reader.Close();

                                if (!hasTimeIn && !hasTimeOut)
                                {
                                    // FIRST SCAN: No time in or time out - RECORD TIME IN
                                    DateTime timeIn = DateTime.Now;
                                    string timeInQuery = @"UPDATE students_events 
                                           SET attendance_status = 'present', 
                                               time_in = @timeIn,
                                               recorded_by = @recordedBy
                                           WHERE event_id = @eventId 
                                           AND student_id = @studentId";

                                    using (var updateCmd = new MySqlCommand(timeInQuery, connection))
                                    {
                                        updateCmd.Parameters.AddWithValue("@eventId", eventId);
                                        updateCmd.Parameters.AddWithValue("@studentId", studentId);
                                        updateCmd.Parameters.AddWithValue("@timeIn", timeIn);
                                        updateCmd.Parameters.AddWithValue("@recordedBy", 56); // Default admin ID

                                        int rowsAffected = updateCmd.ExecuteNonQuery();

                                        if (rowsAffected > 0)
                                        {
                                            MakeReport($"[{scannerId}] ✓ {studentName} TIMED IN successfully at {timeIn:hh:mm tt}");

                                            // Check for late fine
                                            int attendanceId = GetAttendanceId(eventId, studentId);
                                            CheckAndGenerateLateFine(studentId, eventId, attendanceId, timeIn);
                                            LoadAttendanceData(eventId);
                                        }
                                        else
                                        {
                                            MakeReport($"[{scannerId}] ✗ Failed to record time in");
                                        }
                                    }
                                }
                                else if (hasTimeIn && !hasTimeOut)
                                {
                                    // SECOND SCAN: Has time in but no time out - CHECK MINIMUM SESSION DURATION
                                    string getTimeInQuery = @"SELECT time_in FROM students_events 
                                                     WHERE event_id = @eventId 
                                                     AND student_id = @studentId";

                                    using (var timeInCmd = new MySqlCommand(getTimeInQuery, connection))
                                    {
                                        timeInCmd.Parameters.AddWithValue("@eventId", eventId);
                                        timeInCmd.Parameters.AddWithValue("@studentId", studentId);

                                        object timeInResult = timeInCmd.ExecuteScalar();
                                        if (timeInResult != DBNull.Value && timeInResult != null)
                                        {
                                            DateTime timeIn = Convert.ToDateTime(timeInResult);
                                            TimeSpan sessionDuration = DateTime.Now - timeIn;

                                            // Check if minimum session duration has passed
                                            if (sessionDuration.TotalMinutes < minimumSessionMinutes)
                                            {
                                                // Show popup message
                                                if (this.InvokeRequired)
                                                {
                                                    this.Invoke(new Action(() =>
                                                    {
                                                        MessageBox.Show(
                                                            $"{studentName} cannot time out yet!\n\n" +
                                                            $"Time in: {timeIn:hh:mm tt}\n" +
                                                            $"Current time: {DateTime.Now:hh:mm tt}\n" +
                                                            $"Session duration: {sessionDuration.TotalMinutes:F0} minutes",
                                                            "Cannot Time Out Yet",
                                                            MessageBoxButtons.OK,
                                                            MessageBoxIcon.Warning);
                                                    }));
                                                }
                                                else
                                                {
                                                    MessageBox.Show(
                                                        $"{studentName} cannot time out yet!\n\n" +
                                                        $"Time in: {timeIn:hh:mm tt}\n" +
                                                        $"Current time: {DateTime.Now:hh:mm tt}\n" +
                                                        $"Session duration: {sessionDuration.TotalMinutes:F0} minutes",
                                                        "Cannot Time Out Yet",
                                                        MessageBoxButtons.OK,
                                                        MessageBoxIcon.Warning);
                                                }

                                                MakeReport($"[{scannerId}] ⚠ {studentName} attempted to time out too early ({sessionDuration.TotalMinutes:F0} minutes, need {minimumSessionMinutes} minutes)");
                                                return; // Don't proceed with time out
                                            }

                                            // If minimum duration has passed, allow time out
                                            string timeOutQuery = @"UPDATE students_events 
                                                           SET time_out = NOW()
                                                           WHERE event_id = @eventId 
                                                           AND student_id = @studentId";

                                            using (var updateCmd = new MySqlCommand(timeOutQuery, connection))
                                            {
                                                updateCmd.Parameters.AddWithValue("@eventId", eventId);
                                                updateCmd.Parameters.AddWithValue("@studentId", studentId);
                                                int rowsAffected = updateCmd.ExecuteNonQuery();

                                                if (rowsAffected > 0)
                                                {
                                                    MakeReport($"[{scannerId}] ✓ {studentName} TIMED OUT successfully after {sessionDuration.TotalMinutes:F0} minutes");
                                                    LoadAttendanceData(eventId);
                                                }
                                                else
                                                {
                                                    MakeReport($"[{scannerId}] ✗ Failed to record time out");
                                                }
                                            }
                                        }
                                    }
                                }
                                else if (hasTimeIn && hasTimeOut)
                                {
                                    // THIRD+ SCANS: Already completed both time in and time out
                                    MakeReport($"[{scannerId}] ⚠ {studentName} already completed attendance (both time in and out recorded)");
                                }
                                else
                                {
                                    // This shouldn't normally happen - has time out but no time in
                                    MakeReport($"[{scannerId}] ⚠ Invalid attendance state - time out exists but no time in");
                                }
                            }
                            else
                            {
                                reader.Close();

                                // No record exists - create new attendance record with time in
                                DateTime timeIn = DateTime.Now;
                                string insertQuery = @"INSERT INTO students_events 
                                      (event_id, student_id, attendance_status, time_in, recorded_by) 
                                      VALUES (@eventId, @studentId, 'present', @timeIn, @recordedBy)";

                                using (var insertCmd = new MySqlCommand(insertQuery, connection))
                                {
                                    insertCmd.Parameters.AddWithValue("@eventId", eventId);
                                    insertCmd.Parameters.AddWithValue("@studentId", studentId);
                                    insertCmd.Parameters.AddWithValue("@timeIn", timeIn);
                                    insertCmd.Parameters.AddWithValue("@recordedBy", 56); // Default admin ID

                                    int rowsAffected = insertCmd.ExecuteNonQuery();

                                    if (rowsAffected > 0)
                                    {
                                        MakeReport($"[{scannerId}] ✓ {studentName} TIMED IN successfully at {timeIn:hh:mm tt}");

                                        // Check for late fine
                                        int attendanceId = GetAttendanceId(eventId, studentId);
                                        CheckAndGenerateLateFine(studentId, eventId, attendanceId, timeIn);
                                        LoadAttendanceData(eventId);
                                    }
                                    else
                                    {
                                        MakeReport($"[{scannerId}] ✗ Failed to record attendance");
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MakeReport($"[{scannerId}] Error recording attendance: {ex.Message}");
            }
        }

        // Helper method to get attendance ID
        private int GetAttendanceId(int eventId, int studentId)
        {
            try
            {
                string query = "SELECT id FROM students_events WHERE event_id = @eventId AND student_id = @studentId";

                using (var connection = new MySqlConnection(connectionString))
                using (var command = new MySqlCommand(query, connection))
                {
                    connection.Open();
                    command.Parameters.AddWithValue("@eventId", eventId);
                    command.Parameters.AddWithValue("@studentId", studentId);

                    object result = command.ExecuteScalar();
                    return result != null ? Convert.ToInt32(result) : -1;
                }
            }
            catch (Exception)
            {
                return -1;
            }
        }

        // Helper method to get student ID by UID
        private int GetStudentIdByUid(string uid)
        {
            try
            {
                string query = "SELECT id FROM register_student WHERE uid = @uid";

                using (var connection = new MySqlConnection(connectionString))
                using (var command = new MySqlCommand(query, connection))
                {
                    connection.Open();
                    command.Parameters.AddWithValue("@uid", uid);

                    object result = command.ExecuteScalar();
                    return result != null ? Convert.ToInt32(result) : -1;
                }
            }
            catch (Exception)
            {
                return -1;
            }
        }

        // Your original events form methods
        private void FindListViewControl()
        {
            try
            {
                // Method 1: Direct assignment if you know the control name
                attendanceListView = listView1; // Change 'listView1' to your actual ListView name

                // Method 2: If direct assignment doesn't work, try finding by name
                if (attendanceListView == null)
                {
                    var control = this.Controls.Find("listView1", true).FirstOrDefault();
                    attendanceListView = control as ListView;
                }

                // Method 3: Search recursively through all controls
                if (attendanceListView == null)
                {
                    attendanceListView = FindControlRecursive<ListView>(this, "listView1");
                }

                // Method 4: Find any ListView on the form
                if (attendanceListView == null)
                {
                    attendanceListView = FindAnyListView(this);
                }

                if (attendanceListView == null)
                {
                    MessageBox.Show("ListView control not found! Please make sure you have a ListView named 'listView1' on your form.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    MakeReport("ERROR: ListView control not found!");
                }
                else
                {
                    MakeReport($"ListView found: {attendanceListView.Name}");
                    SetupListView();
                }
            }
            catch (Exception ex)
            {
                MakeReport($"Error finding ListView: {ex.Message}");
            }
        }

        // Helper method to find control recursively by name
        private T FindControlRecursive<T>(Control parent, string controlName) where T : Control
        {
            foreach (Control control in parent.Controls)
            {
                if (control.Name == controlName && control is T)
                {
                    return (T)control;
                }

                var found = FindControlRecursive<T>(control, controlName);
                if (found != null)
                    return found;
            }
            return null;
        }

        // Helper method to find any ListView
        private ListView FindAnyListView(Control parent)
        {
            foreach (Control control in parent.Controls)
            {
                if (control is ListView listView)
                {
                    return listView;
                }

                var found = FindAnyListView(control);
                if (found != null)
                    return found;
            }
            return null;
        }

        private void SetupListView()
        {
            if (attendanceListView == null)
            {
                MakeReport("ListView is null - cannot setup columns");
                return;
            }

            try
            {
                attendanceListView.Columns.Clear();

                // Calculate column widths based on form width for proper alignment
                int totalWidth = attendanceListView.Width - 25; // Account for scrollbar
                int[] columnWidths = {
                    (int)(totalWidth * 0.25), // Student Name - 25%
                    (int)(totalWidth * 0.15), // UID - 15%
                    (int)(totalWidth * 0.20), // Course - 20%
                    (int)(totalWidth * 0.10), // Year Level - 10%
                    (int)(totalWidth * 0.10), // Status - 10%
                    (int)(totalWidth * 0.10), // Time In - 10%
                    (int)(totalWidth * 0.10)  // Time Out - 10%
                };

                // Adjust if total exceeds available width
                int totalColumnWidth = columnWidths.Sum();
                if (totalColumnWidth > totalWidth)
                {
                    int difference = totalColumnWidth - totalWidth;
                    columnWidths[0] -= difference; // Reduce Student Name width
                }

                attendanceListView.Columns.Add("Student Name", columnWidths[0]);
                attendanceListView.Columns.Add("UID", columnWidths[1]);
                attendanceListView.Columns.Add("Course", columnWidths[2]);
                attendanceListView.Columns.Add("Year Level", columnWidths[3]);
                attendanceListView.Columns.Add("Status", columnWidths[4]);
                attendanceListView.Columns.Add("Time In", columnWidths[5]);
                attendanceListView.Columns.Add("Time Out", columnWidths[6]); // Fixed: No extra space

                attendanceListView.View = View.Details;
                attendanceListView.FullRowSelect = true;
                attendanceListView.GridLines = true;

                // Enable auto-resize for the last column to fill remaining space
                attendanceListView.Columns[6].Width = -2;

                MakeReport("ListView setup completed successfully with proper column alignment");
            }
            catch (Exception ex)
            {
                MakeReport($"Error setting up ListView: {ex.Message}");
            }
        }

        private class EventItem
        {
            public int EventId { get; set; }
            public string DisplayText { get; set; }

            public EventItem(int eventId, string displayText)
            {
                EventId = eventId;
                DisplayText = displayText;
            }

            public override string ToString()
            {
                return DisplayText;
            }
        }

        private int GetSelectedEventId()
        {
            if (this.InvokeRequired)
            {
                return (int)this.Invoke(new Func<int>(() =>
                {
                    if (comboBox1.SelectedItem is EventItem selectedEvent)
                    {
                        return selectedEvent.EventId;
                    }
                    return -1;
                }));
            }
            else
            {
                if (comboBox1.SelectedItem is EventItem selectedEvent)
                    return selectedEvent.EventId;
                return -1;
            }
        }

        private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            int selectedEventId = GetSelectedEventId();
            if (selectedEventId != -1)
            {
                MakeReport($"Event selected: {selectedEventId}");
                LoadAttendanceData(selectedEventId);
            }
            else
            {
                MakeReport("No event selected or invalid selection");
            }
        }

        // FIXED: LoadAttendanceData method with better error handling and debugging
        private void LoadAttendanceData(int eventId)
        {
            try
            {
                if (attendanceListView == null)
                {
                    MakeReport("ERROR: Cannot load attendance - ListView is null");
                    return;
                }

                // MODIFIED QUERY: Only show students who have scanned (present status)
                string query = @"SELECT rs.student_name, rs.uid, rs.course, rs.year_level, 
                        se.attendance_status, se.time_in, se.time_out
                 FROM students_events se
                 INNER JOIN register_student rs ON se.student_id = rs.id
                 WHERE se.event_id = @eventId 
                 AND se.attendance_status = 'present'  -- ONLY SHOW STUDENTS WHO SCANNED
                 ORDER BY se.time_in DESC";

                using (MySqlConnection connection = new MySqlConnection(connectionString))
                {
                    connection.Open();
                    MakeReport($"Loading scanned students for event ID: {eventId}");

                    using (MySqlCommand command = new MySqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@eventId", eventId);

                        // Use DataAdapter instead of DataReader for better compatibility
                        using (MySqlDataAdapter adapter = new MySqlDataAdapter(command))
                        {
                            DataTable dataTable = new DataTable();
                            adapter.Fill(dataTable);

                            // DEBUG: Check what data we retrieved
                            MakeReport($"DEBUG: Retrieved {dataTable.Rows.Count} rows from database");

                            if (dataTable.Rows.Count > 0)
                            {
                                MakeReport($"DEBUG: First student - Name: {dataTable.Rows[0]["student_name"]}, Status: {dataTable.Rows[0]["attendance_status"]}");
                            }

                            // Always use Invoke to ensure thread safety
                            if (attendanceListView.InvokeRequired)
                            {
                                attendanceListView.Invoke(new Action<DataTable>(UpdateListViewWithDataTable), dataTable);
                            }
                            else
                            {
                                UpdateListViewWithDataTable(dataTable);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MakeReport($"Error loading attendance: {ex.Message}");
                MessageBox.Show($"Error loading attendance: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // FIXED: Update ListView using DataTable with better error handling - REMOVED "N/A" VALUES
        private void UpdateListViewWithDataTable(DataTable dataTable)
        {
            if (attendanceListView == null)
            {
                MakeReport("ERROR: ListView is null in UpdateListViewWithDataTable");
                return;
            }

            try
            {
                attendanceListView.BeginUpdate();
                attendanceListView.Items.Clear();

                int recordCount = 0;

                foreach (DataRow row in dataTable.Rows)
                {
                    try
                    {
                        recordCount++;
                        string studentName = row["student_name"]?.ToString() ?? ""; // CHANGED: Empty string instead of "N/A"
                        string uid = row["uid"]?.ToString() ?? ""; // CHANGED: Empty string instead of "N/A"
                        string course = row["course"]?.ToString() ?? ""; // CHANGED: Empty string instead of "N/A"
                        string yearLevel = row["year_level"]?.ToString() ?? ""; // CHANGED: Empty string instead of "N/A"
                        string status = row["attendance_status"]?.ToString() ?? ""; // CHANGED: Empty string instead of "N/A"

                        ListViewItem item = new ListViewItem(studentName);
                        item.SubItems.Add(uid);
                        item.SubItems.Add(course);
                        item.SubItems.Add(yearLevel);
                        item.SubItems.Add(status);

                        // Time In
                        if (row["time_in"] != DBNull.Value && row["time_in"] != null)
                        {
                            DateTime timeIn = Convert.ToDateTime(row["time_in"]);
                            item.SubItems.Add(timeIn.ToString("hh:mm tt"));
                        }
                        else
                        {
                            item.SubItems.Add(""); // CHANGED: Empty string instead of "N/A"
                        }

                        // Time Out
                        if (row["time_out"] != DBNull.Value && row["time_out"] != null)
                        {
                            DateTime timeOut = Convert.ToDateTime(row["time_out"]);
                            item.SubItems.Add(timeOut.ToString("hh:mm tt"));
                        }
                        else
                        {
                            item.SubItems.Add(""); // CHANGED: Empty string instead of "N/A"
                        }

                        attendanceListView.Items.Add(item);
                    }
                    catch (Exception ex)
                    {
                        MakeReport($"Error adding row {recordCount} to ListView: {ex.Message}");
                    }
                }

                attendanceListView.EndUpdate();
                MakeReport($"Loaded {recordCount} scanned students");

                // Force refresh and ensure visibility
                attendanceListView.Refresh();
                attendanceListView.Focus();
                Application.DoEvents();
            }
            catch (Exception ex)
            {
                MakeReport($"Error updating ListView: {ex.Message}");
            }
        }

        // Add a button to manually generate fines for absent students
        private void btnGenerateFines_Click(object sender, EventArgs e)
        {
            int selectedEventId = GetSelectedEventId();
            if (selectedEventId == -1)
            {
                MessageBox.Show("Please select an event first.", "Information", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            DialogResult result = MessageBox.Show(
                "Generate fines for all absent students in this event?",
                "Generate Fines",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (result == DialogResult.Yes)
            {
                GenerateFinesForAbsentStudents(selectedEventId);
            }
        }

        private void btnRefresh_Click(object sender, EventArgs e)
        {
            LoadEvents();
        }

        private void start_scan_Click(object sender, EventArgs e)
        {
            if (GetSelectedEventId() == -1)
            {
                MessageBox.Show("Please select an event first.", "Information", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            StartAllScanners();
        }

        private void events_Load(object sender, EventArgs e)
        {
            this.WindowState = FormWindowState.Maximized;
            this.FormBorderStyle = FormBorderStyle.Sizable;

            // Set comboBox properties
            comboBox1.DropDownStyle = ComboBoxStyle.DropDownList;
            comboBox1.DropDownHeight = 200;

            MakeReport("Events form loaded successfully");
        }

        // Add this empty method to satisfy the designer reference
        private void timer1_Tick(object sender, EventArgs e)
        {
            // Empty method - timer functionality was removed
        }

        private void start_scan_Click_1(object sender, EventArgs e)
        {
            if (GetSelectedEventId() == -1)
            {
                MessageBox.Show("Please select an event first.", "Information", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            StartAllScanners();
        }

        private void stop_scan_Click(object sender, EventArgs e)
        {
            StopAllScanners();
        }

        // New methods to control individual scanners
        private void StartScanner1_Click(object sender, EventArgs e)
        {
            StartScanner("Scanner_1");
        }

        private void StopScanner1_Click(object sender, EventArgs e)
        {
            StopScanner("Scanner_1");
        }

        private void StartScanner2_Click(object sender, EventArgs e)
        {
            StartScanner("Scanner_2");
        }

        private void StopScanner2_Click(object sender, EventArgs e)
        {
            StopScanner("Scanner_2");
        }

        // NEW: Button to show all students (including absent)
        private void btnShowAll_Click(object sender, EventArgs e)
        {
            int selectedEventId = GetSelectedEventId();
            if (selectedEventId != -1)
            {
                LoadAllStudents(selectedEventId);
            }
        }

        // NEW: Button to show only scanned students
        private void btnShowScanned_Click(object sender, EventArgs e)
        {
            int selectedEventId = GetSelectedEventId();
            if (selectedEventId != -1)
            {
                LoadAttendanceData(selectedEventId); // This now shows only scanned students
            }
        }

        // NEW: Method to load all students (including absent)
        private void LoadAllStudents(int eventId)
        {
            try
            {
                if (attendanceListView == null)
                {
                    MakeReport("ERROR: Cannot load attendance - ListView is null");
                    return;
                }

                // Query to show ALL students (both present and absent)
                string query = @"SELECT rs.student_name, rs.uid, rs.course, rs.year_level, 
                        se.attendance_status, se.time_in, se.time_out
                 FROM students_events se
                 INNER JOIN register_student rs ON se.student_id = rs.id
                 WHERE se.event_id = @eventId
                 ORDER BY 
                    CASE WHEN se.attendance_status = 'present' THEN 1 ELSE 2 END,
                    se.time_in DESC";

                using (MySqlConnection connection = new MySqlConnection(connectionString))
                {
                    connection.Open();
                    MakeReport($"Loading ALL students for event ID: {eventId}");

                    using (MySqlCommand command = new MySqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@eventId", eventId);

                        using (MySqlDataAdapter adapter = new MySqlDataAdapter(command))
                        {
                            DataTable dataTable = new DataTable();
                            adapter.Fill(dataTable);

                            if (attendanceListView.InvokeRequired)
                            {
                                attendanceListView.Invoke(new Action<DataTable>(UpdateListViewWithDataTable), dataTable);
                            }
                            else
                            {
                                UpdateListViewWithDataTable(dataTable);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MakeReport($"Error loading all students: {ex.Message}");
                MessageBox.Show($"Error loading all students: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // NEW: Debug method to check database contents
        private void btnDebug_Click(object sender, EventArgs e)
        {
            int selectedEventId = GetSelectedEventId();
            if (selectedEventId == -1)
            {
                MessageBox.Show("Please select an event first.", "Information", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                using (var connection = new MySqlConnection(connectionString))
                {
                    connection.Open();

                    // Check what's actually in the database for this event
                    string debugQuery = @"SELECT rs.student_name, rs.uid, se.attendance_status, se.time_in, se.time_out
                                  FROM students_events se 
                                  INNER JOIN register_student rs ON se.student_id = rs.id 
                                  WHERE se.event_id = @eventId
                                  ORDER BY se.time_in DESC
                                  LIMIT 10";

                    using (var debugCmd = new MySqlCommand(debugQuery, connection))
                    {
                        debugCmd.Parameters.AddWithValue("@eventId", selectedEventId);

                        using (var reader = debugCmd.ExecuteReader())
                        {
                            MakeReport("DEBUG: Database contents for this event:");
                            int count = 0;
                            while (reader.Read())
                            {
                                count++;
                                string studentName = reader["student_name"].ToString();
                                string uid = reader["uid"].ToString();
                                string status = reader["attendance_status"].ToString();
                                string timeIn = reader["time_in"] != DBNull.Value ? Convert.ToDateTime(reader["time_in"]).ToString("HH:mm:ss") : "N/A";
                                string timeOut = reader["time_out"] != DBNull.Value ? Convert.ToDateTime(reader["time_out"]).ToString("HH:mm:ss") : "N/A";

                                MakeReport($"- {studentName} ({uid}): {status}, In: {timeIn}, Out: {timeOut}");
                            }
                            MakeReport($"DEBUG: Total records found: {count}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MakeReport($"Debug error: {ex.Message}");
            }
        }

        // ADD CLEAR LOG BUTTON FUNCTIONALITY
        private void btnClearLog_Click(object sender, EventArgs e)
        {
            StatusText.Clear();
            MakeReport("Log cleared - ready for new scans");
        }
    }
}