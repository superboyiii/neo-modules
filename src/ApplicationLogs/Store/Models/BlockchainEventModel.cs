// Copyright (C) 2015-2023 The Neo Project.
//
// The Neo.Plugins.ApplicationLogs is free software distributed under the MIT software license,
// see the accompanying file LICENSE in the main directory of the
// project or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using ApplicationLogs.Store.States;
using Neo;
using Neo.VM.Types;
using Array = System.Array;

namespace ApplicationLogs.Store.Models
{
    public class BlockchainEventModel
    {
        public UInt160 ScriptHash { get; set; } = new();
        public string EventName { get; set; } = string.Empty;
        public StackItem[] State { get; set; } = Array.Empty<StackItem>();

        public static BlockchainEventModel Create(UInt160 scriptHash, string eventName, StackItem[] state) =>
            new()
            {
                ScriptHash = scriptHash,
                EventName = eventName ?? string.Empty,
                State = state,
            };

        public static BlockchainEventModel Create(NotifyLogState notifyLogState, StackItem[] state) =>
            new()
            {
                ScriptHash = notifyLogState.ScriptHash,
                EventName = notifyLogState.EventName,
                State = state,
            };

        public static BlockchainEventModel Create(ContractLogState contractLogState, StackItem[] state) =>
            new()
            {
                ScriptHash = contractLogState.ScriptHash,
                EventName = contractLogState.EventName,
                State = state,
            };
    }
}