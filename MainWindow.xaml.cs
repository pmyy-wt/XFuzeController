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
using System.Diagnostics;
using System.ComponentModel;
using System.Windows.Threading;

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
        private int deviceId = 0;
        private ViGEmClient client;
        private List<XFuzeController> controllers = new List<XFuzeController>();
        private readonly List<DispatcherTimer> timers = new List<DispatcherTimer>() { new DispatcherTimer(), new DispatcherTimer(), new DispatcherTimer(), new DispatcherTimer(), };
        private List<Grid> CheckboxBg = new List<Grid>();

        public MainWindow()
        {
            InitializeComponent();

            taskbarIcon.Icon = System.Drawing.Icon.ExtractAssociatedIcon(System.Windows.Forms.Application.ExecutablePath);
            client = CreateViGEmClient();
        }

        private ViGEmClient CreateViGEmClient()
        {
            try
            {
                return new ViGEmClient();
            }
            catch (VigemBusNotFoundException)
            {
                MessageBox.Show("未安装ViGEmBus驱动，请先安装ViGEmBus驱动！", "提示");
            }
            catch (DllNotFoundException)
            {
                MessageBox.Show("ViGEmClient DLL 异常！", "提示");
            }
            return null;
        }

        private void MainWindowLoaded(object sender, RoutedEventArgs e)
        {
            //Connection.Content = $"0x{0x0:x4}";
            //CidChannel.Content = $"0x{0x0:x4}";
            if(client != null)
            {
                CheckboxBg.AddRange(new List<Grid>() { cb1_bg, cb2_bg, cb3_bg, cb4_bg });
                List<CheckBox> checkBoxes = (new List<CheckBox>() { cb1, cb2, cb3, cb4});
                for (int i = 0; i < 4; ++i) 
                {
                    int j = i;
                    var controller = new XFuzeController(client);
                    controllers.Add(controller);
                    checkBoxes[i].Checked += (sender, e)=>controller.Enable = true;
                    checkBoxes[i].Unchecked += (sender, e)=>controller.Enable = false;
                    checkBoxes[i].IsEnabled = true;

                    var tooltip = new ToolTip() { };
                    CheckboxBg[i].ToolTip = tooltip;
                    CheckboxBg[i].ToolTipOpening += (sender, e) => tooltip.Content = $"0x{controller.Connection:x4}\n0x{controller.CidChannel:x4}".ToUpper();

                    timers[i].Interval = TimeSpan.FromMilliseconds(200);
                    timers[i].Tick += (sender, e)=> {
                        CheckboxBg[j].Background = null;
                        ((DispatcherTimer)sender).Stop();
                    };
                }
                cb1.IsChecked = true;
            }

            if(!USBPcapClient.is_usbpcap_upper_filter_installed())
            {
                MessageBox.Show("未安装USBPcap驱动，请先安装USBPcap驱动！", "提示");
                return;
            }

            var interfaces = USBPcapClient.find_usbpcap_filters();
            foreach (var filter in interfaces)
            {
                Interfaces.Items.Add(filter.Trim());
            }

            Interfaces.SelectionChanged += (object sender, SelectionChangedEventArgs e) => {
                var devices = USBPcapClient.enumerate_print_usbpcap_interactive(e.AddedItems[0].ToString());

                var devTree = devices.Split("\n");
                var devPattern = "\\[Port \\d+\\]\\s+\\(device id: (\\d+)\\) (.*)";
                var subPattern = "(\\s{6,})(.*)";
                var bthPattern = "Bluetooth|蓝牙";
                List<TreeViewItem> itemLevel = new List<TreeViewItem>();
                Devices.Items.Clear();
                deviceId = 0;
                foreach (var device in devTree)
                {
                    if (device.Contains("\\??\\")) continue;
                    var match = Regex.Match(device, devPattern);
                    if(match.Success)
                    {
                        TreeViewItem item = new TreeViewItem();
                        item.Header = $"({match.Groups[1]}) {match.Groups[2].ToString().Trim()}";
                        item.DataContext = match.Groups[1];
                        Devices.Items.Add(item);
                        itemLevel.Clear();
                        itemLevel.Add(item);
                        item.Selected += (object sender, RoutedEventArgs e)=> { deviceId = int.Parse(item.DataContext.ToString()); };
                        if (Regex.Match(device, bthPattern).Success && !bBluetoothFound)
                        { 
                            bBluetoothFound = true;
                            deviceId = int.Parse(item.DataContext.ToString());
                            item.IsSelected = true;
                        }
                    }
                    
                    match = Regex.Match(device, subPattern);
                    if (match.Success && itemLevel.Count > 0)
                    {
                        TreeViewItem itemChild = new TreeViewItem();
                        itemChild.Header = match.Groups[2].ToString().Trim();
                        int level = match.Groups[1].Length / 2 - 2;
                        itemLevel.RemoveRange(level, itemLevel.Count - level);
                        itemLevel.Last().Items.Add(itemChild);
                        itemLevel.Add(itemChild);
                    }
                }
            };

            bBluetoothFound = false;
            for (int i = 0; i < interfaces.Count; i++)
            {
                Interfaces.SelectedIndex = i;
                if (bBluetoothFound) break;
            }

            if (!bBluetoothFound)
            {
                Data.Text = "未找到蓝牙适配器\n您可能未以管理员身份运行此程序";
                Data.Foreground = Brushes.Red;
                Data.FontSize = 18;
            }
        }

        //private StreamWriter writer;

        private void StartCapture(object sender, RoutedEventArgs e)
        {
            if (!bStarted)
            {
                Data.Text = "";
                Data.Foreground = Brushes.Black;
                Data.FontSize = 12;
                //writer = new StreamWriter(new FileStream("XFuze_usbpcap.log", FileMode.Append, FileAccess.Write, FileShare.Read, 8096, FileOptions.Asynchronous | FileOptions.WriteThrough), Encoding.UTF8);
                //writer.WriteAsync("开始侦听-------\n");
                bStarted = true;
                var filter = Interfaces.SelectedItem.ToString();
                usbPcapClient = new USBPcapClient(filter, deviceId);
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
                        Dispatcher.InvokeAsync(new Action(() => UpdateControllers(e.Data)), System.Windows.Threading.DispatcherPriority.Input);
                        Dispatcher.InvokeAsync(new Action(() => Data.Text = data), System.Windows.Threading.DispatcherPriority.Render);
                        //Dispatcher.InvokeAsync(new Action(() => writer.WriteAsync(data + "\n\n")), System.Windows.Threading.DispatcherPriority.Background);
                    }
                };

                usbPcapClient.start_capture();
                StartButton.Content = "结束";
            }
            else
            {
                usbPcapClient.Dispose();
                //writer.WriteAsync("结束侦听-------\n");
                //writer?.Dispose();
                bStarted = false;
                StartButton.Content = "开始";
            }
        }

        void UpdateControllers(byte[] data)
        {
            if (data.Length != 20) return;

            for (int i = 0; i < controllers.Count; ++i)
            {
                var controller = controllers[i];
                if (controller.Parse(data))
                {
                    CheckboxBg[i].Background = Brushes.LightCyan;
                    timers[i].Stop();
                    timers[i].Start();
                    break;
                }
            }
        }

        public static string ToHexString(IEnumerable<byte> bytes, string deliminator = "")
        {
            var str = bytes.Aggregate(string.Empty, (current, b) => $"{current}{b:x2}{deliminator}")[..^deliminator.Length];
            return Regex.Replace(str, "(.{23}) (.{0,23}) ?", "$1  $2\n").ToUpper();
        }

        private void EnableHid(object sender, RoutedEventArgs e)
        {
            Process p = new Process();
            p.StartInfo.FileName = AppContext.BaseDirectory + "\\devcon.exe";
            p.StartInfo.Arguments = @"enable ""HID\{00001124-0000-1000-8000-00805F9B34FB}_VID&000212D1_PID&A560&COL02""";
            p.StartInfo.UseShellExecute = false;
            p.StartInfo.RedirectStandardOutput = true;
            p.StartInfo.CreateNoWindow = true;

            try
            {
                p.Start();
                var output = p.StandardOutput.ReadToEnd();
                p.WaitForExit();
                MessageBox.Show(output);
            }
            catch(Exception ex) 
            {
                MessageBox.Show(ex.ToString());
            }
        }

        private void DisableHid(object sender, RoutedEventArgs e)
        {
            try
            {
                var p = Process.Start(new ProcessStartInfo
                {
                    FileName = AppContext.BaseDirectory + "\\devcon.exe",
                    Arguments = @"disable ""HID\{00001124-0000-1000-8000-00805F9B34FB}_VID&000212D1_PID&A560&COL02""",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true,
                });
                var output = p.StandardOutput.ReadToEnd();
                p.WaitForExit();
                MessageBox.Show(output);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
        }

        private void ShowMain(object sender, RoutedEventArgs e)
        {
            Visibility = Visibility.Visible;
            ShowInTaskbar = true;
            Activate();
        }

        private void ExitClick(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void WindowClosing(object sender, EventArgs e)
        {
            Visibility = Visibility.Hidden;
            ShowInTaskbar = false;
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            e.Cancel = bStarted;
            base.OnClosing(e);
        }
    }
}
