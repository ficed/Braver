﻿using LiteNetLib;
using LiteNetLib.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Braver.Net {
    public class Client : Net {
        public override string Status => (_client.FirstPeer?.ConnectionState ?? ConnectionState.Disconnected).ToString();

        private NetManager _client;
        private FGame _game;

        public Client(FGame game, string host, int port, string key) {
            _game = game;
            EventBasedNetListener listener = new EventBasedNetListener();
            _client = new NetManager(listener);
            _client.Start();
            _client.Connect(host, port, key);
            listener.NetworkReceiveEvent += Listener_NetworkReceiveEvent;
            new ClientAudio(game, this);
        }

        private void Listener_NetworkReceiveEvent(NetPeer peer, NetPacketReader reader, DeliveryMethod deliveryMethod) {
            var type = (MessageType)reader.GetInt();
            var message = GetMessage(type);
            message.Load(reader);
            Dispatch(message);
            reader.Recycle();
        }

        public override void Send(NetMessage message) {
            if (message is ClientMessage) {
                var writer = new NetDataWriter();
                writer.Put((int)GetMessageType(message));
                message.Save(writer);
                _client.SendToAll(writer, DeliveryMethod.ReliableOrdered);
            }
        }

        public override void Shutdown() {
            _client.Stop();
        }

        public override void Update() {
            _client.PollEvents();
        }
    }

    public class ClientAudio : IListen<SfxMessage>, IListen<MusicMessage> {

        private FGame _game;
        public ClientAudio(FGame game, Net net) {
            _game = game;
            net.Listen<SfxMessage>(this);
            net.Listen<MusicMessage>(this);
        }

        public void Received(SfxMessage message) {
            _game.Audio.PlaySfx(message.Which, message.Volume, message.Pan);
        }

        public void Received(MusicMessage message) {
            if (string.IsNullOrEmpty(message.Track))
                _game.Audio.StopMusic();
            else
                _game.Audio.PlayMusic(message.Track);
        }
    }
}