using ApplicationLogs.Store.States;
using Neo;
using Neo.VM.Types;
using Array = System.Array;

namespace ApplicationLogs.Store.Models
{
    public class BlockchainEventModel
    {
        public UInt160 ScriptHash { get; set; } = new();
        public string EventName { get; set; } = string.Empty;
        public StackItem[] State { get; set; } = Array.Empty<StackItem>();

        public static BlockchainEventModel Create(BlockLogState blockLogState, StackItem[] state) =>
            new()
            {
                ScriptHash = blockLogState.ScriptHash,
                EventName = blockLogState.EventName,
                State = state,
            };

        public static BlockchainEventModel Create(TransactionLogState transactionLogState, StackItem[] state) =>
            new()
            {
                ScriptHash = transactionLogState.ScriptHash,
                EventName = transactionLogState.EventName,
                State = state,
            };
    }
}
