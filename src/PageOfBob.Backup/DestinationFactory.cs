using System;

namespace PageOfBob.Backup
{

    public static class DestinationFactory
    {
        public static IDestination Resolve(dynamic destination)
        {
            string type = destination.type;
            switch (type)
            {
                case "PackedDestination": return PackedDestination(destination.config);
                default: return ResolveWithPartial(destination);
            }
        }

        public static IDestinationWithPartialRead ResolveWithPartial(dynamic destination)
        {
            string type = destination.type;
            switch (type)
            {
                case "FileSystemDestination": return FileSystemDestination(destination.config);
                case "S3Destination": return S3Destination(destination.config);
                default: throw new NotImplementedException();
            }
        }

        static FileSystem.FileSystemDestination FileSystemDestination(dynamic config) => new FileSystem.FileSystemDestination((string)config.basePath);

        static Packed.PackedDestination PackedDestination(dynamic config)
        {
            IDestinationWithPartialRead destination = Resolve(config.destination);
            return new Packed.PackedDestination(destination);
        }

        static S3.S3Destination S3Destination(dynamic config) => new S3.S3Destination((string)config.bucket, (string)config.prefix, (string)config.accessKey, (string)config.secretKey);
    }
}

