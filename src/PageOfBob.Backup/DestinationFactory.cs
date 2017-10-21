using System;
using System.IO;

namespace PageOfBob.Backup
{
    public static class DestinationFactory
    {
        const string PackedPrefix = "packed:";
        const string S3Prefix = "s3:";

        public static IDestination TryParse(string text)
            => FromPacked(text)
                ?? TryParsePartial(text);

        static IDestination FromPacked(string text)
            => text.StartsWith(PackedPrefix, StringComparison.OrdinalIgnoreCase) ? new Packed.PackedDestination(TryParsePartial(text.Substring(PackedPrefix.Length))) : null;

        static IDestinationWithPartialRead TryParsePartial(string text)
            => FromS3Path(text)
            ?? FromFilePath(text);

        static IDestinationWithPartialRead FromFilePath(string text) => Directory.Exists(text) ? new FileSystem.FileSystemDestination(text) : null;

        static IDestinationWithPartialRead FromS3Path(string text)
        {
            if (!text.StartsWith(S3Prefix, StringComparison.OrdinalIgnoreCase))
                return null;

            string[] split = text.Split(':');
            if (split.Length != 5)
                return null;

            return new S3.S3Destination(split[1], split[2], split[3], split[4]);
        }
    }

    public static class SourceFactory
    {
        public static ISource TryParse(string text) => FromPath(text);

        static ISource FromPath(string text) => Directory.Exists(text) ? new FileSystem.FileSystemSource(text) : null;
    }
}

