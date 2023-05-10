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
            oscReceiver.Dispose();
            oscSender.Dispose();
        }
    }
}
