using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Identity.Client;
using System.Drawing;
using System.Globalization;
using WellsChat.Shared;
using static System.Net.Mime.MediaTypeNames;

namespace WellsChat.Clientconsole
{
    class Program
    {
        static string _accessToken = string.Empty;
        static async Task Main(string[] args) {
            IPublicClientApplication app = PublicClientApplicationBuilder.Create(Environment.GetEnvironmentVariable("WellsChat_ClientId")) 
                .WithDefaultRedirectUri()
                .WithAuthority(AadAuthorityAudience.AzureAdMyOrg)
                .WithTenantId(Environment.GetEnvironmentVariable("WellsChat_TenantId"))
                .Build();
            AuthenticationResult result;
            var account = await app.GetAccountsAsync();
            var scopes = new string[] { Environment.GetEnvironmentVariable("WellsChat_ApiScope") };

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
            }

            var hubConnection = new HubConnectionBuilder()
                .WithUrl(Environment.GetEnvironmentVariable("WellsChat_Url"), options =>
                {
                    options.AccessTokenProvider = () => Task.FromResult(_accessToken);
                })
                .WithAutomaticReconnect()
                .Build();            

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

            hubConnection.On<DateTime, string, string>("ReceiveMessage", (time, user, message) =>
            {
                //TODO: decrypt message

                var receivedMsg = $"{time.ToString("g")} | {user}: {message}";
                Console.SetCursorPosition(0, Console.CursorTop);
                Console.BackgroundColor = ConsoleColor.DarkBlue;
                Console.WriteLine(receivedMsg);
                Console.ResetColor();
                Console.Write("> ");
            });

            hubConnection.On<DateTime, string, string>("SendSuccess", (time, user, message) =>
            {
                var receivedMsg = $"{time.ToString("g")} | {user}: {message}";
                Console.SetCursorPosition(0, Console.CursorTop - 1);
                Console.BackgroundColor = ConsoleColor.DarkGreen;
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
                var msg = Console.ReadLine();

                if (msg.ToLower() == "exit")
                {
                    await hubConnection.DisposeAsync();
                    Environment.Exit(0);
                }
                else
                {
                    if (!string.IsNullOrWhiteSpace(msg))
                    {
                        //TODO: encrypt message, then send
                        try
                        {
                            await hubConnection.SendAsync("SendMessage", msg);
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



