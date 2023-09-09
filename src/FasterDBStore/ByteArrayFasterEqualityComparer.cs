using FASTER.core;
using Neo.Cryptography;
using System.Linq;

namespace Neo.Plugins.Storage
{
    public class ByteArrayFasterEqualityComparer : IFasterEqualityComparer<byte[]>
    {
        public long GetHashCode64(ref byte[] key)
        {
            var hash256 = key.Sha256();
            long res = 0;
            foreach (byte bt in hash256)
                res = res * 31 * 31 * bt + 17;
            return res;
        }

        public bool Equals(ref byte[] right, ref byte[] left)
        {
            if (ReferenceEquals(right, left) == true) return true;
            return right.SequenceEqual(left);
        }
    }
}
