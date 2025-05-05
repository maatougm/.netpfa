using System;
using System.Data;
using System.Windows.Forms;
using PFAA.UI.Helpers;

namespace PFAA.UI.Forms
{
    public partial class LoginForm : Form
    {
        public LoginForm()
        {
            InitializeComponent();
        }

        private void btnLogin_Click(object sender, EventArgs e)
        {
            string email = txtEmail.Text.Trim();
            string password = txtPassword.Text;

            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                MessageBox.Show("Veuillez saisir l'email et le mot de passe.", "Erreur", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var dt = DatabaseHelper.ExecuteQuery("SELECT id, role, password FROM users WHERE email = @email", new[] {
                new MySql.Data.MySqlClient.MySqlParameter("@email", email)
            });
            if (dt.Rows.Count == 0)
            {
                MessageBox.Show("Email ou mot de passe incorrect.", "Erreur", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            var row = dt.Rows[0];
            if (row["password"].ToString() != password)
            {
                MessageBox.Show("Email ou mot de passe incorrect.", "Erreur", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            // Success
            int userId = Convert.ToInt32(row["id"]);
            string role = row["role"].ToString();
            this.Hide();
            var mainForm = new MainForm(role, userId);
            mainForm.ShowDialog();
            this.Close();
        }

        private void btnRegister_Click(object sender, EventArgs e)
        {
            var registerForm = new RegisterForm();
            registerForm.ShowDialog();
        }
    }
} 