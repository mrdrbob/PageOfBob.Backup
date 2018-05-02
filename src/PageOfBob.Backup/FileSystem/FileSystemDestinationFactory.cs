namespace PageOfBob.Backup.FileSystem
{
    public class FileSystemDestinationFactory : IFactory
    {
        public object Instantiate(IRootFactory parent, dynamic config) => new FileSystemDestination((string)config.basePath);
    }
}
