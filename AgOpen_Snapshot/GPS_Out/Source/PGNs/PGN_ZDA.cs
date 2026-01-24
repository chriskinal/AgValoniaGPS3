using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GPS_Out.PGNs
{
    public class PGN_ZDA
    {
        #region ZDA Message

        //$GPZDA,160012.71,11,03,2004,-01,00*7D

        //ZDA       time and date
        //160012    hhmmss.ss
        //11        day, 01 to 31
        //03        month, 01 to 12
        //2004      year, 4 digits
        //-01       local time zone description, 00 to +-13 hours
        //00        local time zone description, 00 to 59, same sign as local hours
        //*7D       checksum

        #endregion ZDA Message

        private string cSentence;
        private frmStart mf;

        public PGN_ZDA(frmStart CalledFrom)
        {
            mf = CalledFrom;
        }
        public string Sentence
        { get { return cSentence; } }

        public string Build()
        {
            cSentence = Properties.Settings.Default.SentenceStart + "ZDA";
            cSentence += "," + DateTime.UtcNow.ToString("HHmmss.fff", CultureInfo.InvariantCulture);
            cSentence += "," + DateTime.UtcNow.Day.ToString("00");
            cSentence += "," + DateTime.UtcNow.Month.ToString("00");
            cSentence += "," + DateTime.UtcNow.Year.ToString("0000");

            var offset = TimeZoneInfo.Local.GetUtcOffset(DateTime.UtcNow);
            cSentence += "," + offset.Hours.ToString("00");
            cSentence += "," + offset.Minutes.ToString("00");

            cSentence += "*";
            string Hex = mf.CheckSum(cSentence).ToString("X2");
            cSentence += Hex;

            return cSentence;
        }
    }
}
