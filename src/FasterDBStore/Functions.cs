using FASTER.core;
using System;
using System.Buffers;

namespace Neo.Plugins.Storage
{
    public class ByteArrayFunctions : SimpleFunctions<byte[], byte[]>
    {
        public ByteArrayFunctions() : base() { }
        public ByteArrayFunctions(Func<byte[], byte[], byte[]> merger) : base(merger) { }
    }
}
