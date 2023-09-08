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
        private readonly ClientSession<byte[], byte[], byte[], byte[], Empty, ByteArrayFunctions> _session;
        private readonly FasterKVSettings<byte[], byte[]> _settings;
        private readonly FasterKV<byte[], byte[]> _store;

        public FasterStore(string path)
        {
            _settings = new(Path.GetFullPath(path))
            {
                TryRecoverLatest = true,
                ReadCacheEnabled = false,
                PageSize = 512, // Keep memory as small as we can, so it writes to log on disk.
                MemorySize = 512,// Keep memory as small as we can, so it writes to log on disk.
            };
            _store = new(_settings);
            _session = _store.For(new ByteArrayFunctions()).NewSession<ByteArrayFunctions>();
            try
            {
                _store.Recover();
            }
            catch { }
        }

        public void Dispose()
        {
            _session.CompletePending(true);
            _store.TakeFullCheckpointAsync(CheckpointType.Snapshot).AsTask().GetAwaiter().GetResult();
            _store.CompleteCheckpointAsync().AsTask().GetAwaiter().GetResult();
            _store.Log.DisposeFromMemory();
            _store.Log.FlushAndEvict(true);
            _store.Dispose();
            _settings.Dispose();
            GC.SuppressFinalize(this);
        }

        public bool Contains(byte[] key)
        {
            return TryGet(key) != null;
        }

        public void Delete(byte[] key)
        {
            using var session = _store.For(new ByteArrayFunctions()).NewSession<ByteArrayFunctions>();
            var status = session.Delete(key);
            if (status.IsPending)
                session.CompletePending(true);
        }

        public ISnapshot GetSnapshot() =>
            new FasterSnapshot(this);

        public void Put(byte[] key, byte[] value)
        {
            using var session = _store.For(new ByteArrayFunctions()).NewSession<ByteArrayFunctions>();
            var status = session.Upsert(key, value);
            if (status.IsPending)
                session.CompletePending(false);
        }

        public IEnumerable<(byte[] Key, byte[] Value)> Seek(byte[] keyOrPrefix, SeekDirection direction)
        {
            ByteArrayComparer comparer = direction == SeekDirection.Forward ? ByteArrayComparer.Default : ByteArrayComparer.Reverse;
            var records = SeekKeyValuesPairs(keyOrPrefix, comparer);
            return records.OrderBy(p => p.key, comparer);
        }

        public byte[] TryGet(byte[] key)
        {
            using var session = _store.For(new ByteArrayFunctions()).NewSession<ByteArrayFunctions>();
            var (status, output) = session.Read(key);
            if (status.IsPending)
            {
                session.CompletePendingWithOutputs(out var iter, true);
                while (iter.Next())
                {
                    if (iter.Current.Status.Found)
                        return iter.Current.Output;
                }
            }
            else if (status.Found)
                return output;
            return null;
        }

        internal IEnumerable<(byte[] key, byte[] value)> SeekKeyValuesPairs(byte[] keyOrPrefix, ByteArrayComparer comparer)
        {
            using var session = _store.For(new ByteArrayFunctions()).NewSession<ByteArrayFunctions>();
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
        }
    }
}
