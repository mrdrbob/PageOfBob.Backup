using ProtoBuf;
using System;
using System.IO;
using System.Threading.Tasks;

namespace PageOfBob.Backup
{
    public static class StreamFunctions
    {
        public static ProcessStream WriteObjectAsync<T>(T obj)
            => (str) =>
            {
                Serializer.Serialize(str, obj);
                return Task.CompletedTask;
            };

        public static ProcessStream WriteStringAsync(string value)
            => async (str) =>
            {
                var writer = new StreamWriter(str);
                await writer.WriteAsync(value);
                await writer.FlushAsync();
            };

        public static ProcessStream CopyFromStream(Stream outputStream) => (Stream inputStream) => inputStream.CopyToAsync(outputStream);
        public static ProcessStream CopyToStream(Stream inputStream) => (Stream outputStream) => inputStream.CopyToAsync(outputStream);

        public static async Task<string> ReadStringAsync(Func<ProcessStream, Task> action)
        {
            string value = null;

            await action(async (str) =>
            {
                var reader = new StreamReader(str);
                value = await reader.ReadToEndAsync();
            });

            return value;
        }
        public static async Task<T> ReadObjectAsync<T>(Func<ProcessStream, Task> action) where T : class
        {
            T value = null;

            await action((src) =>
            {
                value = Serializer.Deserialize<T>(src);
                return Task.CompletedTask;
            });

            return value;
        }

        public static async Task<string> CalculateHashAndWrite<T>(T obj, Func<string, Stream, Task> action)
        {
            using (var memStream = GlobalContext.MemoryStreamManager.GetStream())
            {
                // Write to memory
                await WriteObjectAsync(obj).Invoke(memStream);
                memStream.Seek(0, SeekOrigin.Begin);

                // Calcuate Hash
                string hash = memStream.CalculateHashOnStream();
                memStream.Seek(0, SeekOrigin.Begin);

                await action(hash, memStream);

                return hash;
            }
        }
    }
}
