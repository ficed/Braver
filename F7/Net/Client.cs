using LiteNetLib;
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
            new ClientScreens(game, this);
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

    public class ClientScreens : IListen<FieldScreenMessage>, IListen<UIScreenMessage>, 
        IListen<PopScreenMessage>, IListen<TransitionMessage> {
        private FGame _game;

        public ClientScreens(FGame game, Net net) {
            _game = game;
            net.Listen<FieldScreenMessage>(this);
            net.Listen<UIScreenMessage>(this);
            net.Listen<PopScreenMessage>(this);
            net.Listen<TransitionMessage>(this);
        }

        public void Received(FieldScreenMessage message) {
            _game.PushScreen(new Field.FieldScreen(message.Destination));
        }

        public void Received(UIScreenMessage message) {
            _game.PushScreen(new UI.Layout.ClientScreen());
        }

        public void Received(PopScreenMessage message) {
            if (!(_game.Screen is UI.Splash))
                _game.PopScreen(_game.Screen);
        }

        public void Received(TransitionMessage message) {
            switch (message.Kind) {
                case TransitionKind.FadeIn:
                    _game.Screen.FadeIn(null);
                    break;
                case TransitionKind.FadeOut:
                    _game.Screen.FadeOut(null);
                    break;
                default:
                    throw new NotImplementedException();
            }
        }
    }


    public class ClientAudio : IListen<SfxMessage>, IListen<MusicMessage>, IListen<MusicVolumeMessage>,
        IListen<SfxChannelMessage> {

        private FGame _game;
        public ClientAudio(FGame game, Net net) {
            _game = game;
            net.Listen<SfxMessage>(this);
            net.Listen<MusicMessage>(this);
            net.Listen<MusicVolumeMessage>(this);
            net.Listen<SfxChannelMessage>(this);
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

        public void Received(MusicVolumeMessage message) {
            _game.Audio.SetMusicVolume(message.VolumeFrom, message.VolumeTo, message.Duration);
        }

        public void Received(SfxChannelMessage message) {
            if (message.StopLoops)
                _game.Audio.StopLoopingSfx(message.StopChannelLoops);
            else if (message.DoStop)
                _game.Audio.StopChannel(message.Channel);
            else
                _game.Audio.ChannelProperty(message.Channel, message.Pan, message.Volume);
        }
    }
}
