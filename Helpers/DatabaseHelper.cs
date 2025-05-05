using MySql.Data.MySqlClient;
using System.Data;

namespace PFAA.UI.Helpers
{
    public static class DatabaseHelper
    {
        private static readonly string connectionString =
            "server=localhost;user id=root;password=;database=kene_therapy;";

        public static MySqlConnection GetConnection()
        {
            var conn = new MySqlConnection(connectionString);
            conn.Open();
            return conn;
        }

        public static DataTable ExecuteQuery(string query, params MySqlParameter[] parameters)
        {
            using (var conn = GetConnection())
            using (var cmd = new MySqlCommand(query, conn))
            using (var adapter = new MySqlDataAdapter(cmd))
            {
                cmd.Parameters.AddRange(parameters);
                var dt = new DataTable();
                adapter.Fill(dt);
                return dt;
            }
        }

        public static int ExecuteNonQuery(string query, params MySqlParameter[] parameters)
        {
            using (var conn = GetConnection())
            using (var cmd = new MySqlCommand(query, conn))
            {
                cmd.Parameters.AddRange(parameters);
                return cmd.ExecuteNonQuery();
            }
        }

        public static object ExecuteScalar(string query, params MySqlParameter[] parameters)
        {
            using (var conn = GetConnection())
            using (var cmd = new MySqlCommand(query, conn))
            {
                cmd.Parameters.AddRange(parameters);
                return cmd.ExecuteScalar();
            }
        }

        public static MySqlTransaction BeginTransaction(out MySqlConnection conn)
        {
            conn = new MySqlConnection(connectionString);
            conn.Open();
            return conn.BeginTransaction();
        }
    }
}
