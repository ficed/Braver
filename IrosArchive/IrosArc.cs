/*
  This source is subject to the Microsoft Public License. See LICENSE.TXT for details.
  The original developer is Iros <irosff@outlook.com>

  Modified by Ficedula to split out into a separate library not depending on anything else in 7H,
  for read only access to IRO archives.
*/

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace IrosArchive {

    public static class StreamUtil {
        public static long ReadLong(this Stream s) {
            byte[] b = new byte[8];
            s.Read(b, 0, 8);
            return BitConverter.ToInt64(b);
        }
        public static int ReadInt(this Stream s) {
            byte[] b = new byte[4];
            s.Read(b, 0, 4);
            return BitConverter.ToInt32(b);
        }
        public static ushort ReadUShort(this Stream s) {
            byte[] b = new byte[2];
            s.Read(b, 0, 2);
            return BitConverter.ToUInt16(b);
        }
    }

    public class IrosArcException : Exception {
        public IrosArcException(string msg) : base(msg) { }
    }

    [Flags]
    public enum ArchiveFlags {
        None = 0,
        Patch = 0x1,
    }

    [Flags]
    public enum FileFlags {
        None = 0,
        CompressLZS = 0x1,
        CompressLZMA = 0x2,

        COMPRESSION_FLAGS = 0xF,

#if RUDE
        Obfuscate = 0x10000,
#else
        RudeFlags = 0xff0000,
#endif
    }

    public enum CompressType {
        Nothing = 0,
        Everything,
        ByExtension,
        ByContent,
    }

    public class IrosArc : IDisposable {
        public const int SIG = 0x534f5249;
        public const int MAX_VERSION = 0x10002;
        public const int MIN_VERSION = 0x10000;

        internal const int SZ_OK = 0;
        internal const int SZ_ERROR_DATA = 1;
        internal const int SZ_ERROR_MEM = 2;
        internal const int SZ_ERROR_CRC = 3;
        internal const int SZ_ERROR_UNSUPPORTED = 4;
        internal const int SZ_ERROR_PARAM = 5;
        internal const int SZ_ERROR_INPUT_EOF = 6;
        internal const int SZ_ERROR_OUTPUT_EOF = 7;

        private static HashSet<string> _noCompressExt = new HashSet<string>(new[] {
            ".jpg", ".png", ".mp3", ".ogg"
        }, StringComparer.InvariantCultureIgnoreCase);

        private class ArcHeader {

            public int Version { get; set; }
            public ArchiveFlags Flags { get; set; }
            public int Directory { get; set; }

            public void Open(Stream s) {
                if (s.ReadInt() != SIG) throw new IrosArcException("Signature mismatch");
                Version = s.ReadInt();
                Flags = (ArchiveFlags)s.ReadInt();
                Directory = s.ReadInt();
                if (Version < MIN_VERSION) throw new IrosArcException("Invalid header version " + Version.ToString());
                if (Version > MAX_VERSION) throw new IrosArcException("Invalid header version " + Version.ToString());
            }

            public override string ToString() {
                return String.Format("Version: {0}.{1}  Directory at: {2}  Flags: {3}", Version >> 16, Version & 0xffff, Directory, Flags);
            }

        }

        private class DirectoryEntry {
            public string Filename { get; set; }
            public FileFlags Flags { get; set; }
            public long Offset { get; set; }
            public int Length { get; set; }

            public void Open(Stream s, int version) {
                long pos = s.Position;
                ushort len = s.ReadUShort();
                ushort flen = s.ReadUShort();
                byte[] fn = new byte[flen];
                s.Read(fn, 0, flen);
                Filename = System.Text.Encoding.Unicode.GetString(fn);
                Flags = (FileFlags)s.ReadInt();
                if (version < 0x10001)
                    Offset = s.ReadInt();
                else
                    Offset = s.ReadLong();
                Length = s.ReadInt();
                s.Position = pos + len;
            }

            public ushort GetSize() {
                byte[] fndata = System.Text.Encoding.Unicode.GetBytes(Filename);
                ushort len = (ushort)(fndata.Length + 4 + 16);
                return len;
            }

            public override string ToString() {
                return String.Format("File: {0} Offset: {1} Size: {2} Flags: {3}", Filename, Offset, Length, Flags);
            }
        }

        private ArcHeader _header;
        private List<DirectoryEntry> _entries;
        private Dictionary<string, DirectoryEntry> _lookup;
        private HashSet<string> _folderNames;
        private FileStream _data;
        private string _source;

        private class CacheEntry {
            public byte[] Data;
            public DateTime LastAccess;
            public string File;
        }

        private System.Collections.Concurrent.ConcurrentDictionary<long, CacheEntry> _cache = new System.Collections.Concurrent.ConcurrentDictionary<long, CacheEntry>();

        private struct DataRecord {
            public byte[] Data;
            public bool Compressed;
        }
        private static DataRecord GetData(byte[] input, string filename, CompressType compress) {
            if (compress == CompressType.Nothing) {
                return new DataRecord() { Data = input };
            }
            if (compress == CompressType.ByExtension && _noCompressExt.Contains(Path.GetExtension(filename))) {
                return new DataRecord() { Data = input };
            }

            var cdata = new MemoryStream();
            //Lzs.Encode(new MemoryStream(input), cdata);
            byte[] lprops;
            using (var lzma = new SharpCompress.Compressors.LZMA.LzmaStream(new SharpCompress.Compressors.LZMA.LzmaEncoderProperties(), false, cdata)) {
                lzma.Write(input, 0, input.Length);
                lprops = lzma.Properties;
            }
            if (/*compress == CompressType.ByContent &&*/ (cdata.Length + lprops.Length + 8) > (input.Length * 10 / 8)) {
                return new DataRecord() { Data = input };
            }

            byte[] data = new byte[cdata.Length + lprops.Length + 8];
            Array.Copy(BitConverter.GetBytes(input.Length), data, 4);
            Array.Copy(BitConverter.GetBytes(lprops.Length), 0, data, 4, 4);
            Array.Copy(lprops, 0, data, 8, lprops.Length);
            cdata.Position = 0;
            cdata.Read(data, lprops.Length + 8, (int)cdata.Length);
            return new DataRecord() { Data = data, Compressed = true };
        }

        public bool CheckValid() {
            foreach (var entry in _entries) {
                if ((entry.Offset + entry.Length) > _data.Length) return false;
            }
            return true;
        }

        public IrosArc(string filename, bool patchable = false, Action<int, int> progressAction = null) {
            _source = filename;
            var sw = new Stopwatch();
            sw.Start();
            if (patchable)
                _data = new FileStream(filename, FileMode.Open, FileAccess.ReadWrite);
            else
                _data = new FileStream(filename, FileMode.Open, FileAccess.Read);
            _header = new ArcHeader();
            _header.Open(_data);

            int numfiles;
            _data.Position = _header.Directory;
            do {
                numfiles = _data.ReadInt();
                if (numfiles == -1) {
                    _data.Position = _data.ReadLong();
                }
            } while (numfiles < 0);
            _entries = new List<DirectoryEntry>();
            _lookup = new Dictionary<string, DirectoryEntry>(StringComparer.InvariantCultureIgnoreCase);
            _folderNames = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
            for (int i = 0; i < numfiles; i++) {
                progressAction?.Invoke(i, numfiles);
                DirectoryEntry e = new DirectoryEntry();
                e.Open(_data, _header.Version);
#if !RUDE
                if ((e.Flags & FileFlags.RudeFlags) != 0) throw new IrosArcException(String.Format("Archive {0} entry {1} has invalid flags", filename, e.Filename));
#endif

                _entries.Add(e);
                _lookup[e.Filename] = e;
                int lpos = e.Filename.LastIndexOf('\\');
                if (lpos > 0) {
                    _folderNames.Add(e.Filename.Substring(0, lpos));
                }
            }
            sw.Stop();
            Trace.WriteLine($"IrosArc: opened {filename}, contains {_lookup.Count} files, took {sw.ElapsedMilliseconds} ms to parse");
        }

        public IEnumerable<string> AllFileNames() {
            return _lookup.Keys;
        }

        public IEnumerable<string> AllFolderNames() {
            return _folderNames.Select(s => s);
        }

        public bool HasFolder(string name) {
            bool result = _folderNames.Contains(name);
            if (result) {
                Trace.WriteLine($"ARCHIVE: {_source} contains folder {name}");
            }

            return result;
        }

        public bool HasFile(string name) {
            bool result = _lookup.ContainsKey(name);
            if (result) {
                Trace.WriteLine($"ARCHIVE: {_source} contains file {name}");
            }

            return result;
        }

        public int GetFileSize(string name) {
            DirectoryEntry e;
            if (_lookup.TryGetValue(name, out e)) {
                switch (e.Flags & FileFlags.COMPRESSION_FLAGS) {
                    case FileFlags.CompressLZMA:
                        _data.Position = e.Offset;
                        return _data.ReadInt();
                    default:
                    case FileFlags.None:
                        return e.Length;
                }
            } else
                return -1;
        }

        private void CleanCache() {
            long[] remove = _cache
                .ToArray()
                .Where(kv => kv.Value.LastAccess < DateTime.Now.AddSeconds(-60))
                .Select(kv => kv.Key)
                .ToArray();
            if (remove.Any()) {
                Trace.WriteLine($"Removing {remove.Length} compressed files from cache: ");
                CacheEntry _;
                foreach (long r in remove) _cache.TryRemove(r, out _);
            }
        }

        //private int _cacheCounter = 0;

        private CacheEntry GetCache(DirectoryEntry e) {
            CacheEntry ce;
            if (!_cache.TryGetValue(e.Offset, out ce)) {
                ce = new CacheEntry() { File = e.Filename };
                byte[] data;
                lock (_data) {
                    switch (e.Flags & FileFlags.COMPRESSION_FLAGS) {
                        case FileFlags.CompressLZS:
                            data = new byte[e.Length];
                            _data.Position = e.Offset;
                            _data.Read(data, 0, e.Length);
                            var ms = new MemoryStream(data);
                            var output = new MemoryStream();
                            Lzs.Decode(ms, output);
                            data = new byte[output.Length];
                            output.Position = 0;
                            output.Read(data, 0, data.Length);
                            ce.Data = data;
                            break;
                        case FileFlags.CompressLZMA:
                            _data.Position = e.Offset;
                            int decSize = _data.ReadInt(), propSize = _data.ReadInt();
                            byte[] props = new byte[propSize];
                            _data.Read(props, 0, props.Length);
                            byte[] cdata = new byte[e.Length - propSize - 8];
                            _data.Read(cdata, 0, cdata.Length);
                            data = new byte[decSize];
                            var lzma = new SharpCompress.Compressors.LZMA.LzmaStream(props, new MemoryStream(cdata));
                            lzma.Read(data, 0, data.Length);
                            /*int srcSize = cdata.Length;
                            switch (LzmaUncompress(data, ref decSize, cdata, ref srcSize, props, props.Length)) {
                                case SZ_OK:
                                    //Woohoo!
                                    break;
                                default:
                                    throw new IrosArcException("Error decompressing " + e.Filename);
                            }*/
                            ce.Data = data;
                            break;
                        default:
                            throw new IrosArcException("Bad compression flags " + e.Flags.ToString());
                    }
                }
                _cache.AddOrUpdate(e.Offset, ce, (_, __) => ce);
            }
            ce.LastAccess = DateTime.Now;
            CleanCache();

            /*
            if ((_cacheCounter++ % 100) == 0)
                DebugLogger.WriteLine("IRO cache contents; " + String.Join(",", _cache.Values.Select(e => e.File)));
            */

            return ce;
        }

        public byte[] GetBytes(string name) {
            DirectoryEntry e;
            if (_lookup.TryGetValue(name, out e)) {
                if ((e.Flags & FileFlags.COMPRESSION_FLAGS) != 0)
                    return GetCache(e).Data;
                else {
                    lock (_data) {
                        byte[] data = new byte[e.Length];
                        _data.Position = e.Offset;
                        _data.Read(data, 0, e.Length);
                        return data;
                    }
                }
            } else
                return null;
        }
        public Stream GetData(string name) {
            byte[] data = GetBytes(name);
            return data == null ? null : new MemoryStream(data);
        }

        public void Dispose() {
            if (_data != null) {
                _data.Close();
                _data = null;
            }
        }

        public override string ToString() {
            return "[IrosArchive " + _source + "]";
        }

        public IEnumerable<string> GetInformation() {
            yield return _header.ToString();

            foreach (var entry in _entries)
                yield return entry.ToString();
        }
    }

}
