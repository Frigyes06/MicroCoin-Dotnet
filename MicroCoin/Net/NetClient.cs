﻿//-----------------------------------------------------------------------
// This file is part of MicroCoin - The first hungarian cryptocurrency
// Copyright (c) 2019 Peter Nemeth
// NetClient.cs - Copyright (c) 2019 Németh Péter
//-----------------------------------------------------------------------
// MicroCoin is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// MicroCoin is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the 
// GNU General Public License for more details.
//-----------------------------------------------------------------------
// You should have received a copy of the GNU General Public License
// along with MicroCoin. If not, see <http://www.gnu.org/licenses/>.
//-----------------------------------------------------------------------
using MicroCoin.Protocol;
using Prism.Events;
using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using MicroCoin.Common;
using MicroCoin.Types;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace MicroCoin.Net
{
    public class NetworkEvent : PubSubEvent<NetworkPacket> { }
    public class NewServerConnection : PubSubEvent<Node> { }

    public class NetClient : INetClient
    {
        private readonly object clientLock = new object();
        private readonly IEventAggregator eventAggregator;
        private readonly ILogger<NetClient> logger;

        protected TcpClient TcpClient { get; set; } = new TcpClient();
        private Thread Thread { get; set; }
        private Node Node { get; set; }
        public bool IsConnected { get => TcpClient == null ? false : TcpClient.Connected; }
        public bool Started { get; set; }

        public NetClient(IEventAggregator eventAggregator, ILogger<NetClient> logger)
        {
            this.eventAggregator = eventAggregator;
            this.logger = logger;
        }
        public bool Connect(Node node, int timeout = 500)
        {
            lock (clientLock)
            {
                if (IsConnected) return IsConnected;
                Node = node;
                var old = TcpClient.ReceiveTimeout;
                TcpClient.ReceiveTimeout = 500;
                var asyncResult = TcpClient.BeginConnect(node.IP, node.Port, null, null);
                asyncResult.AsyncWaitHandle.WaitOne(TimeSpan.FromMilliseconds(timeout));
                try
                {
                    TcpClient.EndConnect(asyncResult);
                }
                catch (Exception)
                {
                    return false;
                }
                TcpClient.ReceiveTimeout = old;
                Node.NetClient = this;
                return IsConnected;
            }
        }
        public void Start()
        {
            lock (clientLock)
            {
                if (!IsConnected) throw new InvalidOperationException("Not connected");
                Started = true;
                Thread = new Thread(HandleClient);
                Thread.Start();
            }
            eventAggregator.GetEvent<NewServerConnection>().Publish(Node);
        }

        public async Task<NetworkPacket> SendAndWaitAsync(NetworkPacket packet)
        {
            lock (clientLock)
            {
                if (Started) throw new InvalidOperationException("Client already started");
                if (!IsConnected) throw new InvalidOperationException("Not connected");
            }
            using (var sendStream = new MemoryStream())
            {
                packet.Header.DataLength = packet.RawData.Length;
                packet.Header.SaveToStream(sendStream);
                await sendStream.WriteAsync(packet.RawData, 0, packet.RawData.Length);
                sendStream.Position = 0;
                await sendStream.CopyToAsync(TcpClient.GetStream());
                await TcpClient.GetStream().FlushAsync();
                sendStream.SetLength(0);
                using (BinaryReader br = new BinaryReader(TcpClient.GetStream(), Encoding.ASCII, true))
                {
                    do
                    {
                        var header = new PacketHeader
                        {
                            Magic = br.ReadUInt32()
                        };
                        if (header.Magic == Params.NetworkPacketMagic)
                        {
                            header.RequestType = (RequestType)br.ReadUInt16();
                            header.Operation = (NetOperationType)br.ReadUInt16();
                            header.Error = br.ReadUInt16();
                            header.RequestId = br.ReadUInt32();
                            header.ProtocolVersion = br.ReadUInt16();
                            header.AvailableProtocol = br.ReadUInt16();
                            header.DataLength = br.ReadInt32();
                            if (header.Error > 0)
                            {
                                ByteString message = ByteString.ReadFromStream(br);
                                string error = message;
                                logger.LogError(error);
                                throw new Exception(error);
                            }
                            if (header.RequestType != RequestType.Response || header.Operation != packet.Header.Operation || header.RequestId != packet.Header.RequestId)
                            {
                                br.ReadBytes(header.DataLength); // Drop package
                                continue;
                            }
                            return new NetworkPacket(header)
                            {
                                Node = Node,
                                RawData = br.ReadBytes(header.DataLength)
                            };
                        }
                        throw new InvalidDataException("Invalid magic");
                    } while (true);
                }
            }
        }

        public void Send(NetworkPacket packet)
        {
            lock (clientLock)
            {
                logger.LogDebug("Sending {0} {1} to {2}", packet.Header.Operation, packet.Header.RequestType, Node.EndPoint.ToString());
                using (var sendStream = new MemoryStream())
                {
                    packet.Header.DataLength = packet.RawData.Length;
                    packet.Header.SaveToStream(sendStream);
                    sendStream.Write(packet.RawData, 0, packet.RawData.Length);
                    sendStream.Position = 0;
                    sendStream.CopyTo(TcpClient.GetStream());
                    TcpClient.GetStream().Flush();
                }
            }
        }

        protected void HandleClient()
        {
            try
            {
                var stream = TcpClient.GetStream();
                using (var br = new BinaryReader(stream, Encoding.Default, true))
                {
                    try
                    {
                        while (true)
                        {
                            var header = new PacketHeader
                            {
                                Magic = br.ReadUInt32()
                            };
                            Node.LastConnection = DateTime.Now;
                            if (TcpClient.Available < PacketHeader.Size - sizeof(uint))
                            {
                                break;
                            }
                            if (header.Magic != Params.NetworkPacketMagic)
                            {
                                break;
                            }
                            else
                            {
                                header.RequestType = (RequestType)br.ReadUInt16();
                                header.Operation = (NetOperationType)br.ReadUInt16();
                                header.Error = br.ReadUInt16();
                                header.RequestId = br.ReadUInt32();
                                header.ProtocolVersion = br.ReadUInt16();
                                header.AvailableProtocol = br.ReadUInt16();
                                header.DataLength = br.ReadInt32();

                                if (header.Error > 0)
                                {
                                    ByteString message = ByteString.ReadFromStream(br);
                                    logger.LogError(this.TcpClient.Client.RemoteEndPoint.ToString() + " " + message);
                                    break;
                                }
                                var packet = new NetworkPacket(header)
                                {
                                    Node = Node,
                                    RawData = br.ReadBytes(header.DataLength)
                                };                 
                                eventAggregator.GetEvent<NetworkEvent>().Publish(packet);
                            }
                        }
                    }
                    catch (IOException e)
                    {
                        logger.LogWarning(Node.EndPoint + " " + e.Message);
                        return;
                    }
                }
            }
            finally
            {
                if (TcpClient.Connected)
                {
                    logger.LogInformation("Disconnected from {0}", Node?.EndPoint.ToString());
                    TcpClient.Close();
                }
            }
        }

        public void Dispose()
        {
            if (TcpClient.Connected)
            {
                TcpClient.Close();
            }
            TcpClient.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}