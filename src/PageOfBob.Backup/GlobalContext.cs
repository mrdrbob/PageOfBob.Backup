using Microsoft.IO;

namespace PageOfBob.Backup
{
    public static class GlobalContext
    {
        public static RecyclableMemoryStreamManager MemoryStreamManager { get; } = new RecyclableMemoryStreamManager();
    }
}
