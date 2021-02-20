using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace Listener
{
    class Program
    {
        public static ManualResetEvent _allDone = new ManualResetEvent(false);
        private static Socket _socket;

        static void Main(string[] args)
        {
            var ipAddress = GetLocalIPv4();
            var localEndpoint = new IPEndPoint(ipAddress, 12345);
            _socket = new Socket(ipAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);


            _socket.Bind(localEndpoint);
            _socket.Listen(100);

            Console.WriteLine("Server listening at: {0}", localEndpoint);
            Console.WriteLine("========================================================================================================================");

            var state = new StateObject(_socket);

            do
            {
                _allDone.Reset();

                try
                {
                    _socket.BeginAccept(new AsyncCallback(AcceptCallback), _socket);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.ToString());
                }

                _allDone.WaitOne();
            }
            while (true);
        }

        private static void AcceptCallback(IAsyncResult ar)
        {
            _allDone.Set();

            var listener = (Socket)ar.AsyncState;
            var handler = listener?.EndAccept(ar);

            var state = new StateObject(handler);
            handler?.BeginReceive(state.Buffer, 0, state.BufferSize, 0, new AsyncCallback(ReceiveCallback), state);
        }

        private static void ReceiveCallback(IAsyncResult ar)
        {
            EndPoint endPoint = default(EndPoint);

            try
            {
                var state = (StateObject)ar.AsyncState;
                var handler = state?.Socket;
                endPoint = handler.RemoteEndPoint;

                if (handler == null)
                    return;

                int bytesRead = handler.EndReceive(ar);

                if (bytesRead <= 0)
                    return;

                state.TotalReceivedBytes += bytesRead;
                state.Cache.AddRange(state.Buffer);
                state.SendData.AddRange(state.Buffer);

                if (state.Cache.Skip(state.TotalReceivedBytes - 2).Take(2).SequenceEqual(new byte[]
                {
                    0x0D, 0x0A
                }))
                {
                    Console.WriteLine("Received {0} bytes from {1}:\r\n{2}\r\n", bytesRead, handler.RemoteEndPoint, string.Join(" ", BitConverter.ToString(state.Cache.Take(bytesRead).ToArray()).Split("-").Select(x => "0x" + x)));

                    var stateCopy = new StateObject(handler);
                    state.CopyTo(stateCopy);
                    Send(stateCopy);

                    state = new StateObject(handler);
                    handler?.BeginReceive(state.Buffer, 0, state.BufferSize, 0, new AsyncCallback(ReceiveCallback), state);
                }
                else
                {
                    handler.BeginReceive(state.Buffer, 0, state.BufferSize, 0, new AsyncCallback(ReceiveCallback), state);
                }
            }
            catch (Exception ex)
            {
                if (ex is SocketException)
                {
                    Console.WriteLine("{0} - {1}", endPoint, ex.Message);
                    Console.WriteLine(ex.ToString());
                    Console.WriteLine("========================================================================================================================");
                }
            }
        }

        private static void Send(StateObject state)
        {
            var handler = state.Socket;

            var size = state.BufferSize;
            if (state.TotalReceivedBytes < state.BufferSize)
                size = state.TotalReceivedBytes;

            var bytesToSend = state.SendData.Take(size).ToArray();

            handler.BeginSend(bytesToSend, 0, bytesToSend.Length, 0,
                new AsyncCallback(SendCallback), state);
        }

        private static void SendCallback(IAsyncResult ar)
        {
            try
            {
                StateObject state = (StateObject)ar.AsyncState;
                var socket = state.Socket;
                int bytesSent = socket.EndSend(ar);
                state.TotalSentBytes = bytesSent;

                if (bytesSent > state.BufferSize)
                {
                    var sendCache = state.SendData.Skip(state.TotalSentBytes).Take(state.SendData.Count - state.TotalSentBytes);
                    state.SendData.Clear();
                    state.SendData.AddRange(sendCache);
                }

                if (state.TotalReceivedBytes == state.TotalSentBytes)
                {
                    Console.WriteLine("Sent {0} bytes to client:\r\n{1}", bytesSent, string.Join(" ", BitConverter.ToString(state.Cache.Take(state.TotalReceivedBytes).ToArray()).Split("-").Select(x => "0x" + x)));
                    Console.WriteLine("========================================================================================================================");
                }
                else
                {
                    Send(state);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }

        public static IPAddress GetLocalIPv4()
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    return ip;
                }
            }
            throw new Exception("No network adapters with an IPv4 address in the system!");
        }
    }
}
