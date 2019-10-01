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
            Thread thr1 = new Thread(StartServer); // создание отдельного потока
            thr1.IsBackground = true; // завершить поток при завершении основного потока (объявлять, если точно знаете, что вам это нужно, иначе поток завершится не выполнив свою работу до конца)
            thr1.Start(); // запуск потока
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
        public void timer1_Tick(object sender, EventArgs e) //async
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
                
               // HttpListenerContext listenerBeginContext = await listener.BeginGetContext(new AsyncCallback(AcceptCallback), listener);
                HttpListenerContext listenerContext = await listener.GetContextAsync();

               // listener.BeginGetContext(new AsyncCallback(AcceptCallback), listener);

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
        public static void AcceptCallback(IAsyncResult ar)
        {
            
        }



        //### Accepting WebSocket connections
        // Calling `AcceptWebSocketAsync` on the `HttpListenerContext` will accept the WebSocket connection, sending the required 101 response to the client
        // and return an instance of `WebSocketContext`. This class captures relevant information available at the time of the request and is a read-only 
        // type - you cannot perform any actual IO operations such as sending or receiving using the `WebSocketContext`. These operations can be 
        // performed by accessing the `System.Net.WebSocket` instance via the `WebSocketContext.WebSocket` property.        
        private async void ProcessRequest(HttpListenerContext listenerContext)
        {
            WebSocketContext webSocketContext = null;
            try
            {
                // When calling `AcceptWebSocketAsync` the negotiated subprotocol must be specified. This sample assumes that no subprotocol 
                // was requested. 
                webSocketContext = await listenerContext.AcceptWebSocketAsync(subProtocol: null);
                Interlocked.Increment(ref count);
                //Program.f1.richTextBox1.Text += "Processed: " + count + "\r\n";
                //Program.f1.richTextBox1.BeginInvoke(new Action(() => {
                //    Program.f1.richTextBox1.Text += "Processed: " + count + "\r\n";
                //}));              
            }
            catch (Exception e)
            {
                // The upgrade process failed somehow. For simplicity lets assume it was a failure on the part of the server and indicate this using 500.
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
                // Define a receive buffer to hold data received on the WebSocket connection. The buffer will be reused as we only need to hold on to the data
                // long enough to send it back to the sender.
                byte[] receiveBuffer = new byte[512];
                const int maxMessageSize = 512;
                // While the WebSocket connection remains open run a simple loop that receives data and sends it back.
                while (webSocket.State == WebSocketState.Open)
                {
                    // The first step is to begin a receive operation on the WebSocket. `ReceiveAsync` takes two parameters:
                    //
                    // * An `ArraySegment` to write the received data to. 
                    // * A cancellation token. In this example we are not using any timeouts so we use `CancellationToken.None`.
                    //
                    // `ReceiveAsync` returns a `Task<WebSocketReceiveResult>`. The `WebSocketReceiveResult` provides information on the receive operation that was just 
                    // completed, such as:                
                    //
                    // * `WebSocketReceiveResult.MessageType` - What type of data was received and written to the provided buffer. Was it binary, utf8, or a close message?                
                    // * `WebSocketReceiveResult.Count` - How many bytes were read?                
                    // * `WebSocketReceiveResult.EndOfMessage` - Have we finished reading the data for this message or is there more coming?
                    WebSocketReceiveResult receiveResult = await webSocket.ReceiveAsync(new ArraySegment<byte>(receiveBuffer), CancellationToken.None);

                    // The WebSocket protocol defines a close handshake that allows a party to send a close frame when they wish to gracefully shut down the connection.
                    // The party on the other end can complete the close handshake by sending back a close frame.
                    //
                    // If we received a close frame then lets participate in the handshake by sending a close frame back. This is achieved by calling `CloseAsync`. 
                    // `CloseAsync` will also terminate the underlying TCP connection once the close handshake is complete.
                    //
                    // The WebSocket protocol defines different status codes that can be sent as part of a close frame and also allows a close message to be sent. 
                    // If we are just responding to the client's request to close we can just use `WebSocketCloseStatus.NormalClosure` and omit the close message.
                    if (receiveResult.MessageType == WebSocketMessageType.Close)
                    {
                        await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None);
                    }
                    // This echo server can't handle text frames so if we receive any we close the connection with an appropriate status code and message.
                    else if (receiveResult.MessageType == WebSocketMessageType.Text)
                    {
                        //await webSocket.CloseAsync(WebSocketCloseStatus.InvalidMessageType, "Cannot accept text frame", CancellationToken.None);
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
                    // Otherwise we must have received binary data. Send it back by calling `SendAsync`. Note the use of the `EndOfMessage` flag on the receive result. This
                    // means that if this echo server is sent one continuous stream of binary data (with EndOfMessage always false) it will just stream back the same thing.
                    // If binary messages are received then the same binary messages are sent back.
                    else
                    {
                        await webSocket.SendAsync(new ArraySegment<byte>(receiveBuffer, 0, receiveResult.Count), WebSocketMessageType.Binary, receiveResult.EndOfMessage, CancellationToken.None);
                    }
                    // The echo operation is complete. The loop will resume and `ReceiveAsync` is called again to wait for the next data frame.
                }
            }
            catch (Exception e)
            {
                // Just log any exceptions to the console. Pretty much any exception that occurs when calling `SendAsync`/`ReceiveAsync`/`CloseAsync` is unrecoverable in that it will abort the connection and leave the `WebSocket` instance in an unusable state.
                Program.f1.richTextBox1.BeginInvoke(new Action(() => {
                    Program.f1.richTextBox1.Text += "Exception: " + e + "\r\n";
                }));
            }
            finally
            {
                // Clean up by disposing the WebSocket once it is closed/aborted.
                if (webSocket != null)
                    webSocket.Dispose();
            }
        }
    }

    // This extension method wraps the BeginGetContext / EndGetContext methods on HttpListener as a Task, using a helper function from the Task Parallel Library (TPL).
    // This makes it easy to use HttpListener with the C# 5 asynchrony features.
    public static class HelperExtensions
    {
        public static Task GetContextAsync(this HttpListener listener)
        {
            return Task.Factory.FromAsync<HttpListenerContext>(listener.BeginGetContext, listener.EndGetContext, TaskCreationOptions.None);
        }
    }
}
