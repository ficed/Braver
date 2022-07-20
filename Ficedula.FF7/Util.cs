using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ficedula.FF7 {
    public class FFException : Exception {
        public FFException(string msg) : base(msg) { }
    }

    public static class Util {
        public static T? ValueOrNull<T>(T value, T nullValue) where T : struct {
            return value.Equals(nullValue) ? null : value;
        }

        public static uint BSwap(uint i) {
            return (i & 0xff00ff00) | ((i & 0xff) << 16) | ((i >> 16) & 0xff);
        }

    }
}
