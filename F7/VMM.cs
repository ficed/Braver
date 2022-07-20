using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace F7 {
    public class VMM {

        private byte[][] _banks;
        private byte[] _scratch;

        public VMM() {
            _banks = Enumerable.Range(0, 5)
                .Select(_ => new byte[256])
                .ToArray();
            _scratch = new byte[256];
        }

        public void ResetScratch() {
            _scratch = new byte[256];
        }

        public void Write(int bank, int offset, byte value) {
            switch (bank) {
                case 0:
                    throw new F7Exception("Can't write to literal bank 0");
                case 1:
                    _banks[0][offset] = value;
                    break;
                case 2:
                    _banks[0][offset * 2] = value;
                    break;
                case 3:
                    _banks[1][offset] = value;
                    break;
                case 4:
                    _banks[1][offset * 2] = value;
                    break;
                case 0xB:
                    _banks[2][offset] = value;
                    break;
                case 0xC:
                    _banks[2][offset * 2] = value;
                    break;
                case 0xD:
                    _banks[3][offset] = value;
                    break;
                case 0xE:
                    _banks[3][offset * 2] = value;
                    break;
                case 0xF:
                    _banks[4][offset] = value;
                    break;
                case 7:
                    _banks[4][offset * 2] = value;
                    break;

                case 5:
                    _scratch[offset] = value;
                    break;
                case 6:
                    _scratch[offset * 2] = value;
                    break;

                default:
                    throw new F7Exception($"Unknown memory bank {bank}/{offset}");
            }
        }

        public int Read(int bank, int offset) {
            switch (bank) {
                case 0:
                    return offset;
                case 1:
                    return _banks[0][offset];
                case 2:
                    return _banks[0][offset * 2] | (_banks[0][offset * 2 + 1] << 8);
                case 3:
                    return _banks[1][offset];
                case 4:
                    return _banks[1][offset * 2] | (_banks[1][offset * 2 + 1] << 8);
                case 0xB:
                    return _banks[2][offset];
                case 0xC:
                    return _banks[2][offset * 2] | (_banks[2][offset * 2 + 1] << 8);
                case 0xD:
                    return _banks[3][offset];
                case 0xE:
                    return _banks[3][offset * 2] | (_banks[3][offset * 2 + 1] << 8);
                case 0xF:
                    return _banks[4][offset];
                case 7:
                    return _banks[4][offset * 2] | (_banks[4][offset * 2 + 1] << 8);

                case 5:
                    return _scratch[offset];
                case 6:
                    return _scratch[offset * 2] | (_scratch[offset * 2 + 1] << 8);

                default:
                    throw new F7Exception($"Unknown memory bank {bank}/{offset}");
            }
        }
    }
}
