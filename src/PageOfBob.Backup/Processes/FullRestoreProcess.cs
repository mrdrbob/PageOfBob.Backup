using System;
using System.IO;
using System.Threading.Tasks;
using static PageOfBob.Backup.StreamFunctions;

namespace PageOfBob.Backup.Processes
{
    public class FullRestoreProcessConfiguration : AbstractProcessConfiguration
    {
        public ISource Source { get; }
        public IDestination Destination { get; }

        public bool ForceOverwrite { get; set; }
        public string EntryKey { get; set; }
        public bool VerifyOnly { get; set; }
        public ShouldProcessFile ShouldRestore { get; set; } = ShouldRestoreLogic.YesOfCourseYouShould;

        public FullRestoreProcessConfiguration(ISource source, IDestination destination)
        {
            Source = source;
            Destination = destination;
        }
    }


    public class FullRestoreProcess
    {
        public static Task ExecuteFullRestore(FullRestoreProcessConfiguration configuration)
        {
            var process = new FullRestoreProcess(configuration);
            return process.Execute();
        }

        public FullRestoreProcessConfiguration Configuration { get; }

        FullRestoreProcess(FullRestoreProcessConfiguration configuration)
        {
            Configuration = configuration;
        }

        async Task Execute()
        {
            await Configuration.Destination.InitAsync();

            string headKey = Configuration.EntryKey ?? await ReadStringAsync(proc => {
                var readHeadKey = GetReadStream(proc, false);
                return Configuration.Destination.ReadAsync(Keys.Head, ReadOptions.FromLocalCache, readHeadKey);
            });

            if (headKey == null)
                return;

            var headSet = await ReadObjectAsync<BackupSetEntry>((proc) =>
            {
                var readHead = GetReadStream(proc, true);
                return Configuration.Destination.ReadAsync(headKey, ReadOptions.FromLocalCache, readHead);
            });

            foreach (var file in headSet.Entries)
            {
                if (Configuration.CancellationToken.IsCancellationRequested)
                    break;

                if (!Configuration.ShouldRestore(file))
                    continue;

                if (Configuration.VerifyOnly)
                {
                    try
                    {

                        var existingFile = await Configuration.Source.GetFileInfoAsync(file.Path);
                        if (existingFile == null)
                        {
                            Console.Error.WriteLine($"MISSING: {file.Path}");
                            continue;
                        }

                        bool isMatch = await VerifyFile(file);
                        if (!isMatch)
                        {
                            Console.Error.WriteLine($"INVALID: {file.Path}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine($"ERR: {file.Path} - {ex.Message}");
                    }

                    continue;
                }


                if (!Configuration.ForceOverwrite)
                {
                    var existingFile = await Configuration.Source.GetFileInfoAsync(file.Path);
                    if (existingFile != null && existingFile.AppearsIdentical(file))
                        continue;
                }

                try
                {
                    await Configuration.Source.WriteFileAsync(file, (str) => ProcessFile(file, str));
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"ERR: {file.Path} - {ex.Message}");
                }
            }
        }

        async Task<bool> VerifyFile(FileEntry file)
        {
            byte[] buffer = new byte[2048];

            bool isMatch = true;
            await Configuration.Source.ReadFileAsync(file.Path, async (source) =>
            {
                foreach (var hash in file.SubHashes)
                {
                    if (Configuration.CancellationToken.IsCancellationRequested)
                    {
                        isMatch = false;
                        return;
                    }

                    var readChunk = GetReadStream(async (dest) =>
                    {

                        int readCount = await dest.ReadAsync(buffer, 0, buffer.Length);
                        while (readCount > 0)
                        {
                            if (readCount + source.Position > source.Length)
                            {
                                isMatch = false;
                                return;
                            }

                            for (int x = 0; x < readCount; x++)
                            {
                                // TODO: Use buffers for both streams, rather than reading
                                // single bytes from source.
                                if (source.ReadByte() != buffer[x])
                                {
                                    isMatch = false;
                                    return;
                                }
                            }

                            readCount = await dest.ReadAsync(buffer, 0, buffer.Length);
                        }
                    }, file.IsCompressed);

                    await Configuration.Destination.ReadAsync(hash, ReadOptions.None, readChunk);
                }
            });

            return isMatch;
        }

        async Task ProcessFile(FileEntry file, Stream fileStream)
        {
            foreach (var hash in file.SubHashes)
            {
                if (Configuration.CancellationToken.IsCancellationRequested)
                    break;

                var readChunk = GetReadStream(CopyFromStream(fileStream), file.IsCompressed);

                await Configuration.Destination.ReadAsync(hash, ReadOptions.None, readChunk);
            }
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
