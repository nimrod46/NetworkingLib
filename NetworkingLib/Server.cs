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
        public enum NetworkInterfaceType
        {
            TCP,
            UDP
        }

        public struct SocketInfo
        {
            public string Ip { get; set; }
            public int Port { get; set; }
            public NetworkInterfaceType NetworkInterface { get; set; }

            public SocketInfo(string ip, int port, NetworkInterfaceType networkInterface)
            {
                Ip = ip;
                Port = port;
                NetworkInterface = networkInterface;
            }
        }
        public struct EndPointId : IComparable
        {
            public long Id { get; set; }

            private EndPointId(long id)
            {
                Id = id;
            }

            public static bool operator ==(EndPointId i1, EndPointId i2)
            {
                return i1.Equals(i2);
            }

            public static bool operator !=(EndPointId i1, EndPointId i2)
            {
                return !i1.Equals(i2);
            }

            public override int GetHashCode()
            {
                return Id.GetHashCode();
            }

            internal static EndPointId FromSocket(SocketInfo socketInfo)
            {
                return new EndPointId(GetIdFromSocket(socketInfo.Ip, socketInfo.Port));
            }

            internal static EndPointId FromSocket(string ipAddress, int port)
            {
                return new EndPointId(GetIdFromSocket(ipAddress, port));
            }

            public static EndPointId FromLong(long id)
            {
                return new EndPointId(id);
            }

            private static long GetIdFromSocket(string ipAddress, int port)
            {
                return long.Parse(ipAddress.Replace(".", "") + port.ToString());
            }

            public static EndPointId InvalidIdentityId = new EndPointId(-1);

            public override bool Equals(object obj)
            {
                return obj is EndPointId id &&
                       Id == id.Id;
            }

            public int CompareTo(object obj)
            {
                if (obj is EndPointId id)
                {
                    return Id.CompareTo(id.Id);
                }
                return 0;
            }

            public override string ToString()
            {
                return Id.ToString();
            }
        }

        //public struct TcpIdentity
        //{
        //    private readonly IdentityId identityId;
        //    private readonly TcpClient tcpClient;

        //    public TcpIdentity(IdentityId identityId, TcpClient tcpClient)
        //    {
        //        this.identityId = identityId;
        //        this.tcpClient = tcpClient;
        //    }
        //}

        public delegate void ReceivedEventHandler(object[][] data, EndPointId id, SocketInfo socketInfo);
        public delegate void ConnectionLostEventHandler(EndPointId id);
        public delegate void ConnectionAcceptedEventHandler(EndPointId id, long ping);
        public delegate void ConnectionLobbyAcceptedEventHandler(EndPointId id, long ping);
        public event ConnectionLostEventHandler OnClientDisconnectedEvent;
        public event ReceivedEventHandler OnReceivedEvent;
        public event ConnectionAcceptedEventHandler OnConnectionAcceptedEvent;
        public event ConnectionLobbyAcceptedEventHandler OnConnectionLobbyAcceptedEvent;

        private readonly int port;
        private readonly EndPointId serverEndPointId;
        private readonly char packetSplitter;
        private readonly char argSplitter;
        
        public Dictionary<EndPointId, TcpClient> clients = new Dictionary<EndPointId, TcpClient>();
        public Dictionary<EndPointId, TcpClient> lobbyClients = new Dictionary<EndPointId, TcpClient>();
        private TcpListener tcpListener;
        private TcpListener tcpLobbyListener;

       

        public Server(int port, char packetSplitter, char argSplitter)
        {
            this.port = port;
            serverEndPointId = EndPointId.FromLong(port);
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
                    string ip = ((IPEndPoint)client.Client.RemoteEndPoint).Address.ToString();
                    int port = ((IPEndPoint)client.Client.RemoteEndPoint).Port;
                    Ping p = new Ping();
                    PingReply pInfo = p.Send(((IPEndPoint)client.Client.RemoteEndPoint).Address);
                    ping = pInfo.RoundtripTime;
                    EndPointId identityId = EndPointId.FromSocket(ip, port);
                    OnConnectionAcceptedEvent?.Invoke(identityId, ping);
                    clients.Add(identityId, client);
                    new Thread(new ThreadStart(() => TryToRecieve(identityId, client, ip, port))).Start();
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
                    EndPointId identityId = EndPointId.FromSocket(((IPEndPoint)client.Client.RemoteEndPoint).Address.ToString(), ((IPEndPoint)client.Client.RemoteEndPoint).Port);
                    lobbyClients.Add(identityId, client);
                    Ping p = new Ping();
                    PingReply pInfo = p.Send(((IPEndPoint)client.Client.RemoteEndPoint).Address);
                    ping = pInfo.RoundtripTime;
                    OnConnectionLobbyAcceptedEvent?.Invoke(identityId, ping);
                }
                catch
                {

                }
            }
        }

        public void SendToAUser(object[] args, EndPointId identityId)
        {
            TcpClient client = GetClient(identityId);
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
                ConnectionLostRaise(identityId, client);
            }

        }

        public void Broadcast(object[] args, params EndPointId[] identityIds)
        {
            List<EndPointId> idsList;
            if (identityIds == null)
            {
                idsList = new List<EndPointId>();
            }
            else
            {
                idsList = identityIds.ToList();
            }
            string data = "";
            foreach (object arg in args)
            {
                data += arg + argSplitter.ToString();
            }
            data = data.Substring(0, data.Length - 1);
            TcpClient client = null;
            EndPointId id = EndPointId.InvalidIdentityId;
            try
            {
                byte[] b = Encoding.UTF8.GetBytes(data + packetSplitter.ToString());
                foreach (var idAndClient in clients)
                {
                    client = idAndClient.Value;
                    id = idAndClient.Key;
                    if (!idsList.Contains(id))
                    {
                        client.GetStream().Write(b, 0, b.Length);
                        idsList.Add(id);
                    }
                }
            }
            catch
            {
                ConnectionLostRaise(id, client);
                Broadcast(args, idsList.ToArray()); // Keep send data to all other connected users.
            }

        }
        private void TryToRecieve(EndPointId identityId, TcpClient client, string ip, int port)
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
                    ConnectionLostRaise(identityId, client);
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
                OnReceivedEvent?.Invoke(packetsArgs, identityId, new SocketInfo(ip, port, NetworkInterfaceType.TCP));
            }
        }

        private TcpClient GetClient(EndPointId identityId)
        {
            if(clients.TryGetValue(identityId, out TcpClient client))
            {
                return client;
            }
            if (lobbyClients.TryGetValue(identityId, out client))
            {
                return client;
            }
            return null;
        }

        private void ConnectionLostRaise(EndPointId id, TcpClient client)
        {
            if (clients.ContainsKey(id) || lobbyClients.ContainsKey(id))
            {
                Console.WriteLine("An unexpected disconnection, source: " + ((IPEndPoint)client.Client.RemoteEndPoint).ToString());
                clients.Remove(id);
                lobbyClients.Remove(id);
                OnClientDisconnectedEvent?.Invoke(id);
            }
        }
    }
}
