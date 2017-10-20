using System.Collections.Generic;
using System.Threading.Tasks;

namespace PageOfBob.Backup
{
    public interface ISource
    {
        Task<IEnumerable<string>> ListFilesAsync(string path);
        Task<IEnumerable<string>> ListDirectoriesAsync(string path);
        Task<FileEntry> GetFileInfoAsync(string path);

        Task ReadFileAsync(string path, ProcessStream function);
        Task WriteFileAsync(FileEntry entry, ProcessStream function);
    }
}
