using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;

namespace Listener
{
    public class StateObject
    {
        public int BufferSize => 1024;

        public byte[] Buffer
        {
            get;
            set;
        }

        public List<byte> Cache
        {
            get;
            set;
        } = new List<byte>();

        public List<byte> SendData
        {
            get;
            set;
        } = new List<byte>();

        public int TotalReceivedBytes
        {
            get;
            set;
        }

        public int TotalSentBytes
        {
            get;
            set;
        }

        public Socket Socket
        {
            get;
        }

        public StateObject(Socket socket)
        {
            Socket = socket;
            Buffer = new byte[BufferSize];
        }

        public void CopyTo(StateObject state)
        {
            Buffer.CopyTo(state.Buffer, 0);
            state.Cache = new List<byte>(Cache);
            state.SendData = new List<byte>(SendData);
            state.TotalReceivedBytes = TotalReceivedBytes;
            state.TotalSentBytes = TotalSentBytes;
        }
    }
}
