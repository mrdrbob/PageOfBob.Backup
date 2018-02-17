using System;
using System.Collections.Generic;
using System.Text;

namespace PageOfBob.Backup
{
    public static class SourceFactory
    {
        public static ISource Resolve(dynamic destination)
        {
            string type = destination.type;
            switch (type)
            {
                case "FileSystemSource": return FileSystemSource(destination.config);
                case "GroupedSource": return GroupedSource(destination.config);
                default: throw new NotImplementedException();
            }
        }

        static FileSystem.FileSystemSource FileSystemSource(dynamic config) => new FileSystem.FileSystemSource((string)config.basePath);

        static Grouped.GroupedSource GroupedSource(dynamic config)
        {
            var list = new List<Grouped.NamedSource>();
            foreach(dynamic namedSource in config.sources)
            {
                list.Add(new Grouped.NamedSource
                {
                    Name = namedSource.name,
                    Source = Resolve(namedSource.source)
                });
            }
            return new Grouped.GroupedSource(list);
        }
    }
}
