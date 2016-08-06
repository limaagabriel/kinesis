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
        private KinectSensor kinect = null;
        private SerialPort port = null;
        private readonly Brush trackedJointBrush = new SolidColorBrush(Color.FromArgb(255, 68, 192, 68));
        private readonly Brush inferredJointBrush = Brushes.Yellow;
        private const double JointThickness = 15;
        private readonly Pen trackedBonePen = new Pen(Brushes.Green, 6);
        private readonly Pen inferredBonePen = new Pen(Brushes.Gray, 6);
        private int frameReduction = 2;

        private Point SkeletonPointToScreen(SkeletonPoint skelpoint)
        {
            DepthImagePoint depthPoint = kinect.CoordinateMapper.MapSkeletonPointToDepthPoint(skelpoint, DepthImageFormat.Resolution640x480Fps30);
            return new Point(depthPoint.X, depthPoint.Y);
        }

        public MainWindow()
        {
            InitializeComponent();
            KinectSensors_StatusChanged(null, null);
            portBtn.Click += SetUpSerialConnection;
            portUpdate.Click += UpdatePortSelection;
            angleBtn.Click += UpdateAngle;
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            kinect.Stop();
            if (port != null)
            {
                port.Close();
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
                        break;
                    }
                }
            }
        }

        private void drawJoints(Skeleton skeleton)
        {
            
            // Render Joints
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
                    Point p = SkeletonPointToScreen(joint.Position);
                    e.Fill = drawBrush;
                    e.Width = JointThickness;
                    e.Height = JointThickness;
                    canvas.Children.Add(e);
                    Canvas.SetTop(e, p.Y);
                    Canvas.SetLeft(e, p.X);
                }
            }
        }

       

        private void Kinect_ColorFrameReady(object sender, ColorImageFrameReadyEventArgs e)
        {
            ColorImageFrame frame = e.OpenColorImageFrame();
            
            if(frame != null && frame.FrameNumber % frameReduction == 0)
            {
                byte[] pixelData = new byte[frame.PixelDataLength];
                frame.CopyPixelDataTo(pixelData);

                camera.Source = BitmapSource.Create(
                    frame.Width, frame.Height, 96, 96, PixelFormats.Bgr32,
                    null, pixelData, frame.Width * 4);
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
                catch(Exception e1)
                {
                    this.Close();
                    MessageBox.Show(e1.Message);
                }
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
                string selected = (string)portInput.Items.GetItemAt(portInput.SelectedIndex);
                if (SerialPort.GetPortNames().Contains(selected))
                {
                    if (port != null && port.IsOpen)
                    {
                        port.Close();
                    }
                    port = new SerialPort(selected, 9600);
                    port.Open();
                }
                else
                {
                    MessageBox.Show("I'm not going to connect with this...", "Invalid Serial Port");
                }
            }
        }

        private void UpdateAngle(object sender, RoutedEventArgs e)
        {
            int angle = 0;
            bool result = int.TryParse(angleInput.Text, out angle);
            if (result)
            {
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

    public static class KinectExtensions
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
    }
}
