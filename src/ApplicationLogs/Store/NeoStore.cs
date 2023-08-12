using ApplicationLogs.Store.Models;
using ApplicationLogs.Store.States;
using Neo;
using Neo.Ledger;
using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using Neo.SmartContract;
using Neo.VM.Types;

namespace ApplicationLogs.Store
{
    public sealed class NeoStore : IDisposable
    {
        #region Globals

        private IStore _store;
        private ISnapshot _blocklogsnapshot;

        #endregion

        #region ctor

        public NeoStore(
            IStore store)
        {
            _store = store;
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            _store?.Dispose();
            GC.SuppressFinalize(this);
        }

        #endregion

        #region Batching

        public void StartBlockLogBatch()
        {
            _blocklogsnapshot?.Dispose();
            _blocklogsnapshot = _store.GetSnapshot();
        }

        public void CommitBlockLog() =>
            _blocklogsnapshot?.Commit();

        #endregion

        #region Store

        public IStore GetStore() => _store;

        #endregion

        #region Block

        public BlockchainExecutionModel GetBlockLog(UInt256 hash, TriggerType trigger)
        {
            using var lss = new LogStorageStore(_store.GetSnapshot());
            if (lss.TryGetExecutionBlockState(hash, trigger, out var executionBlockStateId) &&
                lss.TryGetExecutionState(executionBlockStateId, out var executionLogState))
            {
                var lstOfStackItems = new List<StackItem>();
                foreach (var stackItemId in executionLogState.StackItemIds)
                {
                    if (lss.TryGetStackItemState(stackItemId, out var stackItem))
                        lstOfStackItems.Add(stackItem);
                }
                var model = BlockchainExecutionModel.Create(trigger, executionLogState, lstOfStackItems.ToArray());
                if (lss.TryGetBlockState(hash, trigger, out var blockLogState))
                {
                    lstOfStackItems.Clear();
                    var lstOfEventModel = new List<BlockchainEventModel>();
                    foreach (var stackItemId in blockLogState.StackItemIds)
                    {
                        if (lss.TryGetStackItemState(stackItemId, out var stackItem))
                            lstOfStackItems.Add(stackItem);
                        lstOfEventModel.Add(BlockchainEventModel.Create(blockLogState, lstOfStackItems.ToArray()));
                    }
                    model.TxHash = blockLogState.TransactionHash;
                    model.Notifications = lstOfEventModel.ToArray();
                }
                return model;
            }
            return null;
        }

        public BlockchainExecutionModel GetTransactionLog(UInt256 hash, TriggerType trigger)
        {
            using var lss = new LogStorageStore(_store.GetSnapshot());
            if (lss.TryGetExecutionTransactionState(hash, trigger, out var executionTransactionStateId) &&
                lss.TryGetExecutionState(executionTransactionStateId, out var executionLogState))
            {
                var lstOfStackItems = new List<StackItem>();
                foreach (var stackItemId in executionLogState.StackItemIds)
                {
                    if (lss.TryGetStackItemState(stackItemId, out var stackItem))
                        lstOfStackItems.Add(stackItem);
                }
                var model = BlockchainExecutionModel.Create(trigger, executionLogState, lstOfStackItems.ToArray());
                var lstOfEventModel = new List<BlockchainEventModel>();
                lstOfStackItems.Clear();
                foreach (var txState in lss.FindTransactionState(hash, trigger))
                {
                    foreach (var stackItemId in txState.StackItemIds)
                    {
                        if (lss.TryGetStackItemState(stackItemId, out var stackItem))
                            lstOfStackItems.Add(stackItem);
                    }
                    lstOfEventModel.Add(BlockchainEventModel.Create(txState, lstOfStackItems.ToArray()));
                }
                model.TxHash = hash;
                model.Notifications = lstOfEventModel.ToArray();
                return model;
            }
            return null;
        }

        public void PutBlockLog(Block block, IReadOnlyList<Blockchain.ApplicationExecuted> applicationExecutedList)
        {

            foreach (var appExecution in applicationExecutedList)
            {
                using var lss = new LogStorageStore(_blocklogsnapshot);
                var exeStateId = PutExecutionLogBlock(lss, block, appExecution);
                PutTransactionLog(lss, block, appExecution, exeStateId);
            }
        }

        private Guid PutExecutionLogBlock(LogStorageStore logStore, Block block, Blockchain.ApplicationExecuted appExecution)
        {
            var exeStateId = logStore.PutExecutionState(ExecutionLogState.Create(appExecution, CreateStackItemIdList(logStore, appExecution)));
            logStore.PutExecutionBlockState(block.Hash, appExecution.Trigger, exeStateId);
            return exeStateId;
        }

        #endregion

        #region Transaction

        private void PutTransactionLog(LogStorageStore logStore, Block block, Blockchain.ApplicationExecuted appExecution, Guid executionStateId)
        {
            if (appExecution.Transaction != null)
                logStore.PutExecutionTransactionState(appExecution.Transaction.Hash, appExecution.Trigger, executionStateId); // For looking up execution log by transaction hash

            for (int i = 0; i < appExecution.Notifications.Length; i++)
            {
                var notifyItem = appExecution.Notifications[i];
                var stackItemStateIds = CreateStackItemIdList(logStore, notifyItem); // Save notify stack items
                logStore.PutBlockState(block.Hash, appExecution.Trigger, // For looking up transaction stack items by block hash. Note: if transaction is null than its OnPost or OnPersist triggers
                    BlockLogState.Create(appExecution.Transaction?.Hash, notifyItem.ScriptHash, notifyItem.EventName, stackItemStateIds));
                if (appExecution.Transaction != null)
                    logStore.PutTransactionState(appExecution.Transaction.Hash, (uint)i, appExecution.Trigger, // For looking up transaction stack items by transaction hash
                        TransactionLogState.Create(notifyItem.ScriptHash, notifyItem.EventName, stackItemStateIds));
                logStore.PutContractState(notifyItem.ScriptHash, block.Timestamp, (uint)i, // For looking up contract stack items by contract scriptHash
                    ContractLogState.Create(appExecution.Transaction?.Hash, appExecution.Trigger, notifyItem.EventName, stackItemStateIds));
            }
        }

        #endregion

        #region StackItem

        private Guid[] CreateStackItemIdList(LogStorageStore logStore, Blockchain.ApplicationExecuted appExecution)
        {
            var lstStackItemIds = new List<Guid>();
            foreach (var stackItem in appExecution.Stack)
                lstStackItemIds.Add(logStore.PutStackItemState(stackItem));
            return lstStackItemIds.ToArray();
        }

        private Guid[] CreateStackItemIdList(LogStorageStore logStore, NotifyEventArgs notifyEventArgs)
        {
            var lstStackItemIds = new List<Guid>();
            foreach (var stackItem in notifyEventArgs.State)
                lstStackItemIds.Add(logStore.PutStackItemState(stackItem));
            return lstStackItemIds.ToArray();
        }

        #endregion
    }
}
