using System.Collections.Generic;
using System.IO;

namespace PageOfBob.Backup
{
    public static class CompressionLogic
    {
        static readonly HashSet<string> DefaultExtensionsToSkipCompression = new HashSet<string>
        {
            // MPEG audio/video
            ".mp2", ".mp3", ".mp4", ".mpg", ".mpeg", ".mpv", ".mpa", ".ogg", ".ogv", ".avi", ".mov", ".aac",
            // Images
            ".jpg", "jpeg", ".png",
            // Word documents (actually zip files)
            ".docx", ".xlsx", ".pptx",
            // Zip files
            ".bz2", ".7z", ".zip", ".rar", ".jar", ".gz"
        };

        // 1K default min size for compression
        const int DefaultMinimumSize = 1024;

        public static ShouldProcessFile LargerThan(int sizeInBytes) => (file) => file.Size >= sizeInBytes;

        public static ShouldProcessFile SkipExtensions(ISet<string> extensionsToSkip)
            => (file) =>
            {
                string ext = Path.GetExtension(file.Path.ToLowerInvariant());
                return ext == null || !extensionsToSkip.Contains(ext);
            };

        public static readonly ShouldProcessFile Default = ShouldProcessFunctions.All(
            LargerThan(DefaultMinimumSize),
            SkipExtensions(DefaultExtensionsToSkipCompression)
        );
    }
}
