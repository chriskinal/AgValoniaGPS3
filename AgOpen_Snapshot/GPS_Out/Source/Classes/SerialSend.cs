using System;
using System.IO.Ports;

namespace GPS_Out
{
    public class SerialSend
    {
        private readonly frmStart mf;
        private bool cWriteTimeOut = false;
        private SerialPort Sport;
        private System.Windows.Forms.Timer Timer1 = new System.Windows.Forms.Timer();
        private int WriteErrorCount;

        public SerialSend(frmStart CalledFrom)
        {
            this.mf = CalledFrom;
            Sport = new SerialPort(Properties.Settings.Default.Port, Properties.Settings.Default.Baud);
            Sport.WriteTimeout = 500;
            Sport.Parity = Parity.None;
            Sport.DataBits = 8;
            Sport.StopBits = StopBits.One;
            Timer1.Interval = 1000;
            Timer1.Tick += new EventHandler(CheckConnection);

            if (Properties.Settings.Default.AutoConnect && Properties.Settings.Default.SerialSuccessful) Open();
        }

        public int Baud
        {
            get { return Sport.BaudRate; }
            set
            {
                if (!Sport.IsOpen && value > 0 && value < 115201)
                {
                    Sport.BaudRate = value;
                    Properties.Settings.Default.Baud = Sport.BaudRate;
                }
            }
        }

        public string PortNm
        {
            get { return Sport.PortName; }
            set
            {
                if (!Sport.IsOpen && value != "")
                {
                    Sport.PortName = value;
                    Properties.Settings.Default.Port = Sport.PortName;
                }
            }
        }

        public void Close()
        {
            try
            {
                Timer1.Stop();
                if (Sport.IsOpen)
                {
                    Sport.Close();
                    Sport.Dispose();
                }
            }
            catch (Exception ex)
            {
                mf.Tls.WriteErrorLog("SerialSend/CloseRCport: " + ex.Message);
            }
        }

        public bool IsOpen()
        {
            return Sport.IsOpen;
        }

        public bool Open()
        {
            bool Result = false;
            try
            {
                if (SerialPortExists(Sport.PortName))
                {
                    if (!Sport.IsOpen) Sport.Open();

                    if (Sport.IsOpen)
                    {
                        Sport.DiscardOutBuffer();
                        WriteErrorCount = 0;
                        Timer1.Start();
                        Result = true;
                    }
                }
            }
            catch (Exception ex)
            {
                mf.Tls.WriteErrorLog("SerialSend/OpenRCport: " + ex.Message);
            }
            Properties.Settings.Default.SerialSuccessful = Result;
            return Result;
        }

        public void SendStringData(String data)
        {
            if (Sport.IsOpen)
            {
                try
                {
                    Sport.WriteLine(data + "\r\n");
                    cWriteTimeOut = false;
                }
                catch (Exception ex)
                {
                    if (ex is TimeoutException) cWriteTimeOut = true;
                    mf.Tls.WriteErrorLog("SerialSend/SendStringData: " + ex.Message);
                }
            }
        }

        private void CheckConnection(object myObject, EventArgs myEventArgs)
        {
            if (cWriteTimeOut)
            {
                if (++WriteErrorCount > 2)
                {
                    mf.Tls.ShowHelp(Sport.PortName + " is not sending correctly. It will be closed.", "Serial Port", 5000, true, false, true);
                    Close();
                    mf.SetPortButtons1();
                }
            }
            else
            {
                WriteErrorCount = 0;
            }
        }

        private bool SerialPortExists(string Name)
        {
            bool Result = false;
            foreach (string s in SerialPort.GetPortNames())
            {
                if (s == Name)
                {
                    Result = true;
                    break;
                }
            }
            return Result;
        }
    }
}