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
using System.IO;
using System.IO.Ports;
using Microsoft.Kinect;

namespace Kinesis
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        //Define variables
        private KinectSensor kinect;
        private readonly Brush inferredJointBrush = new SolidColorBrush(Color.FromRgb(52, 52, 52));
        private readonly Brush trackedJointBrush = new SolidColorBrush(Color.FromRgb(237, 84, 52));
        private readonly double JointThickness = 15;
        private readonly double boneThickness = 5;
        private readonly Brush trackedBoneBrush = Brushes.Black;
        private readonly Brush inferredBoneBrush = Brushes.Gray;
        private StateFlow flow = new StateFlow();
        private SerialPort port;
        private int frameReduction = 1;
        private FileStream fs;
        private bool isCalibrating = false;
        private JointType[] trackedJoints = {
            JointType.ElbowLeft, JointType.ElbowRight,
            JointType.WristLeft, JointType.WristRight
        };
        private int calibrationOffset = 150;
        private int calibrationIndex = 0;
        private SkeletonPoint[] lastJointsData = new SkeletonPoint[20];
        private SkeletonPoint[] differentials = new SkeletonPoint[20];
        private SkeletonPoint[] calibrationData = new SkeletonPoint[20];
        private string dir = "C:\\KinesisSettings\\";
        private string filePath = "C:\\KinesisSettings\\defaultPosition.txt";
        private string oldFilePath = "C:\\KinesisSettings\\defaultPosition.old.txt";
        private SkeletonPoint[] defaultPosition;
        private bool canWriteToPort = true;
        private int baudRate = 57600;

        public MainWindow()
        {
            InitializeComponent();
            KinectSensor.KinectSensors.StatusChanged += StatusChanged;
            angleBtn.Click += UpdateAngle;
            connectToDevice.Click += ConnectToDevice;
            disconnectDevice.Click += DisconnectDevice;
            disconnectDevice.IsEnabled = false;
            reloadDevices.Click += ReloadDevices;
            calibrateBtn.Click += SetFiles;
            loadDefaultBtn.Click += LoadDefaultPosition;
            ReloadDevices(null, null);
            
            flow.subscribe(reducer);
            if (kinect == null)
                StatusChanged(null, null);

            for(int i = 0; i < differentials.Length; i++)
            {
                differentials[i] = new SkeletonPoint();
                differentials[i].X = 0;
                differentials[i].Y = 0;
                differentials[i].Z = 0;
            }
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            if (kinect != null && kinect.IsRunning)
                kinect.Stop();
            if (port != null && port.IsOpen)
                port.Close();
            port = null;
            kinect = null;
        }

        private void StatusChanged(object sender, StatusChangedEventArgs e)
        {
            if (KinectSensor.KinectSensors.Count > 0)
            {
                try
                {
                    kinect = KinectSensor.KinectSensors[0];

                    kinect.Start();

                    kinect.SkeletonStream.Enable();
                    kinect.ColorStream.Enable();
                    kinect.SkeletonFrameReady += SkeletonFrameReady;
                    kinect.ColorFrameReady += ColorFrameReady;
                    flow.dispatch("KINECT_ATTACHED");
                }
                catch (Exception e1)
                {
                    status.Text = e1.Message;
                    flow.dispatch("NO_KINECT_ATTACHED");
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

                camera.Source = (BitmapSource.Create(
                    frame.Width, frame.Height, 96, 96, PixelFormats.Bgr32,
                    null, pixelData, frame.Width * 4));
            }
        }

        private void SetFiles(object sender, RoutedEventArgs e)
        {
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            if (File.Exists(filePath))
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

        private SkeletonPoint Subtract(SkeletonPoint f, SkeletonPoint s)
        {
            SkeletonPoint sp = new SkeletonPoint();
            sp.X = f.X - s.X;
            sp.Y = f.Y - s.Y;
            sp.Z = f.Z - s.Z;
            return sp;
        }

        private SkeletonPoint Add(SkeletonPoint f, SkeletonPoint s)
        {
            SkeletonPoint sp = new SkeletonPoint();
            sp.X = f.X + s.X;
            sp.Y = f.Y + s.Y;
            sp.Z = f.Z + s.Z;
            return sp;
        }

        private void SkeletonFrameReady(object sender, SkeletonFrameReadyEventArgs e)
        {
            canvas.Children.Clear();

            SkeletonFrame frame = e.OpenSkeletonFrame();
            if (frame == null) return;

            Skeleton[] trackedSkeletons = new Skeleton[frame.SkeletonArrayLength];
            frame.CopySkeletonDataTo(trackedSkeletons);
            Skeleton s = null;

            foreach(Skeleton skel in trackedSkeletons)
            {
                if(skel.TrackingState == SkeletonTrackingState.Tracked)
                {
                    s = skel;
                    break;
                }
            }

            if (s == null) return;

            drawBody(s);

            foreach (Joint j in s.Joints)
            {
                int index = (int)j.JointType;
                SkeletonPoint aux = Subtract(j.Position, lastJointsData[index]);
                differentials[index] = Add(aux, differentials[index]);
            }

            foreach (Joint j in s.Joints)
            {
                int index = (int)j.JointType;
                lastJointsData[index] = j.Position;
            }

            if (isCalibrating)
            {
                flow.dispatch("CALIBRATE");
                foreach (Joint j in s.Joints)
                {
                    int index = (int) j.JointType;
                    if(calibrationData[index] == null)
                    {
                        calibrationData[index] = j.Position;
                    }
                    else
                    {
                        SkeletonPoint oldPoint = calibrationData[index];
                        SkeletonPoint newPoint = new SkeletonPoint();
                        newPoint.X = (j.Position.X + oldPoint.X) / 2;
                        newPoint.Y = (j.Position.Y + oldPoint.Y) / 2;
                        newPoint.Z = (j.Position.Z + oldPoint.Z) / 2;

                        calibrationData[index] = newPoint;
                    }
                }

                calibrationIndex += 1;

                if(calibrationIndex > calibrationOffset)
                {
                    string content = "";
                    for (int i = 0; i < calibrationData.Length; i++)
                    {
                        string x = calibrationData[i].X.ToString().Replace(',', '.');
                        string y = calibrationData[i].Y.ToString().Replace(',', '.');
                        string z = calibrationData[i].Z.ToString().Replace(',', '.');

                        content += i + "," + x + "," + y + "," + z + ";"; 
                    }

                    byte[] byteArray = Encoding.ASCII.GetBytes(content);
                    fs.Write(byteArray, 0, byteArray.Length);

                    flow.dispatch("FINISH_CALIBRATING");
                    fs.Close();
                    fs = null;
                }
            }
            else if (port != null && port.IsOpen && canWriteToPort)
            {
                Thread t = new Thread(new ParameterizedThreadStart((object o) =>
                {
                    SerialPort p = (SerialPort)o;
                    string content = "";

                    foreach (JointType type in trackedJoints)
                    {
                        int index = (int)type;
                        SkeletonPoint sp = differentials[index];
                        string z = sp.Z.ToString().Replace(',', '.');
                        string y = sp.Y.ToString().Replace(',', '.');
                        string x = sp.X.ToString().Replace(',', '.');
                        content += type.ToString() + "," + x + "," + y + "," + z + ";";

                        differentials[index].X = 0;
                        differentials[index].Y = 0;
                        differentials[index].Z = 0;
                    }

                    p.WriteLine(content);
                }));

                t.Start(port);
                canWriteToPort = false;
            }
        }

        private void ReceiveDataFromDevice(object sender, SerialDataReceivedEventArgs e)
        {
            canWriteToPort = true;
        }

        private object reducer(string state)
        {
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
                        angleInput.Text = "" + kinect.ElevationAngle;
                        angleBtn.IsEnabled = true;
                        break;
                    case "CONNECTED_TO_DEVICE":
                        connectToDevice.Content = port.PortName;
                        connectToDevice.IsEnabled = false;
                        disconnectDevice.IsEnabled = true;
                        break;
                    case "DEVICE_DISCONNECTED":
                        connectToDevice.Content = "Connect";
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
                        break;
                }
            }
            return null;
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
                if (SerialPort.GetPortNames().Contains(selected) && 
                    (port == null || !port.IsOpen))
                {
                    LoadDefaultPosition(null, null);

                    if(defaultPosition != null)
                    {
                        port = new SerialPort(selected, baudRate);
                        port.Open();
                        port.DataReceived += ReceiveDataFromDevice;
                        flow.dispatch("CONNECTED_TO_DEVICE");
                    }
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

        private void LoadDefaultPosition(object sender, RoutedEventArgs e)
        {
            defaultPosition = ReadDefaultSkeletonPoints();
            if(defaultPosition != null)
            {
                status.Text = "Default position loaded successfully!";
                lastJointsData = defaultPosition;
            }

            for(int i = 0; i < differentials.Length; i++)
            {
                differentials[i] = new SkeletonPoint();
                differentials[i].X = 0;
                differentials[i].Y = 0;
                differentials[i].Z = 0;
            }
        }

        private SkeletonPoint[] ReadDefaultSkeletonPoints()
        {
            SkeletonPoint[] allSp = new SkeletonPoint[20];
            if(!File.Exists(filePath))
            {
                MessageBox.Show("Please, start calibration! Kinesis needs this file to make things right.",
                    "defaultPosition file not found");
                return null;
            }
            byte[] content = File.ReadAllBytes(filePath);
            string completeFile = Encoding.ASCII.GetString(content);

            string[] lines = completeFile.Split('\n');
            for (int i = 0; i < lines.Length - 1; i++)
            {
                string[] data = lines[i].Split(';');
                SkeletonPoint sp = new SkeletonPoint();
                int index = int.Parse(data[0]);
                sp.X = float.Parse(data[1]);
                sp.Y = float.Parse(data[2]);
                sp.Z = float.Parse(data[3]);

                allSp[index] = sp;
            }

            return allSp;
        }

        private void drawJointsFromDefaultPosition()
        {
            SkeletonPoint[] allSp = ReadDefaultSkeletonPoints();

            for(int i = 0; i < allSp.Length; i++) {
                SkeletonPoint sp = allSp[i];
                Point p = kinect.SkeletonPointToScreen(sp);
                Ellipse e = new Ellipse();
                e.Fill = trackedJointBrush;
                e.Width = JointThickness;
                e.Height = JointThickness;
                canvas.Children.Add(e);
                Canvas.SetTop(e, p.Y);
                Canvas.SetLeft(e, p.X);
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
            catch (Exception e)
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
