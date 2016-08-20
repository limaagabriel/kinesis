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
        private readonly Brush inferredJointBrush = new SolidColorBrush(Color.FromRgb(52, 52, 52));
        private readonly Brush trackedJointBrush = new SolidColorBrush(Color.FromRgb(237, 84, 52));
        private readonly Brush trackedSkeletonBackground = new SolidColorBrush(Color.FromRgb(146, 206, 26));
        //private readonly Brush trackedSkeletonBackground = new SolidColorBrush(Color.FromRgb(140, 205, 209));
        private readonly double JointThickness = 15;
        private readonly double boneThickness = 10;
        private readonly Brush trackedBoneBrush = new SolidColorBrush(Color.FromRgb(255, 0, 255));
        private readonly Brush inferredBoneBrush = Brushes.Yellow;
        private readonly int frameReduction = 2;
        private StateFlow flow = new StateFlow();

        public MainWindow()
        {
            InitializeComponent();
            status.Text = "Welcome to Kinesis!";
            
            angleBtn.Click += UpdateAngle;

            flow.subscribe((state) =>
            {
                switch(state)
                {
                    case "NO_KINECT_ATTACHED":
                        angleBtn.IsEnabled = false;
                        BitmapImage bitmap = new BitmapImage();
                        bitmap.BeginInit();
                        bitmap.UriSource = new Uri("pack://siteoforigin:,,,/Resources/Logo.png");
                        bitmap.EndInit();
                        camera.Stretch = Stretch.Fill;
                        camera.Source = bitmap;
                        status.Text = "State: NO_KINECT_ATTACHED";
                        break;
                    case "KINECT_ATTACHED":
                        angleBtn.IsEnabled = true;
                        status.Text = "State: KINECT_ATTACHED";
                        break;
                    default:
                        status.Text = "Invalid state";
                        break;
                }

                return null;
            });
            StatusChanged(null, null);
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            if(kinect != null)
                kinect.Stop();
        }

        private void StatusChanged(object sender, StatusChangedEventArgs e)
        {
            if(KinectSensor.KinectSensors.Count > 0)
            {
                try
                {
                    kinect = KinectSensor.KinectSensors[0];
                    
                    kinect.Start();
                    KinectSensor.KinectSensors.StatusChanged += StatusChanged;
                    kinect.SkeletonStream.Enable();
                    kinect.ColorStream.Enable();
                    kinect.SkeletonFrameReady += SkeletonFrameReady;
                    kinect.ColorFrameReady += ColorFrameReady;
                    angleInput.Text = "" + kinect.ElevationAngle;
                    flow.dispatch("KINECT_ATTACHED");
                }
                catch (Exception e1)
                {
                    MessageBox.Show(e1.Message, "Error!");
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
                        handleSkeleton(s);
                        break;
                    }
                }
            }
        }

        private void handleJoints(Skeleton skeleton)
        {
            //int port = 0;
            string content = "";
            //bool ok = int.TryParse(portInput.Text, out port);

            //TcpClient socket = null;
            //NetworkStream stream = null;

            //if (ok)
            //{
            //    try
            //    {
            //        socket = new TcpClient(ipInput.Text, port);
            //        stream = socket.GetStream();
            //    }
            //    catch(Exception e) {
            //        socket = null;
            //        status.Text = "aqui";
            //    }
            //}

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
                    content += "{type:\"" + joint.JointType.ToString() +
                        "\", x:" + p.X + ", y:" + p.Y + "};";
                }
            }
            
            //if(ok && socket != null)
            //{
            //    byte[] byteForm = Encoding.ASCII.GetBytes(content);
            //    stream.Write(byteForm, 0, byteForm.Length);
            //    stream.Close();
            //    socket.Close();
            //}
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

        private void handleSkeleton(Skeleton skeleton)
        {
            // Render Torso
            this.DrawBone(skeleton, JointType.Head, JointType.ShoulderCenter);
            this.DrawBone(skeleton, JointType.ShoulderCenter, JointType.ShoulderLeft);
            this.DrawBone(skeleton, JointType.ShoulderCenter, JointType.ShoulderRight);
            this.DrawBone(skeleton, JointType.ShoulderCenter, JointType.Spine);
            this.DrawBone(skeleton, JointType.Spine, JointType.HipCenter);
            this.DrawBone(skeleton, JointType.HipCenter, JointType.HipLeft);
            this.DrawBone(skeleton, JointType.HipCenter, JointType.HipRight);

            // Left Arm
            this.DrawBone(skeleton, JointType.ShoulderLeft, JointType.ElbowLeft);
            this.DrawBone(skeleton, JointType.ElbowLeft, JointType.WristLeft);
            this.DrawBone(skeleton, JointType.WristLeft, JointType.HandLeft);

            // Right Arm
            this.DrawBone(skeleton, JointType.ShoulderRight, JointType.ElbowRight);
            this.DrawBone(skeleton, JointType.ElbowRight, JointType.WristRight);
            this.DrawBone(skeleton, JointType.WristRight, JointType.HandRight);

            // Left Leg
            this.DrawBone(skeleton, JointType.HipLeft, JointType.KneeLeft);
            this.DrawBone(skeleton, JointType.KneeLeft, JointType.AnkleLeft);
            this.DrawBone(skeleton, JointType.AnkleLeft, JointType.FootLeft);

            // Right Leg
            this.DrawBone(skeleton, JointType.HipRight, JointType.KneeRight);
            this.DrawBone(skeleton, JointType.KneeRight, JointType.AnkleRight);
            this.DrawBone(skeleton, JointType.AnkleRight, JointType.FootRight);

            handleJoints(skeleton);
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
            line.Fill = drawBrush;
            canvas.Children.Add(line);
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
