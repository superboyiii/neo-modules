using Neo;
using Neo.IO;
using Neo.SmartContract;

namespace ApplicationLogs.Store.States
{
    public class NotifyLogState : ISerializable, IEquatable<NotifyLogState>
    {
        public UInt160 ScriptHash { get; set; } = new();
        public string EventName { get; set; } = string.Empty;
        public Guid[] StackItemIds { get; set; } = Array.Empty<Guid>();

        public static NotifyLogState Create(NotifyEventArgs notifyItem, Guid[] stackItemsIds) =>
            new()
            {
                ScriptHash = notifyItem.ScriptHash,
                EventName = notifyItem.EventName,
                StackItemIds = stackItemsIds,
            };

        #region ISerializable

        public virtual int Size =>
            ScriptHash.Size +
            EventName.GetVarSize() +
            StackItemIds.Sum(s => s.ToByteArray().GetVarSize());

        public virtual void Deserialize(ref MemoryReader reader)
        {
            ScriptHash.Deserialize(ref reader);
            EventName = reader.ReadVarString();

            uint aLen = reader.ReadUInt32();
            StackItemIds = new Guid[aLen];
            for (int i = 0; i < aLen; i++)
                StackItemIds[i] = new Guid(reader.ReadVarMemory().Span);
        }

        public virtual void Serialize(BinaryWriter writer)
        {
            ScriptHash.Serialize(writer);
            writer.WriteVarString(EventName);

            writer.Write((uint)StackItemIds.Length);
            for (int i = 0; i < StackItemIds.Length; i++)
                writer.WriteVarBytes(StackItemIds[i].ToByteArray());
        }

        #endregion

        #region IEquatable

        public bool Equals(NotifyLogState other) =>
            EventName == other.EventName && StackItemIds.SequenceEqual(other.StackItemIds) &&
            ScriptHash == other.ScriptHash;

        public override bool Equals(object obj) =>
            Equals(obj as NotifyLogState);

        public override int GetHashCode() =>
            ScriptHash.GetHashCode() + base.GetHashCode();

        #endregion
    }
}