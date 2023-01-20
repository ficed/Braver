using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Braver {

    public static class Utils {
        public static IEnumerable<int> IndicesOfSetBits(int value) {
            foreach(int i in Enumerable.Range(0, 32))
                if ((value & (1 << i)) != 0)
                    yield return i;
        }

        public static void Swap<T>(ref T t1, ref T t2) {
            T t = t1;
            t1 = t2;
            t2 = t;
        }
    }

    public class F7Exception : Exception {
        public F7Exception(string msg) : base(msg) { }
    }

    public static class Serialisation {
        public static void Serialise(object o, Stream s) {
            new System.Xml.Serialization.XmlSerializer(o.GetType()).Serialize(s, o);
        }
        public static string SerialiseString(object o) {
            var sw = new StringWriter();
            new System.Xml.Serialization.XmlSerializer(o.GetType()).Serialize(sw, o);
            return sw.ToString();
        }

        public static T Deserialise<T>(Stream s) {
            return (T)(new System.Xml.Serialization.XmlSerializer(typeof(T)).Deserialize(s));
        }
        public static T Deserialise<T>(string s) {
            return (T)(new System.Xml.Serialization.XmlSerializer(typeof(T)).Deserialize(new StringReader(s)));
        }
    }
}
