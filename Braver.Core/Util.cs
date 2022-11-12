using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Braver {


    public class F7Exception : Exception {
        public F7Exception(string msg) : base(msg) { }
    }

    public static class Serialisation {
        public static void Serialise(object o, System.IO.Stream s) {
            new System.Xml.Serialization.XmlSerializer(o.GetType()).Serialize(s, o);
        }

        public static T Deserialise<T>(System.IO.Stream s) {
            return (T)(new System.Xml.Serialization.XmlSerializer(typeof(T)).Deserialize(s));
        }
        public static T Deserialise<T>(string s) {
            return (T)(new System.Xml.Serialization.XmlSerializer(typeof(T)).Deserialize(new System.IO.StringReader(s)));
        }
    }
}
