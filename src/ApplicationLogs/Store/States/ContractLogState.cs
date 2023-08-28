// Copyright (C) 2015-2023 The Neo Project.
//
// The Neo.Plugins.ApplicationLogs is free software distributed under the MIT software license,
// see the accompanying file LICENSE in the main directory of the
// project or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo;
using Neo.IO;
using Neo.Ledger;
using Neo.SmartContract;

namespace ApplicationLogs.Store.States
{
    public class ContractLogState : NotifyLogState, IEquatable<ContractLogState>
    {
        public UInt256 TransactionHash { get; set; } = new();
        public TriggerType Trigger { get; set; } = TriggerType.All;

        public static ContractLogState Create(Blockchain.ApplicationExecuted applicationExecuted, NotifyEventArgs notifyEventArgs, Guid[] stackItemIds) =>
            new()
            {
                TransactionHash = applicationExecuted.Transaction?.Hash ?? new(),
                ScriptHash = notifyEventArgs.ScriptHash,
                Trigger = applicationExecuted.Trigger,
                EventName = notifyEventArgs.EventName,
                StackItemIds = stackItemIds,
            };

        #region ISerializable

        public override int Size =>
            TransactionHash.Size +
            sizeof(byte) +
            base.Size;

        public override void Deserialize(ref MemoryReader reader)
        {
            TransactionHash.Deserialize(ref reader);
            Trigger = (TriggerType)reader.ReadByte();
            base.Deserialize(ref reader);
        }

        public override void Serialize(BinaryWriter writer)
        {
            TransactionHash.Serialize(writer);
            writer.Write((byte)Trigger);
            base.Serialize(writer);
        }

        #endregion

        #region IEquatable

        public bool Equals(ContractLogState other) =>
            EventName == other.EventName && StackItemIds.SequenceEqual(other.StackItemIds) &&
            TransactionHash == other.TransactionHash && Trigger == other.Trigger;

        public override bool Equals(object obj) =>
            Equals(obj as ContractLogState);

        public override int GetHashCode() =>
            TransactionHash.GetHashCode() + Trigger.GetHashCode() + base.GetHashCode();

        #endregion
    }
}