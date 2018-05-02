using System.Collections.Generic;

namespace PageOfBob.Backup.Grouped
{
    public class GroupedSourceFactory : IFactory
    {
        public object Instantiate(IRootFactory parent, dynamic config)
        {
            var list = new List<NamedSource>();
            foreach (dynamic namedSource in config.sources)
            {
                list.Add(new NamedSource
                {
                    Name = namedSource.name,
                    Source = (ISource)parent.Instantiate(namedSource.source)
                });
            }
            return new GroupedSource(list);
        }
    }
}
