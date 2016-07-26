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
        KinectSensor kinect = null;
        SerialPort port = null;

        public MainWindow()
        {
            KinectSensors_StatusChanged(null, null);
            InitializeComponent();
            portBtn.Click += SetUpSerialConnection;
            portUpdate.Click += UpdatePortSelection;
            angleBtn.Click += UpdateAngle;
            KinectSensor.KinectSensors.StatusChanged += KinectSensors_StatusChanged;
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
