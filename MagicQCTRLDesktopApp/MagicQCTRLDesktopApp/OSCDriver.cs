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

namespace MagicQCTRLDesktopApp;

internal class OSCDriver : IDisposable, INotifyConnectionStatus
{
    public ConcurrentQueue<OscPacket> RXMessages { get; private set; } = [];
    public event Action<bool>? OnConnectionStatusChanged;
    public bool IsConnected => oscReceiver != null && oscSender != null
        && oscReceiver.State == OscSocketState.Connected && oscSender.State == OscSocketState.Connected;

    private OscReceiver? oscReceiver;
    private OscSender? oscSender;

    private IPEndPoint? rxIP;
    private IPEndPoint? txIP;

    private volatile bool isDisposing;

    public bool OSCConnect(IPAddress nicAddress, int rxPort, int txPort)
    {
        OnConnectionStatusChanged?.Invoke(false);
        RXMessages = new();
        rxIP = new IPEndPoint(nicAddress ?? IPAddress.Any, rxPort);
        txIP = new IPEndPoint(nicAddress ?? IPAddress.Broadcast, txPort);

        Log($"Connecting to OSC... rx={rxIP} tx={txIP}");

        try
        {
            Dispose();

            oscReceiver = new(rxIP.Address, rxIP.Port);
            oscReceiver.Connect();

            oscSender = new(txIP.Address, 0, txIP.Port);
            oscSender.Connect();

            Task.Run(OSCRXThread);
            OnConnectionStatusChanged?.Invoke(IsConnected);
        }
        catch (Exception e)
        {
            Log($"Failed to connect to OSC port: {e}", LogLevel.Error);
            return false;
        }

        Log("Connected to OSC network!");

        return true;
    }

    public void SendMessage(OscPacket packet)
    {
        try
        {
            oscSender?.Send(packet);
        }
        catch (Exception e)
        {
            if (isDisposing)
                return;

            Log($"OSC failed to send message: {e}", LogLevel.Warning);
            OnConnectionStatusChanged?.Invoke(false);
        }
    }

    private void OSCRXThread()
    {
        while (oscReceiver != null && oscReceiver.State == OscSocketState.Connected)
        {
            try
            {
                var pkt = oscReceiver.Receive();
                RXMessages.Enqueue(pkt);

                //Log($"Recv osc msg: {pkt}", LogLevel.Debug);
            }
            catch (Exception e)
            {
                if (isDisposing)
                    return;

                Log($"OSC Network connection lost: {e}", LogLevel.Warning);
                OnConnectionStatusChanged?.Invoke(false);
            }
        }
    }

    public void Dispose()
    {
        isDisposing = true;
        OnConnectionStatusChanged?.Invoke(false);
        oscReceiver?.Dispose();
        oscSender?.Dispose();
        isDisposing = false;
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
        if (argsStart != -1)
        {
            address = message[..argsStart];
            var strArgs = message[(argsStart + 1)..].Split(' ');
            for (int i = 0; i < strArgs.Length; i++)
            {
                if (bool.TryParse(strArgs[i], out var bVal))
                    args.Add(bVal);
                else if (int.TryParse(strArgs[i], out var iVal))
                    args.Add(iVal);
                else if (float.TryParse(strArgs[i], out var fVal))
                    args.Add(fVal);
                else if (strArgs[i].Length > 0 && strArgs[i][0] == '\"')
                {
                    if (strArgs[i].Length > 1 && strArgs[i][^1] == '\"')
                    {
                        args.Add(strArgs[i][1..^1]);
                    }
                    else
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
                }
                else if (strArgs[i].Length > 3 && strArgs[i][0] == '`' && strArgs[i][^1] == '`')
                {
                    args.Add(StringToByteArrayFastest(strArgs[i][1..^1]));
                }
                else
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
