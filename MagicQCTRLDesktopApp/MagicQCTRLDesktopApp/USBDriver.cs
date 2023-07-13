using System;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using HidSharp;
using MagicQCTRLDesktopApp;
using static System.Runtime.InteropServices.JavaScript.JSType;
using static MagicQCTRLDesktopApp.ViewModel;

namespace MagicQCTRLDesktopApp
{
    internal class USBDriver
    {
        /// <summary>
        /// A queue of messages received from the hardware to be processed.
        /// </summary>
        public ConcurrentQueue<MagicQCTRLUSBMessage> RXMessages { get; private set; }
        /// <summary>
        /// An event which is invoked when the device is forcibly closed.
        /// </summary>
        public event Action OnClose;
        public event Action OnMessageReceived;

        private readonly string MQCTRL_DEVICE_NAME = "MagicQ CTRL";
        private DeviceStream usbDevice;
        private Task usbRXTask;

        /// <summary>
        /// Connect to the MagicQCTRL USB device.
        /// </summary>
        /// <returns>true if the device was connected to successfully.</returns>
        public bool USBConnect()
        {
            RXMessages = new();

            foreach(var device in DeviceList.Local.GetAllDevices())
            {
                // Log($"Found device: path={device.DevicePath}; canOpen={device.CanOpen}; name={device.GetFriendlyName()}; fsName={device.GetFileSystemName()}", LogLevel.Debug);
                try
                {
                    if (device.GetFriendlyName() == MQCTRL_DEVICE_NAME)
                    {
                        Log("Found MagicQ CTRL hardware! Connecting...");
                        try
                        {
                            usbDevice = device.Open();
                            //usbDevice.Closed += UsbDevice_Closed;
                            usbDevice.ReadTimeout = -1;
                            usbRXTask = Task.Run(UsbRXTask);
                        }
                        catch (Exception ex)
                        {
                            Log($"Failed to open USB device: {ex}", LogLevel.Error);
                            return false;
                        }

                        Log("Connected to MagicQ CTRL hardware.");
                        return true;
                    }
                } catch { } // For some reason some devices fail to present a FriendlyName, we can ignore them
            }

            Log("No compatible device found!", LogLevel.Error);
            return false;
        }

        public void SendColourConfig(int page, int keyId, MagicQCTRLProfile profile)
        {
            if (usbDevice == null)
            {
                // Log($"Failed to send configuration data to hardware. USB device is not initialised!", LogLevel.Error);
                return;
            }

            if (keyId >= COLOUR_BUTTON_COUNT)
                return;

            MagicQCTRLColour colour = (profile.pages[page].keys[keyId].keyColourOff * profile.baseBrightness).Pow(2);
            MagicQCTRLColour activeColour = (profile.pages[page].keys[keyId].keyColourOn * profile.pressedBrightness).Pow(2);

            MagicQCTRLUSBConfigMessage msg = new()
            {
                header=0,
                msgType=MagicQCTRLConfigMessageType.Colour,
                page= (byte)page,
                keyId= (byte)keyId,
                colLowR=colour.r,
                colLowG=colour.g,
                colLowB=colour.b,
                colHighR=activeColour.r,
                colHighG=activeColour.g,
                colHighB=activeColour.b,
            };

            try
            {
                var data = MemoryMarshal.CreateReadOnlySpan(ref msg, 1);
                var bytes = MemoryMarshal.AsBytes(data);
                usbDevice.Write(bytes);
            } catch (Exception e)
            {
                Log($"Failed to send configuration data to hardware.\nError: {e}", LogLevel.Error);
            }
        }

        public void SendKeyNameMessage(int page, int keyId, MagicQCTRLProfile profile)
        {
            if (usbDevice == null)
            {
                Log($"Failed to send configuration data to hardware. USB device is not initialised!", LogLevel.Error);
                return;
            }

            ReadOnlySpan<char> name = profile.pages[page].keys[keyId].name.AsSpan();

            MagicQCTRLUSBConfigMessage msg = new()
            {
                header = 0,
                msgType = MagicQCTRLConfigMessageType.Name,
                page = (byte)page,
                keyId = (byte)(keyId >= COLOUR_BUTTON_COUNT ? keyId - COLOUR_BUTTON_COUNT : keyId),
                isEncoder = (byte)(keyId >= COLOUR_BUTTON_COUNT ? 1 : 0),
            };

            try
            {
                Span<byte> bytes = stackalloc byte[32];
                var data = MemoryMarshal.CreateReadOnlySpan(ref msg, 1);
                MemoryMarshal.AsBytes(data).CopyTo(bytes);
                int offset = (int)Unsafe.ByteOffset(ref msg.header, ref msg.name);
                Encoding.ASCII.GetBytes(name[..Math.Min(name.Length, MAX_NAME_LENGTH)], bytes[offset..]);
                usbDevice.Write(bytes);
            }
            catch (Exception e)
            {
                Log($"Failed to send configuration data to hardware.\nError: {e}", LogLevel.Error);
            }
        }

        private void UsbDevice_Closed(object sender, EventArgs e)
        {
            Log("USB device closed!", LogLevel.Warning);
            OnClose?.Invoke();
        }

        private void UsbRXTask()
        {
            Span<byte> buffer = stackalloc byte[64];

            while(usbDevice.CanRead)
            {
                try
                {
                    int bytesRead = usbDevice.Read(buffer);
                    var msg = MemoryMarshal.AsRef<MagicQCTRLUSBMessage>(buffer[1..]);
                    RXMessages.Enqueue(msg);
                    // Log($"Recv usb msg: len={bytesRead} data={msg}", LogLevel.Debug);
                    OnMessageReceived?.Invoke();
                } catch (Exception e)
                {
                    Log($"USB device closed!\nError: {e}", LogLevel.Warning);
                    OnClose?.Invoke();
                    return;
                }
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
        [FieldOffset(5)] public sbyte delta;

        public override string ToString() => $"{{header=0x{BinaryPrimitives.ReverseEndianness(header):X}; type={msgType}; page={page}; id={keyCode}; value={value}}}";
    }

    public enum MagicQCTRLMessageType : byte
    {
        Unknown = 0,
        Key = 1,
        Button = 2,
        Encoder = 3
    }

    [StructLayout(LayoutKind.Explicit, CharSet = CharSet.Ansi, Pack = 1, Size = 32)]
    public struct MagicQCTRLUSBConfigMessage
    {
        [FieldOffset(0)] public byte header;
        [FieldOffset(1)] public MagicQCTRLConfigMessageType msgType;
        [FieldOffset(2)] public byte page;
        [FieldOffset(3)] public byte keyId;
        [FieldOffset(4)] public byte isEncoder;

        [FieldOffset(5)] public byte colLowR;
        [FieldOffset(6)] public byte colLowG;
        [FieldOffset(7)] public byte colLowB;
        [FieldOffset(8)] public byte colHighR;
        [FieldOffset(9)] public byte colHighG;
        [FieldOffset(10)] public byte colHighB;

        //[MarshalAs(UnmanagedType.ByValTStr, SizeConst = MAX_NAME_LENGTH)]
        //[FieldOffset(5)] public ReadOnlyMemory<char> name;
        [FieldOffset(5)] public byte name;
    }

    public enum MagicQCTRLConfigMessageType : byte
    { 
        None = 0,
        Colour = 1,
        Name = 2
    }
}
