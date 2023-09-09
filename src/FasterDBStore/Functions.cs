using FASTER.core;

namespace Neo.Plugins.Storage
{
    public class ByteArrayFunctions : SimpleFunctions<byte[], byte[]>
    {
        public override bool SingleWriter(ref byte[] key, ref byte[] input, ref byte[] src, ref byte[] dst, ref byte[] output, ref UpsertInfo upsertInfo, WriteReason reason) =>
            ConcurrentWriter(ref key, ref input, ref src, ref dst, ref output, ref upsertInfo);

        public override bool ConcurrentWriter(ref byte[] key, ref byte[] input, ref byte[] src, ref byte[] dst, ref byte[] output, ref UpsertInfo upsertInfo)
        {
            output = dst = src;
            return true;
        }

        public override bool InitialUpdater(ref byte[] key, ref byte[] input, ref byte[] value, ref byte[] output, ref RMWInfo rmwInfo)
        {
            output = value = input;
            return true;
        }

        public override bool CopyUpdater(ref byte[] key, ref byte[] input, ref byte[] oldValue, ref byte[] newValue, ref byte[] output, ref RMWInfo rmwInfo)
        {
            output = newValue = input;
            return true;
        }

        public override bool InPlaceUpdater(ref byte[] key, ref byte[] input, ref byte[] value, ref byte[] output, ref RMWInfo rmwInfo)
        {
            output = value = input;
            return true;
        }
    }
}
