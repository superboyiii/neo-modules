using Neo;
using Neo.IO;
using Neo.SmartContract;

namespace ApplicationLogs.Store.States
{
    public class ContractLogState : NotifyState, IEquatable<ContractLogState>
    {
        public UInt256 TransactionHash { get; set; } = new();
        public TriggerType Trigger { get; set; } = TriggerType.All;

        public static ContractLogState Create(UInt256 txHash, TriggerType trigger, string eventName, Guid[] stackItemIds) =>
            new()
            {
                TransactionHash = txHash ?? new(),
                Trigger = trigger,
                EventName = eventName,
                StackItemIds = stackItemIds,
            };

        #region ISerializable

        public override int Size =>
            TransactionHash.Size +
            sizeof(byte) +
            base.Size;

        public override void Deserialize(ref MemoryReader reader)
        {
            TransactionHash.Deserialize(ref reader);
            Trigger = (TriggerType)reader.ReadByte();
            base.Deserialize(ref reader);
        }

        public override void Serialize(BinaryWriter writer)
        {
            TransactionHash.Serialize(writer);
            writer.Write((byte)Trigger);
            base.Serialize(writer);
        }

        #endregion

        #region IEquatable

        public bool Equals(ContractLogState other) =>
            EventName == other.EventName && StackItemIds.SequenceEqual(other.StackItemIds) &&
            TransactionHash == other.TransactionHash && Trigger == other.Trigger;

        public override bool Equals(object obj) =>
            Equals(obj as ContractLogState);

        public override int GetHashCode() =>
            TransactionHash.GetHashCode() + Trigger.GetHashCode() + base.GetHashCode();

        #endregion
    }
}
