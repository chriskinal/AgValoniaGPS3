using System;
using System.Drawing;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Windows.Forms;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;

namespace ModSim
{
    public partial class FormSim
    {
        // UDP Sockets
        public Socket UDPSocket;
        private EndPoint endPointUDP = new IPEndPoint(IPAddress.Any, 0);

        public bool isUDPNetworkConnected;

        //UDP Endpoints
        public IPEndPoint epAgIO = new IPEndPoint(IPAddress.Parse(
                Properties.Settings.Default.etIP_SubnetOne.ToString() + "." +
                Properties.Settings.Default.etIP_SubnetTwo.ToString() + "." +
                Properties.Settings.Default.etIP_SubnetThree.ToString() + ".255"), 9999);
        
        // Data stream
        private byte[] buffer = new byte[1024];

        //used to send communication check pgn= C8 or 200
        private byte[] helloFromAgIO = { 0x80, 0x81, 0x7F, 200, 3, 56, 0, 0, 0x47 };

        public IPAddress ipCurrent;

        //initialize udp network
        public void LoadUDPNetwork()
        {
            helloFromAgIO[5] = 56;

            lblIP.Text = "";
            try //udp network
            {
                string bob = Dns.GetHostName();
                foreach (IPAddress IPA in Dns.GetHostAddresses(Dns.GetHostName()))
                {
                    if (IPA.AddressFamily == AddressFamily.InterNetwork)
                    {
                        string data = IPA.ToString();
                        lblIP.Text += IPA.ToString().Trim() + "\r\n";
                    }
                }

                // Initialise the socket
                UDPSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                UDPSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Broadcast, true);
                UDPSocket.Bind(new IPEndPoint(IPAddress.Any, 8888));
                UDPSocket.BeginReceiveFrom(buffer, 0, buffer.Length, SocketFlags.None, ref endPointUDP,
                    new AsyncCallback(ReceiveDataUDPAsync), null);

                isUDPNetworkConnected = true;

                //if (!isFound)
                //{
                //    MessageBox.Show("Network Address of Modules -> " + Properties.Settings.Default.setIP_localAOG+"[2 - 254] May not exist. \r\n"
                //    + "Are you sure ethernet is connected?\r\n" + "Go to UDP Settings to fix.\r\n\r\n", "Network Connection Error",
                //    MessageBoxButtons.OK, MessageBoxIcon.Error);
                //    //btnUDP.BackColor = Color.Red;
                //    lblIP.Text = "Not Connected";
                //}
            }
            catch (Exception e)
            {
                //WriteErrorLog("UDP Server" + e);
                MessageBox.Show(e.Message, "Serious Network Connection Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                lblIP.Text = "Error";
            }
        }

        #region Send UDP

        public void SendUDPMessage(byte[] byteData)
        {
            if (isUDPNetworkConnected)
            {
                try
                {
                    // Send packet to the zero
                    if (byteData.Length != 0)
                    {
                        UDPSocket.BeginSendTo(byteData, 0, byteData.Length, SocketFlags.None,
                           epAgIO, new AsyncCallback(SendDataUDPAsync), null);
                    }
                }
                catch (Exception)
                {
                    //WriteErrorLog("Sending UDP Message" + e.ToString());
                    //MessageBox.Show("Send Error: " + e.Message, "UDP Client", MessageBoxButtons.OK,
                    //MessageBoxIcon.Error);
                }
            }
        }

        public void SendUDPMessage(string message)
        {
            if (isUDPNetworkConnected)
            {
                try
                {
                    // Get packet as byte array to send
                    byte[] byteData = Encoding.ASCII.GetBytes(message);
                    if (byteData.Length != 0)
                        UDPSocket.BeginSendTo(byteData, 0, byteData.Length, SocketFlags.None,
                            epAgIO, new AsyncCallback(SendDataUDPAsync), null);
                }
                catch (Exception)
                {
                }
            }
        }


        private void SendDataUDPAsync(IAsyncResult asyncResult)
        {
            try
            {
                UDPSocket.EndSend(asyncResult);
            }
            catch (Exception)
            {
                //WriteErrorLog(" UDP Send Data" + e.ToString());
                //MessageBox.Show("SendData Error: " + e.Message, "UDP Server", MessageBoxButtons.OK,
                //MessageBoxIcon.Error);
            }
        }

        #endregion

        #region Receive UDP

        private void ReceiveDataUDPAsync(IAsyncResult asyncResult)
        {
            try
            {
                // Receive all data
                int msgLen = UDPSocket.EndReceiveFrom(asyncResult, ref endPointUDP);

                byte[] localMsg = new byte[msgLen];
                Array.Copy(buffer, localMsg, msgLen);

                // Listen for more connections again...
                UDPSocket.BeginReceiveFrom(buffer, 0, buffer.Length, SocketFlags.None, ref endPointUDP, 
                    new AsyncCallback(ReceiveDataUDPAsync), null);

                BeginInvoke((MethodInvoker)(() => ReceiveFromUDP(localMsg)));

            }
            catch (Exception)
            {
                //WriteErrorLog("UDP Recv data " + e.ToString());
                //MessageBox.Show("ReceiveData Error: " + e.Message, "UDP Server", MessageBoxButtons.OK,
                //MessageBoxIcon.Error);
            }
        }


        static byte [] PGN_253 = { 128, 129, 126, 253, 8, 0, 0, 0, 0, 0, 0, 0, 0, 12 };
        int PGN_253_Size = PGN_253.Length - 1;

        //Heart beat hello AgIO
        static byte [] helloFromAutoSteer = { 128, 129, 126, 126, 5, 0, 0, 0, 0, 0, 71 };
        //short helloSteerPosition = 0;

        //hello from AgIO
        static byte[] helloFromMachine = { 128, 129, 123, 123, 5, 0, 0, 0, 0, 0, 71 };

        //hello from AgIO
        static byte[] helloFromIMU = { 128, 129, 121, 121, 5, 0, 0, 0, 0, 0, 71 };

        //settings pgn
        static byte[] PGN_237 = { 0x80, 0x81, 0x7f, 237, 8, 1, 2, 3, 4, 0, 0, 0, 0, 0xCC };
        int PGN_237_Size = PGN_237.Length - 1;


        //Relays
        //bool isRelayActiveHigh = true;
        byte relay = 0, relayHi = 0, uTurn = 0;
        byte xte = 0;

        //Switches
        int remoteSwitch = 1, workSwitch = 1, steerSwitch = 1, switchByte = 0;

        //On Off
        byte guidanceStatus = 0;
        byte prevGuidanceStatus = 0;
        bool guidanceStatusChanged = false;

        //speed sent as *10
        double gpsSpeed = 0, gpsSpeedMM = 0;

        //steering variables
        double steerAngleActual = 0;
        double steerAngleSetPoint = 0; //the desired angle from AgOpen
        //int steeringPosition = 0; //from steering sensor
        //double steerAngleError = 0; //setpoint - actual

        //Machine module
        int hydLift = 0;
        int tramline = 0;

        int relayLoM = 0;
        int relayHiM = 0;

        private void ReceiveFromUDP(byte[] data)
        {
            try
            {
                //Hello and scan reply
                if (data[0] == 0x80 && data[1] == 0x81 && data[2] == 0x7F)
                {
                    switch (data[3])
                    {
                        case 254:
                            {
                                gpsSpeed = ((double)(data[5] | data[6] << 8)) * 0.1;

                                prevGuidanceStatus = guidanceStatus;

                                guidanceStatus = data[7];
                                guidanceStatusChanged = (guidanceStatus != prevGuidanceStatus);

                                lblGuidanceStatus.Text = guidanceStatus.ToString();
                                lblSteerSwitchStatus.Text = steerSwitch.ToString();

                                //if (steerConfig.SteerButton == 1)
                                //{
                                //    if (guidanceStatus == 1) steerSwitch = 0;
                                //}

                                int temp = (data[9] << 8);
                                temp |= data[8];
                                short temp2 = (short)temp;

                                //Bit 8,9    set point steer angle * 100 is sent
                                steerAngleSetPoint = (float)(temp2) * 0.01; //high low bytes

                                //Bit 10 Tram 
                                xte = data[10];

                                //Bit 11
                                relay = data[11];

                                //Bit 12
                                relayHi = data[12];

                                byte swap = swapBits[data[11]];
                                lbl1To8.Text = Convert.ToString(swap, 2).PadLeft(8, '0');

                                swap = swapBits[data[12]];
                                lbl9To16.Text = Convert.ToString(swap, 2).PadLeft(8, '0');

                                //----------------------------------------------------------------------------
                                //Serial Send to agopenGPS

                                int sa = (int)(steerAngleActual * 100);

                                PGN_253[5] = unchecked((byte)((int)(sa)));
                                PGN_253[6] = unchecked((byte)((int)(sa) >> 8));

                                //heading         
                                PGN_253[7] = unchecked((byte)((int)(9999)));
                                PGN_253[8] = unchecked((byte)((int)(9999) >> 8));

                                //roll
                                PGN_253[9] = unchecked((byte)((int)(8888)));
                                PGN_253[10] = unchecked((byte)((int)(8888) >> 8));

                                switchByte = 0;
                                switchByte |= ((int)remoteSwitch << 2); //put remote in bit 2
                                switchByte |= (steerSwitch << 1);   //put steerSwitch status in bit 1 position
                                switchByte |= workSwitch;

                                PGN_253[11] = (byte)switchByte;
                                PGN_253[12] = 44;  //(uint8_t)pwmDisplay;

                                //checksum
                                int CK_A = 0;
                                for (int i = 2; i < PGN_253_Size; i++)
                                    CK_A = (CK_A + PGN_253[i]);

                                PGN_253[PGN_253_Size] = unchecked((byte)((int)(CK_A)));

                                SendUDPMessage(PGN_253);

                                if (btnSteerButtonRemote.BackColor == Color.Green)
                                {
                                    btnSteerButtonRemote.BackColor = Color.White;
                                }

                                break;
                            }

                        case 252:
                            {
                                //PID values
                                steerSettings.Kp = data[5];   // read Kp from AgOpenGPS
                                lblKp.Text = steerSettings.Kp.ToString();

                                steerSettings.highPWM = data[6]; // read high pwm
                                lblHighPWM.Text = steerSettings.highPWM.ToString();

                                steerSettings.lowPWM = data[7];   // read lowPWM from AgOpenGPS              

                                steerSettings.minPWM = data[8]; //read the minimum amount of PWM for instant on\
                                lblMinPWM.Text = steerSettings.minPWM.ToString();

                                float temp = steerSettings.minPWM;
                                temp *= 1.2f;
                                steerSettings.lowPWM = (byte)temp;
                                lblLowPWM.Text = steerSettings.lowPWM.ToString();

                                steerSettings.steerSensorCounts = data[9]; //sent as setting displayed in AOG
                                lblWAS_Counts.Text = steerSettings.steerSensorCounts.ToString();

                                steerSettings.wasOffset = (data[10]);  //read was zero offset Lo

                                steerSettings.wasOffset |= (data[11] << 8);  //read was zero offset Hi
                                lblWAS_Offset.Text = steerSettings.wasOffset.ToString();

                                steerSettings.AckermanFix = (float)data[12] * 0.01;
                                lblAckerman.Text = (steerSettings.AckermanFix * 100).ToString("N0") + "%";

                                break;
                            }

                        case 251:
                            {
                                int sett = data[5]; //setting0

                                if ((sett & (1 << 0)) != 0) steerConfig.InvertWAS = 1; else steerConfig.InvertWAS = 0;
                                lblInvertWAS.Text = steerConfig.InvertWAS.ToString();

                                if ((sett & (1 << 1)) != 0) steerConfig.IsRelayActiveHigh = 1; else steerConfig.IsRelayActiveHigh = 0;
                                lblRelayActHigh.Text = steerConfig.IsRelayActiveHigh.ToString();

                                if ((sett & (1 << 2)) != 0) steerConfig.MotorDriveDirection = 1; else steerConfig.MotorDriveDirection = 0;
                                lblMotorDirection.Text = steerConfig.MotorDriveDirection.ToString();

                                if ((sett & (1 << 3)) != 0) steerConfig.SingleInputWAS = 1; else steerConfig.SingleInputWAS = 0;
                                lblSingleInputWAS.Text = steerConfig.SingleInputWAS.ToString();

                                if ((sett & (1 << 4)) != 0) steerConfig.CytronDriver = 1; else steerConfig.CytronDriver = 0;
                                lblCytron.Text = steerConfig.CytronDriver.ToString();

                                if ((sett & (1 << 5)) != 0) steerConfig.SteerSwitch = 1; else steerConfig.SteerSwitch = 0;
                                lblSteerSw.Text = steerConfig.SteerSwitch.ToString();

                                if ((sett & (1 << 6)) != 0) steerConfig.SteerButton = 1; else steerConfig.SteerButton = 0;
                                lblSteerBtn.Text = steerConfig.SteerButton.ToString();

                                if ((sett & (1 << 7)) != 0) steerConfig.ShaftEncoder = 1; else steerConfig.ShaftEncoder = 0;
                                lblShaftEnc.Text = steerConfig.ShaftEncoder.ToString();

                                steerConfig.PulseCountMax = data[6];
                                lblPulseCounts.Text = steerConfig.PulseCountMax.ToString();

                                //was speed
                                //data[7]; 

                                sett = data[8]; //setting1 - Danfoss valve etc

                                if ((sett & (1 << 0)) != 0) steerConfig.IsDanfoss = 1; else steerConfig.IsDanfoss = 0;
                                lblDanfoss.Text = steerConfig.IsDanfoss.ToString();

                                if ((sett & (1 << 1)) != 0) steerConfig.PressureSensor = 1; else steerConfig.PressureSensor = 0;
                                lblPressure.Text = steerConfig.PressureSensor.ToString();

                                if ((sett & (1 << 2)) != 0) steerConfig.CurrentSensor = 1; else steerConfig.CurrentSensor = 0;
                                lblCurrent.Text = steerConfig.CurrentSensor.ToString();

                                if ((sett & (1 << 3)) != 0) steerConfig.IsUseY_Axis = 1; else steerConfig.IsUseY_Axis = 0;
                                lblUseY_Axis.Text = steerConfig.IsUseY_Axis.ToString();
                                break;
                            }

                        case 200: // Hello from AgIO
                            {
                                int sa = (int)(steerAngleActual * 100);

                                helloFromAutoSteer[5] = unchecked((byte)((int)(sa)));
                                helloFromAutoSteer[6] = unchecked((byte)((int)(sa) >> 8));

                                helloFromAutoSteer[7] = 0;
                                helloFromAutoSteer[8] = 0;
                                helloFromAutoSteer[9] = (byte)switchByte;

                                SendUDPMessage(helloFromAutoSteer);

                                helloFromMachine[5] = (byte)relayLoM;
                                helloFromMachine[6] = (byte)relayHiM;

                                SendUDPMessage(helloFromMachine);

                                SendUDPMessage(helloFromIMU);

                                break;
                            }

                        case 201:
                            {
                                //make really sure this is the subnet pgn
                                if (data[4] == 5 && data[5] == 201 && data[6] == 201)
                                {
                                    lblIPSet1.Text = data[7].ToString();
                                    lblIPSet2.Text = data[8].ToString();
                                    lblIPSet3.Text = data[9].ToString();

                                    TimedMessageBox(2000, "IP Set", "New Values Changed");

                                    Properties.Settings.Default.etIP_SubnetOne = data[7];
                                    Properties.Settings.Default.etIP_SubnetTwo = data[8];
                                    Properties.Settings.Default.etIP_SubnetThree = data[9];
                                    Properties.Settings.Default.Save();

                                    YesMessageBox("ModSim will Restart to Enable UDP Networking Changes");

                                    Application.Restart();
                                    Environment.Exit(0);
                                    Close();

                                }

                                break;
                            }

                        //scan reply
                        case 202:
                            {
                                //make really sure this is the reply pgn
                                if (data[4] == 3 && data[5] == 202 && data[6] == 202)
                                {
                                    byte [] scanReply = { 128, 129, 126, 203, 7,
                                        Properties.Settings.Default.etIP_SubnetOne,
                                        Properties.Settings.Default.etIP_SubnetTwo,
                                        Properties.Settings.Default.etIP_SubnetThree, 126,

                                        //source ips

                                        Properties.Settings.Default.etIP_SubnetOne,
                                        Properties.Settings.Default.etIP_SubnetTwo,
                                        Properties.Settings.Default.etIP_SubnetThree, 23 };

                                    lblScanReply.Text = "Yes";


                                    //checksum
                                    int CK_A = 0;
                                    for (int i = 2; i < scanReply.Length - 1; i++)
                                    {
                                        CK_A = (CK_A + scanReply[i]);
                                    }
                                    scanReply[scanReply.Length - 1] = unchecked((byte)((int)(CK_A))); 

                                    SendUDPMessage(scanReply);

                                    //reset to Machine Module
                                    scanReply[2] = 123;
                                    scanReply[8] = 123;

                                    //checksum
                                    CK_A = 0;
                                    for (int i = 2; i < scanReply.Length - 1; i++)
                                    {
                                        CK_A = (CK_A + scanReply[i]);
                                    }
                                    scanReply[scanReply.Length - 1] = unchecked((byte)((int)(CK_A)));

                                    SendUDPMessage(scanReply);

                                    //reset to Machine Module
                                    scanReply[2] = 121;
                                    scanReply[8] = 121;

                                    //checksum
                                    CK_A = 0;
                                    for (int i = 2; i < scanReply.Length - 1; i++)
                                    {
                                        CK_A = (CK_A + scanReply[i]);
                                    }
                                    scanReply[scanReply.Length - 1] = unchecked((byte)((int)(CK_A)));

                                    SendUDPMessage(scanReply);

                                }
                                break;
                            }

                            ///////////// Machine Module

                        case 239:  //machine data
                            {
                                uTurn = data[5];
                                lblUTurn.Text=uTurn.ToString();

                                gpsSpeedMM = (double)data[6];//actual speed times 4, single uint8_t
                                gpsSpeedMM *= 0.1;
                                lblGPSSpeedMM.Text = gpsSpeedMM.ToString("N1");

                                hydLift = data[7];
                                lblHydLift.Text = hydLift.ToString();

                                tramline = data[8];  //bit 0 is right bit 1 is left
                                lblTram.Text = tramline.ToString();

                                relayLoM = data[11];          // read relay control from AgOpenGPS
                                relayHiM = data[12];

                                byte swap = swapBits[data[11]];
                                lbl1To8M.Text = Convert.ToString(swap, 2).PadLeft(8, '0');

                                swap = swapBits[data[12]];
                                lbl9To16M.Text = Convert.ToString(swap, 2).PadLeft(8, '0');

                                break;
                            }

                        case 229:
                            {
                                byte swap = swapBits[data[5]];
                                lblZone1.Text = Convert.ToString(swap, 2).PadLeft(8, '0');

                                swap = swapBits[data[6]];
                                lblZone2.Text = Convert.ToString(swap, 2).PadLeft(8, '0');

                                swap = swapBits[data[7]];
                                lblZone3.Text = Convert.ToString(swap, 2).PadLeft(8, '0');

                                swap = swapBits[data[8]];
                                lblZone4.Text = Convert.ToString(swap, 2).PadLeft(8, '0');

                                swap = swapBits[data[9]];
                                lblZone5.Text = Convert.ToString(swap, 2).PadLeft(8, '0');

                                swap = swapBits[data[10]];
                                lblZone6.Text = Convert.ToString(swap, 2).PadLeft(8, '0');

                                swap = swapBits[data[11]];
                                lblZone7.Text = Convert.ToString(swap, 2).PadLeft(8, '0');

                                swap = swapBits[data[12]];
                                lblZone8.Text = Convert.ToString(swap, 2).PadLeft(8, '0');

                                break;
                            }

                        case 238:
                            {
                                aogConfig.raiseTime = data[5];
                                lblRaiseTime.Text = aogConfig.raiseTime.ToString();

                                aogConfig.lowerTime = data[6];
                                lblLowerTime.Text = aogConfig.lowerTime.ToString();

                                aogConfig.enableToolLift = data[7];
                                lblLiftEnable.Text = aogConfig.enableToolLift.ToString();

                                //set1 
                                int sett = data[8];  //setting0     
                                if ((sett & (1 << 0)) != 0) 
                                    aogConfig.isRelayActiveHigh = 1; else aogConfig.isRelayActiveHigh = 0;
                                lblRelayActiveHigh.Text = aogConfig.isRelayActiveHigh.ToString();

                                aogConfig.user1 = data[9];
                                lblUser1.Text = data[9].ToString();

                                aogConfig.user2 = data[10];
                                lblUser2.Text = data[10].ToString();

                                aogConfig.user3 = data[11];
                                lblUser3.Text = data[11].ToString();

                                aogConfig.user4 = data[12];
                                lblUser4.Text = data[12].ToString();

                                break;
                            }


                        ////////////////////////////////////////////////////////////////////////////////////////////////////////////////

                        default:
                            {
                                //module return via udp sent to AOG
                                //SendToLoopBackMessageAOG(data);

                                break;
                            }
                    }
                } // end of pgns
            }
            catch
            {

            }
        }

        #endregion

        static byte [] swapBits = {
        0x00, 0x80, 0x40, 0xc0, 0x20, 0xa0, 0x60, 0xe0,
        0x10, 0x90, 0x50, 0xd0, 0x30, 0xb0, 0x70, 0xf0,
        0x08, 0x88, 0x48, 0xc8, 0x28, 0xa8, 0x68, 0xe8,
        0x18, 0x98, 0x58, 0xd8, 0x38, 0xb8, 0x78, 0xf8,
        0x04, 0x84, 0x44, 0xc4, 0x24, 0xa4, 0x64, 0xe4,
        0x14, 0x94, 0x54, 0xd4, 0x34, 0xb4, 0x74, 0xf4,
        0x0c, 0x8c, 0x4c, 0xcc, 0x2c, 0xac, 0x6c, 0xec,
        0x1c, 0x9c, 0x5c, 0xdc, 0x3c, 0xbc, 0x7c, 0xfc,
        0x02, 0x82, 0x42, 0xc2, 0x22, 0xa2, 0x62, 0xe2,
        0x12, 0x92, 0x52, 0xd2, 0x32, 0xb2, 0x72, 0xf2,
        0x0a, 0x8a, 0x4a, 0xca, 0x2a, 0xaa, 0x6a, 0xea,
        0x1a, 0x9a, 0x5a, 0xda, 0x3a, 0xba, 0x7a, 0xfa,
        0x06, 0x86, 0x46, 0xc6, 0x26, 0xa6, 0x66, 0xe6,
        0x16, 0x96, 0x56, 0xd6, 0x36, 0xb6, 0x76, 0xf6,
        0x0e, 0x8e, 0x4e, 0xce, 0x2e, 0xae, 0x6e, 0xee,
        0x1e, 0x9e, 0x5e, 0xde, 0x3e, 0xbe, 0x7e, 0xfe,
        0x01, 0x81, 0x41, 0xc1, 0x21, 0xa1, 0x61, 0xe1,
        0x11, 0x91, 0x51, 0xd1, 0x31, 0xb1, 0x71, 0xf1,
        0x09, 0x89, 0x49, 0xc9, 0x29, 0xa9, 0x69, 0xe9,
        0x19, 0x99, 0x59, 0xd9, 0x39, 0xb9, 0x79, 0xf9,
        0x05, 0x85, 0x45, 0xc5, 0x25, 0xa5, 0x65, 0xe5,
        0x15, 0x95, 0x55, 0xd5, 0x35, 0xb5, 0x75, 0xf5,
        0x0d, 0x8d, 0x4d, 0xcd, 0x2d, 0xad, 0x6d, 0xed,
        0x1d, 0x9d, 0x5d, 0xdd, 0x3d, 0xbd, 0x7d, 0xfd,
        0x03, 0x83, 0x43, 0xc3, 0x23, 0xa3, 0x63, 0xe3,
        0x13, 0x93, 0x53, 0xd3, 0x33, 0xb3, 0x73, 0xf3,
        0x0b, 0x8b, 0x4b, 0xcb, 0x2b, 0xab, 0x6b, 0xeb,
        0x1b, 0x9b, 0x5b, 0xdb, 0x3b, 0xbb, 0x7b, 0xfb,
        0x07, 0x87, 0x47, 0xc7, 0x27, 0xa7, 0x67, 0xe7,
        0x17, 0x97, 0x57, 0xd7, 0x37, 0xb7, 0x77, 0xf7,
        0x0f, 0x8f, 0x4f, 0xcf, 0x2f, 0xaf, 0x6f, 0xef,
        0x1f, 0x9f, 0x5f, 0xdf, 0x3f, 0xbf, 0x7f, 0xff,  };

    }



    public static class steerConfig
    {            
        public static byte InvertWAS = 0;
        public static byte IsRelayActiveHigh = 0; //if zero, active low (default)
        public static byte MotorDriveDirection = 0;
        public static byte SingleInputWAS = 1;
        public static byte CytronDriver = 1;
        public static byte SteerSwitch = 0;  //1 if switch selected
        public static byte SteerButton = 0;  //1 if button selected
        public static byte ShaftEncoder = 0;
        public static byte PressureSensor = 0;
        public static byte CurrentSensor = 0;
        public static byte PulseCountMax = 5;
        public static byte IsDanfoss = 0;
        public static byte IsUseY_Axis = 0;
    }

    public static class steerSettings
    {
        public static byte Kp = 120;  //proportional gain
        public static byte lowPWM = 30;  //band of no action
        public static int wasOffset = 0;
        public static byte minPWM = 25;
        public static byte highPWM = 160;//max PWM value
        public static double steerSensorCounts = 30;
        public static double AckermanFix = 1;     //sent as percent
    }

    //relay module config
    public static class aogConfig
    {
        public static byte raiseTime = 2;
        public static byte lowerTime = 4;
        public static byte enableToolLift = 0;
        public static byte isRelayActiveHigh = 0; //if zero, active low (default)
        public static byte user1 = 0; 
        public static byte user2 = 0;
        public static byte user3 = 0;
        public static byte user4 = 0;
    }
}
