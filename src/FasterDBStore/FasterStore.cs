using Akka.Actor;
using FASTER.core;
using Neo.Persistence;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Neo.Plugins.Storage
{
    public class FasterStore : IStore
    {
        private readonly LogSettings _logSettings;
        private readonly CheckpointSettings _checkpointSettings;
        private readonly FasterKV<byte[], byte[]> _store;

        public AsyncPool<ClientSession<byte[], byte[], byte[], byte[], Empty, ByteArrayFunctions>> SessionPool { get; }
        public FasterStore(string path)
        {
            var storePath = Path.GetFullPath(path);
            _logSettings = new LogSettings()
            {
                LogDevice = new ManagedLocalStorageDevice(Path.Combine(storePath, "LOG"), recoverDevice: true, osReadBuffering: true),
                ObjectLogDevice = new ManagedLocalStorageDevice(Path.Combine(storePath, "DATA"), recoverDevice: true, osReadBuffering: true),
                //PageSizeBits = 9,
                //SegmentSizeBits = 9,
                //MemorySizeBits = 11,
                //MutableFraction = 0.5,
            };
            _checkpointSettings = new CheckpointSettings()
            {
                CheckpointManager = new DeviceLogCommitCheckpointManager(
                    new LocalStorageNamedDeviceFactory(),
                    new NeoCheckpointNamingScheme(storePath)),
                RemoveOutdated = true,
            };
            _store = new(
                1L << 20,
                _logSettings,
                _checkpointSettings,
                tryRecoverLatest: true);
            SessionPool = new AsyncPool<ClientSession<byte[], byte[], byte[], byte[], Empty, ByteArrayFunctions>>(
                    _logSettings.LogDevice.ThrottleLimit,
                    () => _store.For(new ByteArrayFunctions()).NewSession<ByteArrayFunctions>());
        }

        public void Dispose()
        {
            _store.TryInitiateHybridLogCheckpoint(out _, CheckpointType.FoldOver);
            _store.CompleteCheckpointAsync().AsTask().GetAwaiter().GetResult();
            //_store.Log.FlushAndEvict(true);
            _store.Dispose();
            SessionPool.Dispose();
            GC.SuppressFinalize(this);
        }

        public bool Contains(byte[] key)
        {
            return TryGet(key) != null;
        }

        public void Delete(byte[] key)
        {
            if (SessionPool.TryGet(out var session) == false)
                session = SessionPool.GetAsync().AsTask().GetAwaiter().GetResult();
            var status = session.Delete(key);
            if (status.IsPending)
                session.CompletePending(true);
            SessionPool.Return(session);
        }

        public ISnapshot GetSnapshot()
        {
            _store.TryInitiateHybridLogCheckpoint(out _, CheckpointType.Snapshot);
            //_store.Log.Flush(true);
            return new FasterSnapshot(this);
        }

        public void Put(byte[] key, byte[] value)
        {
            if (SessionPool.TryGet(out var session) == false)
                session = SessionPool.GetAsync().AsTask().GetAwaiter().GetResult();
            var status = session.Upsert(key, value);
            if (status.IsPending)
                session.CompletePending(true);
            SessionPool.Return(session);
        }

        public IEnumerable<(byte[] Key, byte[] Value)> Seek(byte[] keyOrPrefix, SeekDirection direction)
        {
            ByteArrayComparer comparer = direction == SeekDirection.Forward ? ByteArrayComparer.Default : ByteArrayComparer.Reverse;
            var records = SeekKeyValuesPairs(keyOrPrefix, comparer);
            return records.OrderBy(p => p.key, comparer);
        }

        public byte[] TryGet(byte[] key)
        {
            if (SessionPool.TryGet(out var session) == false)
                session = SessionPool.GetAsync().AsTask().GetAwaiter().GetResult();
            var (status, output) = session.Read(key);
            byte[] value = null;
            if (status.IsPending && session.CompletePendingWithOutputs(out var iter, true, true))
            {
                using (iter)
                {
                    if (iter.Next())
                        value = iter.Current.Output;
                }
            }
            else if (status.Found)
                value = output;
            SessionPool.Return(session);
            return value;
        }

        internal IEnumerable<(byte[] key, byte[] value)> SeekKeyValuesPairs(byte[] keyOrPrefix, ByteArrayComparer comparer)
        {
            if (SessionPool.TryGet(out var session) == false)
                session = SessionPool.GetAsync().AsTask().GetAwaiter().GetResult();
            using (var it = session.Iterate(_store.Log.TailAddress))
            {
                while (it.GetNext(out _))
                {
                    var keyArray = it.GetKey();
                    var valueArray = it.GetValue();
                    if (keyOrPrefix?.Length > 0)
                    {
                        if (comparer.Compare(keyArray, keyOrPrefix) >= 0)
                            yield return (keyArray, valueArray);
                    }
                    else
                        yield return (keyArray, valueArray);
                }
            }
            SessionPool.Return(session);
        }
    }
}
