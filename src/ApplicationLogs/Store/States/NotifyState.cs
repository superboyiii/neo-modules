using Neo.IO;

namespace ApplicationLogs.Store.States
{
    public abstract class NotifyState : ISerializable, IEquatable<NotifyState>
    {
        public string EventName { get; set; } = string.Empty;
        public Guid[] StackItemIds { get; set; } = Array.Empty<Guid>();

        #region ISerializable

        public virtual int Size =>
            EventName.GetVarSize() +
            sizeof(uint) +
            StackItemIds.Sum(s => s.ToByteArray().GetVarSize());
        public virtual void Deserialize(ref MemoryReader reader)
        {
            EventName = reader.ReadVarString();

            uint aLen = reader.ReadUInt32();
            StackItemIds = new Guid[aLen];
            for (int i = 0; i < aLen; i++)
                StackItemIds[i] = new Guid(reader.ReadVarMemory().Span);
        }

        public virtual void Serialize(BinaryWriter writer)
        {
            writer.WriteVarString(EventName);

            writer.Write((uint)StackItemIds.Length);
            for (int i = 0; i < StackItemIds.Length; i++)
                writer.WriteVarBytes(StackItemIds[i].ToByteArray());
        }

        #endregion

        #region IEquatable

        public bool Equals(NotifyState other) =>
            EventName == other.EventName && StackItemIds.SequenceEqual(other.StackItemIds);

        public override bool Equals(object obj) =>
            Equals(obj as NotifyState);

        public override int GetHashCode() =>
            StackItemIds.GetHashCode() + EventName.GetHashCode() + base.GetHashCode();

        #endregion
    }
}
