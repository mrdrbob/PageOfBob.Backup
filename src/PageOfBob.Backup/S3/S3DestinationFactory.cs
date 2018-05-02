namespace PageOfBob.Backup.S3
{
    public class S3DestinationFactory : IFactory
    {
        public object Instantiate(IRootFactory parent, dynamic config)
            => new S3Destination((string)config.bucket, (string)config.prefix, (string)config.accessKey, (string)config.secretKey);
    }
}
