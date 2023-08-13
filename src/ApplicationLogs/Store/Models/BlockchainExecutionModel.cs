using ApplicationLogs.Store.States;
using Neo;
using Neo.SmartContract;
using Neo.VM;
using Neo.VM.Types;

namespace ApplicationLogs.Store.Models
{
    public class BlockchainExecutionModel
    {
        public TriggerType Trigger { get; set; } = TriggerType.All;
        public VMState VmState { get; set; } = VMState.NONE;
        public string Exception { get; set; } = string.Empty;
        public long GasConsumed { get; set; } = 0L;
        public StackItem[] Stack { get; set; } = System.Array.Empty<StackItem>();
        public BlockchainEventModel[] Notifications { get; set; } = System.Array.Empty<BlockchainEventModel>();

        public static BlockchainExecutionModel Create(TriggerType trigger, ExecutionLogState executionLogState, StackItem[] stack) =>
            new()
            {
                Trigger = trigger,
                VmState = executionLogState.VmState,
                Exception = executionLogState.Exception ?? string.Empty,
                GasConsumed = executionLogState.GasConsumed,
                Stack = stack,
            };
    }
}