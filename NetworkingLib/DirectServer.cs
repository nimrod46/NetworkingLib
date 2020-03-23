using System;
using System.Text;
using System.Net.Sockets;
using System.Threading;
using System.Net;
namespace NetworkingLib
{
    public class DirectServer
    {
        public const int SIO_UDP_CONNRESET = -1744830452;
        public delegate void ReceivedEventHandler(string[] data, string ip, int port);
        public delegate void ClientDisconnectedEventHandler(string ip, int port);
        public event ReceivedEventHandler OnReceivedEvent;
        public event ClientDisconnectedEventHandler OnClientDisconnectedEvent;
        private UdpClient server;
        public int port;
        char argSplitter;
        public DirectServer(int port, char argSplitter)
        {
            this.port = port;
            this.argSplitter = argSplitter;
        }

        public void Start()
        {
            server = new UdpClient(port);
            server.Client.IOControl(
    (IOControlCode)SIO_UDP_CONNRESET,
    new byte[] { 0, 0, 0, 0 },
    null
);
            new Thread(new ThreadStart(TryToReceive)).Start();
        }

        byte[] dataReceived = new byte[1024];
        private void TryToReceive()
        {
            IPEndPoint remoteIp = new IPEndPoint(IPAddress.Any, port);
            while (true)
            {
                try
                {
                    dataReceived = server.Receive(ref remoteIp);
                    string arg = Encoding.Unicode.GetString(dataReceived);
                    string[] args;
                    args = arg.Split(argSplitter);
                    OnReceivedEvent?.Invoke(args, remoteIp.Address.ToString(), remoteIp.Port);
                }
                catch (Exception e)
                {
                    OnClientDisconnectedEvent?.Invoke(remoteIp.Address.ToString(), remoteIp.Port);
                    Console.WriteLine(e);
                }
            }
        }

        public void Send(string[] args, string ip, int port)
        {
            IPAddress ipA = IPAddress.Parse(ip);
            IPEndPoint remoteIp = new IPEndPoint(ipA, port);
            string data = "";
            foreach (string arg in args)
            {
                data += arg + argSplitter.ToString();
            }
            data = data.Substring(0, data.Length - 1);
            try
            {
                byte[] messageToSend = Encoding.Unicode.GetBytes(data);
                server.Send(messageToSend, messageToSend.Length, (IPEndPoint)remoteIp);
            }
            catch (Exception e)
            {
                OnClientDisconnectedEvent?.Invoke(ip, port);
                Console.WriteLine(e);
            }
        }
    }
}
