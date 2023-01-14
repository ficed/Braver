using Microsoft.VisualBasic;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Braver.Net {

    public enum MessageType {
        Unknown = 0,

        FieldScreen = 100,
        FieldModel = 101,

        SfxMessage = 9001,
        MusicMessage = 9002, 
    }

    public interface IListen<T> where T : NetMessage {
        void Received(T message);
    }

    public abstract class Net {

        private static Dictionary<MessageType, Func<NetMessage>> _getMessage = new();
        private static Dictionary<Type, MessageType> _getType = new();

        private static void Register<T>(MessageType type) where T : NetMessage, new() {
            _getType[typeof(T)] = type;
            _getMessage[type] = () => new T();
        }

        static Net() {
            Register<FieldScreenMessage>(MessageType.FieldScreen);
            Register<FieldModelMessage>(MessageType.FieldModel);
            Register<SfxMessage>(MessageType.SfxMessage);
            Register<MusicMessage>(MessageType.MusicMessage);   
        }

        protected NetMessage GetMessage(MessageType type) => _getMessage[type]();
        protected MessageType GetMessageType(NetMessage message) => _getType[message.GetType()];

        public abstract string Status { get; }

        public abstract void Send(NetMessage message);
        public abstract void Update();
        public abstract void Shutdown();



        private Dictionary<Type, List<(object obj, Action<NetMessage> dispatch)>> _listeners = new();

        public void Listen<T>(IListen<T> listener) where T : NetMessage {
            if (!_listeners.TryGetValue(typeof(T), out var list)) {
                _listeners[typeof(T)] = list = new List<(object obj, Action<NetMessage> dispatch)>();
            }
            Action<NetMessage> dispatch = msg => listener.Received(msg as T);
            list.Add((listener, dispatch));
        }

        public void Unlisten(object listener) {
            foreach (var list in _listeners.Values)
                list.RemoveAll(a => a.obj == listener);
        }

        protected void Dispatch(NetMessage message) {
            if (_listeners.TryGetValue(message.GetType(), out var list)) {
                foreach(var listener in list.ToArray())
                    listener.dispatch(message);
            }
        }
    }

    public static class NetUtil {
        public static void Put(this LiteNetLib.Utils.NetDataWriter writer, Vector3 vector3) {
            writer.Put(vector3.X);
            writer.Put(vector3.Y);
            writer.Put(vector3.Z);
        }

        public static Vector3 GetVec3(this LiteNetLib.Utils.NetDataReader reader) {
            return new Vector3(reader.GetFloat(), reader.GetFloat(), reader.GetFloat());
        }
    }

    public abstract class NetMessage {
        public abstract void Save(LiteNetLib.Utils.NetDataWriter writer);
        public abstract void Load(LiteNetLib.Utils.NetDataReader reader);

        protected int GenerateMask(params bool[] items) {
            int mask = 0;
            foreach (int index in Enumerable.Range(0, items.Length))
                if (items[index])
                    mask |= 1 << index;
            return mask;
        }

        protected IEnumerable<int> SetBits(int mask) {
            int index = 0;
            while(mask != 0) {
                if ((mask & 1) != 0)
                    yield return index;
                mask >>= 1;
                index++;
            }
        }
    }

    public abstract class ServerMessage : NetMessage { }
    public abstract class ClientMessage : NetMessage { }

    public abstract class ChangeScreenMessage : ServerMessage {

        public abstract Screen GetScreen();
    }

}