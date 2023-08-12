// Copyright (C) 2015-2023 The Neo Project.
//
// The Neo.Plugins.ApplicationLogs is free software distributed under the MIT software license,
// see the accompanying file LICENSE in the main directory of the
// project or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using ApplicationLogs.Store;
using ApplicationLogs.Store.Models;
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
    public class LogReader : Plugin
    {
        #region Globals

        private NeoStore _neostore;

        #endregion

        public override string Name => "ApplicationLogs";
        public override string Description => "Synchronizes the smart contract log with the NativeContract log (Notify)";

        #region Ctor

        public LogReader()
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
            _neostore?.Dispose();
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
            var store = system.LoadStore(GetFullPath(path));
            _neostore = new NeoStore(store);
            RpcServerPlugin.RegisterMethods(this, Settings.Default.Network);
        }

        #endregion

        [RpcMethod]
        public JToken GetApplicationLog(JArray _params)
        {
            UInt256 hash = UInt256.Parse(_params[0].AsString());
            var raw = BlockToJObject(hash);
            if (raw == null)
                raw = TransactionToJObject(hash);
            if (raw == null)
                throw new RpcException(-100, "Unknown transaction/blockhash");

            if (_params.Count >= 2 && Enum.TryParse(_params[1].AsString(), true, out TriggerType triggerType))
            {
                var executions = raw["executions"] as JArray;
                for (int i = 0; i < executions.Count;)
                {
                    if (executions[i]["trigger"].AsString().Equals(triggerType.ToString(), StringComparison.OrdinalIgnoreCase) == false)
                        executions.RemoveAt(i);
                    else
                        i++;
                }
            }

            return raw ?? JToken.Null;
        }

        #region Blockchain Events

        private void OnCommitting(NeoSystem system, Block block, DataCache snapshot, IReadOnlyList<Blockchain.ApplicationExecuted> applicationExecutedList)
        {
            if (system.Settings.Network != Settings.Default.Network) return;

            if (_neostore is null) return;
            _neostore.StartBlockLogBatch();
            _neostore.PutBlockLog(block, applicationExecutedList);
        }

        private void OnCommitted(NeoSystem system, Block block)
        {
            if (system.Settings.Network != Settings.Default.Network) return;
            if (_neostore is null) return;
            _neostore.CommitBlockLog();
        }

        #endregion

        #region Private Methods

        private JObject TransactionToJObject(UInt256 txHash)
        {
            var appLog = _neostore.GetTransactionLog(txHash, TriggerType.Application);
            if (appLog == null)
                return null;

            var raw = new JObject();
            raw["txid"] = appLog.TxHash.ToString();

            var trigger = new JObject();
            trigger["trigger"] = appLog.Trigger;
            trigger["vmstate"] = appLog.VmState;
            trigger["exception"] = string.IsNullOrEmpty(appLog.Exception) ? null : appLog.Exception;
            trigger["gasconsumed"] = appLog.GasConsumed.ToString();

            try
            {
                trigger["stack"] = appLog.Stack.Select(s => s.ToJson(Settings.Default.MaxStackSize)).ToArray();
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
            var blockOnPersist = _neostore.GetBlockLog(blockHash, TriggerType.OnPersist);
            var blockPostPersist = _neostore.GetBlockLog(blockHash, TriggerType.PostPersist);
            //var blockApplication = _neostore.GetBlockLog(blockHash, TriggerType.Application);

            if (blockOnPersist == null && blockPostPersist == null) return null;

            var blockJson = new JObject();
            blockJson["blockhash"] = blockHash.ToString();
            var triggerList = new List<JObject>();

            if (blockOnPersist != null)
                triggerList.Add(BlockItemToJObject(blockOnPersist));
            if (blockPostPersist != null)
                triggerList.Add(BlockItemToJObject(blockPostPersist));
            //if (blockApplication != null)
            //    triggerList.Add(BlockItemToJObject(blockApplication));

            blockJson["executions"] = triggerList.ToArray();
            return blockJson;
        }

        private JObject BlockItemToJObject(BlockchainExecutionModel blockExecutionModel)
        {
            JObject trigger = new();
            trigger["trigger"] = blockExecutionModel.Trigger;
            trigger["vmstate"] = blockExecutionModel.VmState;
            trigger["gasconsumed"] = blockExecutionModel.GasConsumed.ToString();
            try
            {
                trigger["stack"] = blockExecutionModel.Stack.Select(q => q.ToJson(Settings.Default.MaxStackSize)).ToArray();
            }
            catch (Exception ex)
            {
                trigger["exception"] = ex.Message;
            }
            trigger["notifications"] = blockExecutionModel.Notifications.Select(s =>
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

            return trigger;
        }

        #endregion
    }
}
