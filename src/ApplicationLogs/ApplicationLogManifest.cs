using Neo.IO;
using Neo.Ledger;
using Neo.SmartContract;
using Neo.VM.Types;
using Neo.VM;

namespace Neo.Plugins
{
    public class ApplicationLogManifest : ISerializable
    {
        #region Manifest

        public TriggerType Trigger { get; set; } = TriggerType.All;
        public VMState VmState { get; set; } = VMState.NONE;
        public string Exception { get; set; } = string.Empty;
        public long GasConsumed { get; set; } = 0L;
        public StackItem[] Stack { get; set; } = System.Array.Empty<StackItem>();

        #endregion

        #region Static Methods

        public static ApplicationLogManifest Create(Blockchain.ApplicationExecuted appExec) =>
            new()
            {
                Trigger = appExec.Trigger,
                VmState = appExec.VMState,
                Exception = appExec.Exception?.GetBaseException().Message,
                GasConsumed = appExec.GasConsumed,
                Stack = appExec.Stack,
            };

        #endregion

        #region ISerializable

        public int Size =>
            sizeof(byte) +                              // Trigger
            sizeof(byte) +                              // VmState
            Exception.GetVarSize() +
            sizeof(long) +                              // GasConsumed
            sizeof(ushort) +                            // Length Stack Array
            sizeof(int) * Stack.Length +                // Length of each StackItem Byte Array
            CalculateStackSize();

        public void Deserialize(ref MemoryReader reader)
        {
            Trigger = (TriggerType)reader.ReadByte();
            VmState = (VMState)reader.ReadByte();
            Exception = reader.ReadVarString();
            GasConsumed = reader.ReadInt64();

            ushort arrayLen2 = reader.ReadUInt16();
            Stack = new StackItem[arrayLen2];
            for (int i = 0; i < Stack.Length; i++)
            {
                int dataSize = reader.ReadInt32();
                Stack[i] = BinarySerializer.Deserialize(reader.ReadMemory(dataSize), ExecutionEngineLimits.Default with { MaxItemSize = uint.MaxValue / 2 });
            }

        }

        public void Serialize(BinaryWriter writer)
        {
            writer.Write((byte)Trigger);
            writer.Write((byte)VmState);
            writer.WriteVarString(Exception ?? string.Empty);
            writer.Write(GasConsumed);

            writer.Write(checked((ushort)Stack.Length));
            for (int i = 0; i < Stack.Length; i++)
            {
                var data = BinarySerializer.Serialize(Stack[i], uint.MaxValue / 2);
                writer.Write(data.Length);
                writer.Write(data);
            }
        }

        private int CalculateStackSize()
        {
            int size = 0;
            foreach (StackItem item in Stack)
                size += BinarySerializer.Serialize(item, uint.MaxValue / 2).Length;
            return size;
        }

        #endregion
    }
}
