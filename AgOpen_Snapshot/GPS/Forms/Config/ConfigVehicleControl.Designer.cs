namespace AgOpenGPS.Forms.Config
{
    partial class ConfigVehicleControl
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

        #region Component Designer generated code

        /// <summary> 
        /// Required method for Designer support - do not modify 
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.lblOpacityPercent = new System.Windows.Forms.Label();
            this.labelVehicleGroupBox = new System.Windows.Forms.GroupBox();
            this.rbtnHarvester = new System.Windows.Forms.RadioButton();
            this.rbtnArticulated = new System.Windows.Forms.RadioButton();
            this.rbtnTractor = new System.Windows.Forms.RadioButton();
            this.panelArticulatedBrands = new System.Windows.Forms.Panel();
            this.rbtnBrandAHolder = new System.Windows.Forms.RadioButton();
            this.rbtnBrandAAgOpenGPS = new System.Windows.Forms.RadioButton();
            this.rbtnBrandAChallenger = new System.Windows.Forms.RadioButton();
            this.rbtnBrandACase = new System.Windows.Forms.RadioButton();
            this.rbtnBrandANH = new System.Windows.Forms.RadioButton();
            this.rbtnBrandAJDeere = new System.Windows.Forms.RadioButton();
            this.labelOpacity = new System.Windows.Forms.Label();
            this.panelHarvesterBrands = new System.Windows.Forms.Panel();
            this.rbtnBrandHAgOpenGPS = new System.Windows.Forms.RadioButton();
            this.rbtnBrandHCase = new System.Windows.Forms.RadioButton();
            this.rbtnBrandHClaas = new System.Windows.Forms.RadioButton();
            this.rbtnBrandHJDeere = new System.Windows.Forms.RadioButton();
            this.rbtnBrandHNH = new System.Windows.Forms.RadioButton();
            this.panelTractorBrands = new System.Windows.Forms.Panel();
            this.rbtnBrandTJCB = new System.Windows.Forms.RadioButton();
            this.rbtnBrandTAgOpenGPS = new System.Windows.Forms.RadioButton();
            this.rbtnBrandTCase = new System.Windows.Forms.RadioButton();
            this.rbtnBrandTClaas = new System.Windows.Forms.RadioButton();
            this.rbtnBrandTDeutz = new System.Windows.Forms.RadioButton();
            this.rbtnBrandTFendt = new System.Windows.Forms.RadioButton();
            this.rbtnBrandTJDeere = new System.Windows.Forms.RadioButton();
            this.rbtnBrandTKubota = new System.Windows.Forms.RadioButton();
            this.rbtnBrandTMassey = new System.Windows.Forms.RadioButton();
            this.rbtnBrandTNH = new System.Windows.Forms.RadioButton();
            this.rbtnBrandTSame = new System.Windows.Forms.RadioButton();
            this.rbtnBrandTSteyr = new System.Windows.Forms.RadioButton();
            this.rbtnBrandTValtra = new System.Windows.Forms.RadioButton();
            this.rbtnBrandTUrsus = new System.Windows.Forms.RadioButton();
            this.btnOpacityDn = new System.Windows.Forms.Button();
            this.panelOpacity = new System.Windows.Forms.Panel();
            this.pboxAlpha = new System.Windows.Forms.PictureBox();
            this.btnOpacityUp = new System.Windows.Forms.Button();
            this.cboxIsImage = new System.Windows.Forms.CheckBox();
            this.labelImage = new System.Windows.Forms.Label();
            this.labelVehicleGroupBox.SuspendLayout();
            this.panelArticulatedBrands.SuspendLayout();
            this.panelHarvesterBrands.SuspendLayout();
            this.panelTractorBrands.SuspendLayout();
            this.panelOpacity.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.pboxAlpha)).BeginInit();
            this.SuspendLayout();
            // 
            // lblOpacityPercent
            // 
            this.lblOpacityPercent.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.lblOpacityPercent.Font = new System.Drawing.Font("Tahoma", 27.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblOpacityPercent.ForeColor = System.Drawing.Color.Black;
            this.lblOpacityPercent.Location = new System.Drawing.Point(631, 453);
            this.lblOpacityPercent.Name = "lblOpacityPercent";
            this.lblOpacityPercent.Size = new System.Drawing.Size(139, 45);
            this.lblOpacityPercent.TabIndex = 541;
            this.lblOpacityPercent.Text = "100%";
            this.lblOpacityPercent.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // labelVehicleGroupBox
            // 
            this.labelVehicleGroupBox.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.labelVehicleGroupBox.BackColor = System.Drawing.Color.Transparent;
            this.labelVehicleGroupBox.BackgroundImageLayout = System.Windows.Forms.ImageLayout.None;
            this.labelVehicleGroupBox.Controls.Add(this.rbtnHarvester);
            this.labelVehicleGroupBox.Controls.Add(this.rbtnArticulated);
            this.labelVehicleGroupBox.Controls.Add(this.rbtnTractor);
            this.labelVehicleGroupBox.ForeColor = System.Drawing.Color.Black;
            this.labelVehicleGroupBox.Location = new System.Drawing.Point(24, -9);
            this.labelVehicleGroupBox.Name = "labelVehicleGroupBox";
            this.labelVehicleGroupBox.Size = new System.Drawing.Size(516, 122);
            this.labelVehicleGroupBox.TabIndex = 536;
            this.labelVehicleGroupBox.TabStop = false;
            this.labelVehicleGroupBox.Text = "Choose Vehicle Type";
            // 
            // rbtnHarvester
            // 
            this.rbtnHarvester.Appearance = System.Windows.Forms.Appearance.Button;
            this.rbtnHarvester.BackgroundImage = global::AgOpenGPS.Properties.Resources.vehiclePageHarvester;
            this.rbtnHarvester.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Zoom;
            this.rbtnHarvester.CheckAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.rbtnHarvester.FlatAppearance.BorderSize = 0;
            this.rbtnHarvester.FlatAppearance.CheckedBackColor = System.Drawing.Color.LightGreen;
            this.rbtnHarvester.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.rbtnHarvester.Location = new System.Drawing.Point(16, 27);
            this.rbtnHarvester.Name = "rbtnHarvester";
            this.rbtnHarvester.Size = new System.Drawing.Size(127, 83);
            this.rbtnHarvester.TabIndex = 253;
            this.rbtnHarvester.UseVisualStyleBackColor = true;
            this.rbtnHarvester.Click += new System.EventHandler(this.rbtnHarvester_Click);
            // 
            // rbtnArticulated
            // 
            this.rbtnArticulated.Appearance = System.Windows.Forms.Appearance.Button;
            this.rbtnArticulated.BackgroundImage = global::AgOpenGPS.Properties.Resources.vehiclePageArticulated;
            this.rbtnArticulated.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Zoom;
            this.rbtnArticulated.CheckAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.rbtnArticulated.FlatAppearance.BorderSize = 0;
            this.rbtnArticulated.FlatAppearance.CheckedBackColor = System.Drawing.Color.LightGreen;
            this.rbtnArticulated.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.rbtnArticulated.Location = new System.Drawing.Point(366, 27);
            this.rbtnArticulated.Name = "rbtnArticulated";
            this.rbtnArticulated.Size = new System.Drawing.Size(127, 83);
            this.rbtnArticulated.TabIndex = 252;
            this.rbtnArticulated.UseVisualStyleBackColor = true;
            this.rbtnArticulated.Click += new System.EventHandler(this.rbtnArticulated_Click);
            // 
            // rbtnTractor
            // 
            this.rbtnTractor.Appearance = System.Windows.Forms.Appearance.Button;
            this.rbtnTractor.BackgroundImage = global::AgOpenGPS.Properties.Resources.vehiclePageTractor;
            this.rbtnTractor.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Zoom;
            this.rbtnTractor.CheckAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.rbtnTractor.Checked = true;
            this.rbtnTractor.FlatAppearance.BorderSize = 0;
            this.rbtnTractor.FlatAppearance.CheckedBackColor = System.Drawing.Color.LightGreen;
            this.rbtnTractor.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.rbtnTractor.Location = new System.Drawing.Point(192, 27);
            this.rbtnTractor.Name = "rbtnTractor";
            this.rbtnTractor.Size = new System.Drawing.Size(127, 83);
            this.rbtnTractor.TabIndex = 112;
            this.rbtnTractor.TabStop = true;
            this.rbtnTractor.UseVisualStyleBackColor = true;
            this.rbtnTractor.Click += new System.EventHandler(this.rbtnTractor_Click);
            // 
            // panelArticulatedBrands
            // 
            this.panelArticulatedBrands.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.panelArticulatedBrands.Controls.Add(this.rbtnBrandAHolder);
            this.panelArticulatedBrands.Controls.Add(this.rbtnBrandAAgOpenGPS);
            this.panelArticulatedBrands.Controls.Add(this.rbtnBrandAChallenger);
            this.panelArticulatedBrands.Controls.Add(this.rbtnBrandACase);
            this.panelArticulatedBrands.Controls.Add(this.rbtnBrandANH);
            this.panelArticulatedBrands.Controls.Add(this.rbtnBrandAJDeere);
            this.panelArticulatedBrands.Location = new System.Drawing.Point(412, 115);
            this.panelArticulatedBrands.Name = "panelArticulatedBrands";
            this.panelArticulatedBrands.Size = new System.Drawing.Size(80, 467);
            this.panelArticulatedBrands.TabIndex = 539;
            // 
            // rbtnBrandAHolder
            // 
            this.rbtnBrandAHolder.Appearance = System.Windows.Forms.Appearance.Button;
            this.rbtnBrandAHolder.BackgroundImageLayout = System.Windows.Forms.ImageLayout.None;
            this.rbtnBrandAHolder.CheckAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.rbtnBrandAHolder.Checked = true;
            this.rbtnBrandAHolder.FlatAppearance.BorderColor = System.Drawing.Color.Silver;
            this.rbtnBrandAHolder.FlatAppearance.CheckedBackColor = System.Drawing.Color.LightGreen;
            this.rbtnBrandAHolder.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.rbtnBrandAHolder.Image = global::AgOpenGPS.ResourcesBrands.BrandImages.BrandHolder;
            this.rbtnBrandAHolder.Location = new System.Drawing.Point(8, 395);
            this.rbtnBrandAHolder.Name = "rbtnBrandAHolder";
            this.rbtnBrandAHolder.Size = new System.Drawing.Size(64, 64);
            this.rbtnBrandAHolder.TabIndex = 136;
            this.rbtnBrandAHolder.TabStop = true;
            this.rbtnBrandAHolder.Tag = AgOpenGPS.Core.Models.ArticulatedBrand.Holder;
            this.rbtnBrandAHolder.UseVisualStyleBackColor = true;
            this.rbtnBrandAHolder.CheckedChanged += new System.EventHandler(this.rbtnArticulatedBrand_CheckedChanged);
            // 
            // rbtnBrandAAgOpenGPS
            // 
            this.rbtnBrandAAgOpenGPS.Appearance = System.Windows.Forms.Appearance.Button;
            this.rbtnBrandAAgOpenGPS.BackgroundImageLayout = System.Windows.Forms.ImageLayout.None;
            this.rbtnBrandAAgOpenGPS.CheckAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.rbtnBrandAAgOpenGPS.FlatAppearance.BorderColor = System.Drawing.Color.Silver;
            this.rbtnBrandAAgOpenGPS.FlatAppearance.CheckedBackColor = System.Drawing.Color.LightGreen;
            this.rbtnBrandAAgOpenGPS.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.rbtnBrandAAgOpenGPS.Image = global::AgOpenGPS.ResourcesBrands.BrandImages.BrandAoG;
            this.rbtnBrandAAgOpenGPS.Location = new System.Drawing.Point(8, 6);
            this.rbtnBrandAAgOpenGPS.Name = "rbtnBrandAAgOpenGPS";
            this.rbtnBrandAAgOpenGPS.Size = new System.Drawing.Size(64, 64);
            this.rbtnBrandAAgOpenGPS.TabIndex = 131;
            this.rbtnBrandAAgOpenGPS.Tag = AgOpenGPS.Core.Models.ArticulatedBrand.AgOpenGPS;
            this.rbtnBrandAAgOpenGPS.UseVisualStyleBackColor = true;
            this.rbtnBrandAAgOpenGPS.CheckedChanged += new System.EventHandler(this.rbtnArticulatedBrand_CheckedChanged);
            // 
            // rbtnBrandAChallenger
            // 
            this.rbtnBrandAChallenger.Appearance = System.Windows.Forms.Appearance.Button;
            this.rbtnBrandAChallenger.BackgroundImageLayout = System.Windows.Forms.ImageLayout.None;
            this.rbtnBrandAChallenger.CheckAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.rbtnBrandAChallenger.Checked = true;
            this.rbtnBrandAChallenger.FlatAppearance.BorderColor = System.Drawing.Color.Silver;
            this.rbtnBrandAChallenger.FlatAppearance.CheckedBackColor = System.Drawing.Color.LightGreen;
            this.rbtnBrandAChallenger.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.rbtnBrandAChallenger.Image = global::AgOpenGPS.ResourcesBrands.BrandImages.BrandChallenger;
            this.rbtnBrandAChallenger.Location = new System.Drawing.Point(8, 84);
            this.rbtnBrandAChallenger.Name = "rbtnBrandAChallenger";
            this.rbtnBrandAChallenger.Size = new System.Drawing.Size(64, 64);
            this.rbtnBrandAChallenger.TabIndex = 132;
            this.rbtnBrandAChallenger.TabStop = true;
            this.rbtnBrandAChallenger.Tag = AgOpenGPS.Core.Models.ArticulatedBrand.Challenger;
            this.rbtnBrandAChallenger.UseVisualStyleBackColor = true;
            this.rbtnBrandAChallenger.CheckedChanged += new System.EventHandler(this.rbtnArticulatedBrand_CheckedChanged);
            // 
            // rbtnBrandACase
            // 
            this.rbtnBrandACase.Appearance = System.Windows.Forms.Appearance.Button;
            this.rbtnBrandACase.BackgroundImageLayout = System.Windows.Forms.ImageLayout.None;
            this.rbtnBrandACase.CheckAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.rbtnBrandACase.Checked = true;
            this.rbtnBrandACase.FlatAppearance.BorderColor = System.Drawing.Color.Silver;
            this.rbtnBrandACase.FlatAppearance.CheckedBackColor = System.Drawing.Color.LightGreen;
            this.rbtnBrandACase.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.rbtnBrandACase.Image = global::AgOpenGPS.ResourcesBrands.BrandImages.BrandCase;
            this.rbtnBrandACase.Location = new System.Drawing.Point(8, 162);
            this.rbtnBrandACase.Name = "rbtnBrandACase";
            this.rbtnBrandACase.Size = new System.Drawing.Size(64, 64);
            this.rbtnBrandACase.TabIndex = 133;
            this.rbtnBrandACase.TabStop = true;
            this.rbtnBrandACase.Tag = AgOpenGPS.Core.Models.ArticulatedBrand.Case;
            this.rbtnBrandACase.UseVisualStyleBackColor = true;
            this.rbtnBrandACase.CheckedChanged += new System.EventHandler(this.rbtnArticulatedBrand_CheckedChanged);
            // 
            // rbtnBrandANH
            // 
            this.rbtnBrandANH.Appearance = System.Windows.Forms.Appearance.Button;
            this.rbtnBrandANH.BackgroundImageLayout = System.Windows.Forms.ImageLayout.None;
            this.rbtnBrandANH.CheckAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.rbtnBrandANH.Checked = true;
            this.rbtnBrandANH.FlatAppearance.BorderColor = System.Drawing.Color.Silver;
            this.rbtnBrandANH.FlatAppearance.CheckedBackColor = System.Drawing.Color.LightGreen;
            this.rbtnBrandANH.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.rbtnBrandANH.Image = global::AgOpenGPS.ResourcesBrands.BrandImages.BrandNewHolland;
            this.rbtnBrandANH.Location = new System.Drawing.Point(8, 318);
            this.rbtnBrandANH.Name = "rbtnBrandANH";
            this.rbtnBrandANH.Size = new System.Drawing.Size(64, 64);
            this.rbtnBrandANH.TabIndex = 135;
            this.rbtnBrandANH.TabStop = true;
            this.rbtnBrandANH.Tag = AgOpenGPS.Core.Models.ArticulatedBrand.NewHolland;
            this.rbtnBrandANH.UseVisualStyleBackColor = true;
            this.rbtnBrandANH.CheckedChanged += new System.EventHandler(this.rbtnArticulatedBrand_CheckedChanged);
            // 
            // rbtnBrandAJDeere
            // 
            this.rbtnBrandAJDeere.Appearance = System.Windows.Forms.Appearance.Button;
            this.rbtnBrandAJDeere.BackgroundImageLayout = System.Windows.Forms.ImageLayout.None;
            this.rbtnBrandAJDeere.CheckAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.rbtnBrandAJDeere.Checked = true;
            this.rbtnBrandAJDeere.FlatAppearance.BorderColor = System.Drawing.Color.Silver;
            this.rbtnBrandAJDeere.FlatAppearance.CheckedBackColor = System.Drawing.Color.LightGreen;
            this.rbtnBrandAJDeere.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.rbtnBrandAJDeere.Image = global::AgOpenGPS.ResourcesBrands.BrandImages.BrandJohnDeere;
            this.rbtnBrandAJDeere.Location = new System.Drawing.Point(8, 240);
            this.rbtnBrandAJDeere.Name = "rbtnBrandAJDeere";
            this.rbtnBrandAJDeere.Size = new System.Drawing.Size(64, 64);
            this.rbtnBrandAJDeere.TabIndex = 134;
            this.rbtnBrandAJDeere.TabStop = true;
            this.rbtnBrandAJDeere.Tag = AgOpenGPS.Core.Models.ArticulatedBrand.JohnDeere;
            this.rbtnBrandAJDeere.UseVisualStyleBackColor = true;
            this.rbtnBrandAJDeere.CheckedChanged += new System.EventHandler(this.rbtnArticulatedBrand_CheckedChanged);
            // 
            // labelOpacity
            // 
            this.labelOpacity.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.labelOpacity.AutoSize = true;
            this.labelOpacity.Font = new System.Drawing.Font("Tahoma", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.labelOpacity.ForeColor = System.Drawing.SystemColors.ActiveCaptionText;
            this.labelOpacity.Location = new System.Drawing.Point(665, 422);
            this.labelOpacity.Name = "labelOpacity";
            this.labelOpacity.Size = new System.Drawing.Size(70, 19);
            this.labelOpacity.TabIndex = 540;
            this.labelOpacity.Text = "Opacity";
            // 
            // panelHarvesterBrands
            // 
            this.panelHarvesterBrands.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.panelHarvesterBrands.Controls.Add(this.rbtnBrandHAgOpenGPS);
            this.panelHarvesterBrands.Controls.Add(this.rbtnBrandHCase);
            this.panelHarvesterBrands.Controls.Add(this.rbtnBrandHClaas);
            this.panelHarvesterBrands.Controls.Add(this.rbtnBrandHJDeere);
            this.panelHarvesterBrands.Controls.Add(this.rbtnBrandHNH);
            this.panelHarvesterBrands.Location = new System.Drawing.Point(59, 115);
            this.panelHarvesterBrands.Name = "panelHarvesterBrands";
            this.panelHarvesterBrands.Size = new System.Drawing.Size(80, 467);
            this.panelHarvesterBrands.TabIndex = 538;
            // 
            // rbtnBrandHAgOpenGPS
            // 
            this.rbtnBrandHAgOpenGPS.Appearance = System.Windows.Forms.Appearance.Button;
            this.rbtnBrandHAgOpenGPS.BackgroundImageLayout = System.Windows.Forms.ImageLayout.None;
            this.rbtnBrandHAgOpenGPS.CheckAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.rbtnBrandHAgOpenGPS.FlatAppearance.BorderColor = System.Drawing.Color.Silver;
            this.rbtnBrandHAgOpenGPS.FlatAppearance.CheckedBackColor = System.Drawing.Color.LightGreen;
            this.rbtnBrandHAgOpenGPS.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.rbtnBrandHAgOpenGPS.Image = global::AgOpenGPS.ResourcesBrands.BrandImages.BrandAoG;
            this.rbtnBrandHAgOpenGPS.Location = new System.Drawing.Point(8, 6);
            this.rbtnBrandHAgOpenGPS.Name = "rbtnBrandHAgOpenGPS";
            this.rbtnBrandHAgOpenGPS.Size = new System.Drawing.Size(64, 64);
            this.rbtnBrandHAgOpenGPS.TabIndex = 127;
            this.rbtnBrandHAgOpenGPS.Tag = AgOpenGPS.Core.Models.HarvesterBrand.AgOpenGPS;
            this.rbtnBrandHAgOpenGPS.UseVisualStyleBackColor = true;
            this.rbtnBrandHAgOpenGPS.CheckedChanged += new System.EventHandler(this.rbtnHarvesterBrand_CheckedChanged);
            // 
            // rbtnBrandHCase
            // 
            this.rbtnBrandHCase.Appearance = System.Windows.Forms.Appearance.Button;
            this.rbtnBrandHCase.BackgroundImageLayout = System.Windows.Forms.ImageLayout.None;
            this.rbtnBrandHCase.CheckAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.rbtnBrandHCase.Checked = true;
            this.rbtnBrandHCase.FlatAppearance.BorderColor = System.Drawing.Color.Silver;
            this.rbtnBrandHCase.FlatAppearance.CheckedBackColor = System.Drawing.Color.LightGreen;
            this.rbtnBrandHCase.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.rbtnBrandHCase.Image = global::AgOpenGPS.ResourcesBrands.BrandImages.BrandCase;
            this.rbtnBrandHCase.Location = new System.Drawing.Point(8, 102);
            this.rbtnBrandHCase.Name = "rbtnBrandHCase";
            this.rbtnBrandHCase.Size = new System.Drawing.Size(64, 64);
            this.rbtnBrandHCase.TabIndex = 114;
            this.rbtnBrandHCase.TabStop = true;
            this.rbtnBrandHCase.Tag = AgOpenGPS.Core.Models.HarvesterBrand.Case;
            this.rbtnBrandHCase.UseVisualStyleBackColor = true;
            this.rbtnBrandHCase.CheckedChanged += new System.EventHandler(this.rbtnHarvesterBrand_CheckedChanged);
            // 
            // rbtnBrandHClaas
            // 
            this.rbtnBrandHClaas.Appearance = System.Windows.Forms.Appearance.Button;
            this.rbtnBrandHClaas.BackgroundImageLayout = System.Windows.Forms.ImageLayout.None;
            this.rbtnBrandHClaas.CheckAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.rbtnBrandHClaas.Checked = true;
            this.rbtnBrandHClaas.FlatAppearance.BorderColor = System.Drawing.Color.Silver;
            this.rbtnBrandHClaas.FlatAppearance.CheckedBackColor = System.Drawing.Color.LightGreen;
            this.rbtnBrandHClaas.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.rbtnBrandHClaas.Image = global::AgOpenGPS.ResourcesBrands.BrandImages.BrandClaas;
            this.rbtnBrandHClaas.Location = new System.Drawing.Point(8, 198);
            this.rbtnBrandHClaas.Name = "rbtnBrandHClaas";
            this.rbtnBrandHClaas.Size = new System.Drawing.Size(64, 64);
            this.rbtnBrandHClaas.TabIndex = 116;
            this.rbtnBrandHClaas.TabStop = true;
            this.rbtnBrandHClaas.Tag = AgOpenGPS.Core.Models.HarvesterBrand.Claas;
            this.rbtnBrandHClaas.UseVisualStyleBackColor = true;
            this.rbtnBrandHClaas.CheckedChanged += new System.EventHandler(this.rbtnHarvesterBrand_CheckedChanged);
            // 
            // rbtnBrandHJDeere
            // 
            this.rbtnBrandHJDeere.Appearance = System.Windows.Forms.Appearance.Button;
            this.rbtnBrandHJDeere.BackgroundImageLayout = System.Windows.Forms.ImageLayout.None;
            this.rbtnBrandHJDeere.CheckAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.rbtnBrandHJDeere.Checked = true;
            this.rbtnBrandHJDeere.FlatAppearance.BorderColor = System.Drawing.Color.Silver;
            this.rbtnBrandHJDeere.FlatAppearance.CheckedBackColor = System.Drawing.Color.LightGreen;
            this.rbtnBrandHJDeere.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.rbtnBrandHJDeere.Image = global::AgOpenGPS.ResourcesBrands.BrandImages.BrandJohnDeere;
            this.rbtnBrandHJDeere.Location = new System.Drawing.Point(8, 294);
            this.rbtnBrandHJDeere.Name = "rbtnBrandHJDeere";
            this.rbtnBrandHJDeere.Size = new System.Drawing.Size(64, 64);
            this.rbtnBrandHJDeere.TabIndex = 129;
            this.rbtnBrandHJDeere.TabStop = true;
            this.rbtnBrandHJDeere.Tag = AgOpenGPS.Core.Models.HarvesterBrand.JohnDeere;
            this.rbtnBrandHJDeere.UseVisualStyleBackColor = true;
            this.rbtnBrandHJDeere.CheckedChanged += new System.EventHandler(this.rbtnHarvesterBrand_CheckedChanged);
            // 
            // rbtnBrandHNH
            // 
            this.rbtnBrandHNH.Appearance = System.Windows.Forms.Appearance.Button;
            this.rbtnBrandHNH.BackgroundImageLayout = System.Windows.Forms.ImageLayout.None;
            this.rbtnBrandHNH.CheckAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.rbtnBrandHNH.Checked = true;
            this.rbtnBrandHNH.FlatAppearance.BorderColor = System.Drawing.Color.Silver;
            this.rbtnBrandHNH.FlatAppearance.CheckedBackColor = System.Drawing.Color.LightGreen;
            this.rbtnBrandHNH.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.rbtnBrandHNH.Image = global::AgOpenGPS.ResourcesBrands.BrandImages.BrandNewHolland;
            this.rbtnBrandHNH.Location = new System.Drawing.Point(8, 390);
            this.rbtnBrandHNH.Name = "rbtnBrandHNH";
            this.rbtnBrandHNH.Size = new System.Drawing.Size(64, 64);
            this.rbtnBrandHNH.TabIndex = 130;
            this.rbtnBrandHNH.TabStop = true;
            this.rbtnBrandHNH.Tag = AgOpenGPS.Core.Models.HarvesterBrand.NewHolland;
            this.rbtnBrandHNH.UseVisualStyleBackColor = true;
            this.rbtnBrandHNH.CheckedChanged += new System.EventHandler(this.rbtnHarvesterBrand_CheckedChanged);
            // 
            // panelTractorBrands
            // 
            this.panelTractorBrands.AccessibleName = "";
            this.panelTractorBrands.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.panelTractorBrands.Controls.Add(this.rbtnBrandTJCB);
            this.panelTractorBrands.Controls.Add(this.rbtnBrandTAgOpenGPS);
            this.panelTractorBrands.Controls.Add(this.rbtnBrandTCase);
            this.panelTractorBrands.Controls.Add(this.rbtnBrandTClaas);
            this.panelTractorBrands.Controls.Add(this.rbtnBrandTDeutz);
            this.panelTractorBrands.Controls.Add(this.rbtnBrandTFendt);
            this.panelTractorBrands.Controls.Add(this.rbtnBrandTJDeere);
            this.panelTractorBrands.Controls.Add(this.rbtnBrandTKubota);
            this.panelTractorBrands.Controls.Add(this.rbtnBrandTMassey);
            this.panelTractorBrands.Controls.Add(this.rbtnBrandTNH);
            this.panelTractorBrands.Controls.Add(this.rbtnBrandTSame);
            this.panelTractorBrands.Controls.Add(this.rbtnBrandTSteyr);
            this.panelTractorBrands.Controls.Add(this.rbtnBrandTValtra);
            this.panelTractorBrands.Controls.Add(this.rbtnBrandTUrsus);
            this.panelTractorBrands.Location = new System.Drawing.Point(131, 115);
            this.panelTractorBrands.Name = "panelTractorBrands";
            this.panelTractorBrands.Size = new System.Drawing.Size(289, 467);
            this.panelTractorBrands.TabIndex = 537;
            // 
            // rbtnBrandTJCB
            // 
            this.rbtnBrandTJCB.Appearance = System.Windows.Forms.Appearance.Button;
            this.rbtnBrandTJCB.BackgroundImageLayout = System.Windows.Forms.ImageLayout.None;
            this.rbtnBrandTJCB.CheckAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.rbtnBrandTJCB.FlatAppearance.BorderColor = System.Drawing.Color.Silver;
            this.rbtnBrandTJCB.FlatAppearance.CheckedBackColor = System.Drawing.Color.LightGreen;
            this.rbtnBrandTJCB.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.rbtnBrandTJCB.Image = global::AgOpenGPS.ResourcesBrands.BrandImages.BrandJCB;
            this.rbtnBrandTJCB.Location = new System.Drawing.Point(13, 6);
            this.rbtnBrandTJCB.Name = "rbtnBrandTJCB";
            this.rbtnBrandTJCB.Size = new System.Drawing.Size(64, 64);
            this.rbtnBrandTJCB.TabIndex = 127;
            this.rbtnBrandTJCB.Tag = AgOpenGPS.Core.Models.TractorBrand.JCB;
            this.rbtnBrandTJCB.UseVisualStyleBackColor = true;
            this.rbtnBrandTJCB.CheckedChanged += new System.EventHandler(this.rbtnTractorBrand_CheckedChanged);
            // 
            // rbtnBrandTAgOpenGPS
            // 
            this.rbtnBrandTAgOpenGPS.Appearance = System.Windows.Forms.Appearance.Button;
            this.rbtnBrandTAgOpenGPS.BackgroundImageLayout = System.Windows.Forms.ImageLayout.None;
            this.rbtnBrandTAgOpenGPS.CheckAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.rbtnBrandTAgOpenGPS.FlatAppearance.BorderColor = System.Drawing.Color.Silver;
            this.rbtnBrandTAgOpenGPS.FlatAppearance.CheckedBackColor = System.Drawing.Color.LightGreen;
            this.rbtnBrandTAgOpenGPS.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.rbtnBrandTAgOpenGPS.Image = global::AgOpenGPS.ResourcesBrands.BrandImages.BrandAoG;
            this.rbtnBrandTAgOpenGPS.Location = new System.Drawing.Point(112, 6);
            this.rbtnBrandTAgOpenGPS.Name = "rbtnBrandTAgOpenGPS";
            this.rbtnBrandTAgOpenGPS.Size = new System.Drawing.Size(64, 64);
            this.rbtnBrandTAgOpenGPS.TabIndex = 126;
            this.rbtnBrandTAgOpenGPS.Tag = AgOpenGPS.Core.Models.TractorBrand.AGOpenGPS;
            this.rbtnBrandTAgOpenGPS.UseVisualStyleBackColor = true;
            this.rbtnBrandTAgOpenGPS.CheckedChanged += new System.EventHandler(this.rbtnTractorBrand_CheckedChanged);
            // 
            // rbtnBrandTCase
            // 
            this.rbtnBrandTCase.Appearance = System.Windows.Forms.Appearance.Button;
            this.rbtnBrandTCase.BackgroundImage = global::AgOpenGPS.ResourcesBrands.BrandImages.BrandCase;
            this.rbtnBrandTCase.BackgroundImageLayout = System.Windows.Forms.ImageLayout.None;
            this.rbtnBrandTCase.CheckAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.rbtnBrandTCase.FlatAppearance.BorderColor = System.Drawing.Color.Silver;
            this.rbtnBrandTCase.FlatAppearance.CheckedBackColor = System.Drawing.Color.LightGreen;
            this.rbtnBrandTCase.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.rbtnBrandTCase.Location = new System.Drawing.Point(13, 102);
            this.rbtnBrandTCase.Name = "rbtnBrandTCase";
            this.rbtnBrandTCase.Size = new System.Drawing.Size(64, 64);
            this.rbtnBrandTCase.TabIndex = 114;
            this.rbtnBrandTCase.Tag = AgOpenGPS.Core.Models.TractorBrand.Case;
            this.rbtnBrandTCase.UseVisualStyleBackColor = true;
            this.rbtnBrandTCase.CheckedChanged += new System.EventHandler(this.rbtnTractorBrand_CheckedChanged);
            // 
            // rbtnBrandTClaas
            // 
            this.rbtnBrandTClaas.Appearance = System.Windows.Forms.Appearance.Button;
            this.rbtnBrandTClaas.BackgroundImageLayout = System.Windows.Forms.ImageLayout.None;
            this.rbtnBrandTClaas.CheckAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.rbtnBrandTClaas.FlatAppearance.BorderColor = System.Drawing.Color.Silver;
            this.rbtnBrandTClaas.FlatAppearance.CheckedBackColor = System.Drawing.Color.LightGreen;
            this.rbtnBrandTClaas.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.rbtnBrandTClaas.Image = global::AgOpenGPS.ResourcesBrands.BrandImages.BrandClaas;
            this.rbtnBrandTClaas.Location = new System.Drawing.Point(112, 102);
            this.rbtnBrandTClaas.Name = "rbtnBrandTClaas";
            this.rbtnBrandTClaas.Size = new System.Drawing.Size(64, 64);
            this.rbtnBrandTClaas.TabIndex = 115;
            this.rbtnBrandTClaas.Tag = AgOpenGPS.Core.Models.TractorBrand.Claas;
            this.rbtnBrandTClaas.UseVisualStyleBackColor = true;
            this.rbtnBrandTClaas.CheckedChanged += new System.EventHandler(this.rbtnTractorBrand_CheckedChanged);
            // 
            // rbtnBrandTDeutz
            // 
            this.rbtnBrandTDeutz.Appearance = System.Windows.Forms.Appearance.Button;
            this.rbtnBrandTDeutz.BackgroundImageLayout = System.Windows.Forms.ImageLayout.None;
            this.rbtnBrandTDeutz.CheckAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.rbtnBrandTDeutz.FlatAppearance.BorderColor = System.Drawing.Color.Silver;
            this.rbtnBrandTDeutz.FlatAppearance.CheckedBackColor = System.Drawing.Color.LightGreen;
            this.rbtnBrandTDeutz.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.rbtnBrandTDeutz.Image = global::AgOpenGPS.ResourcesBrands.BrandImages.BrandDeutz;
            this.rbtnBrandTDeutz.Location = new System.Drawing.Point(211, 102);
            this.rbtnBrandTDeutz.Name = "rbtnBrandTDeutz";
            this.rbtnBrandTDeutz.Size = new System.Drawing.Size(64, 64);
            this.rbtnBrandTDeutz.TabIndex = 116;
            this.rbtnBrandTDeutz.Tag = AgOpenGPS.Core.Models.TractorBrand.Deutz;
            this.rbtnBrandTDeutz.UseVisualStyleBackColor = true;
            this.rbtnBrandTDeutz.CheckedChanged += new System.EventHandler(this.rbtnTractorBrand_CheckedChanged);
            // 
            // rbtnBrandTFendt
            // 
            this.rbtnBrandTFendt.Appearance = System.Windows.Forms.Appearance.Button;
            this.rbtnBrandTFendt.BackgroundImageLayout = System.Windows.Forms.ImageLayout.None;
            this.rbtnBrandTFendt.CheckAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.rbtnBrandTFendt.FlatAppearance.BorderColor = System.Drawing.Color.Silver;
            this.rbtnBrandTFendt.FlatAppearance.CheckedBackColor = System.Drawing.Color.LightGreen;
            this.rbtnBrandTFendt.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.rbtnBrandTFendt.Image = global::AgOpenGPS.ResourcesBrands.BrandImages.BrandFendt;
            this.rbtnBrandTFendt.Location = new System.Drawing.Point(13, 390);
            this.rbtnBrandTFendt.Name = "rbtnBrandTFendt";
            this.rbtnBrandTFendt.Size = new System.Drawing.Size(64, 64);
            this.rbtnBrandTFendt.TabIndex = 117;
            this.rbtnBrandTFendt.Tag = AgOpenGPS.Core.Models.TractorBrand.Fendt;
            this.rbtnBrandTFendt.UseVisualStyleBackColor = true;
            this.rbtnBrandTFendt.CheckedChanged += new System.EventHandler(this.rbtnTractorBrand_CheckedChanged);
            // 
            // rbtnBrandTJDeere
            // 
            this.rbtnBrandTJDeere.Appearance = System.Windows.Forms.Appearance.Button;
            this.rbtnBrandTJDeere.BackgroundImageLayout = System.Windows.Forms.ImageLayout.None;
            this.rbtnBrandTJDeere.CheckAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.rbtnBrandTJDeere.FlatAppearance.BorderColor = System.Drawing.Color.Silver;
            this.rbtnBrandTJDeere.FlatAppearance.CheckedBackColor = System.Drawing.Color.LightGreen;
            this.rbtnBrandTJDeere.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.rbtnBrandTJDeere.Image = global::AgOpenGPS.ResourcesBrands.BrandImages.BrandJohnDeere;
            this.rbtnBrandTJDeere.Location = new System.Drawing.Point(211, 294);
            this.rbtnBrandTJDeere.Name = "rbtnBrandTJDeere";
            this.rbtnBrandTJDeere.Size = new System.Drawing.Size(64, 64);
            this.rbtnBrandTJDeere.TabIndex = 118;
            this.rbtnBrandTJDeere.Tag = AgOpenGPS.Core.Models.TractorBrand.JohnDeere;
            this.rbtnBrandTJDeere.UseVisualStyleBackColor = true;
            this.rbtnBrandTJDeere.CheckedChanged += new System.EventHandler(this.rbtnTractorBrand_CheckedChanged);
            // 
            // rbtnBrandTKubota
            // 
            this.rbtnBrandTKubota.Appearance = System.Windows.Forms.Appearance.Button;
            this.rbtnBrandTKubota.BackgroundImageLayout = System.Windows.Forms.ImageLayout.None;
            this.rbtnBrandTKubota.CheckAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.rbtnBrandTKubota.FlatAppearance.BorderColor = System.Drawing.Color.Silver;
            this.rbtnBrandTKubota.FlatAppearance.CheckedBackColor = System.Drawing.Color.LightGreen;
            this.rbtnBrandTKubota.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.rbtnBrandTKubota.Image = global::AgOpenGPS.ResourcesBrands.BrandImages.BrandKubota;
            this.rbtnBrandTKubota.Location = new System.Drawing.Point(211, 390);
            this.rbtnBrandTKubota.Name = "rbtnBrandTKubota";
            this.rbtnBrandTKubota.Size = new System.Drawing.Size(64, 64);
            this.rbtnBrandTKubota.TabIndex = 119;
            this.rbtnBrandTKubota.Tag = AgOpenGPS.Core.Models.TractorBrand.Kubota;
            this.rbtnBrandTKubota.UseVisualStyleBackColor = true;
            this.rbtnBrandTKubota.CheckedChanged += new System.EventHandler(this.rbtnTractorBrand_CheckedChanged);
            // 
            // rbtnBrandTMassey
            // 
            this.rbtnBrandTMassey.Appearance = System.Windows.Forms.Appearance.Button;
            this.rbtnBrandTMassey.BackgroundImageLayout = System.Windows.Forms.ImageLayout.None;
            this.rbtnBrandTMassey.CheckAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.rbtnBrandTMassey.FlatAppearance.BorderColor = System.Drawing.Color.Silver;
            this.rbtnBrandTMassey.FlatAppearance.CheckedBackColor = System.Drawing.Color.LightGreen;
            this.rbtnBrandTMassey.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.rbtnBrandTMassey.Image = global::AgOpenGPS.ResourcesBrands.BrandImages.BrandMassey;
            this.rbtnBrandTMassey.Location = new System.Drawing.Point(13, 198);
            this.rbtnBrandTMassey.Name = "rbtnBrandTMassey";
            this.rbtnBrandTMassey.Size = new System.Drawing.Size(64, 64);
            this.rbtnBrandTMassey.TabIndex = 120;
            this.rbtnBrandTMassey.Tag = AgOpenGPS.Core.Models.TractorBrand.Massey;
            this.rbtnBrandTMassey.UseVisualStyleBackColor = true;
            this.rbtnBrandTMassey.CheckedChanged += new System.EventHandler(this.rbtnTractorBrand_CheckedChanged);
            // 
            // rbtnBrandTNH
            // 
            this.rbtnBrandTNH.Appearance = System.Windows.Forms.Appearance.Button;
            this.rbtnBrandTNH.BackgroundImageLayout = System.Windows.Forms.ImageLayout.None;
            this.rbtnBrandTNH.CheckAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.rbtnBrandTNH.FlatAppearance.BorderColor = System.Drawing.Color.Silver;
            this.rbtnBrandTNH.FlatAppearance.CheckedBackColor = System.Drawing.Color.LightGreen;
            this.rbtnBrandTNH.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.rbtnBrandTNH.Image = global::AgOpenGPS.ResourcesBrands.BrandImages.BrandNewHolland;
            this.rbtnBrandTNH.Location = new System.Drawing.Point(112, 198);
            this.rbtnBrandTNH.Name = "rbtnBrandTNH";
            this.rbtnBrandTNH.Size = new System.Drawing.Size(64, 64);
            this.rbtnBrandTNH.TabIndex = 121;
            this.rbtnBrandTNH.Tag = AgOpenGPS.Core.Models.TractorBrand.NewHolland;
            this.rbtnBrandTNH.UseVisualStyleBackColor = true;
            this.rbtnBrandTNH.CheckedChanged += new System.EventHandler(this.rbtnTractorBrand_CheckedChanged);
            // 
            // rbtnBrandTSame
            // 
            this.rbtnBrandTSame.Appearance = System.Windows.Forms.Appearance.Button;
            this.rbtnBrandTSame.BackgroundImageLayout = System.Windows.Forms.ImageLayout.None;
            this.rbtnBrandTSame.CheckAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.rbtnBrandTSame.FlatAppearance.BorderColor = System.Drawing.Color.Silver;
            this.rbtnBrandTSame.FlatAppearance.CheckedBackColor = System.Drawing.Color.LightGreen;
            this.rbtnBrandTSame.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.rbtnBrandTSame.Image = global::AgOpenGPS.ResourcesBrands.BrandImages.BrandSame;
            this.rbtnBrandTSame.Location = new System.Drawing.Point(211, 198);
            this.rbtnBrandTSame.Name = "rbtnBrandTSame";
            this.rbtnBrandTSame.Size = new System.Drawing.Size(64, 64);
            this.rbtnBrandTSame.TabIndex = 122;
            this.rbtnBrandTSame.Tag = AgOpenGPS.Core.Models.TractorBrand.Same;
            this.rbtnBrandTSame.UseVisualStyleBackColor = true;
            this.rbtnBrandTSame.CheckedChanged += new System.EventHandler(this.rbtnTractorBrand_CheckedChanged);
            // 
            // rbtnBrandTSteyr
            // 
            this.rbtnBrandTSteyr.Appearance = System.Windows.Forms.Appearance.Button;
            this.rbtnBrandTSteyr.BackgroundImageLayout = System.Windows.Forms.ImageLayout.None;
            this.rbtnBrandTSteyr.CheckAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.rbtnBrandTSteyr.FlatAppearance.BorderColor = System.Drawing.Color.Silver;
            this.rbtnBrandTSteyr.FlatAppearance.CheckedBackColor = System.Drawing.Color.LightGreen;
            this.rbtnBrandTSteyr.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.rbtnBrandTSteyr.Image = global::AgOpenGPS.ResourcesBrands.BrandImages.BrandSteyr;
            this.rbtnBrandTSteyr.Location = new System.Drawing.Point(112, 390);
            this.rbtnBrandTSteyr.Name = "rbtnBrandTSteyr";
            this.rbtnBrandTSteyr.Size = new System.Drawing.Size(64, 64);
            this.rbtnBrandTSteyr.TabIndex = 123;
            this.rbtnBrandTSteyr.Tag = AgOpenGPS.Core.Models.TractorBrand.Steyr;
            this.rbtnBrandTSteyr.UseVisualStyleBackColor = true;
            this.rbtnBrandTSteyr.CheckedChanged += new System.EventHandler(this.rbtnTractorBrand_CheckedChanged);
            // 
            // rbtnBrandTValtra
            // 
            this.rbtnBrandTValtra.Appearance = System.Windows.Forms.Appearance.Button;
            this.rbtnBrandTValtra.BackgroundImageLayout = System.Windows.Forms.ImageLayout.None;
            this.rbtnBrandTValtra.CheckAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.rbtnBrandTValtra.FlatAppearance.BorderColor = System.Drawing.Color.Silver;
            this.rbtnBrandTValtra.FlatAppearance.CheckedBackColor = System.Drawing.Color.LightGreen;
            this.rbtnBrandTValtra.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.rbtnBrandTValtra.Image = global::AgOpenGPS.ResourcesBrands.BrandImages.BrandValtra;
            this.rbtnBrandTValtra.Location = new System.Drawing.Point(112, 294);
            this.rbtnBrandTValtra.Name = "rbtnBrandTValtra";
            this.rbtnBrandTValtra.Size = new System.Drawing.Size(64, 64);
            this.rbtnBrandTValtra.TabIndex = 125;
            this.rbtnBrandTValtra.Tag = AgOpenGPS.Core.Models.TractorBrand.Valtra;
            this.rbtnBrandTValtra.UseVisualStyleBackColor = true;
            this.rbtnBrandTValtra.CheckedChanged += new System.EventHandler(this.rbtnTractorBrand_CheckedChanged);
            // 
            // rbtnBrandTUrsus
            // 
            this.rbtnBrandTUrsus.Appearance = System.Windows.Forms.Appearance.Button;
            this.rbtnBrandTUrsus.BackgroundImageLayout = System.Windows.Forms.ImageLayout.None;
            this.rbtnBrandTUrsus.CheckAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.rbtnBrandTUrsus.FlatAppearance.BorderColor = System.Drawing.Color.Silver;
            this.rbtnBrandTUrsus.FlatAppearance.CheckedBackColor = System.Drawing.Color.LightGreen;
            this.rbtnBrandTUrsus.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.rbtnBrandTUrsus.Image = global::AgOpenGPS.ResourcesBrands.BrandImages.BrandUrsus;
            this.rbtnBrandTUrsus.Location = new System.Drawing.Point(13, 294);
            this.rbtnBrandTUrsus.Name = "rbtnBrandTUrsus";
            this.rbtnBrandTUrsus.Size = new System.Drawing.Size(64, 64);
            this.rbtnBrandTUrsus.TabIndex = 124;
            this.rbtnBrandTUrsus.Tag = AgOpenGPS.Core.Models.TractorBrand.Ursus;
            this.rbtnBrandTUrsus.UseVisualStyleBackColor = true;
            this.rbtnBrandTUrsus.CheckedChanged += new System.EventHandler(this.rbtnTractorBrand_CheckedChanged);
            // 
            // btnOpacityDn
            // 
            this.btnOpacityDn.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnOpacityDn.BackColor = System.Drawing.Color.Transparent;
            this.btnOpacityDn.FlatAppearance.BorderColor = System.Drawing.Color.Silver;
            this.btnOpacityDn.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnOpacityDn.Image = global::AgOpenGPS.Properties.Resources.DnArrow64;
            this.btnOpacityDn.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            this.btnOpacityDn.Location = new System.Drawing.Point(555, 438);
            this.btnOpacityDn.Name = "btnOpacityDn";
            this.btnOpacityDn.Size = new System.Drawing.Size(73, 72);
            this.btnOpacityDn.TabIndex = 544;
            this.btnOpacityDn.UseVisualStyleBackColor = false;
            this.btnOpacityDn.Click += new System.EventHandler(this.btnOpacityDn_Click);
            // 
            // panelOpacity
            // 
            this.panelOpacity.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.panelOpacity.BackColor = System.Drawing.Color.MistyRose;
            this.panelOpacity.BackgroundImage = global::AgOpenGPS.Properties.Resources.VehicleOpacity;
            this.panelOpacity.Controls.Add(this.pboxAlpha);
            this.panelOpacity.Location = new System.Drawing.Point(571, 144);
            this.panelOpacity.Name = "panelOpacity";
            this.panelOpacity.Size = new System.Drawing.Size(256, 256);
            this.panelOpacity.TabIndex = 545;
            // 
            // pboxAlpha
            // 
            this.pboxAlpha.BackColor = System.Drawing.Color.Transparent;
            this.pboxAlpha.BackgroundImage = global::AgOpenGPS.ResourcesBrands.BrandImages.TractorDeutz;
            this.pboxAlpha.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Zoom;
            this.pboxAlpha.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pboxAlpha.Location = new System.Drawing.Point(0, 0);
            this.pboxAlpha.Name = "pboxAlpha";
            this.pboxAlpha.Size = new System.Drawing.Size(256, 256);
            this.pboxAlpha.TabIndex = 484;
            this.pboxAlpha.TabStop = false;
            // 
            // btnOpacityUp
            // 
            this.btnOpacityUp.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnOpacityUp.BackColor = System.Drawing.Color.Transparent;
            this.btnOpacityUp.FlatAppearance.BorderColor = System.Drawing.Color.Silver;
            this.btnOpacityUp.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnOpacityUp.Image = global::AgOpenGPS.Properties.Resources.UpArrow64;
            this.btnOpacityUp.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            this.btnOpacityUp.Location = new System.Drawing.Point(773, 438);
            this.btnOpacityUp.Name = "btnOpacityUp";
            this.btnOpacityUp.Size = new System.Drawing.Size(73, 72);
            this.btnOpacityUp.TabIndex = 543;
            this.btnOpacityUp.UseVisualStyleBackColor = false;
            this.btnOpacityUp.Click += new System.EventHandler(this.btnOpacityUp_Click);
            // 
            // cboxIsImage
            // 
            this.cboxIsImage.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.cboxIsImage.Appearance = System.Windows.Forms.Appearance.Button;
            this.cboxIsImage.BackColor = System.Drawing.Color.Transparent;
            this.cboxIsImage.FlatAppearance.BorderColor = System.Drawing.Color.Silver;
            this.cboxIsImage.FlatAppearance.CheckedBackColor = System.Drawing.Color.LightGreen;
            this.cboxIsImage.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.cboxIsImage.Font = new System.Drawing.Font("Tahoma", 14.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.cboxIsImage.ForeColor = System.Drawing.SystemColors.ControlText;
            this.cboxIsImage.Image = global::AgOpenGPS.ResourcesBrands.BrandImages.BrandTriangleVehicle;
            this.cboxIsImage.Location = new System.Drawing.Point(651, 17);
            this.cboxIsImage.Name = "cboxIsImage";
            this.cboxIsImage.RightToLeft = System.Windows.Forms.RightToLeft.Yes;
            this.cboxIsImage.Size = new System.Drawing.Size(96, 96);
            this.cboxIsImage.TabIndex = 542;
            this.cboxIsImage.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.cboxIsImage.UseVisualStyleBackColor = false;
            this.cboxIsImage.Click += new System.EventHandler(this.cboxIsImage_Click);
            // 
            // labelImage
            // 
            this.labelImage.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.labelImage.AutoSize = true;
            this.labelImage.Font = new System.Drawing.Font("Tahoma", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.labelImage.ForeColor = System.Drawing.SystemColors.ActiveCaptionText;
            this.labelImage.Location = new System.Drawing.Point(653, -9);
            this.labelImage.Name = "labelImage";
            this.labelImage.Size = new System.Drawing.Size(88, 19);
            this.labelImage.TabIndex = 546;
            this.labelImage.Text = "No Image";
            // 
            // ConfigVehicleControl
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.WhiteSmoke;
            this.Controls.Add(this.lblOpacityPercent);
            this.Controls.Add(this.labelVehicleGroupBox);
            this.Controls.Add(this.panelArticulatedBrands);
            this.Controls.Add(this.labelOpacity);
            this.Controls.Add(this.panelHarvesterBrands);
            this.Controls.Add(this.panelTractorBrands);
            this.Controls.Add(this.labelImage);
            this.Controls.Add(this.btnOpacityDn);
            this.Controls.Add(this.panelOpacity);
            this.Controls.Add(this.btnOpacityUp);
            this.Controls.Add(this.cboxIsImage);
            this.Font = new System.Drawing.Font("Tahoma", 9.75F);
            this.Name = "ConfigVehicleControl";
            this.Size = new System.Drawing.Size(859, 584);
            this.labelVehicleGroupBox.ResumeLayout(false);
            this.panelArticulatedBrands.ResumeLayout(false);
            this.panelHarvesterBrands.ResumeLayout(false);
            this.panelTractorBrands.ResumeLayout(false);
            this.panelOpacity.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.pboxAlpha)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label lblOpacityPercent;
        private System.Windows.Forms.GroupBox labelVehicleGroupBox;
        private System.Windows.Forms.RadioButton rbtnHarvester;
        private System.Windows.Forms.RadioButton rbtnArticulated;
        private System.Windows.Forms.RadioButton rbtnTractor;
        private System.Windows.Forms.Panel panelArticulatedBrands;
        private System.Windows.Forms.RadioButton rbtnBrandAHolder;
        private System.Windows.Forms.RadioButton rbtnBrandAAgOpenGPS;
        private System.Windows.Forms.RadioButton rbtnBrandAChallenger;
        private System.Windows.Forms.RadioButton rbtnBrandACase;
        private System.Windows.Forms.RadioButton rbtnBrandANH;
        private System.Windows.Forms.RadioButton rbtnBrandAJDeere;
        private System.Windows.Forms.Label labelOpacity;
        private System.Windows.Forms.Panel panelHarvesterBrands;
        private System.Windows.Forms.RadioButton rbtnBrandHAgOpenGPS;
        private System.Windows.Forms.RadioButton rbtnBrandHCase;
        private System.Windows.Forms.RadioButton rbtnBrandHClaas;
        private System.Windows.Forms.RadioButton rbtnBrandHJDeere;
        private System.Windows.Forms.RadioButton rbtnBrandHNH;
        private System.Windows.Forms.Panel panelTractorBrands;
        private System.Windows.Forms.RadioButton rbtnBrandTJCB;
        private System.Windows.Forms.RadioButton rbtnBrandTAgOpenGPS;
        private System.Windows.Forms.RadioButton rbtnBrandTCase;
        private System.Windows.Forms.RadioButton rbtnBrandTClaas;
        private System.Windows.Forms.RadioButton rbtnBrandTDeutz;
        private System.Windows.Forms.RadioButton rbtnBrandTFendt;
        private System.Windows.Forms.RadioButton rbtnBrandTJDeere;
        private System.Windows.Forms.RadioButton rbtnBrandTKubota;
        private System.Windows.Forms.RadioButton rbtnBrandTMassey;
        private System.Windows.Forms.RadioButton rbtnBrandTNH;
        private System.Windows.Forms.RadioButton rbtnBrandTSame;
        private System.Windows.Forms.RadioButton rbtnBrandTSteyr;
        private System.Windows.Forms.RadioButton rbtnBrandTValtra;
        private System.Windows.Forms.RadioButton rbtnBrandTUrsus;
        private System.Windows.Forms.Button btnOpacityDn;
        private System.Windows.Forms.Panel panelOpacity;
        private System.Windows.Forms.PictureBox pboxAlpha;
        private System.Windows.Forms.Button btnOpacityUp;
        private System.Windows.Forms.CheckBox cboxIsImage;
        private System.Windows.Forms.Label labelImage;
    }
}
