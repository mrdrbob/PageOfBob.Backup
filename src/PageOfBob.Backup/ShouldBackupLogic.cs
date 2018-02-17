using System;
using System.Collections.Generic;

namespace PageOfBob.Backup
{
    public static class ShouldBackupLogic
    {
        static readonly string[] DefaultIgnores = new[]
        {
            "node_modules", ".git", ".svn", "thumbs.db", "packages", "$tf", "Previews.lrdata"
        };

        public static ShouldProcessFile IgnoreContaining(params string[] values)
            => (file) =>
            {
                foreach (var value in values)
                {
                    if (file.Path.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0)
                        return false;
                }

                return true;
            };

        public static ShouldProcessFile IgnoreContaining(IEnumerable<string> values)
            => (file) =>
            {
                foreach (var value in values)
                {
                    if (file.Path.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0)
                        return false;
                }

                return true;
            };


        public static ShouldProcessFile ProcessMatchingPrefix(string prefix) 
            => (file) => file.Path.StartsWith(prefix, StringComparison.CurrentCultureIgnoreCase);

        public static readonly ShouldProcessFile Default = IgnoreContaining(DefaultIgnores);
    }

    public static class ShouldRestoreLogic
    {
        public static ShouldProcessFile ProcessMatchingPrefix(string prefix)
            => (file) => file.Path.StartsWith(prefix, StringComparison.CurrentCultureIgnoreCase);

        public static readonly ShouldProcessFile YesOfCourseYouShould = new ShouldProcessFile((file) => true);
    }
}
