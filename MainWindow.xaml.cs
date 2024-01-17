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

        public MainWindow()
        {
            InitializeComponent();
        }

        private void MainWindowLoaded(object sender, RoutedEventArgs e)
        {
            if(!USBPcapClient.is_usbpcap_upper_filter_installed())
            {
                MessageBox.Show("未安装USBPcap，请先安装USBPcap！", "提示");
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
        }

        private StreamWriter writer;

        private void StartCapture(object sender, RoutedEventArgs e)
        {
            if (!bStarted)
            {
                writer = new StreamWriter(new FileStream("XFuze_usbpcap.log", FileMode.Append, FileAccess.Write, FileShare.Read), Encoding.UTF8);
                writer.WriteAsync("开始侦听-------\n");
                bStarted = true;
                var filter = Interfaces.SelectedItem.ToString();
                var deviceid = int.Parse(DeviceId.Text);
                usbPcapClient = new USBPcapClient(filter, deviceid);
                usbPcapClient.DataRead += (sender, e) =>
                {
                    var text = $"DATA READ Device:'{e.Header.device}'" + $" in?:{e.Header.In} " + $"func:'{e.Header.function}' " + $"len: {e.Data.Length} \n";
                    if(e.Data.Length > 0) text += ToHexString(e.Data, " ") + "\n";
                    writer.WriteAsync(text);
                    Dispatcher.InvokeAsync(new Action(() => Data.Text = text), System.Windows.Threading.DispatcherPriority.Render);
                };
                usbPcapClient.start_capture();
                StartButton.Content = "停止";
            }
            else
            {
                usbPcapClient.Dispose();
                writer.WriteAsync("结束侦听-------\n");
                writer?.Dispose();
                bStarted = false;
                StartButton.Content = "开始";
            }
        }

        public static string ToHexString(IEnumerable<byte> bytes, string deliminator = "")
        {
            var str = bytes.Aggregate(string.Empty, (current, b) => $"{current}{b:x2}{deliminator}")[..^deliminator.Length];
            return Regex.Replace(str, "(.{23}) (.{23}) ?", "$1  $2\n").ToUpper();
        }
    }
}
