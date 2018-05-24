using Microsoft.Extensions.CommandLineUtils;
using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace PageOfBob.Backup.App
{
    public static class WinInterop
    {
        [DllImport("kernel32.dll", CharSet = CharSet.Ansi, SetLastError = true)]
        public static extern ThreadExecutionState SetThreadExecutionState(ThreadExecutionState esFlags);
        [Flags]
        public enum ThreadExecutionState : uint
        {
            CONTINUOUS = 0x80000000,
            DISPLAY_REQUIRED = 0x00000002,
            SYSTEM_REQUIRED = 0x00000001
        }
    }

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
                var setOption = cmd.Option("-s|--set <set>", "Configuration Set", CommandOptionType.SingleValue);

                var encryptionKeyOption = cmd.Option("-k|--key <key>", "Encryption Key", CommandOptionType.SingleValue);

                cmd.OnExecute(() =>
                {
                    var set = BackupSetConfiguration.FromJsonFile(setOption.Value());
                    var config = new Processes.FullBackupProcessConfiguration(set.Source, set.Destination);
                    config.WriteInProgressEveryCount = set.ProgressEveryCount;
                    config.WriteInProgressEveryBytes = set.ProgressEveryBytes;

                    if (encryptionKeyOption.HasValue())
                        config.EncryptionKey = encryptionKeyOption.Value();

                    if (set.SkipFilesContaining != null)
                    {
                        config.ShouldBackup = ShouldBackupLogic.IgnoreContaining(set.SkipFilesContaining);
                    }
                    if (set.SkipCompressionContaining != null)
                    {
                        config.ShouldCompressFile = ShouldProcessFunctions.All(
                            CompressionLogic.LargerThan(CompressionLogic.DefaultMinimumSize),
                            CompressionLogic.SkipExtensions(set.SkipCompressionContaining));
                    }

                    // On Windows, disable sleep
                    WinInterop.ThreadExecutionState previousThreadState = 0;
                    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
                        previousThreadState = WinInterop.SetThreadExecutionState(WinInterop.ThreadExecutionState.SYSTEM_REQUIRED | WinInterop.ThreadExecutionState.CONTINUOUS);
                    }

                    var task = Processes.FullBackupProcess.ExecuteBackup(config);

                    Console.CancelKeyPress += (sender, e) =>
                    {
                        Console.Out.WriteLine("Stopping");
                        config.Cancel();
                        e.Cancel = true;
                    };

                    task.Wait();

                    // After sleeping, must restore previous thread state
                    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
                        WinInterop.SetThreadExecutionState(previousThreadState);
                    }

                    return 0;
                });
            });

            app.Command("restore", (cmd) =>
            {
                var setOption = cmd.Option("-s|--set <set>", "Configuration Set", CommandOptionType.SingleValue);

                var prefixOption = cmd.Option("-p|--prefix <prefix>", "Prefix to match for restoring files", CommandOptionType.SingleValue);
                var encryptionKeyOption = cmd.Option("-k|--key <key>", "Decryption Key", CommandOptionType.SingleValue);
                var entryOption = cmd.Option("-e|--entry <entry>", "Backup entry key", CommandOptionType.SingleValue);
                var verifyOnlyOption = cmd.Option("-v|--verify", "Verify Only", CommandOptionType.NoValue);
                var overwriteOption = cmd.Option("-f|--force", "Force overwrite", CommandOptionType.NoValue);

                cmd.OnExecute(() =>
                {
                    var set = BackupSetConfiguration.FromJsonFile(setOption.Value());
                    var config = new Processes.FullRestoreProcessConfiguration(set.Source, set.Destination);

                    if (encryptionKeyOption.HasValue())
                        config.EncryptionKey = encryptionKeyOption.Value();
                    if (prefixOption.HasValue())
                        config.ShouldRestore = ShouldRestoreLogic.ProcessMatchingPrefix(prefixOption.Value());
                    if (entryOption.HasValue())
                        config.EntryKey = entryOption.Value();

                    config.VerifyOnly = verifyOnlyOption.HasValue();
                    config.ForceOverwrite = overwriteOption.HasValue();

                    
                    var task = Task.Run(() => Processes.FullRestoreProcess.ExecuteFullRestore(config));

                    Console.CancelKeyPress += (sender, e) =>
                    {
                        Console.Out.WriteLine("Stopping!");
                        config.Cancel();
                        e.Cancel = true;
                    };

                    task.Wait();

                    return 0;
                });
            });

            app.Command("report", (cmd) =>
            {
                var setOption = cmd.Option("-s|--set <set>", "Configuration Set", CommandOptionType.SingleValue);

                var prefixOption = cmd.Option("-p|--prefix <prefix>", "Prefix to match for reporting files", CommandOptionType.SingleValue);
                var encryptionKeyOption = cmd.Option("-k|--key <key>", "Decryption Key", CommandOptionType.SingleValue);
                var entryOption = cmd.Option("-e|--entry <entry>", "Backup entry key", CommandOptionType.SingleValue);
                var outputOption = cmd.Option("-o|--out <filename>", "Report to a file", CommandOptionType.SingleValue);
                var subHashesOption = cmd.Option("-h|--subhashes", "Include all subhashes", CommandOptionType.NoValue);
                var dupesOption = cmd.Option("-i|--includeDupes", "Include duplicate files", CommandOptionType.NoValue);

                cmd.OnExecute(() =>
                {
                    var set = BackupSetConfiguration.FromJsonFile(setOption.Value());
                    var config = new Processes.BackupReportProcessConfiguration(set.Destination);

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

                    Console.CancelKeyPress += (sender, e) =>
                    {
                        Console.Out.WriteLine("Stopping");
                        config.Cancel();
                        e.Cancel = true;
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
