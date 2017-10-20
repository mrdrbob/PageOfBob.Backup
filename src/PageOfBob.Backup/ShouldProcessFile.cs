namespace PageOfBob.Backup
{
    public delegate bool ShouldProcessFile(FileEntry file);

    public static class ShouldProcessFunctions
    {
        public static ShouldProcessFile All(params ShouldProcessFile[] tests)
            => (file) =>
            {
                foreach (var test in tests)
                {
                    if (!test.Invoke(file))
                        return false;
                }

                return true;
            };
    }
}
