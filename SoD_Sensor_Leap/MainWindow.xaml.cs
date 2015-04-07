using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Diagnostics;

// Use Leap
using Leap;


// Sockets related DLLs
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Threading;
using System.Text.RegularExpressions;
using SocketIOClient;
using SocketIOClient.Messages;
using System.Net;

namespace SoD_Sensor_Leap
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>

        public partial class MainWindow : Window, ILeapEventDelegate
        {
            #region Global Local Variables & Constants
            private Controller controller = new Controller();
            private LeapEventListener listener;
            private Boolean isClosing = false;

            private string leapID;                  // Client ID
            private static Client socket;           // socket object


            private const String IP_REGEX = @"\b\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}\b";
            private const String URL_REGEX = @"^(http|https|ftp|)\://|[a-zA-Z0-9\-\.]+\.[a-zA-Z](:[a-zA-Z0-9]*)?/?([a-zA-Z0-9\-\._\?\,\'/\\\+&amp;%\$#\=~])*[^\.\,\)\(\s]$";
            private const String PORT_REGEX = @"^(4915[0-1]|491[0-4]\d|490\d\d|4[0-8]\d{3}|[1-3]\d{4}|[1-9]\d{0,3}|0)$";
            #endregion

            public MainWindow()
            {
                InitializeComponent();

                IPHostEntry host;
                string localIP = "?";
                host = Dns.GetHostEntry(Dns.GetHostName());
                foreach (IPAddress ip in host.AddressList)
                {
                    if (ip.AddressFamily.ToString() == "InterNetwork")
                    {
                        localIP = ip.ToString();
                    }
                }
                ServerTextBox.Text = localIP;

               
                
                this.controller = new Controller();
                this.listener = new LeapEventListener(this);
                controller.AddListener(listener);




            }

            private void Window_Loaded(object sender, RoutedEventArgs e)
            {
                leapID = System.Environment.MachineName;
                NameTextBox.Text = leapID;

                /*try
                {
                    if (!TestKinect2Availability())
                    {
                        //no Kinects or Kinect2s connected
                        Application.Current.Shutdown(0);
                    }
                    else
                    {
                        InitializeKinect2();
                    }
                }
                catch (MissingMethodException)
                {
                    MessageBox.Show("Missing method exception, most likely due to no Kinect2 being plugged in.");
                    Application.Current.Shutdown(0);
                }
                */

                StatusSubmit.Click += new RoutedEventHandler(StatusSubmit_Click);
            }

            void StatusSubmit_Click(object sender, RoutedEventArgs e)
            {
                if (!isValidInput()) { return; }

                if (StatusSubmit.Content.Equals("Connect")) //Users wants to connect
                {
                    //Connect to server//      
                    string address = "http://" + ServerTextBox.Text + ":" + PortTextBox.Text + "/";
                    Console.WriteLine("Connecting to: " + address);
                    socket = new Client(address);
                    socket.Connect();

                    //Debug, check if sensor still exists
                    Console.WriteLine("Sensor: Leap");
                    ///////////////////////////////////////
                    TimeSpan maxDuration = TimeSpan.FromMilliseconds(1000);
                    Stopwatch sw = Stopwatch.StartNew();

                    while (sw.Elapsed < maxDuration && !socket.IsConnected)
                    {
                        // wait for connected
                    }

                    if (socket.IsConnected)
                    {
                        StatusSubmit.Content = "Disconnect";
                        StatusLabel.Text = "Connected";

                        if (TellServerAboutSensor())
                        {
                            //sensor registered with server
                        }
                        else
                        {
                            //no sensor was registered with server!
                        }
                        //socket.Message += new EventHandler<MessageEventArgs>(socket_Message);
                        SubscribeToRoutes(socket);
                    }
                    else
                    {
                        Console.WriteLine("Device never registered with server!");
                    }
                }
                else //Users wants to disconnect
                {
                    socket.Close();
                    if (!socket.IsConnected) //replace true with condition for successful disconnect
                    {
                        //disconnected, cleanup
                        StatusSubmit.Content = "Connect";
                        StatusLabel.Text = "Disconnected";
                    }
                    else
                    {
                        MessageBox.Show("Server failed to disconnect properly.");
                    }
                }
            }
            public void SubscribeToRoutes(Client socket)
            {
                socket.On("connect", (fn) =>
                {
                    Console.WriteLine("\r\nConnected ...\r\n");
                    TellServerAboutSensor();
                });
            }
            private bool TellServerAboutSensor()
            {
                if (true)
                {
                    socket.Emit("registerSensor", new RegisterCapsule("leapMotion"));
                    Console.WriteLine("registered Leap with server");
                    return true;
                }
                else
                {
                    //no sensor available
                    return false;
                }
            }

            private bool isValidInput()
            {
                if (!Regex.IsMatch(ServerTextBox.Text, IP_REGEX) && !Regex.IsMatch(ServerTextBox.Text, URL_REGEX))
                {
                    MessageBox.Show("\"" + ServerTextBox.Text + "\" is an invalid server address!", "Invalid Input");
                    return false;
                }
                else if (!Regex.IsMatch(PortTextBox.Text, PORT_REGEX))
                {
                    MessageBox.Show("\"" + PortTextBox.Text + "\" is an invalid port number!", "Invalid Input");
                    return false;
                }

                return true;
            }

            delegate void LeapEventDelegate(string EventName);

            
            public void LeapEventNotification(string EventName)
            {
                if (this.CheckAccess())
                {
                    switch (EventName)
                    {
                        case "onInit":
                            Debug.WriteLine("Init");
                            break;
                        case "onExit":
                            Debug.WriteLine("Exit");
                            break;
                        case "onConnect":
                            this.connectHandler();
                            Debug.WriteLine("Connect");
                            break;
                        case "onDisconnect":
                            Debug.WriteLine("Disconnect");
                            break;
                        case "onDeviceChange":
                            Debug.WriteLine("DeviceChange");
                            break;
                        case "onServiceConnect":
                            Debug.WriteLine("ServiceConnect");
                            break;
                        case "onServiceDisconnect":
                            Debug.WriteLine("ServiceDisconnect");
                            break;
                        
                        case "onFrame":
                            if (!this.isClosing)
                                this.newFrameHandler(this.controller.Frame());                
                            break;
                    }
                }
                else
                {
                    Dispatcher.Invoke(new LeapEventDelegate(LeapEventNotification), new object[] { EventName });
                }
            }

            void connectHandler()
            {
                //this.controller.SetPolicy(Controller.PolicyFlag.POLICY_IMAGES);
                //this.controller.EnableGesture(Gesture.GestureType.TYPE_SWIPE);
                //this.controller.Config.SetFloat("Gesture.Swipe.MinLength", 100.0f);
            }

            void newFrameHandler(Leap.Frame frame)
            {
                string whichHand = null;
                
                HandList handsinFrame= frame.Hands;
                GestureList gestureinFrame= frame.Gestures();

                if (handsinFrame.IsEmpty == true)
                {
                    displayHandsID1.Text = "0";
                    displayHandsID2.Text = "0";
                    whichHand = "no hand";
                    displayWhichHand1.Text = whichHand;
                    displayWhichHand2.Text = whichHand;
                    whichHand = null;
                }

                else if (handsinFrame.Count >= 1)
                {

                    displayHandsID1.Text = handsinFrame[0].Id.ToString();
                  //  displayGestureType1.Text = gestureinFrame[0].Type.ToString();
                    if (handsinFrame[0].IsLeft == true)
                        whichHand = "left";
                    else
                        whichHand = "right";
                    displayWhichHand1.Text = whichHand;
                    whichHand = null;
                    if (handsinFrame.Count > 1)
                    {

                            displayHandsID2.Text = handsinFrame[1].Id.ToString();
                          //  displayGestureType2.Text = gestureinFrame[1].Type.ToString();
                            if (handsinFrame[1].IsLeft == true)
                                whichHand = "left";
                            else
                                whichHand = "right";
                            displayWhichHand2.Text = whichHand;
                            whichHand = null;
                        
                    }
                    else
                    {
                        displayHandsID2.Text = "0";
                        whichHand = "no hand";
                        displayWhichHand2.Text = whichHand;
                        whichHand = null;
                    }
                }


                if (gestureinFrame[0].Hands[0].Id == handsinFrame[0].Id)
                {
                    displayGestureType1.Text = gestureinFrame[0].Type.ToString();
                    displayGestureType2.Text = gestureinFrame[1].Type.ToString();
                }

                else if (gestureinFrame[0].Hands[0].Id == handsinFrame[1].Id)
                {
                    displayGestureType2.Text = gestureinFrame[0].Type.ToString();
                    displayGestureType1.Text = gestureinFrame[1].Type.ToString();
                }
                    

                displayID.Text = frame.Id.ToString();
                displayFingerCount.Text = frame.Fingers.Count.ToString();
                displayHandCount.Text = frame.Hands.Count.ToString();
                displayGestureCount.Text = frame.Gestures().Count.ToString();

                
                /*for (int g = 0; g < frame.Gestures().Count; g++)
                {
                    //displayGestureType.Text = frame.Gestures()[g].GetType().ToString();
                    switch (frame.Gestures()[g].Type)
                    {
                        case Gesture.GestureType.TYPE_CIRCLE:
                            //Handle circle gestures
                            Debug.WriteLine("circle"+frame.Id);
                            displayGestureType.Text = "circle";
                            break;
                        case Gesture.GestureType.TYPE_KEY_TAP:
                            //Handle key tap gestures
                            Debug.WriteLine("key_tap" + frame.Id);
                            displayGestureType.Text = "key_tap";
                            break;
                        case Gesture.GestureType.TYPE_SCREEN_TAP:
                            //Handle screen tap gestures
                            Debug.WriteLine("screen_tap" + frame.Id);
                            displayGestureType.Text = "screen_tap";
                            break;
                        case Gesture.GestureType.TYPE_SWIPE:
                            //Handle swipe gestures
                            Debug.WriteLine("swipe" + frame.Id);
                            displayGestureType.Text = "swipe";
                            break;
                        default:
                            //Handle unrecognized gestures
                            break;
                    }
                }
                
              
                 
                for (int g = 0; g < frame.Gestures().Count; g++)
                {
                    Console.WriteLine(frame.Gestures()[g].Type);
                    displayGestureType1.Text = frame.Gestures()[g].Type;
                }  */


            }

            void MainWindow_Closing(object sender, EventArgs e)
            {
                this.isClosing = true;
                this.controller.RemoveListener(this.listener);
                this.controller.Dispose();
            }


        }

        public interface ILeapEventDelegate
        {
            void LeapEventNotification(string EventName);
        }

        public class LeapEventListener : Listener
        {
            ILeapEventDelegate eventDelegate;

            private Object thisLock= new Object();


            private void SafeWriteLine(String line)
            {
                lock (thisLock)
                { Console.WriteLine(line); }
            }

            public LeapEventListener(ILeapEventDelegate delegateObject)
            {
                this.eventDelegate = delegateObject;
            }

            //Init,Exit
            public override void OnInit(Controller controller)
            {
                this.eventDelegate.LeapEventNotification("onInit");
            }
            public override void OnExit(Controller controller)
            {
                this.eventDelegate.LeapEventNotification("onExit");
            }


            //Connect, Disconnect, DeviceChange
            public override void OnConnect(Controller controller)
            {
                controller.SetPolicy(Controller.PolicyFlag.POLICY_IMAGES);
                controller.EnableGesture(Gesture.GestureType.TYPE_SWIPE);
                controller.EnableGesture(Gesture.GestureType.TYPE_CIRCLE);
                controller.EnableGesture(Gesture.GestureType.TYPE_KEY_TAP);
                controller.EnableGesture(Gesture.GestureType.TYPE_SCREEN_TAP);
                this.eventDelegate.LeapEventNotification("onConnect");
            }
            public override void OnDeviceChange(Controller controller)
            {
                this.eventDelegate.LeapEventNotification("onDeviceChange");
            }
            public override void OnDisconnect(Controller controller)
            {
                this.eventDelegate.LeapEventNotification("onDisconnect");
            }


            //ServiceConnect, ServiceDisconnect
            public override void OnServiceConnect(Controller controller)
            {
                this.eventDelegate.LeapEventNotification("onServiceConnect");
            }
            public override void OnServiceDisconnect(Controller controller)
            {
                this.eventDelegate.LeapEventNotification("onServiceDisconnect");
            }

            //FocusGained, FocusLost
            public override void OnFocusGained(Controller controller)
            {
                this.eventDelegate.LeapEventNotification("onFocusGained");
            }
            public override void OnFocusLost(Controller controller)
            {
                this.eventDelegate.LeapEventNotification("onFocusLost");
            }

            //Frame
            public override void OnFrame(Controller controller)
            {
                this.eventDelegate.LeapEventNotification("onFrame");
               // SafeWriteLine("Frame");
              
               // Leap.Frame frame = controller.Frame();
               // Debug.WriteLine("Frame id: " + frame.Id
               //         + ", timestamp: " + frame.Timestamp
               //          + ", hands: " + frame.Hands.Count
               //          + ", fingers: " + frame.Fingers.Count
               //          + ", tools: " + frame.Tools.Count
               //          + ", gestures: " + frame.Gestures().Count);

            }


        }

        public class RegisterCapsule
        {
            public RegisterCapsule(string sensorType)
            {
                this.sensorType = sensorType;
     
                if (sensorType == "leapMotion")
                {
                    Console.WriteLine("We got a Leap!!");
     
                }
            }
            public string sensorType;

        }

        public class updateHands
        {
            public updateHands(int handID, string whichHand, string gestureType)
            {
                this.handID = handID;
                this.whichHand = whichHand;
                this.gestureType = gestureType;
            }
            public int handID;
            public string whichHand;


            public string gestureType;


        }

        /*public partial class MainWindow : Window
        {
            public MainWindow()
            {
                InitializeComponent();
                // Init Controller
                try{
                    SampleListener listener = new SampleListener();
                    Leap.Controller controller = new Leap.Controller();
                    controller.AddListener(listener);
                    Console.WriteLine("haha");
                    System.Threading.Thread.Sleep(15000);

                    Console.WriteLine("Press Enter to quit...");
                    Console.ReadLine();

                    // dispose controller and listener process
                    controller.RemoveListener(listener);
                    controller.Dispose();
                }
                catch (Exception e){
                    Console.WriteLine("Exception yo: "+e);
                }

            


            
            }     
        }
        class SampleListener : Leap.Listener
        {
       
            private Object thisLock = new Object();
        
            private void SafeWriteLine(String line)
            {
                lock (thisLock)
                {
                    Console.WriteLine(line);
                }
            }

            public override void OnConnect(Controller controller)
            {
                SafeWriteLine("Connected");
            }


            public override void OnFrame(Controller controller)
            {
                SafeWriteLine("Frame available");
                Leap.Frame frame = controller.Frame();

                SafeWriteLine("Frame id: " + frame.Id
                         + ", timestamp: " + frame.Timestamp
                         + ", hands: " + frame.Hands.Count
                         + ", fingers: " + frame.Fingers.Count
                         + ", tools: " + frame.Tools.Count
                         + ", gestures: " + frame.Gestures().Count);

            }
        
        }*/
    
}
