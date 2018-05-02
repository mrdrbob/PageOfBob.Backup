namespace PageOfBob.Backup.Packed
{
    public class PackedDestinationFactory : IFactory
    {
        public object Instantiate(IRootFactory parent, dynamic config)
        {
            var destination = (IDestinationWithPartialRead)parent.Instantiate(config.destination);
            return new PackedDestination(destination);
        }
    }
}
