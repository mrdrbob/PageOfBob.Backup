using System;
using System.IO;

namespace PageOfBob.Backup
{
    public static class DestinationFactory
    {
        const string PackedPrefix = "packed:";

        public static IDestination TryParse(string text)
            => FromPacked(text)
                ?? TryParsePartial(text);

        static IDestination FromPacked(string text)
            => text.StartsWith(PackedPrefix, StringComparison.OrdinalIgnoreCase) ? new Packed.PackedDestination(TryParsePartial(text.Substring(PackedPrefix.Length))) : null;

        static IDestinationWithPartialRead TryParsePartial(string text)
            => FromFilePath(text);

        static IDestinationWithPartialRead FromFilePath(string text) => Directory.Exists(text) ? new FileSystem.FileSystemDestination(text) : null;
    }

    public static class SourceFactory
    {
        public static ISource TryParse(string text) => FromPath(text);

        static ISource FromPath(string text) => Directory.Exists(text) ? new FileSystem.FileSystemSource(text) : null;
    }
}

