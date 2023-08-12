using Neo;
using Neo.IO;

namespace ApplicationLogs.Store.States
{
    public class BlockLogState : NotifyLogState, IEquatable<BlockLogState>
    {
        public UInt256 TransactionHash { get; set; } = new();

        public static BlockLogState Create(UInt256 txHash, UInt160 scriptHash, string eventName, Guid[] stackItemIds) =>
            new()
            {
                TransactionHash = txHash ?? new(),
                ScriptHash = scriptHash,
                EventName = eventName,
                StackItemIds = stackItemIds,
            };

        #region ISerializable

        public override int Size =>
            TransactionHash.Size +
            base.Size;

        public override void Deserialize(ref MemoryReader reader)
        {
            TransactionHash.Deserialize(ref reader);
            base.Deserialize(ref reader);
        }

        public override void Serialize(BinaryWriter writer)
        {
            TransactionHash.Serialize(writer);
            base.Serialize(writer);
        }

        #endregion

        #region IEquatable

        public bool Equals(BlockLogState other) =>
            ScriptHash == other.ScriptHash && EventName == other.EventName &&
            StackItemIds.SequenceEqual(other.StackItemIds) && TransactionHash == other.TransactionHash;

        public override bool Equals(object obj) =>
            Equals(obj as BlockLogState);

        public override int GetHashCode() =>
            TransactionHash.GetHashCode() + base.GetHashCode();

        #endregion
    }
}
