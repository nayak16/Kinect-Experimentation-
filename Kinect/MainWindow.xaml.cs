using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
using Microsoft.Kinect.Toolkit;


namespace DadTest
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
      
    public partial class MainWindow : Window
    {

        const float MaxDepthDistance = 4000; // max value returned
        const float MinDepthDistance = 850; // min value returned
        const float MaxDepthDistanceOffset = MaxDepthDistance - MinDepthDistance; 

        private readonly KinectSensorChooser _sensorChooser = new KinectSensorChooser();
        int cnt = 0;
        
        public MainWindow()
        {
            InitializeComponent();
            _sensorChooser.Start();

        }
        bool closing = false;
        const int skeletonCount = 6;
        Skeleton[] allSkeletons = new Skeleton[skeletonCount];

        private void Grid_Loaded(object sender, RoutedEventArgs e)
        {
        }

    
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            IntialState();

        }

        void IntialState()
        {
            
            label1.Content = "Counter = " + cnt.ToString();


            KinectSensor newSensor = _sensorChooser.Kinect;
            newSensor.ColorStream.Enable();
            newSensor.SkeletonStream.Enable();
            
            newSensor.DepthStream.Enable();
            newSensor.AllFramesReady += new EventHandler<AllFramesReadyEventArgs>(newSensor_AllFramesReady);
            try
            {
                newSensor.Start();
            }
            catch (System.IO.IOException)
            {
                _sensorChooser.TryResolveConflict();
               
            }
            //throw new NotImplementedException();
        }
        Skeleton GetFirstSkeleton(AllFramesReadyEventArgs e)
        {
            using (SkeletonFrame skeletonFrameData = e.OpenSkeletonFrame())
            {
                if(skeletonFrameData == null) return null;
                skeletonFrameData.CopySkeletonDataTo(allSkeletons);

                //get the first tracked skeleton
                Skeleton frame = (from s in allSkeletons
                                  where s.TrackingState == SkeletonTrackingState.Tracked
                                  select s).FirstOrDefault();
                return frame; 
            }
        }
        public static byte CalculateIntensityFromDepth(int distance)
        {
            //monochromatic intensity for histogram
            return (byte)(255 - (255 * Math.Max(distance - MinDepthDistance, 0) / (MaxDepthDistanceOffset)));
        }
        private byte[] GenerateColoredBytes(DepthImageFrame depthFrame)
        {
            //get raw data from kinect
            short[] rawDepthData = new short[depthFrame.PixelDataLength];
            depthFrame.CopyPixelDataTo(rawDepthData);
            Byte[] pixels = new byte[depthFrame.Width * depthFrame.Height * 4];

            //constants
            const int BlueIndex = 0;
            const int GreenIndex = 1;
            const int RedIndex = 2; 

            //loop through distances
            for (int depthIndex = 0, colorIndex = 0; depthIndex < rawDepthData.Length && colorIndex < pixels.Length;
                 depthIndex++, colorIndex += 4)
            {
                //get player 
                int player = rawDepthData[depthIndex] & DepthImageFrame.PlayerIndexBitmask;
                //get depth value
                int depth = rawDepthData[depthIndex] >> DepthImageFrame.PlayerIndexBitmaskWidth;
                //.9M or 2.95'
                if (depth <= 900)
                {
                    pixels[colorIndex + BlueIndex] = 0;
                    pixels[colorIndex + GreenIndex] = 0;
                    pixels[colorIndex + RedIndex] = 255;
                }
                //.9M - 2M or 2.95' - 6.26'
                else if (depth > 900 && depth < 2000)
                {
                    pixels[colorIndex + BlueIndex] = 0;
                    pixels[colorIndex + GreenIndex] = 255;
                    pixels[colorIndex + RedIndex] = 0;
                }
                //2M+ or 6.26'+
                else if (depth > 2000)
                {
                    pixels[colorIndex + BlueIndex] = 255;
                    pixels[colorIndex + GreenIndex] = 0;
                    pixels[colorIndex + RedIndex] = 0;
                }
                //byte intensity = CalculateIntensityFromDepth(depth);
                //pixels[colorIndex + BlueIndex] = intensity;
                //pixels[colorIndex + GreenIndex] = intensity;
                //pixels[colorIndex + RedIndex] = intensity;

            }

            return pixels;

        }
        void newSensor_AllFramesReady(object sender, AllFramesReadyEventArgs e)
        {
            
            using (DepthImageFrame depthFrame = e.OpenDepthImageFrame())
            {
                if (depthFrame == null)
                {
                    return;
                }

                byte[] pixels = GenerateColoredBytes(depthFrame);
                //colorFrame.CopyPixelDataTo(pixels);

                int stride = depthFrame.Width * 4;
                depthImg.Source =
                   BitmapSource.Create(depthFrame.Width, depthFrame.Height, 
                                       96, 96, PixelFormats.Bgr32, null, pixels, stride);
            
            }
            
            using (ColorImageFrame colorFrame = e.OpenColorImageFrame())
            {
                if (colorFrame == null)
                {
                    return;
                }

                byte[] pixels = new byte[colorFrame.PixelDataLength];
                colorFrame.CopyPixelDataTo(pixels);

                int stride = colorFrame.Width * 4;
                colorImg.Source =
                    BitmapSource.Create(colorFrame.Width, colorFrame.Height, 
                                        96, 96, PixelFormats.Bgr32, null, pixels, stride);
            }
            
            if (closing)
            {
                return;
            }
            Skeleton first = GetFirstSkeleton(e);
            if (first == null) return;

            GetCameraPoint(first, e);
            
            
        }
        
        void GetCameraPoint(Skeleton first, AllFramesReadyEventArgs e)
        {
            using (DepthImageFrame depth = e.OpenDepthImageFrame())
            {
                if (depth == null || _sensorChooser.Kinect == null) return;

                //head
                DepthImagePoint headDepthPoint = depth.MapFromSkeletonPoint(first.Joints[JointType.Head].Position);
                //left hand
                DepthImagePoint leftDepthPoint = depth.MapFromSkeletonPoint(first.Joints[JointType.HandLeft].Position);
                //right hand
                DepthImagePoint rightDepthPoint = depth.MapFromSkeletonPoint(first.Joints[JointType.HandRight].Position);

                //image point
                //head
                ColorImagePoint headColorPoint =
                    depth.MapToColorImagePoint(headDepthPoint.X, headDepthPoint.Y, ColorImageFormat.RgbResolution640x480Fps30);
                //left hand
                ColorImagePoint leftColorPoint =
                   depth.MapToColorImagePoint(leftDepthPoint.X, leftDepthPoint.Y, ColorImageFormat.RgbResolution640x480Fps30);
                //right hand
                ColorImagePoint rightColorPoint =
                   depth.MapToColorImagePoint(rightDepthPoint.X, rightDepthPoint.Y, ColorImageFormat.RgbResolution640x480Fps30);
                //Set Location
                CameraPosition(head, headColorPoint);
                CameraPosition(leftE, leftColorPoint);
                CameraPosition(rightE, rightColorPoint);


            }
        }
        private void CameraPosition(FrameworkElement element, ColorImagePoint point)
        {
            Console.Out.WriteLine(point.X.ToString());
            Canvas.SetLeft(element, point.X - element.Width / 2);
            Canvas.SetTop(element, point.Y - element.Height / 2);
        }
        void StopKinect(KinectSensor sensor)
        {
            if (sensor != null)
            {
                sensor.Stop();
                sensor.AudioSource.Stop();
            }
        }
        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            StopKinect(_sensorChooser.Kinect);
        }

        private void btnOK_Click(object sender, RoutedEventArgs e)
        {
            Int32 value = 0;

            Int32.TryParse(txtElevationAngle.Text, out value);

            if (value < _sensorChooser.Kinect.MaxElevationAngle & value > _sensorChooser.Kinect.MinElevationAngle)
            {
                _sensorChooser.Kinect.ElevationAngle = value;

            }
        }

    }
}
