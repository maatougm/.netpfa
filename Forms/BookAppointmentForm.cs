using System;
using System.Data;
using System.Windows.Forms;
using PFAA.UI.Helpers;
using System.Collections.Generic;

namespace PFAA.UI.Forms
{
    public partial class BookAppointmentForm : Form
    {
        private int therapistId;
        private int selectedClientId = -1;
        private List<ComboBoxItem> allPatients = new List<ComboBoxItem>();

        public BookAppointmentForm(int therapistId)
        {
            this.therapistId = therapistId;
            InitializeComponent();
            InitializePatientCombo();
            InitializeLocationCombo();
            dtpDate.MinDate = DateTime.Today;
            dtpTime.Value = DateTime.Today.AddHours(8);
            btnBook.Click += (s, e) => BookAppointment();
        }

        private void InitializePatientCombo()
        {
            cbPatient.Items.Clear();
            allPatients.Clear();
            var patients = DatabaseHelper.ExecuteQuery("SELECT id, name FROM users WHERE role = 'client' AND status = 'active' ORDER BY name");
            foreach (DataRow row in patients.Rows)
            {
                allPatients.Add(new ComboBoxItem(row["name"].ToString(), Convert.ToInt32(row["id"])));
            }
            UpdatePatientComboBox("");
            cbPatient.DropDownStyle = ComboBoxStyle.DropDown;
            cbPatient.AutoCompleteMode = AutoCompleteMode.None;
            cbPatient.AutoCompleteSource = AutoCompleteSource.None;
            cbPatient.SelectedIndex = -1;
            cbPatient.TextChanged += (s, e) =>
            {
                string filter = cbPatient.Text.Trim();
                UpdatePatientComboBox(filter);
                cbPatient.DroppedDown = true;
                cbPatient.SelectionStart = cbPatient.Text.Length;
                cbPatient.SelectionLength = 0;
            };
            cbPatient.SelectedIndexChanged += (s, e) =>
            {
                if (cbPatient.SelectedItem is ComboBoxItem item)
                    selectedClientId = item.Value;
                else
                    selectedClientId = -1;
            };
        }

        private void UpdatePatientComboBox(string filter)
        {
            cbPatient.Items.Clear();
            IEnumerable<ComboBoxItem> filtered;
            if (string.IsNullOrWhiteSpace(filter))
                filtered = allPatients;
            else
                filtered = allPatients.FindAll(p => p.Text.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0);
            foreach (var item in filtered)
                cbPatient.Items.Add(item);
            cbPatient.DisplayMember = "Text";
            cbPatient.ValueMember = "Value";
        }

        private void InitializeLocationCombo()
        {
            cbLocation.Items.Clear();
            var locations = DatabaseHelper.ExecuteQuery("SELECT id, name FROM locations ORDER BY name");
            foreach (DataRow row in locations.Rows)
            {
                cbLocation.Items.Add(new ComboBoxItem(row["name"].ToString(), Convert.ToInt32(row["id"])));
            }
            cbLocation.DisplayMember = "Text";
            cbLocation.ValueMember = "Value";
        }

        private void BookAppointment()
        {
            if (cbPatient.SelectedItem == null)
            {
                MessageBox.Show("Veuillez sélectionner un patient.", "Erreur", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (cbLocation.SelectedItem == null)
            {
                MessageBox.Show("Veuillez sélectionner un lieu.", "Erreur", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            int locationId = ((ComboBoxItem)cbLocation.SelectedItem).Value;
            int clientId = ((ComboBoxItem)cbPatient.SelectedItem).Value;
            DateTime date = dtpDate.Value.Date;
            TimeSpan time = dtpTime.Value.TimeOfDay;

        
            // Insert appointment
            DatabaseHelper.ExecuteNonQuery(
                "INSERT INTO appointments (user_id, location_id, date, hour, status) VALUES (@uid, @tid, @lid, @date, @hour, 'confirmed')",
                new[] {
                    new MySql.Data.MySqlClient.MySqlParameter("@uid", clientId),
                    new MySql.Data.MySqlClient.MySqlParameter("@lid", locationId),
                    new MySql.Data.MySqlClient.MySqlParameter("@date", date),
                    new MySql.Data.MySqlClient.MySqlParameter("@hour", time)
                }
            );
            MessageBox.Show("Rendez-vous créé avec succès.", "Succès", MessageBoxButtons.OK, MessageBoxIcon.Information);
            this.Close();
        }
    }

    // Helper class for ComboBox items
    public class ComboBoxItem
    {
        public string Text { get; set; }
        public int Value { get; set; }
        public ComboBoxItem(string text, int value)
        {
            Text = text;
            Value = value;
        }
        public override string ToString() => Text;
    }
} 