# Fuze手柄Xbox 360手柄模拟程序
淘宝买了个Fuze手柄，开机键是文字FUZE的那个版本。在windows上使用发现有很大延时，根本不能使用。
![XFuze.png](https://github.com/pmyy-wt/XFuzeController/blob/master/xfuze.png)

## 实现原理
用[USBPcap](https://github.com/desowin/usbpcap)抓包发现没有延时，于是采用抓包的方式将手柄的数据抓取下来，然后按照这个数据[格式](https://github.com/mumumusuc/FuzeController/tree/master)进行解释，然后转发到[ViGEmBus](https://github.com/nefarius/ViGEmBus)进行手柄模拟。

## 使用方法
1. 下载[XFuze](https://github.com/pmyy-wt/XFuzeController/releases/download/0.1/XFuze.zip)并解压
2. 下载并安装[.NET 8.0](https://dotnet.microsoft.com/zh-cn/download/dotnet/8.0)
3. 安装USBPcap驱动和ViGEmBus驱动，然后重启电脑
4. 配对并连接好FUZE手柄，断开其他蓝牙设备
5. 以管理员权限运行程序XFuze.exe，程序会默认选中蓝牙设备，并创建一个Xbox 360手柄
6. 点开始按钮，界面下方会显示抓取到的数据，摇一下手柄，界面上的1号手柄背景会相应变色指示

## 已知问题
1. 程序下方没有显示抓取的数据
   - 内嵌的蓝牙适配器抓包可能会有这个问题，可以更换USB蓝牙接收器解决
2. 只能解释Fuze手柄的数据，不支持其他牌子的蓝牙手柄
   - 其他牌子手柄请使用[XOutput](https://github.com/csutorasa/XOutput)
3. 不支持手柄震动
