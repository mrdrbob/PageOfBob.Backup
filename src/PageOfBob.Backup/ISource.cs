using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace PageOfBob.Backup
{
    public interface ISource
    {
        Task ProcessFiles(CancellationToken cancellationToken, Func<FileEntry, Task> action);
        Task<FileEntry> GetFileInfoAsync(string path);

        Task ReadFileAsync(string path, ProcessStream function);
        Task WriteFileAsync(FileEntry entry, ProcessStream function);
    }
}
