using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

using System.Threading;

using System.Web;
      
using System.Net;
using System.Net.WebSockets;

using InTheHand.Net.Bluetooth;
using InTheHand.Net.Sockets;

using System.IO;

namespace CAR
{
    public partial class Form1 : Form
    {
        BluetoothClient bc = new BluetoothClient();
        BluetoothDeviceInfo[] info = null;
        BluetoothDeviceInfo selectedDevice;
        public Stream peerStream;
        int tickTime;
        public String dialCmd;

        public posXY pos = new posXY();

        public bool isWdown; //вперед
        public bool isAdown; // влево
        public bool isSdown; // назад
        public bool isDdown; // вправо
        public string inputText;

        public Form1()
        {
            Program.f1 = this;
            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            richTextBox1.Text += "Запуск сервера" + "\r\n";
            Thread thr1 = new Thread(StartServer); 
            thr1.IsBackground = true; 
            thr1.Start();
        }

        public void StartServer()
        {
            var server = new Server();
            server.Start("http://ip/port/");   //server.Start("http://+:8999/");
        }

        private void richTextBox1_TextChanged(object sender, EventArgs e)
        {
            richTextBox1.SelectionStart = richTextBox1.Text.Length;
            richTextBox1.ScrollToCaret();
        }
        private void button3_Click(object sender, EventArgs e)
        {

        }
        private void button2_Click(object sender, EventArgs e)
        {
            this.Enabled = false;
            if (!BluetoothRadio.IsSupported)
                MessageBox.Show("No Bluetooth device detected.");
            if (BluetoothRadio.PrimaryRadio.Mode == RadioMode.PowerOff)
                BluetoothRadio.PrimaryRadio.Mode = RadioMode.Connectable;
            label1.Text = BluetoothRadio.PrimaryRadio.Name.ToString() + " " + BluetoothRadio.PrimaryRadio.Mode.ToString();
            info = bc.DiscoverDevices(999);
            foreach (BluetoothDeviceInfo device in info)
            {
                listBox1.Items.Add(device.DeviceName + " - " + device.DeviceAddress);
            }
            listBox1.Items.Add("--END--");
            this.Enabled = true;
        }
        private void listBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            button2.Enabled = true;
            selectedDevice = info[listBox1.SelectedIndex];
            if (MessageBox.Show(String.Format("Would you like to attempt to pair with {0}?", selectedDevice.DeviceName), "Pair Device", MessageBoxButtons.YesNo) == DialogResult.Yes)
            {
                if (BluetoothSecurity.PairRequest(selectedDevice.DeviceAddress, "1234"))
                {
                    listBox1.Items.Add("paired with " + selectedDevice.DeviceName);
                    timer1.Enabled = true;
                    InTheHand.Net.BluetoothEndPoint ep = new InTheHand.Net.BluetoothEndPoint(selectedDevice.DeviceAddress, BluetoothService.SerialPort);
                    BluetoothClient cli = new BluetoothClient();
                    cli.Connect(ep);
                    peerStream = cli.GetStream();
                }
                else
                {
                    listBox1.Items.Add("Failed to pair with " + selectedDevice.DeviceName);
                }
            }
        }
        private void PairBluetoothTask()
        {
            selectedDevice = info[listBox1.SelectedIndex];
            if (BluetoothSecurity.PairRequest(selectedDevice.DeviceAddress, null))
            {
                MessageBox.Show("We paired!");
            }else{
                MessageBox.Show("Failed to pair!");
            }
        }
        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            try{peerStream.Close();}catch{}
        }
        public void timer1_Tick(object sender, EventArgs e) 
        {

            if (isWdown) { pos.ChangeY = 40; }
            if (isSdown) { pos.ChangeY = -40; }
            if (isAdown) { pos.ChangeX = -40; pos.ChangeY = 0; }
            if (isDdown) { pos.ChangeX = 40; pos.ChangeY = 0; }
            if (isWdown | isSdown | isAdown | isDdown)
            {
                dialCmd = string.Format("${0} {1};", pos.posX.ToString(), pos.posY.ToString());
            }
            else
            {
                pos.ChangeX = 0; pos.ChangeY = 0;
                dialCmd = "$0 0;";
            }
            Byte[] dcB = System.Text.Encoding.ASCII.GetBytes(dialCmd);
            peerStream.Write(dcB, 0, dcB.Length);
            listBox2.Items.Add(dialCmd.ToString());

            Program.f1.isWdown = false;
            Program.f1.isAdown = false;
            Program.f1.isSdown = false;
            Program.f1.isDdown = false;
            tickTime++;
        }

    }
    public class posXY
    {
        public int posX, posY;
        public posXY() {
            posX = 0;
            posY = 0;
        }
        private int value;
        public int ChangeX
        {
            get { return value; }
            set
            {
                this.posX = value;
                if (this.posX < -40) { this.posX = -40; }
                if (this.posX > 40) { this.posX = 40; }
            }
        }
        public int ChangeY
        {
            get { return value; }
            set
            {
                this.posY = value;
                if (this.posY < -40) { this.posY = -40; }
                if (this.posY > 40) { this.posY = 40; }
            }
        }
    }

    class Server
    {
        private int count = 0;

        //### Starting the server        
        // Using HttpListener is reasonably straightforward. Start the listener and run a loop that receives and processes incoming WebSocket connections.
        // Each iteration of the loop "asynchronously waits" for the next incoming request using the `GetContextAsync` extension method (defined below).             
        // If the request is for a WebSocket connection then pass it on to `ProcessRequest` - otherwise set the status code to 400 (bad request). 
        public async void Start(string listenerPrefix)
        {
            HttpListener listener = new HttpListener();
            listener.Prefixes.Add(listenerPrefix);
            listener.Start();
            Program.f1.richTextBox1.BeginInvoke(new Action(() => {
                Program.f1.richTextBox1.Text +=  "Listening...\r\n";
            }));
            
            while (true)
            {
                HttpListenerContext listenerContext = await listener.GetContextAsync();
                if (listenerContext.Request.IsWebSocketRequest)
                {
                    ProcessRequest(listenerContext);
                }
                else
                {
                    listenerContext.Response.StatusCode = 400;
                    listenerContext.Response.Close();
                }
            }
        }
      
        private async void ProcessRequest(HttpListenerContext listenerContext)
        {
            WebSocketContext webSocketContext = null;
            try
            {
                webSocketContext = await listenerContext.AcceptWebSocketAsync(subProtocol: null);
                Interlocked.Increment(ref count);            
            }
            catch (Exception e)
            {
                listenerContext.Response.StatusCode = 500;
                listenerContext.Response.Close();
                Program.f1.richTextBox1.BeginInvoke(new Action(() => {
                    Program.f1.richTextBox1.Text += "Exception: " + e + "\r\n";
                }));
                return;
            }
            WebSocket webSocket = webSocketContext.WebSocket;
            try
            {
                //### Receiving
                byte[] receiveBuffer = new byte[512];
                const int maxMessageSize = 512;
                while (webSocket.State == WebSocketState.Open)
                {
                    WebSocketReceiveResult receiveResult = await webSocket.ReceiveAsync(new ArraySegment<byte>(receiveBuffer), CancellationToken.None);
                    if (receiveResult.MessageType == WebSocketMessageType.Close)
                    {
                        await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None);
                    }
                    else if (receiveResult.MessageType == WebSocketMessageType.Text)
                    {
                        int count = receiveResult.Count;
                        while (receiveResult.EndOfMessage == false)
                        {
                            if (count >= maxMessageSize)
                            {
                                string closeMessage = string.Format("Maximum message size: {0} bytes.", maxMessageSize);
                                await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, closeMessage, CancellationToken.None);
                                return;
                            }
                            receiveResult = await webSocket.ReceiveAsync(new ArraySegment<byte>(receiveBuffer, count, maxMessageSize - count), CancellationToken.None);
                            count += receiveResult.Count;
                        }

                        var receivedString = Encoding.UTF8.GetString(receiveBuffer, 0, count);
                        string inputText = receivedString.ToString();
                        var echoString = "echo "+receivedString;
                        ArraySegment<byte> outputBuffer = new ArraySegment<byte>(Encoding.UTF8.GetBytes(echoString));
                        await webSocket.SendAsync(outputBuffer, WebSocketMessageType.Text, true, CancellationToken.None);

                        Program.f1.richTextBox1.BeginInvoke(new Action(() => {
                            Program.f1.richTextBox1.Text += echoString + "\r\n";
                            if (inputText[1].ToString() == "1") { Program.f1.isWdown = true; } else { Program.f1.isWdown = false; }
                            if (inputText[3].ToString() == "1") { Program.f1.isAdown = true; } else { Program.f1.isAdown = false; }
                            if (inputText[5].ToString() == "1") { Program.f1.isSdown = true; } else { Program.f1.isSdown = false; }
                            if (inputText[7].ToString() == "1") { Program.f1.isDdown = true; } else { Program.f1.isDdown = false; }
                        }));

                    }
                    else
                    {
                        await webSocket.SendAsync(new ArraySegment<byte>(receiveBuffer, 0, receiveResult.Count), WebSocketMessageType.Binary, receiveResult.EndOfMessage, CancellationToken.None);
                    }
                }
            }
            catch (Exception e)
            {
                Program.f1.richTextBox1.BeginInvoke(new Action(() => {
                    Program.f1.richTextBox1.Text += "Exception: " + e + "\r\n";
                }));
            }
            finally
            {
                if (webSocket != null)
                    webSocket.Dispose();
            }
        }
    }
    public static class HelperExtensions
    {
        public static Task GetContextAsync(this HttpListener listener)
        {
            return Task.Factory.FromAsync<HttpListenerContext>(listener.BeginGetContext, listener.EndGetContext, TaskCreationOptions.None);
        }
    }
}
