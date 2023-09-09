using FASTER.core;
using System.IO;
using System;
using System.Linq;

namespace Neo.Plugins.Storage
{
    public class NeoCheckpointNamingScheme : ICheckpointNamingScheme
    {
        private readonly string baseName;

        public string IndexCheckpointBasePath() => "index";

        public string LogCheckpointBasePath() => "log";

        public string FasterLogCommitBasePath() => "commits";

        public NeoCheckpointNamingScheme(string baseName = "")
        {
            this.baseName = baseName;
        }

        public string BaseName() => baseName;

        public FileDescriptor LogCheckpointBase(Guid token) =>
            new($"{LogCheckpointBasePath()}/{token}", null);

        public FileDescriptor LogCheckpointMetadata(Guid token) =>
            new($"{LogCheckpointBasePath()}/{token}", "INFO");

        public FileDescriptor LogSnapshot(Guid token) =>
            new($"{LogCheckpointBasePath()}/{token}", "LOG");

        public FileDescriptor ObjectLogSnapshot(Guid token) =>
            new($"{LogCheckpointBasePath()}/{token}", "DATA");

        public FileDescriptor DeltaLog(Guid token) =>
            new($"{LogCheckpointBasePath()}/{token}", "DELTA");

        public FileDescriptor IndexCheckpointBase(Guid token) =>
            new($"{IndexCheckpointBasePath()}/{token}", null);

        public FileDescriptor IndexCheckpointMetadata(Guid token) =>
            new($"{IndexCheckpointBasePath()}/{token}", "INFO");

        public FileDescriptor HashTable(Guid token) =>
            new($"{IndexCheckpointBasePath()}/{token}", "TABLE");

        public FileDescriptor FasterLogCommitMetadata(long commitNumber) =>
            new($"{FasterLogCommitBasePath()}", $"COMMIT.{commitNumber}");

        public Guid Token(FileDescriptor fileDescriptor) =>
            Guid.Parse(new DirectoryInfo(fileDescriptor.directoryName).Name);

        public long CommitNumber(FileDescriptor fileDescriptor) =>
            long.Parse(fileDescriptor.fileName.Split('.').Reverse().Take(2).Last());
    }
}
