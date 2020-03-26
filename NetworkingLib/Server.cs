using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;

namespace NetworkingLib
{

    public class Server
    {
        public delegate void ReceivedEventHandler(object[][] data, string ip, int port);
        public delegate void ConnectionLostEventHandler(string ip, int port);
        public delegate void ConnectionAcceptedEventHandler(string ip, int port, long ping);
        public delegate void ConnectionLobbyAcceptedEventHandler(string ip, int port, long ping);
        public event ConnectionLostEventHandler OnClientDisconnectedEvent;
        public event ReceivedEventHandler OnReceivedEvent;
        public event ConnectionAcceptedEventHandler OnConnectionAcceptedEvent;
        public event ConnectionLobbyAcceptedEventHandler OnConnectionLobbyAcceptedEvent;

        private readonly int port;
        private readonly char packetSplitter;
        private readonly char argSplitter;
        
        public List<TcpClient> clients = new List<TcpClient>();
        public List<TcpClient> lobbyClients = new List<TcpClient>();
        private TcpListener tcpListener;
        private TcpListener tcpLobbyListener;

       

        public Server(int port, char packetSplitter, char argSplitter)
        {
            this.port = port;
            this.packetSplitter = packetSplitter;
            this.argSplitter = argSplitter;
        }

        public void StartServer()
        {
            try
            {
                tcpListener = new TcpListener(IPAddress.Any, port);
                tcpListener.Start();
                tcpLobbyListener = new TcpListener(IPAddress.Any, port - 1);
                tcpLobbyListener.Start();
                new Thread(new ThreadStart(AcceptConnection)).Start();
                new Thread(new ThreadStart(AcceptLobbyConnection)).Start();
            }
            catch (Exception e)
            {
                throw e;
            }
        }

        private void AcceptConnection()
        {
            while (true)
            {
                TcpClient client;
                long ping;
                try
                {
                    client = tcpListener.AcceptTcpClient();
                    clients.Add(client);
                    new Thread(new ThreadStart(() => TryToRecieve(client))).Start();
                    Ping p = new Ping();
                    PingReply pInfo = p.Send(((IPEndPoint)client.Client.RemoteEndPoint).Address);
                    ping = pInfo.RoundtripTime;
                    OnConnectionAcceptedEvent?.Invoke(((IPEndPoint)client.Client.RemoteEndPoint).Address.ToString(), ((IPEndPoint)client.Client.RemoteEndPoint).Port, ping);
                }
                catch
                {

                }
            }
        }

        private void AcceptLobbyConnection()
        {
            while (true)
            {
                TcpClient client;
                long ping;
                try
                {
                    client = tcpLobbyListener.AcceptTcpClient();
                    lobbyClients.Add(client);
                    Ping p = new Ping();
                    PingReply pInfo = p.Send(((IPEndPoint)client.Client.RemoteEndPoint).Address);
                    ping = pInfo.RoundtripTime;
                    OnConnectionLobbyAcceptedEvent?.Invoke(((IPEndPoint)client.Client.RemoteEndPoint).Address.ToString(), ((IPEndPoint)client.Client.RemoteEndPoint).Port, ping);
                }
                catch
                {

                }
            }
        }

        public void SendToAUser(object[] args, string ip, int port)
        {
            TcpClient client = GetClient(ip, port);
            if (client == null)
            {
                Console.WriteLine("Client not found!");
                return;
            }
            string data = "";
            foreach (object arg in args)
            {
                data += arg + argSplitter.ToString();
            }
            data = data.Substring(0, data.Length - 1);
            try
            {
                byte[] b = Encoding.UTF8.GetBytes(data + packetSplitter.ToString());
                client.GetStream().Write(b, 0, b.Length);
            }
            catch
            {
                ConnectionLostRaise(client);
            }

        }

        public void Broadcast(object[] args, params int[] ports)
        {
            TcpClient client = null;
            List<int> portsList = null;
            if (ports == null)
            {
                portsList = new List<int>();
            }
            else
            {
                portsList = ports.ToList();
            }
            string data = "";
            foreach (object arg in args)
            {
                data += arg + argSplitter.ToString();
            }
            data = data.Substring(0, data.Length - 1);
            try

            {
                byte[] b = Encoding.UTF8.GetBytes(data + packetSplitter.ToString());
                for (int i = 0; i < clients.Count; i++)
                {
                    client = clients[i];
                    if (!portsList.Contains(int.Parse(((IPEndPoint)client.Client.RemoteEndPoint).Address.ToString().Replace(".", "") + "" + ((IPEndPoint)client.Client.RemoteEndPoint).Port)))
                    {
                        client.GetStream().Write(b, 0, b.Length);
                        portsList.Add(int.Parse(((IPEndPoint)client.Client.RemoteEndPoint).Address.ToString().Replace(".", "") + "" + ((IPEndPoint)client.Client.RemoteEndPoint).Port));
                    }
                }
            }
            catch
            {
                ConnectionLostRaise(client);
                Broadcast(args, portsList.ToArray()); // Keeps send data to all other connected users.
            }

        }
        private void TryToRecieve(TcpClient client)
        {
            PacketManager packetManager = new PacketManager();
            while (true)
            {
                byte[] b = new byte[1024 * 8];
                int i = 0;
                try
                {
                    i = client.GetStream().Read(b, 0, b.Length);
                }
                catch
                {
                    ConnectionLostRaise(client);
                    return;
                }
                if (Encoding.UTF8.GetString(b, 0, i) == "")
                {
                    continue;
                }
                object[] packets = packetManager.AddStream(b, i, packetSplitter);
                object[][] packetsArgs = new object[packets.Length][];
                for (int j = 0; j < packetsArgs.Length; j++)
                {
                    packetsArgs[j] = packets[j].ToString().Split(argSplitter);
                }
                OnReceivedEvent?.Invoke(packetsArgs, ((IPEndPoint)client.Client.RemoteEndPoint).Address.ToString(), ((IPEndPoint)client.Client.RemoteEndPoint).Port);
            }
        }

        private TcpClient GetClient(string ip, int port)
        {
            TcpClient client = GetClientByList(ip, port, clients);
            if (client != null) return client;
            client = GetClientByList(ip, port, lobbyClients);
            return client;
        }

        private TcpClient GetClientByList(string ip, int port, List<TcpClient> list)
        {
            try
            {
                for (int i = 0; i < clients.Count; i++)
                {
                    TcpClient client = clients[i];
                    IPEndPoint cliendInfo = (IPEndPoint)client.Client.RemoteEndPoint;
                    if (cliendInfo.Address.ToString() == ip && cliendInfo.Port == port)
                    {
                        return client;
                    }
                }


            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw e;
            }

            return null;
        }

        private void ConnectionLostRaise(TcpClient client)
        {
            Console.WriteLine("An unexpected disconnection, source: " + ((IPEndPoint)client.Client.RemoteEndPoint).ToString());
            if (clients.Contains(client))
            {
                clients.Remove(client);
            }

            if (lobbyClients.Contains(client))
            {
                lobbyClients.Remove(client);
            }
            OnClientDisconnectedEvent?.Invoke(((IPEndPoint)client.Client.RemoteEndPoint).Address.ToString(), ((IPEndPoint)client.Client.RemoteEndPoint).Port);
        }
    }
}
