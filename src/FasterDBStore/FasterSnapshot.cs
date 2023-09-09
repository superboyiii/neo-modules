using Akka.Actor;
using FASTER.core;
using Neo.Persistence;
using System;
using System.Collections.Generic;

namespace Neo.Plugins.Storage
{
    public class FasterSnapshot : ISnapshot
    {
        private readonly LogSettings _logSettings;

        private readonly FasterKV<byte[], byte[]> _store;
        private readonly FasterStore _db;
        
        private readonly AsyncPool<ClientSession<byte[], byte[], byte[], byte[], Empty, ByteArrayFunctions>> _sessionPool;

        public FasterSnapshot(
            FasterStore store)
        {
            _db = store;
            _logSettings = new LogSettings()
            {
                LogDevice = new NullDevice(),
                ObjectLogDevice = new NullDevice(),
                PageSizeBits = 21,
                MemorySizeBits = 21,
                SegmentSizeBits = 21,
                MutableFraction = 1,
                PreallocateLog = true,
            };
            _store = new(
                1 << 20,
                _logSettings,
                serializerSettings: new SerializerSettings<byte[], byte[]>()
                {
                    keySerializer = () => new ByteArrayBinaryObjectSerializer(),
                    valueSerializer = () => new ByteArrayBinaryObjectSerializer(),
                },
                comparer: new ByteArrayFasterEqualityComparer());
            _sessionPool = new AsyncPool<ClientSession<byte[], byte[], byte[], byte[], Empty, ByteArrayFunctions>>(
                    _logSettings.LogDevice.ThrottleLimit,
                    () => _store.For(new ByteArrayFunctions()).NewSession<ByteArrayFunctions>());
        }

        public void Dispose()
        {
            _store.Dispose();
            _sessionPool.Dispose();
            GC.SuppressFinalize(this);
        }

        public void Commit()
        {
            if (_sessionPool.TryGet(out var session) == false)
                session = _sessionPool.GetAsync().AsTask().GetAwaiter().GetResult();
            using var it = session.Iterate(_store.Log.TailAddress);
            while (it.GetNext(out _))
            {
                var keyArray = it.GetKey();
                var valueArray = it.GetValue();
                if (valueArray == null)
                    _db.Delete(keyArray);
                else
                    _db.Put(keyArray, valueArray);
            }
            _sessionPool.Return(session);
        }

        public bool Contains(byte[] key) =>
            _db.Contains(key);

        public void Delete(byte[] key)
        {
            if (_sessionPool.TryGet(out var session) == false)
                session = _sessionPool.GetAsync().AsTask().GetAwaiter().GetResult();
            var status = session.Upsert(key, null);
            if (status.IsPending)
                session.CompletePending(true);
            _sessionPool.Return(session);
        }

        public void Put(byte[] key, byte[] value)
        {
            if (_sessionPool.TryGet(out var session) == false)
                session = _sessionPool.GetAsync().AsTask().GetAwaiter().GetResult();
            var status = session.Upsert(key, value);
            if (status.IsPending)
                session.CompletePending(true);
            _sessionPool.Return(session);
        }

        public IEnumerable<(byte[] Key, byte[] Value)> Seek(byte[] keyOrPrefix, SeekDirection direction) =>
            _db.Seek(keyOrPrefix, direction);

        public byte[] TryGet(byte[] key) =>
            _db.TryGet(key);
    }
}
