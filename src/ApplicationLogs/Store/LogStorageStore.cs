using ApplicationLogs.Store.States;
using Neo;
using Neo.IO;
using Neo.Persistence;
using Neo.SmartContract;
using Neo.VM;
using Neo.VM.Types;

namespace ApplicationLogs.Store
{
    public sealed class LogStorageStore : IDisposable
    {
        #region Prefixes

        private static readonly int Prefix_Size = sizeof(int) + sizeof(byte);
        private static readonly int Prefix_Block_Trigger_Size = Prefix_Size + UInt256.Length;
        private static readonly int Prefix_Transaction_Trigger_Size = Prefix_Size + UInt256.Length + sizeof(int);
        private static readonly int Prefix_Execution_Block_Trigger_Size = Prefix_Size + UInt256.Length;
        private static readonly int Prefix_Execution_Transaction_Trigger_Size = Prefix_Size + UInt256.Length;

        private static readonly int Prefix_Id = 0x414c4f47;                 // Magic Code: (ALOG);
        private static readonly byte Prefix_Block = 0x20;                   // BlockHash, Trigger -> TxHash, ScriptHash, EventName, StackItem_GUID_List
        private static readonly byte Prefix_Contract = 0x21;                // ScriptHash, TimeStamp, EventIterIndex -> txHash, Trigger, EventName, StackItem_GUID_List
        private static readonly byte Prefix_Execution = 0x22;               // Execution_GUID -> Data, StackItem_GUID_List
        private static readonly byte Prefix_Execution_Block = 0x23;         // BlockHash, Trigger -> Execution_GUID
        private static readonly byte Prefix_Execution_Transaction = 0x24;   // TxHash, Trigger -> Execution_GUID
        private static readonly byte Prefix_Transaction = 0x25;             // TxHash, Trigger, EventIterIndex -> ScriptHash, EventName, StackItem_GUID_List
        private static readonly byte Prefix_StackItem = 0xed;               // StackItem_GUID -> Data

        #endregion

        #region Global Variables

        private readonly ISnapshot _snapshot;

        #endregion

        #region Ctor

        public LogStorageStore(ISnapshot snapshot)
        {
            ArgumentNullException.ThrowIfNull(snapshot, nameof(snapshot));
            _snapshot = snapshot;
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            _snapshot.Dispose();
            GC.SuppressFinalize(this);
        }

        #endregion

        #region Put

        public void PutBlockState(UInt256 hash, TriggerType trigger, BlockLogState state)
        {
            var key = new KeyBuilder(Prefix_Id, Prefix_Block)
                .Add(hash)
                .Add((byte)trigger)
                .ToArray();
            _snapshot.Put(key, state.ToArray());
        }

        public void PutContractState(UInt160 scriptHash, ulong timestamp, uint iterIndex, ContractLogState state)
        {
            var key = new KeyBuilder(Prefix_Id, Prefix_Contract)
                .Add(scriptHash)
                .AddBigEndian(timestamp)
                .AddBigEndian(iterIndex)
                .ToArray();
            _snapshot.Put(key, state.ToArray());
        }

        public Guid PutExecutionState(ExecutionLogState state)
        {
            var id = Guid.NewGuid();
            var key = new KeyBuilder(Prefix_Id, Prefix_Execution)
                .Add(id.ToByteArray())
                .ToArray();
            _snapshot.Put(key, state.ToArray());
            return id;
        }

        public void PutExecutionBlockState(UInt256 blockHash, TriggerType trigger, Guid executionStateId)
        {
            var key = new KeyBuilder(Prefix_Id, Prefix_Execution_Block)
                .Add(blockHash)
                .Add((byte)trigger)
                .ToArray();
            _snapshot.Put(key, executionStateId.ToByteArray());
        }

        public void PutExecutionTransactionState(UInt256 txHash, TriggerType trigger, Guid executionStateId)
        {
            var key = new KeyBuilder(Prefix_Id, Prefix_Execution_Transaction)
                .Add(txHash)
                .Add((byte)trigger)
                .ToArray();
            _snapshot.Put(key, executionStateId.ToByteArray());
        }

        public void PutTransactionState(UInt256 hash, uint iterIndex, TriggerType trigger, TransactionLogState state)
        {
            var key = new KeyBuilder(Prefix_Id, Prefix_Transaction)
                .Add(hash)
                .AddBigEndian(iterIndex)
                .Add((byte)trigger)
                .ToArray();
            _snapshot.Put(key, state.ToArray());
        }

        public Guid PutStackItemState(StackItem stackItem)
        {
            var id = Guid.NewGuid();
            var key = new KeyBuilder(Prefix_Id, Prefix_StackItem)
                .Add(id.ToByteArray())
                .ToArray();
            _snapshot.Put(key, BinarySerializer.Serialize(stackItem, uint.MaxValue / 2));
            return id;
        }

        #endregion

        #region Find

        public IEnumerable<(BlockLogState State, TriggerType Trigger)> FindBlockState(UInt256 hash)
        {
            var prefixKey = new KeyBuilder(Prefix_Id, Prefix_Block)
                .Add(hash)
                .ToArray();
            foreach (var (key, value) in _snapshot.Seek(prefixKey, SeekDirection.Forward))
            {
                if (key.AsSpan().StartsWith(prefixKey))
                    yield return (value.AsSerializable<BlockLogState>(), (TriggerType)key.AsSpan(Prefix_Block_Trigger_Size)[0]);
                else
                    yield break;
            }
        }

        public IEnumerable<BlockLogState> FindBlockState(UInt256 hash, TriggerType trigger)
        {
            var prefix = new KeyBuilder(Prefix_Id, Prefix_Block)
                .ToArray();
            var prefixKey = new KeyBuilder(Prefix_Id, Prefix_Block)
                .Add(hash)
                .ToArray();
            foreach (var (key, value) in _snapshot.Seek(prefixKey, SeekDirection.Forward))
            {
                var skey = key.AsSpan();
                if (skey.StartsWith(prefix))
                {
                    if (skey.EndsWith(new byte[] { (byte)trigger }))
                        yield return value.AsSerializable<BlockLogState>();
                }
                else
                    yield break;
            }
        }

        public IEnumerable<ContractLogState> FindContractState(UInt160 scriptHash, uint page, uint pageSize)
        {
            var prefix = new KeyBuilder(Prefix_Id, Prefix_Contract)
                .Add(scriptHash)
                .ToArray();
            var prefixKey = new KeyBuilder(Prefix_Id, Prefix_Contract)
                .Add(scriptHash)
                .AddBigEndian(ulong.MaxValue)
                .ToArray();
            uint index = 1;
            foreach (var (key, value) in _snapshot.Seek(prefixKey, SeekDirection.Backward))
            {
                if (key.AsSpan().StartsWith(prefix))
                {
                    if (index >= page && index < (pageSize + page))
                        yield return value.AsSerializable<ContractLogState>();
                    index++;
                }
                else
                    yield break;
            }
        }

        public IEnumerable<ContractLogState> FindContractState(UInt160 scriptHash, TriggerType trigger, uint page, uint pageSize)
        {
            var prefix = new KeyBuilder(Prefix_Id, Prefix_Contract)
                .Add(scriptHash)
                .ToArray();
            var prefixKey = new KeyBuilder(Prefix_Id, Prefix_Contract)
                .Add(scriptHash)
                .AddBigEndian(ulong.MaxValue)
                .ToArray();
            uint index = 1;
            foreach (var (key, value) in _snapshot.Seek(prefixKey, SeekDirection.Backward))
            {
                if (key.AsSpan().StartsWith(prefix))
                {
                    var state = value.AsSerializable<ContractLogState>();
                    if (state.Trigger == trigger)
                    {
                        if (index >= page && index < (pageSize + page))
                            yield return state;
                        index++;
                    }
                }
                else
                    yield break;
            }
        }

        public IEnumerable<(Guid ExecutionStateId, TriggerType Trigger)> FindExecutionBlockState(UInt256 hash)
        {
            var prefixKey = new KeyBuilder(Prefix_Id, Prefix_Execution_Block)
                .Add(hash)
                .ToArray();
            foreach (var (key, value) in _snapshot.Seek(prefixKey, SeekDirection.Forward))
            {
                if (key.AsSpan().StartsWith(prefixKey))
                    yield return (new Guid(value), (TriggerType)key.AsSpan(Prefix_Execution_Block_Trigger_Size)[0]);
                else
                    yield break;
            }
        }

        public IEnumerable<(Guid ExecutionStateId, TriggerType Trigger)> FindExecutionTransactionState(UInt256 hash)
        {
            var prefixKey = new KeyBuilder(Prefix_Id, Prefix_Execution_Transaction)
                .Add(hash)
                .ToArray();
            foreach (var (key, value) in _snapshot.Seek(prefixKey, SeekDirection.Forward))
            {
                if (key.AsSpan().StartsWith(prefixKey))
                    yield return (new Guid(value), (TriggerType)key.AsSpan(Prefix_Execution_Transaction_Trigger_Size)[0]);
                else
                    yield break;
            }
        }

        public IEnumerable<(TransactionLogState State, TriggerType Trigger)> FindTransactionState(UInt256 hash)
        {
            var prefixKey = new KeyBuilder(Prefix_Id, Prefix_Transaction)
                .Add(hash)
                .ToArray();
            foreach (var (key, value) in _snapshot.Seek(prefixKey, SeekDirection.Forward))
            {
                if (key.AsSpan().StartsWith(prefixKey))
                    yield return (value.AsSerializable<TransactionLogState>(), (TriggerType)key.AsSpan(Prefix_Transaction_Trigger_Size)[0]);
                else
                    yield break;
            }
        }

        public IEnumerable<TransactionLogState> FindTransactionState(UInt256 hash, TriggerType trigger)
        {
            var prefix = new KeyBuilder(Prefix_Id, Prefix_Transaction)
                .Add(hash)
                .ToArray();
            var prefixKey = new KeyBuilder(Prefix_Id, Prefix_Transaction)
                .Add(hash)
                .ToArray();
            foreach (var (key, value) in _snapshot.Seek(prefixKey, SeekDirection.Forward))
            {
                var skey = key.AsSpan();
                if (skey.StartsWith(prefix))
                {
                    if (skey.EndsWith(new byte[] { (byte)trigger }))
                        yield return value.AsSerializable<TransactionLogState>();
                }
                else
                    yield break;
            }
        }

        #endregion

        #region TryGet

        public bool TryGetBlockState(UInt256 hash, TriggerType trigger, out BlockLogState state)
        {
            var key = new KeyBuilder(Prefix_Id, Prefix_Block)
                .Add(hash)
                .Add((byte)trigger)
                .ToArray();
            var data = _snapshot.TryGet(key);
            state = data?.AsSerializable<BlockLogState>();
            return data != null && data.Length > 0;
        }

        public bool TryGetContractState(UInt160 scriptHash, ulong timestamp, uint iterIndex, out ContractLogState state)
        {
            var key = new KeyBuilder(Prefix_Id, Prefix_Contract)
                .Add(scriptHash)
                .AddBigEndian(timestamp)
                .AddBigEndian(iterIndex)
                .ToArray();
            var data = _snapshot.TryGet(key);
            state = data?.AsSerializable<ContractLogState>();
            return data != null && data.Length > 0;
        }

        public bool TryGetExecutionState(Guid executionStateId, out ExecutionLogState state)
        {
            var key = new KeyBuilder(Prefix_Id, Prefix_Execution)
                .Add(executionStateId.ToByteArray())
                .ToArray();
            var data = _snapshot.TryGet(key);
            if (data == null)
            {
                state = null;
                return false;
            }
            else
            {
                state = data.AsSerializable<ExecutionLogState>();
                return true;
            }
        }

        public bool TryGetExecutionBlockState(UInt256 blockHash, TriggerType trigger, out Guid executionStateId)
        {
            var key = new KeyBuilder(Prefix_Id, Prefix_Execution_Block)
                .Add(blockHash)
                .Add((byte)trigger)
                .ToArray();
            var data = _snapshot.TryGet(key);
            if (data == null)
            {
                executionStateId = Guid.Empty;
                return false;
            }
            else
            {
                executionStateId = new Guid(data);
                return true;
            }
        }

        public bool TryGetExecutionTransactionState(UInt256 txHash, TriggerType trigger, out Guid executionStateId)
        {
            var key = new KeyBuilder(Prefix_Id, Prefix_Execution_Transaction)
                .Add(txHash)
                .Add((byte)trigger)
                .ToArray();
            var data = _snapshot.TryGet(key);
            if (data == null)
            {
                executionStateId = Guid.Empty;
                return false;
            }
            else
            {
                executionStateId = new Guid(data);
                return true;
            }
        }

        public bool TryGetTransactionState(UInt256 hash, TriggerType trigger, uint iterIndex, out TransactionLogState state)
        {
            var key = new KeyBuilder(Prefix_Id, Prefix_Transaction)
                .Add(hash)
                .AddBigEndian(iterIndex)
                .Add((byte)trigger)
                .ToArray();
            var data = _snapshot.TryGet(key);
            state = data?.AsSerializable<TransactionLogState>();
            return data != null && data.Length > 0;
        }

        public bool TryGetStackItemState(Guid stackItemId, out StackItem stackItem)
        {
            var key = new KeyBuilder(Prefix_Id, Prefix_StackItem)
                .Add(stackItemId.ToByteArray())
                .ToArray();
            var data = _snapshot.TryGet(key);
            if (data == null)
            {
                stackItem = StackItem.Null;
                return false;
            }
            else
            {
                stackItem = BinarySerializer.Deserialize(data, ExecutionEngineLimits.Default);
                return true;
            }
        }

        #endregion
    }
}
