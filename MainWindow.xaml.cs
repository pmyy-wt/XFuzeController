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
using UsbPcapDotNet;

namespace XFuze
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void MainWindowLoaded(object sender, RoutedEventArgs e)
        {
            if(!USBPcapClient.is_usbpcap_upper_filter_installed())
            {
                System.Windows.MessageBox.Show("未安装USBPcap，请先安装USBPcap！", "提示");
                return;
            }

            var interfaces = USBPcapClient.find_usbpcap_filters();
            foreach (var filter in interfaces)
            {
                Interfaces.Items.Add(filter);
            }

            Interfaces.SelectionChanged += (object sender, SelectionChangedEventArgs e) => {
                System.Windows.MessageBox.Show(e.AddedItems[0].ToString(), "消息");
                var devices = USBPcapClient.enumerate_print_usbpcap_interactive(e.AddedItems[0].ToString());
                System.Windows.MessageBox.Show(devices, "消息");
                //Devices.Items.Clear();
                //foreach (var device in devices)
                //{
                //    Devices.Items.Add(device);
                //}
            };
            Interfaces.SelectedIndex = 0;

   //         var devices = interfaces.Select(f => (filterId: f, deviceTree: USBPcapClient.enumerate_print_usbpcap_interactive(f)))
   //.OrderByDescending(f => f.deviceTree.Length);
        }
    }
}
