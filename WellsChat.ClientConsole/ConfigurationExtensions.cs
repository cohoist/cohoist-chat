using Microsoft.Extensions.Configuration;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;

namespace WellsChat.ClientConsole
{
    public static class ConfigurationRootExtensions
    {
        public static IConfigurationRoot Decrypt(this IConfigurationRoot root, Aes256Cipher cipher)
        {
            DecryptChildren(root);
            return root;

            void DecryptChildren(IConfiguration parent)
            {
                foreach (var child in parent.GetChildren())
                {
                    if (child.Value is not null)
                    {
                        parent[child.Key] = cipher.Decrypt(child.Value);
                    }

                    DecryptChildren(child);
                }
            }

            
        }
        public static IConfigurationRoot Encrypt(this IConfigurationRoot root)
        {
            using var aes = Aes.Create();
            aes.GenerateKey();
            aes.GenerateIV();
            var cipher = new Aes256Cipher(aes.Key, aes.IV);
            Console.WriteLine($"Key: {Convert.ToBase64String(aes.Key)}");
            Console.WriteLine($"IV: {Convert.ToBase64String(aes.IV)}");

            using StreamWriter sw = new StreamWriter("appsettings.json");
            sw.WriteLine("{");
            sw.WriteLine("    \"WellsChat\": {");
            EncryptChildren(root);
            sw.WriteLine("  }");
            sw.WriteLine("}");
            return root;            

            void EncryptChildren(IConfiguration parent)
            {
                foreach (var child in parent.GetChildren())
                {
                    if (child.Value is not null)
                    {
                        sw.WriteLine($"      \"{child.Key}\": \"{cipher.Encrypt(child.Value)}\",");
                        Console.WriteLine($"\"{child.Key}\": \"{cipher.Encrypt(child.Value)}\",");
                    }
                    EncryptChildren(child);
                }
            }
        }
    }
}
