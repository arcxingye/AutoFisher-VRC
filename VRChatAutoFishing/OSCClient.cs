using System;
using System.Net;
using System.Net.Sockets;

namespace VRChatAutoFishing
{
    public class OSCClient : IDisposable
    {
        private readonly UdpClient _udpClient;
        private readonly IPEndPoint _endPoint;

        public OSCClient(string ipAddress, int port)
        {
            _udpClient = new UdpClient();
            _endPoint = new IPEndPoint(IPAddress.Parse(ipAddress), port);
        }

        public void SendUseRight(int value)
        {
            try
            {
                // 简单的OSC消息格式：地址模式 + 类型标签 + 数据
                // /input/UseRight ,i [value]
                byte[] message = CreateOSCMessage("/input/UseRight", value);
                _udpClient.Send(message, message.Length, _endPoint);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"OSC发送错误: {ex.Message}");
            }
        }

        private byte[] CreateOSCMessage(string address, int value)
        {
            using (var stream = new System.IO.MemoryStream())
            {
                // 地址模式
                WriteOSCString(stream, address);

                // 类型标签
                WriteOSCString(stream, ",i");

                // 整数值 (大端序)
                byte[] intBytes = BitConverter.GetBytes(value);
                if (BitConverter.IsLittleEndian)
                    Array.Reverse(intBytes);
                stream.Write(intBytes, 0, 4);

                return stream.ToArray();
            }
        }

        private void WriteOSCString(System.IO.Stream stream, string str)
        {
            byte[] strBytes = System.Text.Encoding.ASCII.GetBytes(str);
            stream.Write(strBytes, 0, strBytes.Length);

            // 填充到4字节边界
            int padding = (4 - (strBytes.Length % 4)) % 4;
            for (int i = 0; i < padding; i++)
            {
                stream.WriteByte(0);
            }
        }

        public void Dispose()
        {
            _udpClient?.Dispose();
        }
    }
}