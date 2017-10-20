using ProtoBuf;
using System.Collections.Generic;

namespace PageOfBob.Backup
{
    [ProtoContract]
    public class FileEntry
    {
        [ProtoMember(1)]
        public string Path { get; set; }

        [ProtoMember(2)]
        public long Created { get; set; }

        [ProtoMember(3)]
        public long LastModified { get; set; }

        [ProtoMember(4)]
        public long Size { get; set; }

        [ProtoMember(5)]
        public bool IsCompressed { get; set; }

        [ProtoMember(6)]
        public IList<string> SubHashes { get; } = new List<string>();
    }
}
