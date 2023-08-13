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

        public static BlockchainEventModel Create(UInt160 scriptHash, string eventName, StackItem[] state) =>
            new()
            {
                ScriptHash = scriptHash,
                EventName = eventName ?? string.Empty,
                State = state,
            };

        public static BlockchainEventModel Create(NotifyLogState notifyLogState, StackItem[] state) =>
            new()
            {
                ScriptHash = notifyLogState.ScriptHash,
                EventName = notifyLogState.EventName,
                State = state,
            };
    }
}
