using Akka.Actor;
using FASTER.core;
using Neo.Persistence;
using System;
using System.Collections.Generic;

namespace Neo.Plugins.Storage
{
    public class FasterSnapshot : ISnapshot
    {
        readonly AsyncPool<ClientSession<byte[], byte[], byte[], byte[], Empty, ByteArrayFunctions>> _sessionPool;
        private readonly FasterKVSettings<byte[], byte[]> _settings;
        private readonly FasterKV<byte[], byte[]> _store;
        private readonly IStore _db;

        public FasterSnapshot(
            IStore store)
        {
            _db = store;
            _settings = new(null);
            _store = new(_settings);
            _sessionPool = new AsyncPool<ClientSession<byte[], byte[], byte[], byte[], Empty, ByteArrayFunctions>>(
                    _settings.LogDevice.ThrottleLimit,
                    () => _store.For(new ByteArrayFunctions()).NewSession<ByteArrayFunctions>());
        }

        public void Dispose()
        {
            _store.Dispose();
            _settings.Dispose();
            _sessionPool.Dispose();
            GC.SuppressFinalize(this);
        }

        public void Commit()
        {
            if (!_sessionPool.TryGet(out var session))
                session = _sessionPool.GetAsync().AsTask().GetAwaiter().GetResult();
            using var it = session.Iterate();
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
            if (!_sessionPool.TryGet(out var session))
                session = _sessionPool.GetAsync().AsTask().GetAwaiter().GetResult();
            session.Upsert(key, null);
            _sessionPool.Return(session);
        }

        public void Put(byte[] key, byte[] value)
        {
            if (!_sessionPool.TryGet(out var session))
                session = _sessionPool.GetAsync().AsTask().GetAwaiter().GetResult();
            session.Upsert(key, value);
            _sessionPool.Return(session);
        }

        public IEnumerable<(byte[] Key, byte[] Value)> Seek(byte[] keyOrPrefix, SeekDirection direction) =>
            _db.Seek(keyOrPrefix, direction);

        public byte[] TryGet(byte[] key) =>
            _db.TryGet(key);
    }
}
