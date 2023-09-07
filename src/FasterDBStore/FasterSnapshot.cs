using Akka.Actor;
using FASTER.core;
using Neo.Persistence;
using System;
using System.Collections.Generic;

namespace Neo.Plugins.Storage
{
    public class FasterSnapshot : ISnapshot
    {
        private readonly ClientSession<byte[], byte[], byte[], byte[], Empty, ByteArrayFunctions> _session;
        private readonly FasterKVSettings<byte[], byte[]> _settings;
        private readonly FasterKV<byte[], byte[]> _store;
        private readonly IStore _db;

        public FasterSnapshot(
            IStore store)
        {
            _db = store;
            _settings = new(null);
            _store = new(_settings);
            _session = _store.For(new ByteArrayFunctions()).NewSession<ByteArrayFunctions>();
        }

        public void Dispose()
        {
            _store.Dispose();
            _session.Dispose();
            GC.SuppressFinalize(this);
        }

        public void Commit()
        {
            _session.CompletePending(true);
            using var it = _session.Iterate();
            while (it.GetNext(out _))
            {
                var keyArray = it.GetKey();
                var valueArray = it.GetValue();
                if (valueArray == null)
                    _db.Delete(keyArray);
                else
                    _db.Put(keyArray, valueArray);
            }
        }

        public bool Contains(byte[] key) =>
            _db.Contains(key);

        public void Delete(byte[] key) =>
            _session.Upsert(key, null);

        public void Put(byte[] key, byte[] value) =>
            _session.Upsert(key, value);

        public IEnumerable<(byte[] Key, byte[] Value)> Seek(byte[] keyOrPrefix, SeekDirection direction) =>
            _db.Seek(keyOrPrefix, direction);

        public byte[] TryGet(byte[] key) =>
            _db.TryGet(key);
    }
}
