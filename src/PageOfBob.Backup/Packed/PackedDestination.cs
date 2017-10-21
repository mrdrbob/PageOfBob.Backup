using ProtoBuf;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using static PageOfBob.Backup.StreamFunctions;

namespace PageOfBob.Backup.Packed
{
    public class PackedDestination : IDestination
    {
        public static class Keys
        {
            public const string PackHead = "packhead";
        }

        public IDestinationWithPartialRead Destination { get; }
        public long PackSize { get; set; } = 500 * 1024 * 1024;
        public int MaxPackFilesToCache { get; set; } = 5;
        IDictionary<string, PackIndexEntry> entries;
        string head;

        public PackedDestination(IDestinationWithPartialRead destination)
        {
            Destination = destination;
        }

        public async Task InitAsync()
        {
            await Destination.InitAsync();
            await EnsureIndex();
        }

        public Task<bool> DeleteAsync(string key)
            => Destination.DeleteAsync(key);

        PackAndPosition FindEntry(string key)
        {
            var indexKey = head;
            while (indexKey != null)
            {
                var entry = entries[indexKey];
                var matchingPosition = entry.Positions.FirstOrDefault(y => y.Key == key);
                if (matchingPosition != null)
                    return (matchingPosition.Position >= 0) ? new PackAndPosition { PackKey = entry.PackKey, Position = matchingPosition } : null;

                indexKey = entry.ParentKey;
            }

            return null;
        }

        public async Task<bool> ExistsAsync(string key, ReadOptions readOptions)
        {
            if ((readOptions & ReadOptions.FromLocalCache) != 0)
                return await Destination.ExistsAsync(key, readOptions);

            return FindEntry(key) != null;
        }

        public async Task FlushAsync()
        {
            if (currentWriter != null)
            {
                await currentWriter.FlushAsync();
                currentWriter = null;
            }

            await Destination.FlushAsync();
        }

        public Task<bool> ReadAsync(string key, ReadOptions readOptions, ProcessStream readAction)
        {
            if ((readOptions & ReadOptions.FromLocalCache) != 0)
                return Destination.ReadAsync(key, readOptions, readAction);

            var position = FindEntry(key);
            if (position == null)
                return Task.FromResult(false);

            return Destination.ReadAsync(position.PackKey, position.Position.Position, position.Position.Position + position.Position.Length, readOptions, readAction);
        }

        public async Task<bool> WriteAsync(string key, WriteOptions writeOptions, ProcessStream writeAction)
        {
            if ((writeOptions & WriteOptions.CacheLocally) != 0)
                return await Destination.WriteAsync(key, writeOptions, writeAction);

            bool overwrite = (writeOptions & WriteOptions.Overwrite) != 0;
            if (!overwrite && await ExistsAsync(key, ReadOptions.None))
                return false;

            var writer = EnsureWriter();

            string newHead = await writer.WriteAsync(key, writeAction);
            if (newHead != null)
            {
                entries.Add(newHead, writer.Entry);

                currentWriter = null;
                head = newHead;
            }

            return true;
        }

        #region Index
        async Task EnsureIndex()
        {
            if (entries != null)
                return;

            entries = new Dictionary<string, PackIndexEntry>();

            head = await ReadStringAsync((proc) => Destination.ReadAsync(Keys.PackHead, ReadOptions.FromLocalCache, proc));
            string key = head;
            while (key != null)
            {
                var entry = await ReadObjectAsync<PackIndexEntry>((proc) => Destination.ReadAsync(key, ReadOptions.FromLocalCache, proc));
                entries.Add(key, entry);
                key = entry.ParentKey;
            }
        }
        #endregion

        #region Pack Writer
        PackWriter currentWriter = null;
        PackWriter EnsureWriter()
        {
            if (currentWriter == null)
            {
                currentWriter = new PackWriter(this, head);
            }
            return currentWriter;
        }

        [ProtoContract]
        class PackIndexEntry
        {
            [ProtoMember(1)]
            public string ParentKey { get; set; }

            [ProtoMember(2)]
            public string PackKey { get; set; }

            [ProtoMember(3)]
            public IList<PackPosition> Positions { get; set; } = new List<PackPosition>();
        }

        [ProtoContract]
        class PackPosition
        {
            [ProtoMember(1)]
            public string Key { get; set; }

            [ProtoMember(2)]
            public long Position { get; set; }

            [ProtoMember(3)]
            public long Length { get; set; }
        }

        class PackAndPosition
        {
            public string PackKey { get; set; }
            public PackPosition Position { get; set; }
        }

        class PackWriter
        {
            public PackedDestination Parent { get; }
            public PackIndexEntry Entry { get; }
            readonly string path;
            readonly Stream stream;

            public PackWriter(PackedDestination parent, string parentKey)
            {
                Parent = parent;

                Entry = new PackIndexEntry
                {
                    ParentKey = parentKey
                };

                path = Path.GetTempFileName();
                stream = File.Open(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            }

            public async Task<string> WriteAsync(string key, ProcessStream writeAction)
            {
                long position = stream.Position;
                await writeAction(stream);
                await stream.FlushAsync();

                Entry.Positions.Add(new PackPosition
                {
                    Key = key,
                    Position = position,
                    Length = stream.Position - position
                });

                long newSize = stream.Position;

                if (newSize < Parent.PackSize)
                    return null;

                return await FlushAsync();
            }

            public async Task<string> FlushAsync()
            {
                Console.WriteLine("BEGIN FLUSH");
                stream.Seek(0, SeekOrigin.Begin);
                Entry.PackKey = stream.CalculateHashOnStream();

                Console.WriteLine($"WRITING {Entry.PackKey}");
                stream.Seek(0, SeekOrigin.Begin);
                await Parent.Destination.WriteAsync(Entry.PackKey, WriteOptions.None, CopyToStream(stream));

                var packIndexKey = await CalculateHashAndWrite(
                    Entry,
                    (hash, str) => Parent.Destination.WriteAsync(hash, WriteOptions.CacheLocally, CopyToStream(str))
                );

                await Parent.Destination.WriteAsync(Keys.PackHead, WriteOptions.CacheLocally | WriteOptions.Overwrite, WriteStringAsync(packIndexKey));
                Console.WriteLine($"FLUSHED");

                stream.Dispose();
                File.Delete(path);

                return packIndexKey;
            }
        }
        #endregion

        #region Local Pack File Cache
        class LocalPackCache
        {
            IList<LocalPackFile> cachedFiles = new List<LocalPackFile>();
            readonly PackedDestination parent;
            int access = 0;
            public LocalPackCache(PackedDestination parent)
            {
                this.parent = parent;
            }

            async Task ReadAsync(string packFileKey, PackPosition position, ProcessStream readAction)
            {
                var cached = cachedFiles.FirstOrDefault(x => x.Key ==  packFileKey);
                if (cached == null)
                {
                    while (cachedFiles.Count >= parent.MaxPackFilesToCache)
                    {
                        var oldest = cachedFiles.OrderBy(x => x.LastAccess).First();
                        File.Delete(oldest.Path);
                        cachedFiles.Remove(oldest);
                    }

                    cached = new LocalPackFile
                    {
                        Key = packFileKey,
                        Path = Path.GetTempFileName(),
                        LastAccess = access
                    };

                    using (var fileStream = File.Open(cached.Path, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite))
                    {
                        await parent.Destination.ReadAsync(packFileKey, ReadOptions.None, CopyToStream(fileStream));
                    }
                }
                else
                {
                    cached.LastAccess = access;
                }

                using (var memoryStream = GlobalContext.MemoryStreamManager.GetStream())
                {
                    using (var readStream = File.OpenRead(cached.Path))
                    {
                        byte[] buffer = new byte[4 * 1024];
                        readStream.Seek(position.Position, SeekOrigin.Begin);
                        long currentPos = position.Position;
                        int totalBytesRead = 0;

                        while (totalBytesRead < position.Length)
                        {
                            long bytesToTake = Math.Min(buffer.Length, position.Length - currentPos);
                            int bytesRead = await readStream.ReadAsync(buffer, 0, (int)bytesToTake);
                            memoryStream.Write(buffer, 0, bytesRead);
                            totalBytesRead += bytesRead;
                        }
                    }

                    memoryStream.Seek(0, SeekOrigin.Begin);
                    await readAction(memoryStream);
                }

                access += 1;
            }
        }

        class LocalPackFile
        {
            public string Key { get; set; }
            public string Path { get; set; }
            public int LastAccess { get; set; }
        }
        #endregion
    }
}
