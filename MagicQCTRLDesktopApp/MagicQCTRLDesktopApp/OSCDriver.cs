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
    internal class OSCDriver
    {
        public ConcurrentQueue<string> RXMessages { get; private set; }

        private UdpClient rxUdpClient;
        private UdpClient txUdpClient;

        private Task rxThread;
        private Task txThread;

        private IPEndPoint rxIP;
        private IPEndPoint txIP;

        public bool OSCConnect(int rxPort, int txPort)
        {
            RXMessages = new();
            rxIP = new IPEndPoint(IPAddress.Broadcast, rxPort);
            txIP = new IPEndPoint(IPAddress.Loopback, rxPort);

            Log($"Connecting to OSC... rx={rxIP} tx={txIP}");

            try
            {
                rxUdpClient = new();
                txUdpClient = new();

                rxUdpClient.Connect(rxIP);
                txUdpClient.Connect(txIP);

                rxThread = new Task(OSCRXThread);
            } catch (Exception e)
            {
                Log($"Failed to connect to OSC port: {e}", LogLevel.Error);
                return false;
            }

            Log("Connected to OSC network!");

            return true;
        }

        private void OSCRXThread()
        {
            rxUdpClient.Client.ReceiveTimeout = -1;
            while(rxUdpClient.Client.Connected)
            {
                try
                {
                    string msg = Encoding.UTF8.GetString(rxUdpClient.Receive(ref rxIP));
                    RXMessages.Enqueue(msg);
                    Log($"Recv osc msg: {msg}", LogLevel.Debug);
                } catch (Exception e)
                {
                    Log($"OSC Network connection lost: {e}", LogLevel.Warning);
                }
            }
        }
    }
}
