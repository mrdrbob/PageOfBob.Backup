using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using static PageOfBob.Backup.StreamFunctions;

namespace PageOfBob.Backup.Processes
{
    public class BackupReportProcessConfiguration : AbstractProcessConfiguration
    {
        public ShouldProcessFile ShouldReport { get; set; } = ShouldRestoreLogic.YesOfCourseYouShould;
        public string EntryKey { get; set; }
        public bool IncludeSubHashes { get; set; }
        public bool IncludeDuplicates { get; set; }
        public IDestination Destination { get; }
        public TextWriter Output { get; set; } = Console.Out;

        public BackupReportProcessConfiguration(IDestination destination)
        {
            Destination = destination;
        }
    }

    public class BackupReportProcess
    {
        public BackupReportProcessConfiguration Configuration { get; }
        readonly ISet<string> reportedFiles = new HashSet<string>();

        BackupReportProcess(BackupReportProcessConfiguration configuration)
        {
            Configuration = configuration;
        }

        public static Task Execute(BackupReportProcessConfiguration configuration)
        {
            var report = new BackupReportProcess(configuration);
            return report.Execute();
        }

        async Task Execute()
        {
            var key = Configuration.EntryKey ?? await ReadStringAsync(proc => {
                var readHeadKey = GetReadStream(proc, false);
                return Configuration.Destination.ReadAsync(Keys.Head, ReadOptions.FromLocalCache, readHeadKey);
            });

            if (key == null)
                return;

            if (Configuration.IncludeSubHashes)
            {
                WriteCsv(Configuration.Output,
                    "EntryKey",
                    "Path",
                    "IsCompressed",
                    "FileSize",
                    "Subhash"
                );
            }
            else
            {
                WriteCsv(Configuration.Output,
                    "EntryKey",
                    "Path",
                    "IsCompressed",
                    "FileSize",
                    "HashCount"
                );
            }

            while (key != null)
            {
                var set = await ReadObjectAsync<BackupSetEntry>((proc) =>
                {
                    var readHead = GetReadStream(proc, true);
                    return Configuration.Destination.ReadAsync(key, ReadOptions.FromLocalCache, readHead);
                });
                
                foreach(var fileEntry in set.Entries)
                {
                    if (!Configuration.ShouldReport(fileEntry))
                        continue;
                    
                    if (!Configuration.IncludeDuplicates)
                    {
                        string fakeHash = fileEntry.Path + string.Join("-", fileEntry.SubHashes);
                        if (reportedFiles.Contains(fakeHash))
                            continue;

                        reportedFiles.Add(fakeHash);
                    }

                    if (Configuration.IncludeSubHashes)
                    {
                        foreach (var hash in fileEntry.SubHashes)
                        {
                            WriteCsv(Configuration.Output,
                                key,
                                fileEntry.Path,
                                fileEntry.IsCompressed.ToString(),
                                fileEntry.Size.ToString(),
                                hash
                            );
                        }
                    }
                    else
                    {
                        WriteCsv(Configuration.Output,
                            key,
                            fileEntry.Path,
                            fileEntry.IsCompressed.ToString(),
                            fileEntry.Size.ToString(),
                            fileEntry.SubHashes.Count.ToString()
                        );
                    }
                }

                key = Configuration.EntryKey != null ? null : set.ParentKey;
            }
        }

        static void WriteCsv(TextWriter writer, params string[] values)
        {
            for(int x= 0; x < values.Length; x++)
            {
                if (x > 0)
                    writer.Write(',');
                string value = values[x] ?? string.Empty;
                if (value.IndexOfAny(new [] { '"', ',' }) >= 0)
                {
                    writer.Write('"');
                    writer.Write(value.Replace("\"", "\"\""));
                    writer.Write('"');
                }
                else
                {
                    writer.Write(value);
                }
            }
            writer.WriteLine();
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
