using Rug.Osc;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using static MagicQCTRLDesktopApp.ViewModel;

namespace MagicQCTRLDesktopApp
{
    internal class OSCDriver : IDisposable
    {
        public ConcurrentQueue<OscPacket> RXMessages { get; private set; }

        private OscReceiver oscReceiver;
        private OscSender oscSender;

        private IPEndPoint rxIP;
        private IPEndPoint txIP;

        public bool OSCConnect(int rxPort, int txPort)
        {
            RXMessages = new();
            rxIP = new IPEndPoint(IPAddress.Any, rxPort);
            txIP = new IPEndPoint(IPAddress.Broadcast, txPort);

            Log($"Connecting to OSC... rx={rxIP} tx={txIP}");

            try
            {
                Dispose();

                oscReceiver = new(rxPort);
                oscReceiver.Connect();

                oscSender = new(txIP.Address, 0, txIP.Port);
                oscSender.Connect();

                Task.Run(OSCRXThread);
            } catch (Exception e)
            {
                Log($"Failed to connect to OSC port: {e}", LogLevel.Error);
                return false;
            }

            Log("Connected to OSC network!");

            return true;
        }

        public void SendMessage(OscPacket packet)
        {
            oscSender.Send(packet);
        }

        private void OSCRXThread()
        {
            while(oscReceiver.State == OscSocketState.Connected)
            {
                try
                {
                    var pkt = oscReceiver.Receive();
                    RXMessages.Enqueue(pkt);
                    // Log($"Recv osc msg: {pkt}", LogLevel.Debug);
                } catch (Exception e)
                {
                    Log($"OSC Network connection lost: {e}", LogLevel.Warning);
                }
            }
        }

        public void Dispose()
        {
            oscReceiver?.Dispose();
            oscSender?.Dispose();
        }
    }

    public static class OSCMessageParser
    {
        /// <summary>
        /// Parses a string representing an OSC message into an address and a list of arguments.<br/>
        /// Arguments must be separated by spaces and are parsed automatically
        /// Supports:
        ///  - strings -> Surrounded by double quotes
        ///  - ints
        ///  - floats
        ///  - bools
        ///  - blobs -> Surrounded by backticks
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        public static (string address, object[] args) ParseOSCMessage(string message)
        {
            ReadOnlyMemory<char> msg = message.AsMemory();
            int argsStart = message.IndexOf(' ');
            var args = new List<object>();
            string address = message;
            if(argsStart != -1)
            {
                address = message[..argsStart];
                var strArgs = message[(argsStart+1)..].Split(' ');
                for(int i = 0; i < strArgs.Length; i++) 
                {
                    if (bool.TryParse(strArgs[i], out var bVal))
                        args.Add(bVal);
                    else if(int.TryParse(strArgs[i], out var iVal))
                        args.Add(iVal);
                    else if(float.TryParse(strArgs[i], out var fVal))
                        args.Add(fVal);
                    else if (strArgs[i].Length > 0 && strArgs[i][0] == '\"')
                    {
                        if (strArgs[i].Length > 1 && strArgs[i][^1] == '\"')
                        {
                            args.Add(strArgs[i][1..^1]);
                        } else
                        {
                            // String must have spaces in it, search for the next arg that ends in a double quote
                            StringBuilder sb = new(strArgs[i][1..]);
                            do
                            {
                                i++;
                                sb.Append(' ');
                                sb.Append(strArgs[i]);
                            } while (i < strArgs.Length && strArgs[i][^1] != '\"');

                            if (strArgs[i][^1] != '\"')
                                throw new ArgumentException($"Unparsable OSC argument, string is not closed: {sb.ToString()}");

                            args.Add(sb.ToString());
                        }
                    } else if (strArgs[i].Length > 3 && strArgs[i][0] == '`' && strArgs[i][^1] == '`')
                    {
                        args.Add(StringToByteArrayFastest(strArgs[i][1..^1]));
                    } else
                    {
                        throw new ArgumentException($"Unparsable OSC argument encountered: {strArgs[i]}");
                    }
                }
            }

            return (address, args.ToArray());
        }

        // https://stackoverflow.com/a/9995303
        private static byte[] StringToByteArrayFastest(string hex)
        {
            if (hex.Length % 2 == 1)
                throw new Exception("The binary key cannot have an odd number of digits");

            byte[] arr = new byte[hex.Length >> 1];

            for (int i = 0; i < hex.Length >> 1; ++i)
            {
                arr[i] = (byte)((GetHexVal(hex[i << 1]) << 4) + (GetHexVal(hex[(i << 1) + 1])));
            }

            return arr;
        }

        private static int GetHexVal(char hex)
        {
            int val = (int)hex;
            //For uppercase A-F letters:
            //return val - (val < 58 ? 48 : 55);
            //For lowercase a-f letters:
            //return val - (val < 58 ? 48 : 87);
            //Or the two combined, but a bit slower:
            return val - (val < 58 ? 48 : (val < 97 ? 55 : 87));
        }
    }
}
