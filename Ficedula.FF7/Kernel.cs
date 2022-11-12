using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;

namespace Ficedula.FF7 {
    public class Kernel {

        private List<byte[]> _sections = new();

        public IEnumerable<byte[]> Sections => _sections.AsReadOnly();

        public Kernel(Stream source) {
            while (source.Position < source.Length) {
                ushort gzSize = source.ReadU16(), size = source.ReadU16(), fileType = source.ReadU16();
                //TODO - use file type?
                var ms = new MemoryStream();
                byte[] input = new byte[gzSize];
                source.Read(input, 0, gzSize);
                new GZipStream(new MemoryStream(input), CompressionMode.Decompress).CopyTo(ms);
                _sections.Add(ms.ToArray());
            }
        }
    }

    public class KernelText {

        private class Item {
            public bool Highlight { get; set; }
            public byte[] Data { get; set; }
        }


        private List<Item> _items = new();

        public int Count => _items.Count;

        //EA: Name of a Character EB: Name of an Item EC: Number ED: Name of the Target EF: Name of Attack FO: Target's Letter (for enemies) 
        public string Get(int index) {
            return Get(index, out _);
        }
        public string Get(int index, out bool highlight) {
            return Get(index, null, null, null, null, -1, out highlight);
        }
        public string Get(int index, IEnumerable<string> charNames, IEnumerable<string> itemNames,
            IEnumerable<string> targetNames, IEnumerable<string> attackNames, int subIndex, out bool highlight) {
            StringBuilder sb = new();
            var item = _items[index];
            highlight = item.Highlight;
            foreach (byte b in item.Data) {
                if (b == 0xff)
                    break;
                switch (b) {
                    case 0xEA:
                        sb.Append(charNames.ElementAt(subIndex));
                        break;
                    case 0xEB:
                        sb.Append(itemNames.ElementAt(subIndex));
                        break;
                    case 0xEC:
                        sb.Append(subIndex.ToString());
                        break;
                    case 0xED:
                        sb.Append(targetNames.ElementAt(subIndex));
                        break;
                    case 0xEF:
                        sb.Append(attackNames.ElementAt(subIndex));
                        break;
                    case 0xF0:
                        sb.Append('A' + subIndex); //TODO translated!
                        break;
                    default:
                        sb.Append(Text.Convert(new[] { b }, 0));
                        break;
                }
            }
            return sb.ToString();
        }

        public KernelText(byte[] source) : this(new MemoryStream(source)) { }
        public KernelText(Stream source) {
            List<int> offsets = new();
            do {
                offsets.Add(source.ReadU16());
            } while (source.Position < offsets[0]);

            foreach (int offset in offsets) {
                List<byte> output = new List<byte>();
                source.Position = offset;
                byte b;
                do {
                    b = (byte)source.ReadByte();
                    switch (b) {
                        case 0xf9:
                            byte data = (byte)source.ReadByte();
                            int len = (data >> 6) * 2 + 4, distance = data & 0x3f;
                            long pos = source.Position;
                            source.Position = source.Position - 2 - distance - 1;
                            foreach (int _ in Enumerable.Range(0, len))
                                output.Add((byte)source.ReadByte());
                            source.Position = pos;
                            break;
                        case 0xff:
                            break;
                        default:
                            output.Add(b);
                            break;
                    }
                } while (b != 0xff);
                bool highlight = false;
                if (output.ElementAtOrDefault(0) == 0xf8) {
                    highlight = output[1] == 0x02;
                    output.RemoveRange(0, 2);
                }
                _items.Add(new Item {
                    Highlight = highlight,
                    Data = output.ToArray(),
                });
            }
        }
    }
}
