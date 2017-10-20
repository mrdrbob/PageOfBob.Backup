using System;
using System.Security.Cryptography;

namespace PageOfBob.Backup
{
    public static class CryptoUtilities
    {
        public static string GenerateKey()
        {
            using (var aes = Aes.Create())
            {
                aes.GenerateKey();
                return Convert.ToBase64String(aes.Key);
            }
        }
    }
}
