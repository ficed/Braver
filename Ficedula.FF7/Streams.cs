using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ficedula.FF7 {
    public static class Streams {

        public static long ReadI64(this Stream s) {
            byte[] buffer = new byte[8];
            s.Read(buffer, 0, 8);
            return BitConverter.ToInt64(buffer, 0);
        }
        public static int ReadI32(this Stream s) {
            byte[] buffer = new byte[4];
            s.Read(buffer, 0, 4);
            return BitConverter.ToInt32(buffer, 0);
        }
        public static uint ReadU32(this Stream s) {
            byte[] buffer = new byte[4];
            s.Read(buffer, 0, 4);
            return BitConverter.ToUInt32(buffer, 0);
        }

        public static void WriteI32(this Stream s, int i) {
            byte[] buffer = BitConverter.GetBytes(i);
            s.Write(buffer, 0, 4);
        }
        public static void WriteI64(this Stream s, long L) {
            byte[] buffer = BitConverter.GetBytes(L);
            s.Write(buffer, 0, 8);
        }

        public static short ReadI16(this Stream s) {
            byte[] buffer = new byte[16];
            s.Read(buffer, 0, 2);
            return BitConverter.ToInt16(buffer, 0);
        }
        public static ushort ReadU16(this Stream s) {
            byte[] buffer = new byte[16];
            s.Read(buffer, 0, 2);
            return BitConverter.ToUInt16(buffer, 0);
        }

        public static byte ReadU8(this Stream s) {
            return (byte)s.ReadByte();
        }

        public static void WriteI16(this Stream s, short i) {
            byte[] buffer = BitConverter.GetBytes(i);
            s.Write(buffer, 0, 2);
        }

        public static string ReadS(this Stream s) {
            byte[] data = new byte[s.ReadI32()];
            s.Read(data, 0, data.Length);
            return Encoding.Unicode.GetString(data);
        }
        public static string ReadAscii(this Stream s, int len) {
            byte[] data = new byte[len];
            s.Read(data, 0, data.Length);
            return Encoding.ASCII.GetString(data).Trim();
        }

        public static byte[] ReadBytes(this Stream s, int count) {
            byte[] data = new byte[count];
            s.Read(data, 0, count);
            return data;
        }

        public static void WriteS(this Stream s, string str) {
            byte[] data = Encoding.Unicode.GetBytes(str);
            s.WriteI32(data.Length);
            s.Write(data, 0, data.Length);
        }

        public static void WriteAscii(this Stream s, string str) {
            byte[] data = Encoding.ASCII.GetBytes(str);
            s.Write(data, 0, data.Length);            
        }

        public static void WriteF(this Stream s, double f) {
            byte[] data = BitConverter.GetBytes(f);
            s.Write(data, 0, 8);
        }

        public static double ReadF64(this Stream s) {
            byte[] data = new byte[8];
            s.Read(data, 0, 8);
            return BitConverter.ToDouble(data, 0);
        }
        public static float ReadF32(this Stream s) {
            byte[] data = new byte[4];
            s.Read(data, 0, 4);
            return BitConverter.ToSingle(data, 0);
        }

        public static void WriteG(this Stream s, Guid g) {
            s.Write(g.ToByteArray(), 0, 16);
        }

        public static Guid ReadG(this Stream s) {
            byte[] data = new byte[16];
            s.Read(data, 0, 16);
            return new Guid(data);
        }

        public static string ReadAllText(this Stream s) {
            using (var sr = new StreamReader(s))
                return sr.ReadToEnd();
        }

        public static IEnumerable<string> ReadAllLines(this Stream s) {
            using (var sr = new StreamReader(s)) {
                string? line;
                while ((line = sr.ReadLine()) != null)
                    yield return line;
            }
        }
    }
}
