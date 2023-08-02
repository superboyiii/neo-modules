using Neo.IO;
using Neo.Network.P2P.Payloads;
using Neo.SmartContract;
using Neo.VM;
using Neo.VM.Types;

namespace Neo.Plugins
{
    public class NotifyLogManifest : ISerializable
    {
        #region Manifest

        public UInt256 TransactionHash { get; set; } = new();
        public UInt160 ScriptHash { get; set; } = new();
        public string EventName { get; set; } = string.Empty;
        public StackItem[] State { get; set; } = System.Array.Empty<StackItem>();

        #endregion

        #region Static Methods

        public static NotifyLogManifest Create(NotifyEventArgs notifyArgs) =>
            new()
            {
                TransactionHash = ((Transaction)notifyArgs.ScriptContainer).Hash,
                ScriptHash = notifyArgs.ScriptHash,
                EventName = notifyArgs.EventName,
                State = notifyArgs.State.ToArray(),
            };

        #endregion

        #region ISerializable

        public int Size =>
            TransactionHash.Size +
            ScriptHash.Size +
            EventName.GetVarSize() +
            sizeof(ushort) +                // Length of StackItems Array
            sizeof(int) * State.Length +   // Length of each StackItem Byte Array
            CalculateStateSize();

        public void Deserialize(ref MemoryReader reader)
        {
            TransactionHash.Deserialize(ref reader);
            ScriptHash.Deserialize(ref reader);
            EventName = reader.ReadVarString();

            ushort arraylen = reader.ReadUInt16();
            State = new StackItem[arraylen];
            for (int i = 0; i < State.Length; i++)
            {
                int dataSize = reader.ReadInt32();
                State[i] = BinarySerializer.Deserialize(reader.ReadMemory(dataSize), ExecutionEngineLimits.Default with { MaxItemSize = 1024 * 1024 });
            }
        }

        public void Serialize(BinaryWriter writer)
        {
            writer.Write(TransactionHash);
            writer.Write(ScriptHash);
            writer.WriteVarString(EventName);
            writer.Write(checked((ushort)State.Length));
            for (int i = 0; i < State.Length; i++)
            {
                var data = State[i] is InteropInterface ?
                    BinarySerializer.Serialize(StackItem.Null, 1024 * 1024) :
                    BinarySerializer.Serialize(State[i], 1024 * 1024);
                writer.Write(data.Length);
                writer.Write(data);
            }
        }

        private int CalculateStateSize()
        {
            int size = 0;
            foreach (StackItem item in State)
                size += item is InteropInterface ?
                    BinarySerializer.Serialize(StackItem.Null, 1024 * 1024).Length :
                    BinarySerializer.Serialize(item, 1024 * 1024).Length;
            return size;
        }

        #endregion
    }
}
