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
        readonly AsyncPool<ClientSession<byte[], byte[], byte[], byte[], Empty, ByteArrayFunctions>> _sessionPool;
        private readonly FasterKVSettings<byte[], byte[]> _settings;
        private readonly FasterKV<byte[], byte[]> _store;

        public string StorePath { get; }

        public FasterStore(string path)
        {
            StorePath = path;
            _settings = new(Path.GetFullPath(path))
            {
                TryRecoverLatest = true,
            };
            _store = new(_settings);
            _sessionPool = new AsyncPool<ClientSession<byte[], byte[], byte[], byte[], Empty, ByteArrayFunctions>>(
                    _settings.LogDevice.ThrottleLimit,
                    () => _store.For(new ByteArrayFunctions()).NewSession<ByteArrayFunctions>());
        }

        public void Dispose()
        {
            _store.TakeFullCheckpointAsync(CheckpointType.Snapshot).AsTask().GetAwaiter().GetResult();
            _store.CompleteCheckpointAsync().AsTask().GetAwaiter().GetResult();
            _store.Log.DisposeFromMemory();
            _store.Log.FlushAndEvict(true);
            _store.Dispose();
            _settings.Dispose();
            _sessionPool.Dispose();
            GC.SuppressFinalize(this);
        }

        public bool Contains(byte[] key)
        {
            return TryGet(key) != null;
        }

        public void Delete(byte[] key)
        {
            if (!_sessionPool.TryGet(out var session))
                session = _sessionPool.GetAsync().AsTask().GetAwaiter().GetResult();
            var status = session.Delete(key);
            _store.TryInitiateHybridLogCheckpoint(out _, CheckpointType.Snapshot, true);
            _sessionPool.Return(session);
        }

        public ISnapshot GetSnapshot() =>
            new FasterSnapshot(this);

        public void Put(byte[] key, byte[] value)
        {
            if (!_sessionPool.TryGet(out var session))
                session = _sessionPool.GetAsync().AsTask().GetAwaiter().GetResult();
            var status = session.Upsert(key, value);
            _store.TryInitiateHybridLogCheckpoint(out _, CheckpointType.Snapshot, true);
            _sessionPool.Return(session);
        }

        public IEnumerable<(byte[] Key, byte[] Value)> Seek(byte[] keyOrPrefix, SeekDirection direction)
        {
            ByteArrayComparer comparer = direction == SeekDirection.Forward ? ByteArrayComparer.Default : ByteArrayComparer.Reverse;
            var records = SeekKeyValuesPairs(keyOrPrefix, comparer);
            return records.OrderBy(p => p.key, comparer);
        }

        public byte[] TryGet(byte[] key)
        {
            if (!_sessionPool.TryGet(out var session))
                session = _sessionPool.GetAsync().AsTask().GetAwaiter().GetResult();
            var (status, output) = session.Read(key);
            byte[] value = null;
            if (status.IsPending)
            {
                session.CompletePendingWithOutputs(out var iter, true);
                while (iter.Next())
                {
                    if (iter.Current.Status.Found)
                        value = iter.Current.Output;
                }
                iter.Dispose();
            }
            else if (status.Found)
                value = output;
            _sessionPool.Return(session);
            return value;
        }

        internal IEnumerable<(byte[] key, byte[] value)> SeekKeyValuesPairs(byte[] keyOrPrefix, ByteArrayComparer comparer)
        {
            if (!_sessionPool.TryGet(out var session))
                session = _sessionPool.GetAsync().AsTask().GetAwaiter().GetResult();
            using var it = session.Iterate(_store.Log.TailAddress);
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
            it.Dispose();
            _sessionPool.Return(session);
        }
    }
}
