using Nefarius.ViGEm.Client;
using Nefarius.ViGEm.Client.Targets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Nefarius.ViGEm.Client.Targets.Xbox360;

namespace XFuze
{
    public sealed class XFuzeController
    {
        private readonly IXbox360Controller controller;
        public ushort Connection = 0;
        public ushort CidChannel = 0;
        private bool EnableState = false;
        public bool Enable
        {
            get => EnableState;
            set
            {
                if (value) controller.Connect();
                else controller.Disconnect();
                Connection = CidChannel = 0;
                EnableState = value;
            }
        }

        public XFuzeController(ViGEmClient client) 
        {
            controller = client.CreateXbox360Controller();
            controller.SetButtonsFull(0);
        }

        ~XFuzeController() 
        { 
            controller.Disconnect();
        }

        public bool Parse(byte[] data)
        {
            if (data.Length != 20) return false;
            if (controller == null || !Enable) return false;

            byte[] conn = new byte[2];
            byte[] chnl = new byte[2];
            Buffer.BlockCopy(data, 0, conn, 0, conn.Length);
            Buffer.BlockCopy(data, 6, chnl, 0, chnl.Length);
            ushort connection = BitConverter.ToUInt16(conn, 0);
            ushort channel = BitConverter.ToUInt16(chnl, 0);
            if (Connection == 0)  Connection = connection;  
            if (CidChannel == 0) CidChannel = channel; 
            if (Connection != connection || CidChannel != channel) return false;

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
            controller.SetButtonState(Xbox360Button.Guide,          (input[2] & (0x11)) > 0);
            controller.SetButtonState(Xbox360Button.LeftThumb,      (input[2] & (0x20)) > 0);
            controller.SetButtonState(Xbox360Button.RightThumb,     (input[2] & (0x40)) > 0);
            controller.SetButtonState(Xbox360Button.Up,             (input[3] == 0 || input[3] == 1 || input[3] == 7));
            controller.SetButtonState(Xbox360Button.Down,           (input[3] == 3 || input[3] == 4 || input[3] == 5));
            controller.SetButtonState(Xbox360Button.Left,           (input[3] == 5 || input[3] == 6 || input[3] == 7));
            controller.SetButtonState(Xbox360Button.Right,          (input[3] == 1 || input[3] == 2 || input[3] == 3));

            controller.SetSliderValue(Xbox360Slider.LeftTrigger,    input[8]);
            controller.SetSliderValue(Xbox360Slider.RightTrigger,   input[9]);

            controller.SetAxisValue(Xbox360Axis.LeftThumbX,         (short)((input[4] << 8) - 0x8000));
            controller.SetAxisValue(Xbox360Axis.LeftThumbY,         (short)((~input[5] << 8) - 0x8000));
            controller.SetAxisValue(Xbox360Axis.RightThumbX,        (short)((input[6] << 8) - 0x8000));
            controller.SetAxisValue(Xbox360Axis.RightThumbY,        (short)((~input[7] << 8) - 0x8000));
            controller.SubmitReport();
            return true;
        }
    }
}
