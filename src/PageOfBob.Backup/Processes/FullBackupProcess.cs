using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using static PageOfBob.Backup.StreamFunctions;

namespace PageOfBob.Backup.Processes
{
    public class FullBackupProcessConfiguration : AbstractProcessConfiguration
    {
        public ISource Source { get; }
        public IDestination Destination { get; }
        public int WriteInProgressEvery { get; set; }
        public ShouldProcessFile ShouldBackup { get; set; } = ShouldBackupLogic.Default;
        public ShouldProcessFile ShouldCompressFile { get; set; } = CompressionLogic.Default;
        public int ChunkSize { get; set; } = 100 * 1024 * 1024;
        public bool ForceOverwrite { get; set; }

        public FullBackupProcessConfiguration(ISource source, IDestination destination)
        {
            Source = source;
            Destination = destination;
        }
    }

    public class FullBackupProcess
    {
        public static Task ExecuteBackup(FullBackupProcessConfiguration configuration)
        {
            var process = new FullBackupProcess(configuration);
            return process.Execute();
        }

        public FullBackupProcessConfiguration Configuration { get; }
        public IDictionary<string, FileEntry> PreviousEntries { get; } = new Dictionary<string, FileEntry>();
        public BackupSetEntry NewSet { get; } = new BackupSetEntry();
        long filesProcessed;

        FullBackupProcess(FullBackupProcessConfiguration configuration)
        {
            Configuration = configuration;
        }

        async Task Execute()
        {
            // Do any initialization necessary
            await Configuration.Destination.InitAsync();

            // Attempt to load last full successful backup
            await LoadHead();

            // Attempt to load last partial backup
            await PreloadExistingSet(Keys.Progress);

            // Execute actual process
            await Configuration.Source.ProcessFiles(Configuration.CancellationToken, ProcessFile);

            // Write final results
            await WriteHead();

            // Some destinations hold content locally until the final flush
            await Configuration.Destination.FlushAsync();
        }

        async Task LoadHead()
        {
            string headKey = await ReadStringAsync(proc =>
            {
                var readHeadKey = GetReadStream(proc, false);
                return Configuration.Destination.ReadAsync(Keys.Head, ReadOptions.FromLocalCache, readHeadKey);
            });
            if (headKey != null)
            {
                NewSet.ParentKey = headKey;
                await PreloadExistingSet(headKey);
            }
        }

        async Task PreloadExistingSet(string key)
        {
            BackupSetEntry set = await ReadObjectAsync<BackupSetEntry>(proc => {
                var readSet = GetReadStream(proc, true);
                return Configuration.Destination.ReadAsync(key, ReadOptions.FromLocalCache, readSet);
            });

            if (set == null)
                return;

            foreach (var file in set.Entries)
            {
                if (file.Path == null)
                {
                    continue;
                }

                PreviousEntries[file.Path] = file;
            }
        }

        async Task ProcessFile(FileEntry file)
        {
            // Skip file if it should be skipped
            if (!Configuration.ShouldBackup(file))
                return;

            // If file appears to have not changed, skip it
            if (!Configuration.ForceOverwrite
                && PreviousEntries.TryGetValue(file.Path, out FileEntry existingFile)
                && file.AppearsIdentical(existingFile))
            {
                NewSet.Entries.Add(existingFile);
                return;
            }

            // Process the file
            try
            {
                await Configuration.Source.ReadFileAsync(file.Path, (src) => ProcessFileChunks(file, src));
                NewSet.Entries.Add(file);
                Console.Out.WriteLine(file.Path);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"ERR: {file.Path} - {ex.Message}");
            }

            // Write progress if necessary
            filesProcessed += 1;
            if (!Configuration.CancellationToken.IsCancellationRequested
                && Configuration.WriteInProgressEvery > 0
                && filesProcessed >= Configuration.WriteInProgressEvery)
            {
                var writeProgress = GetWriteStream(WriteObjectAsync(NewSet), true);
                await Configuration.Destination.WriteAsync(Keys.Progress, WriteOptions.CacheLocally | WriteOptions.Overwrite, writeProgress);
                filesProcessed = 0;
            }
        }

        async Task ProcessFileChunks(FileEntry file, Stream fileStream)
        {
            file.IsCompressed = Configuration.ShouldCompressFile(file);

            long position = 0;
            byte[] buffer = new byte[4 * 1024];
            while (position < file.Size && !Configuration.CancellationToken.IsCancellationRequested)
            {
                using (var memoryStream = GlobalContext.MemoryStreamManager.GetStream())
                {
                    // Read a chunk into the memor stream
                    long readEnd = Math.Min(position + Configuration.ChunkSize, file.Size);
                    while (position < readEnd)
                    {
                        int bytesRead = await fileStream.ReadAsync(buffer, 0, buffer.Length);
                        memoryStream.Write(buffer, 0, bytesRead);
                        position += bytesRead;
                    }

                    // Process said chunk
                    memoryStream.Seek(0, SeekOrigin.Begin);
                    string chunkHash = await ProcessChunk(file, memoryStream);
                    file.SubHashes.Add(chunkHash);
                }
            }
        }

        async Task<string> ProcessChunk(FileEntry file, Stream chunk)
        {
            var hash = chunk.CalculateHashOnStream();
            chunk.Seek(0, SeekOrigin.Begin);

            var writeChunk = GetWriteStream(CopyToStream(chunk), file.IsCompressed);

            var options = WriteOptions.None;
            if (Configuration.ForceOverwrite)
                options |= WriteOptions.Overwrite;
            await Configuration.Destination.WriteAsync(hash, options, writeChunk);

            return hash;
        }

        async Task WriteHead()
        {
            // Write new head or in-progress index as necessary
            if (Configuration.CancellationToken.IsCancellationRequested)
            {
                var writeProgress = GetWriteStream(WriteObjectAsync(NewSet), true);
                await Configuration.Destination.WriteAsync(Keys.Progress, WriteOptions.CacheLocally | WriteOptions.Overwrite, writeProgress);
            }
            else
            {
                NewSet.Completed = DateTime.UtcNow.Ticks;
                var newHead = await CalculateHashAndWrite(
                    NewSet,
                    (hash, str) => {
                        var writeHeadObject = GetWriteStream(CopyToStream(str), true);
                        return Configuration.Destination.WriteAsync(hash, WriteOptions.CacheLocally, writeHeadObject);
                    }
                );
                var writeHeadKey = GetWriteStream(WriteStringAsync(newHead), false);
                await Configuration.Destination.WriteAsync(Keys.Head, WriteOptions.CacheLocally | WriteOptions.Overwrite, writeHeadKey);
                await Configuration.Destination.DeleteAsync(Keys.Progress);
            }
        }

        ProcessStream GetWriteStream(ProcessStream process, bool useCompression)
        {
            if (useCompression)
            {
                process = process.WithCompression();
            }
            if (Configuration.EncryptionKey != null)
            {
                process = process.WithEncryption(Configuration.EncryptionKey);
            }

            return process;
        }

        ProcessStream GetReadStream(ProcessStream process, bool useCompression)
        {
            if (useCompression)
            {
                process = process.WithDecompression();
            }
            if (Configuration.EncryptionKey != null)
            {
                process = process.WithDecryption(Configuration.EncryptionKey);
            }

            return process;
        }
    }
}
