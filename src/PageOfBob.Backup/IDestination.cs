using System;
using System.Threading.Tasks;

namespace PageOfBob.Backup
{
    public interface IDestination
    {
        Task InitAsync();
        Task<bool> WriteAsync(string key, WriteOptions writeOptions, ProcessStream writeAction);
        Task<bool> ReadAsync(string key, ReadOptions readOptions, ProcessStream readAction);
        Task<bool> DeleteAsync(string key);
        Task<bool> ExistsAsync(string key, ReadOptions readOptions);
        Task FlushAsync();
    }

    public interface IDestinationWithPartialRead : IDestination
    {
        Task<bool> ReadAsync(string key, long begin, long end, ReadOptions readOptions, ProcessStream readAction);
    }

    [Flags]
    public enum WriteOptions
    {
        None = 0,
        /// <summary>
        /// Suggests that a client that writes data to a slow remote place would benefit from caching this file locally.
        /// </summary>
        CacheLocally = 1,
        /// <summary>
        /// If the file exists, overwrite it. Otherwise, assume the file is identical and do not overwrite it.
        /// </summary>
        Overwrite = 2
    }

    [Flags]
    public enum ReadOptions
    {
        None = 0,
        /// <summary>
        /// File has been saved with WriteOptions.CacheLocally, so check the local cache first
        /// </summary>
        FromLocalCache = 1
    }
}
