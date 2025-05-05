    using System;
    using System.Windows.Forms;
    using System.Collections.Generic;
    using System.Data;
    using PFAA.UI.Helpers;
    using BrightIdeasSoftware;
    using System.Windows.Forms.DataVisualization.Charting;

    namespace PFAA.UI.Forms
    {
        public partial class MainForm : Form
        {
            private string userRole;
            private int userId;
            private Panel mainPanel;
            private MenuStrip menuStrip;
            private ToolStripMenuItem dashboardMenu;
            private ToolStripMenuItem appointmentsMenu;
            private ToolStripMenuItem usersMenu;
            private ToolStripMenuItem locationsMenu;
            private ToolStripMenuItem settingsMenu;
            private ToolStripMenuItem profileMenu;
            private ToolStripMenuItem logoutMenu;
            private ToolStripMenuItem patientsMenu;
            private ToolStripMenuItem createPatientMenu;
            private ToolStripMenuItem myAppointmentsMenu;

            // --- Fields for calendar/locations ---
            private FlowLayoutPanel locationFlowPanel;
            private List<CheckBox> locationCheckBoxes = new List<CheckBox>();
            private TableLayoutPanel calendarTable;
            private DateTime currentWeek;
            private List<int> availableLocationIds = new List<int>();

            // --- Helper class for location items ---
            private class LocationItem
            {
                public int Id { get; set; }
                public string Name { get; set; }
                public override string ToString() => Name;
            }

            // --- Helper methods for calendar ---
            private DateTime GetStartOfWeek(DateTime date)
            {
                int diff = (7 + (date.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)date.DayOfWeek) - 1) % 7;
                return date.AddDays(-1 * diff).Date;
            }

            private void LoadLocations()
            {
                locationFlowPanel.Controls.Clear();
                locationCheckBoxes.Clear();
                availableLocationIds.Clear();
                DataTable dt;
                if (userRole == "admin")
                {
                    dt = DatabaseHelper.ExecuteQuery("SELECT id, name FROM locations WHERE status = 'active' ORDER BY name");
                }
                else // therapist
                {
                    dt = DatabaseHelper.ExecuteQuery(
                        "SELECT l.id, l.name FROM locations l JOIN therapist_locations tl ON l.id = tl.location_id WHERE tl.therapist_id = @tid AND l.status = 'active' ORDER BY l.name",
                        new[] { new MySql.Data.MySqlClient.MySqlParameter("@tid", userId) }
                    );
                }
                foreach (DataRow row in dt.Rows)
                {
                    int locId = Convert.ToInt32(row["id"]);
                    var cb = new CheckBox
                    {
                        Text = row["name"].ToString(),
                        Tag = locId,
                        Checked = true,
                        AutoSize = true,
                        Appearance = Appearance.Button,
                        FlatStyle = FlatStyle.Flat,
                        Margin = new Padding(4, 8, 4, 8),
                        Font = new System.Drawing.Font("Segoe UI", 10, System.Drawing.FontStyle.Bold)
                    };
                    cb.CheckedChanged += (s, e) => {
                        UpdateLocationButtonColors();
                        RefreshCalendar();
                    };
                    locationFlowPanel.Controls.Add(cb);
                    locationCheckBoxes.Add(cb);
                    availableLocationIds.Add(locId);
                }
                UpdateLocationButtonColors();
            }

            private void UpdateLocationButtonColors()
            {
                foreach (var cb in locationCheckBoxes)
                {
                    if (cb.Checked)
                    {
                        cb.BackColor = System.Drawing.Color.FromArgb(0, 120, 215); // Blue highlight
                        cb.ForeColor = System.Drawing.Color.White;
                        cb.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(0, 84, 153);
                    }
                    else
                    {
                        cb.BackColor = System.Drawing.Color.FromArgb(240, 240, 240); // Neutral gray
                        cb.ForeColor = System.Drawing.Color.Black;
                        cb.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(200, 200, 200);
                    }
                }
            }

            private void RefreshCalendar()
            {
                // Get selected location IDs
                var selectedLocationIds = new List<int>();
                foreach (var cb in locationCheckBoxes)
                    if (cb.Checked) selectedLocationIds.Add((int)cb.Tag);
                if (selectedLocationIds.Count == 0)
                {
                    for (int row = 1; row < 13; row++)
                        for (int col = 1; col < 8; col++)
                            (calendarTable.GetControlFromPosition(col, row) as Panel)?.Controls.Clear();
                    return;
                }

                // Clear all appointment panels from cells
                for (int row = 1; row < 13; row++)
                {
                    for (int col = 1; col < 8; col++)
                    {
                        var cell = calendarTable.GetControlFromPosition(col, row) as Panel;
                        if (cell != null)
                            cell.Controls.Clear();
                    }
                }

                // Build location filter for SQL
                string locationFilter = string.Join(",", selectedLocationIds);
                var dt = DatabaseHelper.ExecuteQuery(
                    $@"SELECT a.*, u.name as client_name, l.name as location_name 
                    FROM appointments a 
                    JOIN users u ON a.user_id = u.id 
                    JOIN locations l ON a.location_id = l.id 
                    WHERE a.date BETWEEN @startDate AND @endDate 
                    AND a.location_id IN ({locationFilter})
                    AND a.status != 'cancelled'
                    ORDER BY a.date, a.hour",
                    new[] {
                        new MySql.Data.MySqlClient.MySqlParameter("@startDate", currentWeek),
                        new MySql.Data.MySqlClient.MySqlParameter("@endDate", currentWeek.AddDays(6))
                    }
                );

                // Group appointments by (col, rowIdx)
                var cellAppointments = new Dictionary<(int col, int rowIdx), List<DataRow>>();
                foreach (DataRow row in dt.Rows)
                {
                    var date = Convert.ToDateTime(row["date"]);
                    var hour = TimeSpan.Parse(row["hour"].ToString());
                    var dayOfWeek = (int)date.DayOfWeek;
                    if (dayOfWeek == 0) dayOfWeek = 7; // Sunday is 0, make it 7
                    int col = dayOfWeek;
                    int rowIdx = hour.Hours - 7; // 8:00 is row 1
                    if (rowIdx < 1 || rowIdx > 12) continue;
                    var key = (col, rowIdx);
                    if (!cellAppointments.ContainsKey(key)) cellAppointments[key] = new List<DataRow>();
                    cellAppointments[key].Add(row);
                }

                // Render summary for all appointments in each cell
                for (int row = 1; row < 13; row++)
                {
                    for (int col = 1; col < 8; col++)
                    {
                        var cell = calendarTable.GetControlFromPosition(col, row) as Panel;
                        if (cell == null) continue;
                        cell.Controls.Clear();
                        cell.Click -= Cell_Click;
                        cell.Tag = null;
                        cell.Cursor = Cursors.Hand;
                    }
                }
                foreach (var kvp in cellAppointments)
                {
                    var cell = calendarTable.GetControlFromPosition(kvp.Key.col, kvp.Key.rowIdx) as Panel;
                    if (cell == null) continue;
                    var appts = kvp.Value;
                    int maxSummary = 2;
                    cell.Tag = appts;
                    for (int i = 0; i < appts.Count && i < maxSummary; i++)
                    {
                        var row = appts[i];
                        var status = row["status"].ToString();
                        var client = row["client_name"].ToString();
                        var color = GetStatusColor(status);
                        var miniPanel = new Panel
                        {
                            Height = 18,
                            Dock = DockStyle.Top,
                            BackColor = color,
                            Margin = new Padding(2, 2, 2, 2),
                            Cursor = Cursors.Hand
                        };
                        var miniLabel = new Label
                        {
                            Text = client,
                            Dock = DockStyle.Fill,
                            Font = new System.Drawing.Font("Segoe UI", 8, System.Drawing.FontStyle.Bold),
                            ForeColor = System.Drawing.Color.Black,
                            TextAlign = System.Drawing.ContentAlignment.MiddleLeft,
                            Padding = new Padding(4, 0, 0, 0),
                            Cursor = Cursors.Hand
                        };
                        miniPanel.Controls.Add(miniLabel);
                        cell.Controls.Add(miniPanel);
                        // Ensure all are clickable
                        miniPanel.Click += Cell_Click;
                        miniLabel.Click += Cell_Click;
                    }
                    if (appts.Count > maxSummary)
                    {
                        var moreLabel = new Label
                        {
                            Text = $"+{appts.Count - maxSummary} autres...",
                            Dock = DockStyle.Top,
                            Font = new System.Drawing.Font("Segoe UI", 8, System.Drawing.FontStyle.Italic),
                            ForeColor = System.Drawing.Color.Gray,
                            Height = 16,
                            TextAlign = System.Drawing.ContentAlignment.MiddleLeft,
                            Padding = new Padding(4, 0, 0, 0),
                            Cursor = Cursors.Hand
                        };
                        cell.Controls.Add(moreLabel);
                        moreLabel.Click += Cell_Click;
                    }
                }
                // Attach click event to all cells
                for (int row = 1; row < 13; row++)
                {
                    for (int col = 1; col < 8; col++)
                    {
                        var cell = calendarTable.GetControlFromPosition(col, row) as Panel;
                        if (cell == null) continue;
                        cell.Click -= Cell_Click;
                        cell.Click += Cell_Click;
                    }
                }
            }

            private void Cell_Click(object sender, EventArgs e)
            {
                // Always find the parent cell panel (the one in the TableLayoutPanel)
                Control ctrl = sender as Control;
                Panel cell = null;
                while (ctrl != null && !(ctrl is TableLayoutPanel))
                {
                    if (ctrl is Panel && ctrl.Parent is TableLayoutPanel)
                    {
                        cell = (Panel)ctrl;
                        break;
                    }
                    ctrl = ctrl.Parent;
                }
                var appts = cell?.Tag as List<DataRow>;
                var form = new Form
                {
                    Text = "Rendez-vous pour ce créneau",
                    Size = new System.Drawing.Size(420, 300),
                    StartPosition = FormStartPosition.CenterParent,
                    FormBorderStyle = FormBorderStyle.FixedDialog,
                    MaximizeBox = false,
                    MinimizeBox = false
                };
                var panel = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
                if (appts == null || appts.Count == 0)
                {
                    var noneLabel = new Label
                    {
                        Text = "Aucun rendez-vous pour ce créneau.",
                        Dock = DockStyle.Fill,
                        Font = new System.Drawing.Font("Segoe UI", 12, System.Drawing.FontStyle.Italic),
                        ForeColor = System.Drawing.Color.Gray,
                        TextAlign = System.Drawing.ContentAlignment.MiddleCenter
                    };
                    panel.Controls.Add(noneLabel);
                }
                else
                {
                    int y = 10;
                    foreach (var row in appts)
                    {
                        var status = row["status"].ToString();
                        var color = GetStatusColor(status);
                        var apptPanel = new Panel
                        {
                            Location = new System.Drawing.Point(10, y),
                            Size = new System.Drawing.Size(370, 50),
                            BackColor = color,
                            BorderStyle = BorderStyle.FixedSingle
                        };
                        var clientLabel = new Label
                        {
                            Text = $"{row["client_name"]}",
                            Location = new System.Drawing.Point(10, 5),
                            Size = new System.Drawing.Size(180, 20),
                            Font = new System.Drawing.Font("Segoe UI", 10, System.Drawing.FontStyle.Bold)
                        };
                        var locLabel = new Label
                        {
                            Text = $"Cabinet: {row["location_name"]}",
                            Location = new System.Drawing.Point(10, 25),
                            Size = new System.Drawing.Size(180, 18),
                            Font = new System.Drawing.Font("Segoe UI", 8)
                        };
                        var timeLabel = new Label
                        {
                            Text = $"Heure: {TimeSpan.Parse(row["hour"].ToString()):hh\\:mm}",
                            Location = new System.Drawing.Point(200, 5),
                            Size = new System.Drawing.Size(80, 20),
                            Font = new System.Drawing.Font("Segoe UI", 9)
                        };
                        var statusLabel = new Label
                        {
                            Text = GetStatusText(status),
                            Location = new System.Drawing.Point(200, 25),
                            Size = new System.Drawing.Size(80, 18),
                            Font = new System.Drawing.Font("Segoe UI", 8),
                            ForeColor = GetStatusTextColor(status)
                        };
                        var detailsBtn = new Button
                        {
                            Text = "Détails",
                            Location = new System.Drawing.Point(300, 12),
                            Size = new System.Drawing.Size(70, 28),
                            BackColor = System.Drawing.Color.FromArgb(0, 120, 215),
                            ForeColor = System.Drawing.Color.White,
                            FlatStyle = FlatStyle.Flat,
                            Tag = row["id"].ToString()
                        };
                        detailsBtn.FlatAppearance.BorderSize = 0;
                        detailsBtn.Click += (s2, e2) => ShowAppointmentDetails(row["id"].ToString());
                        apptPanel.Controls.Add(clientLabel);
                        apptPanel.Controls.Add(locLabel);
                        apptPanel.Controls.Add(timeLabel);
                        apptPanel.Controls.Add(statusLabel);
                        apptPanel.Controls.Add(detailsBtn);
                        panel.Controls.Add(apptPanel);
                        y += 60;
                    }
                }
                form.Controls.Add(panel);
                form.ShowDialog();
            }

            private System.Drawing.Color GetStatusColor(string status)
            {
                switch (status.ToLower())
                {
                    case "confirmed":
                        return System.Drawing.Color.FromArgb(198, 239, 206);
                    case "pending":
                        return System.Drawing.Color.FromArgb(255, 235, 156);
                    case "completed":
                        return System.Drawing.Color.FromArgb(200, 200, 255);
                    case "cancelled":
                        return System.Drawing.Color.FromArgb(255, 199, 206);
                    default:
                        return System.Drawing.Color.White;
                }
            }
            private string GetStatusText(string status)
            {
                switch (status.ToLower())
                {
                    case "confirmed":
                        return "Confirmé";
                    case "pending":
                        return "En attente";
                    case "completed":
                        return "Terminé";
                    case "cancelled":
                        return "Annulé";
                    default:
                        return status;
                }
            }
            private System.Drawing.Color GetStatusTextColor(string status)
            {
                switch (status.ToLower())
                {
                    case "confirmed":
                        return System.Drawing.Color.FromArgb(0, 97, 0);
                    case "pending":
                        return System.Drawing.Color.FromArgb(156, 87, 0);
                    case "completed":
                        return System.Drawing.Color.FromArgb(0, 0, 120);
                    case "cancelled":
                        return System.Drawing.Color.FromArgb(156, 0, 6);
                    default:
                        return System.Drawing.Color.Black;
                }
            }

            // --- END calendar/locations helpers ---

            public MainForm(string role, int id)
            {
                userRole = role;
                userId = id;
                InitializeComponent();
                InitializeNavigation();
            }

            private void InitializeNavigation()
            {
                menuStrip = new MenuStrip();
                dashboardMenu = new ToolStripMenuItem("Tableau de bord");
                appointmentsMenu = new ToolStripMenuItem("Rendez-vous");
                usersMenu = new ToolStripMenuItem("Utilisateurs");
                locationsMenu = new ToolStripMenuItem("Cabinets");
                settingsMenu = new ToolStripMenuItem("Paramètres");
                profileMenu = new ToolStripMenuItem("Profil");
                logoutMenu = new ToolStripMenuItem("Déconnexion");
                patientsMenu = new ToolStripMenuItem("Patients");
                createPatientMenu = new ToolStripMenuItem("Créer patient");
                myAppointmentsMenu = new ToolStripMenuItem("Mes rendez-vous");

                menuStrip.Items.Add(dashboardMenu);
                if (userRole == "client")
                {
                    menuStrip.Items.Add(myAppointmentsMenu);
                }
                menuStrip.Items.Add(appointmentsMenu);
                if (userRole == "admin")
                {
                    menuStrip.Items.Add(usersMenu);
                    menuStrip.Items.Add(locationsMenu);
                    menuStrip.Items.Add(settingsMenu);
                }
                else if (userRole == "therapist")
                {
                    menuStrip.Items.Add(patientsMenu);
                }
                menuStrip.Items.Add(createPatientMenu);
                menuStrip.Items.Add(profileMenu);
                menuStrip.Items.Add(logoutMenu);

                dashboardMenu.Click += (s, e) => ShowDashboard();
                myAppointmentsMenu.Click += (s, e) => ShowClientAppointments();
                appointmentsMenu.Click += (s, e) => ShowAppointments();
                usersMenu.Click += (s, e) => ShowUsers();
                locationsMenu.Click += (s, e) => ShowLocations();
                settingsMenu.Click += (s, e) => ShowSettings();
                profileMenu.Click += (s, e) => ShowProfile();
                logoutMenu.Click += (s, e) => Logout();
                patientsMenu.Click += (s, e) => ShowPatients();
                createPatientMenu.Click += (s, e) => ShowCreatePatientTab();

                this.Controls.Add(menuStrip);
                mainPanel = new Panel { Dock = DockStyle.Fill, Top = menuStrip.Height };
                this.Controls.Add(mainPanel);
                this.MainMenuStrip = menuStrip;
                this.Text = "Tableau de bord";
                this.WindowState = FormWindowState.Maximized;
                ShowDashboard();

                // Add new appointment tab for therapists
                if (userRole == "therapist")
                {
                    var bookForMenu = new ToolStripMenuItem("Nouveau rendez-vous");
                    menuStrip.Items.Insert(2, bookForMenu); // After appointments
                    bookForMenu.Click += (s, e) => ShowBookFor();
                }
            }

            private void ShowDashboard()
            {
                mainPanel.Controls.Clear();
                if (userRole == "admin")
                {
                    ShowAdminDashboard();
                }
                else if (userRole == "therapist")
                {
                    var lbl = new Label { Text = "Bienvenue sur le tableau de bord!", Dock = DockStyle.Fill, Font = new System.Drawing.Font("Segoe UI", 18), TextAlign = System.Drawing.ContentAlignment.MiddleCenter };
                    mainPanel.Controls.Add(lbl);
                }
                else
                {
                    var lbl = new Label { Text = "Bienvenue sur le tableau de bord!", Dock = DockStyle.Fill, Font = new System.Drawing.Font("Segoe UI", 18), TextAlign = System.Drawing.ContentAlignment.MiddleCenter };
                    mainPanel.Controls.Add(lbl);
                }
            }

            private void ShowClientAppointments()
            {
                mainPanel.Controls.Clear();
                var titleLabel = new Label
                {
                    Text = "Mes rendez-vous",
                    Font = new System.Drawing.Font("Segoe UI", 16, System.Drawing.FontStyle.Bold),
                    Dock = DockStyle.Top,
                    Height = 50,
                    TextAlign = System.Drawing.ContentAlignment.MiddleLeft,
                    Padding = new Padding(10, 0, 0, 0)
                };
                mainPanel.Controls.Add(titleLabel);
                var apptTable = new DataGridView
                {
                    Dock = DockStyle.Fill,
                    AllowUserToAddRows = false,
                    AllowUserToDeleteRows = false,
                    AllowUserToResizeRows = false,
                    AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                    SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                    MultiSelect = false,
                    ReadOnly = true
                };
                apptTable.Columns.Add("date", "Date");
                apptTable.Columns.Add("heure", "Heure");
                apptTable.Columns.Add("therapist", "Thérapeute");
                apptTable.Columns.Add("location", "Cabinet");
                apptTable.Columns.Add("status", "Statut");
                apptTable.Columns.Add("rapport", "Rapport");
                // Load recent appointments
                var dt = DatabaseHelper.ExecuteQuery(
                    "SELECT a.id, a.date, a.hour, l.name as location, u2.name as therapist, a.status, r.content as rapport, u2.id as therapist_id " +
                    "FROM appointments a " +
                    "JOIN locations l ON a.location_id = l.id " +
                    "LEFT JOIN reports r ON r.appointment_id = a.id " +
                    "LEFT JOIN users u2 ON r.therapist_id = u2.id " +
                    "WHERE a.user_id = @uid ORDER BY a.date DESC, a.hour DESC LIMIT 30",
                    new[] { new MySql.Data.MySqlClient.MySqlParameter("@uid", userId) }
                );
                foreach (DataRow row in dt.Rows)
                {
                    apptTable.Rows.Add(
                        Convert.ToDateTime(row["date"]).ToString("dd/MM/yyyy"),
                        TimeSpan.Parse(row["hour"].ToString()).ToString(@"hh\:mm"),
                        row["therapist"].ToString(),
                        row["location"].ToString(),
                        row["status"].ToString(),
                        string.IsNullOrEmpty(row["rapport"].ToString()) ? "" : "Voir rapport",
                        row["therapist_id"].ToString()
                    );
                }
                apptTable.CellClick += (s, e) =>
                {
                    if (e.ColumnIndex == apptTable.Columns["rapport"].Index && e.RowIndex >= 0)
                    {
                        var apptId = dt.Rows[e.RowIndex]["id"].ToString();
                        var rapport = dt.Rows[e.RowIndex]["rapport"].ToString();
                        if (!string.IsNullOrEmpty(rapport))
                        {
                            var rapportForm = new Form
                            {
                                Text = "Rapport du rendez-vous",
                                Size = new System.Drawing.Size(500, 400),
                                StartPosition = FormStartPosition.CenterParent,
                                FormBorderStyle = FormBorderStyle.FixedDialog,
                                MaximizeBox = false,
                                MinimizeBox = false
                            };
                            var rapportBox = new TextBox
                            {
                                Multiline = true,
                                ReadOnly = true,
                                Dock = DockStyle.Fill,
                                Text = rapport,
                                ScrollBars = ScrollBars.Vertical
                            };
                            rapportForm.Controls.Add(rapportBox);
                            rapportForm.ShowDialog();
                        }
                    }
                };
                mainPanel.Controls.Add(apptTable);
            }

            private void ShowAppointments()
            {
                mainPanel.Controls.Clear();

                // Title
                var titleLabel = new Label
                {
                    Text = "Calendrier hebdomadaire",
                    Font = new System.Drawing.Font("Segoe UI", 16, System.Drawing.FontStyle.Bold),
                    Dock = DockStyle.Top,
                    Height = 60,
                    TextAlign = System.Drawing.ContentAlignment.MiddleLeft,
                    Padding = new Padding(10, 20, 0, 0)
                };
                mainPanel.Controls.Add(titleLabel);

                // Location selection panel (side-by-side)
                var locationPanel = new Panel
                {
                    Dock = DockStyle.Top,
                    Height = 70, // Increased height
                    Padding = new Padding(0, 10, 0, 10), // Add top padding
                    Margin = new Padding(0, 0, 0, 20) // Add bottom margin for more space below
                };
                var locationLabel = new Label
                {
                    Text = "Cabinets :",
                    Location = new System.Drawing.Point(10, 20), // Move label lower
                    Width = 80,
                    Font = new System.Drawing.Font("Segoe UI", 10, System.Drawing.FontStyle.Bold)
                };
                locationFlowPanel = new FlowLayoutPanel
                {
                    Location = new System.Drawing.Point(100, 18), // Move buttons lower
                    Width = 800,
                    Height = 40,
                    FlowDirection = FlowDirection.LeftToRight,
                    AutoSize = true,
                    AutoSizeMode = AutoSizeMode.GrowAndShrink,
                    WrapContents = false
                };
                locationPanel.Controls.Add(locationLabel);
                locationPanel.Controls.Add(locationFlowPanel);
                mainPanel.Controls.Add(locationPanel);

                // Main container
                var container = new Panel
                {
                    Dock = DockStyle.Fill,
                    Padding = new Padding(20, 0, 20, 20), // Reduce top padding
                    AutoScroll = true,
                    BackColor = System.Drawing.Color.White
                };

                // Optional: Add a separator line between locationPanel and container
                var separator = new Panel
                {
                    Dock = DockStyle.Top,
                    Height = 2,
                    BackColor = System.Drawing.Color.LightGray,
                    Margin = new Padding(0, 0, 0, 10)
                };
                mainPanel.Controls.Add(separator);

                // Navigation panel (move inside container, above calendar)
                var navPanel = new Panel
                {
                    Height = 50,
                    Padding = new Padding(0, 0, 0, 5), // Reduce bottom padding
                    Margin = new Padding(0, 0, 0, 0)
                };
                var prevWeekBtn = new Button
                {
                    Text = "◀ Semaine précédente",
                    Width = 150,
                    Height = 35,
                    Location = new System.Drawing.Point(10, 10),
                    FlatStyle = FlatStyle.Flat
                };
                var nextWeekBtn = new Button
                {
                    Text = "Semaine suivante ▶",
                    Width = 150,
                    Height = 35,
                    Location = new System.Drawing.Point(170, 10),
                    FlatStyle = FlatStyle.Flat
                };
                var thisWeekBtn = new Button
                {
                    Text = "Cette semaine",
                    Width = 120,
                    Height = 35,
                    Location = new System.Drawing.Point(330, 10),
                    FlatStyle = FlatStyle.Flat
                };
                navPanel.Controls.AddRange(new Control[] { prevWeekBtn, nextWeekBtn, thisWeekBtn });
                container.Controls.Add(navPanel);

                // Add larger padding just above the calendar table
                var calendarTopPadding = new Panel
                {
                    Dock = DockStyle.Top,
                    Height = 40,
                    BackColor = System.Drawing.Color.White,
                };
                container.Controls.Add(calendarTopPadding);

                // TableLayoutPanel for calendar
                calendarTable = new TableLayoutPanel
                {
                    Dock = DockStyle.Top,
                    ColumnCount = 8, // Time + 7 days
                    RowCount = 13,   // 8:00 to 20:00 (12 slots + header)
                    CellBorderStyle = TableLayoutPanelCellBorderStyle.Single,
                    BackColor = System.Drawing.Color.White,
                    AutoSize = true, // Set to false to control height
                    AutoSizeMode = AutoSizeMode.GrowAndShrink,
                    Height = 560, // Ensure all rows are visible
                    Margin = new Padding(0, 0, 0, 0)
                };

                // Set column widths
                calendarTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80)); // Time column
                for (int i = 0; i < 7; i++)
                {
                    calendarTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F / 7));
                }

                // Set row heights
                calendarTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 40)); // Header row
                for (int i = 1; i < 13; i++)
                {
                    calendarTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 40)); // Reduce from 60 to 40
                }

                // Add day headers
                string[] dayNames = { "Lundi", "Mardi", "Mercredi", "Jeudi", "Vendredi", "Samedi", "Dimanche" };
                var headerFont = new System.Drawing.Font("Segoe UI", 10, System.Drawing.FontStyle.Bold);
                var headerBack = System.Drawing.Color.FromArgb(240, 240, 240);
                var headerFore = System.Drawing.Color.FromArgb(27, 53, 60);

                // Empty top-left cell
                var emptyHeader = new Label
                {
                    Text = "",
                    Dock = DockStyle.Fill,
                    BackColor = headerBack,
                    ForeColor = headerFore
                };
                calendarTable.Controls.Add(emptyHeader, 0, 0);

                for (int i = 0; i < 7; i++)
                {
                    var dayHeader = new Label
                    {
                        Text = dayNames[i],
                        Dock = DockStyle.Fill,
                        TextAlign = System.Drawing.ContentAlignment.MiddleCenter,
                        Font = headerFont,
                        BackColor = headerBack,
                        ForeColor = headerFore
                    };
                    calendarTable.Controls.Add(dayHeader, i + 1, 0);
                }

                // Add time slots and empty cells
                var timeFont = new System.Drawing.Font("Segoe UI", 9, System.Drawing.FontStyle.Regular);
                var timeBack = System.Drawing.Color.FromArgb(248, 249, 250);
                for (int row = 1, hour = 8; row < 13; row++, hour++)
                {
                    // Time label
                    var timeLabel = new Label
                    {
                        Text = $"{hour:00}:00",
                        Dock = DockStyle.Fill,
                        TextAlign = System.Drawing.ContentAlignment.MiddleCenter,
                        Font = timeFont,
                        BackColor = timeBack,
                        ForeColor = System.Drawing.Color.FromArgb(73, 80, 87)
                    };
                    calendarTable.Controls.Add(timeLabel, 0, row);

                    // Empty cells for each day (will be filled with appointments)
                    for (int col = 1; col < 8; col++)
                    {
                        var cellPanel = new Panel
                        {
                            Dock = DockStyle.Fill,
                            BackColor = (row % 2 == 0) ? System.Drawing.Color.White : System.Drawing.Color.FromArgb(245, 247, 250),
                            Name = $"cell_{col}_{row}"
                        };
                        calendarTable.Controls.Add(cellPanel, col, row);
                    }
                }

                container.Controls.Add(calendarTable);
                mainPanel.Controls.Add(container);

                // Week navigation logic
                currentWeek = GetStartOfWeek(DateTime.Today);

                // Navigation button events
                prevWeekBtn.Click += (s, e) => { currentWeek = currentWeek.AddDays(-7); RefreshCalendar(); };
                nextWeekBtn.Click += (s, e) => { currentWeek = currentWeek.AddDays(7); RefreshCalendar(); };
                thisWeekBtn.Click += (s, e) => { currentWeek = GetStartOfWeek(DateTime.Today); RefreshCalendar(); };

                // Initial load
                LoadLocations();
                RefreshCalendar();
            }

            private void ShowAppointmentDetails(string appointmentId)
            {
                var form = new Form
                {
                    Text = "Détails du rendez-vous",
                    Size = new System.Drawing.Size(500, 500),
                    StartPosition = FormStartPosition.CenterParent,
                    FormBorderStyle = FormBorderStyle.FixedDialog,
                    MaximizeBox = false,
                    MinimizeBox = false
                };

                var dt = DatabaseHelper.ExecuteQuery(
                    @"SELECT a.*, u.name as client_name, u.phone as client_phone, l.name as location_name, r.content as rapport 
                    FROM appointments a 
                    JOIN users u ON a.user_id = u.id 
                    JOIN locations l ON a.location_id = l.id 
                    LEFT JOIN reports r ON r.appointment_id = a.id 
                    WHERE a.id = @id",
                    new[] { new MySql.Data.MySqlClient.MySqlParameter("@id", appointmentId) }
                );

                if (dt.Rows.Count > 0)
                {
                    var appt = dt.Rows[0];
                    var status = appt["status"].ToString();
                    var clientId = appt["user_id"].ToString();
                    var rapport = appt["rapport"].ToString();

                    var statusLabel = new Label
                    {
                        Text = $"Statut: {GetStatusText(status)}",
                        Location = new System.Drawing.Point(20, 20),
                        AutoSize = true,
                        Font = new System.Drawing.Font("Segoe UI", 10, System.Drawing.FontStyle.Bold),
                        ForeColor = GetStatusTextColor(status)
                    };

                    var clientLabel = new Label
                    {
                        Text = $"Client: {appt["client_name"]}",
                        Location = new System.Drawing.Point(20, 50),
                        AutoSize = true
                    };

                    var phoneLabel = new Label
                    {
                        Text = $"Téléphone: {appt["client_phone"]}",
                        Location = new System.Drawing.Point(20, 75),
                        AutoSize = true
                    };

                    var locationLabel = new Label
                    {
                        Text = $"Cabinet: {appt["location_name"]}",
                        Location = new System.Drawing.Point(20, 100),
                        AutoSize = true
                    };

                    var dateLabel = new Label
                    {
                        Text = $"Date: {Convert.ToDateTime(appt["date"]).ToString("dd/MM/yyyy")}",
                        Location = new System.Drawing.Point(20, 125),
                        AutoSize = true
                    };

                    var timeLabel = new Label
                    {
                        Text = $"Heure: {TimeSpan.Parse(appt["hour"].ToString()).ToString(@"hh\:mm")}",
                        Location = new System.Drawing.Point(20, 150),
                        AutoSize = true
                    };

                    var rapportLabel = new Label
                    {
                        Text = "Rapport:",
                        Location = new System.Drawing.Point(20, 180),
                        AutoSize = true,
                        Font = new System.Drawing.Font("Segoe UI", 9, System.Drawing.FontStyle.Bold)
                    };
                    var rapportBox = new TextBox
                    {
                        Multiline = true,
                        ReadOnly = true,
                        Location = new System.Drawing.Point(20, 200),
                        Size = new System.Drawing.Size(440, 60),
                        Text = string.IsNullOrEmpty(rapport) ? "Aucun rapport." : rapport,
                        ScrollBars = ScrollBars.Vertical
                    };

                    // Previous appointments
                    var prevLabel = new Label
                    {
                        Text = "Rendez-vous précédents:",
                        Location = new System.Drawing.Point(20, 270),
                        AutoSize = true,
                        Font = new System.Drawing.Font("Segoe UI", 9, System.Drawing.FontStyle.Bold)
                    };
                    var prevTable = new DataGridView
                    {
                        Location = new System.Drawing.Point(20, 290),
                        Size = new System.Drawing.Size(440, 120),
                        AllowUserToAddRows = false,
                        AllowUserToDeleteRows = false,
                        AllowUserToResizeRows = false,
                        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                        SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                        MultiSelect = false,
                        ReadOnly = true,
                        RowHeadersVisible = false
                    };
                    prevTable.Columns.Add("date", "Date");
                    prevTable.Columns.Add("heure", "Heure");
                    prevTable.Columns.Add("location", "Cabinet");
                    prevTable.Columns.Add("status", "Statut");

                    var prevAppts = DatabaseHelper.ExecuteQuery(
                        "SELECT a.date, a.hour, l.name as location, a.status FROM appointments a JOIN locations l ON a.location_id = l.id WHERE a.user_id = @uid AND a.id != @aid ORDER BY a.date DESC, a.hour DESC LIMIT 10",
                        new[] {
                            new MySql.Data.MySqlClient.MySqlParameter("@uid", clientId),
                            new MySql.Data.MySqlClient.MySqlParameter("@aid", appointmentId)
                        }
                    );
                    foreach (DataRow prow in prevAppts.Rows)
                    {
                        prevTable.Rows.Add(
                            Convert.ToDateTime(prow["date"]).ToString("dd/MM/yyyy"),
                            TimeSpan.Parse(prow["hour"].ToString()).ToString(@"hh\:mm"),
                            prow["location"].ToString(),
                            GetStatusText(prow["status"].ToString())
                        );
                    }

                    var closeButton = new Button
                    {
                        Text = "Fermer",
                        Location = new System.Drawing.Point(20, 420),
                        Width = 100
                    };
                    closeButton.Click += (s, e) => form.Close();

                    form.Controls.AddRange(new Control[] {
                        statusLabel, clientLabel, phoneLabel, locationLabel, dateLabel, timeLabel,
                        rapportLabel, rapportBox, prevLabel, prevTable, closeButton
                    });
                }

                form.ShowDialog();
            }

            private void BookAppointment(DateTime day, int hour)
            {
                var form = new Form
                {
                    Text = "Créer un rendez-vous",
                    Size = new System.Drawing.Size(400, 300),
                    StartPosition = FormStartPosition.CenterParent,
                    FormBorderStyle = FormBorderStyle.FixedDialog,
                    MaximizeBox = false,
                    MinimizeBox = false
                };

                var dateLabel = new Label
                {
                    Text = $"Date: {day.ToString("dd/MM/yyyy")}",
                    Location = new System.Drawing.Point(20, 20),
                    AutoSize = true
                };

                var timeLabel = new Label
                {
                    Text = $"Heure: {hour:00}:00",
                    Location = new System.Drawing.Point(20, 50),
                    AutoSize = true
                };

                var clientLabel = new Label { Text = "Client:", Location = new System.Drawing.Point(20, 80) };
                var clientCombo = new ComboBox
                {
                    Location = new System.Drawing.Point(20, 100),
                    Width = 340,
                    DropDownStyle = ComboBoxStyle.DropDownList
                };

                var locationLabel = new Label { Text = "Cabinet:", Location = new System.Drawing.Point(20, 130) };
                var locationCombo = new ComboBox
                {
                    Location = new System.Drawing.Point(20, 150),
                    Width = 340,
                    DropDownStyle = ComboBoxStyle.DropDownList
                };

                // Load clients
                var clients = DatabaseHelper.ExecuteQuery("SELECT id, name FROM users WHERE role = 'patient' AND status = 'active' ORDER BY name");
                foreach (DataRow row in clients.Rows)
                    clientCombo.Items.Add(new { Text = row["name"].ToString(), Value = row["id"] });
                clientCombo.DisplayMember = "Text";
                clientCombo.ValueMember = "Value";

                // Load locations
                var locations = DatabaseHelper.ExecuteQuery(
                    userRole == "admin"
                        ? "SELECT id, name FROM locations WHERE status = 'active' ORDER BY name"
                        : @"SELECT l.id, l.name FROM locations l 
                        JOIN therapist_locations tl ON l.id = tl.location_id 
                        WHERE tl.therapist_id = @tid AND l.status = 'active' 
                        ORDER BY l.name",
                    userRole == "admin" ? new MySql.Data.MySqlClient.MySqlParameter[0] :
                        new[] { new MySql.Data.MySqlClient.MySqlParameter("@tid", userId) }
                );
                foreach (DataRow row in locations.Rows)
                    locationCombo.Items.Add(new { Text = row["name"].ToString(), Value = row["id"] });
                locationCombo.DisplayMember = "Text";
                locationCombo.ValueMember = "Value";

                var saveButton = new Button
                {
                    Text = "Enregistrer",
                    Location = new System.Drawing.Point(20, 190),
                    Width = 150
                };

                saveButton.Click += (s, e) =>
                {
                    if (clientCombo.SelectedItem == null || locationCombo.SelectedItem == null)
                    {
                        MessageBox.Show("Veuillez sélectionner un client et un cabinet.", "Erreur", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    try
                    {
                        DatabaseHelper.ExecuteNonQuery(
                            @"INSERT INTO appointments (user_id, location_id, date, hour, status) 
                            VALUES (@user_id, @location_id, @date, @hour, 'pending')",
                            new[] {
                                new MySql.Data.MySqlClient.MySqlParameter("@user_id", ((dynamic)clientCombo.SelectedItem).Value),
                                new MySql.Data.MySqlClient.MySqlParameter("@location_id", ((dynamic)locationCombo.SelectedItem).Value),
                                new MySql.Data.MySqlClient.MySqlParameter("@date", day.Date),
                                new MySql.Data.MySqlClient.MySqlParameter("@hour", new TimeSpan(hour, 0, 0))
                            }
                        );
                        MessageBox.Show("Rendez-vous créé avec succès.", "Succès", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        form.Close();
                        // Refresh the appointments list if needed
                        ShowAppointments();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Erreur lors de la création du rendez-vous: {ex.Message}", "Erreur", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                };

                form.Controls.AddRange(new Control[] {
                    dateLabel, timeLabel,
                    clientLabel, clientCombo,
                    locationLabel, locationCombo,
                    saveButton
                });

                form.ShowDialog();
            }

            private void ShowUsers()
            {
                mainPanel.Controls.Clear();
                
                // Only admin can access users management
                if (userRole != "admin")
                {
                    var lbl = new Label { Text = "Accès non autorisé", Dock = DockStyle.Fill, Font = new System.Drawing.Font("Segoe UI", 18), TextAlign = System.Drawing.ContentAlignment.MiddleCenter };
                    mainPanel.Controls.Add(lbl);
                    return;
                }

                // Title
                var titleLabel = new Label
                {
                    Text = "Gestion des utilisateurs",
                    Font = new System.Drawing.Font("Segoe UI", 16, System.Drawing.FontStyle.Bold),
                    Dock = DockStyle.Top,
                    Height = 50,
                    TextAlign = System.Drawing.ContentAlignment.MiddleLeft,
                    Padding = new Padding(10, 0, 0, 0)
                };
                mainPanel.Controls.Add(titleLabel);

                // Add User Button
                var btnAddUser = new Button
                {
                    Text = "Ajouter un utilisateur",
                    Dock = DockStyle.Top,
                    Height = 40,
                    Margin = new Padding(10, 0, 10, 10)
                };
                btnAddUser.Click += (s, e) => ShowAddUserForm();
                mainPanel.Controls.Add(btnAddUser);

                // Search Panel
                var searchPanel = new Panel { Dock = DockStyle.Top, Height = 60 };
                var searchBox = new TextBox { Location = new System.Drawing.Point(10, 20), Width = 300 };
                var searchButton = new Button { Text = "Rechercher", Location = new System.Drawing.Point(320, 20), Width = 100 };
                var resetButton = new Button { Text = "Réinitialiser", Location = new System.Drawing.Point(430, 20), Width = 100 };
                searchPanel.Controls.AddRange(new Control[] { searchBox, searchButton, resetButton });
                mainPanel.Controls.Add(searchPanel);

                // Users Table
                var usersTable = new DataGridView
                {
                    Dock = DockStyle.Fill,
                    AllowUserToAddRows = false,
                    AllowUserToDeleteRows = false,
                    AllowUserToResizeRows = false,
                    AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                    SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                    MultiSelect = false,
                    ReadOnly = true
                };

                // Add columns
                usersTable.Columns.Add("name", "Nom");
                usersTable.Columns.Add("email", "Email");
                usersTable.Columns.Add("role", "Rôle");
                usersTable.Columns.Add("status", "Statut");
                usersTable.Columns.Add("actions", "Actions");

                // Load users
                LoadUsers(usersTable);

                // Add table to main panel
                mainPanel.Controls.Add(usersTable);

                // Event handlers
                searchButton.Click += (s, e) => SearchUsers(usersTable, searchBox.Text);
                resetButton.Click += (s, e) => { searchBox.Clear(); LoadUsers(usersTable); };
                usersTable.CellClick += (s, e) =>
                {
                    if (e.ColumnIndex == usersTable.Columns["actions"].Index && e.RowIndex >= 0)
                    {
                        var userId = Convert.ToInt32(usersTable.Rows[e.RowIndex].Tag);
                        var userRole = usersTable.Rows[e.RowIndex].Cells["role"].Value.ToString();
                        ShowUserEditForm(userId, userRole);
                    }
                };
            }

            private void ShowAddUserForm()
            {
                var form = new Form
                {
                    Text = "Ajouter un utilisateur",
                    Size = new System.Drawing.Size(400, 500),
                    StartPosition = FormStartPosition.CenterParent,
                    FormBorderStyle = FormBorderStyle.FixedDialog,
                    MaximizeBox = false,
                    MinimizeBox = false
                };

                // Create form controls
                var nameLabel = new Label { Text = "Nom complet:", Location = new System.Drawing.Point(20, 20) };
                var nameBox = new TextBox { Location = new System.Drawing.Point(20, 40), Width = 340 };

                var emailLabel = new Label { Text = "Email:", Location = new System.Drawing.Point(20, 80) };
                var emailBox = new TextBox { Location = new System.Drawing.Point(20, 100), Width = 340 };

                var phoneLabel = new Label { Text = "Téléphone:", Location = new System.Drawing.Point(20, 140) };
                var phoneBox = new TextBox { Location = new System.Drawing.Point(20, 160), Width = 340 };

                var addressLabel = new Label { Text = "Adresse:", Location = new System.Drawing.Point(20, 200) };
                var addressBox = new TextBox { Location = new System.Drawing.Point(20, 220), Width = 340, Height = 60, Multiline = true };

                var passwordLabel = new Label { Text = "Mot de passe:", Location = new System.Drawing.Point(20, 300) };
                var passwordBox = new TextBox { Location = new System.Drawing.Point(20, 320), Width = 340, UseSystemPasswordChar = true };

                var saveButton = new Button
                {
                    Text = "Enregistrer",
                    Location = new System.Drawing.Point(20, 380),
                    Width = 150
                };

                var cancelButton = new Button
                {
                    Text = "Annuler",
                    Location = new System.Drawing.Point(210, 380),
                    Width = 150
                };

                // Add controls to form
                form.Controls.AddRange(new Control[] {
                    nameLabel, nameBox,
                    emailLabel, emailBox,
                    phoneLabel, phoneBox,
                    addressLabel, addressBox,
                    passwordLabel, passwordBox,
                    saveButton, cancelButton
                });

                // Event handlers
                saveButton.Click += (s, e) =>
                {
                    if (string.IsNullOrWhiteSpace(nameBox.Text) || string.IsNullOrWhiteSpace(emailBox.Text) || string.IsNullOrWhiteSpace(passwordBox.Text))
                    {
                        MessageBox.Show("Le nom, l'email et le mot de passe sont obligatoires.", "Erreur", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    try
                    {
                        DatabaseHelper.ExecuteNonQuery(
                            "INSERT INTO users (name, email, phone, address, password, role, status) VALUES (@name, @email, @phone, @address, @password, 'client', 'active')",
                            new[] {
                                new MySql.Data.MySqlClient.MySqlParameter("@name", nameBox.Text),
                                new MySql.Data.MySqlClient.MySqlParameter("@email", emailBox.Text),
                                new MySql.Data.MySqlClient.MySqlParameter("@phone", phoneBox.Text),
                                new MySql.Data.MySqlClient.MySqlParameter("@address", addressBox.Text),
                                new MySql.Data.MySqlClient.MySqlParameter("@password", passwordBox.Text)
                            }
                        );
                        MessageBox.Show("Utilisateur créé avec succès.", "Succès", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        form.Close();
                        ShowUsers(); // Refresh the users list
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Erreur lors de la création: {ex.Message}", "Erreur", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                };

                cancelButton.Click += (s, e) => form.Close();

                form.ShowDialog();
            }

            private void StyleModernTable(DataGridView dgv)
            {
                dgv.BackgroundColor = System.Drawing.Color.WhiteSmoke;
                dgv.BorderStyle = BorderStyle.None;
                dgv.EnableHeadersVisualStyles = false;
                dgv.ColumnHeadersDefaultCellStyle.BackColor = System.Drawing.Color.FromArgb(27, 53, 60);
                dgv.ColumnHeadersDefaultCellStyle.ForeColor = System.Drawing.Color.White;
                dgv.ColumnHeadersDefaultCellStyle.Font = new System.Drawing.Font("Segoe UI", 11, System.Drawing.FontStyle.Bold);
                dgv.DefaultCellStyle.Font = new System.Drawing.Font("Segoe UI", 10);
                dgv.DefaultCellStyle.SelectionBackColor = System.Drawing.Color.FromArgb(220, 240, 255);
                dgv.DefaultCellStyle.SelectionForeColor = System.Drawing.Color.Black;
                dgv.RowTemplate.Height = 32;
                dgv.AlternatingRowsDefaultCellStyle.BackColor = System.Drawing.Color.FromArgb(240, 245, 248);
                dgv.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
                dgv.GridColor = System.Drawing.Color.LightGray;
            }

            private void LoadUsers(DataGridView usersTable)
            {
                usersTable.Rows.Clear();
                var dt = DatabaseHelper.ExecuteQuery("SELECT id, name, email, role, status FROM users ORDER BY created_at DESC");
                foreach (DataRow row in dt.Rows)
                {
                    var role = row["role"].ToString();
                    var status = row["status"].ToString();
                    var rowIndex = usersTable.Rows.Add(
                        row["name"],
                        row["email"],
                        role,
                        status
                    );
                    usersTable.Rows[rowIndex].Tag = row["id"];

                    // Add action buttons
                    var editButton = new DataGridViewButtonCell
                    {
                        Value = "Modifier"
                    };
                    usersTable.Rows[rowIndex].Cells["actions"] = editButton;

                    // Color status cell
                    var cell = usersTable.Rows[rowIndex].Cells["status"];
                    if (status == "active")
                    {
                        cell.Style.BackColor = System.Drawing.Color.FromArgb(198, 239, 206);
                        cell.Style.ForeColor = System.Drawing.Color.FromArgb(0, 97, 0);
                    }
                    else
                    {
                        cell.Style.BackColor = System.Drawing.Color.FromArgb(255, 199, 206);
                        cell.Style.ForeColor = System.Drawing.Color.FromArgb(156, 0, 6);
                    }
                }
                StyleModernTable(usersTable);
            }

            private void SearchUsers(DataGridView usersTable, string searchText)
            {
                if (string.IsNullOrWhiteSpace(searchText))
                {
                    LoadUsers(usersTable);
                    return;
                }

                usersTable.Rows.Clear();
                var searchParam = $"%{searchText}%";
                var dt = DatabaseHelper.ExecuteQuery(
                    "SELECT id, name, email, role, status FROM users WHERE name LIKE @search OR email LIKE @search ORDER BY created_at DESC",
                    new[] { new MySql.Data.MySqlClient.MySqlParameter("@search", searchParam) }
                );

                foreach (DataRow row in dt.Rows)
                {
                    var rowIndex = usersTable.Rows.Add(
                        row["name"],
                        row["email"],
                        row["role"],
                        row["status"]
                    );
                    usersTable.Rows[rowIndex].Tag = row["id"];

                    // Add action buttons
                    var editButton = new DataGridViewButtonCell
                    {
                        Value = "Modifier"
                    };
                    usersTable.Rows[rowIndex].Cells["actions"] = editButton;
                }
            }

            private void ShowUserEditForm(int userId, string userRole)
            {
                var editForm = new Form
                {
                    Text = "Modifier l'utilisateur",
                    Size = new System.Drawing.Size(400, 500),
                    StartPosition = FormStartPosition.CenterParent,
                    FormBorderStyle = FormBorderStyle.FixedDialog,
                    MaximizeBox = false,
                    MinimizeBox = false
                };

                // Load user data
                var dt = DatabaseHelper.ExecuteQuery(
                    "SELECT * FROM users WHERE id = @id",
                    new[] { new MySql.Data.MySqlClient.MySqlParameter("@id", userId) }
                );
                if (dt.Rows.Count == 0) return;
                var user = dt.Rows[0];

                // Create form controls
                var nameLabel = new Label { Text = "Nom complet:", Location = new System.Drawing.Point(20, 20) };
                var nameBox = new TextBox { Location = new System.Drawing.Point(20, 40), Width = 340, Text = user["name"].ToString() };

                var emailLabel = new Label { Text = "Email:", Location = new System.Drawing.Point(20, 80) };
                var emailBox = new TextBox { Location = new System.Drawing.Point(20, 100), Width = 340, Text = user["email"].ToString() };

                var phoneLabel = new Label { Text = "Téléphone:", Location = new System.Drawing.Point(20, 140) };
                var phoneBox = new TextBox { Location = new System.Drawing.Point(20, 160), Width = 340, Text = user["phone"].ToString() };

                var addressLabel = new Label { Text = "Adresse:", Location = new System.Drawing.Point(20, 200) };
                var addressBox = new TextBox { Location = new System.Drawing.Point(20, 220), Width = 340, Height = 60, Multiline = true, Text = user["address"].ToString() };

                var statusLabel = new Label { Text = "Statut:", Location = new System.Drawing.Point(20, 300) };
                var statusCombo = new ComboBox { Location = new System.Drawing.Point(20, 320), Width = 340 };
                statusCombo.Items.AddRange(new[] { "active", "inactive" });
                statusCombo.SelectedItem = user["status"].ToString();

                var saveButton = new Button
                {
                    Text = "Enregistrer",
                    Location = new System.Drawing.Point(20, 380),
                    Width = 150
                };

                var cancelButton = new Button
                {
                    Text = "Annuler",
                    Location = new System.Drawing.Point(210, 380),
                    Width = 150
                };

                // Add controls to form
                editForm.Controls.AddRange(new Control[] {
                    nameLabel, nameBox,
                    emailLabel, emailBox,
                    phoneLabel, phoneBox,
                    addressLabel, addressBox,
                    statusLabel, statusCombo,
                    saveButton, cancelButton
                });

                // Event handlers
                saveButton.Click += (s, e) =>
                {
                    if (string.IsNullOrWhiteSpace(nameBox.Text) || string.IsNullOrWhiteSpace(emailBox.Text))
                    {
                        MessageBox.Show("Le nom et l'email sont obligatoires.", "Erreur", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    try
                    {
                        DatabaseHelper.ExecuteNonQuery(
                            "UPDATE users SET name = @name, email = @email, phone = @phone, address = @address, status = @status WHERE id = @id",
                            new[] {
                                new MySql.Data.MySqlClient.MySqlParameter("@name", nameBox.Text),
                                new MySql.Data.MySqlClient.MySqlParameter("@email", emailBox.Text),
                                new MySql.Data.MySqlClient.MySqlParameter("@phone", phoneBox.Text),
                                new MySql.Data.MySqlClient.MySqlParameter("@address", addressBox.Text),
                                new MySql.Data.MySqlClient.MySqlParameter("@status", statusCombo.SelectedItem),
                                new MySql.Data.MySqlClient.MySqlParameter("@id", userId)
                            }
                        );
                        MessageBox.Show("Utilisateur mis à jour avec succès.", "Succès", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        editForm.Close();
                        LoadUsers((DataGridView)mainPanel.Controls[2]); // Refresh the users table
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Erreur lors de la mise à jour: {ex.Message}", "Erreur", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                };

                cancelButton.Click += (s, e) => editForm.Close();

                editForm.ShowDialog();
            }

            private void ShowLocations() 
            { 
                mainPanel.Controls.Clear();
                
                // Only admin can access locations management
                if (userRole != "admin")
                {
                    var lbl = new Label { Text = "Accès non autorisé", Dock = DockStyle.Fill, Font = new System.Drawing.Font("Segoe UI", 18), TextAlign = System.Drawing.ContentAlignment.MiddleCenter };
                    mainPanel.Controls.Add(lbl);
                    return;
                }

                // Title
                var titleLabel = new Label
                {
                    Text = "Gestion des Cabinets",
                    Font = new System.Drawing.Font("Segoe UI", 16, System.Drawing.FontStyle.Bold),
                    Dock = DockStyle.Top,
                    Height = 50,
                    TextAlign = System.Drawing.ContentAlignment.MiddleLeft,
                    Padding = new Padding(10, 0, 0, 0)
                };
                mainPanel.Controls.Add(titleLabel);

                // Add New Location Button
                var addButton = new Button
                {
                    Text = "Ajouter un cabinet",
                    Dock = DockStyle.Top,
                    Height = 40,
                    Margin = new Padding(10, 0, 10, 10)
                };
                addButton.Click += (s, e) => ShowLocationEditForm();
                mainPanel.Controls.Add(addButton);

                // Locations Table
                var locationsTable = new DataGridView
                {
                    Dock = DockStyle.Fill,
                    AllowUserToAddRows = false,
                    AllowUserToDeleteRows = false,
                    AllowUserToResizeRows = false,
                    AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                    SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                    MultiSelect = false,
                    ReadOnly = true
                };

                // Add columns
                locationsTable.Columns.Add("name", "Nom");
                locationsTable.Columns.Add("address", "Adresse");
                locationsTable.Columns.Add("status", "Statut");
                locationsTable.Columns.Add("actions", "Actions");

                // Load locations
                LoadLocations(locationsTable);

                // Add table to main panel
                mainPanel.Controls.Add(locationsTable);

                // Event handlers
                locationsTable.CellClick += (s, e) =>
                {
                    if (e.ColumnIndex == locationsTable.Columns["actions"].Index && e.RowIndex >= 0)
                    {
                        var locationId = Convert.ToInt32(locationsTable.Rows[e.RowIndex].Tag);
                        var status = locationsTable.Rows[e.RowIndex].Cells["status"].Value.ToString();
                        ToggleLocationStatus(locationId, status, locationsTable);
                    }
                };
            }

            private void LoadLocations(DataGridView locationsTable)
            {
                locationsTable.Rows.Clear();
                var dt = DatabaseHelper.ExecuteQuery("SELECT id, name, address, status FROM locations ORDER BY name");
                foreach (DataRow row in dt.Rows)
                {
                    var status = row["status"].ToString();
                    var rowIndex = locationsTable.Rows.Add(
                        row["name"],
                        row["address"],
                        status == "active" ? "Actif" : "Gelé"
                    );
                    locationsTable.Rows[rowIndex].Tag = row["id"];

                    // Add action button
                    var actionButton = new DataGridViewButtonCell
                    {
                        Value = status == "active" ? "Geler" : "Réactiver"
                    };
                    locationsTable.Rows[rowIndex].Cells["actions"] = actionButton;

                    // Color status cell
                    var cell = locationsTable.Rows[rowIndex].Cells["status"];
                    if (status == "active")
                    {
                        cell.Style.BackColor = System.Drawing.Color.FromArgb(198, 239, 206);
                        cell.Style.ForeColor = System.Drawing.Color.FromArgb(0, 97, 0);
                    }
                    else
                    {
                        cell.Style.BackColor = System.Drawing.Color.FromArgb(255, 199, 206);
                        cell.Style.ForeColor = System.Drawing.Color.FromArgb(156, 0, 6);
                    }
                }
                StyleModernTable(locationsTable);
            }

            private void ToggleLocationStatus(int locationId, string currentStatus, DataGridView locationsTable)
            {
                var newStatus = currentStatus == "Actif" ? "inactive" : "active";
                var action = currentStatus == "Actif" ? "gel" : "réactivation";
                var confirmMessage = $"Confirmer le {action} de ce cabinet ?";

                if (MessageBox.Show(confirmMessage, "Confirmation", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                {
                    try
                    {
                        if (newStatus == "inactive")
                        {
                            // Begin transaction for freezing location
                            DatabaseHelper.ExecuteNonQuery("START TRANSACTION");
                            
                            // Update location status
                            DatabaseHelper.ExecuteNonQuery(
                                "UPDATE locations SET status = 'inactive' WHERE id = @id",
                                new[] { new MySql.Data.MySqlClient.MySqlParameter("@id", locationId) }
                            );
                            
                            // Cancel today's appointments
                            DatabaseHelper.ExecuteNonQuery(
                                @"UPDATE appointments 
                                SET status = 'cancelled' 
                                WHERE location_id = @id 
                                AND date = CURDATE() 
                                AND hour <= CURTIME()",
                                new[] { new MySql.Data.MySqlClient.MySqlParameter("@id", locationId) }
                            );
                            
                            // Delete future appointments
                            DatabaseHelper.ExecuteNonQuery(
                                @"DELETE FROM appointments 
                                WHERE location_id = @id 
                                AND (date > CURDATE() OR (date = CURDATE() AND hour > CURTIME()))",
                                new[] { new MySql.Data.MySqlClient.MySqlParameter("@id", locationId) }
                            );
                            
                            DatabaseHelper.ExecuteNonQuery("COMMIT");
                        }
                        else
                        {
                            // Simple status update for reactivation
                            DatabaseHelper.ExecuteNonQuery(
                                "UPDATE locations SET status = 'active' WHERE id = @id",
                                new[] { new MySql.Data.MySqlClient.MySqlParameter("@id", locationId) }
                            );
                        }

                        MessageBox.Show($"Le cabinet a été {action}é avec succès.", "Succès", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        LoadLocations(locationsTable);
                    }
                    catch (Exception ex)
                    {
                        if (newStatus == "inactive")
                        {
                            DatabaseHelper.ExecuteNonQuery("ROLLBACK");
                        }
                        MessageBox.Show($"Une erreur est survenue lors du {action} du cabinet: {ex.Message}", "Erreur", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }

            private void ShowLocationEditForm(int? locationId = null)
            {
                var editForm = new Form
                {
                    Text = locationId.HasValue ? "Modifier le cabinet" : "Ajouter un cabinet",
                    Size = new System.Drawing.Size(400, 300),
                    StartPosition = FormStartPosition.CenterParent,
                    FormBorderStyle = FormBorderStyle.FixedDialog,
                    MaximizeBox = false,
                    MinimizeBox = false
                };

                // Create form controls
                var nameLabel = new Label { Text = "Nom:", Location = new System.Drawing.Point(20, 20) };
                var nameBox = new TextBox { Location = new System.Drawing.Point(20, 40), Width = 340 };

                var addressLabel = new Label { Text = "Adresse:", Location = new System.Drawing.Point(20, 80) };
                var addressBox = new TextBox { Location = new System.Drawing.Point(20, 100), Width = 340, Height = 60, Multiline = true };

                var saveButton = new Button
                {
                    Text = "Enregistrer",
                    Location = new System.Drawing.Point(20, 180),
                    Width = 150
                };

                var cancelButton = new Button
                {
                    Text = "Annuler",
                    Location = new System.Drawing.Point(210, 180),
                    Width = 150
                };

                // Load existing location data if editing
                if (locationId.HasValue)
                {
                    var dt = DatabaseHelper.ExecuteQuery(
                        "SELECT * FROM locations WHERE id = @id",
                        new[] { new MySql.Data.MySqlClient.MySqlParameter("@id", locationId.Value) }
                    );
                    if (dt.Rows.Count > 0)
                    {
                        var location = dt.Rows[0];
                        nameBox.Text = location["name"].ToString();
                        addressBox.Text = location["address"].ToString();
                    }
                }

                // Add controls to form
                editForm.Controls.AddRange(new Control[] {
                    nameLabel, nameBox,
                    addressLabel, addressBox,
                    saveButton, cancelButton
                });

                // Event handlers
                saveButton.Click += (s, e) =>
                {
                    if (string.IsNullOrWhiteSpace(nameBox.Text))
                    {
                        MessageBox.Show("Le nom est obligatoire.", "Erreur", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    try
                    {
                        if (locationId.HasValue)
                        {
                            // Update existing location
                            DatabaseHelper.ExecuteNonQuery(
                                "UPDATE locations SET name = @name, address = @address WHERE id = @id",
                                new[] {
                                    new MySql.Data.MySqlClient.MySqlParameter("@name", nameBox.Text),
                                    new MySql.Data.MySqlClient.MySqlParameter("@address", addressBox.Text),
                                    new MySql.Data.MySqlClient.MySqlParameter("@id", locationId.Value)
                                }
                            );
                        }
                        else
                        {
                            // Insert new location
                            DatabaseHelper.ExecuteNonQuery(
                                "INSERT INTO locations (name, address, status) VALUES (@name, @address, 'active')",
                                new[] {
                                    new MySql.Data.MySqlClient.MySqlParameter("@name", nameBox.Text),
                                    new MySql.Data.MySqlClient.MySqlParameter("@address", addressBox.Text)
                                }
                            );
                        }

                        MessageBox.Show("Cabinet enregistré avec succès.", "Succès", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        editForm.Close();
                        LoadLocations((DataGridView)mainPanel.Controls[2]); // Refresh the locations table
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Erreur lors de l'enregistrement: {ex.Message}", "Erreur", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                };

                cancelButton.Click += (s, e) => editForm.Close();

                editForm.ShowDialog();
            }

            private void ShowSettings() 
            { 
                mainPanel.Controls.Clear();
                
                // Only admin can access settings
                if (userRole != "admin")
                {
                    var lbl = new Label { Text = "Accès non autorisé", Dock = DockStyle.Fill, Font = new System.Drawing.Font("Segoe UI", 18), TextAlign = System.Drawing.ContentAlignment.MiddleCenter };
                    mainPanel.Controls.Add(lbl);
                    return;
                }

                // Title
                var titleLabel = new Label
                {
                    Text = "Paramètres d'annulation",
                    Font = new System.Drawing.Font("Segoe UI", 16, System.Drawing.FontStyle.Bold),
                    Dock = DockStyle.Top,
                    Height = 50,
                    TextAlign = System.Drawing.ContentAlignment.MiddleLeft,
                    Padding = new Padding(20, 10, 0, 0)
                };
                mainPanel.Controls.Add(titleLabel);

                // Create a panel for settings
                var settingsPanel = new Panel
                {
                    Dock = DockStyle.Fill,
                    Padding = new Padding(20),
                    AutoScroll = true,
                    BackColor = System.Drawing.Color.White
                };

                // 1. General Cancellation Time Section
                var cancelTimeSection = new Panel
                {
                    Dock = DockStyle.Top,
                    Height = 180,
                    Padding = new Padding(15),
                    BorderStyle = BorderStyle.FixedSingle,
                    BackColor = System.Drawing.Color.FromArgb(248, 249, 250)
                };
                var cancelTimeTitle = new Label
                {
                    Text = "Délai d'annulation général",
                    Font = new System.Drawing.Font("Segoe UI", 12, System.Drawing.FontStyle.Bold),
                    Dock = DockStyle.Top,
                    Height = 30,
                    ForeColor = System.Drawing.Color.FromArgb(27, 53, 60)
                };
                cancelTimeSection.Controls.Add(cancelTimeTitle);

                // Cancel time setting
                var cancelTimeLabel = new Label
                {
                    Text = "Délai minimum pour annuler (en heures)",
                    Location = new System.Drawing.Point(15, 40),
                    AutoSize = true,
                    Font = new System.Drawing.Font("Segoe UI", 9)
                };
                var cancelTimeBox = new NumericUpDown
                {
                    Location = new System.Drawing.Point(15, 60),
                    Width = 200,
                    Minimum = 1,
                    Maximum = 72,
                    Value = 24,
                    Font = new System.Drawing.Font("Segoe UI", 10),
                    BorderStyle = BorderStyle.FixedSingle
                };
                var hoursLabel = new Label
                {
                    Text = "heures",
                    Location = new System.Drawing.Point(225, 62),
                    AutoSize = true,
                    Font = new System.Drawing.Font("Segoe UI", 9)
                };
                var cancelTimeInfo = new Label
                {
                    Text = "Les patients ne pourront pas annuler leur rendez-vous moins de X heures avant l'heure du rendez-vous.",
                    Location = new System.Drawing.Point(15, 90),
                    AutoSize = true,
                    Font = new System.Drawing.Font("Segoe UI", 9),
                    ForeColor = System.Drawing.Color.FromArgb(73, 80, 87)
                };

                // Save button for cancellation time
                var saveCancelTimeButton = new Button
                {
                    Text = "Enregistrer",
                    Location = new System.Drawing.Point(15, 120),
                    Width = 120,
                    Height = 35,
                    Font = new System.Drawing.Font("Segoe UI", 9, System.Drawing.FontStyle.Bold),
                    BackColor = System.Drawing.Color.FromArgb(27, 53, 60),
                    ForeColor = System.Drawing.Color.White,
                    FlatStyle = FlatStyle.Flat
                };
                saveCancelTimeButton.FlatAppearance.BorderSize = 0;
                saveCancelTimeButton.Click += (s, e) =>
                {
                    try
                    {
                        DatabaseHelper.ExecuteNonQuery(
                            "INSERT INTO settings (name, value) VALUES ('cancel_time', @value) ON DUPLICATE KEY UPDATE value = @value",
                            new[] { new MySql.Data.MySqlClient.MySqlParameter("@value", cancelTimeBox.Value) }
                        );
                        cancelTimeInfo.Text = $"Les patients ne pourront pas annuler leur rendez-vous moins de {cancelTimeBox.Value} heures avant l'heure du rendez-vous.";
                        MessageBox.Show("Paramètres enregistrés avec succès.", "Succès", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Erreur lors de l'enregistrement: {ex.Message}", "Erreur", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                };

                cancelTimeSection.Controls.AddRange(new Control[] { 
                    cancelTimeLabel, cancelTimeBox, hoursLabel, cancelTimeInfo, saveCancelTimeButton 
                });

                // 2. Special Days Section
                var specialDaysSection = new Panel
                {
                    Dock = DockStyle.Top,
                    Height = 450,
                    Padding = new Padding(15),
                    BorderStyle = BorderStyle.FixedSingle,
                    Top = cancelTimeSection.Height + 20,
                    BackColor = System.Drawing.Color.FromArgb(248, 249, 250)
                };

                var specialDaysTitle = new Label
                {
                    Text = "Jours spéciaux",
                    Font = new System.Drawing.Font("Segoe UI", 12, System.Drawing.FontStyle.Bold),
                    Dock = DockStyle.Top,
                    Height = 30,
                    ForeColor = System.Drawing.Color.FromArgb(27, 53, 60)
                };
                specialDaysSection.Controls.Add(specialDaysTitle);

                // Special days form
                var specialDaysForm = new Panel
                {
                    Dock = DockStyle.Top,
                    Height = 220,
                    Padding = new Padding(15)
                };

                // Location dropdown
                var locationLabel = new Label 
                { 
                    Text = "Cabinet", 
                    Location = new System.Drawing.Point(15, 10),
                    Font = new System.Drawing.Font("Segoe UI", 9)
                };
                var locationCombo = new ComboBox
                {
                    Location = new System.Drawing.Point(15, 30),
                    Width = 250,
                    DropDownStyle = ComboBoxStyle.DropDownList,
                    Font = new System.Drawing.Font("Segoe UI", 10),
                    FlatStyle = FlatStyle.Flat
                };

                // Date picker
                var dateLabel = new Label 
                { 
                    Text = "Date", 
                    Location = new System.Drawing.Point(15, 60),
                    Font = new System.Drawing.Font("Segoe UI", 9)
                };
                var datePicker = new DateTimePicker
                {
                    Location = new System.Drawing.Point(15, 80),
                    Width = 250,
                    Format = DateTimePickerFormat.Short,
                    Font = new System.Drawing.Font("Segoe UI", 10)
                };

                // Time inputs
                var startTimeLabel = new Label 
                { 
                    Text = "Heure de début", 
                    Location = new System.Drawing.Point(15, 110),
                    Font = new System.Drawing.Font("Segoe UI", 9)
                };
                var startTimePicker = new DateTimePicker
                {
                    Location = new System.Drawing.Point(15, 130),
                    Width = 120,
                    Format = DateTimePickerFormat.Time,
                    ShowUpDown = true,
                    Font = new System.Drawing.Font("Segoe UI", 10)
                };

                var endTimeLabel = new Label 
                { 
                    Text = "Heure de fin", 
                    Location = new System.Drawing.Point(145, 110),
                    Font = new System.Drawing.Font("Segoe UI", 9)
                };
                var endTimePicker = new DateTimePicker
                {
                    Location = new System.Drawing.Point(145, 130),
                    Width = 120,
                    Format = DateTimePickerFormat.Time,
                    ShowUpDown = true,
                    Font = new System.Drawing.Font("Segoe UI", 10)
                };

                // Whole day checkbox
                var wholeDayCheck = new CheckBox
                {
                    Text = "Journée entière",
                    Location = new System.Drawing.Point(15, 160),
                    AutoSize = true,
                    Font = new System.Drawing.Font("Segoe UI", 9)
                };
                wholeDayCheck.CheckedChanged += (s, e) =>
                {
                    startTimePicker.Enabled = !wholeDayCheck.Checked;
                    endTimePicker.Enabled = !wholeDayCheck.Checked;
                };

                // Special days list
                var specialDaysList = new DataGridView
                {
                    Dock = DockStyle.Fill,
                    AllowUserToAddRows = false,
                    AllowUserToDeleteRows = false,
                    AllowUserToResizeRows = false,
                    AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                    SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                    MultiSelect = false,
                    ReadOnly = true,
                    BackgroundColor = System.Drawing.Color.White,
                    BorderStyle = BorderStyle.None,
                    Font = new System.Drawing.Font("Segoe UI", 9),
                    RowHeadersVisible = false
                };

                // Add columns
                specialDaysList.Columns.Add("location", "Cabinet");
                specialDaysList.Columns.Add("date", "Date");
                specialDaysList.Columns.Add("time", "Heure");
                specialDaysList.Columns.Add("actions", "Actions");

                // Style the DataGridView
                specialDaysList.ColumnHeadersDefaultCellStyle.BackColor = System.Drawing.Color.FromArgb(27, 53, 60);
                specialDaysList.ColumnHeadersDefaultCellStyle.ForeColor = System.Drawing.Color.White;
                specialDaysList.ColumnHeadersDefaultCellStyle.Font = new System.Drawing.Font("Segoe UI", 9, System.Drawing.FontStyle.Bold);
                specialDaysList.EnableHeadersVisualStyles = false;
                specialDaysList.DefaultCellStyle.Padding = new Padding(5);
                specialDaysList.DefaultCellStyle.SelectionBackColor = System.Drawing.Color.FromArgb(220, 240, 255);
                specialDaysList.DefaultCellStyle.SelectionForeColor = System.Drawing.Color.Black;

                // Add special day button
                var addSpecialDayButton = new Button
                {
                    Text = "Ajouter",
                    Location = new System.Drawing.Point(15, 190),
                    Width = 120,
                    Height = 35,
                    Font = new System.Drawing.Font("Segoe UI", 9, System.Drawing.FontStyle.Bold),
                    BackColor = System.Drawing.Color.FromArgb(27, 53, 60),
                    ForeColor = System.Drawing.Color.White,
                    FlatStyle = FlatStyle.Flat
                };
                addSpecialDayButton.FlatAppearance.BorderSize = 0;
                addSpecialDayButton.Click += (s, e) =>
                {
                    if (locationCombo.SelectedItem == null)
                    {
                        MessageBox.Show("Veuillez sélectionner un cabinet.", "Erreur", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    try
                    {
                        var locationId = ((dynamic)locationCombo.SelectedItem).Value;
                        var date = datePicker.Value.Date;
                        var startTime = wholeDayCheck.Checked ? TimeSpan.Zero : startTimePicker.Value.TimeOfDay;
                        var endTime = wholeDayCheck.Checked ? new TimeSpan(23, 59, 59) : endTimePicker.Value.TimeOfDay;

                        DatabaseHelper.ExecuteNonQuery(
                            "INSERT INTO special_days (date, start_time, end_time, is_whole_day, location_id) VALUES (@date, @start_time, @end_time, @is_whole_day, @location_id)",
                            new[] {
                                new MySql.Data.MySqlClient.MySqlParameter("@date", date),
                                new MySql.Data.MySqlClient.MySqlParameter("@start_time", startTime),
                                new MySql.Data.MySqlClient.MySqlParameter("@end_time", endTime),
                                new MySql.Data.MySqlClient.MySqlParameter("@is_whole_day", wholeDayCheck.Checked),
                                new MySql.Data.MySqlClient.MySqlParameter("@location_id", locationId)
                            }
                        );
                        MessageBox.Show("Jour spécial ajouté avec succès.", "Succès", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        LoadSpecialDays();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Erreur lors de l'ajout: {ex.Message}", "Erreur", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                };

                // Add controls to panels
                specialDaysForm.Controls.AddRange(new Control[] {
                    locationLabel, locationCombo,
                    dateLabel, datePicker,
                    startTimeLabel, startTimePicker,
                    endTimeLabel, endTimePicker,
                    wholeDayCheck,
                    addSpecialDayButton
                });

                // Create a container panel for the list
                var listContainer = new Panel
                {
                    Dock = DockStyle.Fill,
                    Padding = new Padding(0, 220, 0, 0) // Space for the form above
                };
                listContainer.Controls.Add(specialDaysList);

                specialDaysSection.Controls.Add(specialDaysForm);
                specialDaysSection.Controls.Add(listContainer);

                // Load locations
                void LoadLocations()
                {
                    locationCombo.Items.Clear();
                    var dt = DatabaseHelper.ExecuteQuery("SELECT id, name FROM locations WHERE status = 'active' ORDER BY name");
                    foreach (DataRow row in dt.Rows)
                    {
                        locationCombo.Items.Add(new { Text = row["name"].ToString(), Value = row["id"] });
                    }
                    locationCombo.DisplayMember = "Text";
                    locationCombo.ValueMember = "Value";
                }

                // Load special days
                void LoadSpecialDays()
                {
                    specialDaysList.Rows.Clear();
                    try
                    {
                        var dt = DatabaseHelper.ExecuteQuery(
                            "SELECT sd.*, l.name as location_name FROM special_days sd " +
                            "LEFT JOIN locations l ON sd.location_id = l.id " +
                            "ORDER BY sd.date DESC"
                        );
                        foreach (DataRow row in dt.Rows)
                        {
                            var date = Convert.ToDateTime(row["date"]).ToString("dd/MM/yyyy");
                            var time = row["is_whole_day"].ToString() == "1" ? "Journée entière" :
                                $"{TimeSpan.Parse(row["start_time"].ToString()):hh\\:mm} - {TimeSpan.Parse(row["end_time"].ToString()):hh\\:mm}";
                            
                            var rowIndex = specialDaysList.Rows.Add(
                                row["location_name"].ToString(),
                                date,
                                time
                            );
                            specialDaysList.Rows[rowIndex].Tag = row["id"];

                            // Add delete button
                            var deleteButton = new DataGridViewButtonCell
                            {
                                Value = "Supprimer",
                                Style = new DataGridViewCellStyle
                                {
                                    BackColor = System.Drawing.Color.FromArgb(220, 53, 69),
                                    ForeColor = System.Drawing.Color.White,
                                    Font = new System.Drawing.Font("Segoe UI", 9, System.Drawing.FontStyle.Bold)
                                }
                            };
                            specialDaysList.Rows[rowIndex].Cells["actions"] = deleteButton;
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Erreur lors du chargement des jours spéciaux: {ex.Message}", 
                            "Erreur", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }

                // Handle special days list cell click
                specialDaysList.CellClick += (s, e) =>
                {
                    if (e.ColumnIndex == specialDaysList.Columns["actions"].Index && e.RowIndex >= 0)
                    {
                        var dayId = specialDaysList.Rows[e.RowIndex].Tag.ToString();
                        if (MessageBox.Show("Êtes-vous sûr de vouloir supprimer ce jour spécial ?", 
                            "Confirmation", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                        {
                            try
                            {
                                DatabaseHelper.ExecuteNonQuery(
                                    "DELETE FROM special_days WHERE id = @id",
                                    new[] { new MySql.Data.MySqlClient.MySqlParameter("@id", dayId) }
                                );
                                LoadSpecialDays();
                            }
                            catch (Exception ex)
                            {
                                MessageBox.Show($"Erreur lors de la suppression: {ex.Message}", 
                                    "Erreur", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            }
                        }
                    }
                };

                // Load initial data
                LoadLocations();
                LoadSpecialDays();

                // Add sections to settings panel
                settingsPanel.Controls.Add(cancelTimeSection);
                settingsPanel.Controls.Add(specialDaysSection);

                mainPanel.Controls.Add(settingsPanel);
            }

            private void ShowProfile()
            {
                mainPanel.Controls.Clear();

                // Load user data
                var dt = DatabaseHelper.ExecuteQuery(
                    "SELECT * FROM users WHERE id = @id",
                    new[] { new MySql.Data.MySqlClient.MySqlParameter("@id", userId) }
                );
                if (dt.Rows.Count == 0)
                {
                    var lbl = new Label { Text = "Utilisateur introuvable.", Dock = DockStyle.Fill, Font = new System.Drawing.Font("Segoe UI", 18), TextAlign = System.Drawing.ContentAlignment.MiddleCenter };
                    mainPanel.Controls.Add(lbl);
                    return;
                }
                var user = dt.Rows[0];

                // Title
                var titleLabel = new Label
                {
                    Text = "Mon profil",
                    Font = new System.Drawing.Font("Segoe UI", 16, System.Drawing.FontStyle.Bold),
                    Dock = DockStyle.Top,
                    Height = 50,
                    TextAlign = System.Drawing.ContentAlignment.MiddleLeft,
                    Padding = new Padding(10, 0, 0, 0)
                };
                mainPanel.Controls.Add(titleLabel);

                // Profile form
                var formPanel = new Panel { Dock = DockStyle.Top, Height = 350, Padding = new Padding(20) };

                var nameLabel = new Label { Text = "Nom complet:", Location = new System.Drawing.Point(10, 20) };
                var nameBox = new TextBox { Location = new System.Drawing.Point(10, 40), Width = 340, Text = user["name"].ToString() };

                var emailLabel = new Label { Text = "Email:", Location = new System.Drawing.Point(10, 80) };
                var emailBox = new TextBox { Location = new System.Drawing.Point(10, 100), Width = 340, Text = user["email"].ToString() };

                var phoneLabel = new Label { Text = "Téléphone:", Location = new System.Drawing.Point(10, 140) };
                var phoneBox = new TextBox { Location = new System.Drawing.Point(10, 160), Width = 340, Text = user["phone"].ToString() };

                var addressLabel = new Label { Text = "Adresse:", Location = new System.Drawing.Point(10, 200) };
                var addressBox = new TextBox { Location = new System.Drawing.Point(10, 220), Width = 340, Height = 60, Multiline = true, Text = user["address"].ToString() };

                var saveButton = new Button
                {
                    Text = "Enregistrer",
                    Location = new System.Drawing.Point(10, 300),
                    Width = 150
                };

                saveButton.Click += (s, e) =>
                {
                    if (string.IsNullOrWhiteSpace(nameBox.Text) || string.IsNullOrWhiteSpace(emailBox.Text))
                    {
                        MessageBox.Show("Le nom et l'email sont obligatoires.", "Erreur", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }
                    try
                    {
                        DatabaseHelper.ExecuteNonQuery(
                            "UPDATE users SET name = @name, email = @email, phone = @phone, address = @address WHERE id = @id",
                            new[] {
                                new MySql.Data.MySqlClient.MySqlParameter("@name", nameBox.Text),
                                new MySql.Data.MySqlClient.MySqlParameter("@email", emailBox.Text),
                                new MySql.Data.MySqlClient.MySqlParameter("@phone", phoneBox.Text),
                                new MySql.Data.MySqlClient.MySqlParameter("@address", addressBox.Text),
                                new MySql.Data.MySqlClient.MySqlParameter("@id", userId)
                            }
                        );
                        MessageBox.Show("Profil mis à jour avec succès.", "Succès", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Erreur lors de la mise à jour: {ex.Message}", "Erreur", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                };

                formPanel.Controls.AddRange(new Control[] {
                    nameLabel, nameBox,
                    emailLabel, emailBox,
                    phoneLabel, phoneBox,
                    addressLabel, addressBox,
                    saveButton
                });
                mainPanel.Controls.Add(formPanel);

                // Password change form
                var pwdPanel = new Panel { Dock = DockStyle.Top, Height = 220, Padding = new Padding(20) };
                var pwdTitle = new Label { Text = "Changer le mot de passe", Font = new System.Drawing.Font("Segoe UI", 12, System.Drawing.FontStyle.Bold), Location = new System.Drawing.Point(10, 0) };
                var currentPwdLabel = new Label { Text = "Mot de passe actuel:", Location = new System.Drawing.Point(10, 40) };
                var currentPwdBox = new TextBox { Location = new System.Drawing.Point(10, 60), Width = 340, UseSystemPasswordChar = true };
                var newPwdLabel = new Label { Text = "Nouveau mot de passe:", Location = new System.Drawing.Point(10, 100) };
                var newPwdBox = new TextBox { Location = new System.Drawing.Point(10, 120), Width = 340, UseSystemPasswordChar = true };
                var confirmPwdLabel = new Label { Text = "Confirmer le nouveau mot de passe:", Location = new System.Drawing.Point(10, 160) };
                var confirmPwdBox = new TextBox { Location = new System.Drawing.Point(10, 180), Width = 340, UseSystemPasswordChar = true };
                var pwdSaveButton = new Button { Text = "Changer le mot de passe", Location = new System.Drawing.Point(10, 210), Width = 200 };

                pwdSaveButton.Click += (s, e) =>
                {
                    if (string.IsNullOrWhiteSpace(currentPwdBox.Text) || string.IsNullOrWhiteSpace(newPwdBox.Text) || string.IsNullOrWhiteSpace(confirmPwdBox.Text))
                    {
                        MessageBox.Show("Tous les champs sont obligatoires.", "Erreur", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }
                    if (newPwdBox.Text != confirmPwdBox.Text)
                    {
                        MessageBox.Show("Les nouveaux mots de passe ne correspondent pas.", "Erreur", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }
                    try
                    {
                        // Verify current password (plain text)
                        var dtPwd = DatabaseHelper.ExecuteQuery(
                            "SELECT password FROM users WHERE id = @id",
                            new[] { new MySql.Data.MySqlClient.MySqlParameter("@id", userId) }
                        );
                        if (dtPwd.Rows.Count == 0 || currentPwdBox.Text != dtPwd.Rows[0]["password"].ToString())
                        {
                            MessageBox.Show("Mot de passe actuel incorrect.", "Erreur", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            return;
                        }
                        // Update password (plain text)
                        DatabaseHelper.ExecuteNonQuery(
                            "UPDATE users SET password = @pwd WHERE id = @id",
                            new[] {
                                new MySql.Data.MySqlClient.MySqlParameter("@pwd", newPwdBox.Text),
                                new MySql.Data.MySqlClient.MySqlParameter("@id", userId)
                            }
                        );
                        MessageBox.Show("Mot de passe changé avec succès.", "Succès", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        currentPwdBox.Clear(); newPwdBox.Clear(); confirmPwdBox.Clear();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Erreur lors du changement de mot de passe: {ex.Message}", "Erreur", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                };

                pwdPanel.Controls.AddRange(new Control[] {
                    pwdTitle,
                    currentPwdLabel, currentPwdBox,
                    newPwdLabel, newPwdBox,
                    confirmPwdLabel, confirmPwdBox,
                    pwdSaveButton
                });
                mainPanel.Controls.Add(pwdPanel);
            }
            private void Logout()
            {
                this.Hide();
                var loginForm = new LoginForm();
                loginForm.ShowDialog();
                this.Close();
            }

            private void ShowPatients()
            {
                mainPanel.Controls.Clear();

                // Initialize patientsOlv first
                var patientsOlv = new BrightIdeasSoftware.ObjectListView
                {
                    Dock = DockStyle.Fill,
                    FullRowSelect = true,
                    GridLines = true,
                    MultiSelect = false,
                    ShowGroups = false,
                    HeaderUsesThemes = false,
                    Font = new System.Drawing.Font("Segoe UI", 9),
                    BackColor = System.Drawing.Color.White
                };

                // Define columns
                patientsOlv.Columns.Add(new BrightIdeasSoftware.OLVColumn("ID", "Id") { Width = 60 });
                patientsOlv.Columns.Add(new BrightIdeasSoftware.OLVColumn("Nom", "Nom") { Width = 180 });
                patientsOlv.Columns.Add(new BrightIdeasSoftware.OLVColumn("Email", "Email") { Width = 180 });
                patientsOlv.Columns.Add(new BrightIdeasSoftware.OLVColumn("Téléphone", "Telephone") { Width = 120 });
                patientsOlv.Columns.Add(new BrightIdeasSoftware.OLVColumn("Actions", "Actions") { Width = 100 });

                // Title
                var titleLabel = new Label
                {
                    Text = "Gestion des clients",
                    Font = new System.Drawing.Font("Segoe UI", 16, System.Drawing.FontStyle.Bold),
                    Dock = DockStyle.Top,
                    Height = 50,
                    TextAlign = System.Drawing.ContentAlignment.MiddleLeft,
                    Padding = new Padding(10, 20, 0, 0)
                };
                mainPanel.Controls.Add(titleLabel);

                // Search Panel
                var searchPanel = new Panel { Dock = DockStyle.Top, Height = 60 };
                var searchBox = new TextBox { Location = new System.Drawing.Point(10, 20), Width = 300 };
                var searchButton = new Button { Text = "Rechercher", Location = new System.Drawing.Point(320, 20), Width = 100 };
                var resetButton = new Button { Text = "Réinitialiser", Location = new System.Drawing.Point(430, 20), Width = 100 };
                searchPanel.Controls.AddRange(new Control[] { searchBox, searchButton, resetButton });
                mainPanel.Controls.Add(searchPanel);

                // Add ObjectListView to main panel
                mainPanel.Controls.Add(patientsOlv);

                // Define LoadClients and LoadClientsByLetter methods
                void LoadClients(string search = "")
                {
                    var data = new List<dynamic>();
                    string query = "SELECT id, name, email, phone FROM users WHERE role = 'client'";
                    var paramList = new List<MySql.Data.MySqlClient.MySqlParameter>();
                    if (!string.IsNullOrWhiteSpace(search))
                    {
                        query += " AND (LOWER(name) LIKE LOWER(@search) OR LOWER(email) LIKE LOWER(@search) OR phone LIKE @search)";
                        paramList.Add(new MySql.Data.MySqlClient.MySqlParameter("@search", $"%{search}%"));
                    }
                    query += " ORDER BY name";
                    var dt = DatabaseHelper.ExecuteQuery(query, paramList.ToArray());
                    foreach (DataRow row in dt.Rows)
                    {
                        data.Add(new {
                            Id = row["id"],
                            Nom = row["name"].ToString(),
                            Email = row["email"].ToString(),
                            Telephone = row["phone"].ToString(),
                            Actions = "Détails"
                        });
                    }
                    patientsOlv.SetObjects(data);
                }

                // Event handlers
                searchButton.Click += (s, e) => LoadClients(searchBox.Text);
                resetButton.Click += (s, e) => { searchBox.Clear(); LoadClients(); };
                searchBox.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) LoadClients(searchBox.Text); };
                patientsOlv.CellClick += (s, e) =>
                {
                    if (e.ColumnIndex == 4 && e.RowIndex >= 0)
                    {
                        var model = patientsOlv.GetModelObject(e.RowIndex) as dynamic;
                        if (model != null)
                            ShowPatientDetails(model.Id.ToString());
                    }
                };

                // Initial load
                LoadClients();
            }

            private void ShowBookFor()
            {
                var form = new BookAppointmentForm(userId);
                form.ShowDialog();
            }

            private void ShowPatientDetails(string patientId)
            {
                var form = new Form
                {
                    Text = "Détails du client",
                    Size = new System.Drawing.Size(600, 500),
                    StartPosition = FormStartPosition.CenterParent,
                    FormBorderStyle = FormBorderStyle.FixedDialog,
                    MaximizeBox = false,
                    MinimizeBox = false
                };
                var dt = DatabaseHelper.ExecuteQuery(
                    "SELECT * FROM users WHERE id = @id",
                    new[] { new MySql.Data.MySqlClient.MySqlParameter("@id", patientId) }
                );
                if (dt.Rows.Count > 0)
                {
                    var user = dt.Rows[0];
                    var nameLabel = new Label { Text = $"Nom: {user["name"]}", Location = new System.Drawing.Point(20, 20), AutoSize = true };
                    var emailLabel = new Label { Text = $"Email: {user["email"]}", Location = new System.Drawing.Point(20, 50), AutoSize = true };
                    var phoneLabel = new Label { Text = $"Téléphone: {user["phone"]}", Location = new System.Drawing.Point(20, 80), AutoSize = true };
                    form.Controls.AddRange(new Control[] { nameLabel, emailLabel, phoneLabel });

                    // Previous appointments table
                    var apptLabel = new Label { Text = "Rendez-vous précédents:", Location = new System.Drawing.Point(20, 120), AutoSize = true, Font = new System.Drawing.Font("Segoe UI", 10, System.Drawing.FontStyle.Bold) };
                    form.Controls.Add(apptLabel);
                    var apptTable = new DataGridView
                    {
                        Location = new System.Drawing.Point(20, 150),
                        Size = new System.Drawing.Size(540, 250),
                        AllowUserToAddRows = false,
                        AllowUserToDeleteRows = false,
                        AllowUserToResizeRows = false,
                        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                        SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                        MultiSelect = false,
                        ReadOnly = true
                    };
                    apptTable.Columns.Add("date", "Date");
                    apptTable.Columns.Add("heure", "Heure");
                    apptTable.Columns.Add("therapist", "Thérapeute");
                    apptTable.Columns.Add("location", "Cabinet");
                    apptTable.Columns.Add("status", "Statut");
                    apptTable.Columns.Add("rapport", "Rapport");
                    // Load previous appointments
                    var appts = DatabaseHelper.ExecuteQuery(
                        "SELECT a.id, a.date, a.hour, l.name as location, COALESCE(u2.name, '-') as therapist, a.status, r.content as rapport " +
                        "FROM appointments a " +
                        "JOIN locations l ON a.location_id = l.id " +
                        "LEFT JOIN reports r ON r.appointment_id = a.id " +
                        "LEFT JOIN users u2 ON r.therapist_id = u2.id " +
                        "WHERE a.user_id = @uid ORDER BY a.date DESC, a.hour DESC",
                        new[] { new MySql.Data.MySqlClient.MySqlParameter("@uid", patientId) }
                    );
                    foreach (DataRow row in appts.Rows)
                    {
                        apptTable.Rows.Add(
                            Convert.ToDateTime(row["date"]).ToString("dd/MM/yyyy"),
                            TimeSpan.Parse(row["hour"].ToString()).ToString(@"hh\:mm"),
                            row["therapist"].ToString(),
                            row["location"].ToString(),
                            row["status"].ToString(),
                            string.IsNullOrEmpty(row["rapport"].ToString()) ? "-" : "Voir rapport"
                        );
                    }
                    apptTable.CellClick += (s, e) =>
                    {
                        if (e.ColumnIndex == apptTable.Columns["rapport"].Index && e.RowIndex >= 0)
                        {
                            var rapport = appts.Rows[e.RowIndex]["rapport"].ToString();
                            var therapist = appts.Rows[e.RowIndex]["therapist"].ToString();
                            if (!string.IsNullOrEmpty(rapport) && rapport != "-")
                            {
                                var rapportForm = new Form
                                {
                                    Text = "Rapport du rendez-vous",
                                    Size = new System.Drawing.Size(500, 400),
                                    StartPosition = FormStartPosition.CenterParent,
                                    FormBorderStyle = FormBorderStyle.FixedDialog,
                                    MaximizeBox = false,
                                    MinimizeBox = false
                                };
                                var rapportBox = new TextBox
                                {
                                    Multiline = true,
                                    ReadOnly = true,
                                    Dock = DockStyle.Fill,
                                    Text = $"Rédigé par: {therapist}\r\n\r\n{rapport}",
                                    ScrollBars = ScrollBars.Vertical
                                };
                                rapportForm.Controls.Add(rapportBox);
                                rapportForm.ShowDialog();
                            }
                        }
                    };
                    form.Controls.Add(apptTable);
                }
                var closeButton = new Button { Text = "Fermer", Location = new System.Drawing.Point(20, 420), Width = 100 };
                closeButton.Click += (s, e) => form.Close();
                form.Controls.Add(closeButton);
                form.ShowDialog();
            }

            private void ShowCreatePatientTab()
            {
                mainPanel.Controls.Clear();
                var createForm = new CreatePatientForm();
                createForm.TopLevel = false;
                createForm.FormBorderStyle = FormBorderStyle.None;
                createForm.Dock = DockStyle.Fill;
                mainPanel.Controls.Add(createForm);
                createForm.Show();
            }

            // Minimal InitializeComponent to fix missing method error
            private void InitializeComponent()
            {
                this.SuspendLayout();
                this.ClientSize = new System.Drawing.Size(1200, 800);
                this.ResumeLayout(false);
            }

            private void ShowAdminDashboard()
            {
                mainPanel.Controls.Clear();

                // 1. Weekly/Monthly View Toggle
                var togglePanel = new Panel { Dock = DockStyle.Top, Height = 50, Padding = new Padding(10) };
                var weeklyBtn = new Button { Text = "Vue Hebdomadaire", Width = 150, Height = 35, FlatStyle = FlatStyle.Flat, BackColor = System.Drawing.Color.FromArgb(0,120,215), ForeColor = System.Drawing.Color.White };
                var monthlyBtn = new Button { Text = "Vue Mensuelle", Width = 150, Height = 35, FlatStyle = FlatStyle.Flat, BackColor = System.Drawing.Color.White, ForeColor = System.Drawing.Color.Black };
                togglePanel.Controls.Add(weeklyBtn);
                togglePanel.Controls.Add(monthlyBtn);
                weeklyBtn.Left = 10;
                monthlyBtn.Left = 170;
                mainPanel.Controls.Add(togglePanel);

                // State
                string view = "weekly";
                void SetToggleColors()
                {
                    if (view == "weekly")
                    {
                        weeklyBtn.BackColor = System.Drawing.Color.FromArgb(0,120,215);
                        weeklyBtn.ForeColor = System.Drawing.Color.White;
                        monthlyBtn.BackColor = System.Drawing.Color.White;
                        monthlyBtn.ForeColor = System.Drawing.Color.Black;
                    }
                    else
                    {
                        monthlyBtn.BackColor = System.Drawing.Color.FromArgb(0,120,215);
                        monthlyBtn.ForeColor = System.Drawing.Color.White;
                        weeklyBtn.BackColor = System.Drawing.Color.White;
                        weeklyBtn.ForeColor = System.Drawing.Color.Black;
                    }
                }

                // 2. Statistics Cards
                var statsPanel = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 120, Padding = new Padding(10), FlowDirection = FlowDirection.LeftToRight, AutoSize = true };
                var usersCard = new Panel { Width = 180, Height = 100, BackColor = System.Drawing.Color.FromArgb(0,120,215), Margin = new Padding(10) };
                var therapistsCard = new Panel { Width = 180, Height = 100, BackColor = System.Drawing.Color.FromArgb(40,167,69), Margin = new Padding(10) };
                var apptsCard = new Panel { Width = 180, Height = 100, BackColor = System.Drawing.Color.FromArgb(23,162,184), Margin = new Padding(10) };
                var locationsCard = new Panel { Width = 180, Height = 100, BackColor = System.Drawing.Color.FromArgb(255,193,7), Margin = new Padding(10) };
                statsPanel.Controls.AddRange(new[] { usersCard, therapistsCard, apptsCard, locationsCard });
                mainPanel.Controls.Add(statsPanel);

                // 3. Charts
                var chartsPanel = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 260, Padding = new Padding(10), FlowDirection = FlowDirection.LeftToRight, AutoSize = true };
                var hourlyChart = new Chart { Width = 350, Height = 220, BackColor = System.Drawing.Color.WhiteSmoke };
                var periodChart = new Chart { Width = 350, Height = 220, BackColor = System.Drawing.Color.WhiteSmoke };
                chartsPanel.Controls.Add(hourlyChart);
                chartsPanel.Controls.Add(periodChart);
                mainPanel.Controls.Add(chartsPanel);

                // 4. Appointments Table
                var apptTable = new DataGridView
                {
                    Dock = DockStyle.Fill,
                    AllowUserToAddRows = false,
                    AllowUserToDeleteRows = false,
                    AllowUserToResizeRows = false,
                    AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                    SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                    MultiSelect = false,
                    ReadOnly = true,
                    RowHeadersVisible = false
                };
                apptTable.Columns.Add("date", "Date");
                apptTable.Columns.Add("heure", "Heure");
                apptTable.Columns.Add("client", "Client");
                apptTable.Columns.Add("therapist", "Thérapeute");
                apptTable.Columns.Add("location", "Cabinet");
                apptTable.Columns.Add("status", "Statut");
                mainPanel.Controls.Add(apptTable);

                // Data loading function
                void LoadDashboardData()
                {
                    // Date range
                    DateTime today = DateTime.Today;
                    DateTime start, end;
                    if (view == "weekly")
                    {
                        int diff = (7 + (today.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)today.DayOfWeek) - 1) % 7;
                        start = today.AddDays(-1 * diff).Date;
                        end = start.AddDays(6);
                    }
                    else
                    {
                        start = new DateTime(today.Year, today.Month, 1);
                        end = start.AddMonths(1).AddDays(-1);
                    }
                    // Users
                    var users = DatabaseHelper.ExecuteQuery("SELECT * FROM users");
                    var therapists = DatabaseHelper.ExecuteQuery("SELECT * FROM users WHERE role = 'therapist'");
                    var locations = DatabaseHelper.ExecuteQuery("SELECT * FROM locations");
                    // Appointments
                    var appts = DatabaseHelper.ExecuteQuery(
                        "SELECT a.*, u.name as client_name, t.name as therapist_name, l.name as location_name FROM appointments a " +
                        "JOIN users u ON a.user_id = u.id " +
                        "LEFT JOIN reports r ON r.appointment_id = a.id " +
                        "LEFT JOIN users t ON r.therapist_id = t.id " +
                        "JOIN locations l ON a.location_id = l.id " +
                        "WHERE a.date BETWEEN @start AND @end ORDER BY a.date, a.hour",
                        new[] {
                            new MySql.Data.MySqlClient.MySqlParameter("@start", start),
                            new MySql.Data.MySqlClient.MySqlParameter("@end", end)
                        }
                    );
                    // Stats cards
                    usersCard.Controls.Clear();
                    usersCard.Controls.Add(new Label { Text = "Utilisateurs", Dock = DockStyle.Top, ForeColor = System.Drawing.Color.White, Font = new System.Drawing.Font("Segoe UI", 11, System.Drawing.FontStyle.Bold), Height = 28, TextAlign = System.Drawing.ContentAlignment.MiddleLeft, Padding = new Padding(8, 8, 0, 0) });
                    usersCard.Controls.Add(new Label { Text = users.Rows.Count.ToString(), Dock = DockStyle.Fill, ForeColor = System.Drawing.Color.White, Font = new System.Drawing.Font("Segoe UI", 22, System.Drawing.FontStyle.Bold), TextAlign = System.Drawing.ContentAlignment.MiddleCenter });
                    therapistsCard.Controls.Clear();
                    therapistsCard.Controls.Add(new Label { Text = "Kinésithérapeutes", Dock = DockStyle.Top, ForeColor = System.Drawing.Color.White, Font = new System.Drawing.Font("Segoe UI", 11, System.Drawing.FontStyle.Bold), Height = 28, TextAlign = System.Drawing.ContentAlignment.MiddleLeft, Padding = new Padding(8, 8, 0, 0) });
                    therapistsCard.Controls.Add(new Label { Text = therapists.Rows.Count.ToString(), Dock = DockStyle.Fill, ForeColor = System.Drawing.Color.White, Font = new System.Drawing.Font("Segoe UI", 22, System.Drawing.FontStyle.Bold), TextAlign = System.Drawing.ContentAlignment.MiddleCenter });
                    apptsCard.Controls.Clear();
                    apptsCard.Controls.Add(new Label { Text = $"RDV {(view == "weekly" ? "cette semaine" : "ce mois")}", Dock = DockStyle.Top, ForeColor = System.Drawing.Color.White, Font = new System.Drawing.Font("Segoe UI", 11, System.Drawing.FontStyle.Bold), Height = 28, TextAlign = System.Drawing.ContentAlignment.MiddleLeft, Padding = new Padding(8, 8, 0, 0) });
                    apptsCard.Controls.Add(new Label { Text = appts.Rows.Count.ToString(), Dock = DockStyle.Fill, ForeColor = System.Drawing.Color.White, Font = new System.Drawing.Font("Segoe UI", 22, System.Drawing.FontStyle.Bold), TextAlign = System.Drawing.ContentAlignment.MiddleCenter });
                    locationsCard.Controls.Clear();
                    locationsCard.Controls.Add(new Label { Text = "Lieux", Dock = DockStyle.Top, ForeColor = System.Drawing.Color.White, Font = new System.Drawing.Font("Segoe UI", 11, System.Drawing.FontStyle.Bold), Height = 28, TextAlign = System.Drawing.ContentAlignment.MiddleLeft, Padding = new Padding(8, 8, 0, 0) });
                    locationsCard.Controls.Add(new Label { Text = locations.Rows.Count.ToString(), Dock = DockStyle.Fill, ForeColor = System.Drawing.Color.White, Font = new System.Drawing.Font("Segoe UI", 22, System.Drawing.FontStyle.Bold), TextAlign = System.Drawing.ContentAlignment.MiddleCenter });
                    // Charts
                    hourlyChart.Series.Clear();
                    hourlyChart.ChartAreas.Clear();
                    hourlyChart.ChartAreas.Add(new ChartArea());
                    var hourlySeries = new Series { ChartType = SeriesChartType.Column, Color = System.Drawing.Color.FromArgb(54,162,235) };
                    for (int h = 8; h <= 20; h++) hourlySeries.Points.AddXY($"{h:00}h", 0);
                    foreach (DataRow row in appts.Rows)
                    {
                        int hour = TimeSpan.Parse(row["hour"].ToString()).Hours;
                        if (hour >= 8 && hour <= 20) hourlySeries.Points[hour - 8].YValues[0]++;
                    }
                    hourlyChart.Series.Add(hourlySeries);
                    // Weekly/Monthly chart
                    periodChart.Series.Clear();
                    periodChart.ChartAreas.Clear();
                    periodChart.ChartAreas.Add(new ChartArea());
                    var periodSeries = new Series { ChartType = SeriesChartType.Column, Color = System.Drawing.Color.FromArgb(75,192,192) };
                    if (view == "weekly")
                    {
                        string[] days = { "Lundi", "Mardi", "Mercredi", "Jeudi", "Vendredi", "Samedi", "Dimanche" };
                        foreach (var d in days) periodSeries.Points.AddXY(d, 0);
                        foreach (DataRow row in appts.Rows)
                        {
                            var date = Convert.ToDateTime(row["date"]);
                            int dow = (int)date.DayOfWeek;
                            if (dow == 0) dow = 7;
                            periodSeries.Points[dow - 1].YValues[0]++;
                        }
                    }
                    else
                    {
                        int daysInMonth = (end - start).Days + 1;
                        for (int d = 1; d <= daysInMonth; d++) periodSeries.Points.AddXY(d.ToString(), 0);
                        foreach (DataRow row in appts.Rows)
                        {
                            var date = Convert.ToDateTime(row["date"]);
                            int dom = date.Day;
                            periodSeries.Points[dom - 1].YValues[0]++;
                        }
                    }
                    periodChart.Series.Add(periodSeries);
                    // Appointments table
                    apptTable.Rows.Clear();
                    foreach (DataRow row in appts.Rows)
                    {
                        apptTable.Rows.Add(
                            Convert.ToDateTime(row["date"]).ToString("dd/MM/yyyy"),
                            TimeSpan.Parse(row["hour"].ToString()).ToString(@"hh\:mm"),
                            row["client_name"].ToString(),
                            row["therapist_name"].ToString(),
                            row["location_name"].ToString(),
                            GetStatusText(row["status"].ToString())
                        );
                    }
                }

                weeklyBtn.Click += (s, e) => { view = "weekly"; SetToggleColors(); LoadDashboardData(); };
                monthlyBtn.Click += (s, e) => { view = "monthly"; SetToggleColors(); LoadDashboardData(); };
                SetToggleColors();
                LoadDashboardData();
            }
        }
    } 