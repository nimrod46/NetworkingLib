using System;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace NetworkingLib
{
    public class DirectClient
    {
        public delegate void ReceivedEventHandler(string[] data, string address, int port);
        public event ReceivedEventHandler OnReceivedEvent;
        public delegate void ServerDisconnectedEventHandler(string ip, int port);
        public event ServerDisconnectedEventHandler OnServerDisconnectedEvent;
        public string ip;
        public int port;
        public bool isConnected;
        private UdpClient client;
        private IPEndPoint server;
        char argSplitter;
        public DirectClient(string ip, int port, char argSplitter)
        {
            IPAddress iPAddress;
            try
            {
                iPAddress = IPAddress.Parse(ip);
            }
            catch (Exception e)
            {
                throw e;
            }
            this.ip = ip;
            this.port = port;
            isConnected = false;
            this.argSplitter = argSplitter;
            server = new IPEndPoint(iPAddress, port);
        }
        public void Start()
        {
            client = new UdpClient(ip, port);
            try
            {
                client.Connect(ip, port);
                isConnected = true;
                new Thread(new ThreadStart(TryToReceive)).Start();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                return;
            }
        }

        private void TryToReceive()
        {
            while (isConnected)
            {
                try
                {
                    string arg = Encoding.Unicode.GetString(client.Receive(ref server));
                    string[] args;
                    args = arg.Split(argSplitter);
                    OnReceivedEvent?.Invoke(args, server.Address.ToString(), server.Port);
                }
                catch (Exception e)
                {
                    ConnectionLostRaise();
                    Console.WriteLine(e);
                    break;
                }
            }
        }
        public void Send(object[] args)
        {
            string data = "";
            foreach (object arg in args)
            {
                data += arg + argSplitter.ToString();
            }
            data = data.Substring(0, data.Length - 1);
            byte[] messageToSend = Encoding.Unicode.GetBytes(data);
            try
            {
                client.Send(messageToSend, messageToSend.Length);
            }
            catch (Exception e)
            {
                ConnectionLostRaise();
                Console.WriteLine(e);
            }
        }

        public void Refresh()
        {
            if (client == null) // Returns if connected has not been called.
            {
                isConnected = false;
                return;
            }

            if (!client.Client.Connected) // Check if client is connected.
            {
                isConnected = false;
                return;
            }
            isConnected = true;
        }

        private void ConnectionLostRaise()
        {
            Console.WriteLine("The UDP client encountered a plroblem with the server and will close the connection");
            isConnected = false;
            OnServerDisconnectedEvent?.Invoke(ip, port);
        }

        public void Disconnect()
        {
            Refresh();
            if (!isConnected) return;
            try
            {
                client.Close();
            }
            catch
            {
            }
            finally
            {
                ConnectionLostRaise();
            }
        }
    }
}
