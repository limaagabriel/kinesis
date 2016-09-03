using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
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
using System.IO;
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
        private readonly Brush inferredJointBrush = new SolidColorBrush(Color.FromRgb(52, 52, 52));
        private readonly Brush trackedJointBrush = new SolidColorBrush(Color.FromRgb(237, 84, 52));
        private readonly double JointThickness = 15;
        private readonly double boneThickness = 5;
        private readonly Brush trackedBoneBrush = Brushes.Black;
        private readonly Brush inferredBoneBrush = Brushes.Gray;
        private readonly int frameReduction = 2;
        private FileStream fs = null;
        private bool isCalibrating = false;
        private StateFlow flow = new StateFlow();
        private SerialPort port = null;

        public MainWindow()
        {
            InitializeComponent();
            status.Text = "Welcome to Kinesis!";
            KinectSensor.KinectSensors.StatusChanged += StatusChanged;
            angleBtn.Click += UpdateAngle;
            connectToDevice.Click += ConnectToDevice;
            disconnectDevice.Click += DisconnectDevice;
            calibrateBtn.Click += SetDefaultPosition;
            disconnectDevice.IsEnabled = false;
            reloadDevices.Click += ReloadDevices;
            ReloadDevices(null, null);

            flow.subscribe((state) =>
            {
                status.Text = "State: " + state;
                switch (state)
                {
                    case "NO_KINECT_ATTACHED":
                        angleBtn.IsEnabled = false;
                        BitmapImage bitmap = new BitmapImage();
                        bitmap.BeginInit();
                        bitmap.UriSource = new Uri("pack://siteoforigin:,,,/Resources/Logo.png");
                        bitmap.EndInit();
                        camera.Stretch = Stretch.Fill;
                        camera.Source = bitmap;
                        break;
                    case "KINECT_ATTACHED":
                        angleBtn.IsEnabled = true;
                        break;
                    case "CONNECTED_TO_DEVICE":
                        connectToDevice.IsEnabled = false;
                        disconnectDevice.IsEnabled = true;
                        break;
                    case "DEVICE_DISCONNECTED":
                        disconnectDevice.IsEnabled = false;
                        connectToDevice.IsEnabled = true;
                        break;
                    case "CALIBRATE":
                        isCalibrating = true;
                        canvas.Background = Brushes.DimGray;
                        break;
                    case "FINISH_CALIBRATING":
                        isCalibrating = false;
                        canvas.Background = Brushes.White;
                        break;
                    default:
                        status.Text = "Invalid state";
                        break;
                }

                return null;
            });

            if(kinect == null || !kinect.IsRunning)
                StatusChanged(null, null);
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            if(kinect != null)
                kinect.Stop();
        }

        private void SetDefaultPosition(object sender, RoutedEventArgs e)
        {
            string dir = "C:\\KinesisSettings\\";
            string filePath = dir + "defaultPosition.csv";
            string oldFilePath = dir + "defaultPosition.old.csv";

            if(!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            if(File.Exists(filePath))
            {
                if (File.Exists(oldFilePath))
                    File.Delete(oldFilePath);

                byte[] file = File.ReadAllBytes(filePath);
                FileStream auxFs = File.Create(oldFilePath);
                auxFs.Write(file, 0, file.Length);
                auxFs.Close();
            }

            fs = File.Create(filePath);
            flow.dispatch("CALIBRATE");
        }

        private void ReloadDevices(object sender, RoutedEventArgs e)
        {
            devicesList.Items.Clear();
            foreach (String s in SerialPort.GetPortNames())
            {
                devicesList.Items.Add(s);
            }
        }

        private void ConnectToDevice(object sender, RoutedEventArgs e)
        {
            if(devicesList.SelectedItem != null)
            {
                string selected = devicesList.SelectedItem.ToString();
                if (SerialPort.GetPortNames().Contains(selected) && (port == null || !port.IsOpen))
                {
                    port = new SerialPort(selected, 9600);
                    port.Open();

                    flow.dispatch("CONNECTED_TO_DEVICE");
                }
                else
                {
                    status.Text = "Invalid device port descriptor.";
                }
            }
        }

        private void DisconnectDevice(object sender, RoutedEventArgs e)
        {
            if(port != null)
            {
                if (port.IsOpen)
                    port.Close();
                port = null;
            }

            flow.dispatch("DEVICE_DISCONNECTED");
        }

        private void StatusChanged(object sender, StatusChangedEventArgs e)
        {
            if(KinectSensor.KinectSensors.Count > 0)
            {
                try
                {
                    kinect = KinectSensor.KinectSensors[0];
                    
                    kinect.Start();
                    
                    kinect.SkeletonStream.Enable();
                    kinect.ColorStream.Enable();
                    kinect.SkeletonFrameReady += SkeletonFrameReady;
                    kinect.ColorFrameReady += ColorFrameReady;
                    angleInput.Text = "" + kinect.ElevationAngle;
                    flow.dispatch("KINECT_ATTACHED");
                }
                catch (Exception e1)
                {
                    flow.dispatch("NO_KINECT_ATTACHED");
                    status.Text = "Error: " + e1.Message;
                }
            }
            else
            {
                flow.dispatch("NO_KINECT_ATTACHED");
            }
        }

        private void ColorFrameReady(object sender, ColorImageFrameReadyEventArgs e)
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

        private void SkeletonFrameReady(object sender, SkeletonFrameReadyEventArgs e)
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
                        drawBody(s);
                        if(isCalibrating)
                        {
                            
                            flow.dispatch("FINISH_CALIBRATING");
                        }
                        else if(port != null && port.IsOpen)
                        {
                            Thread teleactive = new Thread(new ParameterizedThreadStart((object o) => {
                                SerialPort p = (SerialPort) o;

                                string content = "";

                                foreach(Joint j in s.Joints)
                                {
                                    Point pos = kinect.SkeletonPointToScreen(j.Position);
                                    content += j.JointType.ToString() + "," + pos.X + "," + pos.Y + ";";
                                }

                                p.WriteLine(content);
                            }));

                            teleactive.Start(port);
                        }
                        break;
                    }
                }
            }
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

        private void drawBody(Skeleton skeleton)
        {
            // Render Torso
            DrawBone(skeleton, JointType.Head, JointType.ShoulderCenter);
            DrawBone(skeleton, JointType.ShoulderCenter, JointType.ShoulderLeft);
            DrawBone(skeleton, JointType.ShoulderCenter, JointType.ShoulderRight);
            DrawBone(skeleton, JointType.ShoulderCenter, JointType.Spine);
            DrawBone(skeleton, JointType.Spine, JointType.HipCenter);
            DrawBone(skeleton, JointType.HipCenter, JointType.HipLeft);
            DrawBone(skeleton, JointType.HipCenter, JointType.HipRight);

            // Left Arm
            DrawBone(skeleton, JointType.ShoulderLeft, JointType.ElbowLeft);
            DrawBone(skeleton, JointType.ElbowLeft, JointType.WristLeft);
            DrawBone(skeleton, JointType.WristLeft, JointType.HandLeft);

            // Right Arm
            DrawBone(skeleton, JointType.ShoulderRight, JointType.ElbowRight);
            DrawBone(skeleton, JointType.ElbowRight, JointType.WristRight);
            DrawBone(skeleton, JointType.WristRight, JointType.HandRight);

            // Left Leg
            DrawBone(skeleton, JointType.HipLeft, JointType.KneeLeft);
            DrawBone(skeleton, JointType.KneeLeft, JointType.AnkleLeft);
            DrawBone(skeleton, JointType.AnkleLeft, JointType.FootLeft);

            // Right Leg
            DrawBone(skeleton, JointType.HipRight, JointType.KneeRight);
            DrawBone(skeleton, JointType.KneeRight, JointType.AnkleRight);
            DrawBone(skeleton, JointType.AnkleRight, JointType.FootRight);

            drawJoints(skeleton);
        }

        private void DrawBone(Skeleton skeleton, JointType jointType0, JointType jointType1)
        {
            Joint joint0 = skeleton.Joints[jointType0];
            Joint joint1 = skeleton.Joints[jointType1];

            // If we can't find either of these joints, exit
            if (joint0.TrackingState == JointTrackingState.NotTracked ||
                joint1.TrackingState == JointTrackingState.NotTracked)
            {
                return;
            }

            // Don't draw if both points are inferred
            if (joint0.TrackingState == JointTrackingState.Inferred &&
                joint1.TrackingState == JointTrackingState.Inferred)
            {
                return;
            }

            // We assume all drawn bones are inferred unless BOTH joints are tracked
            Brush drawBrush = this.inferredBoneBrush;
            if (joint0.TrackingState == JointTrackingState.Tracked && joint1.TrackingState == JointTrackingState.Tracked)
            {
                drawBrush = this.trackedBoneBrush;
            }
            Line line = new Line();
            line.X1 = joint0.Position.X;
            line.X2 = joint1.Position.X;
            line.Y1 = joint0.Position.Y;
            line.Y2 = joint1.Position.Y;

            line.StrokeThickness = boneThickness;
            line.Stroke = drawBrush;
            canvas.Children.Add(line);
            Canvas.SetTop(line, line.X1);
            Canvas.SetLeft(line, line.Y1);
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
