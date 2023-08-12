using Neo;

namespace ApplicationLogs.Store.States
{
    public class TransactionLogState : NotifyLogState, IEquatable<TransactionLogState>
    {
        public static TransactionLogState Create(UInt160 scriptHash, string eventName, Guid[] stackItemsIds) =>
            new()
            {
                ScriptHash = scriptHash,
                EventName = eventName,
                StackItemIds = stackItemsIds,
            };

        #region IEquatable

        public bool Equals(TransactionLogState other) =>
            EventName == other.EventName && StackItemIds.SequenceEqual(other.StackItemIds);

        public override bool Equals(object obj) =>
            Equals(obj as TransactionLogState);

        public override int GetHashCode() =>
            base.GetHashCode();

        #endregion
    }
}
