// This program and the accompanying materials are made available under the terms of the
//  Eclipse Public License v2.0 which accompanies this distribution, and is available at
//  https://www.eclipse.org/legal/epl-v20.html
//  
//  SPDX-License-Identifier: EPL-2.0

namespace Ficedula {

    public static class Utils {
        public static IEnumerable<int> IndicesOfSetBits(int value) {
            foreach (int i in Enumerable.Range(0, 32))
                if ((value & (1 << i)) != 0)
                    yield return i;
        }

        public static void Swap<T>(ref T t1, ref T t2) {
            T t = t1;
            t1 = t2;
            t2 = t;
        }

        public static T? MapToNull<T>(T value, T nullPlaceholder) where T : struct {
            return value.Equals(nullPlaceholder) ? null : value;
        }

        public static T? ValueOrNull<T>(T value, T nullValue) where T : struct {
            return value.Equals(nullValue) ? null : value;
        }

        public static uint BSwap(uint i) {
            return (i & 0xff00ff00) | ((i & 0xff) << 16) | ((i >> 16) & 0xff);
        }

        public static int IndexOf<T>(this IReadOnlyList<T> list, T value) where T : class {
            foreach (int i in Enumerable.Range(0, list.Count))
                if (list[i] == value)
                    return i;
            return -1;
        }

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