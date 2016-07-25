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
using Microsoft.Research.Kinect.Nui;
using Kinect.Extensions;
using System.IO.Ports;

namespace Kinesis
{
    public partial class MainWindow : Window
    {
        Runtime kinect = Runtime.Kinects[0];
        SerialPort port = null;

        public MainWindow()
        {
            InitializeComponent();
            angleBtn.Click += UpdateAngle;
            portBtn.Click += SetUpSerialConnection;
            portUpdate.Click += UpdatePortSelection;
            kinect.Initialize(RuntimeOptions.UseSkeletalTracking);
            kinect.SkeletonFrameReady += new EventHandler<SkeletonFrameReadyEventArgs>(nui_SkeletonFrameReady);
        }

        void UpdatePortSelection(object sender, RoutedEventArgs e)
        {
            portInput.Items.Clear();
            foreach(string serialPort in SerialPort.GetPortNames())
            {
                portInput.Items.Add(serialPort);
            }
        }

        void nui_SkeletonFrameReady(object sender, SkeletonFrameReadyEventArgs e)
        {
            SkeletonFrame allSkeletons = e.SkeletonFrame;
            SkeletonData skeleton = (from s in allSkeletons.Skeletons
                                     where s.TrackingState == SkeletonTrackingState.Tracked
                                     select s).FirstOrDefault();

            cnArea.Children.Clear();

            if (skeleton != null)
            {
                foreach (Joint item in skeleton.Joints)
                {
                    Ellipse objEllipse = new Ellipse();
                    objEllipse.Fill = new SolidColorBrush(Color.FromArgb(150, 55, 255, 0));
                    objEllipse.Width = 20;
                    objEllipse.Height = 20;
                    cnArea.Children.Add(objEllipse);
                    setDisplayPosition(ref objEllipse, item);
                }
            }
        }

        public void setDisplayPosition(ref Ellipse ellipse, Joint joint)
        {
            int colourX, colourY;

            float depthX, depthY;
            // Converte a posição de Skeleton para DepthImage que gera dois floats entre 0.0 e 1.0
            this.kinect.SkeletonEngine.SkeletonToDepthImage(joint.Position, out depthX, out depthY);

            // Converte para 320 / 240 que é o que o GetColorPixelCoordinatesFromDepthPixel suporta
            depthX = Math.Max(0, Math.Min(depthX * 320, 320));
            depthY = Math.Max(0, Math.Min(depthY * 240, 240));

            ImageViewArea imageView = new ImageViewArea();

            // Converte os valores de DepthPixel para ColorPixel, que agora sim é o que precisamos
            this.kinect.NuiCamera.GetColorPixelCoordinatesFromDepthPixel(ImageResolution.Resolution640x480,
                imageView, (int)depthX, (int)depthY, 0, out colourX, out colourY);

            Canvas.SetLeft(ellipse, colourX);
            Canvas.SetTop(ellipse, colourY);
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            kinect.Uninitialize();
        }

        void UpdateAngle(object sender, RoutedEventArgs e)
        {
            int angle = 0;
            bool result = int.TryParse(angleInput.Text, out angle);
            if(result)
            {
                var movement = kinect.NuiCamera.TryToSetAngle(angle);
                if(!movement)
                {
                    MessageBox.Show("Movement not supported");
                }
            }
            else
            {
                MessageBox.Show("Value not parsable u.u");
            }
        }

        void SetUpSerialConnection(object sender, RoutedEventArgs e)
        { 
            if(portInput.SelectedIndex >= 0)
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
                    MessageBox.Show("I'm not going to connect with this...");
                }
            }   
        }
    }
}
