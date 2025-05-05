using System;
using System.Data;
using System.Windows.Forms;
using PFAA.UI.Helpers;

namespace PFAA.UI.Forms
{
    public partial class RegisterForm : Form
    {
        public RegisterForm()
        {
            InitializeComponent();
        }

        private void btnRegister_Click(object sender, EventArgs e)
        {
            string name = txtName.Text.Trim();
            string email = txtEmail.Text.Trim();
            string password = txtPassword.Text;
            string confirmPassword = txtConfirmPassword.Text;

            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                MessageBox.Show("Veuillez remplir tous les champs.", "Erreur", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (password != confirmPassword)
            {
                MessageBox.Show("Les mots de passe ne correspondent pas.", "Erreur", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
            // Insert new user (role: patient, status: active)
            DatabaseHelper.ExecuteNonQuery(
                "INSERT INTO users (name, email, password, role, status) VALUES (@name, @email, @password, 'patient', 'active')",
                new[] {
                    new MySql.Data.MySqlClient.MySqlParameter("@name", name),
                    new MySql.Data.MySqlClient.MySqlParameter("@email", email),
                    new MySql.Data.MySqlClient.MySqlParameter("@password", password)
                }
            );
            MessageBox.Show("Inscription réussie ! Vous pouvez maintenant vous connecter.", "Succès", MessageBoxButtons.OK, MessageBoxIcon.Information);
            this.Close();
        }
    }
} 