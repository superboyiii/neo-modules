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
        internal static readonly byte Prefix_ApplicationLog_Block = 13;

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
            var raw = TransactionToJObject(hash);
            if (raw == null)
                raw = BlockToJObject(hash);
            if (raw == null)
                throw new RpcException(-100, "Unknown transaction/blockhash");

            if (_params.Count >= 2 && Enum.TryParse(_params[1].AsString(), true, out TriggerType triggerType))
            {
                var executions = raw["executions"] as JArray;
                for (int i = 0; i < executions.Count;)
                {
                    if (executions[i]["trigger"].AsString().Equals(triggerType.ToString(), StringComparison.InvariantCultureIgnoreCase) == false)
                        executions.RemoveAt(i);
                    else
                        i++;
                }
            }

            return raw;
        }

        public IEnumerable<(ApplicationLogManifest ApplicationManifest, NotifyLogManifest[] Notifications)> GetBlockLog(UInt256 blockHash)
        {
            var appLogKey = new KeyBuilder(Prefix_Id, Prefix_ApplicationLog).Add(blockHash).ToArray();

            foreach (var (key, value) in _db.Seek(appLogKey, SeekDirection.Forward))
            {
                if (key.AsSpan().StartsWith(appLogKey))
                {
                    var tigger = key.AsSpan(sizeof(int) + sizeof(byte) + UInt256.Length)[0];
                    var appManifest = value?.AsSerializable<ApplicationLogManifest>();

                    var blockKey = new KeyBuilder(Prefix_Id, Prefix_ApplicationLog_Block)
                        .Add(blockHash).Add(tigger).ToArray();
                    
                    var nManifests = new List<NotifyLogManifest>();
                    foreach (var (notifyKey, notifyValue) in _db.Seek(blockKey, SeekDirection.Forward))
                    {
                        if (notifyKey.AsSpan().StartsWith(blockKey))
                        {
                            var txNotifyLogData = _db.TryGet(notifyValue);
                            nManifests.Add(txNotifyLogData.AsSerializable<NotifyLogManifest>());
                        }
                    }

                    yield return (appManifest, nManifests.ToArray());
                }
                else
                    yield break;
            }
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

        #region Blockchain Events

        private void OnCommitting(NeoSystem system, Block block, DataCache snapshot, IReadOnlyList<Blockchain.ApplicationExecuted> applicationExecutedList)
        {
            if (system.Settings.Network != Settings.Default.Network) return;

            if (_db is null) return;
            ResetBatch();

            foreach (var item in applicationExecutedList.Where(w => w.Transaction != null))
                PutTransaction(block, item);
            foreach (var item in applicationExecutedList.Where(w => w.Transaction == null))
                PutBlock(block, item);
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

        private JObject TransactionToJObject(UInt256 txHash)
        {
            var appLog = GetTransactionLog(txHash);
            if (appLog.ApplicationManifest == null && appLog.Notifications.Length == 0)
                return null;

            var raw = new JObject();
            raw["txid"] = txHash.ToString();

            var trigger = new JObject();
            trigger["trigger"] = appLog.ApplicationManifest.Trigger;
            trigger["vmstate"] = appLog.ApplicationManifest.VmState;
            trigger["exception"] = string.IsNullOrEmpty(appLog.ApplicationManifest.Exception) ? null : appLog.ApplicationManifest.Exception;
            trigger["gasconsumed"] = appLog.ApplicationManifest.GasConsumed.ToString();

            try
            {
                trigger["stack"] = appLog.ApplicationManifest.Stack.Select(s => s.ToJson(Settings.Default.MaxStackSize)).ToArray();
            }
            catch (Exception ex)
            {
                trigger["exception"] = ex.Message;
            }

            trigger["notifications"] = appLog.Notifications.Select(s =>
            {
                var notification = new JObject();
                notification["contract"] = s.ScriptHash.ToString();
                notification["eventname"] = s.EventName;

                try
                {
                    var state = new JObject();
                    state["type"] = "Array";
                    state["value"] = s.State.Select(ss => ss.ToJson()).ToArray();

                    notification["state"] = state;
                }
                catch (InvalidOperationException)
                {
                    notification["state"] = "error: recursive reference";
                }

                return notification;
            }).ToArray();

            raw["executions"] = new[] { trigger };
            return raw;
        }

        private JObject BlockToJObject(UInt256 blockHash)
        {
            var blocks = GetBlockLog(blockHash);
            if (blocks.Any())
            {
                var blockJson = new JObject();
                blockJson["blockhash"] = blockHash.ToString();
                var triggerList = new List<JObject>();
                foreach (var appExec in blocks)
                {
                    JObject trigger = new();
                    trigger["trigger"] = appExec.ApplicationManifest.Trigger;
                    trigger["vmstate"] = appExec.ApplicationManifest.VmState;
                    trigger["gasconsumed"] = appExec.ApplicationManifest.GasConsumed.ToString();
                    try
                    {
                        trigger["stack"] = appExec.ApplicationManifest.Stack.Select(q => q.ToJson(Settings.Default.MaxStackSize)).ToArray();
                    }
                    catch (Exception ex)
                    {
                        trigger["exception"] = ex.Message;
                    }
                    trigger["notifications"] = appExec.Notifications.Select(s =>
                    {
                        JObject notification = new();
                        notification["contract"] = s.ScriptHash.ToString();
                        notification["eventname"] = s.EventName;
                        try
                        {
                            var state = new JObject();
                            state["type"] = "Array";
                            state["value"] = s.State.Select(ss => ss.ToJson()).ToArray();

                            notification["state"] = state;
                        }
                        catch (InvalidOperationException)
                        {
                            notification["state"] = "error: recursive reference";
                        }
                        return notification;
                    }).ToArray();
                    triggerList.Add(trigger);
                }
                blockJson["executions"] = triggerList.ToArray();
                return blockJson;
            }

            return null;
        }

        private void PutBlock(Block block, Blockchain.ApplicationExecuted applicationExecuted)
        {
            var appManifest = ApplicationLogManifest.Create(applicationExecuted);
            var appLogKey = new KeyBuilder(Prefix_Id, Prefix_ApplicationLog)
                .Add(block.Hash).Add((byte)applicationExecuted.Trigger).ToArray();
            _snapshot.Put(appLogKey, appManifest.ToArray());

            if (applicationExecuted.Notifications.Length == 0) return;

            var notifications = applicationExecuted.Notifications;
            for (int i = 0; i < notifications.Length; i++)
            {
                var notifyManifest = NotifyLogManifest.Create(notifications[i], block.Hash, new());
                var notifyLogKey = new KeyBuilder(Prefix_Id, Prefix_ApplicationLog_Notify)
                    .Add(notifications[i].ScriptHash)
                    .AddBigEndian(block.Timestamp)
                    .Add((byte)applicationExecuted.Trigger)
                    .AddBigEndian(unchecked((uint)i))
                    .ToArray();
                _snapshot.Put(notifyLogKey, notifyManifest.ToArray());

                var blockkey = new KeyBuilder(Prefix_Id, Prefix_ApplicationLog_Block)
                    .Add(block.Hash)
                    .Add((byte)applicationExecuted.Trigger)
                    .AddBigEndian(unchecked((uint)i))
                    .ToArray();
                _snapshot.Put(blockkey, notifyLogKey);
            }
        }

        private void PutTransaction(Block block, Blockchain.ApplicationExecuted applicationExecuted)
        {
            var appManifest = ApplicationLogManifest.Create(applicationExecuted);
            var appLogKey = new KeyBuilder(Prefix_Id, Prefix_ApplicationLog).Add(applicationExecuted.Transaction.Hash).ToArray();
            _snapshot.Put(appLogKey, appManifest.ToArray());

            if (applicationExecuted.Notifications.Length == 0) return;

            var notifications = applicationExecuted.Notifications;
            for (int i = 0; i < notifications.Length; i++)
            {
                var notifyManifest = NotifyLogManifest.Create(notifications[i], block.Hash);
                var notifyLogKey = new KeyBuilder(Prefix_Id, Prefix_ApplicationLog_Notify)
                    .Add(notifications[i].ScriptHash)
                    .AddBigEndian(block.Timestamp)
                    .Add((byte)applicationExecuted.Trigger)
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
