using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System;

namespace Neo.Plugins.Storage
{
    internal class ByteArrayComparer : IComparer<byte[]>
    {
        public static readonly ByteArrayComparer Default = new(1);
        public static readonly ByteArrayComparer Reverse = new(-1);

        private readonly int _direction;

        private ByteArrayComparer(int direction)
        {
            _direction = direction;
        }

        public int Compare(byte[] x, byte[] y)
        {
            return _direction > 0
                ? CompareInternal(x, y)
                : -CompareInternal(x, y);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int CompareInternal(byte[] x, byte[] y)
        {
            int length = Math.Min(x.Length, y.Length);
            for (int i = 0; i < length; i++)
            {
                int r = x[i].CompareTo(y[i]);
                if (r != 0) return r;
            }
            return x.Length.CompareTo(y.Length);
        }
    }
}
