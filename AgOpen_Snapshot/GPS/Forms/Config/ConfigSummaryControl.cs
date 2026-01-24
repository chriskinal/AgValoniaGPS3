using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using AgOpenGPS.Core.Translations;

namespace AgOpenGPS.Forms.Config
{
    public partial class ConfigSummaryControl : UserControl
    {
        public ConfigSummaryControl()
        {
            InitializeComponent();

            labelUnits.Text = gStr.gsUnit;
            labelWidth.Text = gStr.gsWidth;
            labelSections.Text = gStr.gsSections;
            labelOffset.Text = gStr.gsOffset;
            labelOverlap.Text = gStr.gsOverlap;
            labelLookAhead.Text = gStr.gsLookAhead;
            labelNudge.Text = gStr.gsNudge;
            labelTramW.Text = gStr.gsTramWidth;
            labelWheelBase.Text = gStr.gsWheelbase;
        }

        public void UpdateSummary(FormGPS mf)
        {
            lblSumWheelbase.Text = (mf.isMetric ?
                (Properties.Settings.Default.setVehicle_wheelbase * mf.m2InchOrCm).ToString("N0") :
                (Properties.Settings.Default.setVehicle_wheelbase * mf.m2InchOrCm).ToString("N0"))
                + mf.unitsInCm;

            lblSumNumSections.Text = mf.tool.numOfSections.ToString();

            string snapDist = mf.isMetric ?
                Properties.Settings.Default.setAS_snapDistance.ToString() :
                (Properties.Settings.Default.setAS_snapDistance * mf.cm2CmOrIn).ToString("N1");

            lblNudgeDistance.Text = snapDist + mf.unitsInCm.ToString();
            lblUnits.Text = mf.isMetric ? "Metric" : "Imperial";

            lblSummaryVehicleName.Text = gStr.gsCurrent + ": " + RegistrySettings.vehicleFileName;

            lblTramWidth.Text = mf.isMetric ?
                ((Properties.Settings.Default.setTram_tramWidth).ToString() + " m") :
                ConvertMeterToFeet(Properties.Settings.Default.setTram_tramWidth);

            lblToolOffset.Text = (mf.isMetric ?
                (Properties.Settings.Default.setVehicle_toolOffset * mf.m2InchOrCm).ToString() :
                (Properties.Settings.Default.setVehicle_toolOffset * mf.m2InchOrCm).ToString("N1")) +
                mf.unitsInCm;

            lblOverlap.Text = (mf.isMetric ?
                (Properties.Settings.Default.setVehicle_toolOverlap * mf.m2InchOrCm).ToString() :
                (Properties.Settings.Default.setVehicle_toolOverlap * mf.m2InchOrCm).ToString("N1")) +
                mf.unitsInCm;

            lblLookahead.Text = Properties.Settings.Default.setVehicle_toolLookAheadOn.ToString() + " sec";
        }

        public void SetSummaryWidth(string widthText)
        {
            lblSummaryWidth.Text = widthText;
        }

        private string ConvertMeterToFeet(double meter)
        {
            double toFeet = meter * 3.28;
            string feetInch = Convert.ToString((int)toFeet) + "' ";
            double temp = Math.Round((toFeet - Math.Truncate(toFeet)) * 12, 0);
            feetInch += Convert.ToString(temp) + '"';
            return feetInch;
        }
    }
}
