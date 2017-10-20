using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace PageOfBob.Backup.FileSystem
{
    public class FileSystemSource : ISource
    {
        readonly string basePath;

        public FileSystemSource(string basePath)
        {
            this.basePath = basePath.TrimEnd('\\');
        }

        string PartialPath(string fullPath) => fullPath.Substring(basePath.Length + 1);

        string FullPath(string partialPath) => partialPath == null ? basePath : Path.Combine(basePath, partialPath);

        public Task<FileEntry> GetFileInfoAsync(string path)
        {
            var fullPath = FullPath(path);
            if (!File.Exists(fullPath))
                return Task.FromResult<FileEntry>(null);

            var fileInfo = new FileInfo(FullPath(path));
            return Task.FromResult(new FileEntry
            {
                Path = path,
                Created = fileInfo.CreationTimeUtc.Ticks,
                LastModified = fileInfo.LastWriteTimeUtc.Ticks,
                Size = fileInfo.Length
            });
        }

        public Task<IEnumerable<string>> ListDirectoriesAsync(string path)
            => Task.FromResult(Directory.GetDirectories(FullPath(path)).Select(PartialPath));

        public Task<IEnumerable<string>> ListFilesAsync(string path)
            => Task.FromResult(Directory.GetFiles(FullPath(path)).Select(PartialPath));

        public async Task ReadFileAsync(string path, ProcessStream function)
        {
            using (var stream = File.OpenRead(FullPath(path)))
            {
                await function.Invoke(stream);
            }
        }

        public async Task WriteFileAsync(FileEntry entry, ProcessStream function)
        {
            string fullPath = FullPath(entry.Path);

            var directory = Path.GetDirectoryName(fullPath);
            if (!Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            using (var stream = File.Create(fullPath))
            {
                await function.Invoke(stream);
            }

            var created = new DateTime(entry.Created);
            File.SetCreationTimeUtc(fullPath, created);

            var lastModified = new DateTime(entry.LastModified);
            File.SetLastWriteTimeUtc(fullPath, lastModified);
        }
    }
}
