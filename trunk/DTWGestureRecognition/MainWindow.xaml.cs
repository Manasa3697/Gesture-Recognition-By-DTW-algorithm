﻿//-----------------------------------------------------------------------
// <copyright file="MainWindow.xaml.cs" company="Rhemyst and Rymix">
//     Open Source. Do with this as you will. Include this statement or 
//     don't - whatever you like.
//
//     No warranty or support given. No guarantees this will work or meet
//     your needs. Some elements of this project have been tailored to
//     the authors' needs and therefore don't necessarily follow best
//     practice. Subsequent releases of this project will (probably) not
//     be compatible with different versions, so whatever you do, don't
//     overwrite your implementation with any new releases of this
//     project!
//
//     Enjoy working with Kinect!
// </copyright>
//-----------------------------------------------------------------------

namespace DTWGestureRecognition
{
    using System;
    using System.Text;
    using System.Collections;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Windows;
    using System.Windows.Forms;
    using System.Windows.Media;
    using System.Windows.Media.Imaging;
    using System.Windows.Shapes;
    using Microsoft.Research.Kinect.Nui;
    using System.IO;



    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow
    {
        // We want to control how depth data gets converted into false-color data
        // for more intuitive visualization, so we keep 32-bit color frame buffer versions of
        // these, to be updated whenever we receive and process a 16-bit frame.

        /// <summary>
        /// The red index
        /// </summary>
        private const int RedIdx = 2;
        Boolean b = true;
        /// <summary>
        /// The green index
        /// </summary>
        private const int GreenIdx = 1;

        /// <summary>
        /// The blue index
        /// </summary>
        private const int BlueIdx = 0;

        /// <summary>
        /// How many skeleton frames to ignore (_flipFlop)
        /// 1 = capture every frame, 2 = capture every second frame etc.
        /// </summary>
        private const int Ignore = 2;
        int counter = 0;
        int counter1 = 0;
        /// <summary>
        /// How many skeleton frames to store in the _video buffer
        /// </summary>
        private const int BufferSize = 32;

        /// <summary>
        /// The minumum number of frames in the _video buffer before we attempt to start matching gestures
        /// </summary>
        private const int MinimumFrames = 6;

        /// <summary>
        /// The minumum number of frames in the _video buffer before we attempt to start matching gestures
        /// </summary>
        private const int CaptureCountdownSeconds = 3;

        /// <summary>
        /// Where we will save our gestures to. The app will append a data/time and .txt to this string
        /// </summary>
        //private const string GestureSaveFileLocation = @"C:\Users\Manasa\Desktop\kinectdtw-1.0\trunk\Gesture Files\";

        /// <summary>
        /// Where we will save our gestures to. The app will append a data/time and .txt to this string
        /// </summary>
        private const string GestureSaveFileNamePrefix = @"RecordedGestures";

        /// <summary>
        /// Dictionary of all the joints Kinect SDK is capable of tracking. You might not want always to use them all but they are included here for thouroughness.
        /// </summary>
        private readonly Dictionary<JointID, Brush> _jointColors = new Dictionary<JointID, Brush>
        { 
            {JointID.HipCenter, new SolidColorBrush(Color.FromRgb(169, 176, 155))},
            {JointID.Spine, new SolidColorBrush(Color.FromRgb(169, 176, 155))},
            {JointID.ShoulderCenter, new SolidColorBrush(Color.FromRgb(168, 230, 29))},
            {JointID.Head, new SolidColorBrush(Color.FromRgb(200, 0, 0))},
            {JointID.ShoulderLeft, new SolidColorBrush(Color.FromRgb(79, 84, 33))},
            {JointID.ElbowLeft, new SolidColorBrush(Color.FromRgb(84, 33, 42))},
            {JointID.WristLeft, new SolidColorBrush(Color.FromRgb(255, 126, 0))},
            {JointID.HandLeft, new SolidColorBrush(Color.FromRgb(215, 86, 0))},
            {JointID.ShoulderRight, new SolidColorBrush(Color.FromRgb(33, 79,  84))},
            {JointID.ElbowRight, new SolidColorBrush(Color.FromRgb(33, 33, 84))},
            {JointID.WristRight, new SolidColorBrush(Color.FromRgb(77, 109, 243))},
            {JointID.HandRight, new SolidColorBrush(Color.FromRgb(37,  69, 243))},
            {JointID.HipLeft, new SolidColorBrush(Color.FromRgb(77, 109, 243))},
            {JointID.KneeLeft, new SolidColorBrush(Color.FromRgb(69, 33, 84))},
            {JointID.AnkleLeft, new SolidColorBrush(Color.FromRgb(229, 170, 122))},
            {JointID.FootLeft, new SolidColorBrush(Color.FromRgb(255, 126, 0))},
            {JointID.HipRight, new SolidColorBrush(Color.FromRgb(181, 165, 213))},
            {JointID.KneeRight, new SolidColorBrush(Color.FromRgb(71, 222, 76))},
            {JointID.AnkleRight, new SolidColorBrush(Color.FromRgb(245, 228, 156))},
            {JointID.FootRight, new SolidColorBrush(Color.FromRgb(77, 109, 243))}
        };

        /// <summary>
        /// The depth frame byte array. Only supports 320 * 240 at this time
        /// </summary>
        private readonly byte[] _depthFrame32 = new byte[320 * 240 * 4];

        /// <summary>
        /// Flag to show whether or not the gesture recogniser is capturing a new pose
        /// </summary>
        private bool _capturing;

        /// <summary>
        /// Dynamic Time Warping object
        /// </summary>
        private DtwGestureRecognizer _dtw;

        /// <summary>
        /// How many frames occurred 'last time'. Used for calculating frames per second
        /// </summary>
        private int _lastFrames;

        /// <summary>
        /// The 'last time' DateTime. Used for calculating frames per second
        /// </summary>
        private DateTime _lastTime = DateTime.MaxValue;

        /// <summary>
        /// The Natural User Interface runtime
        /// </summary>
        private Runtime _nui;

        /// <summary>
        /// Total number of framed that have occurred. Used for calculating frames per second
        /// </summary>
        private int _totalFrames;

        /// <summary>
        /// Switch used to ignore certain skeleton frames
        /// </summary>
        private int _flipFlop;

        /// <summary>
        /// ArrayList of coordinates which are recorded in sequence to define one gesture
        /// </summary>
        private ArrayList _video;

        /// <summary>
        /// ArrayList of coordinates which are recorded in sequence to define one gesture
        /// </summary>
        private DateTime _captureCountdown = DateTime.Now;

        /// <summary>
        /// ArrayList of coordinates which are recorded in sequence to define one gesture
        /// </summary>
        private Timer _captureCountdownTimer;

        /// <summary>
        /// Initializes a new instance of the MainWindow class
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Opens the sent text file and creates a _dtw recorded gesture sequence
        /// Currently not very flexible and totally intolerant of errors.
        /// </summary>
        /// <param name="fileLocation">Full path to the gesture file</param>
        public void LoadGesturesFromFile(string fileLocation)
        {
            int itemCount = 0;
            string line;
            string gestureName = String.Empty;

            // TODO I'm defaulting this to 12 here for now as it meets my current need but I need to cater for variable lengths in the future
            ArrayList frames = new ArrayList();
            double[] items = new double[12];

            // Read the file and display it line by line.
            System.IO.StreamReader file = new System.IO.StreamReader(fileLocation);
            while ((line = file.ReadLine()) != null)
            {
                if (line.StartsWith("@"))
                {
                    Console.Write(line, "@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@2##########################################################");
                    //gestureName = line;
                    continue;
                }

                if (line.StartsWith("~"))
                {
                    frames.Add(items);
                    itemCount = 0;
                    items = new double[12];
                    continue;
                }

                if (!line.StartsWith("----"))
                {
                    Console.Write(line, "##########################################################");
                    items[itemCount] = Double.Parse(line);
                }

                itemCount++;

                if (line.StartsWith("----"))
                {
                    _dtw.AddOrUpdate(frames, gestureName);
                    frames = new ArrayList();
                    gestureName = String.Empty;
                    itemCount = 0;
                }
            }

            file.Close();
        }

        /// <summary>
        /// Called each time a skeleton frame is ready. Passes skeletal data to the DTW processor
        /// </summary>
        /// <param name="sender">The sender object</param>
        /// <param name="e">Skeleton Frame Ready Event Args</param>
        private static void SkeletonExtractSkeletonFrameReady(object sender, SkeletonFrameReadyEventArgs e)
        {
            SkeletonFrame skeletonFrame = e.SkeletonFrame;
            foreach (SkeletonData data in skeletonFrame.Skeletons)
            {
                Skeleton2DDataExtract.ProcessData(data);
            }
        }

        /// <summary>
        /// Converts a 16-bit grayscale depth frame which includes player indexes into a 32-bit frame that displays different players in different colors
        /// </summary>
        /// <param name="depthFrame16">The depth frame byte array</param>
        /// <returns>A depth frame byte array containing a player image</returns>
        private byte[] ConvertDepthFrame(byte[] depthFrame16)
        {
            for (int i16 = 0, i32 = 0; i16 < depthFrame16.Length && i32 < _depthFrame32.Length; i16 += 2, i32 += 4)
            {
                int player = depthFrame16[i16] & 0x07;
                int realDepth = (depthFrame16[i16 + 1] << 5) | (depthFrame16[i16] >> 3);
                
                // transform 13-bit depth information into an 8-bit intensity appropriate
                // for display (we disregard information in most significant bit)
                var intensity = (byte)(255 - (255 * realDepth / 0x0fff));

                _depthFrame32[i32 + RedIdx] = 0;
                _depthFrame32[i32 + GreenIdx] = 0;
                _depthFrame32[i32 + BlueIdx] = 0;

                // choose different display colors based on player
                switch (player)
                {
                    case 0:
                        _depthFrame32[i32 + RedIdx] = (byte)(intensity / 2);
                        _depthFrame32[i32 + GreenIdx] = (byte)(intensity / 2);
                        _depthFrame32[i32 + BlueIdx] = (byte)(intensity / 2);
                        break;
                    case 1:
                        _depthFrame32[i32 + RedIdx] = intensity;
                        break;
                    case 2:
                        _depthFrame32[i32 + GreenIdx] = intensity;
                        break;
                    case 3:
                        _depthFrame32[i32 + RedIdx] = (byte)(intensity / 4);
                        _depthFrame32[i32 + GreenIdx] = intensity;
                        _depthFrame32[i32 + BlueIdx] = intensity;
                        break;
                    case 4:
                        _depthFrame32[i32 + RedIdx] = intensity;
                        _depthFrame32[i32 + GreenIdx] = intensity;
                        _depthFrame32[i32 + BlueIdx] = (byte)(intensity / 4);
                        break;
                    case 5:
                        _depthFrame32[i32 + RedIdx] = intensity;
                        _depthFrame32[i32 + GreenIdx] = (byte)(intensity / 4);
                        _depthFrame32[i32 + BlueIdx] = intensity;
                        break;
                    case 6:
                        _depthFrame32[i32 + RedIdx] = (byte)(intensity / 2);
                        _depthFrame32[i32 + GreenIdx] = (byte)(intensity / 2);
                        _depthFrame32[i32 + BlueIdx] = intensity;
                        break;
                    case 7:
                        _depthFrame32[i32 + RedIdx] = (byte)(255 - intensity);
                        _depthFrame32[i32 + GreenIdx] = (byte)(255 - intensity);
                        _depthFrame32[i32 + BlueIdx] = (byte)(255 - intensity);
                        break;
                }
            }

            return _depthFrame32;
        }

        /// <summary>
        /// Called when each depth frame is ready
        /// </summary>
        /// <param name="sender">The sender object</param>
        /// <param name="e">Image Frame Ready Event Args</param>
        private void NuiDepthFrameReady(object sender, ImageFrameReadyEventArgs e)
        {
            PlanarImage image = e.ImageFrame.Image;
            Console.Write("////////////////////////////");

            Console.Write(image.GetType());
     
            byte[] convertedDepthFrame = ConvertDepthFrame(image.Bits);
            Console.Write("@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@");
            // Console.Write(convertedDepthFrame.GetValue(8));
            //string str = Encoding.UTF8.GetString(convertedDepthFrame, 0, convertedDepthFrame.Length);
            //File.WriteAllText(@"C:\Users\Manasa\Desktop\kinectdtw-1.0\filename.txt", str);
            //Console.Write("@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@");
            // Console.Write(str);
            BitmapSource myBitmapColor = BitmapSource.Create(
                image.Width, image.Height, 96, 96, PixelFormats.Bgr32, null, convertedDepthFrame, image.Width * 4);
            depthImage.Source = BitmapSource.Create(
                image.Width, image.Height, 96, 96, PixelFormats.Bgr32, null, convertedDepthFrame, image.Width * 4);
            /*
            if (counter%10==0) {
                FileStream streamColor = new FileStream(@"C:\Users\Manasa\Desktop\kinectdtw-1.0\YES__\yesH\imgV" + counter.ToString() + ".png", FileMode.Create);

                BmpBitmapEncoder encoderColor = new BmpBitmapEncoder();
                encoderColor.Frames.Add(BitmapFrame.Create(myBitmapColor));
                encoderColor.Save(streamColor);
                
            }
            */
            //button2.IsEnabled = true;
            Console.Write(depthImage.GetType());
            counter = counter + 1;
            ++_totalFrames;

            DateTime cur = DateTime.Now;
            if (cur.Subtract(_lastTime) > TimeSpan.FromSeconds(1))
            {
                int frameDiff = _totalFrames - _lastFrames;
                _lastFrames = _totalFrames;
                _lastTime = cur;
                frameRate.Text = frameDiff + " fps";
            }
        }

        /// <summary>
        /// Gets the display position (i.e. where in the display image) of a Joint
        /// </summary>
        /// <param name="joint">Kinect NUI Joint</param>
        /// <returns>Point mapped location of sent joint</returns>
        private Point GetDisplayPosition(Joint joint)
        {
            float depthX, depthY;
            _nui.SkeletonEngine.SkeletonToDepthImage(joint.Position, out depthX, out depthY);
            depthX = Math.Max(0, Math.Min(depthX * 320, 320)); // convert to 320, 240 space
            depthY = Math.Max(0, Math.Min(depthY * 240, 240)); // convert to 320, 240 space
            int colorX, colorY;
            var iv = new ImageViewArea();

            // Only ImageResolution.Resolution640x480 is supported at this point
            _nui.NuiCamera.GetColorPixelCoordinatesFromDepthPixel(ImageResolution.Resolution640x480, iv, (int)depthX, (int)depthY, 0, out colorX, out colorY);

            // Map back to skeleton.Width & skeleton.Height
            return new Point((int)(skeletonCanvas.Width * colorX / 640.0), (int)(skeletonCanvas.Height * colorY / 480));
        }

        /// <summary>
        /// Works out how to draw a line ('bone') for sent Joints
        /// </summary>
        /// <param name="joints">Kinect NUI Joints</param>
        /// <param name="brush">The brush we'll use to colour the joints</param>
        /// <param name="ids">The JointsIDs we're interested in</param>
        /// <returns>A line or lines</returns>
        private Polyline GetBodySegment(JointsCollection joints, Brush brush, params JointID[] ids)
        {
            var points = new PointCollection(ids.Length);
            foreach (JointID t in ids)
            {
                points.Add(GetDisplayPosition(joints[t]));
            }

            var polyline = new Polyline();
            polyline.Points = points;
            polyline.Stroke = brush;
            polyline.StrokeThickness = 5;
            return polyline;
        }

        /// <summary>
        /// Runds every time a skeleton frame is ready. Updates the skeleton canvas with new joint and polyline locations.
        /// </summary>
        /// <param name="sender">The sender object</param>
        /// <param name="e">Skeleton Frame Event Args</param>
        private void NuiSkeletonFrameReady(object sender, SkeletonFrameReadyEventArgs e)
        {
            SkeletonFrame skeletonFrame = e.SkeletonFrame;
            int iSkeleton = 0;
            var brushes = new Brush[6];
            brushes[0] = new SolidColorBrush(Color.FromRgb(255, 0, 0));
            brushes[1] = new SolidColorBrush(Color.FromRgb(0, 255, 0));
            brushes[2] = new SolidColorBrush(Color.FromRgb(64, 255, 255));
            brushes[3] = new SolidColorBrush(Color.FromRgb(255, 255, 64));
            brushes[4] = new SolidColorBrush(Color.FromRgb(255, 64, 255));
            brushes[5] = new SolidColorBrush(Color.FromRgb(128, 128, 255));

            skeletonCanvas.Children.Clear();
            foreach (SkeletonData data in skeletonFrame.Skeletons)
            {
                if (SkeletonTrackingState.Tracked == data.TrackingState)
                {
                    // Draw bones
                    Brush brush = brushes[iSkeleton % brushes.Length];
                    skeletonCanvas.Children.Add(GetBodySegment(data.Joints, brush, JointID.HipCenter, JointID.Spine, JointID.ShoulderCenter, JointID.Head));
                    skeletonCanvas.Children.Add(GetBodySegment(data.Joints, brush, JointID.ShoulderCenter, JointID.ShoulderLeft, JointID.ElbowLeft, JointID.WristLeft, JointID.HandLeft));
                    skeletonCanvas.Children.Add(GetBodySegment(data.Joints, brush, JointID.ShoulderCenter, JointID.ShoulderRight, JointID.ElbowRight, JointID.WristRight, JointID.HandRight));
                    skeletonCanvas.Children.Add(GetBodySegment(data.Joints, brush, JointID.HipCenter, JointID.HipLeft, JointID.KneeLeft, JointID.AnkleLeft, JointID.FootLeft));
                    skeletonCanvas.Children.Add(GetBodySegment(data.Joints, brush, JointID.HipCenter, JointID.HipRight, JointID.KneeRight, JointID.AnkleRight, JointID.FootRight));
                    
                    // Draw joints
                    foreach (Joint joint in data.Joints)
                    {
                        Point jointPos = GetDisplayPosition(joint);
                        var jointLine = new Line();
                        jointLine.X1 = jointPos.X - 3;
                        jointLine.X2 = jointLine.X1 + 6;
                        jointLine.Y1 = jointLine.Y2 = jointPos.Y;
                        jointLine.Stroke = _jointColors[joint.ID];
                        jointLine.StrokeThickness = 6;
                        skeletonCanvas.Children.Add(jointLine);
                    }
                }

                iSkeleton++;
            } // for each skeleton
        }

        /// <summary>
        /// Called every time a video (RGB) frame is ready
        /// </summary>
        /// <param name="sender">The sender object</param>
        /// <param name="e">Image Frame Ready Event Args</param>
        private void NuiColorFrameReady(object sender, ImageFrameReadyEventArgs e)
        {
            // 32-bit per pixel, RGBA image
            PlanarImage image = e.ImageFrame.Image;
            byte[] rbg = image.Bits;
            videoImage.Source = BitmapSource.Create(
                image.Width, image.Height, 96, 96, PixelFormats.Bgr32, null, rbg, image.Width * image.BytesPerPixel);
            // Console.Write(str);
            BitmapSource myBitmapColor1 = BitmapSource.Create(
                image.Width, image.Height, 96, 96, PixelFormats.Bgr32, null, rbg, image.Width * 4);
           /* 
            if (counter1 % 10 == 0)
            {
                FileStream streamColor1 = new FileStream(@"C:\Users\Manasa\Desktop\kinectdtw-1.0\YES__\yesH1\imgV" + counter1.ToString() + ".png", FileMode.Create);

                BmpBitmapEncoder encoderColor1 = new BmpBitmapEncoder();
                encoderColor1.Frames.Add(BitmapFrame.Create(myBitmapColor1));
                encoderColor1.Save(streamColor1);

            }
            */
            counter1 = counter1 + 1;
        }

        /// <summary>
        /// Runs after the window is loaded
        /// </summary>
        /// <param name="sender">The sender object</param>
        /// <param name="e">Routed Event Args</param>
        private void WindowLoaded(object sender, RoutedEventArgs e)
        {
            _nui = new Runtime();

            try
            {
                _nui.Initialize(RuntimeOptions.UseDepthAndPlayerIndex | RuntimeOptions.UseSkeletalTracking |
                               RuntimeOptions.UseColor);
            }
            catch (InvalidOperationException)
            {
                System.Windows.MessageBox.Show("Runtime initialization failed. Please make sure Kinect device is plugged in.");
                return;
            }

            try
            {
                _nui.VideoStream.Open(ImageStreamType.Video, 2, ImageResolution.Resolution640x480, ImageType.Color);
                _nui.DepthStream.Open(ImageStreamType.Depth, 2, ImageResolution.Resolution320x240, ImageType.DepthAndPlayerIndex);
            }
            catch (InvalidOperationException)
            {
                System.Windows.MessageBox.Show(
                    "Failed to open stream. Please make sure to specify a supported image type and resolution.");
                return;
            }

            _lastTime = DateTime.Now;

            _dtw = new DtwGestureRecognizer(12, 0.6, 2, 2, 10);
            _video = new ArrayList();

            // If you want to see the depth image and frames per second then include this
            // I'mma turn this off 'cos my 'puter is proper slow
            _nui.DepthFrameReady += NuiDepthFrameReady;

            _nui.SkeletonFrameReady += NuiSkeletonFrameReady;
            _nui.SkeletonFrameReady += SkeletonExtractSkeletonFrameReady;

            // If you want to see the RGB stream then include this
            _nui.VideoFrameReady += NuiColorFrameReady;

            Skeleton2DDataExtract.Skeleton2DdataCoordReady += NuiSkeleton2DdataCoordReady;

            // Update the debug window with Sequences information
            dtwTextOutput.Text = _dtw.RetrieveText();

            Debug.WriteLine("Finished Window Loading");
        }

        /// <summary>
        /// Runs some tidy-up code when the window is closed. This is especially important for our NUI instance because the Kinect SDK is very picky about this having been disposed of nicely.
        /// </summary>
        /// <param name="sender">The sender object</param>
        /// <param name="e">Event Args</param>
        private void WindowClosed(object sender, EventArgs e)
        {
            Debug.WriteLine("Stopping NUI");
            _nui.Uninitialize();
            Debug.WriteLine("NUI stopped");
            Environment.Exit(0);
        }

        /// <summary>
        /// Runs every time our 2D coordinates are ready.
        /// </summary>
        /// <param name="sender">The sender object</param>
        /// <param name="a">Skeleton 2Ddata Coord Event Args</param>
        private void NuiSkeleton2DdataCoordReady(object sender, Skeleton2DdataCoordEventArgs a)
        {
            currentBufferFrame.Text = _video.Count.ToString();

            // We need a sensible number of frames before we start attempting to match gestures against remembered sequences
            if (_video.Count > MinimumFrames && _capturing == false)
            {
                ////Debug.WriteLine("Reading and video.Count=" + video.Count);
                string s = _dtw.Recognize(_video);
                results.Text = "Recognised as: " + s;
                if (!s.Contains("__UNKNOWN"))
                {
                    int i = 0;
                    while (i < 1000)
                    {
                        i = i + 1;
                    }
                    // There was no match so reset the buffer
                    _video = new ArrayList();
                    
                }
                            
            }


            // Ensures that we remember only the last x frames
            if (_video.Count > BufferSize)
            {
                // If we are currently capturing and we reach the maximum buffer size then automatically store
                if (_capturing)
                {
                    DtwStoreClick(null, null);
                }
                else
                {
                    // Remove the first frame in the buffer
                    _video.RemoveAt(0);
                }
            }

            // Decide which skeleton frames to capture. Only do so if the frames actually returned a number. 
            // For some reason my Kinect/PC setup didn't always return a double in range (i.e. infinity) even when standing completely within the frame.
            // TODO Weird. Need to investigate this
            if (!double.IsNaN(a.GetPoint(0).X))
            {
                // Optionally register only 1 frame out of every n
                _flipFlop = (_flipFlop + 1) % Ignore;
                if (_flipFlop == 0)
                {
                    _video.Add(a.GetCoords());
                }
            }

            // Update the debug window with Sequences information
            //dtwTextOutput.Text = _dtw.RetrieveText();
        }

        /// <summary>
        /// Read mode. Sets our control variables and button enabled states
        /// </summary>
        /// <param name="sender">The sender object</param>
        /// <param name="e">Routed Event Args</param>
        private void DtwReadClick(object sender, RoutedEventArgs e)
        {
            // Set the buttons enabled state
            dtwRead.IsEnabled = false;
            dtwCapture.IsEnabled = true;
            dtwStore.IsEnabled = false;

            // Set the capturing? flag
            _capturing = false;

            // Update the status display
            status.Text = "Reading";
        }

        /// <summary>
        /// Starts a countdown timer to enable the player to get in position to record gestures
        /// </summary>
        /// <param name="sender">The sender object</param>
        /// <param name="e">Routed Event Args</param>
        private void DtwCaptureClick(object sender, RoutedEventArgs e)
        {
            _captureCountdown = DateTime.Now.AddSeconds(CaptureCountdownSeconds);

            _captureCountdownTimer = new Timer();
            _captureCountdownTimer.Interval = 50;
            _captureCountdownTimer.Start();
            _captureCountdownTimer.Tick += CaptureCountdown;
        }

        /// <summary>
        /// The method fired by the countdown timer. Either updates the countdown or fires the StartCapture method if the timer expires
        /// </summary>
        /// <param name="sender">The sender object</param>
        /// <param name="e">Event Args</param>
        private void CaptureCountdown(object sender, EventArgs e)
        {
            if (sender == _captureCountdownTimer)
            {
                if (DateTime.Now < _captureCountdown)
                {
                    status.Text = "Wait " + ((_captureCountdown - DateTime.Now).Seconds + 1) + " seconds";
                }
                else
                {
                    _captureCountdownTimer.Stop();
                    status.Text = "Recording gesture";
                    StartCapture();
                }
            }
        } 

        /// <summary>
        /// Capture mode. Sets our control variables and button enabled states
        /// </summary>
        private void StartCapture()
        {
            // Set the buttons enabled state
            dtwRead.IsEnabled = false;
            dtwCapture.IsEnabled = false;
            dtwStore.IsEnabled = true;

            // Set the capturing? flag
            _capturing = true;

            ////_captureCountdownTimer.Dispose();

            status.Text = "Recording gesture" + gestureList.Text;

            // Clear the _video buffer and start from the beginning
            _video = new ArrayList();
        }

        /// <summary>
        /// Stores our gesture to the DTW sequences list
        /// </summary>
        /// <param name="sender">The sender object</param>
        /// <param name="e">Routed Event Args</param>
        private void DtwStoreClick(object sender, RoutedEventArgs e)
        {
            // Set the buttons enabled state
            dtwRead.IsEnabled = false;
            dtwCapture.IsEnabled = true;
            dtwStore.IsEnabled = false;

            // Set the capturing? flag
            _capturing = false;

            status.Text = "Remembering " + gestureList.Text;

            // Add the current video buffer to the dtw sequences list
            _dtw.AddOrUpdate(_video, gestureList.Text);
            results.Text = "Gesture " + gestureList.Text + "added";

            // Scratch the _video buffer
            _video = new ArrayList();

            // Switch back to Read mode
            DtwReadClick(null, null);
        }

        /// <summary>
        /// Stores our gesture to the DTW sequences list
        /// </summary>
        /// <param name="sender">The sender object</param>
        /// <param name="e">Routed Event Args</param>
        private void DtwSaveToFile(object sender, RoutedEventArgs e)
        {
            SaveFileDialog dlg = new SaveFileDialog();

            string FileName = "";
            dlg.InitialDirectory = "C:";
            dlg.FileName = "";
            dlg.Title = "Save File As";
            //dlg.Filter = "Text Files|*.txt|All Files|*.xls|*.*";
            if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {



                string fileName = dlg.FileName+ DateTime.Now.ToString("yyyy-MM-dd_HH-mm") + ".txt";
                System.IO.File.WriteAllText(fileName, _dtw.RetrieveText());
                status.Text = "Saved to " + fileName;
            }





            
        }

        /// <summary>
        /// Loads the user's selected gesture file
        /// </summary>
        /// <param name="sender">The sender object</param>
        /// <param name="e">Routed Event Args</param>
        private void DtwLoadFile(object sender, RoutedEventArgs e)
        {
            // Create OpenFileDialog
            Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog();

            // Set filter for file extension and default file extension
            dlg.DefaultExt = ".txt";
            dlg.Filter = "Text documents (.txt)|*.txt";

            dlg.InitialDirectory = @"C:\";

            // Display OpenFileDialog by calling ShowDialog method
            Nullable<bool> result = dlg.ShowDialog();

            // Get the selected file name and display in a TextBox
            if (result == true)
            {
                // Open document
                LoadGesturesFromFile(dlg.FileName);
                dtwTextOutput.Text = _dtw.RetrieveText();
                status.Text = "Gestures loaded!";
            } 
        }

        /// <summary>
        /// Stores our gesture to the DTW sequences list
        /// </summary>
        /// <param name="sender">The sender object</param>
        /// <param name="e">Routed Event Args</param>
        private void DtwShowGestureText(object sender, RoutedEventArgs e)
        {
                dtwTextOutput.Text = _dtw.RetrieveText();
  
        }
        
        private void DtwInstructions(object sender, RoutedEventArgs e)
        {
            string inst =   "1. To start, step into frame and allow Kinect to track your skeleton \n 2.Select a gesture you wish to record from the ComboBox\n3.Perform a gesture over 32 frames - watch the frame counter above\n4.Your gesture will be stored automatically and return to read mode\n5.Test your gesture. If you're unhappy, record the gesture again to overwrite\n6.Record some other gestures and test them\n7.Optionally save your gestures to file for future loading\n8.Improve this script, add your own gestures, share and enjoy Kinect!";
            System.Windows.Forms.MessageBox.Show(inst);
        }
        
    }
}