// Copyright (C) 2015-2023 The Neo Project.
//
// The Neo.Plugins.ApplicationLogs is free software distributed under the MIT software license,
// see the accompanying file LICENSE in the main directory of the
// project or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.IO;
using Neo.Json;
using Neo.Ledger;
using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using Neo.SmartContract;
using Neo.VM;
using static System.IO.Path;

namespace Neo.Plugins
{
    public class EventStore : Plugin
    {
        #region Prefixes

        internal static readonly int Prefix_Id = 0x415050; // Magic Code (APP)

        internal static readonly byte Prefix_ApplicationLog = 10;
        internal static readonly byte Prefix_ApplicationLog_Notify = 11;
        internal static readonly byte Prefix_ApplicationLog_Transaction = 12;

        #endregion

        #region Globals

        private IStore _db;
        private ISnapshot _snapshot;

        #endregion

        public override string Name => "ApplicationLogs";
        public override string Description => "Synchronizes the smart contract log with the NativeContract log (Notify)";

        #region Ctor

        public EventStore()
        {
            Blockchain.Committing += OnCommitting;
            Blockchain.Committed += OnCommitted;
        }

        #endregion

        #region Override Methods

        public override void Dispose()
        {
            Blockchain.Committing -= OnCommitting;
            Blockchain.Committed -= OnCommitted;
            _snapshot?.Dispose();
            _db?.Dispose();
            GC.SuppressFinalize(this);
        }

        protected override void Configure()
        {
            Settings.Load(GetConfiguration());
        }

        protected override void OnSystemLoaded(NeoSystem system)
        {
            if (system.Settings.Network != Settings.Default.Network) return;
            string path = string.Format(Settings.Default.Path, Settings.Default.Network.ToString("X8"));
            _db = system.LoadStore(GetFullPath(path));
            RpcServerPlugin.RegisterMethods(this, Settings.Default.Network);
        }

        #endregion

        [RpcMethod]
        public JToken GetApplicationLog(JArray _params)
        {
            UInt256 hash = UInt256.Parse(_params[0].AsString());
            var appLog = GetTransactionLog(hash);
            if (appLog.ApplicationManifest == null && appLog.Notifications.Length == 0)
                throw new RpcException(-100, "Unknown transaction/blockhash");

            var output = new JObject();
            output["txid"] = hash.ToString();
            output["exception"] =  string.IsNullOrEmpty(appLog.ApplicationManifest.Exception) ? null : appLog.ApplicationManifest.Exception;
            output["trigger"] = appLog.ApplicationManifest.Trigger;
            output["vmstate"] = appLog.ApplicationManifest.VmState;
            output["gasconsumed"] = appLog.ApplicationManifest.GasConsumed;

            try
            {
                output["stack"] = appLog.ApplicationManifest.Stack.Select(s => s.ToJson(Settings.Default.MaxStackSize)).ToArray();
            }
            catch (Exception ex)
            {
                output["exception"] = ex.Message;
            }

            output["notifications"] = appLog.Notifications.Select(s =>
            {
                var notification = new JObject();
                notification["contract"] = s.ScriptHash.ToString();
                notification["eventname"] = s.EventName;

                output["blockhash"] = s.BlockHash.ToString();

                try
                {
                    notification["state"] = s.State.Select(ss => ss.ToJson()).ToArray();
                }
                catch (InvalidOperationException)
                {
                    notification["state"] = "error: recursive reference";
                }

                return notification;
            }).ToArray();

            return output;
        }

        public (ApplicationLogManifest ApplicationManifest, NotifyLogManifest[] Notifications) GetTransactionLog(UInt256 txHash)
        {
            var appLogKey = new KeyBuilder(Prefix_Id, Prefix_ApplicationLog).Add(txHash).ToArray();

            var appLogData = _db.TryGet(appLogKey);
            var appManifest = appLogData?.AsSerializable<ApplicationLogManifest>();

            var txKey = new KeyBuilder(Prefix_Id, Prefix_ApplicationLog_Transaction).Add(txHash).ToArray();
            var nManifests = new List<NotifyLogManifest>();
            foreach (var (key, value) in _db.Seek(txKey, SeekDirection.Forward))
            {
                if (key.AsSpan().StartsWith(txKey))
                {
                    var txNotifyLogData = _db.TryGet(value);
                    nManifests.Add(txNotifyLogData.AsSerializable<NotifyLogManifest>());
                }
            }

            return (appManifest, nManifests.ToArray());
        }

        public IEnumerable<NotifyLogManifest> Find(UInt160 scriptHash, uint page = 1, uint pageSize = 50)
        {
            var prefixKey = new KeyBuilder(Prefix_Id, Prefix_ApplicationLog_Notify)
                .Add(scriptHash).ToArray();
            var searchPrefix = new KeyBuilder(Prefix_Id, Prefix_ApplicationLog_Notify)
                .Add(scriptHash).AddBigEndian(ulong.MaxValue).ToArray();

            uint index = 1;
            foreach (var (key, value) in _db.Seek(searchPrefix, SeekDirection.Backward))
            {
                if (key.AsSpan().StartsWith(prefixKey))
                {
                    if (index >= page && index < (pageSize + page))
                        yield return value.AsSerializable<NotifyLogManifest>();
                    index++;
                }
                else
                    yield break;
            }
        }

        #region Blockchain Events

        private void OnCommitting(NeoSystem system, Block block, DataCache snapshot, IReadOnlyList<Blockchain.ApplicationExecuted> applicationExecutedList)
        {
            if (system.Settings.Network != Settings.Default.Network) return;

            if (_db is null) return;
            ResetBatch();

            foreach (var item in applicationExecutedList.Where(w => w.Transaction != null))
                Put(block, item);
        }

        private void OnCommitted(NeoSystem system, Block block)
        {
            if (system.Settings.Network != Settings.Default.Network) return;
            _snapshot?.Commit();
        }

        #endregion

        #region Private Methods

        private void ResetBatch()
        {
            _snapshot?.Dispose();
            _snapshot = _db.GetSnapshot();
        }

        private void Put(Block block, Blockchain.ApplicationExecuted applicationExecuted)
        {
            var appManifest = ApplicationLogManifest.Create(applicationExecuted);
            var appLogKey = new KeyBuilder(Prefix_Id, Prefix_ApplicationLog).Add(applicationExecuted.Transaction.Hash).ToArray();
            _snapshot.Put(appLogKey, appManifest.ToArray());

            if (applicationExecuted.Notifications.Length == 0) return;

            var notifications = applicationExecuted.Notifications;
            for (int i = (notifications.Length - 1); i != -1; i--)
            {
                var notifyManifest = NotifyLogManifest.Create(notifications[i], block.Hash);
                var notifyLogKey = new KeyBuilder(Prefix_Id, Prefix_ApplicationLog_Notify)
                    .Add(notifications[i].ScriptHash)
                    .AddBigEndian(block.Timestamp)
                    .AddBigEndian(unchecked((uint)i))
                    .ToArray();
                _snapshot.Put(notifyLogKey, notifyManifest.ToArray());

                var txKey = new KeyBuilder(Prefix_Id, Prefix_ApplicationLog_Transaction)
                    .Add(applicationExecuted.Transaction.Hash)
                    .AddBigEndian(unchecked((uint)i))
                    .ToArray();
                _snapshot.Put(txKey, notifyLogKey);
            }
        }

        #endregion
    }
}
