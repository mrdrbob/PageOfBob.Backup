using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PageOfBob.Backup.FileSystem
{
    public class FileSystemSource : ISource
    {
        readonly string basePath;

        public FileSystemSource(string basePath)
        {
            this.basePath = basePath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }

        string PartialPath(string fullPath) => fullPath.Substring(basePath.Length + 1);

        string FullPath(string partialPath) => partialPath == null ? basePath : Path.Combine(basePath, partialPath);

        public Task<FileEntry> GetFileInfoAsync(string path)
        {
            var fullPath = FullPath(path);
            if (!File.Exists(fullPath))
                return Task.FromResult<FileEntry>(null);
            return Task.FromResult(GetFileInfo(fullPath, path));
        }

        FileEntry GetFileInfo(string fullPath, string partialPath)
        {
            var fileInfo = new FileInfo(fullPath);
            return new FileEntry
            {
                Path = partialPath,
                Created = fileInfo.CreationTimeUtc.Ticks,
                LastModified = fileInfo.LastWriteTimeUtc.Ticks,
                Size = fileInfo.Length
            };
        }

        public async Task ProcessFiles(CancellationToken cancellationToken, Func<FileEntry, Task> action)
        {
            var stack = new Stack<string>();
            stack.Push(FullPath(basePath));

            while (stack.Count > 0 && !cancellationToken.IsCancellationRequested)
            {
                var path = stack.Pop();

                IEnumerable<string> directories = Enumerable.Empty<string>();
                try
                {
                    directories = Directory.GetDirectories(path);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"DIR ERR: {path} - {ex.Message}");
                    directories = Enumerable.Empty<string>();
                }

                foreach (var directory in directories)
                {
                    stack.Push(directory);
                }


                IEnumerable<string> files = Enumerable.Empty<string>();
                try
                {
                    files = Directory.GetFiles(path);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"DIR ERR: {path} - {ex.Message}");
                    files = Enumerable.Empty<string>();
                }

                foreach (var file in files)
                {
                    if (cancellationToken.IsCancellationRequested)
                        return;

                    var info = GetFileInfo(file, PartialPath(file));

                    await action(info);
                }
            }
        }
            

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
