using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace PageOfBob.Backup.Split
{
    [Plugin("SplitDestination", typeof(SplitDestinationFactory))]
    public class SplitDestination : IDestinationWithPartialRead
    {
        private readonly IDestinationWithPartialRead primaryDestination;
        private readonly IDestination[] destinations;
        public bool CacheOnDisk { get; set; }
        public bool Verbose { get; set; }

        public SplitDestination(IDestinationWithPartialRead primaryDestination, params IDestination[] destinations)
        {
            this.primaryDestination = primaryDestination;
            this.destinations = destinations;
        }

        public Task InitAsync()
        {
            var tasks = new List<Task>(destinations.Length + 1);
            tasks.Add(primaryDestination.InitAsync());
            tasks.AddRange(destinations.Select(x => x.InitAsync()));
            return Task.WhenAll(tasks.ToArray());
        }

        public async Task<bool> WriteAsync(string key, WriteOptions writeOptions, ProcessStream writeAction)
        {
            Stream str;
            string tempPath;

            if (CacheOnDisk)
            {
                tempPath = Path.GetTempFileName();
                str = File.Open(tempPath, FileMode.Truncate, FileAccess.ReadWrite, FileShare.None);
            }
            else
            {
                str = GlobalContext.MemoryStreamManager.GetStream();
                tempPath = null;
            }
            
            try
            {
                await writeAction.Invoke(str);
                str.Flush();

                if (Verbose)
                    Console.Out.WriteLine("Writing to primary destination");
                str.Seek(0, SeekOrigin.Begin);
                bool result = await primaryDestination.WriteAsync(key, writeOptions, (innerStr) => str.CopyToAsync(innerStr));

                int secondaryId = 0;
                foreach (var destination in destinations)
                {
                    if (Verbose)
                        Console.Out.WriteLine($"Writing to secondary destination {secondaryId}");
                    str.Seek(0, SeekOrigin.Begin);
                    await destination.WriteAsync(key, writeOptions, (innerStr) => str.CopyToAsync(innerStr));
                    secondaryId += 1;
                }
                return result;
            }
            finally
            {
                str.Dispose();

                if (tempPath != null)
                    File.Delete(tempPath);
            }
        }

        public Task<bool> ReadAsync(string key, ReadOptions readOptions, ProcessStream readAction)
            => primaryDestination.ReadAsync(key, readOptions, readAction);
        public Task<bool> ReadAsync(string key, long begin, long end, ReadOptions readOptions, ProcessStream readAction)
            => primaryDestination.ReadAsync(key, begin, end, readOptions, readAction);

        public async Task<bool> DeleteAsync(string key)
        {
            bool result = await primaryDestination.DeleteAsync(key);
            await Task.WhenAll(destinations.Select(x => x.DeleteAsync(key)));
            return result;
        }

        public Task<bool> ExistsAsync(string key, ReadOptions readOptions) => primaryDestination.ExistsAsync(key, readOptions);

        public Task FlushAsync()
        {
            var tasks = new List<Task>(destinations.Length + 1);
            tasks.Add(primaryDestination.FlushAsync());
            tasks.AddRange(destinations.Select(x => x.FlushAsync()));
            return Task.WhenAll(tasks.ToArray());
        }
    }
}
