using System;
using System.IO;
using System.Threading.Tasks;

namespace PageOfBob.Backup.FileSystem
{
    public class FileSystemDestination : IDestinationWithPartialRead
    {
        readonly string basePath;
        public int PrefixLength { get; set; } = 2;

        public FileSystemDestination(string basePath)
        {
            this.basePath = basePath;
        }

        public Task InitAsync() => Task.CompletedTask;

        public Task<bool> DeleteAsync(string key)
        {
            string path = EnsurePath(key, false);

            if (path == null || !File.Exists(path))
                return Task.FromResult(false);

            File.Delete(path);

            return Task.FromResult(false);
        }

        public Task<bool> ExistsAsync(string key, ReadOptions readOptions)
        {
            string path = EnsurePath(key, false);

            return Task.FromResult(path != null && File.Exists(path));
        }

        public async Task<bool> ReadAsync(string key, ReadOptions readOptions, ProcessStream readAction)
        {
            string path = EnsurePath(key, false);

            if (path == null || !File.Exists(path))
                return false;

            using (FileStream fileStream = File.OpenRead(path))
            {
                await readAction(fileStream);
            }
            return true;
        }

        public async Task<bool> ReadAsync(string key, long begin, long end, ReadOptions readOptions, ProcessStream readAction)
        {
            string path = EnsurePath(key, false);

            if (path == null || !File.Exists(path))
                return false;

            using (FileStream fileStream = File.OpenRead(path))
            using (PartialStream partial = new PartialStream(fileStream, begin, end))
            {
                await readAction(partial);
            }
            return true;
        }

        public async Task<bool> WriteAsync(string key, WriteOptions writeOptions, ProcessStream writeAction)
        {
            string path = EnsurePath(key, true);

            bool overwrite = (writeOptions & WriteOptions.Overwrite) != 0;
            if (!overwrite && File.Exists(path))
                return false;

            using (var fileStream = File.Create(path))
            {
                await writeAction(fileStream);
                await fileStream.FlushAsync();
            }

            return true;
        }

        string EnsurePath(string key, bool createPath)
        {
            string path = basePath;

            if (PrefixLength > 0)
            {
                int prefixSize = Math.Min(key.Length, PrefixLength);
                string dirPrefix = key.Substring(0, prefixSize);
                path = Path.Combine(path, dirPrefix);
                if (!Directory.Exists(path))
                {
                    if (createPath)
                    {
                        Directory.CreateDirectory(path);
                    }
                    else
                    {
                        return null;
                    }
                }
            }

            path = Path.Combine(path, key);
            return path;
        }

        public Task FlushAsync() => Task.CompletedTask;
    }
}
