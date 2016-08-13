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
using Microsoft.Kinect;
using System.IO.Ports;

namespace Kinesis
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        //Define variables
        private KinectSensor kinect = null;
        private SerialPort port = null;
        private readonly Brush trackedJointBrush = new SolidColorBrush(Color.FromArgb(255, 68, 192, 68));
        private readonly Brush inferredJointBrush = Brushes.Yellow;
        private const double JointThickness = 15;
        private readonly Pen trackedBonePen = new Pen(Brushes.Green, 6);
        private readonly Pen inferredBonePen = new Pen(Brushes.Gray, 6);
        private int frameReduction = 2;
        JointType[] elegible = {JointType.ElbowRight, JointType.ElbowLeft,
                JointType.WristRight, JointType.WristLeft};

        public MainWindow()
        {
            InitializeComponent();
            status.Text = "Welcome to Kinesis!";
            serialMonitor.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            //serialMonitor.AppendText("Serial monitor:\n");
            KinectSensors_StatusChanged(null, null);
            UpdatePortSelection(null, null);
            connectBtn.Click += SetUpSerialConnection;
            portUpdate.Click += UpdatePortSelection;
            disconnectBtn.Click += DisconnectPort;
            angleBtn.Click += UpdateAngle;
            if(port == null)
            {
                disconnectBtn.IsEnabled = false;
            }
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            kinect.Stop();
            if (port != null)
            {
                port.Close();
            }
        }

        private void KinectSensors_StatusChanged(object sender, StatusChangedEventArgs e)
        {
            if (KinectSensor.KinectSensors.Count == 0)
            {
                this.Close();
                MessageBox.Show("Closing Kinesis because it couldn't find any Kinect Sensors",
                    "No Kinect found");
            }
            else
            {
                kinect = KinectSensor.KinectSensors[0];
                try
                {
                    kinect.Start();
                    KinectSensor.KinectSensors.StatusChanged += KinectSensors_StatusChanged;
                    kinect.SkeletonStream.Enable();
                    kinect.ColorStream.Enable();
                    kinect.SkeletonFrameReady += Kinect_SkeletonFrameReady;
                    kinect.ColorFrameReady += Kinect_ColorFrameReady;
                    angleInput.Text = "" + kinect.ElevationAngle;
                }
                catch (Exception e1)
                {
                    Close();
                    MessageBox.Show(e1.Message, "Error!");
                }
            }
        }

        private void Kinect_ColorFrameReady(object sender, ColorImageFrameReadyEventArgs e)
        {
            ColorImageFrame frame = e.OpenColorImageFrame();

            if (frame != null && frame.FrameNumber % frameReduction == 0)
            {
                byte[] pixelData = new byte[frame.PixelDataLength];
                frame.CopyPixelDataTo(pixelData);

                camera.Source = BitmapSource.Create(
                    frame.Width, frame.Height, 96, 96, PixelFormats.Bgr32,
                    null, pixelData, frame.Width * 4);
            }
        }

        private void Kinect_SkeletonFrameReady(object sender, SkeletonFrameReadyEventArgs e)
        {
            SkeletonFrame frame = e.OpenSkeletonFrame();
            canvas.Children.Clear();
            if (frame != null)
            {
                Skeleton[] trackedSkeletons = new Skeleton[frame.SkeletonArrayLength];
                frame.CopySkeletonDataTo(trackedSkeletons);

                foreach(Skeleton s in trackedSkeletons)
                {
                    if(s.TrackingState == SkeletonTrackingState.Tracked)
                    {
                        drawJoints(s);
                        if (port != null && port.IsOpen)
                        {
                            Dictionary<string, Point> points = filterJoints(s); 
                            port.sendJointsData(points);
                        }
                        break;
                    }
                }
            }
        }

        private void drawJoints(Skeleton skeleton)
        {
            foreach (Joint joint in skeleton.Joints)
            {
                Brush drawBrush = null;

                if (joint.TrackingState == JointTrackingState.Tracked)
                {
                    drawBrush = trackedJointBrush;
                }
                else if (joint.TrackingState == JointTrackingState.Inferred)
                {
                    drawBrush = inferredJointBrush;
                }

                if (drawBrush != null)
                {
                    Ellipse e = new Ellipse();
                    Point p = kinect.SkeletonPointToScreen(joint.Position);
                    e.Fill = drawBrush;
                    e.Width = JointThickness;
                    e.Height = JointThickness;
                    canvas.Children.Add(e);
                    Canvas.SetTop(e, p.Y);
                    Canvas.SetLeft(e, p.X);
                }
            }
        }

        private Dictionary<string, Point> filterJoints(Skeleton s)
        {
            Dictionary<string, Point> selectedJoints = new Dictionary<string, Point>();

            foreach (Joint j in s.Joints)
            {
                if (elegible.Contains(j.JointType))
                {
                    selectedJoints.Add(j.JointType.ToString(), kinect.SkeletonPointToScreen(j.Position));
                }
            }

            return selectedJoints;
        }

        private void UpdateAngle(object sender, RoutedEventArgs e)
        {
            int angle = 0;
            bool result = int.TryParse(angleInput.Text, out angle);
            if (result)
            {
                status.Text = "Moving to " + angle + "...";
                var movement = kinect.TryToSetAngle(angle);
                if (port != null && port.IsOpen)
                {
                    port.Write(angle + "");

                }
            }
            else
            {
                MessageBox.Show("Elevation angle must be an integer between " +
                    kinect.MinElevationAngle + " and " + kinect.MaxElevationAngle,
                    "Error on input");
            }
        }

        private void UpdatePortSelection(object sender, RoutedEventArgs e)
        {
            portInput.Items.Clear();
            foreach (string serialPort in SerialPort.GetPortNames())
            {
                portInput.Items.Add(serialPort);
            }
        }

        private void SetUpSerialConnection(object sender, RoutedEventArgs e)
        {
            if (portInput.SelectedIndex >= 0)
            {
                try
                {
                    string selected = (string)portInput.Items.GetItemAt(portInput.SelectedIndex);
                    if (SerialPort.GetPortNames().Contains(selected))
                    {
                        if (port != null && port.IsOpen)
                        {
                            port.Close();
                        }
                        port = new SerialPort(selected, 9600);
                        port.Open();
                        disconnectBtn.IsEnabled = true;
                        connectBtn.IsEnabled = false;

                        status.Text = "Connected to " + port.PortName + " at " + port.BaudRate + "!";
                    }
                    else
                    {
                        throw new Exception("I'm not going to connect with this...");
                    }
                }
                catch (Exception e2)
                {
                    MessageBox.Show(e2.Message, "Connection Error");
                }
                
            }
        }

        private void DisconnectPort(object sender, RoutedEventArgs e)
        {
            if (port != null)
            {
                port.Close();
                status.Text = "No longer connected with " + port.PortName + ".";
                port = null;
                disconnectBtn.IsEnabled = false;
                connectBtn.IsEnabled = true;
            }
            else
            {
                status.Text = "No longer connected with a Serial Device.";
            }
        }
    }

    public static class KinesisExtensions
    {
        public static bool TryToSetAngle(this KinectSensor kinect, int angle)
        {
            try
            {
                kinect.ElevationAngle = angle;
                return true;
            }
            catch(Exception e)
            {
                MessageBox.Show(e.Message, "Elevation error");
                return false;
            }
        }

        public static Point SkeletonPointToScreen(this KinectSensor kinect, SkeletonPoint skelpoint)
        {
            DepthImagePoint depthPoint = kinect.CoordinateMapper.MapSkeletonPointToDepthPoint(skelpoint, DepthImageFormat.Resolution640x480Fps30);
            return new Point(depthPoint.X, depthPoint.Y);
        }
    }

    public static class SerialPortExtensions
    {
        public static string sendJointsData(this SerialPort p, Dictionary<string, Point> points)
        {
            string message = "";
            foreach (string key in points.Keys)
            {
                message += key + " " + points[key].X + " " + points[key].Y + "\n";
            }
            p.Write(message);
            return p.ReadExisting();
        }   
    }
}
