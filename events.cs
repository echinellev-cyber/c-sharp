using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using DPFP;
using DPFP.Capture;
using DPFP.Processing;
using DPFP.Verification;
using System.Threading.Tasks;

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
        private string stationDepartmentFilter = ""; // e.g. "Medical Sciences" - only show events for this department
        // private bool _isTimerUpdate = false; // REMOVED: Flag for silent updates - bad pattern
        private bool _isLoading = false; // Flag to prevent overlapping async calls

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

            // Auto-start/stop scanners on focus
            this.Activated += (s, e) => StartAllScanners();
            this.Deactivate += (s, e) => StopAllScanners();
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
                "year.*restricted",
                "Course Restriction"
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
            private TextBox promptLabel;
            private Label statusLabel;
            private TextBox statusText; // CHANGED: From RichTextBox to TextBox

            public ScannerHandler(events parent, string id, PictureBox image, TextBox prompt, Label status, TextBox text) // CHANGED: TextBox parameter
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
                // Create a deep copy immediately to avoid GDI+ issues with the source bitmap
                // "Parameter is not valid" often happens when the source bitmap is disposed 
                // while the UI thread is trying to render or marshal it.
                Bitmap bitmapCopy = new Bitmap(bitmap);

                if (fingerprintImage != null && !fingerprintImage.IsDisposed && fingerprintImage.InvokeRequired)
                {
                    try 
                    {
                        fingerprintImage.Invoke(new Action<Bitmap>((bmp) => {
                            try 
                            {
                                if (fingerprintImage != null && !fingerprintImage.IsDisposed)
                                {
                                    if (fingerprintImage.Image != null)
                                    {
                                        var oldImage = fingerprintImage.Image;
                                        fingerprintImage.Image = null;
                                        oldImage.Dispose();
                                    }
                                    // Resize to fit the control
                                    fingerprintImage.Image = new Bitmap(bmp, fingerprintImage.Size);
                                }
                            }
                            finally
                            {
                                // Clean up the copy we passed to the UI thread
                                bmp.Dispose();
                            }
                        }), bitmapCopy);
                    }
                    catch 
                    { 
                        // If Invoke fails, we must dispose the copy
                        bitmapCopy.Dispose(); 
                    }
                }
                else if (fingerprintImage != null && !fingerprintImage.IsDisposed)
                {
                    try
                    {
                        if (fingerprintImage.Image != null)
                        {
                            var oldImage = fingerprintImage.Image;
                            fingerprintImage.Image = null;
                            oldImage.Dispose();
                        }
                        fingerprintImage.Image = new Bitmap(bitmapCopy, fingerprintImage.Size);
                    }
                    finally
                    {
                        bitmapCopy.Dispose();
                    }
                }
                else
                {
                    bitmapCopy.Dispose();
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

        private TextBox GetScannerPromptControl(string scannerId)
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
            return control as TextBox;
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

        private bool _isProcessingFingerprint = false;
        private object _processingLock = new object();

        // Process fingerprint sample from specific scanner
        protected void Process(string scannerId, DPFP.Sample Sample)
        {
            lock (_processingLock)
            {
                if (_isProcessingFingerprint) return;
                _isProcessingFingerprint = true;
            }

            try
            {
                if (scannerHandlers.ContainsKey(scannerId))
            {
                // ConvertSampleToBitmap creates a new Bitmap. DrawPicture takes ownership.
                // However, we must ensure we don't leak it if DrawPicture copies it instead of using it directly.
                // In our implementation, DrawPicture creates a NEW Bitmap from the input: new Bitmap(bitmap, size)
                // So the input bitmap MUST be disposed here!
                
                using (Bitmap rawBitmap = ConvertSampleToBitmap(Sample))
                {
                    scannerHandlers[scannerId].DrawPicture(rawBitmap);
                }
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
            finally
            {
                _isProcessingFingerprint = false;
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
                string query = "SELECT setting_name, setting_value FROM settings WHERE setting_name IN ('late_threshold_minutes', 'minimum_session_minutes', 'station_department')";

                using (var connection = new MySqlConnection(connectionString))
                using (var command = new MySqlCommand(query, connection))
                {
                    connection.Open();
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string settingName = reader["setting_name"].ToString();
                            string settingValue = reader["setting_value"]?.ToString()?.Trim() ?? "";

                            switch (settingName)
                            {
                                case "late_threshold_minutes":
                                    lateThresholdMinutes = int.Parse(settingValue);
                                    break;
                                case "minimum_session_minutes":
                                    minimumSessionMinutes = int.Parse(settingValue);
                                    break;
                                case "station_department":
                                    stationDepartmentFilter = settingValue;
                                    break;
                            }
                        }
                    }
                }
                // Fallback: App.config (for Medical Sciences station, set StationDepartment=Medical Sciences)
                if (string.IsNullOrWhiteSpace(stationDepartmentFilter))
                {
                    stationDepartmentFilter = ConfigurationManager.AppSettings["StationDepartment"]?.Trim() ?? "";
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
                using (MySqlConnection connection = new MySqlConnection(connectionString))
                {
                    connection.Open();

                    // Try with allowed_course; JOIN admin to get creator's department when allowed_course is empty
                    string queryWithCourse = @"SELECT ae.event_id, ae.event_name, ae.allowed_course, ae.date, ae.start_time, ae.end_time, ae.location, a.department as creator_department 
                                FROM admin_event ae 
                                LEFT JOIN admin a ON ae.created_by = a.admin_id 
                                WHERE (ae.status != 'Completed' OR ae.status IS NULL) 
                                ORDER BY ae.date DESC, ae.start_time DESC";
                    string queryWithoutCourse = @"SELECT ae.event_id, ae.event_name, ae.date, ae.start_time, ae.end_time, ae.location, a.department as creator_department 
                                FROM admin_event ae 
                                LEFT JOIN admin a ON ae.created_by = a.admin_id 
                                WHERE (ae.status != 'Completed' OR ae.status IS NULL) 
                                ORDER BY ae.date DESC, ae.start_time DESC";

                    bool useAllowedCourse = true;
                    try
                    {
                        using (var testCmd = new MySqlCommand("SELECT allowed_course FROM admin_event LIMIT 1", connection))
                        {
                            testCmd.ExecuteScalar();
                        }
                    }
                    catch
                    {
                        useAllowedCourse = false;
                    }

                    string query = useAllowedCourse ? queryWithCourse : queryWithoutCourse;

                    using (MySqlCommand command = new MySqlCommand(query, connection))
                    using (MySqlDataReader reader = command.ExecuteReader())
                    {
                        comboBox1.Items.Clear();
                        int count = 0;

                        while (reader.Read())
                        {
                            count++;
                            string eventName = reader["event_name"].ToString();
                            string allowedCourse = useAllowedCourse ? reader["allowed_course"]?.ToString() : "";
                            string creatorDept = "";
                            try { creatorDept = reader["creator_department"]?.ToString()?.Trim() ?? ""; } catch { }
                            DateTime eventDate = Convert.ToDateTime(reader["date"]);
                            TimeSpan startTime = (TimeSpan)reader["start_time"];
                            TimeSpan endTime = (TimeSpan)reader["end_time"];
                            string location = reader["location"].ToString();

                            // When allowed_course is empty, use creator's department (CASE-IT for Arts & Sciences, etc.)
                            string courseDisplay = GetCourseDisplayForDropdown(allowedCourse, creatorDept);
                            // Filter by station department: if set, only show events for that department
                            if (!string.IsNullOrWhiteSpace(stationDepartmentFilter) &&
                                !string.Equals(courseDisplay, stationDepartmentFilter.Trim(), StringComparison.OrdinalIgnoreCase))
                                continue;
                            string displayText = $"{eventName} - {courseDisplay} - {eventDate:MMM dd, yyyy} ({startTime:hh\\:mm} - {endTime:hh\\:mm}) - {location}";

                            comboBox1.Items.Add(new EventItem(
                                Convert.ToInt32(reader["event_id"]),
                                displayText
                            ));
                        }

                        if (count == 0)
                        {
                            MakeReport("No active events found. Create events in the admin panel (Events Management) or ensure existing events are not marked as Completed.");
                        }
                        else
                        {
                            MakeReport($"Loaded {count} event(s)");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MakeReport($"Error loading events: {ex.Message}");
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

                    // Get student ID, course, and year level
                    string studentQuery = "SELECT id, course, year_level FROM register_student WHERE uid = @uid";
                    int studentId = -1;
                    string studentCourse = "";
                    string studentYearLevel = "";

                    using (var studentCmd = new MySqlCommand(studentQuery, connection))
                    {
                        studentCmd.Parameters.AddWithValue("@uid", uid);
                        using (var reader = studentCmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                studentId = Convert.ToInt32(reader["id"]);
                                studentCourse = reader["course"]?.ToString() ?? "";
                                studentYearLevel = reader["year_level"]?.ToString() ?? "";
                            }
                            else
                            {
                                MakeReport($"[{scannerId}] ✗ Student not found in database");
                                return;
                            }
                        }
                    }

                    // Check if event has course or year level restriction
                    string eventName = "";
                    string allowedCourse = "";
                    string eventYearLevel = "";

                    // Try with allowed_course and created_by; fall back if column doesn't exist
                    string eventQueryWithCourse = @"SELECT event_name, allowed_course, year_level, created_by FROM admin_event WHERE event_id = @eventId";
                    string eventQueryWithoutCourse = @"SELECT event_name, year_level, created_by FROM admin_event WHERE event_id = @eventId";

                    bool hasAllowedCourseColumn = false;
                    try
                    {
                        using (var testCmd = new MySqlCommand("SELECT allowed_course FROM admin_event LIMIT 1", connection))
                        {
                            testCmd.ExecuteScalar();
                            hasAllowedCourseColumn = true;
                        }
                    }
                    catch { }

                    int? createdBy = null;
                    string eventQuery = hasAllowedCourseColumn ? eventQueryWithCourse : eventQueryWithoutCourse;
                    using (var eventCmd = new MySqlCommand(eventQuery, connection))
                    {
                        eventCmd.Parameters.AddWithValue("@eventId", eventId);
                        using (var reader = eventCmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                eventName = reader["event_name"]?.ToString() ?? "";
                                allowedCourse = hasAllowedCourseColumn ? (reader["allowed_course"]?.ToString() ?? "").Trim() : "";
                                eventYearLevel = reader["year_level"]?.ToString() ?? "";
                                if (reader["created_by"] != DBNull.Value && reader["created_by"] != null)
                                    createdBy = Convert.ToInt32(reader["created_by"]);
                            }
                        }
                    }

                    // FALLBACK: When allowed_course is empty, get creator's department from admin table and apply restriction
                    if (string.IsNullOrEmpty(allowedCourse) && createdBy.HasValue)
                    {
                        string creatorDept = "";
                        using (var deptCmd = new MySqlCommand("SELECT department FROM admin WHERE admin_id = @aid", connection))
                        {
                            deptCmd.Parameters.AddWithValue("@aid", createdBy.Value);
                            var deptObj = deptCmd.ExecuteScalar();
                            if (deptObj != null && deptObj != DBNull.Value)
                                creatorDept = (deptObj.ToString() ?? "").Trim();
                        }
                        if (!string.IsNullOrEmpty(creatorDept))
                        {
                            allowedCourse = GetAllowedCoursesFromDepartment(creatorDept);
                            // Auto-fix: update event in DB so we don't need to look up again
                            if (!string.IsNullOrEmpty(allowedCourse))
                            {
                                try
                                {
                                    using (var updateCmd = new MySqlCommand("UPDATE admin_event SET allowed_course = @ac WHERE event_id = @eid", connection))
                                    {
                                        updateCmd.Parameters.AddWithValue("@ac", allowedCourse);
                                        updateCmd.Parameters.AddWithValue("@eid", eventId);
                                        updateCmd.ExecuteNonQuery();
                                    }
                                }
                                catch { }
                            }
                        }
                    }

                    // Check COURSE restriction - when event is IT-only, other courses cannot time in
                    if (!string.IsNullOrEmpty(allowedCourse))
                    {
                        string normalizedAllowed = allowedCourse.Trim().ToLower();
                        string normalizedStudent = studentCourse.Trim().ToLower();

                        bool courseMatch = false;

                        // Handle multiple courses if separated by commas
                        string[] allowedCourses = normalizedAllowed.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                        foreach (string course in allowedCourses)
                        {
                            if (normalizedStudent.Contains(course.Trim()) || course.Trim().Contains(normalizedStudent))
                            {
                                courseMatch = true;
                                break;
                            }
                        }

                        // Also check for common variations (IT, Information Technology, etc.)
                        if (!courseMatch)
                        {
                            if (normalizedAllowed.Contains("information technology") &&
                                (normalizedStudent.Contains("it") || normalizedStudent.Contains("information tech")))
                                courseMatch = true;
                            else if (normalizedAllowed.Contains("business administration") &&
                                     normalizedStudent.Contains("business admin"))
                                courseMatch = true;
                            else if (normalizedAllowed.Contains("accountancy") &&
                                     normalizedStudent.Contains("accountancy"))
                                courseMatch = true;
                            else if (normalizedAllowed.Contains("nursing") &&
                                     normalizedStudent.Contains("nursing"))
                                courseMatch = true;
                            else if (normalizedAllowed.Contains("education") &&
                                     (normalizedStudent.Contains("education") || normalizedStudent.Contains("elementary") || normalizedStudent.Contains("secondary")))
                                courseMatch = true;
                            else if (normalizedAllowed.Contains("criminology") &&
                                     normalizedStudent.Contains("criminology"))
                                courseMatch = true;
                            else if (normalizedAllowed.Contains("engineering") &&
                                     (normalizedStudent.Contains("civil") || normalizedStudent.Contains("electrical") || normalizedStudent.Contains("engineering")))
                                courseMatch = true;
                        }

                        if (!courseMatch)
                        {
                            // Friendly department names for popup (e.g. "This event is only for Medical. You are IT.")
                            string eventDept = GetDepartmentDisplayName(allowedCourse);
                            string studentDept = GetStudentDepartmentDisplayName(studentCourse);
                            string message = $"📚 COURSE RESTRICTION\n\n" +
                                           $"This event is only for {eventDept} students.\n\n" +
                                           $"You are from {studentDept}.\n\n" +
                                           $"❌ ACCESS DENIED\n\n" +
                                           $"If you believe this is a mistake or have special permission,\n" +
                                           $"please see the event organizer for assistance.";

                            if (this.InvokeRequired)
                            {
                                this.Invoke(new Action(() =>
                                {
                                    MessageBox.Show(message, "Course Restriction",
                                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                                }));
                            }
                            else
                            {
                                MessageBox.Show(message, "Course Restriction",
                                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            }

                            MakeReport($"[{scannerId}] ✗ {studentName} ({studentCourse}) restricted from {eventName} (for {allowedCourse} only)");
                            return;
                        }
                    }

                    // Check year level restriction (with normalized comparison for "4" vs "4th Year" etc.)
                    if (!string.IsNullOrEmpty(eventYearLevel) && !YearLevelsMatch(studentYearLevel, eventYearLevel))
                    {
                        // Show friendly restriction message (format "5" -> "5th year student")
                        string eventYearDisplay = GetYearLevelDisplayForMessage(eventYearLevel);
                        string studentYearDisplay = GetYearLevelDisplayForMessage(studentYearLevel);
                        string message = $"📚 Event Access Notice\n\n" +
                                       $"This event is specifically for {eventYearDisplay} students.\n" +
                                       $"You are currently enrolled as a {studentYearDisplay} student.\n\n" +
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
                                            var task = LoadAttendanceData(eventId, true);
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
                                                    var task = LoadAttendanceData(eventId, true);
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
                                        var task = LoadAttendanceData(eventId, true);
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

        // Helper: Format year level for display in messages ("5" -> "5th year student", "4" -> "4th year")
        private string GetYearLevelDisplayForMessage(string yearLevel)
        {
            if (string.IsNullOrWhiteSpace(yearLevel)) return yearLevel ?? "";
            string y = yearLevel.Trim();
            // Handle numeric-only (e.g. "5", "4")
            if (y == "1") return "1st year";
            if (y == "2") return "2nd year";
            if (y == "3") return "3rd year";
            if (y == "4") return "4th year";
            if (y == "5") return "5th year";
            string yLower = y.ToLower();
            if (yLower.Contains("1st")) return "1st year";
            if (yLower.Contains("2nd")) return "2nd year";
            if (yLower.Contains("3rd")) return "3rd year";
            if (yLower.Contains("4th")) return "4th year";
            if (yLower.Contains("5th")) return "5th year";
            return yearLevel;
        }

        // Helper: Normalize year level for comparison (handles "4" vs "4th Year", "3" vs "3rd Year", etc.)
        private bool YearLevelsMatch(string studentYear, string eventYear)
        {
            if (string.IsNullOrWhiteSpace(studentYear) || string.IsNullOrWhiteSpace(eventYear))
                return false;

            string s = studentYear.Trim().ToLower();
            string e = eventYear.Trim().ToLower();

            // Exact match
            if (s == e) return true;

            // Normalize to numeric form for comparison
            string Normalize(string y)
            {
                if (y.Contains("1st") || y == "1") return "1";
                if (y.Contains("2nd") || y == "2") return "2";
                if (y.Contains("3rd") || y == "3") return "3";
                if (y.Contains("4th") || y == "4") return "4";
                if (y.Contains("5th") || y == "5") return "5";
                return y;
            }

            return Normalize(s) == Normalize(e);
        }

        // Helper: Get allowed courses from admin department (matches course_mapping.json)
        private string GetAllowedCoursesFromDepartment(string department)
        {
            if (string.IsNullOrWhiteSpace(department)) return "";
            string dept = department.Trim();
            // Format: "BS in Information Technology,BS in Computer Science,..." (matches register_student.course)
            var mapping = new System.Collections.Generic.Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Information Technology"] = "BS in Information Technology",
                ["Arts, Science, Education & Information Technology"] = "BS in Information Technology,BS in Education,BS in Elementary Education,BS in Secondary Education",
                ["Business, Management & Accountancy"] = "BS in Accountancy,BS in Accounting Information Systems,BS in Business Administration,BS in Hospitality Management,BS in Tourism Management",
                ["Criminology"] = "BS in Criminology",
                ["Engineering & Technology"] = "BS in Civil Engineering,BS in Electrical Engineering,BS in Mechanical Engineering",
                ["Medical Sciences"] = "BS in Midwifery,BS in Nursing"
            };
            if (mapping.TryGetValue(dept, out string courses)) return courses;
            foreach (var kv in mapping)
                if (kv.Key.IndexOf(dept, StringComparison.OrdinalIgnoreCase) >= 0 || dept.IndexOf(kv.Key, StringComparison.OrdinalIgnoreCase) >= 0)
                    return kv.Value;
            return "";
        }

        // Helper: Get short display for event dropdown (CASE-IT for Arts & Sciences, EPT for Engineering, etc.)
        private string GetCourseDisplayForDropdown(string allowedCourse, string creatorDept)
        {
            if (string.IsNullOrWhiteSpace(allowedCourse))
            {
                if (!string.IsNullOrWhiteSpace(creatorDept))
                    return GetDepartmentDisplayForDropdown(creatorDept);
                return "CASE-IT";
            }
            string lower = allowedCourse.Trim().ToLower();
            if (lower.Contains("nursing") || lower.Contains("midwifery")) return "Medical Sciences";
            if (lower.Contains("civil") || lower.Contains("electrical") || lower.Contains("mechanical")) return "EPT";
            if (lower.Contains("criminology")) return "Criminology";
            if (lower.Contains("accountancy") || lower.Contains("business") || lower.Contains("hospitality") || lower.Contains("tourism")) return "Business, Management & Accountancy";
            // Arts & Sciences: IT, Education, Elementary Ed, Secondary Ed
            if (lower.Contains("information technology") || lower.Contains("elementary education") || lower.Contains("secondary education") || (lower.Contains("education") && !lower.Contains("engineering")))
                return "CASE-IT";
            return allowedCourse;
        }

        private string GetDepartmentDisplayForDropdown(string department)
        {
            if (string.IsNullOrWhiteSpace(department)) return "CASE-IT";
            string d = department.Trim();
            if (d.IndexOf("Engineering", StringComparison.OrdinalIgnoreCase) >= 0 && d.IndexOf("Information", StringComparison.OrdinalIgnoreCase) < 0) return "EPT";
            if (d.IndexOf("Medical", StringComparison.OrdinalIgnoreCase) >= 0) return "Medical Sciences";
            if (d.IndexOf("Criminology", StringComparison.OrdinalIgnoreCase) >= 0) return "Criminology";
            if (d.IndexOf("Business", StringComparison.OrdinalIgnoreCase) >= 0 || d.IndexOf("Accountancy", StringComparison.OrdinalIgnoreCase) >= 0) return "Business, Management & Accountancy";
            if (d.IndexOf("Arts", StringComparison.OrdinalIgnoreCase) >= 0 || d.IndexOf("Science", StringComparison.OrdinalIgnoreCase) >= 0 || d.IndexOf("Education", StringComparison.OrdinalIgnoreCase) >= 0 || d.IndexOf("Information Technology", StringComparison.OrdinalIgnoreCase) >= 0)
                return "CASE-IT";
            return department;
        }

        // Helper: Get friendly department name from allowed_course (e.g. "Medical Sciences", "Arts & Sciences")
        private string GetDepartmentDisplayName(string allowedCourse)
        {
            if (string.IsNullOrWhiteSpace(allowedCourse)) return "this department";
            string lower = allowedCourse.Trim().ToLower();
            if (lower.Contains("nursing") || lower.Contains("midwifery")) return "Medical Sciences";
            if (lower.Contains("information technology") || lower.Contains("computer science") || lower.Contains("biology") ||
                lower.Contains("psychology") || lower.Contains("education")) return "Arts & Sciences";
            if (lower.Contains("accountancy") || lower.Contains("business") || lower.Contains("hospitality") || lower.Contains("tourism")) return "Business, Management & Accountancy";
            if (lower.Contains("criminology")) return "Criminology";
            if (lower.Contains("engineering") || lower.Contains("civil") || lower.Contains("electrical") || lower.Contains("mechanical")) return "Engineering & Technology";
            return "this department";
        }

        // Helper: Get friendly department/course name for student (e.g. "IT", "Medical")
        private string GetStudentDepartmentDisplayName(string studentCourse)
        {
            if (string.IsNullOrWhiteSpace(studentCourse)) return "another department";
            string lower = studentCourse.Trim().ToLower();
            if (lower.Contains("nursing") || lower.Contains("midwifery")) return "Medical Sciences";
            if (lower.Contains("information technology")) return "IT";
            if (lower.Contains("computer science") || lower.Contains("biology") || lower.Contains("psychology") || lower.Contains("education")) return "Arts & Sciences";
            if (lower.Contains("accountancy") || lower.Contains("business") || lower.Contains("hospitality") || lower.Contains("tourism")) return "Business, Management & Accountancy";
            if (lower.Contains("criminology")) return "Criminology";
            if (lower.Contains("engineering") || lower.Contains("civil") || lower.Contains("electrical") || lower.Contains("mechanical")) return "Engineering & Technology";
            return studentCourse;
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
                var task = LoadAttendanceData(selectedEventId, false);
            }
            else
            {
                MakeReport("No event selected or invalid selection");
            }
        }

        // FIXED: LoadAttendanceData method with better error handling and debugging
        private async Task LoadAttendanceData(int eventId, bool isSilent = false)
        {
            if (_isLoading) return; // Prevent overlapping calls
            _isLoading = true;

            try
            {
                if (attendanceListView == null)
                {
                    if (!isSilent) MakeReport("ERROR: Cannot load attendance - ListView is null");
                    return;
                }

                await Task.Run(() =>
                {
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
                        if (!isSilent) this.Invoke(new Action(() => MakeReport($"Loading scanned students for event ID: {eventId}")));

                        using (MySqlCommand command = new MySqlCommand(query, connection))
                        {
                            command.Parameters.AddWithValue("@eventId", eventId);

                            // Use DataAdapter instead of DataReader for better compatibility
                            using (MySqlDataAdapter adapter = new MySqlDataAdapter(command))
                            {
                                DataTable dataTable = new DataTable();
                                adapter.Fill(dataTable);

                                // DEBUG: Check what data we retrieved
                                if (!isSilent) this.Invoke(new Action(() => MakeReport($"DEBUG: Retrieved {dataTable.Rows.Count} rows from database")));

                                if (dataTable.Rows.Count > 0 && !isSilent)
                                {
                                    this.Invoke(new Action(() => MakeReport($"DEBUG: First student - Name: {dataTable.Rows[0]["student_name"]}, Status: {dataTable.Rows[0]["attendance_status"]}")));
                                }

                                // Always use Invoke to ensure thread safety
                                if (attendanceListView.InvokeRequired)
                                {
                                    attendanceListView.Invoke(new Action<DataTable, bool>(UpdateListViewWithDataTable), dataTable, isSilent);
                                }
                                else
                                {
                                    UpdateListViewWithDataTable(dataTable, isSilent);
                                }
                            }
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                if (!isSilent)
                {
                    this.Invoke(new Action(() =>
                    {
                        MakeReport($"Error loading attendance: {ex.Message}");
                        MessageBox.Show($"Error loading attendance: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }));
                }
            }
            finally
            {
                _isLoading = false;
            }
        }

        // FIXED: Update ListView using DataTable with better error handling - REMOVED "N/A" VALUES
        private void UpdateListViewWithDataTable(DataTable dataTable, bool isSilent)
        {
            if (attendanceListView == null)
            {
                if (!isSilent) MakeReport("ERROR: ListView is null in UpdateListViewWithDataTable");
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
                        if (!isSilent) MakeReport($"Error adding row {recordCount} to ListView: {ex.Message}");
                    }
                }

                attendanceListView.EndUpdate();
                if (!isSilent) MakeReport($"Loaded {recordCount} scanned students");

                // Force refresh and ensure visibility
                attendanceListView.Refresh();
                Application.DoEvents();
            }
            catch (Exception ex)
            {
                if (!isSilent) MakeReport($"Error updating ListView: {ex.Message}");
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

            // Setup timer for auto-refresh
            timer1.Interval = 1000; // 1 second
            timer1.Tick += timer1_Tick;
            timer1.Start();

            MakeReport("Events form loaded successfully. Auto-refresh enabled.");
        }

        // Add this empty method to satisfy the designer reference
        private async void timer1_Tick(object sender, EventArgs e)
        {
            int selectedEventId = GetSelectedEventId();
            if (selectedEventId != -1)
            {
                await LoadAttendanceData(selectedEventId, true);
            }
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
                var task = LoadAttendanceData(selectedEventId, false); // This now shows only scanned students
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
                                attendanceListView.Invoke(new Action<DataTable, bool>(UpdateListViewWithDataTable), dataTable, false);
                            }
                            else
                            {
                                UpdateListViewWithDataTable(dataTable, false);
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