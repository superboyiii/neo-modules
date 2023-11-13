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

namespace Neo.Plugins.Store.States
{
    public class TransactionEngineLogState : ISerializable, IEquatable<TransactionEngineLogState>
    {
        public Guid[] LogIds { get; set; } = Array.Empty<Guid>();

        public static TransactionEngineLogState Create(Guid[] logIds) =>
            new()
            {
                LogIds = logIds,
            };

        #region ISerializable

        public virtual int Size =>
            sizeof(uint) +
            LogIds.Sum(s => s.ToByteArray().GetVarSize());

        public virtual void Deserialize(ref MemoryReader reader)
        {
            uint aLen = reader.ReadUInt32();
            LogIds = new Guid[aLen];
            for (int i = 0; i < aLen; i++)
                LogIds[i] = new Guid(reader.ReadVarMemory().Span);
        }

        public virtual void Serialize(BinaryWriter writer)
        {
            writer.Write((uint)LogIds.Length);
            for (int i = 0; i < LogIds.Length; i++)
                writer.WriteVarBytes(LogIds[i].ToByteArray());
        }

        #endregion

        #region IEquatable

        public bool Equals(TransactionEngineLogState other) =>
            LogIds.SequenceEqual(other.LogIds);

        public override bool Equals(object obj) =>
            Equals(obj as TransactionEngineLogState);

        public override int GetHashCode() =>
            LogIds.Sum(s => s.GetHashCode());

        #endregion
    }
}
