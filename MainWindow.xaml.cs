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
using System.Text.RegularExpressions;
using UsbPcapDotNet;
using System.Diagnostics.Tracing;
using System.IO;
using Nefarius.ViGEm.Client;
using Nefarius.ViGEm.Client.Exceptions;
using Nefarius.ViGEm.Client.Targets;
using Nefarius.ViGEm.Client.Targets.Xbox360;

namespace XFuze
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        private bool bBluetoothFound = false;
        private bool bStarted = false;
        private USBPcapClient usbPcapClient;
        private readonly IXbox360Controller controller;

        public MainWindow()
        {
            InitializeComponent();
            controller = CreateViGEmController();
        }

        private IXbox360Controller CreateViGEmController()
        {
            try
            {
                var client = new ViGEmClient();
                var controller = client.CreateXbox360Controller();
                controller.AutoSubmitReport = false;
                return controller;
            }
            catch (VigemBusNotFoundException)
            {
            }
            catch (DllNotFoundException)
            {
            }
            return null;
        }

        private void MainWindowLoaded(object sender, RoutedEventArgs e)
        {
            if(!USBPcapClient.is_usbpcap_upper_filter_installed())
            {
                MessageBox.Show("未安装USBPcap驱动，请先安装USBPcap驱动！", "提示");
                return;
            }

            var interfaces = USBPcapClient.find_usbpcap_filters();
            foreach (var filter in interfaces)
            {
                Interfaces.Items.Add(filter);
            }

            Interfaces.SelectionChanged += (object sender, SelectionChangedEventArgs e) => {
                //MessageBox.Show(e.AddedItems[0].ToString(), "消息");
                var devices = USBPcapClient.enumerate_print_usbpcap_interactive(e.AddedItems[0].ToString());

                var devTree = devices.Split("\n");
                var devPattern = "\\[Port \\d+\\]\\s+\\(device id: (\\d+)\\) (.*)";
                var subPattern = "(\\s{6,})(.*)";
                var bthPattern = "Bluetooth|蓝牙";
                List<TreeViewItem> itemLevel = new List<TreeViewItem>();
                Devices.Items.Clear();
                foreach (var device in devTree)
                {
                    if (device.Contains("\\??\\")) continue;
                    var match = Regex.Match(device, devPattern);
                    if(match.Success)
                    {
                        TreeViewItem item = new TreeViewItem();
                        item.Header = match.Groups[2];
                        item.DataContext = match.Groups[1];
                        Devices.Items.Add(item);
                        itemLevel.Clear();
                        itemLevel.Add(item);
                        item.Selected += (object sender, RoutedEventArgs e)=> { DeviceId.Text = item.DataContext.ToString(); };
                        if (Regex.Match(device, bthPattern).Success)
                        { 
                            bBluetoothFound = true;
                            DeviceId.Text = item.DataContext.ToString();
                        }
                    }
                    
                    match = Regex.Match(device, subPattern);
                    if (match.Success && itemLevel.Count > 0)
                    {
                        TreeViewItem itemChild = new TreeViewItem();
                        itemChild.Header = match.Groups[2];
                        int level = match.Groups[1].Length / 2 - 2;
                        itemLevel.RemoveRange(level, itemLevel.Count - level);
                        itemLevel.Last().Items.Add(itemChild);
                        itemLevel.Add(itemChild);
                    }
                }
                //MessageBox.Show(devices, "消息");
                //Devices.Items.Clear();
                //foreach (var device in devices)
                //{
                //    Devices.Items.Add(device);
                //}
            };

            bBluetoothFound = false;
            for(int i = 0; i < interfaces.Count; i++) {
                Interfaces.SelectedIndex = i;
                if (bBluetoothFound) break;
            }

            if(controller == null)
                MessageBox.Show("未安装ViGEmBus驱动，请先安装ViGEmBus驱动！", "提示");
        }

        private StreamWriter writer;

        private void StartCapture(object sender, RoutedEventArgs e)
        {
            if (!bStarted)
            {
                writer = new StreamWriter(new FileStream("XFuze_usbpcap.log", FileMode.Append, FileAccess.Write, FileShare.Read, 8096, FileOptions.Asynchronous | FileOptions.WriteThrough), Encoding.UTF8);
                writer.WriteAsync("开始侦听-------\n");
                bStarted = true;
                var filter = Interfaces.SelectedItem.ToString();
                var deviceid = int.Parse(DeviceId.Text);
                usbPcapClient = new USBPcapClient(filter, deviceid);
                usbPcapClient.DataRead += (sender, e) =>
                {
                    //var text = $"DATA READ Device:'{e.Header.device}'" + (e.Header.In ? " [IN] " : " [OUT] ") + $"func:'{e.Header.function}' " + $"len: {e.Data.Length} \n";
                    //if(e.Data.Length > 0) text += ToHexString(e.Data, " ") + "\n";
                    ////writer.WriteAsync(text);
                    //Dispatcher.InvokeAsync(new Action(() => writer.WriteAsync(text)), System.Windows.Threading.DispatcherPriority.Background);
                    //Dispatcher.InvokeAsync(new Action(() => Data.Text = text), System.Windows.Threading.DispatcherPriority.Render);
                    if(e.Header.In && e.Data.Length > 0)
                    {
                        var data = ToHexString(e.Data, " ");
                        Dispatcher.InvokeAsync(new Action(() => UpdateController(e.Data)), System.Windows.Threading.DispatcherPriority.Input);
                        Dispatcher.InvokeAsync(new Action(() => Data.Text = data), System.Windows.Threading.DispatcherPriority.Render);
                        Dispatcher.InvokeAsync(new Action(() => writer.WriteAsync(data + "\n\n")), System.Windows.Threading.DispatcherPriority.Background);
                    }
                };
                controller.Connect();
                usbPcapClient.start_capture();
                StartButton.Content = "停止";
            }
            else
            {
                controller.Disconnect();
                usbPcapClient.Dispose();
                writer.WriteAsync("结束侦听-------\n");
                writer?.Dispose();
                bStarted = false;
                StartButton.Content = "开始";
            }
        }

        void UpdateController(byte[] data)
        {
            if (data.Length != 20) return;
            byte[] input = new byte[10];
            Buffer.BlockCopy(data, 9, input, 0, input.Length);
            controller.SetButtonState(Xbox360Button.A,              (input[1] & (0x01)) > 0);
            controller.SetButtonState(Xbox360Button.B,              (input[1] & (0x02)) > 0);
            controller.SetButtonState(Xbox360Button.X,              (input[1] & (0x08)) > 0);
            controller.SetButtonState(Xbox360Button.Y,              (input[1] & (0x10)) > 0);
            controller.SetButtonState(Xbox360Button.LeftShoulder,   (input[1] & (0x40)) > 0);
            controller.SetButtonState(Xbox360Button.RightShoulder,  (input[1] & (0x80)) > 0);
            controller.SetButtonState(Xbox360Button.Back,           (input[2] & (0x08)) > 0);
            controller.SetButtonState(Xbox360Button.Start,          (input[2] & (0x04)) > 0);
            controller.SetButtonState(Xbox360Button.Guide,          (input[2] & (0x11)) == 1);
            controller.SetButtonState(Xbox360Button.LeftThumb,      (input[2] & (0x20)) > 0);
            controller.SetButtonState(Xbox360Button.RightThumb,     (input[2] & (0x40)) > 0);
            controller.SetButtonState(Xbox360Button.Up,             (input[3] == 0 || input[3] == 1 || input[3] == 7));
            controller.SetButtonState(Xbox360Button.Down,           (input[3] == 3 || input[3] == 4 || input[3] == 5));
            controller.SetButtonState(Xbox360Button.Left,           (input[3] == 5 || input[3] == 6 || input[3] == 7));
            controller.SetButtonState(Xbox360Button.Right,          (input[3] == 1 || input[3] == 2 || input[3] == 3));

            controller.SetSliderValue(Xbox360Slider.LeftTrigger,    input[8]);
            controller.SetSliderValue(Xbox360Slider.RightTrigger,   input[9]);

            controller.SetAxisValue(Xbox360Axis.LeftThumbX,         (short)((input[4] << 8) - 0x8000));
            controller.SetAxisValue(Xbox360Axis.LeftThumbY,         (short)(0x8000 - (input[5] << 8)));
            controller.SetAxisValue(Xbox360Axis.RightThumbX,        (short)((input[6] << 8) - 0x8000));
            controller.SetAxisValue(Xbox360Axis.RightThumbY,        (short)((input[7] << 8) - 0x8000));
            controller.SubmitReport();
        }

        public static string ToHexString(IEnumerable<byte> bytes, string deliminator = "")
        {
            var str = bytes.Aggregate(string.Empty, (current, b) => $"{current}{b:x2}{deliminator}")[..^deliminator.Length];
            return Regex.Replace(str, "(.{23}) (.{0,23}) ?", "$1  $2\n").ToUpper();
        }
    }
}
