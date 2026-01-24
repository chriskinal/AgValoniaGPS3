using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GPS_Out.PGNs
{
    public class PGN_GSA
    {
        #region GSA message
        // $GNGSA,A,3,21,5,29,25,12,10,26,2,,,,,1.2,0.7,1.0*27

        // A        mode automatic/manual
        // 3        3D fix
        // 21       sat ID 1
        // 5        sat ID 2
        // 29       sat ID 3
        // 25       sat ID 4
        // 12       sat ID 5
        // 10       sat ID 6
        // 26       sat ID 7
        // 2        sat ID 8
        //          sat ID 9
        //          sat ID 10
        //          sat ID 11
        //          sat ID 12
        // 1.2      PDOP
        // 0.7      HDOP
        // 1.0      VDOP
        // *27      checksum

        #endregion GSA message

        private string cSentence;
        private frmStart mf;

        public PGN_GSA(frmStart CalledFrom)
        {
            mf = CalledFrom;
        }

        public string Build()
        {
            cSentence = Properties.Settings.Default.SentenceStart + "GSA,A,3,01,02,03,,,,,,,,,,2";
            cSentence += "," + mf.AGIOdata.HDOP.ToString("N2", CultureInfo.InvariantCulture);
            cSentence += ",2";

            cSentence += "*";
            string Hex = mf.CheckSum(cSentence).ToString("X2");
            cSentence += Hex;

            return cSentence;
        }
    }
}
