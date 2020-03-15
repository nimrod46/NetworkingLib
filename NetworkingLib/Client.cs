using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.IO;
using System.Drawing;
using System.Threading;

namespace NetworkingLib
{

    public class Client
    {
        public delegate void ReceivedEventHandler(string[][] data, string ip, int port);
        public delegate void ConnectionLostEventHandler(string ip, int port);
        public event ConnectionLostEventHandler OnConnectionLostEvent;
        public event ReceivedEventHandler OnReceivedEvent;

        public readonly string ServerIp;
        public readonly int ServerPort;
        public bool isConnected = false;
        private readonly char packetSplitter;
        private readonly char argSplitter;
        private TcpClient client;
        private TcpClient clientLobby;
        private NetworkStream stream;
        private NetworkStream lobbyInfoStream;
        

        public Client(string ip, int port, char packetSplitter, char argSplitter)
        {
            try
            {
                IPAddress.Parse(ip);
                ServerIp = ip;
            }
            catch (Exception e)
            {
                throw e;
            }
            ServerPort = port;
            this.packetSplitter = packetSplitter;
            this.argSplitter = argSplitter;
        }

        public bool Connect(out long pingMs)
        {
            pingMs = 0;
            try
            {
                client = new TcpClient();
                client.Connect(ServerIp, ServerPort);
                stream = client.GetStream();
                isConnected = true;
                Ping ping = new Ping();
                pingMs = ping.Send(ServerIp).RoundtripTime;
                new Thread(new ThreadStart(TryToRecieve)).Start();
            }
            catch
            {
                ConnectionLostRaise();
                return false;
            }
            return true;
        }

        public bool ConnectLobby(out long pingMs)
        {
            pingMs = 0;
            try
            {
                clientLobby = new TcpClient();
                clientLobby.Connect(ServerIp, ServerPort - 1);
                lobbyInfoStream = clientLobby.GetStream();
                new Thread(new ThreadStart(TryToRecieveLobbyInfo)).Start();
                Ping ping = new Ping();
                pingMs = ping.Send(ServerIp).RoundtripTime;
            }
            catch
            {
                return false;
            }
            return true;
        }


        public void Send(string[] args)
        {
            string data = "";
            if (!client.Connected)
            {
                return;
            }
            foreach (string arg in args)
            {
                data += arg + argSplitter.ToString();
            }
            data = data.Substring(0, data.Length - 1);
            try
            {
                byte[] b = Encoding.UTF8.GetBytes(data + packetSplitter.ToString());
                stream.Write(b, 0, b.Length);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                ConnectionLostRaise();
            }
        }

        private void TryToRecieveLobbyInfo()
        {
            PacketManager packetManager = new PacketManager();
            while (true)
            {
                if (lobbyInfoStream == null)
                {
                    return;
                }

                try
                {
                    byte[] b = new byte[1024 * 8];
                    int i = lobbyInfoStream.Read(b, 0, b.Length);
                    string[] packets = packetManager.AddStream(b, i, packetSplitter);
                    string[][] packetsArgs = new string[packets.Length][];
                    for (int j = 0; j < packetsArgs.Length; j++)
                    {
                        packetsArgs[j] = packets[j].Split(argSplitter);
                    }
                    OnReceivedEvent?.Invoke(packetsArgs, ServerIp, ServerPort);
                }
                catch (Exception)
                {
                    break;
                }
            }
        }


        private void TryToRecieve()
        {
            PacketManager packetManager = new PacketManager();
            while (isConnected)
            {
                if (stream == null)
                {
                    return;
                }

                try
                {
                    byte[] b = new byte[1024 * 8];
                    int i = stream.Read(b, 0, b.Length);
                    string[] packets = packetManager.AddStream(b, i, packetSplitter);
                    string[][] packetsArgs = new string[packets.Length][];
                    for (int j = 0; j < packetsArgs.Length; j++)
                    {
                        packetsArgs[j] = packets[j].Split(argSplitter);
                    }
                    OnReceivedEvent?.Invoke(packetsArgs, ServerIp, ServerPort);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    ConnectionLostRaise();
                    break;
                }
            }
        }

        public void Refresh()
        {
            if (client == null) // Returns if connected has not been called.
            {
                isConnected = false;
                return;
            }
            try
            {
                client.GetStream(); // Check if stream is open.
            }
            catch
            {
                isConnected = false;
                return;
            }
            if (!client.Connected) // Check if client is connected.
            {
                isConnected = false;
                return;
            }
            isConnected = true;
        }

        private void ConnectionLostRaise()
        {
            Console.WriteLine("The server closed the connection");
            isConnected = false;
            OnConnectionLostEvent?.Invoke(null, ServerPort);
        }

        public int GetPort()
        {
            return ((IPEndPoint)client.Client.LocalEndPoint).Port;
        }

        public string GetAdress()
        {
            return ((IPEndPoint)client.Client.LocalEndPoint).Address.ToString();
        }

        public void Disconnect()
        {
            Refresh();
            if (!isConnected) return;

            try
            {
                stream.Close();
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
