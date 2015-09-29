using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using ArDrone2.Interaction;
using WpfApplication1.Model;

namespace WpfApplication1
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private PXCMSession se;
        private PXCMSpeechRecognition sr;
        private PXCMAudioSource audiosource;
        private bool isRecognizing = false;
        private PXCMSpeechRecognition.Handler RecognitionHandler;

        #region Hand
        // Variabili per Gesture/Hands
        private PXCMSenseManager senseManager;
        private PXCMHandModule hand;
        private PXCMHandData handData;
        private PXCMHandData.GestureData gestureData;
        private PXCMHandData.IHand ihand;
        private PXCMHandData.JointData[][] nodes;
        private PXCMHandConfiguration handConfig;
        private Thread processingThread;

        private float[] xValues;
        private float[] yValues;
        private float[] zValues;
        private const int arraySize = 30;
        private Gesture gesture;
        private Int32 nhands;
        private Int32 handId;
        private float handTipX;
        private float handTipY;
        private float handTipZ;
        private int xyzValueIndex;
        Point currentPoint;
        Brush ColorBrush;
        private string detectionAlert;
        private string calibrationAlert;
        private string bordersAlert;
        private bool detectionStatusOk;
        private bool calibrationStatusOk;
        private bool borderStatusOk;
        #endregion Hand


        private enum Gesture
        {
            Undefined,
            FingerSpread,
            Pinch,
            Wave
        };


        public ObservableCollection<GenericItem> genericItems { get; set; }
        public MainWindow()
        {
            InitializeComponent();

            #region Hand
            nodes = new PXCMHandData.JointData[][] { new PXCMHandData.JointData[0x20], new PXCMHandData.JointData[0x20] };
            xValues = new float[arraySize];
            yValues = new float[arraySize];
            zValues = new float[arraySize];
            #endregion Hand

            // Setto la modalita' test per la guida del drone ON/OFF
            TestModeCheck.IsChecked = true;

            genericItems = new ObservableCollection<GenericItem>();

            se = PXCMSession.CreateInstance();

            if (se != null)
            {
                //processingThread = new Thread(new ThreadStart(ProcessingHandThread));
                //senseManager = PXCMSenseManager.CreateInstance();
                //senseManager.EnableHand();
                //senseManager.Init();
                //ConfigureHandModule();
                //processingThread.Start();



                // session is a PXCMSession instance.
                audiosource = se.CreateAudioSource();
                // Scan and Enumerate audio devices
                audiosource.ScanDevices();

                PXCMAudioSource.DeviceInfo dinfo = null;

                for (int d = audiosource.QueryDeviceNum() - 1; d >= 0; d--)
                {
                    audiosource.QueryDeviceInfo(d, out dinfo);
                }
                audiosource.SetDevice(dinfo);

                se.CreateImpl<PXCMSpeechRecognition>(out sr);
              

                PXCMSpeechRecognition.ProfileInfo pinfo;
                sr.QueryProfile(0, out pinfo);
                sr.SetProfile(pinfo);

                // sr is a PXCMSpeechRecognition instance.
                String[] cmds = new String[] { "Takeoff", "Land", "Rotate Left", "Rotate Right", "Advance",
                    "Back", "Up", "Down", "Left", "Right", "Stop" , "Dance"};
                int[] labels = new int[] { 1, 2, 4, 5, 8, 16, 32, 64, 128, 256, 512, 1024 };
                // Build the grammar.
                sr.BuildGrammarFromStringList(1, cmds, labels);
                // Set the active grammar.
                sr.SetGrammar(1);
                // Set handler

                RecognitionHandler = new PXCMSpeechRecognition.Handler();

                RecognitionHandler.onRecognition = OnRecognition;

                Legenda.Items.Add("------ Available Commands ------");
                foreach (var cmd in cmds)
                {
                    Legenda.Items.Add(cmd);
                }
            }
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            DroneControl.StubMode = false;
            await DroneControl.ConnectAsync();
        }

        private void ConfigureHandModule()
        {
            hand = senseManager.QueryHand();
                      handConfig = hand.CreateActiveConfiguration();
            //          handConfig.EnableGesture("spreadfingers");
            //        handConfig.EnableGesture("two_fingers_pinch_open");
            //      handConfig.EnableGesture("wave");
                handConfig.EnableAllAlerts();
                handConfig.ApplyChanges();
        }

        private void ProcessingHandThread()
        {
            // Start AcquireFrame/ReleaseFrame loop
            while (senseManager.AcquireFrame(true) >= pxcmStatus.PXCM_STATUS_NO_ERROR)
            {
                hand = senseManager.QueryHand();

                if (hand != null)
                {

                    // Retrieve the most recent processed data
                    handData = hand.CreateOutput();
                    handData.Update();

                    // Get number of tracked hands
                    nhands = handData.QueryNumberOfHands();

                    if (nhands > 0)
                    {
                        // Retrieve hand identifier
                        handData.QueryHandId(PXCMHandData.AccessOrderType.ACCESS_ORDER_BY_TIME, 0, out handId);

                        // Retrieve hand data
                        handData.QueryHandDataById(handId, out ihand);

                        // Retrieve all hand joint data
                        for (int i = 0; i < nhands; i++)
                        {
                            for (int j = 0; j < 0x20; j++)
                            {
                                PXCMHandData.JointData jointData;
                                ihand.QueryTrackedJoint((PXCMHandData.JointType)j, out jointData);
                                nodes[i][j] = jointData;
                            }
                        }

                        // Get world coordinates for tip of middle finger on the first hand in camera range
                        handTipX = nodes[0][Convert.ToInt32(PXCMHandData.JointType.JOINT_MIDDLE_TIP)].positionWorld.x;
                        handTipY = nodes[0][Convert.ToInt32(PXCMHandData.JointType.JOINT_MIDDLE_TIP)].positionWorld.y;
                        handTipZ = nodes[0][Convert.ToInt32(PXCMHandData.JointType.JOINT_MIDDLE_TIP)].positionWorld.z;

                        // Retrieve gesture data
                        if (handData.IsGestureFired("spreadfingers", out gestureData))
                        {
                            gesture = Gesture.FingerSpread;
                        }
                        else if (handData.IsGestureFired("two_fingers_pinch_open", out gestureData))
                        {
                            gesture = Gesture.Pinch;
                        }
                        else if (handData.IsGestureFired("wave", out gestureData))
                        {
                            gesture = Gesture.Wave;
                        }
                    }
                    else
                    {
                        gesture = Gesture.Undefined;
                    }

                    // Get alert status
                    for (int i = 0; i < handData.QueryFiredAlertsNumber(); i++)
                    {
                        PXCMHandData.AlertData alertData;
                        if (handData.QueryFiredAlertData(i, out alertData) != pxcmStatus.PXCM_STATUS_NO_ERROR) { continue; }

                        //Displaying last alert
                        switch (alertData.label)
                        {
                            case PXCMHandData.AlertType.ALERT_HAND_DETECTED:
                                detectionAlert = "Hand Detected";
                                detectionStatusOk = true;
                                break;
                            case PXCMHandData.AlertType.ALERT_HAND_NOT_DETECTED:
                                detectionAlert = "Hand Not Detected";
                                detectionStatusOk = false;
                                break;
                            case PXCMHandData.AlertType.ALERT_HAND_CALIBRATED:
                                calibrationAlert = "Hand Calibrated";
                                calibrationStatusOk = true;
                                break;
                            case PXCMHandData.AlertType.ALERT_HAND_NOT_CALIBRATED:
                                calibrationAlert = "Hand Not Calibrated";
                                calibrationStatusOk = false;
                                break;
                            case PXCMHandData.AlertType.ALERT_HAND_INSIDE_BORDERS:
                                bordersAlert = "Hand Inside Borders";
                                borderStatusOk = true;
                                break;
                            case PXCMHandData.AlertType.ALERT_HAND_OUT_OF_BORDERS:
                                bordersAlert = "Hand Out Of Borders";
                                borderStatusOk = false;
                                break;
                        }
                    }

                    UpdateUI();
                    if (handData != null) handData.Dispose();
                }
                senseManager.ReleaseFrame();
            }
        }

        private void UpdateUI()
        {
            this.Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.Normal, new Action(delegate ()
            {
                // Show x-y hand coordinates
                lblCoordinates.Content = string.Format("X: {0},   Y: {1},   Z: {2}", handTipX, handTipY, handTipZ);
                lblHandCenter.Content = string.Format("X: {0},   Y: {1},   Z: {2}", handTipX * 100 + 50, handTipY * 100 + 50, handTipZ * 100 + 50);
                //           lblHandCenter.Content= string.Format("X: {0},   Y: {1},   Z: {2}", HandCenterX, HandCenterY,HandCenterZ);

                // Do moving average filtering
                if (xyzValueIndex < arraySize)
                {
                    xValues[xyzValueIndex] = handTipX;
                    yValues[xyzValueIndex] = handTipY;
                    zValues[xyzValueIndex] = handTipZ;
                    xyzValueIndex++;
                }
                else
                {
                    for (int i = 0; i < arraySize - 1; i++)
                    {
                        xValues[i] = xValues[i + 1];
                        yValues[i] = yValues[i + 1];
                        zValues[i] = zValues[i + 1];
                    }

                    xValues[arraySize - 1] = handTipX;
                    yValues[arraySize - 1] = handTipY;
                    zValues[arraySize - 1] = handTipZ;
                }


                /*
                // Update Z axis ellipse
                float scaledZ = zValues.Average() * -100 + 50;

                if (scaledZ > MaxPointSize)
                {
                    ellDrawingPoint.Height = MaxPointSize;
                    ellDrawingPoint.Width = MaxPointSize;
                }
                else if (scaledZ < 0)
                {
                    ellDrawingPoint.Height = 0;
                    ellDrawingPoint.Width = 0;
                }
                else
                {
                    ellDrawingPoint.Height = scaledZ;
                    ellDrawingPoint.Width = scaledZ;
                }
                
                // Update drawing canvas
                Canvas.SetRight(ellDrawingPoint, (xValues.Average() * 3000 + 300) - ellDrawingPoint.Width / 2);
                Canvas.SetTop(ellDrawingPoint, (yValues.Average() * -3000 + 300) - ellDrawingPoint.Height / 2);

                // Draw line
                if ((gesture == Gesture.Pinch) && (handTipX >= -0.12))
                {
                    ellDrawingPoint.Stroke = ColorBrush;
                    ellDrawingPoint.Fill = ColorBrush;
                    Line line = new Line();
                    line.Stroke = ColorBrush;
                    line.StrokeThickness = scaledZ;
                    line.StrokeDashCap = PenLineCap.Round;
                    line.StrokeStartLineCap = PenLineCap.Round;
                    line.StrokeEndLineCap = PenLineCap.Round;
                    line.X1 = currentPoint.X;
                    line.Y1 = currentPoint.Y;
                    line.X2 = xValues.Average() * -3000 + 300;
                    line.Y2 = yValues.Average() * -3000 + 300;

                    currentPoint.X = xValues.Average() * -3000 + 300;
                    currentPoint.Y = yValues.Average() * -3000 + 300;
                    drawingCanvas.Children.Add(line);
                
               }
            
                else
                {
                    ellDrawingPoint.Stroke = ColorBrush;
                    ellDrawingPoint.Fill = Brushes.Transparent;
                    currentPoint.X = xValues.Average() * -3000 + 300;
                    currentPoint.Y = yValues.Average() * -3000 + 300;
                }

                // Erase canvas on hand wave
                if (gesture == Gesture.Wave)
                {
                    drawingCanvas.Children.Clear();
                    drawingCanvas.Children.Add(ellDrawingPoint);
                }

    

                // Handle gesture-based color menu selections
                if (handTipX < -0.12)
                {
                    if (gesture == Gesture.FingerSpread)
                    {
                        if ((handTipY <= 0.1) && (handTipY > 0.075))
                        {
                            ChangePenColor(PenColor.Red);
                        }
                        else if ((handTipY <= 0.075) && (handTipY > 0.05))
                        {
                            ChangePenColor(PenColor.Yellow);
                        }
                        else if ((handTipY <= 0.05) && (handTipY > 0.025))
                        {
                            ChangePenColor(PenColor.Green);
                        }
                        else if ((handTipY <= 0.025) && (handTipY > 0.0))
                        {
                            ChangePenColor(PenColor.Blue);
                        }
                        else if ((handTipY <= 0.0) && (handTipY > -0.025))
                        {
                            ChangePenColor(PenColor.Violet);
                        }
                        else if ((handTipY <= -0.025) && (handTipY > -0.05))
                        {
                            ChangePenColor(PenColor.Orange);
                        }
                        else if ((handTipY <= -0.05) && (handTipY > -0.075))
                        {
                            ChangePenColor(PenColor.Brown);
                        }
                        else if ((handTipY <= -0.075) && (handTipY > -0.1))
                        {
                            ChangePenColor(PenColor.Black);
                        }
                    }
                }

                // Update gesture info
                switch (gesture)
                {
                    case Gesture.Undefined:
                        lblDraw.Foreground = Brushes.White;
                        lblErase.Foreground = Brushes.White;
                        lblHover.Foreground = Brushes.White;
                        break;
                    case Gesture.FingerSpread:
                        lblDraw.Foreground = Brushes.White;
                        lblErase.Foreground = Brushes.White;
                        lblHover.Foreground = Brushes.LightGreen;
                        break;
                    case Gesture.Pinch:
                        lblDraw.Foreground = Brushes.LightGreen;
                        lblErase.Foreground = Brushes.White;
                        lblHover.Foreground = Brushes.White;
                        break;
                    case Gesture.Wave:
                        lblDraw.Foreground = Brushes.White;
                        lblErase.Foreground = Brushes.LightGreen;
                        lblHover.Foreground = Brushes.White;
                        break;
                }
*/


                // Update alert info
                lblDetectionAlert.Foreground = (detectionStatusOk) ? System.Windows.Media.Brushes.LightGreen : System.Windows.Media.Brushes.Red;
                lblDetectionAlert.Content = detectionAlert;

                lblCalibrationAlert.Foreground = (calibrationStatusOk) ? System.Windows.Media.Brushes.LightGreen : System.Windows.Media.Brushes.Red;
                lblCalibrationAlert.Content = calibrationAlert;

                lblBordersAlert.Foreground = (borderStatusOk) ? System.Windows.Media.Brushes.LightGreen : System.Windows.Media.Brushes.Red;
                lblBordersAlert.Content = bordersAlert;
            }));
        }


        public void OnRecognition(PXCMSpeechRecognition.RecognitionData data)
        {
            var pippo = data.scores[0].label;
            double spostamento = 0.3;
            TimeSpan durata = new TimeSpan(0, 0, 0, 500);
            switch (pippo)
            {
                case 1:
                    DroneState.TakeOff();
                    ScriviInLista("Takeoff");
                    break;
                case 2:
                    DroneState.Land();
                    ScriviInLista("Land");
                    break;
                case 4:
                    DroneState.RotateLeftForAsync(spostamento, durata);
                    //DroneState.RollX = .5;
                    //Thread.Sleep(1000);
                    //DroneState.RollX = 0;
                    ScriviInLista("Rotate Left");
                    break;
                case 5:
                    DroneState.RotateRightForAsync(spostamento, durata);
                    //DroneState.RollX = .5;
                    //Thread.Sleep(1000);
                    //DroneState.RollX = 0;
                    ScriviInLista("Rotate Right");
                    break;
                case 8:
                    DroneState.GoForward(spostamento);
                    Thread.Sleep(500);
                    DroneState.Stop();
                    ScriviInLista("Advance");
                    break;
                case 16:
                    DroneState.GoBackward(spostamento);
                    Thread.Sleep(500);
                    DroneState.Stop();
                    ScriviInLista("Back");
                    break;
                case 32:
                    DroneState.GoUp(spostamento);
                    Thread.Sleep(500);
                    DroneState.Stop();
                    ScriviInLista("Up");
                    break;
                case 64:
                    DroneState.GoDown(spostamento);
                    Thread.Sleep(500);
                    DroneState.Stop();
                    ScriviInLista("Down");
                    break;
                case 128:
                    DroneState.StrafeX = .5;
                    Thread.Sleep(500);
                    DroneState.StrafeX = 0;
                    ScriviInLista("Left");
                    break;
                case 256:
                    DroneState.StrafeX = -.5;
                    Thread.Sleep(500);
                    DroneState.StrafeX = 0;
                    ScriviInLista("Right");
                    break;
                case 512:
                    DroneState.Stop();
                    ScriviInLista("Stop");
                    break;
                case 1024:
                    ScriviInLista("Dance");
                    DroneState.RotateLeft(spostamento);
                    Thread.Sleep(500);
                    DroneState.RotateRight(spostamento);
                    Thread.Sleep(500);
                    DroneState.RotateRight(spostamento);
                    Thread.Sleep(500);
                    DroneState.RotateLeft(spostamento);
                    Thread.Sleep(500);
                    DroneState.GoForward(spostamento);
                    Thread.Sleep(500);
                    DroneState.GoBackward(spostamento);
                    Thread.Sleep(500);
                    DroneState.Stop();
                    break;
                default:
                    break;

            }
            Debug.WriteLine(data.grammar.ToString());
            Debug.WriteLine(data.scores[0].label.ToString());
            Debug.WriteLine(data.scores[0].sentence);
            // Process Recognition Data
        }

        void ScriviInLista(string parametro)
        {
            var tmp = DateTime.Now.Year.ToString() + ":" + DateTime.Now.Month.ToString() + ":" +
                      DateTime.Now.Day.ToString() + ":" +
                      DateTime.Now.Hour.ToString() + ":" + DateTime.Now.Minute.ToString() + ":" +
                      DateTime.Now.Second.ToString() + " - " + parametro;
            Dispatcher.Invoke(() =>
            Listacomandi.Items.Insert(0, tmp));
        }

        protected override void OnActivated(EventArgs e)
        {
            base.OnActivated(e);
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            // Clean up
            if (sr != null)
            {
                sr.Dispose();
                sr = null;
            }
            if (audiosource != null)
            {
                audiosource.Dispose();
                audiosource = null;
            }

            base.OnClosing(e);
        }

        private void StartStopButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (isRecognizing)
            {
                TestModeCheck.IsEnabled = true;
                StartStopButton.Content = "Start";
                sr.StopRec();
                MsgTextBlock01.Text = "Click on START to begin recognition";
            }
            else
            {
                StartStopButton.Content = "Stop";
                sr.StartRec(null, RecognitionHandler);
                MsgTextBlock01.Text = "Click on Stop to end recognition";
                TestModeCheck.IsEnabled = false;
            }
            isRecognizing = !isRecognizing;
        }

        private void TestModeCheck_OnClick(object sender, RoutedEventArgs e)
        {
            if ((bool)TestModeCheck.IsChecked)
            {
                DroneControl.StubMode = true;
                TestModeCheck.Content = "Test Mode: Stub";
            }
            else
            {
                DroneControl.StubMode = false;
                TestModeCheck.Content = "Test Mode: Real Drone";
            }
        }

        private void BtnEmergLand_OnClick(object sender, RoutedEventArgs e)
        {
            DroneState.Land();
            ScriviInLista("Land");
        }
    }
}
