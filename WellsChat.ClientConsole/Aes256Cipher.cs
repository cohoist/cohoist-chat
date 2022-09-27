using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace WellsChat.ClientConsole
{
    public class Aes256Cipher
    {
        private readonly byte[] _key;
        private readonly byte[] _iv;

        public Aes256Cipher(byte[] key, byte[] iv)
        {
            _key = key;
            _iv = iv;
        }

        public string Decrypt(string value)
        {
            var ivAndCipherText = Convert.FromBase64String(value);
            using var aes = Aes.Create();
            aes.IV = _iv;
            aes.Key = _key;
            using var cipher = aes.CreateDecryptor();
            var cipherText = ivAndCipherText.Skip(aes.IV.Length).ToArray();
            var text = cipher.TransformFinalBlock(cipherText, 0, cipherText.Length);
            return Encoding.UTF8.GetString(text);
        }

        public string Encrypt(string value)
        {
            using var aes = Aes.Create();
            aes.Key = _key;
            aes.IV = _iv;
            using var cipher = aes.CreateEncryptor();
            var text = Encoding.UTF8.GetBytes(value);
            var cipherText = cipher.TransformFinalBlock(text, 0, text.Length);
            return Convert.ToBase64String(aes.IV.Concat(cipherText).ToArray());
        }

        public static string GenerateNewKey()
        {
            using var aes = Aes.Create();
            aes.GenerateKey();
            return Convert.ToBase64String(aes.Key);
        }
    }
}
