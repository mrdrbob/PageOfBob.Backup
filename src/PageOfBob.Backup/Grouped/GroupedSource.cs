using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PageOfBob.Backup.Grouped
{
    public class NamedSource
    {
        public string Name { get; set; }
        public ISource Source { get; set; }
    }

    public class GroupedSource : ISource
    {
        readonly IEnumerable<NamedSource> sources;

        public GroupedSource(IEnumerable<NamedSource> sources)
        {
            this.sources = sources ?? throw new ArgumentNullException(nameof(sources));
        }

        public async Task<FileEntry> GetFileInfoAsync(string path)
        {
            (string name, string partialPath) = SplitPath(path);
            var source = sources.Single(x => x.Name == name);
            var entry = await source.Source.GetFileInfoAsync(partialPath);
            if (entry == null)
                return entry;
            entry.Path = JoinPath(name, entry.Path);
            return entry;
        }

        public async Task ProcessFiles(CancellationToken cancellationToken, Func<FileEntry, Task> action)
        {
            var tasks = new List<Task>();

            foreach(var source in sources)
            {
                var s = source;
                tasks.Add(source.Source.ProcessFiles(cancellationToken, (entry) =>
                {
                    entry.Path = JoinPath(s.Name, entry.Path);
                    return action(entry);
                }));
            }

            await Task.WhenAll(tasks);
        }

        public Task ReadFileAsync(string path, ProcessStream function)
        {
            (string name, string partialPath) = SplitPath(path);
            var source = sources.Single(x => x.Name == name);
            return source.Source.ReadFileAsync(partialPath, function);
        }

        public Task WriteFileAsync(FileEntry entry, ProcessStream function)
        {
            (string name, string partialPath) = SplitPath(entry.Path);
            var source = sources.Single(x => x.Name == name);
            entry.Path = partialPath;
            return source.Source.WriteFileAsync(entry, function);
        }

        public static string JoinPath(string name, string path) => $"{name}:{path}";

        public static (string Name, string Path) SplitPath(string path)
        {
            int index = path.IndexOf(':');
            return (path.Substring(0, index), path.Substring(index + 1));
        }
    }
}
