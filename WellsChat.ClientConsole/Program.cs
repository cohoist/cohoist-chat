using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Identity.Client.Extensions.Msal;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Identity.Client;
using WellsChat.ClientConsole;
using WellsChat.Shared;

namespace WellsChat.Clientconsole
{
    class Program
    {
        static string _accessToken = string.Empty;
        static SecretClient secretClient = null;
        static User me = null;
        static async Task Main(string[] args) {
            Console.WriteLine("Authenticating...");

            IConfiguration config = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json")
                .Build();

            var credentials = new DefaultAzureCredential(true);
            secretClient = new SecretClient(new Uri(config.GetValue<string>("VaultUri")), credentials);
            

            var cipher = new Aes256Cipher(Convert.FromBase64String(secretClient.GetSecret("Key").Value.Value));            

            IPublicClientApplication app = PublicClientApplicationBuilder.Create(secretClient.GetSecret("ClientId").Value.Value) 
                .WithDefaultRedirectUri()
                .WithAuthority(AadAuthorityAudience.AzureAdMyOrg)
                .WithTenantId(secretClient.GetSecret("TenantId").Value.Value)
                .Build();

            var storageProperties = new StorageCreationPropertiesBuilder(CacheSettings.CacheFileName, CacheSettings.CacheDir)
                .WithLinuxKeyring(
                    CacheSettings.LinuxKeyRingSchema,
                    CacheSettings.LinuxKeyRingCollection,
                    CacheSettings.LinuxKeyRingLabel,
                    CacheSettings.LinuxKeyRingAttr1,
                    CacheSettings.LinuxKeyRingAttr2)
                .WithMacKeyChain(
                    CacheSettings.KeyChainServiceName,
                    CacheSettings.KeyChainAccountName)
                .Build();

            var cacheHelper = await MsalCacheHelper.CreateAsync(storageProperties);
            cacheHelper.RegisterCache(app.UserTokenCache);

            AuthenticationResult result;
            var account = await app.GetAccountsAsync();
            var scopes = new string[] { secretClient.GetSecret("ApiScope").Value.Value };

            try
            {
                result = await app.AcquireTokenSilent(scopes,
                    account.FirstOrDefault()).ExecuteAsync();
            }
            catch (MsalUiRequiredException)
            {
                try
                {
                    result = await app.AcquireTokenInteractive(scopes).ExecuteAsync();
                }
                catch (MsalServiceException e)
                {
                    Console.WriteLine("Not authorized");
                    result = null;
                    Console.ReadLine();
                    Environment.Exit(0);
                }
            }
            

            if (result != null)
            {
                _accessToken = result.AccessToken;
                me = new User() { 
                    Email = result.ClaimsPrincipal.FindFirst("preferred_username").Value, //email
                    DisplayName = result.ClaimsPrincipal.FindFirst("name").Value 
                };                

                var hubConnection = new HubConnectionBuilder()
                    .WithUrl(secretClient.GetSecret("HubUrl").Value.Value, options =>
                    {
                        options.AccessTokenProvider = () => Task.FromResult(_accessToken);
                    })
                    .WithAutomaticReconnect()
                    .Build();
                Console.SetCursorPosition(0, Console.CursorTop - 1);
                Console.WriteLine("Authenticated    ");
                try
                {
                    Console.WriteLine("Connecting...");
                    await hubConnection.StartAsync();
                    Console.SetCursorPosition(0, Console.CursorTop - 1);
                    Console.WriteLine("Connected    ");
                }
                catch (HttpRequestException e)
                {
                    Console.WriteLine("Error connecting to server.");
                    Console.ReadLine();
                    Environment.Exit(0);
                }

            
                hubConnection.On<User>("UserConnected", (user) =>
                {
                    Console.SetCursorPosition(0, Console.CursorTop);
                    Console.BackgroundColor = ConsoleColor.DarkCyan;
                    Console.WriteLine($"{DateTime.Now.ToString("g")} | {user.Email} ({user.DisplayName}) [{user.ActiveConnections}] connected.");
                    Console.ResetColor();
                    Console.Write("> ");
                });

                hubConnection.On<User>("UserDisconnected", (user) =>
                {
                    Console.SetCursorPosition(0, Console.CursorTop);
                    Console.BackgroundColor = ConsoleColor.DarkGray;
                    Console.WriteLine($"{DateTime.Now.ToString("g")} | {user.Email} ({user.DisplayName}) [{user.ActiveConnections}] disconnected.");
                    Console.ResetColor();
                    Console.Write("> ");
                });

                hubConnection.On<Message>("ReceiveMessage", (message) =>
                {                    
                    message = cipher.DecryptMessage(message);
                    bool isMe = message.SenderEmail == me.Email;
                    var receivedMsg = $"{DateTime.Now.ToString("g")} | {message.SenderDisplayName}: {message.Payload}";
                    Console.SetCursorPosition(0, Console.CursorTop);
                    Console.BackgroundColor = isMe ? ConsoleColor.DarkGreen : ConsoleColor.DarkBlue;
                    Console.WriteLine(receivedMsg);
                    Console.ResetColor();
                    Console.Write("> ");
                });

                hubConnection.On<List<User>>("ListUsers", (users) =>
                {
                    Console.SetCursorPosition(0, Console.CursorTop);
                    Console.WriteLine($"{users.Count} users online");
                    foreach(var user in users)
                    {
                        Console.WriteLine($"{user.Email} ({user.DisplayName}) [{user.ActiveConnections}]");
                    }
                    Console.Write("> ");
                });            

                while (true)
                {
                    Console.Write("> ");
                    Message message = new() { Payload = Console.ReadLine() };

                    if (message.Payload.ToLower() == "exit")
                    {
                        await hubConnection.DisposeAsync();
                        Environment.Exit(0);
                    }
                    else
                    {
                        if (!string.IsNullOrWhiteSpace(message.Payload))
                        {
                            if (message.Payload.ToLower() != "!users") //do not encrypt command messages
                            {                                
                                message.SenderEmail = me.Email;
                                message.SenderDisplayName = me.DisplayName;
                                message = cipher.EncryptMessage(message);
                            }

                            try
                            {
                                await hubConnection.SendAsync("SendMessage", message);
                            }
                            catch
                            {
                                Console.WriteLine("Message not sent");
                            }
                        }
                    }
                }
            }
        }
    }
}



