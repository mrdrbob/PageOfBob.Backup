using System.IO;
using System.Security.Cryptography;
using Wiry.Base32;

namespace PageOfBob.Backup
{
    static class InternalExtensions
    {
        public static string CalculateHashOnStream(this Stream str)
        {
            using (var sha1 = SHA1.Create())
            {
                var hash = sha1.ComputeHash(str);
                return Base32Encoding.ZBase32.GetString(hash);
            }
        }

        public static bool AppearsIdentical(this FileEntry entry, FileEntry previousEntry)
            => entry.Path == previousEntry.Path
                && entry.Created == previousEntry.Created
                && entry.LastModified == previousEntry.LastModified
                && entry.Size == previousEntry.Size;
    }
}
