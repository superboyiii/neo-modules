// Copyright (C) 2015-2023 The Neo Project.
//
// The Neo.Plugins.Storage.LevelDBStore is free software distributed under the MIT software license,
// see the accompanying file LICENSE in the main directory of the
// project or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using System;

namespace Neo.IO.Data.LevelDB
{
    public class Snapshot : IDisposable
    {
        internal IntPtr db, handle;

        internal Snapshot(IntPtr db)
        {
            this.db = db;
            this.handle = Native.leveldb_create_snapshot(db);
        }

        public void Dispose()
        {
            if (handle != IntPtr.Zero)
            {
                Native.leveldb_release_snapshot(db, handle);
                handle = IntPtr.Zero;
            }
        }
    }
}
