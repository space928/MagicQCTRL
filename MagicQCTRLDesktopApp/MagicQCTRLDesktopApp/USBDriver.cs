using System;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using HidSharp;
using MagicQCTRLDesktopApp;
using static MagicQCTRLDesktopApp.ViewModel;

namespace MagicQCTRLDesktopApp
{
    internal class USBDriver
    {
        public ConcurrentQueue<MagicQCTRLUSBMessage> RXMessages { get; private set; }

        private readonly string MQCTRL_DEVICE_NAME = "MagicQ CTRL";
        private DeviceStream usbDevice;
        private Task usbRXTask;

        public bool USBConnect()
        {
            RXMessages = new();

            foreach(var device in DeviceList.Local.GetAllDevices())
            {
                Log($"Found device: path={device.DevicePath}; canOpen={device.CanOpen}; name={device.GetFriendlyName()}; fsName={device.GetFileSystemName()}", LogLevel.Debug);

                if(device.GetFriendlyName() == MQCTRL_DEVICE_NAME)
                {
                    Log("Found MagicQ CTRL hardware! Connecting...");
                    try
                    {
                        usbDevice = device.Open();
                        usbDevice.Closed += UsbDevice_Closed;
                        usbDevice.ReadTimeout = -1;
                    } catch (Exception ex)
                    {
                        Log($"Failed to open USB device: {ex}", LogLevel.Error);
                        return false;
                    }

                    break;
                }
            }

            usbRXTask = Task.Run(UsbRXTask);

            Log("Connected to MagicQ CTRL hardware.");
            return true;
        }

        private void UsbDevice_Closed(object sender, EventArgs e)
        {
            Log("USB device closed!", LogLevel.Warning);
        }

        private void UsbRXTask()
        {
            Span<byte> buffer = stackalloc byte[64];

            while(usbDevice.CanRead)
            {
                int bytesRead = usbDevice.Read(buffer);
                var msg = MemoryMarshal.AsRef<MagicQCTRLUSBMessage>(buffer[1..]);
                RXMessages.Enqueue(msg);
                Log($"Recv usb msg: len={bytesRead} data={msg}", LogLevel.Debug);
            }
        }
    }

    [StructLayout(LayoutKind.Explicit)]
    public struct MagicQCTRLUSBMessage
    {
        [FieldOffset(0)] public short header;
        [FieldOffset(2)] public MagicQCTRLMessageType msgType;
        [FieldOffset(3)] public byte page;
        [FieldOffset(4)] public byte keyCode;
        [FieldOffset(4)] public byte buttonCode;
        [FieldOffset(4)] public byte encoderId;
        [FieldOffset(5)] public byte value;
        [FieldOffset(5)] public byte delta;

        public override string ToString() => $"{{header=0x{BinaryPrimitives.ReverseEndianness(header):X}; type={msgType}; page={page}; id={keyCode}; value={value}}}";
    }

    public enum MagicQCTRLMessageType : byte
    {
        Unknown = 0,
        Key = 1,
        Button = 2,
        Encoder = 3
    }
}
