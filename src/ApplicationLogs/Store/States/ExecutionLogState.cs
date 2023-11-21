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
using Neo.Ledger;
using Neo.VM;

namespace ApplicationLogs.Store.States
{
    public class ExecutionLogState : ISerializable, IEquatable<ExecutionLogState>
    {
        public VMState VmState { get; set; } = VMState.NONE;
        public string Exception { get; set; } = string.Empty;
        public long GasConsumed { get; set; } = 0L;
        public Guid[] StackItemIds { get; set; } = Array.Empty<Guid>();

        public static ExecutionLogState Create(Blockchain.ApplicationExecuted appExecution, Guid[] stackItemIds) =>
            new()
            {
                VmState = appExecution.VMState,
                Exception = appExecution.Exception?.InnerException?.Message ?? appExecution.Exception?.Message,
                GasConsumed = appExecution.GasConsumed,
                StackItemIds = stackItemIds,
            };

        #region ISerializable

        public int Size =>
            sizeof(byte) +
            Exception.GetVarSize() +
            sizeof(long) +
            sizeof(uint) +
            StackItemIds.Sum(s => s.ToByteArray().GetVarSize());

        public void Deserialize(ref MemoryReader reader)
        {
            VmState = (VMState)reader.ReadByte();
            Exception = reader.ReadVarString();
            GasConsumed = reader.ReadInt64();

            uint aLen = reader.ReadUInt32();
            StackItemIds = new Guid[aLen];
            for (int i = 0; i < aLen; i++)
                StackItemIds[i] = new Guid(reader.ReadVarMemory().Span);
        }

        public void Serialize(BinaryWriter writer)
        {
            writer.Write((byte)VmState);
            writer.WriteVarString(Exception ?? string.Empty);
            writer.Write(GasConsumed);

            writer.Write((uint)StackItemIds.Length);
            for (int i = 0; i < StackItemIds.Length; i++)
                writer.WriteVarBytes(StackItemIds[i].ToByteArray());
        }

        #endregion

        #region IEquatable

        public bool Equals(ExecutionLogState other) =>
            VmState == other.VmState && Exception == other.Exception &&
            GasConsumed == other.GasConsumed && StackItemIds.SequenceEqual(other.StackItemIds);

        public override bool Equals(object obj) =>
            Equals(obj as ExecutionLogState);

        public override int GetHashCode() =>
            VmState.GetHashCode() + Exception.GetHashCode() +
            GasConsumed.GetHashCode() + StackItemIds.Sum(s => s.GetHashCode());

        #endregion
    }
}
