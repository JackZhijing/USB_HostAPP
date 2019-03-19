using Microsoft.Scripting.Hosting;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
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
using CyUSB;
using System.Runtime.InteropServices;
//using System.Drawing;

namespace StudyWPF
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            WaveformArrList.Add("Triangle");
            WaveformArrList.Add("Saw");
            WaveformArrList.Add("Sin");
            WaveformArrList.Add("DC");
            ChannelArrList.Add("Both");
            ChannelArrList.Add("CH 1");
            ChannelArrList.Add("CH 2");
            AmplitudeArrList.Add("100");
            AmplitudeArrList.Add("80");
            AmplitudeArrList.Add("50");
            AmplitudeArrList.Add("20");
            FrequenceArrList.Add("1000");
            FrequenceArrList.Add("500");
            FrequenceArrList.Add("250");
            FrequenceArrList.Add("125");
            PhaseArrList.Add("0");
            PhaseArrList.Add("90");
            PhaseArrList.Add("180");
            PhaseArrList.Add("270");

            usbDevices = new USBDeviceList(CyConst.DEVICES_CYUSB);
            usbDevices.DeviceAttached += new EventHandler(usbDevices_DeviceAtteched);
            usbDevices.DeviceRemoved += new EventHandler(usbDevices_DeviceRemoved);
            
        }
        CyUSBDevice loopDevice = null;
        USBDeviceList usbDevices = null;
        CyUSBEndPoint inEndPoint = null;
        CyUSBEndPoint outEndPoint = null;
       
        ArrayList WaveformArrList = new ArrayList();
        public int Wave = 0;
        ArrayList ChannelArrList = new ArrayList();
        public int Chan = 0;
        ArrayList AmplitudeArrList = new ArrayList();
        public int Ampl = 0;
        ArrayList FrequenceArrList = new ArrayList();
        public int Freq = 0;
        ArrayList PhaseArrList = new ArrayList();
        public int Phas = 0;
        int Volt1ExpandIndex = 3;
        string[] Volt1UnitArrStr = new string[] { "250V", "100V", "50V", "25V", "10V", "5V", "2.5V", "1V", "500mV", "250mV", "100mV", "50mV", "25mV", "10mV" };
        double[] Volt1ExpandScale = { 0.08, 0.2, 0.4, 0.8, 2, 4, 8, 20, 40, 80, 200, 400, 800, 2000 };
        int Volt2ExpandIndex = 3;
        string[] Volt2UnitArrStr = new string[] { "250V", "100V", "50V", "25V", "10V", "5V", "2.5V", "1V", "500mV", "250mV", "100mV", "50mV", "25mV", "10mV" };
        double[] Volt2ExpandScale = { 0.08, 0.2, 0.4, 0.8, 2, 4, 8, 20, 40, 80, 200, 400, 800, 2000 };
        int TimeExpandIndex = 9;
        string[] TimeUnitArrStr = new string[] { "10.0s", "4.00s", "2.00s", "1.00s", "400ms", "200ms", "100ms", "40.0ms", "20.0ms", "10.0ms", "4.00ms", "2.00ms", "1.00ms", "400us", "200us", "100us" };
        double[] TimeExpandScale = { 0.01, 0.025, 0.05, 0.1, 0.25, 0.5, 1, 2.5, 5, 10, 25, 50, 100, 250, 500, 1000 };

        string WorkPath = @"D:\Plasma Master";
        string ConfigDataFile = @"ConfigData.txt";
        string AcquiredDataFile = @"AcquiredData.txt";
        string PlotDataFile = @"PlotDataFile.txt";
        string AcquiredDataFilePath = @"D:\Plasma Master" + "\\" + @"AcquiredData.txt";
        string PlotDataFilePath = @"D:\Plasma Master" + "\\" + @"PlotDataFile.txt";
        string ConfigDataFilePath = @"D:\Plasma Master" + "\\" + @"ConfigData.txt";

        ArrayList WaveDataArrList = new ArrayList();
        int MaxDACValue = 1023;
        int MaxDepth = 20000;
        int MaxAmpl = 100;
        bool GridVisable = true;
        bool Ch1_On = true;
        bool Ch2_On = false;
        
        ArrayList RawDataArrList = new ArrayList();
        const int XFERSIZE = 3072;
        const int XFERNUM = 50;
        byte[] dataBuf = new byte[XFERNUM * XFERSIZE];
        byte[] dataBuf1 = new byte[XFERNUM * XFERSIZE];
        Thread tXfers;
        bool isRunning = false;
        Thread tRefresh;
        Thread tWriteToDisk;
        bool bWrite = false;

        ushort[] GetPlotDataArr = new UInt16[XFERNUM * XFERSIZE / 2];
        ushort[] PlotDataArr = new UInt16[XFERNUM * XFERSIZE / 2];
        ArrayList PlotArrayList = new ArrayList();

        public delegate void UpdateTextCallback(int num, int ms);

        private void LinkCheck_Click(object sender, RoutedEventArgs e)
        {
            usbDevices_DeviceAtteched(this, e);
        }

        void usbDevices_DeviceAtteched(object sender, EventArgs e)
        {
            setDevice();
        }
        void usbDevices_DeviceRemoved(object sender, EventArgs e)
        {
            setDevice();
        }
        public void setDevice()
        {
            loopDevice = usbDevices[0x1010, 0xABAB] as CyUSBDevice;
            //loopDevice.Reset(); // RESET here could improve usb stability 
            if (loopDevice != null)
            {
                textBlockLogInfo.Text = loopDevice.FriendlyName + " connected.";
                SetEndPoint(this, null);
            }
            else
            {
                textBlockLogInfo.Text = "No USB Device, Please Check";
                textBlockLogInfo.Inlines.Add("\nPower, Connection, Reset USB, ReStart Host APP, RePlug USB");
                return;
            }
        }
        private void SetEndPoint(object sender, EventArgs e)
        {
            if(loopDevice != null)
            {
                outEndPoint = loopDevice.EndPointOf(0x08) as CyBulkEndPoint;    // EP8
                inEndPoint = loopDevice.EndPointOf(0x82) as CyBulkEndPoint;     // EP2
                outEndPoint.TimeOut = 1000;
                inEndPoint.TimeOut = 1000;
                textBlockLogInfo.Inlines.Add("\nUSB set completed");
            }
        }

        private void StartUSB(object sender, EventArgs e)
        {
            if (tXfers != null)
            {
                inEndPoint.Abort();
                inEndPoint.Reset();
                outEndPoint.Abort();
                outEndPoint.Reset();
                tXfers = null;
            }
            //HardwareReset();
            HardwareConfig();
            tXfers = new Thread(new ThreadStart(Xsferloop));
            tXfers.IsBackground = true;
            tXfers.Priority = ThreadPriority.Highest;
            tXfers.Start();

            if (tWriteToDisk != null)
            {
                tWriteToDisk = null;
            }
            tWriteToDisk = new Thread(new ThreadStart(WriteToDisk));
            tWriteToDisk.IsBackground = true;
            tWriteToDisk.Priority = ThreadPriority.AboveNormal;
            tWriteToDisk.Start();
        }
        [DllImport("User32.dll", EntryPoint = "SendMessage")]
        private static extern int SendMessage(
            IntPtr hWnd, // handle to destination window
            int Msg, // message
            int wParam, // first message parameter
            int lParam // second message parameter
        );
        public void Xsferloop()
        {
            CheckPathExists(WorkPath);
            RawDataArrList.Clear();
            int dataBufIndex = 0;
            int inlen = XFERSIZE;
            int num = 0;
            inEndPoint.TimeOut = 2000;
            byte[] inData = new byte[XFERSIZE];
            bool isSuccess = true;
            bool isRuning = true;
            bool isfirstWrite = true;
            int ms = 0;
            DateTime dt = DateTime.Now;
            for (; isRuning == true; )
            {
                if (loopDevice != null)
                {
                    inlen = XFERSIZE;
                    isSuccess = inEndPoint.XferData(ref inData, ref inlen);

                    if (isSuccess)
                    {
                        //bWrite = false;
                        inData.CopyTo(dataBuf, dataBufIndex);
                        dataBufIndex = dataBufIndex + XFERSIZE;
                        if (dataBufIndex == XFERNUM * XFERSIZE)
                        {
                            //dataBuf.CopyTo(dataBuf1, 0);
                            //bWrite = true;
                            if (isfirstWrite)
                            {
                                File.WriteAllBytes(AcquiredDataFilePath, dataBuf);
                                isfirstWrite = false;
                            }
                            else
                            {
                                using (var stream = new FileStream(AcquiredDataFilePath, FileMode.Append))
                                {
                                    stream.Write(dataBuf, 0, dataBuf.Length);
                                }
                            }
                            try
                            {
                                File.WriteAllBytes(PlotDataFilePath, dataBuf);
                            }
                            catch
                            {

                            }
                            dataBufIndex = 0;
                            num++; 
                            
                            if(num % 500 == 499)
                            {
                                TimeSpan ts = DateTime.Now - dt;
                                //MessageBox.Show("TotalMilliseconds " + ts.TotalMilliseconds.ToString());
                                ms = Convert.ToInt32(ts.TotalMilliseconds);
                                textBlockLogInfo.Dispatcher.Invoke(
                                new UpdateTextCallback(this.UpdateTb),
                                new object[] { num, ms }
                                );
                            }
                        }
                    }
                    else
                    {
                        MessageBox.Show("Failure, Restart this App maybe help...");
                        //RawDataArrList.Add(inData.ToString());
                        string str1 = System.Text.Encoding.Default.GetString(inData);
                        RawDataArrList.Add(str1);
                        WriteToFile(AcquiredDataFilePath, RawDataArrList);
                    }
                }
                else
                {
                    tXfers.Abort();
                }
            }
            isRuning = false;
            return;
        }
        public void WriteToDisk()
        {
            //MessageBox.Show("Thread WriteToDisk Start...");
            bool isfirstWrite = true;
            var dataBufIndex = XFERNUM * XFERSIZE;
            for (; false; )
            {
                if (bWrite)
                {
                    if (isfirstWrite)
                    {
                        File.WriteAllBytes(AcquiredDataFilePath, dataBuf1);
                        isfirstWrite = false;
                    }
                    else
                    {
                        using (var stream = new FileStream(AcquiredDataFilePath, FileMode.Append))
                        {
                            stream.Write(dataBuf1, 0, dataBuf1.Length);
                        }
                    }
                    try
                    {
                        File.WriteAllBytes(PlotDataFilePath, dataBuf1);
                    }
                    catch
                    {

                    }
                    dataBufIndex = 0;
                    //Thread.Sleep(TimeSpan.FromSeconds(0.5));
                }
                //bWrite = false;
            }
        }

        private void CloseUSB()
        {
            //if (isRunning)
            //    StartUSB(this, null);
            
            if (tXfers != null)
            {
                inEndPoint.Abort();
                outEndPoint.Abort();
                tXfers = null;
                if (usbDevices != null)
                    usbDevices.Dispose();
            }
        }
        private void HardwareReset()
        {
            byte[] data = new byte[10];
            int outlen = 2;
            isRunning = false;
            data[0] = (byte)01;
            data[1] = (byte)0xE0;
            bool ResetSuccess;
            ResetSuccess = outEndPoint.XferData(ref data, ref outlen);
            if (ResetSuccess)
                textBlockLogInfo.Inlines.Add("\nHardware Reset Success");
            else
                textBlockLogInfo.Inlines.Add("\nHardware Reset Failure");
        }
        private void HardwareConfig()
        {
            bool ConfigSuccess = true;
            int LimitedMaxDepth = 500; //1024
            byte[] data = new byte[LimitedMaxDepth * 2];
            int counter = MaxDepth / LimitedMaxDepth;
            for (int j = 0; j < counter; j++)
            {
                int index = 0;
                for (int i = 0; i < LimitedMaxDepth; i++)
                {
                    data[index] = Convert.ToByte(Convert.ToInt32(WaveDataArrList[i + j * LimitedMaxDepth].ToString()) % 256);
                    data[index + 1] = Convert.ToByte(Convert.ToInt32(WaveDataArrList[i + j * LimitedMaxDepth].ToString()) / 256);
                    index = index + 2;
                }
                outEndPoint.TimeOut = 2000;
                index = 1000;
                ConfigSuccess = ConfigSuccess && outEndPoint.XferData(ref data, ref index);
                //MessageBox.Show("Cofig part " + j + "  " + LimitedMaxDepth + "   Index " + index);
            }
            if(ConfigSuccess)
                textBlockLogInfo.Inlines.Add("\nHardware Configuration Success");
            else
                textBlockLogInfo.Inlines.Add("\nHardware Configuration Failure");
        }

        private void UpdateTb(int num,int ms)
        {
            long length = new System.IO.FileInfo(AcquiredDataFilePath).Length;
            length = length / (1024 * 1024);
            //if (num % 500 == 18)
            textBlockLogInfo.Inlines.Add("\n" + "Successfully Get " +num+" Times " + length + " MB Data in " + ms + " ms...");
            LogInfoScroll.ScrollToVerticalOffset(textBlockLogInfo.ActualHeight);

        }
        
        private void FileOpenClick(object sender, RoutedEventArgs e)
        {
            var OpenFileDialog = new Microsoft.Win32.OpenFileDialog();
            {
                OpenFileDialog.Filter = "Plain Text Files (*.txt)|*.*";
                OpenFileDialog.InitialDirectory = @"D:\";
            };
            var result = OpenFileDialog.ShowDialog();
            if (result == true)
            {
                //this.textblock_filename.Text = OpenFileDialog.FileName;
                textBlockLogInfo.Inlines.Add("\nOpen file successful");
                
            }
        }

        private void FileQuitClick(object sender, RoutedEventArgs e)
        {
            textBlockLogInfo.Text = "To close this App please click yes ...";
            MessageBoxResult result = MessageBox.Show("Do you want Close ?", "Information", MessageBoxButton.YesNo);
            switch (result)
            {
                case MessageBoxResult.Yes:
                    {
                        CloseUSB();
                        System.Windows.Application.Current.Shutdown();
                        break;
                    }
                case MessageBoxResult.No:
                    break;
            }
        }
        
        private void FileSaveClick(object sender, RoutedEventArgs e)
        {
            DateTime dt = DateTime.Now;
            string SavePath = @"D:\Plasma Master\Save" + "\\"
                + dt.Year.ToString().PadLeft(4, '0')
                + dt.Month.ToString().PadLeft(2, '0')
                + dt.Day.ToString().PadLeft(2, '0')
                + "_"
                + dt.Hour.ToString().PadLeft(2, '0')
                + dt.Minute.ToString().PadLeft(2, '0')
                + dt.Second.ToString().PadLeft(2, '0') ;
           
            CheckPathExists(SavePath );
            string SaveAcquiredDataFilePath = SavePath + "\\" + AcquiredDataFile;
            File.Copy(AcquiredDataFilePath, SaveAcquiredDataFilePath);
            string SavePlotDataFilePath = SavePath + "\\" + PlotDataFile;
            File.Copy(PlotDataFilePath, SavePlotDataFilePath);
            string SaveConfigDataFilePath = SavePath + "\\" + ConfigDataFile;
            File.Copy(ConfigDataFilePath, SaveConfigDataFilePath);
            textBlockLogInfo.Text = "Data saved at " + SavePath;
        }

        bool bDefaultRun = true;
        private void BtnDefaultClick(object sender, RoutedEventArgs e)
        {
            if(bScan)
            {
                if (bDefaultRun)
                {
                    BtnDefault.Content = "Stop";
                    Volt1ExpandIndex = 3;
                    TimeExpandIndex = 9;
                    VerticleAdj = 0;
                    HoriztileAdj = 0;

                    textBlockLogInfo.Text = "Set all parameters with default values automatically";
                    Wave = WaveformArrList.IndexOf("Triangle");
                    CmbFreq.IsEnabled = true;
                    CmbPhase.IsEnabled = true;
                    radioButtonTriangle.IsChecked = true;
                    textBlockLogInfo.Inlines.Add("\n" + WaveformArrList[Wave].ToString() + " waveform selected");

                    Chan = ChannelArrList.IndexOf("Both");
                    CmbChan.SelectedIndex = Chan;
                    textBlockLogInfo.Inlines.Add("\nChannel " + ChannelArrList[Chan].ToString());

                    Ampl = AmplitudeArrList.IndexOf("100");
                    CmbAmp.SelectedIndex = Ampl;
                    textBlockLogInfo.Inlines.Add("\nAmplitude " + AmplitudeArrList[Ampl].ToString() + " V");

                    Freq = FrequenceArrList.IndexOf("1000");
                    CmbFreq.SelectedIndex = Freq;
                    textBlockLogInfo.Inlines.Add("\nFrequense " + FrequenceArrList[Freq].ToString() + " Hz");

                    Phas = PhaseArrList.IndexOf("0");
                    CmbPhase.SelectedIndex = Phas;
                    textBlockLogInfo.Inlines.Add("\nPhase " + PhaseArrList[Phas].ToString() + " degree");

                    ConfigDataGen(Wave, Ampl, Freq, Phas);
                    if (bTestMode)
                    {
                        AddChart();
                    }
                    else
                    {
                        usbDevices_DeviceAtteched(this, e);
                        if (loopDevice != null)
                        {
                            //FileStream s = new FileStream(PlotDataFilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);
                            StartUSB(this, e);
                            AddChart();
                        }
                    }
                }
                else
                {
                    BtnDefault.Content = "Default";
                    if (loopDevice != null)
                    {
                        if (tXfers != null)
                        {
                            if (tXfers.IsAlive)
                            {
                                tXfers.Abort();
                            }
                        }
                    }
                }
                bDefaultRun = !bDefaultRun;
            }
            else
            {
                textBlockLogInfo.Text = "On Scanning, Stop scan before default runing";
            }
        }
        
        private SelectionChangedEventArgs CmbChgeArg;
        bool bScan = true;
        private void Scan_Click(object sender, RoutedEventArgs e)
        {
            if(bDefaultRun)
            {
                if (bScan)
                {
                    BtnScan.Content = "Stop";
                    CmbAmp_SelectionChanged(CmbAmp, CmbChgeArg);
                    CmbFreq_SelectionChanged(CmbFreq, CmbChgeArg);
                    CmbPhase_SelectionChanged(CmbPhase, CmbChgeArg);
                    textBlockLogInfo.Text = "Scan with parameters in waveform selection";
                    textBlockLogInfo.Inlines.Add("\nChannel " + ChannelArrList[Chan].ToString());
                    textBlockLogInfo.Inlines.Add("\n" + WaveformArrList[Wave].ToString() + " waveform selected");
                    textBlockLogInfo.Inlines.Add("\nAmplitude " + AmplitudeArrList[Ampl].ToString() + " V");
                    if (Wave != 3)
                    {
                        textBlockLogInfo.Inlines.Add("\nFrequense " + FrequenceArrList[Freq].ToString() + " Hz");
                        textBlockLogInfo.Inlines.Add("\nPhase " + PhaseArrList[Phas].ToString() + " degree");
                    }
                    ConfigDataGen(Wave, Ampl, Freq, Phas);
                    if (bTestMode)
                    {
                        AddChart();
                    }
                    else
                    {
                        usbDevices_DeviceAtteched(this, e);
                        if (loopDevice != null)
                        {
                            StartUSB(this, e);
                            AddChart();
                        }
                    }
                }
                else
                {
                    BtnScan.Content = "Scan";
                    if (loopDevice != null)
                    {
                        if (tXfers != null)
                        {
                            if (tXfers.IsAlive)
                            {
                                tXfers.Abort();
                            }
                        }
                    }
                }
                bScan = !bScan;
            }
            else
            {
                textBlockLogInfo.Text = "Default Runing, Stop running before Scan";
            }
        }

        byte[] PlotDataBuf = new byte[XFERNUM * XFERSIZE];
        private void ReadPlotData()
        {
            try
            {
                PlotDataBuf = File.ReadAllBytes(PlotDataFilePath);
            }
            catch (Exception ex)
            {
                //MessageBox.Show("File is Writing");
            }
            //MessageBox.Show("PlotDataBuf Length" + PlotDataBuf.Length);
            //string myStr1 = "";
            int index = 0;
            for (int i = 0; (i < PlotDataBuf.Length - 2) && (i < XFERNUM * XFERSIZE -2); i = i + 2) //XFERNUM * XFERSIZE - 2
            {
                GetPlotDataArr[index] = Convert.ToUInt16(Convert.ToUInt16(PlotDataBuf[i + 1]) * 256 + Convert.ToUInt16(PlotDataBuf[i]));
                //myStr1 = myStr1 + GetPlotDataArr[index] + " ";
                index++;
            }
            GetPlotDataArr.CopyTo(PlotDataArr, 0);
            //MessageBox.Show("PlotDataArr " + myStr1);
        }

        private double xmin = 0;
        private double xmax = 10;
        private double ymin = -10;
        private double ymax = 10;
        private Polyline polyline;
        bool bTestMode = false;
        double VerticleAdj = 0;
        double HoriztileAdj = 0;
        static bool bFirst = true;

        private void AddChart()
        {
            //if (tRefresh != null)
            //{
            //    tRefresh = null;
            //}
            //tRefresh = new Thread(new ThreadStart(reFresh));
            //tRefresh.IsBackground = true;
            //tRefresh.Priority = ThreadPriority.AboveNormal;
            //tRefresh.Start();
            topCanvas.Children.Clear();
            canvas.Children.Clear();
            bottomCanvas.Children.Clear();
            topCanvas.Children.Add(Logo);
            topCanvas.Children.Add(DeviceInfo);
            topCanvas.Children.Add(Ch1TextBlock);
            topCanvas.Children.Add(Ch2TextBlock);
            bottomCanvas.Children.Add(timeUnitTextBlock);
            bottomCanvas.Children.Add(Volt1UnitTextBlock);
            bottomCanvas.Children.Add(Volt2UnitTextBlock);
            IsGridVisable(GridVisable);
            if(bFirst)
            {
                bFirst = false; // First time is initilization, so no plot data to read.
            }
            else
            {
                ReadPlotData();
            }
            int PlotLength = 0;
            PlotLength = PlotDataArr.Length - 1;
            int MaxPlotValue = 0;
            int MinPLotValue = 1024;
            foreach (var i in PlotDataArr)
            {
                if (MaxPlotValue < Convert.ToUInt16(i))
                    MaxPlotValue = Convert.ToUInt16(i);
                else if (MinPLotValue > Convert.ToUInt16(i))
                    MinPLotValue = Convert.ToUInt16(i);
            }
            if (bTestMode == true)
            {
                Array.Clear(PlotDataArr,0, PlotDataArr.Length);
                for (int i = 0; (i < WaveDataArrList.Count) && (i < PlotDataArr.Length); i++)
                {
                    PlotDataArr[i] = Convert.ToUInt16(WaveDataArrList[i]);
                    //MessageBox.Show("PlotDataArr.reFreshed..." + PlotDataArr[i]);
                }
                PlotLength = PlotDataArr.Length;
            }
            if (Ch1_On == true)
            {
                //MessageBox.Show(PlotDataArr.Count.ToString());
                polyline = new Polyline { Stroke = Brushes.Yellow };
                if (PlotLength != 0)
                {
                    double Yskew = Convert.ToDouble(AmplitudeArrList[Ampl].ToString()) / MaxAmpl * 0.5;
                    double YreScale = ymax - ymin;// * 0.8;
                    double x = 0, y = 0, XreScale = 0;
                    double Xstep = 1.0 / PlotDataArr.Length * xmax;
                   
                    for (int i = 0; i < PlotLength; i++)
                    {
                        //y = (Convert.ToDouble(PlotDataArr[i].ToString()) / MaxPlotValue - Yskew) * YreScale;
                        y = (Convert.ToDouble(PlotDataArr[i].ToString()) / MaxPlotValue) * YreScale;
                        y = y * Volt1ExpandScale[Volt1ExpandIndex] + VerticleAdj;
                        XreScale = x * TimeExpandScale[TimeExpandIndex] + HoriztileAdj;
                        if (XreScale <= 2 * xmax && XreScale >= -1*xmax) 
                            polyline.Points.Add(CorrespondingPoint(new Point(XreScale, y)));
                        x = x + Xstep;
                    }
                }
                canvas.Children.Add(polyline);
            }
            if (Ch2_On == true)
            {
                polyline = new Polyline { Stroke = Brushes.LightGreen };
                if (WaveDataArrList.Count != 0)
                {
                    for (int i = 0; i < MaxDepth; i++)
                    {
                        var x = i * 1.0 / MaxDepth * xmax;
                        var y = (Convert.ToDouble(WaveDataArrList[i].ToString()) / MaxDACValue - Convert.ToDouble(AmplitudeArrList[Ampl].ToString()) / MaxAmpl * 0.5) * (ymax - ymin);//* 0.8;
                        y = y * Volt2ExpandScale[Volt2ExpandIndex];
                        x = x * TimeExpandScale[TimeExpandIndex];
                        if (x <= xmax && x >= xmin)
                            polyline.Points.Add(CorrespondingPoint(new Point(x, y)));
                    }
                }
                canvas.Children.Add(polyline);
            }
            LogInfoScroll.ScrollToVerticalOffset(textBlockLogInfo.ActualHeight);
        }
        private Point CorrespondingPoint(Point pt)
        {
            var result = new Point
            {
                X = (pt.X - xmin) * canvas.ActualWidth / (xmax - xmin),
                Y = canvas.ActualHeight - (pt.Y - ymin) * canvas.ActualHeight
                    / (ymax - ymin)
            };
            return result;
        }
        private void IsGridVisable(bool isTrue)
        {
            if(isTrue)
            {
                polyline = new Polyline
                {
                    Stroke = Brushes.White,
                    StrokeDashArray = new DoubleCollection(new double[] { 1, 4 })
                };
                int tempForPolyLine = -1;
                for (int i = 0; i <= xmax; i++)
                {
                    var hx = i;
                    for (int j = 0; j <= 10; j++)
                    {
                        var hy = j * (ymax - ymin + 2) / 10 + ymin - 2;

                        if (i != tempForPolyLine)
                        {
                            canvas.Children.Add(polyline);
                            polyline = new Polyline
                            {
                                Stroke = Brushes.White,
                                StrokeDashArray = new DoubleCollection(new double[] { 1, 4 })
                            };
                        }
                        else
                        {
                            polyline.Points.Add(CorrespondingPoint(new Point(hx, hy)));
                        }
                        tempForPolyLine = i;
                    }
                }
                canvas.Children.Add(polyline);
                polyline = new Polyline
                {
                    Stroke = Brushes.White,
                    StrokeDashArray = new DoubleCollection(new double[] { 2, 4 })
                };
                for (int i = 0; i <= 10; i++)
                {
                    var vx = 5;
                    var vy = i * (ymax - ymin) / 10 + ymin;
                    polyline.Points.Add(CorrespondingPoint(new Point(vx, vy)));
                }
                canvas.Children.Add(polyline);

                polyline = new Polyline
                {
                    Stroke = Brushes.White,
                    StrokeDashArray = new DoubleCollection(new double[] { 1, 4 })
                };
                for (int i = -8; i <= 10; i = i + 4)
                {
                    var hy = i;
                    for (int j = -1; j <= xmax; j++)
                    {
                        var hx = j;

                        if (i != tempForPolyLine)
                        {
                            canvas.Children.Add(polyline);
                            polyline = new Polyline
                            {
                                Stroke = Brushes.White,
                                StrokeDashArray = new DoubleCollection(new double[] { 1, 4 })
                            };
                        }
                        else
                        {
                            polyline.Points.Add(CorrespondingPoint(new Point(hx, hy)));
                        }
                        tempForPolyLine = i;
                    }
                }
                canvas.Children.Add(polyline);
                polyline = new Polyline
                {
                    Stroke = Brushes.White,
                    StrokeDashArray = new DoubleCollection(new double[] { 3, 4 })
                };
                for (int i = 0; i <= 10; i++)
                {
                    var vx = i * xmax / 10;
                    var vy = 0;
                    polyline.Points.Add(CorrespondingPoint(new Point(vx, vy)));
                }
                canvas.Children.Add(polyline);
            }
        }

        private void RadioButtonTriangle_Click(object sender, RoutedEventArgs e)
        {
            RadioButton rb = sender as RadioButton;
            if (rb.IsChecked == true)
            {
                Wave = WaveformArrList.IndexOf("Triangle");
                textBlockLogInfo.Text = WaveformArrList[Wave] + " waveform selected";
                CmbFreq.IsEnabled = true;
                CmbPhase.IsEnabled = true;
            }
            else
                Wave = WaveformArrList.IndexOf("Triangle");
        }

        private void RadioButtonSaw_Click(object sender, RoutedEventArgs e)
        {
            RadioButton rb = sender as RadioButton;
            if (rb.IsChecked == true)
            {
                Wave = WaveformArrList.IndexOf("Saw");
                textBlockLogInfo.Text = WaveformArrList[Wave] + " waveform selected";
                CmbFreq.IsEnabled = true;
                CmbPhase.IsEnabled = true;
            }
            else
                Wave = WaveformArrList.IndexOf("Triangle");
        }

        private void RadioButtonSin_Click(object sender, RoutedEventArgs e)
        {
            RadioButton rb = sender as RadioButton;
            if (rb.IsChecked == true)
            {
                Wave = WaveformArrList.IndexOf("Sin");
                textBlockLogInfo.Text = WaveformArrList[Wave] + " waveform selected";
                CmbFreq.IsEnabled = true;
                CmbPhase.IsEnabled = true;
            }
            else
                Wave = WaveformArrList.IndexOf("Triangle");
        }

        private void RadioButtonDC_Click(object sender, RoutedEventArgs e)
        {
            RadioButton rb = sender as RadioButton;
            if (rb.IsChecked == true)
            {
                Wave = WaveformArrList.IndexOf("DC");
                textBlockLogInfo.Text = WaveformArrList[Wave] + " waveform selected";
                CmbFreq.IsEnabled = false;
                CmbPhase.IsEnabled = false;
            }
            else
                Wave = WaveformArrList.IndexOf("Triangle");
        }

        private void CmbChan_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Chan = CmbChan.SelectedIndex;
        }

        private void CmbAmp_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            bool exists = AmplitudeArrList.Contains(CmbAmp.Text);
            if (exists)
            {
                Ampl = AmplitudeArrList.IndexOf(CmbAmp.Text);
            }
            else
            {
                int tempValInt = 0;
                try
                {
                    tempValInt = (int)Math.Floor(Convert.ToDouble(CmbAmp.Text));
                    if ((tempValInt > 100) || (tempValInt < -100))
                    {
                        MessageBox.Show("Amplitude " + tempValInt.ToString() + " V, Out of Range, Input between -100 to 100");
                    }
                    else
                    {
                        CmbAmp.Text = tempValInt.ToString();
                        AmplitudeArrList.Add(CmbAmp.Text);
                        Ampl = AmplitudeArrList.IndexOf(CmbAmp.Text);
                    }
                }
                catch
                {
                    MessageBox.Show("Amplitude Out of Range, Input between -100 to 100");
                }
            }
        }

        private void CmbFreq_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            bool exists = FrequenceArrList.Contains(CmbFreq.Text);
            if (exists)
            {
                Freq = FrequenceArrList.IndexOf(CmbFreq.Text);
            }
            else
            {
                int tempValInt = 0;
                try
                {
                    tempValInt = Convert.ToInt16(Math.Floor(Convert.ToDouble(CmbFreq.Text.ToString())));
                    if ((tempValInt > 1000) || (tempValInt < 1))
                    {
                        MessageBox.Show("Frequence " + tempValInt.ToString() + " Hz, Out of Range, Input between 1 to 1000");
                    }
                    else
                    {
                        CmbFreq.Text = tempValInt.ToString();
                        FrequenceArrList.Add(CmbFreq.Text);
                        Freq = FrequenceArrList.IndexOf(CmbFreq.Text);
                    }
                }
                catch
                {
                    MessageBox.Show("Frequence Out of Range, Input between 1 to 1000");
                }
            }
        }
        
        private void CmbPhase_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            bool exists = PhaseArrList.Contains(CmbPhase.Text);
            if (exists)
            {
                Phas = PhaseArrList.IndexOf(CmbPhase.Text);
            }
            else
            {
                double tempValDob = 0;
                try
                {
                    tempValDob = Math.Round(Convert.ToDouble(CmbPhase.Text), 1);
                    if ((tempValDob > 360) || (tempValDob < 0))
                    {
                        MessageBox.Show("Phase " + tempValDob.ToString() + " degree, Out of Range, Input between 0 to 360");
                    }
                    else
                    {
                        CmbPhase.Text = tempValDob.ToString();
                        PhaseArrList.Add(CmbPhase.Text);
                        Phas = PhaseArrList.IndexOf(CmbPhase.Text);
                    }
                }
                catch
                {
                    MessageBox.Show("Phase Out of Range, Input between 0 to 360");
                }
            }
        }

        // Code below does not work, remeber to check it out.
        private void ScrollViewer_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            //ScrollViewer scroll = sender as ScrollViewer;
            LogInfoScroll.ScrollToBottom();
            LogInfoScroll.ScrollToEnd();
            LogInfoScroll.ScrollToVerticalOffset(textBlockLogInfo.ActualHeight);
        }

        private void ConfigDataGen (int Wave, int Ampl, int Freq, int Phas)
        {
            int Datax = 0, Datay = 0;
            int PhasShift = 0;
            int FreqRescale = 10;
            double reAmpl = Convert.ToInt32(AmplitudeArrList[Ampl].ToString()) * 1.0 / MaxAmpl;
            string filePath = ConfigDataFilePath;// WorkPath + "\\" + ConfigDataFile;
            CheckPathExists(WorkPath);
            WaveDataArrList.Clear();
            ArrayList WriteDataArrList = new ArrayList();
            switch (Wave)
            {
                case 0: //Triangle
                    {
                        int part = Convert.ToInt16(FrequenceArrList[Freq].ToString()) * 2 / FreqRescale;
                        int Depth = Convert.ToInt16(Math.Ceiling(MaxDepth * 1.0 / part));
                        double slop = MaxDACValue * 1.0 / Depth * reAmpl;
                        for (int P = 0; P < part; P++)
                        {
                            if (P%2==0)
                            {
                                for (Datax = 0; Datax < Depth; Datax++)
                                {
                                    Datay = (int)(Datax * slop);
                                    WaveDataArrList.Add(Datay.ToString());
                                }
                            }
                            else
                            {
                                for (Datax = 0; Datax < Depth; Datax++)
                                {
                                    Datay =(int) (MaxDACValue * reAmpl - (Datax * slop));
                                    WaveDataArrList.Add(Datay.ToString());
                                }
                            }
                        }
                        PhasShift = Convert.ToInt16(Convert.ToDouble(PhaseArrList[Phas].ToString()) * 1.0 / 360 * Depth * 2);
                        ArrayList PhasShiftArrList = new ArrayList(new int[MaxDepth]);
                        for (int i = 0; i < MaxDepth; i++)
                        {
                            if (i + PhasShift < MaxDepth)
                            {
                                PhasShiftArrList[i] = WaveDataArrList[i + PhasShift];
                            }
                            else
                                PhasShiftArrList[i] = WaveDataArrList[i + PhasShift - MaxDepth];
                        }
                        for (int i = 0; i < MaxDepth; i++)
                        {
                            WaveDataArrList[i] = PhasShiftArrList[i];
                            WriteDataArrList.Add(PhasShiftArrList[i]);
                        }
                        WriteToFile(filePath, WriteDataArrList);
                        break;
                    }
                case 1: //Saw
                    {
                        int part = Convert.ToInt16(FrequenceArrList[Freq].ToString()) / FreqRescale;
                        int Depth = Convert.ToInt16(Math.Ceiling( MaxDepth * 1.0 / part));
                        double slop = MaxDACValue * 1.0 / Depth * reAmpl;
                        for (int P = 0; P < part; P++)
                        {
                            for (Datax = 0; Datax < Depth; Datax++)
                            {
                                Datay = (int)(Datax * slop);
                                WaveDataArrList.Add(Datay.ToString());
                            }
                        }
                        PhasShift = Convert.ToInt16(Convert.ToDouble(PhaseArrList[Phas].ToString()) * 1.0 / 360 * Depth);
                        ArrayList PhasShiftArrList = new ArrayList(new int[MaxDepth]);
                        for (int i = 0; i < MaxDepth; i++)
                        {
                            if (i + PhasShift < MaxDepth)
                                PhasShiftArrList[i] = WaveDataArrList[i + PhasShift];
                            else
                                PhasShiftArrList[i] = WaveDataArrList[i + PhasShift - MaxDepth];
                        }
                        for (int i = 0; i < MaxDepth; i++)
                        {
                            WaveDataArrList[i] = PhasShiftArrList[i];
                            WriteDataArrList.Add(PhasShiftArrList[i]);
                        }
                        WriteToFile(filePath, WriteDataArrList);
                        break;
                    }
                case 2: //Sin
                    {
                        int part = Convert.ToInt16(FrequenceArrList[Freq].ToString()) / FreqRescale;
                        int Depth = Convert.ToInt16(Math.Ceiling(MaxDepth * 1.0 / part));
                        for (int P = 0; P < part; P++)
                        {
                            for (Datax = 0; Datax < Depth; Datax++)
                            {
                                Datay = (int)(reAmpl * MaxDACValue* (1 + Math.Sin(Math.PI * 2 * Datax / Depth)) / 2);
                                WaveDataArrList.Add(Datay.ToString());
                            }
                        }
                        PhasShift = Convert.ToInt16(Convert.ToDouble(PhaseArrList[Phas].ToString()) * 1.0 / 360 * Depth);
                        ArrayList PhasShiftArrList = new ArrayList(new int[MaxDepth]);
                        for (int i = 0; i < MaxDepth; i++)
                        {
                            if (i + PhasShift < MaxDepth)
                                PhasShiftArrList[i] = WaveDataArrList[i + PhasShift];
                            else
                                PhasShiftArrList[i] = WaveDataArrList[i + PhasShift - MaxDepth];
                        }
                        for (int i = 0; i < MaxDepth; i++)
                        {
                            WaveDataArrList[i] = PhasShiftArrList[i];
                            WriteDataArrList.Add(PhasShiftArrList[i]);
                        }
                        WriteToFile(filePath, WriteDataArrList);
                        break;
                    }
                case 3: //DC
                    {
                        for (Datax = 0; Datax < MaxDepth; Datax++)
                        {
                            Datay = (int)(reAmpl * MaxDACValue);
                            WaveDataArrList.Add(Datay.ToString());
                        }
                        WriteToFile(filePath, WaveDataArrList);
                        break;
                    }
                default:
                    {
                        int part = Convert.ToInt16(FrequenceArrList[Freq].ToString()) * 2 / FreqRescale;
                        int Depth = MaxDepth / part;
                        double slop = MaxDACValue * 1.0 / Depth * reAmpl;
                        for (int P = 0; P < part; P++)
                        {
                            if (P % 2 == 0)
                            {
                                for (Datax = 0; Datax < Depth; Datax++)
                                {
                                    Datay = (int)(Datax * slop);
                                    WaveDataArrList.Add(Datay.ToString());
                                }
                            }
                            else
                            {
                                for (Datax = 0; Datax < Depth; Datax++)
                                {
                                    Datay = (int)(MaxDACValue * reAmpl - (Datax * slop));
                                    WaveDataArrList.Add(Datay.ToString());
                                }
                            }
                        }
                        PhasShift = Convert.ToInt16(Convert.ToDouble(PhaseArrList[Phas].ToString()) * 1.0 / 360 * Depth * 2);
                        ArrayList PhasShiftArrList = new ArrayList(new int[MaxDepth]);
                        for (int i = 0; i < MaxDepth; i++)
                        {
                            if (i + PhasShift < MaxDepth)
                                PhasShiftArrList[i] = WaveDataArrList[i + PhasShift];
                            else
                                PhasShiftArrList[i] = WaveDataArrList[i + PhasShift - MaxDepth];
                        }
                        for (int i = 0; i < MaxDepth; i++)
                        {
                            WaveDataArrList[i] = PhasShiftArrList[i];
                        }
                        WriteToFile(filePath, WaveDataArrList);
                        break;
                    }
            }
        }

        private static void CheckPathExists(string path)
        {
                if (!Directory.Exists(path))
                {
                    Directory.CreateDirectory(path);
                }
        }

        private static void WriteToFile(string filePath, ArrayList arrayList)
        {
            File.WriteAllText(filePath, String.Empty);
            File.WriteAllLines(filePath, arrayList.Cast<string>());
        }

        private static void AppendToFile(string filePath, ArrayList arrayList)
        {
            File.AppendAllLines(filePath, arrayList.Cast<string>());
        }

        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            AddChart();
        }
       
        private void GridVisable_Click(object sender, RoutedEventArgs e)
        {
            GridVisable = !GridVisable;
            if (GridVisable)
            {
                GridVisual.Header = "Grid On";
                textBlockLogInfo.Text = "Grid Visable On";
            }
            else
            {
                GridVisual.Header = "Grid Off";
                textBlockLogInfo.Text = "Grid Visable Off";
            }
            AddChart();
        }

        private void Ampl1AdjImage_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (e.Delta > 0)
            {
                if (Volt1ExpandIndex == 13)
                    return;
                else
                    Volt1ExpandIndex = Volt1ExpandIndex + 1;
            }
            else if (e.Delta < 0)
            {
                if (Volt1ExpandIndex == 0)
                    return;
                else
                    Volt1ExpandIndex = Volt1ExpandIndex -1;
            }
            Volt1UnitTextBlock.Text = Volt1UnitArrStr[Volt1ExpandIndex].ToString();
            AddChart();
        }

        private void Ampl2AdjImage_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (e.Delta > 0)
            {
                if (Volt2ExpandIndex == 13)
                    return;
                else
                    Volt2ExpandIndex = Volt2ExpandIndex + 1;
            }

            else if (e.Delta < 0)
            {
                if (Volt2ExpandIndex == 0)
                    return;
                else
                    Volt2ExpandIndex = Volt2ExpandIndex - 1;
            }
            Volt2UnitTextBlock.Text = Volt2UnitArrStr[Volt2ExpandIndex].ToString();
            AddChart();
        }
        
        private void TimeAdjImage_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (e.Delta > 0)
            {
                if (TimeExpandIndex == 15)
                    return;
                else
                    TimeExpandIndex = TimeExpandIndex + 1;
            }
            else if (e.Delta < 0)
            {
                if (TimeExpandIndex == 0)
                    return;
                else
                    TimeExpandIndex = TimeExpandIndex - 1;
            }
            timeUnitTextBlock.Text = TimeUnitArrStr[TimeExpandIndex].ToString();
            AddChart();
        }

        private void Ch1Icon_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            string newUrl;
            if (Ch1_On == true)
            {
                newUrl = "Pack://Application:,,,/Resources/Ch1Off.png";
                Ch1TextBlock.Text = "CH 1 off";
            }
            else
            {
                newUrl = "Pack://Application:,,,/Resources/Ch1On.png";
                Ch1TextBlock.Text = "CH 1 on";
            }
            BitmapImage logo = new BitmapImage();
            logo.BeginInit();
            logo.UriSource = new Uri(newUrl);
            logo.EndInit();
            Ch1Icon.Source = logo;
            Ch1_On = !Ch1_On;
            AddChart();
        }

        private void Ch2Icon_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            string newUrl;
            if (Ch2_On == true)
            {
                newUrl = "Pack://Application:,,,/Resources/Ch2Off.png";
                Ch2TextBlock.Text = "CH 2 off";
            }
            else
            {
                newUrl = "Pack://Application:,,,/Resources/Ch2On.png";
                Ch2TextBlock.Text = "CH 2 on";
            }
            BitmapImage logo = new BitmapImage();
            logo.BeginInit();
            logo.UriSource = new Uri(newUrl);
            logo.EndInit();
            Ch2Icon.Source = logo;
            Ch2_On = !Ch2_On;
            AddChart();
        }

        private void About_MenuItem_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("\t       Plasma Master " +
                            "\n\tDesigned By Jian Zhijing.\t" +
                            "\n\t     USTC MPHY 420." +
                            "\n\n\t2019 All Right Reserved.");
        }

        private void TestMode_Click(object sender, RoutedEventArgs e)
        {
            bTestMode = !bTestMode;
            if(bTestMode)
            {
                TestMode.Header = "Test Mode On";
                textBlockLogInfo.Text = "TestMode Start";
            }
            else
            {
                TestMode.Header = "Test Mode Off";
                textBlockLogInfo.Text = "TestMode Closed";
            }
        }

        private void ProbeClean_Click(object sender, RoutedEventArgs e)
        {
            textBlockLogInfo.Text = "Probe Cleaned"; // Do something...
        }

        private void Volt1UpDownImage_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (e.Delta > 0)
            {
                if (VerticleAdj < 15)
                {
                    VerticleAdj += 0.3;
                }
            }
            else if (e.Delta < 0)
            {
                if (VerticleAdj > -15)
                {
                    VerticleAdj -= 0.3;
                }
            }
            AddChart();
        }

        private void TimeLeftRightImage_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (e.Delta > 0)
            {
                HoriztileAdj += 1;
            }
            else if (e.Delta < 0)
            {
                HoriztileAdj -= 1;
            }
            AddChart();
        }

        private void Volt1UpImage_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (VerticleAdj < 15) 
            {
                VerticleAdj += 1;
                AddChart();
            }
        }

        private void Volt1DownImage_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (VerticleAdj > -15) 
            {
                VerticleAdj -= 1;
                AddChart();
            }
        }

        private void TimeLeftImage_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            HoriztileAdj -= 3;
            AddChart();
        }

        private void TimeRightImage_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            HoriztileAdj += 3;
            AddChart();
        }



        ////////////////////////   Old Version Code Backup ///////////////////////////



        //string myStr = "Start";
        //for (int i = 0; i < XFERNUM * XFERSIZE; i = i + 1)
        //    myStr = myStr + dataBuf[i] + " ";
        //MessageBox.Show("myStr " + myStr);



        //DateTime dt = DateTime.Now;
        //for (.......)
        //{
        //}
        //TimeSpan ts = DateTime.Now - dt;
        //textbox1.Text = ts.TotalMilliseconds.ToString();



        //if (isfirstWrite)
        //{
        //    WriteToFile(AcquiredDataFilePath, RawDataArrList);
        //    isfirstWrite = false;
        //}
        //else
        //{
        //    AppendToFile(AcquiredDataFilePath, RawDataArrList);
        //}
        //WriteToFile(PlotDataFilePath, RawDataArrList);


        //if (isfirstWrite)
        //{
        //    File.WriteAllBytes(AcquiredDataFilePath, dataBuf);
        //    //using (StreamWriter writer = new StreamWriter(AcquiredDataFilePath, false))
        //    //{
        //    //    writer.WriteLine(dataBuf);
        //    //}
        //    isfirstWrite = false;
        //}
        //else
        //{
        //    File.WriteAllBytes(AcquiredDataFilePath, dataBuf);
        //    //using (StreamWriter writer = new StreamWriter(AcquiredDataFilePath, true))
        //    //{
        //    //    writer.WriteLine(dataBuf);
        //    //}
        //}
        ////using (StreamWriter writer = new StreamWriter(PlotDataFilePath, false))
        ////{
        //File.WriteAllBytes(PlotDataFilePath, dataBuf);
        //    //writer.WriteLine(dataBuf);
        ////}



        //private void ReadDataToArray()
        //{
        //    string[] lines = File.ReadAllLines(PlotDataFilePath, Encoding.Unicode).ToArray();
        //    PlotArrayList.Clear();
        //    int maxValue = 0;
        //    int minValue = 100;
        //    foreach (var line in lines)
        //    {
        //        char[] str = line.ToCharArray();
        //        byte[] vs = Encoding.Unicode.GetBytes(str);
        //        for (int i = 0; i < vs.Length - 2; i = i +2)
        //        {
        //            PlotArrayList.Add(Convert.ToUInt16(Convert.ToUInt16(vs[i]) * 256 + Convert.ToUInt16(vs[i+1])));
        //        }
        //        foreach (var i in PlotArrayList)
        //        {
        //            if (maxValue < Convert.ToUInt16(i))
        //                maxValue = Convert.ToUInt16(i);
        //            else if (minValue > Convert.ToUInt16(i))
        //                minValue = Convert.ToUInt16(i);
        //        }
        //    }
        //    //MessageBox.Show("maxValue " + maxValue + "\t");
        //    //MessageBox.Show("minValue " + minValue + "\t");

        //    PlotArrayList.CopyTo(PlotDataArr, 0);
        //}



        //for (int i = 0; i < XFERNUM * XFERSIZE - 2; i = i + 2) 
        //{
        //    GetPlotDataArr[index] = Convert.ToUInt16(Convert.ToUInt16(dataBuf[i + 1]) * 256 + Convert.ToUInt16(dataBuf[i]));
        //    myStr1 = myStr1 + GetPlotDataArr[index] + " ";
        //    index++;
        //}

        //GetPlotDataArr.CopyTo(PlotDataArr, 0);
        //MessageBox.Show("PlotDataArr " + myStr1);

        //ReadDataToArray();
        //
    }
}


