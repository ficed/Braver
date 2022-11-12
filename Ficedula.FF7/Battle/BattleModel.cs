using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Ficedula.FF7.Battle {

    public class BitReader {
        private Stream _source;
        private byte _buffer;
        private int _bits;

        public BitReader(Stream s) {
            _source = s;
        }

        private byte GetBit() {
            if (_bits == 0) {
                _buffer = (byte)_source.ReadByte();
                _bits = 8;
            }
            byte result = (byte)(_buffer >> 7);
            _buffer = (byte)(_buffer << 1);
            _bits--;
            return result;
        }

        public int GetBits(int bits, bool signed) {
            int result = 0;
            for (int i = 0; i < bits; i++)
                result = (result << 1) | GetBit();
            if (signed && (((result >> (bits - 1)) & 1) == 1)) {
                result |= (-1) ^ ((1 << bits) - 1);
            }
            return result;
        }

        public short GetRotation(byte key, ref bool absolute) {
            absolute = false;
            if (GetBit() == 0)
                return 0;
            else {
                int method = GetBits(3, false);
                switch (method) {
                    case 0:
                        return (short)((-1) << key);
                    case 7:
                        int result = GetBits(12 - key, true);
                        result <<= key;
                        absolute = true;
                        return (short)result;
                    default:
                        result = GetBits(method, true);
                        if (result < 0)
                            result -= (1 << (method - 1));
                        else
                            result += (1 << (method - 1));
                        result <<= key;
                        return (short)result;
                }
            }
        }

        public short DeltaRotation(short lastrotation, byte key, ref bool absolute) {
            short delta = GetRotation(key, ref absolute);
            short rot = (short)(lastrotation + delta);
            while (rot < 0) rot += 4096;
            while (rot > 4095) rot -= 4095;
            //if absolute throw
            return rot;
        }

        public short GetOffset() {
            if (GetBit() == 0)
                return (short)GetBits(7, true);
            else
                return (short)GetBits(16, true);
        }
    }

    public struct FrameRotation {
        public short rX, rY, rZ;
    }

    public class Frame {
        public int X, Y, Z;
        public FrameRotation[] Rotations;
    }

    public class Animation {
        public int Bones;
        public Frame[] Frames;
    }

    public class Animations {
        public List<Animation> Anims;

        public Animations(Stream source) {
            int parts = source.ReadI32();
            Anims = new List<Animation>();

            for (int i = 0; i < parts; i++) {
                int hbones = source.ReadI32(), hframes = source.ReadI32(), hsize = source.ReadI32();
                if (hsize < 11) {
                    source.Seek(hsize, System.IO.SeekOrigin.Current);
                    continue;
                }
                long chunkstart = source.Position;
                short fframes = source.ReadI16(), fsize = source.ReadI16();
                byte fkey = (byte)source.ReadByte();
                Animation anim = new Animation() { Bones = hbones, Frames = new Frame[hframes] };
                BitReader br = new BitReader(source);
                Frame frame = new Frame() { X = br.GetBits(16, true), Y = br.GetBits(16, true), Z = br.GetBits(16, true) };
                frame.Rotations = new FrameRotation[hbones];
                for (int ii = 0; ii < hbones; ii++) {
                    frame.Rotations[ii].rX = (short)(br.GetBits(12 - fkey, false) << fkey);
                    frame.Rotations[ii].rY = (short)(br.GetBits(12 - fkey, false) << fkey);
                    frame.Rotations[ii].rZ = (short)(br.GetBits(12 - fkey, false) << fkey);
                }
                anim.Frames[0] = frame;
                bool absolute = false;

                for (int j = 1; j < hframes; j++) {
                    Frame lastframe = frame;
                    frame = new Frame() { Rotations = new FrameRotation[hbones] };
                    frame.X = lastframe.X + br.GetOffset();
                    frame.Y = lastframe.Y + br.GetOffset();
                    frame.Z = lastframe.Z + br.GetOffset();
                    for (int k = 0; k < hbones; k++) {
                        frame.Rotations[k].rX = br.DeltaRotation(lastframe.Rotations[k].rX, fkey, ref absolute);
                        frame.Rotations[k].rY = br.DeltaRotation(lastframe.Rotations[k].rY, fkey, ref absolute);
                        frame.Rotations[k].rZ = br.DeltaRotation(lastframe.Rotations[k].rZ, fkey, ref absolute);
                    }
                    anim.Frames[j] = frame;
                }

                Anims.Add(anim);
                source.Position = chunkstart + hsize;
            }
        }
    }

    public class BBone {
        public float Length;
        public List<BBone> Children;
        public int? PFileIndex;
        public int Index;

        public BBone() {
            Children = new List<BBone>();
        }

        public IEnumerable<BBone> ThisAndDescendants() {
            yield return this;
            foreach (var child in Children)
                foreach (var descendant in child.ThisAndDescendants())
                    yield return descendant;
        }

        public static BBone Decode(Stream skeleton) {
            skeleton.Position = 0xC;
            int numbones = skeleton.ReadI32();
            Dictionary<int, BBone> bones = new Dictionary<int, BBone>();
            var root = new BBone { Index = -1 };
            bones[-1] = root;

            int pfile = 0;

            skeleton.Position = 0x34;
            for (int i = 0; i < numbones; i++) {
                BBone bone = new BBone() { Index = i };
                int parent = skeleton.ReadI32();
                bones[i] = bone;
                bones[parent].Children.Add(bone);

                bone.Length = skeleton.ReadF32();
                if (skeleton.ReadI32() == 1) {
                    bone.PFileIndex = pfile++;
                }
            }

            return root;
        }
    }

}
