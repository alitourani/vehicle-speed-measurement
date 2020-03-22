using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Emgu.CV;
using Emgu.CV.BgSegm;
using Emgu.CV.Structure;
using Emgu.CV.Util;
using System.Diagnostics;
using AForge.Imaging.Filters;
using System.IO;
using System.Xml;

namespace car_speed_calculator
{
    public partial class Form1 : Form
    {
        // Capture Properties
        double FPS;                                 // Video's Frame-rate (Frames-per-Second)
        double TotalFrames;                         // Video's Total Freams
        int FrameCount;                             // Frame Counter
        int CaptureH, CaptureW;                     // Width and Height of capture
        VideoCapture _capture;                      // Capture Video
        Mat frame, frame_gray, mask;                // Matrices of Frame, Grayscaled, and Subtraction Mask
        Mat Cropped_mask, Cropped_frame;            // Matrices of Cropping

        // Form Controllers
        bool play = true;                           // Video Controller
        bool openEnabled = true;                    // Filter Open Controller
        bool closeEnabled = true;                   // Filter Close Controller
        bool erodeEnabled = true;                   // Filter Erode Controller
        bool dilateEnabled = true;                  // Filter Dilate Controller
        bool gaussianEnabled = true;                // Filter Gaussian Controller
        bool FillHolesEnabled = true;               // Filter Fill-Holes Controller
        bool ROIEnabled = true;                     // ROI Controller
        ImageList ImgListViolation;

        // ROI
        System.Drawing.Rectangle speedBox;
        int speedBoxX, speedBoxY, speedBoxW, speedBoxH;

        // Pre-processing Phase
        int openKernelSize, closeKernelSize, erodedKernelSize, dilateKernelSize, fillHolesKernelSize;
        double gaussianBlurKernel;

        // Configuration Speed-Related Values
        double Ref_dist_m;                          // Reference Distance in Meters and Pixels
        double CalibrationVehicleLength;            // Calibration: the size of a vehicle
        double PixelFrameDensity;                   // How many pixels in each frame?
        int speedLimit;                             // Speed Limit for Violation Detection
        double MappingConstant;                     // To map correctly

        // Vehicle Detection
        BackgroundSubtractorMOG mog;                // Background Subtraction Method
        int mog_history;                            // Number of History Frames to be kept
        int mog_nMixtures;                          // Size of Gaussian Mixture Parameters
        double mog_backgroundRatio;                 // BG-Ratio Value
        int mog_noiseSigma;                         // Noise
                                                    // -- Emgu.CV.VideoSurveillance.BackgroundSubtractor mog2;

        // Vehicle Tracking        
        int VehicleID_1;                            // ID of Each Detected Vehicle in ROI-1
        int ROI;                                    // ID of Each Detected Vehicle in ROI-1
        Emgu.CV.Cvb.CvTracks _tracker;
        bool isTracking;                            // Is the tracker active?
        bool wasTracking;                           // Did it Changed?
        double previousCentroidY, currentCentroidY;
        int motionThreshold;                        // if box.Width > (speedBoxW / motionThreshold) then Show Rect
        int MaxDetectedCarSize;                     // Max Size of Detected Car for ListView
        int currentDetectedCarSize;                 // Current Size of Detected Car for ListView
        Bitmap DetectedVehicle;                     // Picture of Detected Car for ListView
        int trackingFrameCounter;                   // To Count Vehicle Presence Frames for speed Calculation
        double trackingDisplacement;                   // Sum of the Displacements of Vehicle

        // Report
        XmlWriter XMLFile;

        public Form1() {
            InitializeComponent();

            // Initialize PictureBoxes
            pictureBox4.Image = Properties.Resources.Initialize;
            pictureBoxDiffrence.Image = Properties.Resources.Initialize;
            pictureBoxPreprocess.Image = Properties.Resources.Initialize;
            pictureBoxROI.Image = Properties.Resources.Initialize;
            pictureBox4.SizeMode = PictureBoxSizeMode.StretchImage;
            pictureBoxDiffrence.SizeMode = PictureBoxSizeMode.StretchImage;
            pictureBoxPreprocess.SizeMode = PictureBoxSizeMode.StretchImage;
            pictureBoxROI.SizeMode = PictureBoxSizeMode.StretchImage;

            // Disable Buttons Before Opening a Video File
            buttonNext.Enabled = false;
            buttonPrevious.Enabled = false;
            buttonPlay.Enabled = false;
            buttonPause.Enabled = false;

            // XML Creator
            XMLFile = XmlWriter.Create("Report.xml");
            XMLFile.WriteStartDocument();
            XMLFile.WriteStartElement("VideoSpeedMeasurement");
        }

        //Select Video Button
        private void button8_Click(object sender, EventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog();
            if (dialog.ShowDialog() == DialogResult.OK) {
                _capture = new VideoCapture(dialog.FileName);
                if (dialog.FileName == "") {
                    MessageBox.Show("Select a video for speed measurement", "Error!", MessageBoxButtons.OK, MessageBoxIcon.Error);
                } else {
                    try {
                        // Initialize Lables, Trackbars, Parameters, etc.
                        initValues(_capture);

                        // Initialize frames
                        frame = new Mat();
                        frame_gray = new Mat();
                        mask = new Mat();

                        // Initialize Background Subtraction Method
                        mog = new BackgroundSubtractorMOG(mog_history, mog_nMixtures, mog_backgroundRatio, mog_noiseSigma);
                        //mog2 = new Emgu.CV.VideoSurveillance.BackgroundSubtractorMOG2();

                        _tracker = new Emgu.CV.Cvb.CvTracks();
                        _capture.ImageGrabbed += MyProcess;
                        _capture.Start();
                    } catch (Exception err) {
                        MessageBox.Show("Error in reading the video file \r\n" + err.ToString(), "Error!", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        // Procedure uses as timer
        private void MyProcess(object sender, EventArgs e)
        {
            // Changing UI Labels (Make thread)
            labelCurrent.Invoke((MethodInvoker)delegate {
                // Check Pause button
                if (!play) {
                    CapturePaused();
                } else {
                    CapturePlayed();
                }

                // Frame-Rate Change
                labelCurrent.Text = FrameCount.ToString();                
                labelROI_H.Text = speedBoxH.ToString();
                labelPixelDensity.Text = PixelFrameDensity.ToString();
            });
            FrameCount++;

            // Capture the frame of video
            _capture.Retrieve(frame);
            
            // Show an adjustable ROI
            if (ROIEnabled) {
                speedBox = new Rectangle(speedBoxX, speedBoxY, speedBoxW, speedBoxH);
                //CvInvoke.PutText(frame, "" + speedBoxY, new Point(speedBoxX, speedBoxY - 10), Emgu.CV.CvEnum.FontFace.HersheyComplexSmall, 1, new MCvScalar(0, 255, 0), 1);
                //CvInvoke.PutText(frame, "" + (speedBoxY + speedBoxH), new Point(speedBoxX, speedBoxY + speedBoxH + 20), Emgu.CV.CvEnum.FontFace.HersheyComplexSmall, 1, new MCvScalar(0, 255, 0), 1);
            } else {
                speedBox = new Rectangle(0, 0, frame.Width, frame.Height);
            }

            CvInvoke.Rectangle(frame, speedBox, new MCvScalar(0, 255, 0), 2); // Green

            PixelFrameDensity = Math.Round((Ref_dist_m + CalibrationVehicleLength) / speedBoxH , 3);

            pictureBox4.Image = new Bitmap(frame.Bitmap);

            // Convert Frame to Grayscale
            CvInvoke.CvtColor(frame, frame_gray, Emgu.CV.CvEnum.ColorConversion.Bgra2Gray, 1);

            // Improved Adaptive Background Mixture Model for Real-time Tracking
            mog.Apply(frame_gray, mask);
            //mog2.Apply(frame_gray, mask);

            pictureBoxDiffrence.Image = new Bitmap(mask.Bitmap);
            
            // Post Processing
            postProcessing();

            // Crop to ROI
            CropROI();

            // Track
            TrackinROI();
            
            // Check End of Video
            if (FrameCount == TotalFrames) {
                MessageBox.Show("End of video", "", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                _capture.Dispose();
                // Change UI
                buttonNext.Invoke((MethodInvoker)delegate {
                    buttonNext.Enabled = true;
                    buttonPrevious.Enabled = true;
                    button_select_video.Enabled = true;
                });
            }
        }

        private void initValues(VideoCapture previewCapture)
        {
            // Show Frame-rate on UI
            FPS = 10;                                       // Default value
            FPS = Convert.ToDouble(previewCapture.GetCaptureProperty(Emgu.CV.CvEnum.CapProp.Fps));
            labelFps.Text = FPS.ToString();

            // Show TotalFrames on UI
            TotalFrames = 0;
            TotalFrames = Convert.ToDouble(previewCapture.GetCaptureProperty(Emgu.CV.CvEnum.CapProp.FrameCount));
            labelTotalFrames.Text = TotalFrames.ToString();

            // Frame Counter
            FrameCount = 0;

            // Video Configuration
            CaptureH = 0;
            CaptureW = 0;
            CaptureH = Convert.ToInt32(previewCapture.GetCaptureProperty(Emgu.CV.CvEnum.CapProp.FrameHeight));
            CaptureW = Convert.ToInt32(previewCapture.GetCaptureProperty(Emgu.CV.CvEnum.CapProp.FrameWidth));
            labelVideoH.Text = CaptureH + "";
            labelVideoW.Text = CaptureW + "";
            Ref_dist_m = 2.7;                               // Referece Mapping in Meters (Size of ROI in Meter)
            CalibrationVehicleLength = 5.7;                 // Calibration Size
            textBox_Ref_meter.Text = Ref_dist_m.ToString();
            MappingConstant = 2.18;                         // For HD video input (Set01Vid01)
            CalibrationConstantTextbox.Text = MappingConstant.ToString();
            PixelFrameDensity = 1.00;            

            // Enable Buttons
            buttonNext.Enabled = true;
            buttonPrevious.Enabled = true;
            buttonPlay.Enabled = true;
            buttonPause.Enabled = true;

            // ROI Initialization (Center of Screen)
            speedBoxW = CaptureW / 3;
            speedBoxH = CaptureH / 3;
            speedBoxX = (CaptureW / 2) - (speedBoxW / 2);
            speedBoxY = (CaptureH / 2) - (speedBoxH / 2);

            // Initialize ROI Trackbar range
            trackBarRoiH.Minimum = CaptureH / 10;
            trackBarRoiH.Maximum = CaptureH / 2;
            trackBarRoiWidth.Minimum = CaptureW / 10;
            trackBarRoiWidth.Maximum = CaptureW / 2;
            trackBarRoiX.Minimum = 5;
            trackBarRoiX.Maximum = (CaptureW / 2) - 5;
            trackBarRoiY.Minimum = 5;
            trackBarRoiY.Maximum = (CaptureH / 2) - 5;

            // Initialize Preporcessing Filter Trackbar range
            openKernelSize = 30;
            closeKernelSize = 25;
            erodedKernelSize = 3;
            dilateKernelSize = 3;
            gaussianBlurKernel = 5;
            fillHolesKernelSize = 40;
            trackBarOpening.Value = openKernelSize;
            trackBarClosing.Value = closeKernelSize;
            trackBarErode.Value = erodedKernelSize;
            trackBarDilate.Value = dilateKernelSize;
            trackBarFillHoles.Value = fillHolesKernelSize;
            trackBarGaussian.Value = Convert.ToInt32(gaussianBlurKernel);
            labelOpeningValue.Text = openKernelSize.ToString();
            labelClosingValue.Text = closeKernelSize.ToString();
            labelErodeValue.Text = erodedKernelSize.ToString();
            labelDilateValue.Text = dilateKernelSize.ToString();
            labelFillHolesValue.Text = fillHolesKernelSize.ToString();
            labelGaussianValue.Text = Convert.ToInt32(gaussianBlurKernel).ToString();

            // Initialize Un-necessary Filters
            // Erode
                label8.ForeColor = Color.Gray;
                labelErodeValue.ForeColor = Color.Gray;
                trackBarErode.Enabled = false;
                erodeEnabled = false;
            // Dilate
                label14.ForeColor = Color.Gray;
                labelDilateValue.ForeColor = Color.Gray;
                trackBarDilate.Enabled = false;
                dilateEnabled = false;
            // Gaussian
                label10.ForeColor = Color.Gray;
                labelGaussianValue.ForeColor = Color.Gray;
                trackBarGaussian.Enabled = false;
                gaussianEnabled = false;
            // Fill-holes
                label23.ForeColor = Color.Gray;
                labelFillHolesValue.ForeColor = Color.Gray;
                trackBarFillHoles.Enabled = false;
                FillHolesEnabled = false;

            // Initialize Detection Values
            mog_history = 20;
            mog_nMixtures = 15;
            mog_backgroundRatio = 0.5;
            mog_noiseSigma = 0;
            trackBar_mog_history.Value = mog_history;
            trackBar_mog_nMixtures.Value = mog_nMixtures;
            trackBar_mog_backgroundRatio.Value = Convert.ToInt32(mog_backgroundRatio*10);
            trackBar_mog_noiseSigma.Value = mog_noiseSigma;
            label_mog_history.Text = mog_history.ToString();
            label_mog_nMixtures.Text = mog_nMixtures.ToString();
            label_mog_backgroundRatio.Text = mog_backgroundRatio.ToString();
            label_mog_noiseSigma_.Text = mog_noiseSigma.ToString();

            // Tracker Centroids
            previousCentroidY = -1;
            currentCentroidY = -1;
            wasTracking = false;
            isTracking = false;
            VehicleID_1 = 1;
            ROI = 2;
            motionThreshold = 3;
            labelMotionThresholdValue.Text = motionThreshold.ToString();
            trackBarMotionThreshold.Value = motionThreshold;
            trackBarMotionThreshold.Minimum = 1;
            trackBarMotionThreshold.Maximum = 300;
            trackingFrameCounter = 0;
            trackingDisplacement = 0;

            // Tracker ListView
            listViewTracking.Items.Clear();

            // Speed Limit (Violation)
            ImgListViolation = new ImageList();
            ImgListViolation.ImageSize = new Size(120, 120);
            listViewViolation.SmallImageList = ImgListViolation;
            listViewViolation.Items.Clear();
            speedLimit = 90;
            labelViolationSet.Text = speedLimit.ToString();
            trackBarViolationSet.Value = speedLimit;
            trackBarViolationSet.Minimum = 40;
            trackBarViolationSet.Maximum = 120;
            MaxDetectedCarSize = 0;
            currentDetectedCarSize = 0;

            // Report

        }

        private void CapturePaused() {
            play = false;
            buttonNext.Enabled = true;
            buttonPrevious.Enabled = true;
            button_select_video.Enabled = true;
            _capture.Stop();            
        }

        private void CapturePlayed() {
            play = true;
            buttonNext.Enabled = false;
            buttonPrevious.Enabled = false;
            button_select_video.Enabled = false;
        }

        private void postProcessing()
        {
            // Closing
            if (closeEnabled) {
                var kernelCl = CvInvoke.GetStructuringElement(Emgu.CV.CvEnum.ElementShape.Rectangle, new Size(closeKernelSize, closeKernelSize), new Point(-1, -1));
                CvInvoke.MorphologyEx(mask, mask, Emgu.CV.CvEnum.MorphOp.Close, kernelCl, new Point(-1, -1), 1, Emgu.CV.CvEnum.BorderType.Default, new MCvScalar());
            }

            // Fill-Holes
            if (FillHolesEnabled) {
                mask = Fill(mask, fillHolesKernelSize, fillHolesKernelSize);
            }

            // Opening
            if (openEnabled) {
                var kernelOp = CvInvoke.GetStructuringElement(Emgu.CV.CvEnum.ElementShape.Rectangle, new Size(openKernelSize, openKernelSize), new Point(-1, -1));
                CvInvoke.MorphologyEx(mask, mask, Emgu.CV.CvEnum.MorphOp.Open, kernelOp, new Point(-1, -1), 1, Emgu.CV.CvEnum.BorderType.Default, new MCvScalar());
            }

            // Erode
            if (erodeEnabled) {
                var kernelErode = CvInvoke.GetStructuringElement(Emgu.CV.CvEnum.ElementShape.Rectangle, new Size(erodedKernelSize, erodedKernelSize), new Point(-1, -1));
                CvInvoke.Erode(mask, mask, kernelErode, new Point(-1, -1), 1, Emgu.CV.CvEnum.BorderType.Default, default(MCvScalar));
            }

            // Dilate
            if (dilateEnabled) {
                var kernelDilate = CvInvoke.GetStructuringElement(Emgu.CV.CvEnum.ElementShape.Rectangle, new Size(dilateKernelSize, dilateKernelSize), new Point(-1, -1));
                CvInvoke.Dilate(mask, mask, kernelDilate, new Point(-1, -1), 1, Emgu.CV.CvEnum.BorderType.Default, default(MCvScalar));
            }

            if (gaussianEnabled) {
                CvInvoke.GaussianBlur(mask, mask, new Size(0, 0), gaussianBlurKernel);
            }

            pictureBoxPreprocess.Image = new Bitmap(mask.Bitmap);
        }

        private Mat Fill(Mat input, int maxWidth, int maxHeight)
        {
            Stopwatch w = new Stopwatch();
            w.Start();

            Bitmap bm = input.Bitmap;
            Image<Gray, byte> im = new Image<Gray, byte>(bm);
            FillHoles fh = new FillHoles();
            
            im = im.Dilate(1);

            fh.MaxHoleWidth = maxWidth;
            fh.MaxHoleHeight = maxHeight;

            try {
                bm = fh.Apply(im.Bitmap);
            } catch (Exception e) {
                MessageBox.Show("" + e.ToString(), "System Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            im = new Image<Gray, byte>(bm);
            im = im.Erode(1);

            w.Stop();
            input = im.Mat;
            
            return input;
        }

        private void CropROI() {
            try
            {
                Cropped_mask = new Mat(mask, speedBox);
                Cropped_frame = new Mat(frame, speedBox);
            }
            catch (Exception e) {
                play = false;
                MessageBox.Show("Error!:" + e.ToString(), "Execution Error", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
            }            
        }

        private void TrackinROI() {
            
            using (VectorOfVectorOfPoint contours = new VectorOfVectorOfPoint()) {

                // Build list of contours
                CvInvoke.FindContours(Cropped_mask, contours, null, Emgu.CV.CvEnum.RetrType.List, Emgu.CV.CvEnum.ChainApproxMethod.ChainApproxSimple);
                
                // Selecting largest contour
                if (contours.Size > 0) {
                    double maxArea = 0;
                    int chosen = 0;
                    for (int i = 0; i < contours.Size; i++) {
                        VectorOfPoint contour = contours[i];
                        double area = CvInvoke.ContourArea(contour);
                        if (area > maxArea) {
                            maxArea = area;
                            chosen = i;
                        }
                    }
                    
                    Rectangle box = CvInvoke.BoundingRectangle(contours[chosen]);

                    // Thresholding
                    if (box.Width > (speedBoxW / motionThreshold) && box.Height > (speedBoxH / motionThreshold)) {
                        //CvInvoke.Polylines(Cropped_frame, contours[chosen], true, new Bgr(Color.Red).MCvScalar);
                        CvInvoke.Rectangle(Cropped_frame, box, new MCvScalar(255.0, 255.0, 255.0), 2);

                        if (!isTracking) {              // If it didn't been tracked in previous frame: calcelate Entrace values
                            isTracking = true;
                            previousCentroidY = box.Top + (box.Bottom - box.Top)/2; // Center
                            //MessageBox.Show("Enterance: " + previousCentroidY, "Vehicle Entered!");
                        } else {                        // Else if the tracker was working in the previous
                            // new
                            previousCentroidY = currentCentroidY;                            

                            currentCentroidY = box.Top + (box.Bottom - box.Top) / 2;

                            // Show centroid point
                            CvInvoke.Line(Cropped_frame, new Point(0, Convert.ToInt32(currentCentroidY)) , new Point(speedBoxW, Convert.ToInt32(currentCentroidY)), new MCvScalar(128, 20, 215), 1);
                            //CvInvoke.Rectangle(Cropped_frame, new Rectangle(box.X + (box.Width / 2), Convert.ToInt32(currentCentroidY), 2, 2), new MCvScalar(255.0, 0, 255.0), 2);
                        }

                        CvInvoke.Rectangle(Cropped_frame, box, new MCvScalar(255.0, 255.0, 255.0), 2);

                        double tempDisplacement = Math.Abs(previousCentroidY - currentCentroidY);

                        if (previousCentroidY>-1 && currentCentroidY>-1 && tempDisplacement>=0) {

                            trackingDisplacement += tempDisplacement;
                            trackingFrameCounter++;

                            //MessageBox.Show("Distance in pixel: " + (previousCentroidY - currentCentroidY) +
                            //    ", Frames: " + trackingFrameCounter + ", Total Displacement: " + trackingDisplacement, "test");

                            ListViewUpdate(ROI, (previousCentroidY - currentCentroidY) + "", "" + trackingFrameCounter);
                        }

                        try {
                            currentDetectedCarSize = Math.Abs(box.Bottom - box.Top);

                            if (currentDetectedCarSize >= MaxDetectedCarSize) {
                                MaxDetectedCarSize = currentDetectedCarSize;
                                DetectedVehicle = new Bitmap(Cropped_frame.Bitmap);
                            }
                            
                        } catch (Exception err) {
                            MessageBox.Show("" + err.ToString(), "Error!", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }

                    } else {
                        wasTracking = isTracking;
                        isTracking = false;

                        if (wasTracking) {          // If it was tracking in previous frame                            
                            calculateSpeed(box, trackingFrameCounter, trackingDisplacement);
                            VehicleID_1++;                            
                        } else {                    // If it was not tracking in previous frame
                            
                        }
                        
                    }

                    pictureBoxROI.Image = new Bitmap(Cropped_frame.Bitmap);

                }
            }
        }

        private void ListViewUpdate(int ROI, String Distance, String Frames) {
            isTracking = true;
            String VehicleIDCreated = "Reg" + ROI + "_Veh" + VehicleID_1;

            try {
                listViewTracking.Invoke((MethodInvoker)delegate {
                    var item = new ListViewItem(new[] { VehicleIDCreated, ""+ROI, Frames, Distance });
                    listViewTracking.Items.Add(item);
                });
            } catch (Exception err) {
                MessageBox.Show("" + err.ToString(), "Error!", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void calculateSpeed(Rectangle rect, int totalFrames, double totalDisplacement)
        {
            
            // Displacement in pixels
            double displacement = totalDisplacement / (totalFrames + 4);    // Each Frame's Displacement

            double distance = displacement * PixelFrameDensity * MappingConstant;     // Distance in Meters

            distance = Math.Round(distance * FPS * 3.6 , 3);              // Speed in m/s and then in Km/h

            // New aspect!
            //distance = Math.Round((Ref_dist_m * FPS / (totalFrames + 4)) * 3.6, 3);

            if (distance > 10)
            {
                MessageBox.Show("Total Frames: " + totalFrames + "\nTotalDisplacement: " + totalDisplacement
                + "\nSpeed: " + distance + " Km/h", "SpeedCalc");

                // Lable Speed Change
                labelSpeedValue.Invoke((MethodInvoker)delegate {
                    labelSpeedValue.Text = distance.ToString();
                });

                // Report XML
                String VehicleIDCreated = "Reg" + ROI + "_Veh" + VehicleID_1;

                XMLFile.WriteStartElement("vehicle");
                XMLFile.WriteAttributeString("frameEnd", FrameCount + "");
                XMLFile.WriteAttributeString("vehicleID", VehicleIDCreated + "");
                XMLFile.WriteAttributeString("region", ROI + "");
                XMLFile.WriteAttributeString("totalFrames", totalFrames + "");
                XMLFile.WriteAttributeString("speed", distance + "");
                XMLFile.WriteEndElement();
            }

            MaxDetectedCarSize = 0;
            previousCentroidY = -1;
            currentCentroidY = -1;
            trackingFrameCounter = 0;
            trackingDisplacement = 0;

            //if (distance > speedLimit)
            violenceDetection(distance, totalFrames);
        }

        private void violenceDetection(double carSpeed, int totalFrames) {
            String violationStatus = "";
            String VehicleIDCreated = "Reg" + ROI + "_Veh" + VehicleID_1;

            if (carSpeed > speedLimit)
                violationStatus = "Violation: " + (carSpeed - speedLimit) + " Exceeded";

            // Make thread to change UI
            try {
                listViewViolation.Invoke((MethodInvoker)delegate {
                    ImgListViolation.Images.Add("Vehicle_" + VehicleID_1, DetectedVehicle);
                    var item = new ListViewItem(new[] { "", VehicleIDCreated, "" + ROI, "" + totalFrames, "" + carSpeed, violationStatus });
                    listViewViolation.Items.Add(item);
                    item.ImageKey = "Vehicle_" + VehicleID_1;
                });
            } catch (Exception err) {
                MessageBox.Show("" + err.ToString(), "Error!", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // What to do when the user pressed link below the form
        private void linkLabel1_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            linkLabel1.LinkVisited = true;
            System.Diagnostics.Process.Start("mailto:a.tourani1991@gmail.com");
        }

        // Control Buttons
        private void buttonPrevious_Click(object sender, EventArgs e)
        {
            FrameCount--;
        }

        private void buttonPlay_Click(object sender, EventArgs e) {
            if (_capture != null) {
                _capture.Start();
                CapturePlayed();
            }
        }

        private void buttonPause_Click(object sender, EventArgs e) {
            if (_capture != null)
                CapturePaused();
        }

        private void buttonNext_Click(object sender, EventArgs e) {
            FrameCount++;
        }

        private void trackBarRoiWidth_Scroll(object sender, EventArgs e) {
            speedBoxW = trackBarRoiWidth.Value;
        }

        private void trackBarRoiH_Scroll(object sender, EventArgs e) {
            speedBoxH = trackBarRoiH.Value;
        }

        private void trackBarRoiX_Scroll(object sender, EventArgs e) {
            speedBoxX = trackBarRoiX.Value;
        }

        private void trackBarRoiY_Scroll(object sender, EventArgs e) {
            speedBoxY = trackBarRoiY.Value;
        }

        private void trackBarErode_Scroll(object sender, EventArgs e) {
            erodedKernelSize = trackBarErode.Value;
            labelErodeValue.Text = erodedKernelSize.ToString();
        }

        private void trackBarGaussian_Scroll(object sender, EventArgs e) {
            gaussianBlurKernel = trackBarGaussian.Value;
            labelGaussianValue.Text = Convert.ToInt32(gaussianBlurKernel).ToString();
        }

        private void checkBoxGaussianEnabled_CheckedChanged(object sender, EventArgs e)
        {
            gaussianEnabled = !gaussianEnabled;
            if (gaussianEnabled) {
                label10.ForeColor = Color.Black;
                labelGaussianValue.ForeColor = Color.Black;
                trackBarGaussian.Enabled = true;
            } else {
                label10.ForeColor = Color.Gray;
                labelGaussianValue.ForeColor = Color.Gray;
                trackBarGaussian.Enabled = false;
            }
        }

        private void checkBoxErode_CheckedChanged(object sender, EventArgs e)
        {
            erodeEnabled = !erodeEnabled;
            if (erodeEnabled) {
                label8.ForeColor = Color.Black;
                labelErodeValue.ForeColor = Color.Black;
                trackBarErode.Enabled = true;
            } else {
                label8.ForeColor = Color.Gray;
                labelErodeValue.ForeColor = Color.Gray;
                trackBarErode.Enabled = false;
            }
        }

        private void checkBoxDilate_CheckedChanged(object sender, EventArgs e)
        {
            dilateEnabled = !dilateEnabled;
            if (dilateEnabled) {
                label14.ForeColor = Color.Black;
                labelDilateValue.ForeColor = Color.Black;
                trackBarDilate.Enabled = true;
            } else {
                label14.ForeColor = Color.Gray;
                labelDilateValue.ForeColor = Color.Gray;
                trackBarDilate.Enabled = false;
            }
        }

        private void checkBoxOpening_CheckedChanged(object sender, EventArgs e)
        {
            openEnabled = !openEnabled;
            if (openEnabled) {
                label15.ForeColor = Color.Black;
                labelOpeningValue.ForeColor = Color.Black;
                trackBarOpening.Enabled = true;
            } else {
                label15.ForeColor = Color.Gray;
                labelOpeningValue.ForeColor = Color.Gray;
                trackBarOpening.Enabled = false;
            }
        }

        private void checkBoxFillHoles_CheckedChanged(object sender, EventArgs e)
        {
            FillHolesEnabled = !FillHolesEnabled;
            if (FillHolesEnabled)
            {
                label23.ForeColor = Color.Black;
                labelFillHolesValue.ForeColor = Color.Black;
                trackBarFillHoles.Enabled = true;
            }
            else
            {
                label23.ForeColor = Color.Gray;
                labelFillHolesValue.ForeColor = Color.Gray;
                trackBarFillHoles.Enabled = false;
            }
        }

        private void button_ImageSet_Click(object sender, EventArgs e)
        {
            FolderBrowserDialog d = new FolderBrowserDialog();
            if (d.ShowDialog() == DialogResult.OK)
            {
                string[] pathes = Directory.GetFiles(d.SelectedPath, "*.bmp");
                MessageBox.Show("Folder contains " + pathes.Length + " images!");
                if (pathes.Length < 1)
                    MessageBox.Show("Select a set of images first", "Error!");
                else {
                    try
                    {
                        foreach (string p in pathes)
                        {
                            pictureBox4.Image = new Bitmap(p);
                            pictureBox4.Refresh();
                        }
                    }
                    catch (Exception err)
                    {
                        MessageBox.Show("Error in reading file" + err.ToString(), "Error!");
                    }
                }
            }
        }

        private void checkBoxROIEnabled_CheckedChanged(object sender, EventArgs e)
        {
            ROIEnabled = !ROIEnabled;
            if (ROIEnabled) {
                motionThreshold = 3;
                labelROIEnabled.ForeColor = Color.Black;
            } else {
                motionThreshold = 500;
                labelROIEnabled.ForeColor = Color.Gray;
            }
        }

        private void LoadXMLButton_Click(object sender, EventArgs e)
        {
            listViewXMLLoad.Items.Clear();
            String XMLAddress = TextboxXMLLoader.Text;
            XmlDocument XMLdoc = new XmlDocument();
            String lane = "", frame_start = "", frame_end = "", speed = "";

            try {
                XMLdoc.Load(XMLAddress);
                XmlNodeList nodeList = XMLdoc.DocumentElement.SelectNodes("/GroundTruthRoot/gtruth/vehicle/radar");                

                foreach (XmlNode node in nodeList)
                {
                    speed = node.Attributes["speed"]?.InnerText;
                    frame_end = node.Attributes["frame_end"]?.InnerText;
                    frame_start = node.Attributes["frame_start"]?.InnerText;
                    lane = node.ParentNode.Attributes["lane"]?.InnerText;

                    try {
                        listViewXMLLoad.Invoke((MethodInvoker)delegate {
                            var ListViewitem = new ListViewItem(new[] { lane, frame_start, frame_end, speed });
                            listViewXMLLoad.Items.Add(ListViewitem);
                        });
                    } catch (Exception ListViewUpdateErr) {
                        MessageBox.Show("Error in updating list:" + ListViewUpdateErr.ToString(), "Error!", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
                
            } catch (Exception xmlloadexception) {
                MessageBox.Show("Error in reading file" + xmlloadexception.ToString(), "Error!", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            
        }

        private void trackBarMotionThreshold_Scroll(object sender, EventArgs e)
        {
            motionThreshold = trackBarMotionThreshold.Value;
            labelMotionThresholdValue.Text = motionThreshold.ToString();
        }

        private void trackBarViolationSet_Scroll(object sender, EventArgs e)
        {
            speedLimit = trackBarViolationSet.Value;
            labelViolationSet.Text = speedLimit.ToString();
        }

        private void بازکردنویدئوToolStripMenuItem_Click(object sender, EventArgs e)
        {
            button8_Click(sender, e);
        }

        private void LoadCalculatedXMLButton_Click(object sender, EventArgs e)
        {
            listViewCalculatedXML.Items.Clear();
            String XMLAddress = textBoxCalculatedXMLLoader.Text;
            XmlDocument XMLdoc = new XmlDocument();
            String lane = "", vehicleID = "", frame_end = "", speed = "";

            try
            {
                XMLdoc.Load(XMLAddress);
                XmlNodeList nodeList = XMLdoc.DocumentElement.SelectNodes("/VideoSpeedMeasurement/vehicle");

                foreach (XmlNode node in nodeList)
                {
                    speed = node.Attributes["speed"]?.InnerText;
                    frame_end = node.Attributes["frameEnd"]?.InnerText;
                    vehicleID = node.Attributes["vehicleID"]?.InnerText;
                    lane = node.Attributes["region"]?.InnerText;

                    try
                    {
                        listViewCalculatedXML.Invoke((MethodInvoker)delegate {
                            var ListViewitem = new ListViewItem(new[] { vehicleID, lane, frame_end, speed });
                            listViewCalculatedXML.Items.Add(ListViewitem);
                        });
                    }
                    catch (Exception ListViewUpdateErr)
                    {
                        MessageBox.Show("Error in updating the list view:" + ListViewUpdateErr.ToString(), "Error!", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }

            }
            catch (Exception xmlloadexception)
            {
                MessageBox.Show("Error in reading file" + xmlloadexception.ToString(), "Error!", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            int CalculatedFrame = 0, RealEnterFrame = 0, RealExitFrame = 0;
            double CalculatedSpeed = 0, RealSpeed = 1;
            int count = 0;
            double RealCalculatedDifference = 0, Mean = 0, MSE = 0, Variance = 0, StandardDeviation = 0;

            foreach (ListViewItem CalcXMLitems in listViewCalculatedXML.Items) {
                foreach (ListViewItem XMLLoaditems in listViewXMLLoad.Items) {
                    CalculatedFrame = Int32.Parse("" + CalcXMLitems.SubItems[2].Text);
                    RealEnterFrame = Int32.Parse("" + XMLLoaditems.SubItems[1].Text);
                    RealExitFrame = Int32.Parse("" + XMLLoaditems.SubItems[2].Text);
                    CalculatedSpeed = Double.Parse("" + CalcXMLitems.SubItems[3].Text);
                    RealSpeed = Double.Parse("" + XMLLoaditems.SubItems[3].Text);

                    if (CalcXMLitems.SubItems[1].Text == XMLLoaditems.SubItems[0].Text
                        && CalculatedFrame <= RealExitFrame && CalculatedFrame >= RealEnterFrame)
                    {
                        count++;
                        RealCalculatedDifference = RealSpeed - CalculatedSpeed;
                        Mean += RealCalculatedDifference;
                        MSE += Math.Pow(RealCalculatedDifference, 2);
                        //MessageBox.Show("VehicleID: " + CalcXMLitems.SubItems[0].Text + "\nFrame Seen: " + CalculatedFrame
                        //    + "\nFrame Entered: " + RealEnterFrame + "\nFrame Exit: " + RealExitFrame
                        //    + "\nCalculated Speed: " + CalculatedSpeed + "\nReal Speed: " + RealSpeed);
                    }
                }
            }

            Mean = Math.Round(Mean / count , 3);
            MSE = Math.Round(Math.Sqrt(MSE / count) , 3);

            // Variance Calculation
            foreach (ListViewItem CalcXMLitems in listViewCalculatedXML.Items)
            {
                foreach (ListViewItem XMLLoaditems in listViewXMLLoad.Items)
                {
                    CalculatedFrame = Int32.Parse("" + CalcXMLitems.SubItems[2].Text);
                    RealEnterFrame = Int32.Parse("" + XMLLoaditems.SubItems[1].Text);
                    RealExitFrame = Int32.Parse("" + XMLLoaditems.SubItems[2].Text);
                    CalculatedSpeed = Double.Parse("" + CalcXMLitems.SubItems[3].Text);
                    RealSpeed = Double.Parse("" + XMLLoaditems.SubItems[3].Text);

                    if (CalcXMLitems.SubItems[1].Text == XMLLoaditems.SubItems[0].Text
                        && CalculatedFrame <= RealExitFrame && CalculatedFrame >= RealEnterFrame)
                    {
                        RealCalculatedDifference = RealSpeed - CalculatedSpeed;
                        Variance += Math.Pow(RealCalculatedDifference - Mean , 2);
                    }
                }
            }

            Variance = Math.Round(Variance / count, 3);
            StandardDeviation = Math.Round(Variance, 3);

            MessageBox.Show("Average: " + Mean + "\nMean error: " + MSE + "\nVariance: " +
                Variance + "\nStandard Deviation: " + StandardDeviation);
        }

        private void trackBarFillHoles_Scroll(object sender, EventArgs e)
        {
            fillHolesKernelSize = trackBarFillHoles.Value;
            labelFillHolesValue.Text = fillHolesKernelSize.ToString();
        }

        private void checkBoxClosing_CheckedChanged(object sender, EventArgs e)
        {
            closeEnabled = !closeEnabled;
            if (closeEnabled) {
                label18.ForeColor = Color.Black;
                labelClosingValue.ForeColor = Color.Black;
                trackBarClosing.Enabled = true;
            } else {
                label18.ForeColor = Color.Gray;
                labelClosingValue.ForeColor = Color.Gray;
                trackBarClosing.Enabled = false;
            }
        }

        private void CalibrationConstantButton_Click(object sender, EventArgs e)
        {
            MappingConstant = Convert.ToDouble(CalibrationConstantTextbox.Text);
        }

        private void trackBarOpening_Scroll(object sender, EventArgs e) {
            openKernelSize = trackBarOpening.Value;
            labelOpeningValue.Text = openKernelSize.ToString();
        }

        private void trackBarClosing_Scroll(object sender, EventArgs e) {
            closeKernelSize = trackBarClosing.Value;
            labelClosingValue.Text = closeKernelSize.ToString();
        }

        private void میانبرهاToolStripMenuItem_Click(object sender, EventArgs e) {
            MessageBox.Show("Ctrl + ->\tNext Frame\n" + "Ctrl + <-\tPrevious Frame\n" + "P\tNext Play/Pause\n"
                , "Shortcuts");
        }

        private void دربارهToolStripMenuItem_Click(object sender, EventArgs e) {
            MessageBox.Show("Ali Tourani - M.Sc. software engineering - University of Guilan" + "\nStudent Number: 950122630008"
                + "\nSupervisor: Dr. Asadollah Shahbahrami" + "\nAdviser: Dr. Alireza Akoushideh"
                , "About");
        }

        // Shortcut Keys
        private void Form1_KeyDown(object sender, KeyEventArgs e) {
            if (e.KeyCode == Keys.Right && e.Control)
                buttonNext_Click(null, null);
            if (e.KeyCode == Keys.Left && e.Control)
                buttonPrevious_Click(null, null);
            if (e.KeyCode == Keys.P) {
                if (!play)
                    buttonPlay_Click(null, null);
                else 
                    buttonPause_Click(null, null);
            }
        }

        private void Button_Ref_meter_Change_Click(object sender, EventArgs e) {
            Ref_dist_m = Convert.ToDouble(textBox_Ref_meter.Text);
        }

        private void trackBar_mog_history_Scroll(object sender, EventArgs e) {
            mog_history = trackBar_mog_history.Value;
            label_mog_history.Text = mog_history.ToString();
        }

        private void trackBar_mog_nMixtures_Scroll(object sender, EventArgs e) {
            mog_nMixtures = trackBar_mog_nMixtures.Value;
            label_mog_nMixtures.Text = mog_nMixtures.ToString();
        }

        private void trackBar_mog_backgroundRatio_Scroll(object sender, EventArgs e) {
            mog_backgroundRatio = trackBar_mog_backgroundRatio.Value / 10.0;
            label_mog_backgroundRatio.Text = mog_backgroundRatio.ToString();
        }

        private void trackBar_mog_noiseSigma_Scroll(object sender, EventArgs e) {
            mog_noiseSigma = trackBar_mog_noiseSigma.Value;
            label_mog_noiseSigma_.Text = mog_noiseSigma.ToString();
        }

        private void trackBarDilate_Scroll(object sender, EventArgs e) {
            dilateKernelSize = trackBarDilate.Value;
            labelDilateValue.Text = dilateKernelSize.ToString();
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e) {

            // Stop Capture
            if (_capture != null)
                _capture.Stop();

            // Close XML File
            try
            {
                XMLFile.WriteEndDocument();
                XMLFile.Close();
            }
            catch (Exception err) {
                MessageBox.Show("Error in writing to XML: " + err.ToString());
            }
            
        }
    }
}