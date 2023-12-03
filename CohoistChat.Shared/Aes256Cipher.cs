using System.Security.Cryptography;

namespace CohoistChat.Shared
{
    public class Aes256Cipher
    {
        private readonly byte[] _key;

        public Aes256Cipher(byte[] key)
        {
            _key = key;
        }

        private string DecryptString(string text, string iv)
        {
            using (var aes = Aes.Create())
            {
                aes.Key = _key;
                aes.IV = Convert.FromBase64String(iv);
                using var decryptor = aes.CreateDecryptor();

                byte[] bytes = Convert.FromBase64String(text);
                using (MemoryStream ms = new MemoryStream(bytes))
                {
                    using (CryptoStream cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read))
                    {
                        using (StreamReader sr = new StreamReader(cs))
                        {
                            return sr.ReadToEnd();
                        }
                    }
                }
            }            
        }

        public Message DecryptMessage(Message message)
        {
            message.Payload = DecryptString(message.Payload, message.IV);
            message.SenderEmail = DecryptString(message.SenderEmail, message.IV);
            message.SenderDisplayName = DecryptString(message.SenderDisplayName, message.IV);

            return message;
        }

        public string EncryptString(string text, string iv)
        {
            using (var aes = Aes.Create())
            {
                aes.Key = _key;
                aes.IV = Convert.FromBase64String(iv);
                using var encryptor = aes.CreateEncryptor();

                byte[] encryptedData;

                using (MemoryStream ms = new MemoryStream())
                {
                    using (CryptoStream cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
                    {
                        using (StreamWriter sw = new StreamWriter(cs))
                        {
                            sw.Write(text);
                        }
                        encryptedData = ms.ToArray();
                    }
                }

                return Convert.ToBase64String(encryptedData);
            }
        }

        public Message EncryptMessage(Message message)
        {
            message.IV = GenerateIV();
            message.Payload = EncryptString(message.Payload, message.IV);
            message.SenderEmail = EncryptString(message.SenderEmail, message.IV);
            message.SenderDisplayName = EncryptString(message.SenderDisplayName, message.IV);

            return message;
        }

        public static string GenerateIV()
        {
            using var aes = Aes.Create();
            aes.GenerateIV();
            return Convert.ToBase64String(aes.IV);
        }
    }
}
