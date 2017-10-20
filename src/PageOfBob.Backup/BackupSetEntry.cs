using ProtoBuf;
using System.Collections.Generic;

namespace PageOfBob.Backup
{
    [ProtoContract]
    public class BackupSetEntry
    {
        [ProtoMember(1)]
        public string ParentKey { get; set; }

        [ProtoMember(2)]
        public long Completed { get; set; }

        [ProtoMember(3)]
        public IList<FileEntry> Entries { get; set; } = new List<FileEntry>();
    }
}
