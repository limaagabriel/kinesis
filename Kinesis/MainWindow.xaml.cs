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
using System.ComponentModel;
using Microsoft.Kinect;

namespace Kinesis
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private KinectSensor Kinect;
        private SerialPort Device;
        private readonly Brush TrackedJointBrush = new SolidColorBrush(Color.FromRgb(237, 84, 52));
        private readonly Brush InferredJointBrush = new SolidColorBrush(Color.FromRgb(52, 52, 52));
        private readonly BackgroundWorker SensorWorker = new BackgroundWorker();
        private readonly int JointThickness = 10;
        private readonly JointType[] trackedJoints =
        {
            JointType.ElbowLeft, JointType.ElbowRight,
            JointType.WristLeft, JointType.WristRight
        };
        private readonly Dictionary<JointType, SkeletonPoint> lastPosition = new Dictionary<JointType, SkeletonPoint>();
        private readonly Dictionary<JointType, SkeletonPoint> differentials = new Dictionary<JointType, SkeletonPoint>();
        private readonly Dictionary<JointType, SkeletonPoint> calibrationData = new Dictionary<JointType, SkeletonPoint>();
        private byte[] colorPixels;
        private WriteableBitmap colorBitmap;
        private int baudRate = 115200;
        private StateFlow flow = new StateFlow();
        private bool isCalibrating = false;
        private int calibrationIndex = 0;
        private readonly int calibrationOffset = 150;
        private string dir = "C:\\KinesisSettings\\";
        private string filePath = "C:\\KinesisSettings\\defaultPosition.txt";
        private string oldFilePath = "C:\\KinesisSettings\\defaultPosition.old.txt";
        private bool canWriteToDevice = true;

        public MainWindow()
        {
            InitializeComponent();
            KinectSensor.KinectSensors.StatusChanged += SensorsStatusChanged;
        }

        private void WindowLoaded(object sender, RoutedEventArgs e)
        {
            flow.subscribe(reducer);
            SensorWorker.DoWork += SkeletonJob;
            SensorWorker.RunWorkerCompleted += FinishingSkeletonJob;
            
            AngleBtn.Click += UpdateAngle;
            ReloadBtn.Click += ReloadDevices;
            DisconnectBtn.Click += DisconnectDevice;
            ConnectBtn.Click += ConnectToDevice;
            LoadBtn.Click += LoadDefaultPosition;
            CalibrateBtn.Click += (o, e1) => flow.dispatch("CALIBRATE");

            ReloadDevices(null, null);
            DisconnectBtn.IsEnabled = false;

            if(Kinect == null || !Kinect.IsRunning)
                SensorsStatusChanged(null, null);

            foreach(JointType type in trackedJoints)
            {
                lastPosition.Add(type, new SkeletonPoint());
                differentials.Add(type, new SkeletonPoint());
            }
        }

        private object reducer(string state)
        {
                status.Text = "State: " + state;
                switch (state)
                {
                    case "NO_KINECT_ATTACHED":
                        AngleBtn.IsEnabled = false;
                        BitmapImage bitmap = new BitmapImage();
                        bitmap.BeginInit();
                        bitmap.UriSource = new Uri("pack://siteoforigin:,,,/Resources/Logo.png");
                        bitmap.EndInit();
                        Camera.Stretch = Stretch.Fill;
                        Camera.Source = bitmap;
                        break;
                    case "KINECT_ATTACHED":
                        AngleInput.Text = "" + Kinect.ElevationAngle;
                        AngleBtn.IsEnabled = true;
                        Camera.Source = colorBitmap;
                        break;
                    case "CONNECTED_TO_DEVICE":
                        ConnectBtn.Content = Device.PortName;
                        ConnectBtn.IsEnabled = false;
                        DisconnectBtn.IsEnabled = true;
                        break;
                    case "DEVICE_DISCONNECTED":
                        ConnectBtn.Content = "Connect";
                        DisconnectBtn.IsEnabled = false;
                        ConnectBtn.IsEnabled = true;
                        break;
                    case "CALIBRATE":
                        isCalibrating = true;
                        Canvas.Background = Brushes.DimGray;
                        break;
                    case "FINISH_CALIBRATING":
                        isCalibrating = false;
                        Canvas.Background = Brushes.White;
                        break;
                    default:
                        break;
                }
                return null;
        }

        private void ConnectToDevice(object sender, RoutedEventArgs e)
        {
            string item = DevicesList.SelectedItem as string;
            if(SerialPort.GetPortNames().Contains(item))
            {
                Device = new SerialPort(item, baudRate);
                Device.Open();
                Device.DataReceived += DataReceived;
                flow.dispatch("CONNECTED_TO_DEVICE");
            }
        }

        private void DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            canWriteToDevice = true;
        }

        private void DisconnectDevice(object sender, RoutedEventArgs e)
        {
            if (Device != null && Device.IsOpen)
                Device.Close();
            Device.Dispose();

            flow.dispatch("DEVICE_DISCONNECTED");
        }

        private void ReloadDevices(object sender, RoutedEventArgs e)
        {
            DevicesList.Items.Clear();
            
            foreach(string port in SerialPort.GetPortNames())
            {
                DevicesList.Items.Add(port);
            }
        }


        private void WindowClosing(object sender, EventArgs e)
        {
            if (Kinect != null && Kinect.IsRunning)
                Kinect.Stop();
            if (Device != null && Device.IsOpen)
                Device.Close();
        }

        private void UpdateAngle(object sender, RoutedEventArgs e)
        {
            int Angle;
            bool ok = int.TryParse(AngleInput.Text, out Angle);
            if(ok)
            {
                Kinect.TryToSetAngle(Angle);
            }
            else
            {
                flow.dispatch("ERROR_PARSING_ANGLE");
            }
        }

        private void SensorsStatusChanged(object sender, StatusChangedEventArgs e)
        {
            if (KinectSensor.KinectSensors.Count > 0)
            {
                try
                {
                    Kinect = KinectSensor.KinectSensors.FirstOrDefault((KinectSensor k) =>
                    {
                        return k.Status == KinectStatus.Connected;
                    });

                    Kinect.Start();

                    Kinect.SkeletonStream.Enable();
                    Kinect.ColorStream.Enable();
                    Kinect.SkeletonFrameReady += SkeletonFrameReady;
                    Kinect.ColorFrameReady += ColorFrameReady;

                    colorPixels = new byte[Kinect.ColorStream.FramePixelDataLength];
                    colorBitmap = new WriteableBitmap(Kinect.ColorStream.FrameWidth,
                        Kinect.ColorStream.FrameHeight, 96, 96, PixelFormats.Bgr32, null);

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
            if (frame == null) return;

            frame.CopyPixelDataTo(colorPixels);
            frame.Dispose();

            colorBitmap.WritePixels(new Int32Rect(0, 0,
                colorBitmap.PixelWidth, colorBitmap.PixelHeight), colorPixels,
                colorBitmap.PixelWidth * sizeof(int), 0);
        }

        private void SkeletonFrameReady(object sender, SkeletonFrameReadyEventArgs e)
        {
            SkeletonFrame frame = e.OpenSkeletonFrame();
            if (frame == null || SensorWorker.IsBusy) return;

            Canvas.Children.Clear();
            SensorWorker.RunWorkerAsync(frame);
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

        private void FinishingSkeletonJob(object sender, RunWorkerCompletedEventArgs e)
        {
            if (e.Error == null && !e.Cancelled)
            {
                SkeletonJobResult sjr = e.Result as SkeletonJobResult;
                drawJoints(sjr.Skeleton);

                if(isCalibrating)
                {
                    calibrate(sjr.Skeleton);
                }
                else if (Device != null && Device.IsOpen && canWriteToDevice)
                {
                    Device.Write(sjr.Content);
                    canWriteToDevice = false;
                }
            }
        }

        private void SkeletonJob(object sender, DoWorkEventArgs e)
        {
            SkeletonFrame frame = e.Argument as SkeletonFrame;
            Skeleton[] skeletons = new Skeleton[frame.SkeletonArrayLength];
            frame.CopySkeletonDataTo(skeletons);
            frame.Dispose();

            Skeleton skeleton = skeletons.FirstOrDefault((Skeleton skel) =>
            {
                return skel.TrackingState == SkeletonTrackingState.Tracked;
            });

            if (skeleton == null)
            {
                if (isCalibrating)
                    calibrationIndex = 0;

                e.Cancel = true;
                return;
            }

            string data = "";
            
            foreach(JointType type in trackedJoints)
            {
                differentials[type] = Subtract(skeleton.Joints[type].Position, lastPosition[type]);
                string x = differentials[type].X.ToString().Replace(',', '.');
                string y = differentials[type].Y.ToString().Replace(',', '.');
                string z = differentials[type].Z.ToString().Replace(',', '.');

                data += type.ToString() + "," + x + "," + y + "," + z + ";";
                lastPosition[type] = skeleton.Joints[type].Position;
            }

            e.Result = new SkeletonJobResult(skeleton, data);
        }

        private void calibrate(Skeleton s)
        {
            if(calibrationData.Count == 0)
            {
                foreach(Joint j in s.Joints)
                {
                    calibrationData.Add(j.JointType, j.Position);
                }
            }
            else
            {
                foreach(Joint j in s.Joints)
                {
                    JointType type = j.JointType;
                    SkeletonPoint sp = new SkeletonPoint();
                    sp.X = (calibrationData[type].X + j.Position.X) / 2;
                    sp.Y = (calibrationData[type].Y + j.Position.Y) / 2;
                    sp.Z = (calibrationData[type].Z + j.Position.Z) / 2;

                    calibrationData[type] = sp;
                }
                calibrationIndex += 1;
            }

            if(calibrationIndex >= calibrationOffset)
            {
                isCalibrating = false;
                flow.dispatch("FINISH_CALIBRATING");
                string data = "";
                foreach(KeyValuePair<JointType, SkeletonPoint> pair in calibrationData)
                {
                    string x = pair.Value.X.ToString().Replace(',', '.');
                    string y = pair.Value.Y.ToString().Replace(',', '.');
                    string z = pair.Value.Z.ToString().Replace(',', '.');
                    data += pair.Key.ToString() + "," + x + "," + y + "," + z + ";";
                }

                if(!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                
                if(File.Exists(filePath))
                {
                    if(File.Exists(oldFilePath))
                    {
                        File.Delete(oldFilePath);
                    }

                    byte[] oldData = File.ReadAllBytes(filePath);
                    FileStream auxFs = File.Create(oldFilePath);
                    auxFs.Write(oldData, 0, oldData.Length);
                    auxFs.Close();
                }

                byte[] byteArray = Encoding.ASCII.GetBytes(data);

                FileStream fs = File.Create(filePath);
                fs.Write(byteArray, 0, byteArray.Length);
                fs.Close();
            }
        }

        private void LoadDefaultPosition(object sender, RoutedEventArgs e)
        {
            try
            {
                byte[] data = File.ReadAllBytes(filePath);
                string content = Encoding.ASCII.GetString(data);
                string[] lines = content.Split(';');

                for(int i = 0; i < lines.Length - 1; i++)
                {
                    string[] tokens = lines[i].Split(',');

                    JointType type = (JointType) Enum.Parse(typeof(JointType), tokens[0]);
                    SkeletonPoint sp = new SkeletonPoint();
                    sp.X = float.Parse(tokens[1].Replace('.', ','));
                    sp.Y = float.Parse(tokens[2].Replace('.', ','));
                    sp.Z = float.Parse(tokens[3].Replace('.', ','));

                    lastPosition[type] = sp;

                    MessageBox.Show(type.ToString() + " " + sp.X + " " + sp.Y + " " + sp.Z);
                }
            }
            catch(FileNotFoundException ex)
            {
                string message = ex.Message;
                MessageBox.Show("Error on loading. You should calibrate first, so Kinesis will have something to read!", "File not found!");
            }
        }

        private void drawJoints(Skeleton skeleton)
        {
            foreach (Joint joint in skeleton.Joints)
            {
                Brush drawBrush = null;

                if (joint.TrackingState == JointTrackingState.Tracked)
                {
                    drawBrush = TrackedJointBrush;
                }
                else if (joint.TrackingState == JointTrackingState.Inferred)
                {
                    drawBrush = InferredJointBrush;
                }

                if (drawBrush != null)
                {
                    Point p = Kinect.SkeletonPointToScreen(joint.Position);
                    Ellipse e = new Ellipse();
                    e.Width = JointThickness;
                    e.Height = JointThickness;
                    e.Fill = drawBrush;
                    Canvas.Children.Add(e);
                    Canvas.SetLeft(e, p.X);
                    Canvas.SetTop(e, p.Y);
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
