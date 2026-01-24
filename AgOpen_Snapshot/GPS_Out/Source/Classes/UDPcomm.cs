using System;
using System.Net;
using System.Net.Sockets;

namespace GPS_Out
{
    public class UDPComm
    {
        private readonly frmStart mf;
        private byte[] buffer = new byte[1024];
        private string cConnectionName;
        private bool cIsUDPSendConnected;
        private string cLog;
        private IPAddress cNetworkEP;
        private int cReceivePort;   // local ports must be unique for each app on same pc and each class instance
        private int cSendFromPort;
        private int cSendToPort;
        private IPAddress cSourceIP;
        private string cSubNet;
        private HandleDataDelegateObj HandleDataDelegate = null;
        private Socket recvSocket;
        private Socket sendSocket;

        public UDPComm(frmStart CallingForm, int ReceivePort, int SendToPort, int SendFromPort,
            string ConnectionName, string SourceIPaddress, string DestinationEndPoint = "")
        {
            mf = CallingForm;
            cReceivePort = ReceivePort;
            cSendToPort = SendToPort;
            cSendFromPort = SendFromPort;
            cConnectionName = ConnectionName;
            SetEP(DestinationEndPoint);
            SetSourceIP(SourceIPaddress);
        }

        // Status delegate
        private delegate void HandleDataDelegateObj(int port, byte[] msg);

        public bool IsUDPSendConnected { get => cIsUDPSendConnected; set => cIsUDPSendConnected = value; }

        public string NetworkEP
        {
            get { return cNetworkEP.ToString(); }
            set
            {
                string[] data;
                if (IPAddress.TryParse(value, out IPAddress IP))
                {
                    data = value.Split('.');
                    cNetworkEP = IPAddress.Parse(data[0] + "." + data[1] + "." + data[2] + ".255");
                    Properties.Settings.Default["EndPoint" + cConnectionName] = cNetworkEP.ToString();
                    cSubNet = data[0].ToString() + "." + data[1].ToString() + "." + data[2].ToString();
                }
            }
        }

        public void StartUDPServer()
        {
            try
            {
                // initialize the delegate which updates the message received
                HandleDataDelegate = HandleData;

                // initialize the receive socket
                recvSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                recvSocket.Bind(new IPEndPoint(cSourceIP, cReceivePort));

                // initialize the send socket
                sendSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

                // Initialise the IPEndPoint for the server to send on port
                IPEndPoint server = new IPEndPoint(IPAddress.Any, cSendFromPort);
                sendSocket.Bind(server);

                // Initialise the IPEndPoint for the client - async listner client only!
                EndPoint client = new IPEndPoint(IPAddress.Any, 0);

                // Start listening for incoming data
                recvSocket.BeginReceiveFrom(buffer, 0, buffer.Length, SocketFlags.None, ref client, new AsyncCallback(ReceiveData), recvSocket);
                IsUDPSendConnected = true;
            }
            catch (Exception e)
            {
                mf.Tls.WriteErrorLog("UDPcomm/StartUDPServer: \n" + e.Message);
            }
        }

        private void AddToLog(string NewData)
        {
            cLog += DateTime.Now.Second.ToString() + "  " + NewData + Environment.NewLine;
            if (cLog.Length > 100000)
            {
                cLog = cLog.Substring(cLog.Length - 98000, 98000);
            }
            cLog = cLog.Replace("\0", string.Empty);
        }

        private void HandleData(int Port, byte[] Data)
        {
            try
            {
                if (Data.Length > 1)
                {
                    int PGN = Data[1] << 8 | Data[0];
                    AddToLog("< " + PGN.ToString());

                    switch (PGN)
                    {
                        case 33152: // AOG, 0x8180
                            int SubPGN = Data[3] << 8 | Data[2];
                            switch (SubPGN)
                            {
                                case 54908: // 0xD67C, AGIO NEMA translation
                                    mf.AGIOdata.ParseByteData(Data);
                                    break;

                                case 25727: // 0x647F, AOG roll corrected lat,lon
                                    mf.AOGdata.ParseByteData(Data);
                                    break;
                            }
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                mf.Tls.WriteErrorLog("UDPcomm/HandleData " + ex.Message);
            }
        }

        private void ReceiveData(IAsyncResult asyncResult)
        {
            try
            {
                // Initialise the IPEndPoint for the client
                EndPoint epSender = new IPEndPoint(IPAddress.Any, 0);

                // Receive all data
                int msgLen = recvSocket.EndReceiveFrom(asyncResult, ref epSender);

                byte[] localMsg = new byte[msgLen];
                Array.Copy(buffer, localMsg, msgLen);

                // Listen for more connections again...
                recvSocket.BeginReceiveFrom(buffer, 0, buffer.Length, SocketFlags.None, ref epSender, new AsyncCallback(ReceiveData), epSender);

                int port = ((IPEndPoint)epSender).Port;
                // Update status through a delegate
                mf.Invoke(HandleDataDelegate, new object[] { port, localMsg });
            }
            catch (ObjectDisposedException)
            {
                // do nothing
            }
            catch (Exception ex)
            {
                //mf.Tls.ShowHelp("ReceiveData Error \n" + e.Message, "Comm", 3000, true);
                mf.Tls.WriteErrorLog("UDPcomm/ReceiveData " + ex.Message);
            }
        }

        private void SendData(IAsyncResult asyncResult)
        {
            try
            {
                sendSocket.EndSend(asyncResult);
            }
            catch (Exception ex)
            {
                mf.Tls.WriteErrorLog(" UDP Send Data" + ex.ToString());
            }
        }

        private void SetEP(string DestinationEndPoint)
        {
            try
            {
                if (IPAddress.TryParse(DestinationEndPoint, out _))
                {
                    NetworkEP = DestinationEndPoint;
                }
                else
                {
                    string EP = mf.Tls.LoadProperty("EndPoint_" + cConnectionName);
                    if (IPAddress.TryParse(EP, out _))
                    {
                        NetworkEP = EP;
                    }
                    else
                    {
                        NetworkEP = "192.168.1.255";
                    }
                }
            }
            catch (Exception ex)
            {
                mf.Tls.WriteErrorLog("UDPcomm/SetEP " + ex.Message);
            }
        }

        private void SetSourceIP(string Source)
        {
            try
            {
                if (IPAddress.TryParse(Source, out IPAddress tmp))
                {
                    cSourceIP = tmp;
                }
                else
                {
                    cSourceIP = IPAddress.Any;
                }
            }
            catch (Exception ex)
            {
                mf.Tls.WriteErrorLog("UDPcomm/SetSourceEP " + ex.Message);
            }
        }
    }
}