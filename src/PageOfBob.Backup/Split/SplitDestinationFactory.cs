using System.Collections.Generic;

namespace PageOfBob.Backup.Split
{
    public class SplitDestinationFactory : IFactory
    {
        public object Instantiate(IRootFactory parent, dynamic config)
        {
            var primary = (IDestinationWithPartialRead)parent.Instantiate(config.primaryDestination);
            var list = new List<IDestination>();
            foreach(var secondaryDestination in config.secondaryDestinations)
            {
                list.Add((IDestination)parent.Instantiate(secondaryDestination));
            }
            var destination = new SplitDestination(primary, list.ToArray());

            if (config.cacheOnDisk != null && (bool)config.cacheOnDisk)
            {
                destination.CacheOnDisk = true;
            }
            if (config.verbose != null && (bool)config.verbose)
            {
                destination.Verbose = true;
            }

            return destination;
        }
    }
}
