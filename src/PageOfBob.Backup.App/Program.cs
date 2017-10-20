using Microsoft.Extensions.CommandLineUtils;
using System;

namespace PageOfBob.Backup.App
{
    class Program
    {
        static int Main(string[] args)
        {
            var app = new CommandLineApplication
            {
                Name = "backup"
            };
            app.HelpOption("-?|-h|--help");

            app.Command("gen-key", (cmd) => cmd.OnExecute(() =>
            {
                var key = CryptoUtilities.GenerateKey();
                Console.Out.WriteLine(key);
                return 0;
            }));

            app.Command("backup", (cmd) =>
            {
                var sourceOption = cmd.Option("-s|--source <source>", "Source of the files to be backed up", CommandOptionType.SingleValue);
                var destinationOption = cmd.Option("-d|--destination <destination>", "Destination to which to store backed up files", CommandOptionType.SingleValue);

                var progressOption = cmd.Option("-p|--progress <progress>", "Save progress ever X files (useful for large, initial backups)", CommandOptionType.SingleValue);
                var encryptionKeyOption = cmd.Option("-k|--key <key>", "Encryption Key", CommandOptionType.SingleValue);

                cmd.OnExecute(() =>
                {
                    var destination = DestinationFactory.TryParse(destinationOption.Value());
                    var source = SourceFactory.TryParse(sourceOption.Value());
                    var config = new Processes.FullBackupProcessConfiguration(source, destination);

                    if (encryptionKeyOption.HasValue())
                        config.EncryptionKey = encryptionKeyOption.Value();
                    if (progressOption.HasValue())
                        config.WriteInProgressEvery = int.Parse(progressOption.Value());

                    var task = Processes.FullBackupProcess.ExecuteBackup(config);

                    Console.CancelKeyPress += delegate
                    {
                        Console.Out.WriteLine("Stopping");
                        config.Cancel();
                    };

                    task.Wait();

                    return 0;
                });
            });

            app.Command("restore", (cmd) =>
            {
                var sourceOption = cmd.Option("-s|--source <source>", "The Source where restored files will go", CommandOptionType.SingleValue);
                var destinationOption = cmd.Option("-d|--destination <destination>", "Destination from which backed up files are read", CommandOptionType.SingleValue);

                var prefixOption = cmd.Option("-p|--prefix <prefix>", "Prefix to match for restoring files", CommandOptionType.SingleValue);
                var encryptionKeyOption = cmd.Option("-k|--key <key>", "Decryption Key", CommandOptionType.SingleValue);
                var entryOption = cmd.Option("-e|--entry <entry>", "Backup entry key", CommandOptionType.SingleValue);
                var verifyOnlyOption = cmd.Option("-v|--verify", "Verify Only", CommandOptionType.NoValue);
                var overwriteOption = cmd.Option("-f|--force", "Force overwrite", CommandOptionType.NoValue);

                cmd.OnExecute(() =>
                {
                    var destination = DestinationFactory.TryParse(destinationOption.Value());
                    var source = SourceFactory.TryParse(sourceOption.Value());
                    var config = new Processes.FullRestoreProcessConfiguration(source, destination);

                    if (encryptionKeyOption.HasValue())
                        config.EncryptionKey = encryptionKeyOption.Value();
                    if (prefixOption.HasValue())
                        config.ShouldRestore = ShouldRestoreLogic.ProcessMatchingPrefix(prefixOption.Value());
                    if (entryOption.HasValue())
                        config.EntryKey = entryOption.Value();

                    config.VerifyOnly = verifyOnlyOption.HasValue();
                    config.ForceOverwrite = overwriteOption.HasValue();

                    var task = Processes.FullRestoreProcess.ExecuteFullRestore(config);

                    Console.CancelKeyPress += delegate
                    {
                        Console.Out.WriteLine("Stopping");
                        config.Cancel();
                    };

                    task.Wait();

                    return 0;
                });
            });

            app.Command("report", (cmd) =>
            {
                var destinationOption = cmd.Option("-d|--destination <destination>", "Destination from which backed up index is read", CommandOptionType.SingleValue);

                var prefixOption = cmd.Option("-p|--prefix <prefix>", "Prefix to match for reporting files", CommandOptionType.SingleValue);
                var encryptionKeyOption = cmd.Option("-k|--key <key>", "Decryption Key", CommandOptionType.SingleValue);
                var entryOption = cmd.Option("-e|--entry <entry>", "Backup entry key", CommandOptionType.SingleValue);
                var outputOption = cmd.Option("-o|--out <filename>", "Report to a file", CommandOptionType.SingleValue);
                var subHashesOption = cmd.Option("-s|--subhashes", "Include all subhashes", CommandOptionType.NoValue);
                var dupesOption = cmd.Option("-i|--includeDupes", "Include duplicate files", CommandOptionType.NoValue);

                cmd.OnExecute(() =>
                {
                    var destination = DestinationFactory.TryParse(destinationOption.Value());
                    var config = new Processes.BackupReportProcessConfiguration(destination);

                    if (encryptionKeyOption.HasValue())
                        config.EncryptionKey = encryptionKeyOption.Value();
                    if (prefixOption.HasValue())
                        config.ShouldReport = ShouldRestoreLogic.ProcessMatchingPrefix(prefixOption.Value());
                    if (entryOption.HasValue())
                        config.EntryKey = entryOption.Value();
                    config.IncludeSubHashes = subHashesOption.HasValue();
                    config.IncludeDuplicates = dupesOption.HasValue();

                    if (outputOption.HasValue())
                    {
                        config.Output = System.IO.File.CreateText(outputOption.Value());
                    }

                    var task = Processes.BackupReportProcess.Execute(config);

                    Console.CancelKeyPress += delegate
                    {
                        Console.Out.WriteLine("Stopping");
                        config.Cancel();
                    };

                    task.Wait();

                    if (outputOption.HasValue())
                    {
                        config.Output.Flush();
                        config.Output.Dispose();
                    }

                    return 0;
                });
            });

            int result = app.Execute(args);
#if DEBUG
            Console.Out.WriteLine("DONE");
            Console.ReadKey();
#endif
            return result;
        }
    }
}
