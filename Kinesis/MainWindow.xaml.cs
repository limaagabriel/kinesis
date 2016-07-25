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

namespace Kinesis
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        Runtime kinectRuntime = new Runtime();

        public MainWindow()
        {
            InitializeComponent();
            kinectRuntime.Initialize(RuntimeOptions.UseSkeletalTracking);
            kinectRuntime.SkeletonFrameReady += new EventHandler<SkeletonFrameReadyEventArgs>(nui_SkeletonFrameReady);
        }

        private void window_loaded(object sender, RoutedEventArgs e)
        {
            
        }

        void nui_SkeletonFrameReady(object sender, SkeletonFrameReadyEventArgs e)
        {
            // Pega todos os 6 esqueletos
            SkeletonFrame allSkeletons = e.SkeletonFrame;

            // Pegamos apenas o primeiro com o status de "Tracked"
            SkeletonData skeleton = (from s in allSkeletons.Skeletons
                                     where s.TrackingState == SkeletonTrackingState.Tracked
                                     select s).FirstOrDefault();

            // Limpa todos os items do canvas
            cnArea.Children.Clear();

            // Cria um loop com todas as juntas do esqueleto
            if (skeleton != null)
            {
                foreach (Joint item in skeleton.Joints)
                {
                    // Cria uma
                    Ellipse objEllipse = new Ellipse();
                    // Adiciona uma cor
                    objEllipse.Fill = new SolidColorBrush(Color.FromArgb(150, 55, 255, 0));
                    // Tamanho
                    objEllipse.Width = 20;
                    objEllipse.Height = 20;
                    // Adiciona ao Canvas
                    cnArea.Children.Add(objEllipse);
                    // Converte a posição da junta e altera a posição da ellipse
                    setDisplayPosition(ref objEllipse, item);
                }
            }
        }


        public void setDisplayPosition(ref Ellipse ellipse, Joint joint)
        {
            int colourX, colourY;

            float depthX, depthY;
            // Converte a posição de Skeleton para DepthImage que gera dois floats entre 0.0 e 1.0
            this.kinectRuntime.SkeletonEngine.SkeletonToDepthImage(joint.Position, out depthX, out depthY);

            // Converte para 320 / 240 que é o que o GetColorPixelCoordinatesFromDepthPixel suporta
            depthX = Math.Max(0, Math.Min(depthX * 320, 320));
            depthY = Math.Max(0, Math.Min(depthY * 240, 240));

            ImageViewArea imageView = new ImageViewArea();

            // Converte os valores de DepthPixel para ColorPixel, que agora sim é o que precisamos
            this.kinectRuntime.NuiCamera.GetColorPixelCoordinatesFromDepthPixel(ImageResolution.Resolution640x480,
                imageView, (int)depthX, (int)depthY, 0, out colourX, out colourY);

            // Agora você ja tem um valor mais exato das coordenadas
            Canvas.SetLeft(ellipse, colourX);
            Canvas.SetTop(ellipse, colourY);

            // Você tem a opção de escalar ela para o tamanho do seu esqueleto também
            // Canvas.SetLeft(ellipse, cnArea.Width * colourX / 640 - 20);
            // Canvas.SetTop(ellipse, cnArea.Height * colourY / 480 - 20);           

        }

        private void Window_Closed(object sender, EventArgs e)
        {
            // Desliga o Kinect
            kinectRuntime.Uninitialize();
        }
    }
}
