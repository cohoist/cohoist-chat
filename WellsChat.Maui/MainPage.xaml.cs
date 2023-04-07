using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Identity.Client;
using Microsoft.Identity.Client.Extensions.Msal;
using System.Collections.ObjectModel;
using WellsChat.Shared;
using Application = Microsoft.Maui.Controls.Application;

namespace WellsChat.Maui
{
    public partial class ChatViewModel
    {
        public ObservableCollection<Message> Messages { get; init; }
        public ChatViewModel()
        {
            Messages = new ObservableCollection<Message>();
        }
    }

    public partial class MainPage : ContentPage
    {
        static HubConnection hubConnection = null;
        public Command EntryReturnCommand { get; set; }        
        static string _accessToken = string.Empty;
        static IPublicClientApplication app = null;
        static SecretClient secretClient = null;
        static Aes256Cipher cipher = null;
        static User me = null;
        private readonly ChatViewModel vm = new();
        public MainPage(IConfiguration config)
        {
            InitializeComponent();
            BindingContext = vm;
            EntryReturnCommand = new Command(async () => await SendMessage());
            MessageEntry.ReturnCommand = EntryReturnCommand;

            var credentials = new DefaultAzureCredential(true); //managed identity credentials don't work on mobile            
            secretClient = new SecretClient(new Uri(config.GetValue<string>("VaultUri")), credentials);
            try
            {
                cipher = new Aes256Cipher(Convert.FromBase64String(secretClient.GetSecret("Key").Value.Value));
            }
            catch
            {
                Environment.Exit(403);
            }
            app = BuildApp();
            if (app == null) return;
            Task.Run(async () =>
            {
                await RegisterCache();
                if (await EstablishConnection())
                {
                    Console.WriteLine("Connected    ");                    
                }
                else
                {
                    Console.WriteLine("Error connecting to server.");
                    Console.ReadLine();
                    Environment.Exit(0);
                }
            });            
        }

        private async Task SendMessage()
        {
            if (!string.IsNullOrWhiteSpace(MessageEntry.Text) || !string.IsNullOrWhiteSpace(MessageEditor.Text))
            {
                Message message = new() { Payload = (checkBox.IsChecked ? MessageEditor.Text : MessageEntry.Text) };
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
                MessageEntry.Text = string.Empty;
                MessageEditor.Text = string.Empty;
            }
        }
        private async void SendButton_Clicked(object sender, EventArgs e)
        {
            await SendMessage();                     
        }

        private static async Task RegisterCache()
        {
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
        }

        private static IPublicClientApplication BuildApp()
        {
#if ANDROID
            return PublicClientApplicationBuilder.Create(secretClient.GetSecret("ClientId").Value.Value)
                .WithDefaultRedirectUri()
                .WithAuthority(AadAuthorityAudience.AzureAdMyOrg)
                .WithTenantId(secretClient.GetSecret("TenantId").Value.Value)
                .WithParentActivityOrWindow(() => Platform.CurrentActivity)
                .Build();
#elif IOS
            return PublicClientApplicationBuilder.Create(secretClient.GetSecret("ClientId").Value.Value)
                .WithDefaultRedirectUri()
                .WithAuthority(AadAuthorityAudience.AzureAdMyOrg)
                .WithIosKeychainSecurityGroup("com.microsoft.adalcache")
                .WithTenantId(secretClient.GetSecret("TenantId").Value.Value)
                .Build();
#else
            return PublicClientApplicationBuilder.Create(secretClient.GetSecret("ClientId").Value.Value)
                .WithRedirectUri("https://login.microsoftonline.com/common/oauth2/nativeclient")
                .WithAuthority(AadAuthorityAudience.AzureAdMyOrg)
                .WithTenantId(secretClient.GetSecret("TenantId").Value.Value)
                .Build();
#endif
        }

        private async Task<bool> EstablishConnection()
        {
            if (hubConnection != null && hubConnection.State != HubConnectionState.Disconnected)
            {
                await hubConnection.StopAsync();
                await hubConnection.DisposeAsync();
            }

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
                me = new User()
                {
                    Email = result.ClaimsPrincipal.FindFirst("preferred_username").Value, //email
                    DisplayName = result.ClaimsPrincipal.FindFirst("name").Value
                };


                hubConnection = new HubConnectionBuilder()
                    .WithUrl(secretClient.GetSecret("HubUrl").Value.Value, options =>
                    {
                        options.AccessTokenProvider = () => Task.FromResult(_accessToken);
                    })
                    .WithAutomaticReconnect()
                    .Build();


                try
                {
                    AddHandlers();
                    await hubConnection.StartAsync();
                    return true;
                }
                catch (HttpRequestException e)
                {
                    return false;
                }
            }
            else
            {
                return false;
            }
        }

        private void AddMessage(Message message)
        {
            Application.Current.Dispatcher.Dispatch(() => {
                vm.Messages.Add(message);
                //MessagesList.ScrollTo(vm.Messages.Count-1);
            });
        }
        private void AddHandlers()
        {
            hubConnection.On<User>("UserConnected", (user) =>
            {
                var message = new Message()
                {
                    SenderDisplayName = "Info",
                    MessageType = MessageTypeEnum.Connected,
                    Payload = $"{user.Email} ({user.DisplayName}) [{user.ActiveConnections}] connected."
                };
                AddMessage(message);
            });

            hubConnection.On<User>("UserDisconnected", (user) =>
            {
                var message = new Message()
                {
                    SenderDisplayName = "Info",
                    MessageType = MessageTypeEnum.Disconnected,
                    Payload = $"{user.Email} ({user.DisplayName}) [{user.ActiveConnections}] disconnected."
                };
                AddMessage(message);
            });

            hubConnection.On<Message>("ReceiveMessage", (message) =>
            {
                message = cipher.DecryptMessage(message);
                bool isMe = message.SenderEmail == me.Email;
                if (isMe) message.MessageType = MessageTypeEnum.Me;
                else message.MessageType = MessageTypeEnum.NotMe;
                AddMessage(message);
            });

            hubConnection.On<Message>("SendSuccess", (message) =>
            {
                
            });

            hubConnection.On<List<User>>("ListUsers", (users) =>
            {
                var message = new Message()
                {
                    SenderDisplayName = "Info",
                    MessageType = MessageTypeEnum.Info,
                    Payload = $"{users.Count} users online"
                };
                foreach (var user in users)
                {
                    message.Payload += $"\n{user.Email} ({user.DisplayName}) [{user.ActiveConnections}]";
                }
                AddMessage(message);
            });

            hubConnection.Closed += HubConnection_Closed;
            hubConnection.Reconnected += HubConnection_Reconnected;
            hubConnection.Reconnecting += HubConnection_Reconnecting;
        }

        private Task HubConnection_Reconnecting(Exception? arg)
        {
            Console.Write($"Connection lost. Reconnecting... ");
            return Task.CompletedTask;
        }

        private Task HubConnection_Reconnected(string? arg)
        {
            Console.WriteLine($"Connected");
            return Task.CompletedTask;
        }

        private async Task HubConnection_Closed(Exception? arg)
        {
            if (arg == null) return; //if client deliberately disconnects, do nothing
            var tryAgainString = " Try again in ";
            Console.WriteLine($"Timed out.");
            for (int i = 1; i <= 5; i++) //attempt to reconnect 5 times
            {
                Console.Write($"Attempting to reconnect {i}/5... ");
                if (await EstablishConnection())
                {
                    Console.WriteLine("Connected    ");
                    return;
                }
                else
                {
                    //Connection failed
                    if (i < 5)
                    {
                        Console.Write($"FAILED.");
                        Console.Write(tryAgainString);
                        for (int j = i * 5; j > 0; j--) //wait i * 5 seconds before trying again 
                        {
                            Console.Write($"{j}");
                            await Task.Delay(1000);
                            //erase current countdown number, get ready to write new number
                            if (j > 1)
                            {                                
                                for (int k = 0; k < j.ToString().Length; k++)
                                    Console.Write(" ");                                
                            }
                            else //if countdown complete
                            {                                
                                for (int k = 0; k < (j.ToString().Length + tryAgainString.Length); k++)
                                    Console.Write(" ");                                
                            }
                        }
                        Console.WriteLine();
                    }
                }
            }
            //Failed 5 reconnection attempts
            Console.Write($"FAILED.");
            Console.WriteLine($" Enter !reconnect to try again.");
            return;
        }
        private async void OnLabelTappedAsync(object sender, TappedEventArgs args)
        {
            Label lblTapped = (Label)sender;
            var item = (TapGestureRecognizer)lblTapped.GestureRecognizers[0];
            await Clipboard.Default.SetTextAsync((string)item.CommandParameter);
         
            CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
            string text = "Copied to clipboard";
            ToastDuration duration = ToastDuration.Short;
            double fontSize = 14;
            var toast = Toast.Make(text, duration, fontSize);
            await toast.Show(cancellationTokenSource.Token);
        }
    }
}