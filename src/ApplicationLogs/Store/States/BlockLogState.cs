using Neo;
using Neo.IO;

namespace ApplicationLogs.Store.States
{
    public class BlockLogState : ISerializable, IEquatable<BlockLogState>
    {
        public Guid[] NotifyLogIds { get; set; } = Array.Empty<Guid>();

        public static BlockLogState Create(Guid[] notifyLogIds) =>
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

        public bool Equals(BlockLogState other) =>
            NotifyLogIds.SequenceEqual(other.NotifyLogIds);

        public override bool Equals(object obj) =>
            Equals(obj as BlockLogState);

        public override int GetHashCode() =>
            NotifyLogIds.Sum(s => s.GetHashCode());

        #endregion
    }
}
