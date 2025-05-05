using System;
using System.Drawing;
using System.Windows.Forms;
using System.Data;
using PFAA.UI.Helpers;

namespace PFAA.UI.Forms
{
    public partial class MainForm : Form
    {
        private void ShowCreateAppointmentForm()
        {
            var form = new Form
            {
                Text = "Créer un rendez-vous",
                Size = new System.Drawing.Size(400, 400),
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false
            };

            // Patient dropdown
            var patientLabel = new Label { Text = "Patient:", Location = new System.Drawing.Point(20, 20) };
            var patientCombo = new ComboBox { Location = new System.Drawing.Point(20, 40), Width = 340, DropDownStyle = ComboBoxStyle.DropDownList };
            var patients = DatabaseHelper.ExecuteQuery("SELECT id, name FROM users WHERE role = 'patient' AND status = 'active' ORDER BY name");
            foreach (DataRow row in patients.Rows)
                patientCombo.Items.Add(new { Text = row["name"].ToString(), Value = row["id"] });
            patientCombo.DisplayMember = "Text";
            patientCombo.ValueMember = "Value";

            // Kiné dropdown (only for admin)
            ComboBox kineCombo = null;
            if (userRole == "admin")
            {
                var kineLabel = new Label { Text = "Kiné:", Location = new System.Drawing.Point(20, 80) };
                kineCombo = new ComboBox { Location = new System.Drawing.Point(20, 100), Width = 340, DropDownStyle = ComboBoxStyle.DropDownList };
                var kines = DatabaseHelper.ExecuteQuery("SELECT id, name FROM users WHERE role = 'therapist' AND status = 'active' ORDER BY name");
                foreach (DataRow row in kines.Rows)
                    kineCombo.Items.Add(new { Text = row["name"].ToString(), Value = row["id"] });
                kineCombo.DisplayMember = "Text";
                kineCombo.ValueMember = "Value";
                form.Controls.Add(kineLabel);
                form.Controls.Add(kineCombo);
            }

            // Cabinet dropdown
            var cabinetLabel = new Label { Text = "Cabinet:", Location = new System.Drawing.Point(20, 140) };
            var cabinetCombo = new ComboBox { Location = new System.Drawing.Point(20, 160), Width = 340, DropDownStyle = ComboBoxStyle.DropDownList };
            var cabinets = DatabaseHelper.ExecuteQuery("SELECT id, name FROM locations WHERE status = 'active' ORDER BY name");
            foreach (DataRow row in cabinets.Rows)
                cabinetCombo.Items.Add(new { Text = row["name"].ToString(), Value = row["id"] });
            cabinetCombo.DisplayMember = "Text";
            cabinetCombo.ValueMember = "Value";

            // Date and time pickers
            var dateLabel = new Label { Text = "Date:", Location = new System.Drawing.Point(20, 200) };
            var datePicker = new DateTimePicker { Location = new System.Drawing.Point(20, 220), Width = 160, Format = DateTimePickerFormat.Short };
            var timeLabel = new Label { Text = "Heure:", Location = new System.Drawing.Point(200, 200) };
            var timePicker = new DateTimePicker { Location = new System.Drawing.Point(200, 220), Width = 160, Format = DateTimePickerFormat.Time, ShowUpDown = true };

            // Save button
            var saveButton = new Button
            {
                Text = "Enregistrer",
                Location = new System.Drawing.Point(20, 270),
                Width = 340,
                BackColor = System.Drawing.Color.FromArgb(27, 53, 60),
                ForeColor = System.Drawing.Color.White,
                Font = new System.Drawing.Font("Segoe UI", 10, System.Drawing.FontStyle.Bold)
            };

            saveButton.Click += (s, e) =>
            {
                // Validation and insert logic here...
            };

            // Add controls to form
            form.Controls.AddRange(new Control[] {
                patientLabel, patientCombo,
                cabinetLabel, cabinetCombo,
                dateLabel, datePicker,
                timeLabel, timePicker,
                saveButton
            });

            form.ShowDialog();
        }
    }
} 