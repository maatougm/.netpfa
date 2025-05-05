namespace PFAA.UI.Forms
{
    partial class BookAppointmentForm
    {
        private System.ComponentModel.IContainer components = null;
        private System.Windows.Forms.Label lblPatient;
        private System.Windows.Forms.ComboBox cbPatient;
        private System.Windows.Forms.Label lblLocation;
        private System.Windows.Forms.ComboBox cbLocation;
        private System.Windows.Forms.Label lblDate;
        private System.Windows.Forms.DateTimePicker dtpDate;
        private System.Windows.Forms.Label lblTime;
        private System.Windows.Forms.DateTimePicker dtpTime;
        private System.Windows.Forms.Button btnBook;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this.lblPatient = new System.Windows.Forms.Label();
            this.cbPatient = new System.Windows.Forms.ComboBox();
            this.lblLocation = new System.Windows.Forms.Label();
            this.cbLocation = new System.Windows.Forms.ComboBox();
            this.lblDate = new System.Windows.Forms.Label();
            this.dtpDate = new System.Windows.Forms.DateTimePicker();
            this.lblTime = new System.Windows.Forms.Label();
            this.dtpTime = new System.Windows.Forms.DateTimePicker();
            this.btnBook = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // lblPatient
            // 
            this.lblPatient.AutoSize = true;
            this.lblPatient.Location = new System.Drawing.Point(30, 30);
            this.lblPatient.Name = "lblPatient";
            this.lblPatient.Size = new System.Drawing.Size(110, 15);
            this.lblPatient.TabIndex = 0;
            this.lblPatient.Text = "Rechercher patient :";
            // 
            // cbPatient
            // 
            this.cbPatient.Location = new System.Drawing.Point(30, 50);
            this.cbPatient.Name = "cbPatient";
            this.cbPatient.Size = new System.Drawing.Size(300, 23);
            this.cbPatient.TabIndex = 1;
            this.cbPatient.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDown;
            this.cbPatient.AutoCompleteMode = System.Windows.Forms.AutoCompleteMode.SuggestAppend;
            this.cbPatient.AutoCompleteSource = System.Windows.Forms.AutoCompleteSource.ListItems;
            // 
            // lblLocation
            // 
            this.lblLocation.AutoSize = true;
            this.lblLocation.Location = new System.Drawing.Point(30, 90);
            this.lblLocation.Name = "lblLocation";
            this.lblLocation.Size = new System.Drawing.Size(34, 15);
            this.lblLocation.TabIndex = 2;
            this.lblLocation.Text = "Lieu :";
            // 
            // cbLocation
            // 
            this.cbLocation.Location = new System.Drawing.Point(30, 110);
            this.cbLocation.Name = "cbLocation";
            this.cbLocation.Size = new System.Drawing.Size(300, 23);
            this.cbLocation.TabIndex = 3;
            this.cbLocation.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            // 
            // lblDate
            // 
            this.lblDate.AutoSize = true;
            this.lblDate.Location = new System.Drawing.Point(30, 150);
            this.lblDate.Name = "lblDate";
            this.lblDate.Size = new System.Drawing.Size(37, 15);
            this.lblDate.TabIndex = 4;
            this.lblDate.Text = "Date :";
            // 
            // dtpDate
            // 
            this.dtpDate.Location = new System.Drawing.Point(30, 170);
            this.dtpDate.Name = "dtpDate";
            this.dtpDate.Size = new System.Drawing.Size(300, 23);
            this.dtpDate.TabIndex = 5;
            this.dtpDate.Format = System.Windows.Forms.DateTimePickerFormat.Short;
            // 
            // lblTime
            // 
            this.lblTime.AutoSize = true;
            this.lblTime.Location = new System.Drawing.Point(30, 210);
            this.lblTime.Name = "lblTime";
            this.lblTime.Size = new System.Drawing.Size(44, 15);
            this.lblTime.TabIndex = 6;
            this.lblTime.Text = "Heure :";
            // 
            // dtpTime
            // 
            this.dtpTime.Location = new System.Drawing.Point(30, 230);
            this.dtpTime.Name = "dtpTime";
            this.dtpTime.Size = new System.Drawing.Size(300, 23);
            this.dtpTime.TabIndex = 7;
            this.dtpTime.Format = System.Windows.Forms.DateTimePickerFormat.Time;
            this.dtpTime.ShowUpDown = true;
            // 
            // btnBook
            // 
            this.btnBook.Location = new System.Drawing.Point(30, 270);
            this.btnBook.Name = "btnBook";
            this.btnBook.Size = new System.Drawing.Size(300, 35);
            this.btnBook.TabIndex = 8;
            this.btnBook.Text = "Créer le rendez-vous";
            this.btnBook.UseVisualStyleBackColor = true;
            // 
            // BookAppointmentForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(370, 330);
            this.Controls.Add(this.btnBook);
            this.Controls.Add(this.dtpTime);
            this.Controls.Add(this.lblTime);
            this.Controls.Add(this.dtpDate);
            this.Controls.Add(this.lblDate);
            this.Controls.Add(this.cbLocation);
            this.Controls.Add(this.lblLocation);
            this.Controls.Add(this.cbPatient);
            this.Controls.Add(this.lblPatient);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "BookAppointmentForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Créer un rendez-vous";
            this.ResumeLayout(false);
            this.PerformLayout();
        }
    }
} 