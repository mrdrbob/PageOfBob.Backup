using System;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;

namespace PageOfBob.Backup
{
    static class StreamExtensions
    {
        public static ProcessStream WithCompression(this ProcessStream writeAction)
            => async (str) =>
            {
                var gzip = new GZipStream(str, CompressionMode.Compress);
                await writeAction(gzip);
                await gzip.FlushAsync();
            };

        public static ProcessStream WithDecompression(this ProcessStream readAction)
            => async (str) =>
            {
                using (var gzip = new GZipStream(str, CompressionMode.Decompress))
                {
                    await readAction(gzip);
                }
            };

        public static ProcessStream WithEncryption(this ProcessStream writeAction, string encryptionKey)
            => async (outputStream) =>
            {
                using (var aes = Aes.Create())
                {
                    aes.Key = Convert.FromBase64String(encryptionKey);
                    aes.GenerateIV();

                    var binaryWriter = new BinaryWriter(outputStream);
                    binaryWriter.Write(aes.IV.Length);
                    binaryWriter.Write(aes.IV);
                    binaryWriter.Flush();

                    var cryptoStream = new CryptoStream(outputStream, aes.CreateEncryptor(), CryptoStreamMode.Write);
                    await writeAction(cryptoStream);

                    cryptoStream.FlushFinalBlock();
                }
            };

        public static ProcessStream WithDecryption(this ProcessStream readAction, string decryptionKey)
            => async (inputStream) =>
            {
                using (var aes = Aes.Create())
                {
                    aes.Key = Convert.FromBase64String(decryptionKey);

                    var reader = new BinaryReader(inputStream);

                    int ivLength = reader.ReadInt32();
                    byte[] IV = reader.ReadBytes(ivLength);

                    aes.IV = IV;
                    var cryptoStream = new CryptoStream(inputStream, aes.CreateDecryptor(), CryptoStreamMode.Read);
                    await readAction(cryptoStream);
                }
            };
    }
}
