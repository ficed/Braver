// This program and the accompanying materials are made available under the terms of the
//  Eclipse Public License v2.0 which accompanies this distribution, and is available at
//  https://www.eclipse.org/legal/epl-v20.html
//  
//  SPDX-License-Identifier: EPL-2.0

using LiteNetLib;
using LiteNetLib.Utils;
using NAudio.CoreAudioApi;
using NAudio.SoundFont;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Braver.Net {
    public class Server : Net {

        private NetManager _server;
        private NetConfig _config;

        private Dictionary<Guid, NetPeer> _connectedPlayers = new();

        public Server(NetConfig config) {
            EventBasedNetListener listener = new EventBasedNetListener();
            
            _config = config;
            _server = new NetManager(listener);
            _server.Start(12508);

            listener.ConnectionRequestEvent += request => {
                if (_server.ConnectedPeersCount < 10 /* max connections */) {
                    string key = request.Data.GetString();
                    var player = _config.Players.SingleOrDefault(p => p.Key.Equals(key, StringComparison.InvariantCultureIgnoreCase));
                    if (player != null) {
                        var peer = request.Accept();
                        if (_connectedPlayers.TryGetValue(player.ID, out var existing)) {
                            existing.Disconnect();
                        }
                        _connectedPlayers[player.ID] = peer;

                        peer.Send(SaveMessage(new ConnectedMessage { PlayerID = player.ID }), DeliveryMethod.ReliableOrdered);

                        Trace.TraceWarning($"{player.Name} connected");

                        return;
                    }
                }
                request.Reject();
            };

            listener.NetworkReceiveEvent += Listener_NetworkReceiveEvent;
            listener.PeerDisconnectedEvent += Listener_PeerDisconnectedEvent;
        }

        private NetDataWriter SaveMessage(ServerMessage message) {
            var writer = new NetDataWriter();
            writer.Put((int)GetMessageType(message));
            message.Save(writer);
            return writer;
        }

        private void Listener_PeerDisconnectedEvent(NetPeer peer, DisconnectInfo disconnectInfo) {
            var id = _connectedPlayers
                .SingleOrDefault(kv => kv.Value == peer)
                .Key;
            if (id != Guid.Empty) {
                _connectedPlayers.Remove(id);
                var player = _config.Players.Single(p => p.ID == id);
                Trace.TraceWarning($"{player.Name} disconnected");
            }
        }

        private void Listener_NetworkReceiveEvent(NetPeer peer, NetPacketReader reader, byte channel, DeliveryMethod deliveryMethod) {
            var id = _connectedPlayers
                .SingleOrDefault(kv => kv.Value == peer)
                .Key;
            if (id != Guid.Empty) {
                int messageType = reader.GetInt();
                var message = GetMessage((MessageType)messageType);
                message.Load(reader);
                Dispatch(message as ClientMessage, id);
            }
            reader.Recycle();
        }

        public override void Send(NetMessage message) {
            if (message is ServerMessage sMessage) {
                var writer = SaveMessage(sMessage);
                //System.Diagnostics.Trace.WriteLine($"Sending message {message.GetType()}");
                _server.SendToAll(writer, DeliveryMethod.ReliableOrdered);
            }
        }
        public override void SendTo(NetMessage message, Guid playerID) {
            if ((message is ServerMessage sMessage) && _connectedPlayers.TryGetValue(playerID, out var peer)) {
                var writer = SaveMessage(sMessage);
                //System.Diagnostics.Trace.WriteLine($"Sending message {message.GetType()}");
                peer.Send(writer, DeliveryMethod.ReliableOrdered);
            }
        }

        public override void Shutdown() {
            _server.Stop();
        }

        public override void Update() {
            _server.PollEvents();
        }

        public override string Status => $"{_server.ConnectedPeersCount} clients connected";
    }
}
