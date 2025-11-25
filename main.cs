using DPFP.Processing;
using System;
using System.Windows.Forms;
using System.Drawing.Text;

namespace BiometricsFingerprint
{
    delegate void Function();
    public partial class main : Form
    {
        private DPFP.Template Template;

        public main()
        {
            InitializeComponent();
            this.Resize += main_Resize; // Handle resizing
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
            EnFrm.Show(this);
        }

        private void verify_btn_Click(object sender, EventArgs e)
        {
            try
            {
                verify verifyForm = new verify();
                verifyForm.ShowDialog(this);
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
                attendanceForm.ShowDialog(this);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Error opening verification form: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}