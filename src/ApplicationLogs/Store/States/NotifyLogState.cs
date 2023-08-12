using Neo;
using Neo.IO;

namespace ApplicationLogs.Store.States
{
    public abstract class NotifyLogState : NotifyState, IEquatable<NotifyLogState>
    {
        public UInt160 ScriptHash { get; set; } = new();

        #region ISerializable

        public override int Size =>
            ScriptHash.Size +
            base.Size;

        public override void Deserialize(ref MemoryReader reader)
        {
            ScriptHash.Deserialize(ref reader);
            base.Deserialize(ref reader);
        }

        public override void Serialize(BinaryWriter writer)
        {
            ScriptHash.Serialize(writer);
            base.Serialize(writer);
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
