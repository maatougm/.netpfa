using System;
using System.Data;
using System.Windows.Forms;
using PFAA.UI.Helpers;

namespace PFAA.UI.Forms
{
    public partial class CreatePatientForm : Form
    {
        public CreatePatientForm()
        {
            InitializeComponent();
        }

        private void btnCreate_Click(object sender, EventArgs e)
        {
            string name = txtName.Text.Trim();
            string email = txtEmail.Text.Trim();
            string phone = txtPhone.Text.Trim();
            string address = txtAddress.Text.Trim();
            string password = txtPassword.Text;

            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                MessageBox.Show("Veuillez remplir tous les champs obligatoires.", "Erreur", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            // Check if email already exists
            var dt = DatabaseHelper.ExecuteQuery("SELECT id FROM users WHERE email = @email", new[] {
                new MySql.Data.MySqlClient.MySqlParameter("@email", email)
            });
            if (dt.Rows.Count > 0)
            {
                MessageBox.Show("Cet email est déjà utilisé.", "Erreur", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            // Insert new patient
            DatabaseHelper.ExecuteNonQuery(
                "INSERT INTO users (name, email, phone, address, password, role, status) VALUES (@name, @email, @phone, @address, @password, 'patient', 'active')",
                new[] {
                    new MySql.Data.MySqlClient.MySqlParameter("@name", name),
                    new MySql.Data.MySqlClient.MySqlParameter("@email", email),
                    new MySql.Data.MySqlClient.MySqlParameter("@phone", phone),
                    new MySql.Data.MySqlClient.MySqlParameter("@address", address),
                    new MySql.Data.MySqlClient.MySqlParameter("@password", password)
                }
            );
            MessageBox.Show("Patient créé avec succès.", "Succès", MessageBoxButtons.OK, MessageBoxIcon.Information);
            this.Close();
        }
    }
} 