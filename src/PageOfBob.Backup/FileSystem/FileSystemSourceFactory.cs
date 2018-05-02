namespace PageOfBob.Backup.FileSystem
{
    public class FileSystemSourceFactory : IFactory
    {
        public object Instantiate(IRootFactory parent, dynamic config) => new FileSystemSource((string) config.basePath);
    }
}
