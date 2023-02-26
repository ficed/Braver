using LiteNetLib;
using LiteNetLib.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Braver.Net {
    public class Server : Net {

        private NetManager _server;

        public Server() {
            EventBasedNetListener listener = new EventBasedNetListener();
            _server = new NetManager(listener);
            _server.Start(12508);

            listener.ConnectionRequestEvent += request => {
                if (_server.ConnectedPeersCount < 10 /* max connections */)
                    request.AcceptIfKey("ficedula");
                else
                    request.Reject();
            };
        }

        public override void Send(NetMessage message) {
            if (message is ServerMessage) {
                var writer = new NetDataWriter();
                writer.Put((int)GetMessageType(message));
                message.Save(writer);
                //System.Diagnostics.Trace.WriteLine($"Sending message {message.GetType()}");
                _server.SendToAll(writer, DeliveryMethod.ReliableOrdered);
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
