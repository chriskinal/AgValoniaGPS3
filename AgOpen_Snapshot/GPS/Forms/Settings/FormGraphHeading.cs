using System;
using System.Globalization;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;
using AgOpenGPS.Helpers;

namespace AgOpenGPS
{
    public partial class FormGraphHeading : Form
    {
        private readonly FormGPS mf = null;

        //chart data
        private string dataSteerAngle = "0";

        private string dataPWM = "-1";

        private string roll = "1";
        private string zero = "0";

        private bool isScroll = true;

        public FormGraphHeading(Form callingForm)
        {
            mf = callingForm as FormGPS;
            InitializeComponent();
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            DrawChart();
        }

        private void DrawChart()
        {
            {
                dataSteerAngle = (glm.toDegrees(mf.gpsHeading)).ToString("N1", CultureInfo.InvariantCulture);
                dataPWM = (glm.toDegrees(mf.imuCorrected)).ToString("N1", CultureInfo.InvariantCulture);

                lblSteerAng.Text = dataSteerAngle;
                lblPWM.Text = dataPWM;

                lblDiff.Text = (glm.toDegrees(mf.gpsHeading - mf.imuCorrected)).ToString("N2", CultureInfo.InvariantCulture);

                roll = lblDiff.Text;
                zero = "0";
            }

            if (isScroll)
            {
                //chart data
                Series s = unoChart.Series["S"];
                Series w = unoChart.Series["PWM"];
                Series r = rollChart.Series["Ro"];
                Series t = rollChart.Series["Ze"];

                double nextX = 1;
                double nextX5 = 1;
                double nextx6 = 1;
                double nextx7 = 1;

                if (s.Points.Count > 0) nextX = s.Points[s.Points.Count - 1].XValue + 1;
                if (w.Points.Count > 0) nextX5 = w.Points[w.Points.Count - 1].XValue + 1;

                if (r.Points.Count > 0) nextx6 = r.Points[r.Points.Count - 1].XValue + 1;
                if (t.Points.Count > 0) nextx7 = t.Points[t.Points.Count - 1].XValue + 1;

                unoChart.Series["S"].Points.AddXY(nextX, dataSteerAngle);
                unoChart.Series["PWM"].Points.AddXY(nextX5, dataPWM);
                rollChart.Series["Ro"].Points.AddXY(nextx6, roll);
                rollChart.Series["Ze"].Points.AddXY(nextx7, zero);

                while (s.Points.Count > 100)
                {
                    s.Points.RemoveAt(0);
                }
                while (w.Points.Count > 100)
                {
                    w.Points.RemoveAt(0);
                }
                while (r.Points.Count > 100)
                {
                    r.Points.RemoveAt(0);
                }
                while (t.Points.Count > 100)
                {
                    t.Points.RemoveAt(0);
                }
                unoChart.ChartAreas[0].RecalculateAxesScale();
                rollChart.ChartAreas[0].RecalculateAxesScale();
            }
        }

        private void FormSteerGraph_Load(object sender, EventArgs e)
        {
            timer1.Interval = (int)((1 / (double)mf.gpsHz) * 1000);

            if (!ScreenHelper.IsOnScreen(Bounds))
            {
                Top = 0;
                Left = 0;
            }
        }

        private void label1_Click(object sender, EventArgs e)
        {
        }
    }
}