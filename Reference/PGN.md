# AOG message format

An AOG message of length `n` has the following general format:

<table>
    <thead>
        <tr>
            <th nowrap align=center>Byte 0</th>
            <th nowrap align=center>Byte 1</th>
            <th nowrap align=center>Byte 2</th>
            <th nowrap align=center>Byte 3</th>
            <th nowrap align=center>Byte 4</th>
            <th nowrap align=center>...</th>
            <th nowrap align=center>Byte n-1</th>
        </tr>
    </thead>
    <tbody>
        <tr>
            <td align=center>0x80</td>
            <td align=center>0x81</td>
            <td align=center>Src</td>
            <td align=center>PGN</td>
            <td align=center>Len</td>
            <td align=center>Data</td>
            <td align=center>CRC</td>
        </tr>
    </tbody>
</table>

- The first two bytes consist of the preamble 0x80 & 0x81 to identify AOG messages.
- **Src**: The source (sender) of the message.
- **PGN**: The message identifier (Parameter Group Number) listed below.
- **Len**: The length of the message (only counting **Data** in bytes).
- **Data**: The actual data corresponding to the respective **PGN**.
- **CRC**: Checksum of the message (sum of byte 2 - n-2).

# PGN list

## Steer Module

IP = 192.168.5.126<br>
Hello = 126<br>
Port = 5126<br>
00 00 56 00 00 7E<br>


<table>
    <thead>
        <tr>
            <th nowrap align=center>PGN Name</th>
            <th nowrap align=center>Src</th>
            <th nowrap align=center>Dec</th>
            <th nowrap align=center>PGN</th>
            <th nowrap align=center>Dec</th>
            <th nowrap align=center>Len</th>
            <th nowrap align=center>Byte 5</th>
            <th nowrap align=center>Byte 6</th>
            <th nowrap align=center>Byte 7</th>
            <th nowrap align=center>Byte 8</th>
            <th nowrap align=center>Byte 9</th>
            <th nowrap align=center>Byte 10</th>
            <th nowrap align=center>Byte 11</th>
            <th nowrap align=center>Byte 12</th>
            <th nowrap align=center>Byte 13</th>
        </tr>
    </thead>
    <tbody>
        <tr>
            <td align=center>Steer Data</td>
            <td align=center>7F</td>
            <td align=center>127</td>
            <td align=center>FE</td>
            <td align=center>254</td>
            <td align=center>8</td>
            <td align=center colspan=2>Speed</td>
            <td align=center>Status</td>
            <td align=center colspan=2>steerAngle</td>
            <td align=center>xte</td>
            <td align=center>SC1to8</td>
            <td align=center>SC9to16</td>
            <td align=center>CRC</td>
        </tr>
        <tr>
            <td align=center>Steer Settings</td>
            <td align=center>7F</td>
            <td align=center>127</td>
            <td align=center>FC</td>
            <td align=center>252</td>
            <td align=center>8</td>
            <td align=center>gainP</td>
            <td align=center>highPWM</td>
            <td align=center>lowPWM</td>
            <td align=center>minPWM</td>
            <td align=center>countsPerDeg</td>
            <td align=center colspan=2>steerOffset</td>
            <td align=center>ackermanFix</td>
            <td align=center>CRC</td>
        </tr>
        <tr>
            <td align=center>Steer Config</td>
            <td align=center>7F</td>
            <td align=center>127</td>
            <td align=center>FB</td>
            <td align=center>251</td>
            <td align=center>8</td>
            <td align=center>set0</td>
            <td align=center>pulseCount</td>
            <td align=center>minSpeed</td>
            <td align=center>sett1</td>
            <td align=center>***</td>
            <td align=center>***</td>
            <td align=center>***</td>
            <td align=center>***</td>
            <td align=center>CRC</td>
        </tr>
        <tr>
            <td align=center>From AutoSteer</td>
            <td align=center>7E</td>
            <td align=center>126</td>
            <td align=center>FD</td>
            <td align=center>253</td>
            <td align=center>8</td>
            <td align=center colspan=2>ActualSteerAngle * 100</td>
            <td align=center colspan=2>IMU Heading Hi/Lo</td>
            <td align=center colspan=2>IMU Roll Hi/Lo</td>
            <td align=center>Switch</td>
            <td align=center>PWMDisplay</td>
            <td align=center>CRC</td>
        </tr>
        <tr>
            <td align=center>From Autosteer 2</td>
            <td align=center>7E</td>
            <td align=center>127</td>
            <td align=center>FA</td>
            <td align=center>250</td>
            <td align=center>8</td>
            <td align=center>Sensor Value</td>
            <td align=center>***</td>
            <td align=center>***</td>
            <td align=center>***</td>
            <td align=center>***</td>
            <td align=center>***</td>
            <td align=center>***</td>
            <td align=center>***</td>
            <td align=center>CRC</td>
        </tr>
    </tbody>
</table>

<br>
<br>

## Machine Module

IP = 192.168.5.123<br>
Hello = 123<br>
Port = 5123<br>
00 00 56 00 00 7B<br>

<table>
    <thead>
        <tr>
            <th nowrap align=center>PGN Name</th>
            <th nowrap align=center>Src</th>
            <th nowrap align=center>Dec</th>
            <th nowrap align=center>PGN</th>
            <th nowrap align=center>Dec</th>
            <th nowrap align=center>Len</th>
            <th nowrap align=center>Byte 5</th>
            <th nowrap align=center>Byte 6</th>
            <th nowrap align=center>Byte 7</th>
            <th nowrap align=center>Byte 8</th>
            <th nowrap align=center>Byte 9</th>
            <th nowrap align=center>Byte 10</th>
            <th nowrap align=center>Byte 11</th>
            <th nowrap align=center>Byte 12</th>
            <th nowrap align=center>Byte 13</th>
            <th nowrap align=center>Byte 14</th>
            <th nowrap align=center>Byte 15</th>
            <th nowrap align=center>Byte 16</th>
            <th nowrap align=center>Byte 17</th>
            <th nowrap align=center>Byte 18</th>
            <th nowrap align=center>Byte 19</th>
            <th nowrap align=center>Byte 20</th>
            <th nowrap align=center>Byte 21</th>
            <th nowrap align=center>Byte 22</th>
            <th nowrap align=center>Byte 23</th>
            <th nowrap align=center>Byte 24</th>
            <th nowrap align=center>Byte 25</th>
            <th nowrap align=center>Byte 26</th>
            <th nowrap align=center>Byte 27</th>
            <th nowrap align=center>Byte 28</th>
            <th nowrap align=center>Byte 29</th>
            <th nowrap align=center>Byte 30</th>
            <th nowrap align=center>Byte 31</th>
            <th nowrap align=center>Byte 32</th>
            <th nowrap align=center>Byte 33</th>
            <th nowrap align=center>Byte 34</th>
            <th nowrap align=center>Byte 35</th>
            <th nowrap align=center>Byte 36</th>
            <th nowrap align=center>Byte 37</th>
            <th nowrap align=center>Byte 38</th>
        </tr>
    </thead>
    <tbody>
        <tr>
            <td align=center>Machine Data</td>
            <td align=center>7F</td>
            <td align=center>127</td>
            <td align=center>EF</td>
            <td align=center>239</td>
            <td align=center>8</td>
            <td align=center>uturn</td>
            <td align=center>speed * 10</td>
            <td align=center>hydLift</td>
            <td align=center>Tram</td>
            <td align=center>Geo Stop</td>
            <td align=center>***</td>
            <td align=center>SC1to8</td>
            <td align=center>SC9to16</td>
            <td align=center>CRC</td>
        </tr>
        <tr>
            <td align=center>Machine Config</td>
            <td align=center>7F</td>
            <td align=center>127</td>
            <td align=center>EE</td>
            <td align=center>238</td>
            <td align=center>8</td>
            <td align=center>raiseTime</td>
            <td align=center>lowerTime</td>
            <td align=center>hydEnable</td>
            <td align=center>set0</td>
            <td align=center>User1</td>
            <td align=center>User2</td>
            <td align=center>User3</td>
            <td align=center>User4</td>
            <td align=center>CRC</td>
        </tr>
        <tr>
            <td align=center>Pin Config</td>
            <td align=center>7F</td>
            <td align=center>127</td>
            <td align=center>EC</td>
            <td align=center>236</td>
            <td align=center>24</td>
            <td align=center>1</td>
            <td align=center>2</td>
            <td align=center>3</td>
            <td align=center>4</td>
            <td align=center>5</td>
            <td align=center>6</td>
            <td align=center>7</td>
            <td align=center>8</td>
            <td align=center>9</td>
            <td align=center>10</td>
            <td align=center>11</td>
            <td align=center>12</td>
            <td align=center>13</td>
            <td align=center>14</td>
            <td align=center>15</td>
            <td align=center>16</td>
            <td align=center>17</td>
            <td align=center>18</td>
            <td align=center>19</td>
            <td align=center>20</td>
            <td align=center>21</td>
            <td align=center>22</td>
            <td align=center>23</td>
            <td align=center>24</td>
            <td align=center>CRC</td>
        </tr>
        <tr>
            <td align=center>SectionDimensions</td>
            <td align=center>7E</td>
            <td align=center>126</td>
            <td align=center>EB</td>
            <td align=center>235</td>
            <td align=center>33</td>
            <td align=center>1</td>
            <td align=center>1Hi</td>
            <td align=center>2</td>
            <td align=center>2Hi</td>
            <td align=center>3</td>
            <td align=center>3Hi</td>
            <td align=center>4</td>
            <td align=center>4Hi</td>
            <td align=center>5</td>
            <td align=center>5Hi</td>
            <td align=center>6</td>
            <td align=center>6Hi</td>
            <td align=center>7</td>
            <td align=center>7Hi</td>
            <td align=center>8</td>
            <td align=center>8Hi</td>
            <td align=center>9</td>
            <td align=center>9Hi</td>
            <td align=center>10</td>
            <td align=center>10Hi</td>
            <td align=center>11</td>
            <td align=center>11Hi</td>
            <td align=center>12</td>
            <td align=center>12Hi</td>
            <td align=center>13</td>
            <td align=center>13Hi</td>
            <td align=center>14</td>
            <td align=center>14Hi</td>
            <td align=center>15</td>
            <td align=center>15Hi</td>
            <td align=center>16</td>
            <td align=center>16Hi</td>
            <td align=center>NumSec</td>
            <td align=center>CRC</td>
        </tr>
        <tr>
            <td align=center>From Machine</td>
            <td align=center>7B</td>
            <td align=center>123</td>
            <td align=center>ED</td>
            <td align=center>237</td>
            <td align=center>8</td>
            <td align=center>1</td>
            <td align=center>2</td>
            <td align=center>3</td>
            <td align=center>4</td>
            <td align=center>*</td>
            <td align=center>?</td>
            <td align=center>?</td>
            <td align=center>?</td>
            <td align=center>CRC</td>
        </tr>
        <tr>
            <td align=center>64 sections</td>
            <td align=center>7F</td>
            <td align=center>127</td>
            <td align=center>E5</td>
            <td align=center>229</td>
            <td align=center>10</td>
            <td align=center>1to8</td>
            <td align=center>9to16</td>
            <td align=center>17to24</td>
            <td align=center>25to32</td>
            <td align=center>33to40</td>
            <td align=center>41to48</td>
            <td align=center>49to56</td>
            <td align=center>57to64</td>
            <td align=center>Lspeed</td>
            <td align=center>Rspeed</td>
            <td align=center>CRC</td>
        </tr>
    </tbody>
</table>


<br>
<br>

## IMU

IP = 192.168.5.121<br>
Hello = 121<br>
Port = 5121<br>
00 00 56 00 00 79<br>



<table>
    <thead>
        <tr>
            <th nowrap align=center>PGN Name</th>
            <th nowrap align=center>Src</th>
            <th nowrap align=center>Dec</th>
            <th nowrap align=center>PGN</th>
            <th nowrap align=center>Dec</th>
            <th nowrap align=center>Len</th>
            <th nowrap align=center>Byte 5</th>
            <th nowrap align=center>Byte 6</th>
            <th nowrap align=center>Byte 7</th>
            <th nowrap align=center>Byte 8</th>
            <th nowrap align=center>Byte 9</th>
            <th nowrap align=center>Byte 10</th>
            <th nowrap align=center>Byte 11</th>
            <th nowrap align=center>Byte 12</th>
            <th nowrap align=center>Byte 13</th>
        </tr>
    </thead>
    <tbody>
        <tr>
            <td align=center>From IMU</td>
            <td align=center>79</td>
            <td align=center>121</td>
            <td align=center>D3</td>
            <td align=center>211</td>
            <td align=center>8</td>
            <td align=center colspan=2>Heading</td>
            <td align=center colspan=2>Roll</td>
            <td align=center colspan=2>Gyro</td>
            <td align=center>0</td>
            <td align=center>0</td>
            <td align=center>CRC</td>
        </tr>
        <tr>
            <td align=center>IMU Disconnect</td>
            <td align=center>7C</td>
            <td align=center>124</td>
            <td align=center>D4</td>
            <td align=center>212</td>
            <td align=center>2</td>
            <td align=center>1</td>
            <td align=center>0</td>
            <td align=center>CRC</td>
        </tr>
    </tbody>
</table>


<br>
<br>

## GPS

IP = 192.168.5.124<br>
Hello = na<br>
Port = 5124<br>
00 00 56 00 00 7C<br>


<table>
    <thead>
        <tr>
            <th nowrap align=center>PGN Name</th>
            <th nowrap align=center>Src</th>
            <th nowrap align=center>Dec</th>
            <th nowrap align=center>PGN</th>
            <th nowrap align=center>Dec</th>
            <th nowrap align=center>Len</th>
            <th nowrap align=center>Byte 5</th>
            <th nowrap align=center>Byte 6</th>
            <th nowrap align=center>Byte 7</th>
            <th nowrap align=center>Byte 8</th>
            <th nowrap align=center>Byte 9</th>
            <th nowrap align=center>Byte 10</th>
            <th nowrap align=center>Byte 11</th>
            <th nowrap align=center>Byte 12</th>
            <th nowrap align=center>Byte 13</th>
            <th nowrap align=center>Byte 14</th>
            <th nowrap align=center>Byte 15</th>
            <th nowrap align=center>Byte 16</th>
            <th nowrap align=center>Byte 17</th>
            <th nowrap align=center>Byte 18</th>
            <th nowrap align=center>Byte 19</th>
            <th nowrap align=center>Byte 20</th>
            <th nowrap align=center>Byte 21</th>
            <th nowrap align=center>Byte 22</th>
            <th nowrap align=center>Byte 23</th>
            <th nowrap align=center>Byte 24</th>
            <th nowrap align=center>Byte 25</th>
            <th nowrap align=center>Byte 26</th>
            <th nowrap align=center>Byte 27</th>
            <th nowrap align=center>Byte 28</th>
            <th nowrap align=center>Byte 29</th>
            <th nowrap align=center>Byte 30</th>
            <th nowrap align=center>Byte 31</th>
            <th nowrap align=center>Byte 32</th>
            <th nowrap align=center>Byte 33</th>
            <th nowrap align=center>Byte 34</th>
            <th nowrap align=center>Byte 35</th>
            <th nowrap align=center>Byte 36</th>
            <th nowrap align=center>Byte 37</th>
            <th nowrap align=center>Byte 38</th>
            <th nowrap align=center>Byte 39</th>
            <th nowrap align=center>Byte 40</th>
            <th nowrap align=center>Byte 41</th>
            <th nowrap align=center>Byte 42</th>
            <th nowrap align=center>Byte 43</th>
            <th nowrap align=center>Byte 44</th>
            <th nowrap align=center>Byte 45</th>
            <th nowrap align=center>Byte 46</th>
            <th nowrap align=center>Byte 47</th>
            <th nowrap align=center>Byte 48</th>
            <th nowrap align=center>Byte 49</th>
            <th nowrap align=center>Byte 50</th>
            <th nowrap align=center>Byte 51</th>
            <th nowrap align=center>Byte 52</th>
            <th nowrap align=center>Byte 53</th>
            <th nowrap align=center>Byte 54</th>
            <th nowrap align=center>Byte 55</th>
            <th nowrap align=center>Byte 56</th>
        </tr>
    </thead>
    <tbody>
        <tr>
            <td align=center>Main Antenna</td>
            <td align=center>7C</td>
            <td align=center>124</td>
            <td align=center>D6</td>
            <td align=center>214</td>
            <td align=center>51</td>
            <td align=center colspan=8>Longitude</td>
            <td align=center colspan=8>Latitude</td>
            <td align=center colspan=4>Heading true dual</td>
            <td align=center colspan=4>Heading true</td>
            <td align=center colspan=4>Speed</td>
            <td align=center colspan=4>Roll</td>
            <td align=center colspan=4>Altitude</td>
            <td align=center colspan=2>Satellites tracked</td>
            <td align=center>Fix quality</td>
            <td align=center colspan=2>HDOP (x100)</td>
            <td align=center colspan=2>Age (x100)</td>
            <td align=center colspan=2>IMU heading</td>
            <td align=center colspan=2>IMU roll</td>
            <td align=center colspan=2>IMU pitch</td>
            <td align=center colspan=2>IMU yaw rate</td>
            <td align=center>CRC</td>
        </tr>
    </tbody>
</table>


<br>
<br>

## Tool GPS

IP = 192.168.5.125<br>
Hello = 125<br>
Port = 10000<br>
00 00 56 00 00 7D<br>


<table>
    <thead>
        <tr>
            <th nowrap align=center>PGN Name</th>
            <th nowrap align=center>Src</th>
            <th nowrap align=center>Dec</th>
            <th nowrap align=center>PGN</th>
            <th nowrap align=center>Dec</th>
            <th nowrap align=center>Len</th>
            <th nowrap align=center>Byte 5</th>
            <th nowrap align=center>Byte 6</th>
            <th nowrap align=center>Byte 7</th>
            <th nowrap align=center>Byte 8</th>
            <th nowrap align=center>Byte 9</th>
            <th nowrap align=center>Byte 10</th>
            <th nowrap align=center>Byte 11</th>
            <th nowrap align=center>Byte 12</th>
            <th nowrap align=center>Byte 13</th>
        </tr>
    </thead>
    <tbody>
        <tr>
            <td align=center>Tool Antenna</td>
            <td align=center>7D</td>
            <td align=center>125</td>
            <td align=center>D7</td>
            <td align=center>215</td>
            <td align=center></td>
            <td align=center>Tool Antenna</td>
        </tr>
    </tbody>
</table>


<br>
<br>

## GPS/IMU/WAS

IP = 192.168.5.122<br>
Hello = ?<br>
Port = 5122<br>
00 00 56 00 00 79<br>


<table>
    <thead>
        <tr>
            <th nowrap align=center>PGN Name</th>
            <th nowrap align=center>Src</th>
            <th nowrap align=center>Dec</th>
            <th nowrap align=center>PGN</th>
            <th nowrap align=center>Dec</th>
            <th nowrap align=center>Len</th>
            <th nowrap align=center>Byte 5</th>
            <th nowrap align=center>Byte 6</th>
            <th nowrap align=center>Byte 7</th>
            <th nowrap align=center>Byte 8</th>
            <th nowrap align=center>Byte 9</th>
            <th nowrap align=center>Byte 10</th>
            <th nowrap align=center>Byte 11</th>
            <th nowrap align=center>Byte 12</th>
            <th nowrap align=center>Byte 13</th>
        </tr>
    </thead>
    <tbody>
        <tr>
            <td align=center>ToAutosteer</td>
            <td align=center>79</td>
            <td align=center>122</td>
            <td align=center>F9</td>
            <td align=center>249</td>
            <td align=center>8</td>
            <td align=center>WAS_Lo</td>
            <td align=center>WAS_Hi</td>
        </tr>
    </tbody>
</table>


<br>
<br>

## Tool Steer

IP = 192.168.5.122<br>
Hello = 122<br>
Port = 5122<br>
00 00 56 00 00 7A<br>



<table>
    <thead>
        <tr>
            <th nowrap align=center>PGN Name</th>
            <th nowrap align=center>Src</th>
            <th nowrap align=center>Dec</th>
            <th nowrap align=center>PGN</th>
            <th nowrap align=center>Dec</th>
            <th nowrap align=center>Len</th>
            <th nowrap align=center>Byte 5</th>
            <th nowrap align=center>Byte 6</th>
            <th nowrap align=center>Byte 7</th>
            <th nowrap align=center>Byte 8</th>
            <th nowrap align=center>Byte 9</th>
            <th nowrap align=center>Byte 10</th>
            <th nowrap align=center>Byte 11</th>
            <th nowrap align=center>Byte 12</th>
            <th nowrap align=center>Byte 13</th>
        </tr>
    </thead>
    <tbody>
        <tr>
            <td align=center>Tool Steering</td>
            <td align=center>7F</td>
            <td align=center>127</td>
            <td align=center>E9</td>
            <td align=center>233</td>
            <td align=center>8</td>
            <td align=center>Low XTE</td>
            <td align=center>High XTE</td>
            <td align=center>Status</td>
            <td align=center>Low Veh XTE</td>
            <td align=center>High Veh XTE</td>
            <td align=center>Speed * 10</td>
            <td align=center>*</td>
            <td align=center>*</td>
            <td align=center>CRC</td>
        </tr>
        <tr>
            <td align=center>Tool Settings</td>
            <td align=center>7F</td>
            <td align=center>127</td>
            <td align=center>E7</td>
            <td align=center>231</td>
            <td align=center>8</td>
            <td align=center>Setting 0</td>
            <td align=center>*</td>
            <td align=center>*</td>
            <td align=center>*</td>
            <td align=center>*</td>
            <td align=center>*</td>
            <td align=center>*</td>
            <td align=center>*</td>
            <td align=center>CRC</td>
        </tr>
        <tr>
            <td align=center></td>
            <td align=center></td>
            <td align=center></td>
            <td align=center></td>
            <td align=center></td>
            <td align=center></td>
            <td align=center>Bit</td>
            <td align=center>0=Invert WAS</td>
            <td align=center>1=Invert Rel</td>
            <td align=center>2=Inv Steer</td>
            <td align=center>3=Convertor</td>
            <td align=center>4=Motor Drv</td>
            <td align=center>5=Danfoss</td>
        </tr>
        <tr>
            <td align=center></td>
            <td align=center></td>
            <td align=center></td>
            <td align=center></td>
            <td align=center></td>
            <td align=center></td>
            <td align=center></td>
            <td align=center>0=off</td>
            <td align=center>0=off</td>
            <td align=center>0=off</td>
            <td align=center>0=Differential</td>
            <td align=center>0=IBT2</td>
            <td align=center>0=off</td>
        </tr>
        <tr>
            <td align=center></td>
            <td align=center></td>
            <td align=center></td>
            <td align=center></td>
            <td align=center></td>
            <td align=center></td>
            <td align=center></td>
            <td align=center>1=inv</td>
            <td align=center>1=inv</td>
            <td align=center>1=inv</td>
            <td align=center>1=Single</td>
            <td align=center>1=Cytron</td>
            <td align=center>1=on</td>
        </tr>
        <tr>
            <td align=center>From  Tool Steer</td>
            <td align=center>7A</td>
            <td align=center>122</td>
            <td align=center>E6</td>
            <td align=center>230</td>
            <td align=center>8</td>
            <td align=center>Low Actual</td>
            <td align=center>High Actual</td>
            <td align=center>Low Error</td>
            <td align=center>High Error</td>
            <td align=center>Tool PWM</td>
            <td align=center>Status</td>
            <td align=center>*</td>
            <td align=center>*</td>
            <td align=center>CRC</td>
        </tr>
        <tr>
            <td align=center>Switch Control</td>
            <td align=center>77</td>
            <td align=center>119</td>
            <td align=center>EA</td>
            <td align=center>234</td>
            <td align=center>8</td>
            <td align=center>Main</td>
            <td align=center>Auto Group 0</td>
            <td align=center>Auto Group 1</td>
            <td align=center>#sections</td>
            <td align=center>On Group 0</td>
            <td align=center>Off Group 0</td>
            <td align=center>On Group 1</td>
            <td align=center>Off Group 1</td>
            <td align=center>CRC</td>
        </tr>
    </tbody>
</table>


<br>
<br>

## Hello Sent To Module

<table>
    <thead>
        <tr>
            <th nowrap align=center>PGN Name</th>
            <th nowrap align=center>Src</th>
            <th nowrap align=center>Dec</th>
            <th nowrap align=center>PGN</th>
            <th nowrap align=center>Dec</th>
            <th nowrap align=center>Len</th>
            <th nowrap align=center>Byte 5</th>
            <th nowrap align=center>Byte 6</th>
            <th nowrap align=center>Byte 7</th>
            <th nowrap align=center>Byte 8</th>
        </tr>
    </thead>
    <tbody>
        <tr>
            <td align=center>Hello</td>
            <td align=center>7F</td>
            <td align=center>127</td>
            <td align=center>C8</td>
            <td align=center>200</td>
            <td align=center>3</td>
            <td align=center>Module ID</td>
            <td align=center>0</td>
            <td align=center>0</td>
            <td align=center>CRC</td>
        </tr>
    </tbody>
</table>


<br>
<br>

## Hello Reply to AgIO

<table>
    <thead>
        <tr>
            <th nowrap align=center>PGN Name</th>
            <th nowrap align=center>Src</th>
            <th nowrap align=center>Dec</th>
            <th nowrap align=center>PGN</th>
            <th nowrap align=center>Dec</th>
            <th nowrap align=center>Len</th>
            <th nowrap align=center>Byte 5</th>
            <th nowrap align=center>Byte 6</th>
            <th nowrap align=center>Byte 7</th>
            <th nowrap align=center>Byte 8</th>
            <th nowrap align=center>Byte 9</th>
            <th nowrap align=center>Byte 10</th>
        </tr>
    </thead>
    <tbody>
        <tr>
            <td align=center>Steer Reply</td>
            <td align=center>7E</td>
            <td align=center>126</td>
            <td align=center>7E</td>
            <td align=center>126</td>
            <td align=center>5</td>
            <td align=center>AngleLo</td>
            <td align=center>AngleHi</td>
            <td align=center>CountsLo</td>
            <td align=center>CountsHi</td>
            <td align=center>Switchbyte</td>
            <td align=center>CRC</td>
        </tr>
        <tr>
            <td align=center>Steer Reply</td>
            <td align=center>7B</td>
            <td align=center>123</td>
            <td align=center>7B</td>
            <td align=center>123</td>
            <td align=center>5</td>
            <td align=center>relayLo</td>
            <td align=center>relayHi</td>
            <td align=center>*</td>
            <td align=center>*</td>
            <td align=center>*</td>
            <td align=center>CRC</td>
        </tr>
        <tr>
            <td align=center>IMU Reply</td>
            <td align=center>79</td>
            <td align=center>121</td>
            <td align=center>79</td>
            <td align=center>121</td>
            <td align=center>5</td>
            <td align=center>*</td>
            <td align=center>*</td>
            <td align=center>*</td>
            <td align=center>*</td>
            <td align=center>*</td>
            <td align=center>CRC</td>
        </tr>
        <tr>
            <td align=center>GPS Reply</td>
            <td align=center>78</td>
            <td align=center>120</td>
            <td align=center>78</td>
            <td align=center>120</td>
            <td align=center>5</td>
            <td align=center>*</td>
            <td align=center>*</td>
            <td align=center>*</td>
            <td align=center>*</td>
            <td align=center>*</td>
            <td align=center>CRC</td>
        </tr>
    </tbody>
</table>


<br>
<br>

## From AgIO

<table>
    <thead>
        <tr>
            <th nowrap align=center>PGN Name</th>
            <th nowrap align=center>Src</th>
            <th nowrap align=center>Dec</th>
            <th nowrap align=center>PGN</th>
            <th nowrap align=center>Dec</th>
            <th nowrap align=center>Len</th>
            <th nowrap align=center>Byte 5</th>
            <th nowrap align=center>Byte 6</th>
            <th nowrap align=center>Byte 7</th>
            <th nowrap align=center>Byte 8</th>
            <th nowrap align=center>Byte 9</th>
            <th nowrap align=center>Byte 10</th>
        </tr>
    </thead>
    <tbody>
        <tr>
            <td align=center>Subnet Change</td>
            <td align=center>7F</td>
            <td align=center>127</td>
            <td align=center>C9</td>
            <td align=center>201</td>
            <td align=center>5</td>
            <td align=center>201</td>
            <td align=center>201</td>
            <td align=center>IP_One</td>
            <td align=center>IP_Two</td>
            <td align=center>IP_Three</td>
            <td align=center>CRC</td>
        </tr>
        <tr>
            <td align=center>Scan request</td>
            <td align=center>7F</td>
            <td align=center>127</td>
            <td align=center>CA</td>
            <td align=center>202</td>
            <td align=center>3</td>
            <td align=center>202</td>
            <td align=center>202</td>
            <td align=center>5</td>
            <td align=center>CRC</td>
        </tr>
    </tbody>
</table>


<br>
<br>

## Subnet Reply to AgIO

<table>
    <thead>
        <tr>
            <th nowrap align=center>PGN Name</th>
            <th nowrap align=center>Src</th>
            <th nowrap align=center>Dec</th>
            <th nowrap align=center>PGN</th>
            <th nowrap align=center>Dec</th>
            <th nowrap align=center>Len</th>
            <th nowrap align=center>Byte 5</th>
            <th nowrap align=center>Byte 6</th>
            <th nowrap align=center>Byte 7</th>
            <th nowrap align=center>Byte 8</th>
            <th nowrap align=center>Byte 9</th>
            <th nowrap align=center>Byte 10</th>
            <th nowrap align=center>Byte 11</th>
            <th nowrap align=center>Byte 12</th>
        </tr>
    </thead>
    <tbody>
        <tr>
            <td align=center>Subnet Steer</td>
            <td align=center>7E</td>
            <td align=center>126</td>
            <td align=center>CB</td>
            <td align=center>203</td>
            <td align=center>7</td>
            <td align=center>IP_One</td>
            <td align=center>IP_Two</td>
            <td align=center>IP_Three</td>
            <td align=center>IP_Four</td>
            <td align=center>Subnet_One</td>
            <td align=center>Subnet_Two</td>
            <td align=center>Subnet_Three</td>
            <td align=center>CRC</td>
        </tr>
        <tr>
            <td align=center>Subnet Machine</td>
            <td align=center>7B</td>
            <td align=center>123</td>
            <td align=center>CB</td>
            <td align=center>203</td>
            <td align=center>7</td>
            <td align=center>IP_One</td>
            <td align=center>IP_Two</td>
            <td align=center>IP_Three</td>
            <td align=center>IP_Four</td>
            <td align=center>Subnet_One</td>
            <td align=center>Subnet_Two</td>
            <td align=center>Subnet_Three</td>
            <td align=center>CRC</td>
        </tr>
        <tr>
            <td align=center>Subnet IMU</td>
            <td align=center>79</td>
            <td align=center>121</td>
            <td align=center>CB</td>
            <td align=center>203</td>
            <td align=center>7</td>
            <td align=center>IP_One</td>
            <td align=center>IP_Two</td>
            <td align=center>IP_Three</td>
            <td align=center>IP_Four</td>
            <td align=center>Subnet_One</td>
            <td align=center>Subnet_Two</td>
            <td align=center>Subnet_Three</td>
            <td align=center>CRC</td>
        </tr>
        <tr>
            <td align=center>Subnet GPS</td>
            <td align=center>78</td>
            <td align=center>120</td>
            <td align=center>CB</td>
            <td align=center>203</td>
            <td align=center>7</td>
            <td align=center>IP_One</td>
            <td align=center>IP_Two</td>
            <td align=center>IP_Three</td>
            <td align=center>IP_Four</td>
            <td align=center>Subnet_One</td>
            <td align=center>Subnet_Two</td>
            <td align=center>Subnet_Three</td>
            <td align=center>CRC</td>
        </tr>
    </tbody>
</table>


<br>
<br>

## Hardware message

<table>
    <thead>
        <tr>
            <th nowrap align=center>PGN Name</th>
            <th nowrap align=center>Src</th>
            <th nowrap align=center>Dec</th>
            <th nowrap align=center>PGN</th>
            <th nowrap align=center>Dec</th>
            <th nowrap align=center>Len</th>
            <th nowrap align=center>Byte 5</th>
            <th nowrap align=center>Byte 6</th>
            <th nowrap align=center>...</th>
            <th nowrap align=center>Byte n-1</th>
        </tr>
    </thead>
    <tbody>
        <tr>
            <td align=center>Hardware Message</td>
            <td align=center>7F</td>
            <td align=center>127</td>
            <td align=center>DD</td>
            <td align=center>221</td>
            <td align=center></td>
            <td align=center>Duration [s]</td>
            <td align=center>Color</td>
            <td align=center>Message</td>
            <td align=center>CRC</td>
        </tr>
    </tbody>
</table>


<br>
<br>

## Nudge by machine

<table>
    <thead>
        <tr>
            <th nowrap align=center>PGN Name</th>
            <th nowrap align=center>Src</th>
            <th nowrap align=center>Dec</th>
            <th nowrap align=center>PGN</th>
            <th nowrap align=center>Dec</th>
            <th nowrap align=center>Len</th>
            <th nowrap align=center>Byte 5</th>
            <th nowrap align=center>Byte 6</th>
            <th nowrap align=center>Byte 7</th>
        </tr>
    </thead>
    <tbody>
        <tr>
            <td align=center>Nudge by Machine</td>
            <td align=center>7F</td>
            <td align=center>127</td>
            <td align=center>DE</td>
            <td align=center>222</td>
            <td align=center></td>
            <td align=center>Mask</td>
            <td align=center>Left/right</td>
            <td align=center>CRC</td>
        </tr>
    </tbody>
</table>


<br>
<br>

## Subnet Reply to AgIO

<table>
    <thead>
        <tr>
            <th nowrap align=center>Module</th>
            <th nowrap align=center>MAC</th>
            <th nowrap align=center>IP</th>
            <th nowrap align=center>IN</th>
            <th nowrap align=center>Dec</th>
            <th nowrap align=center>Out</th>
            <th nowrap align=center>Dec</th>
        </tr>
    </thead>
    <tbody>
        <tr>
            <td align=center>Steer Module</td>
            <td align=center>7E</td>
            <td align=center>126</td>
            <td align=center>FE</td>
            <td align=center>254</td>
            <td align=center>FD</td>
            <td align=center>253</td>
        </tr>
        <tr>
            <td align=center>Machine Module</td>
            <td align=center>7B</td>
            <td align=center>123</td>
            <td align=center>EF</td>
            <td align=center>239</td>
            <td align=center>ED</td>
            <td align=center>237</td>
        </tr>
        <tr>
            <td align=center>IMU Module</td>
            <td align=center>79</td>
            <td align=center>121</td>
            <td align=center></td>
            <td align=center></td>
            <td align=center>D3</td>
            <td align=center>211</td>
        </tr>
        <tr>
            <td align=center>GPS Module</td>
            <td align=center>7C</td>
            <td align=center>124</td>
            <td align=center></td>
            <td align=center></td>
            <td align=center></td>
            <td align=center></td>
        </tr>
    </tbody>
</table>
