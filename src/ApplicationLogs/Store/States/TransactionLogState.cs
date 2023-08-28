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

namespace ApplicationLogs.Store.States
{
    public class TransactionLogState : ISerializable, IEquatable<TransactionLogState>
    {
        public Guid[] NotifyLogIds { get; set; } = Array.Empty<Guid>();

        public static TransactionLogState Create(Guid[] notifyLogIds) =>
            new()
            {
                NotifyLogIds = notifyLogIds,
            };

        #region ISerializable

        public virtual int Size =>
            sizeof(uint) +
            NotifyLogIds.Sum(s => s.ToByteArray().GetVarSize());

        public virtual void Deserialize(ref MemoryReader reader)
        {
            uint aLen = reader.ReadUInt32();
            NotifyLogIds = new Guid[aLen];
            for (int i = 0; i < aLen; i++)
                NotifyLogIds[i] = new Guid(reader.ReadVarMemory().Span);
        }

        public virtual void Serialize(BinaryWriter writer)
        {
            writer.Write((uint)NotifyLogIds.Length);
            for (int i = 0; i < NotifyLogIds.Length; i++)
                writer.WriteVarBytes(NotifyLogIds[i].ToByteArray());
        }

        #endregion

        #region IEquatable

        public bool Equals(TransactionLogState other) =>
            NotifyLogIds.SequenceEqual(other.NotifyLogIds);

        public override bool Equals(object obj) =>
            Equals(obj as TransactionLogState);

        public override int GetHashCode() =>
            NotifyLogIds.Sum(s => s.GetHashCode());

        #endregion
    }
}