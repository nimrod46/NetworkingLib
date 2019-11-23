using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NetworkingLib
{
    internal class PacketManager
    {
        public PacketManager()
        {

        }

        private readonly List<byte> buffer = new List<byte>();
        public string[] AddStream(byte[] fullData, int count, char packetChar)
        {
            byte[] data = new byte[count];
            Array.Copy(fullData, data, count);
            byte packetByte = Encoding.UTF8.GetBytes(packetChar.ToString())[0];
            byte b = 0;
            int lastPacketCount = count;
            while (b != packetByte)
            {
                if (lastPacketCount == 0)
                {
                    Console.Error.WriteLine("An Invalid packet has been received");
                    Print(data.Cast<object>().ToArray());
                    Console.WriteLine(Encoding.UTF8.GetString(data, 0, data.Length));
                }
                b = data[lastPacketCount - 1];
                lastPacketCount--;
            }
            lastPacketCount++;
            byte[] completeStream = new byte[lastPacketCount + buffer.Count];
            if (buffer.Count != 0)
            {
                Array.Copy(buffer.ToArray(), completeStream, buffer.Count);
            }
            Array.Copy(data, 0, completeStream, buffer.Count, lastPacketCount);
            buffer.Clear();
            byte[] incompleteStream = new byte[count - lastPacketCount];
            Array.Copy(data, lastPacketCount, incompleteStream, 0, count - lastPacketCount);
            buffer.AddRange(incompleteStream);
            List<string> stream = Encoding.UTF8.GetString(completeStream, 0, completeStream.Length).Split(packetChar).ToList();
            stream.RemoveAt(stream.Count - 1);
            return stream.ToArray();
        }

        internal static void Print(object[] s)
        {
            foreach (object item in s)
            {
                Console.Write("!" + item.ToString() + "!");
            }
            Console.WriteLine();
        }
    }
}

