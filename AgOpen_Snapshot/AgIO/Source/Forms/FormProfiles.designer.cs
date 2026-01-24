namespace AgIO
{
    partial class FormProfiles
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.cboxOverWrite = new System.Windows.Forms.ComboBox();
            this.tboxCreateNew = new System.Windows.Forms.TextBox();
            this.btnSaveNewProfile = new System.Windows.Forms.Button();
            this.label1 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.btnSerialCancel = new System.Windows.Forms.Button();
            this.label4 = new System.Windows.Forms.Label();
            this.tboxSaveAs = new System.Windows.Forms.TextBox();
            this.btnSaveAs = new System.Windows.Forms.Button();
            this.lblCurrentProfile = new System.Windows.Forms.Label();
            this.lblLast = new System.Windows.Forms.Label();
            this.cboxChooseExisting = new System.Windows.Forms.ComboBox();
            this.label3 = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // cboxOverWrite
            // 
            this.cboxOverWrite.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cboxOverWrite.Font = new System.Drawing.Font("Tahoma", 21.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.cboxOverWrite.FormattingEnabled = true;
            this.cboxOverWrite.Location = new System.Drawing.Point(624, 106);
            this.cboxOverWrite.Name = "cboxOverWrite";
            this.cboxOverWrite.Size = new System.Drawing.Size(439, 43);
            this.cboxOverWrite.TabIndex = 212;
            this.cboxOverWrite.SelectedIndexChanged += new System.EventHandler(this.cboxOverWrite_SelectedIndexChanged);
            // 
            // tboxCreateNew
            // 
            this.tboxCreateNew.BackColor = System.Drawing.Color.White;
            this.tboxCreateNew.Font = new System.Drawing.Font("Tahoma", 18F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.tboxCreateNew.Location = new System.Drawing.Point(12, 93);
            this.tboxCreateNew.Margin = new System.Windows.Forms.Padding(4, 6, 4, 6);
            this.tboxCreateNew.Name = "tboxCreateNew";
            this.tboxCreateNew.Size = new System.Drawing.Size(439, 36);
            this.tboxCreateNew.TabIndex = 214;
            this.tboxCreateNew.Click += new System.EventHandler(this.tboxNewProfile_Click);
            this.tboxCreateNew.TextChanged += new System.EventHandler(this.tboxNewProfile_TextChanged);
            // 
            // btnSaveNewProfile
            // 
            this.btnSaveNewProfile.FlatAppearance.BorderSize = 0;
            this.btnSaveNewProfile.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnSaveNewProfile.Font = new System.Drawing.Font("Tahoma", 15.75F);
            this.btnSaveNewProfile.Image = global::AgIO.Properties.Resources.VehFileSave;
            this.btnSaveNewProfile.Location = new System.Drawing.Point(486, 65);
            this.btnSaveNewProfile.Name = "btnSaveNewProfile";
            this.btnSaveNewProfile.Size = new System.Drawing.Size(84, 75);
            this.btnSaveNewProfile.TabIndex = 215;
            this.btnSaveNewProfile.UseVisualStyleBackColor = true;
            this.btnSaveNewProfile.Click += new System.EventHandler(this.btnSaveNewProfile_Click);
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Font = new System.Drawing.Font("Tahoma", 14.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label1.Location = new System.Drawing.Point(15, 66);
            this.label1.Margin = new System.Windows.Forms.Padding(6, 0, 6, 0);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(60, 23);
            this.label1.TabIndex = 216;
            this.label1.Text = "New:";
            this.label1.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Font = new System.Drawing.Font("Tahoma", 14.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label2.Location = new System.Drawing.Point(627, 80);
            this.label2.Margin = new System.Windows.Forms.Padding(6, 0, 6, 0);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(226, 23);
            this.label2.TabIndex = 217;
            this.label2.Text = "Overwrite This Profile:";
            this.label2.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // btnSerialCancel
            // 
            this.btnSerialCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnSerialCancel.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Zoom;
            this.btnSerialCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnSerialCancel.FlatAppearance.BorderSize = 0;
            this.btnSerialCancel.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnSerialCancel.Font = new System.Drawing.Font("Tahoma", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btnSerialCancel.Image = global::AgIO.Properties.Resources.Cancel64;
            this.btnSerialCancel.Location = new System.Drawing.Point(486, 315);
            this.btnSerialCancel.Name = "btnSerialCancel";
            this.btnSerialCancel.Size = new System.Drawing.Size(84, 75);
            this.btnSerialCancel.TabIndex = 218;
            this.btnSerialCancel.UseVisualStyleBackColor = true;
            this.btnSerialCancel.Click += new System.EventHandler(this.btnSerialCancel_Click);
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Font = new System.Drawing.Font("Tahoma", 14.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label4.Location = new System.Drawing.Point(15, 162);
            this.label4.Margin = new System.Windows.Forms.Padding(6, 0, 6, 0);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(91, 23);
            this.label4.TabIndex = 221;
            this.label4.Text = "Save As:";
            this.label4.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // tboxSaveAs
            // 
            this.tboxSaveAs.BackColor = System.Drawing.Color.White;
            this.tboxSaveAs.Font = new System.Drawing.Font("Tahoma", 18F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.tboxSaveAs.Location = new System.Drawing.Point(12, 190);
            this.tboxSaveAs.Margin = new System.Windows.Forms.Padding(4, 6, 4, 6);
            this.tboxSaveAs.Name = "tboxSaveAs";
            this.tboxSaveAs.Size = new System.Drawing.Size(439, 36);
            this.tboxSaveAs.TabIndex = 220;
            this.tboxSaveAs.Click += new System.EventHandler(this.tboxSaveAs_Click);
            this.tboxSaveAs.TextChanged += new System.EventHandler(this.tboxSaveAs_TextChanged);
            // 
            // btnSaveAs
            // 
            this.btnSaveAs.FlatAppearance.BorderSize = 0;
            this.btnSaveAs.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnSaveAs.Font = new System.Drawing.Font("Tahoma", 15.75F);
            this.btnSaveAs.Image = global::AgIO.Properties.Resources.FileSaveAs;
            this.btnSaveAs.Location = new System.Drawing.Point(486, 163);
            this.btnSaveAs.Name = "btnSaveAs";
            this.btnSaveAs.Size = new System.Drawing.Size(84, 75);
            this.btnSaveAs.TabIndex = 222;
            this.btnSaveAs.UseVisualStyleBackColor = true;
            this.btnSaveAs.Click += new System.EventHandler(this.btnSaveAs_Click);
            // 
            // lblCurrentProfile
            // 
            this.lblCurrentProfile.AutoSize = true;
            this.lblCurrentProfile.Font = new System.Drawing.Font("Tahoma", 14.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblCurrentProfile.Location = new System.Drawing.Point(105, 162);
            this.lblCurrentProfile.Margin = new System.Windows.Forms.Padding(6, 0, 6, 0);
            this.lblCurrentProfile.Name = "lblCurrentProfile";
            this.lblCurrentProfile.Size = new System.Drawing.Size(150, 23);
            this.lblCurrentProfile.TabIndex = 223;
            this.lblCurrentProfile.Text = "Current Profile";
            this.lblCurrentProfile.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // lblLast
            // 
            this.lblLast.Font = new System.Drawing.Font("Tahoma", 15.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblLast.Location = new System.Drawing.Point(19, 3);
            this.lblLast.Margin = new System.Windows.Forms.Padding(6, 0, 6, 0);
            this.lblLast.Name = "lblLast";
            this.lblLast.Size = new System.Drawing.Size(551, 29);
            this.lblLast.TabIndex = 225;
            this.lblLast.Text = "Curent Profile";
            this.lblLast.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // cboxChooseExisting
            // 
            this.cboxChooseExisting.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cboxChooseExisting.Font = new System.Drawing.Font("Tahoma", 21.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.cboxChooseExisting.FormattingEnabled = true;
            this.cboxChooseExisting.Location = new System.Drawing.Point(12, 287);
            this.cboxChooseExisting.Name = "cboxChooseExisting";
            this.cboxChooseExisting.Size = new System.Drawing.Size(439, 43);
            this.cboxChooseExisting.TabIndex = 224;
            this.cboxChooseExisting.SelectedIndexChanged += new System.EventHandler(this.cboxChooseExisting_SelectedIndexChanged);
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Font = new System.Drawing.Font("Tahoma", 14.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label3.Location = new System.Drawing.Point(19, 261);
            this.label3.Margin = new System.Windows.Forms.Padding(6, 0, 6, 0);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(67, 23);
            this.label3.TabIndex = 226;
            this.label3.Text = "Open:";
            this.label3.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // FormProfiles
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.Gainsboro;
            this.ClientSize = new System.Drawing.Size(576, 394);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.lblLast);
            this.Controls.Add(this.cboxChooseExisting);
            this.Controls.Add(this.lblCurrentProfile);
            this.Controls.Add(this.btnSaveAs);
            this.Controls.Add(this.label4);
            this.Controls.Add(this.tboxSaveAs);
            this.Controls.Add(this.btnSerialCancel);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.btnSaveNewProfile);
            this.Controls.Add(this.tboxCreateNew);
            this.Controls.Add(this.cboxOverWrite);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.SizableToolWindow;
            this.Name = "FormProfiles";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "AgIO: Manage Profiles";
            this.Load += new System.EventHandler(this.FormCommSaver_Load);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion
        private System.Windows.Forms.ComboBox cboxOverWrite;
        private System.Windows.Forms.TextBox tboxCreateNew;
        private System.Windows.Forms.Button btnSaveNewProfile;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Button btnSerialCancel;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.TextBox tboxSaveAs;
        private System.Windows.Forms.Button btnSaveAs;
        private System.Windows.Forms.Label lblCurrentProfile;
        private System.Windows.Forms.Label lblLast;
        private System.Windows.Forms.ComboBox cboxChooseExisting;
        private System.Windows.Forms.Label label3;
    }
}