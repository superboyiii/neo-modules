using FASTER.core;
using System;

namespace Neo.Plugins.Storage
{
    public class ByteArrayBinaryObjectSerializer : BinaryObjectSerializer<byte[]>
    {
        public override void Deserialize(out byte[] value)
        {
            var bytesr = new byte[4];
            reader.Read(bytesr, 0, 4);
            var size = BitConverter.ToInt32(bytesr);
            value = reader.ReadBytes(size);
        }

        public override void Serialize(ref byte[] value)
        {
            var len = BitConverter.GetBytes(value.Length);
            writer.Write(len);
            writer.Write(value);
        }
    }
}
