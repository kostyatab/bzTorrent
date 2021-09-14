using System;
using System.Collections.Generic;
using System.Net.Sockets;
using bzBencode;
using System.Net.Torrent.Helpers;
using System.Text;

namespace System.Net.Torrent
{
    public class DHTNode
    {
        private readonly Socket _socket;
        private readonly string _nodeId;
        private IPEndPoint _endPoint;

        public DHTNode(string nodeId)
        {
            _socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            _nodeId = nodeId;

            if(_nodeId.Length != 20)
            {
                throw new Exception("nodeId must be exactly 20 characters");
            }
        }

        public void Connect(IPAddress address, ushort port)
        {
            _endPoint = new IPEndPoint(address, port);
        }

        public void Connect(string address, ushort port)
        {
            if(!IPAddress.TryParse(address, out IPAddress ipAddress))
            {
                var host = Dns.GetHostEntry(address);
                if(host.AddressList.Length == 0)
                {
                    //throw exception
                }

                ipAddress = host.AddressList[0];
            }
            
            _endPoint = new IPEndPoint(ipAddress, port);
        }

        public void Ping()
        {
            var tid = GenerateTID();

            var args = new BDict();
            args.Add("id", new BString(_nodeId));

            var query = CreateQuery(tid, "ping", args);
            var encoded = BencodingUtils.EncodeString(query);
            var bytes = Encoding.GetEncoding(1252).GetBytes(encoded);

            _socket.SendTo(bytes, _endPoint);

            var fullBuffer = new byte[0];

            var buf = new byte[1024];
            int recvd;
            do
            {
                recvd = _socket.Receive(buf, 1024, SocketFlags.None);
                fullBuffer = fullBuffer.Cat(buf);

            } while (recvd == 1024);

            var recvDict = (BDict)BencodingUtils.Decode(fullBuffer);
        }

        public void GetPeers(string hash, Action<IPEndPoint[]> callback)
        {
            var tid = GenerateTID();

            var encHash = PackHelper.Hex(hash, PackHelper.Endianness.Big);
            var encHashStr = Encoding.GetEncoding(1252).GetString(encHash, 0, 20);
            var args = new BDict();
            args.Add("id", new BString(_nodeId));
            args.Add("info_hash", new BString(encHashStr));

            var query = CreateQuery(tid, "get_peers", args);
            var encoded = BencodingUtils.EncodeString(query);
            var bytes = Encoding.GetEncoding(1252).GetBytes(encoded);

            _socket.SendTo(bytes, _endPoint);

            var fullBuffer = new byte[0];

            var buf = new byte[1024];
            int recvd;
            do
            {
                recvd = _socket.Receive(buf, 1024, SocketFlags.None);
                fullBuffer = fullBuffer.Cat(buf);

            } while (recvd == 1024);

            var recvDict = (BDict)BencodingUtils.Decode(fullBuffer);
            if(recvDict.ContainsKey("r") == false)
            {
                //error
            }

            var responseDict = (BDict)recvDict["r"];
            if(responseDict.ContainsKey("nodes") == false)
            {
                //error
            }

            var nodesBStr = (BString)responseDict["nodes"];
            var nodesBytes = nodesBStr.ByteValue;

			var ipAddresses = new List<IPEndPoint>();
            for (var i = 0; i < nodesBytes.Length / 6; i++)
            {
                var ip = UnpackHelper.UInt32(nodesBytes, i * 6, UnpackHelper.Endianness.Little);
                var port = UnpackHelper.UInt16(nodesBytes, (i * 6) + 4, UnpackHelper.Endianness.Big);

                var ipAddr = new IPEndPoint(ip, port);
				ipAddresses.Add(ipAddr);
			}

			callback?.Invoke(ipAddresses.ToArray());
        }

        private BDict CreateQuery(string tid, string type, BDict arguments)
        {
            var query = new BDict
            {
                { "t", new BString(tid) },
                { "y", new BString("q") },
                { "q", new BString(type) },
                { "a", arguments }
            };

            return query;
        }

        private string GenerateTID()
        {
            var rnd = new Random();
            var c1 = (char)rnd.Next(65, 90);
            var c2 = (char)rnd.Next(65, 90);

            return string.Format("{0}{1}", c1, c2);
        }
    }
}
