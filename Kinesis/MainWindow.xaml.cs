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
using System.Net;
using System.Net.Sockets;
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
        private bool tcp = true;
        private readonly Brush trackedJointBrush = new SolidColorBrush(Color.FromArgb(255, 68, 192, 68));
        private readonly Brush inferredJointBrush = Brushes.Yellow;
        private const double JointThickness = 15;
        private readonly Pen trackedBonePen = new Pen(Brushes.Green, 6);
        private readonly Pen inferredBonePen = new Pen(Brushes.Gray, 6);
        private int frameReduction = 2;
        private string[] elegible = {"ElbowRight", "ElbowLeft", "WristRight", "WristLeft"};

        public MainWindow()
        {
            InitializeComponent();
            status.Text = "Welcome to Kinesis!";
            serialMonitor.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            KinectSensors_StatusChanged(null, null);
            angleBtn.Click += UpdateAngle;
            requestBtn.Click += RequestPermittedJoints;
            updatePorts.Click += UpdateSerialSelection;
            UpdateSerialSelection(null, null);
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            if(kinect != null)
                kinect.Stop();
        }

        private void UpdateSerialSelection(object sender, RoutedEventArgs e)
        {
            serialMonitor.Text = "";
            foreach(string port in SerialPort.GetPortNames())
            {
                serialMonitor.AppendText(port + "\n");
            }
        }

        private void RequestPermittedJoints(object sender, RoutedEventArgs e)
        {
            int port = 0;
            bool ok = int.TryParse(portInput.Text, out port);
            if(ok)
            {
                TcpClient socket = new TcpClient(ipInput.Text, port);
                NetworkStream stream = socket.GetStream();

                byte[] response = new byte[1024];

                stream.Read(response, 0, response.Length);
                MessageBox.Show(Encoding.ASCII.GetString(response));

                stream.Close();
                socket.Close();
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

        private void SendDataToServer(Skeleton s)
        {
            int port = 0;
            bool ok = int.TryParse(portInput.Text, out port);
            if(ok)
            {
                TcpClient socket = new TcpClient(ipInput.Text, port);
                NetworkStream stream = socket.GetStream();
                Dictionary<string, Point> joints = filterJoints(s);
                string content = "";

                foreach(string key in joints.Keys)
                {
                    content += "{type:\"" + key + "\", x:" + joints[key].X + ", y:" + joints[key].Y + "};";
                }

                byte[] byteForm = Encoding.ASCII.GetBytes(content);
                stream.Write(byteForm, 0, byteForm.Length);
                stream.Close();
                socket.Close();
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

                foreach (Skeleton s in trackedSkeletons)
                {
                    if (s.TrackingState == SkeletonTrackingState.Tracked)
                    {
                        drawJoints(s);
                        if(tcp)
                        {
                            SendDataToServer(s);
                            tcp = false;
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
                if (elegible.Contains(j.JointType.ToString()))
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
            }
            else
            {
                MessageBox.Show("Elevation angle must be an integer between " +
                    kinect.MinElevationAngle + " and " + kinect.MaxElevationAngle,
                    "Error on input");
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
}
