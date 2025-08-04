using System.Diagnostics;
using System.Web;
using Discord;
using Discord.WebSocket;
using Newtonsoft.Json;
using RestSharp;
using SpacetimeDB;
using SpacetimeDB.Types;

namespace MyApp {
    internal class Program {
        public static Configuration Config;

        public static DbConnection SpacetimeDbConnection;
        public static FileStream LogFile = null;
        public static FileStream RawLogFile = null;
        public static DiscordSocketClient DiscordClient;
        public static App App;

        static int Main(string[] args) {
            Console.WriteLine("Bitcraft Discord Chat Link v1.1 by aytimothy");

            if (!ReadConfig()) {
                Console.WriteLine("Error: Could not find a configuration file. Creating one now!");
                return -1;
            }
            Initialize();
            Run();

            return 0;
        }

        static bool ReadConfig() {
            string currentDirectory = Environment.CurrentDirectory;
            string configurationFile = Path.Combine(currentDirectory, "config.json");
            bool fileExists = File.Exists(configurationFile);
            if (fileExists) {
                string fileContents = File.ReadAllText(configurationFile);
                Config = JsonConvert.DeserializeObject<Configuration>(fileContents);
                // We just get it through the API.
                if (String.IsNullOrEmpty(Config.SpacetimeDbAccessToken)) {
                    RestClient webClient = new RestClient();
                    Console.WriteLine("Error: SpacetimeDb Authentication Token is empty! Let's obtain one!");
                    Console.Write("Please enter your Bitcraft User Email: ");
                    string email = Console.ReadLine();
                    email = HttpUtility.UrlEncode(email);
                    RestRequest startFlowRequest = new RestRequest($"https://api.bitcraftonline.com/authentication/request-access-code?email={email}", Method.Post);
                    RestResponse startFlowResponse = webClient.Post(startFlowRequest);
                    Console.WriteLine("Please check your email for an access code.");
                    string accessCode = string.Empty;
                    string accessToken = string.Empty;
                    while (String.IsNullOrEmpty(accessCode)) {
                        Console.Write("Please enter the access code you received: ");
                        accessCode = Console.ReadLine();
                        RestRequest authenticateRequest = new RestRequest($"https://api.bitcraftonline.com/authentication/authenticate?email={email}&accessCode={accessCode}");
                        try {
                            RestResponse authenticateResponse = webClient.Post(authenticateRequest);
                            accessToken = authenticateResponse.Content;
                        }
                        catch (UnauthorizedAccessException uae) {
                            Console.WriteLine("Error: Incorrect code entered. Please try again.");
                            accessCode = string.Empty;
                        }
                    }
                    if (accessToken.StartsWith("\""))
                        accessToken = accessToken.Substring(1, accessToken.Length - 2); // Remove quotes
                    Config.SpacetimeDbAccessToken = accessToken;
                    // Save a copy of the generated token.
                    string configJson = JsonConvert.SerializeObject(Config, Formatting.Indented);
                    File.WriteAllText(configurationFile, configJson);
                }
            }
            else {
                Config = new Configuration {
                    DiscordToken = "DiscordToken",
                    DiscordClientId = "DiscordClientId",
                    DiscordOutputGuild = "GuildId",
                    DiscordOutputChannel = "ChannelId",
                    SpacetimeDbUrl = "https://bitcraft-early-access.spacetimedb.com",
                    SpacetimeDbAccessToken = "",
                    SpacetimeDbName = "bitcraft-6",
                    SpacetimeDbLastAccessToken = "NA",
                    BitcraftRegionNumber = 6,
                    OutputEverything = false,
                    AllowedSpeakers = [ "" ],
                    DiscordMentions = new Dictionary<string, string>()
                };
                Config.DiscordMentions.Add("aytimothy", "108826894937374720");
                Config.DiscordMentions.Add("ayt", "108826894937374720");
                string configJson = JsonConvert.SerializeObject(Config, Formatting.Indented);
                File.WriteAllText(configurationFile, configJson);
            }

            return fileExists;
        }

        static void Initialize() {
            string currentDirectory = Environment.CurrentDirectory;
            string logFilePath = Path.Combine(currentDirectory, "log.txt");
            LogFile = new FileStream(logFilePath, FileMode.Append);
            if (Config.OutputRawLog) {
                string rawLogFilePath = Path.Combine(currentDirectory, "raw.txt");
                RawLogFile = new FileStream(rawLogFilePath, FileMode.Append);
            }

            App = new App();
            
            AuthToken.Init(".bitcraft-discord-chat-link");
            AuthToken.SaveToken(Config.SpacetimeDbAccessToken);
            SpacetimeDbConnection = DbConnection.Builder()
                .OnConnect(App.SpacetimeDb_OnConnect)
                .OnDisconnect(App.SpacetimeDb_OnDisconnect)
                .OnConnectError(App.SpacetimeDb_OnError)
                .WithUri(Config.SpacetimeDbUrl)
                .WithModuleName(Config.SpacetimeDbName)
                .WithToken(Config.SpacetimeDbAccessToken)
                .Build();

            CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
            Thread websocketThread = new Thread(() => {
                while (!cancellationTokenSource.IsCancellationRequested) {
                    try {
                        SpacetimeDbConnection.FrameTick();
                    }
                    catch (ArgumentOutOfRangeException aoore) {
                        Console.WriteLine(aoore.Message);
                    }
                    Thread.Sleep(100);
                }
                SpacetimeDbConnection.Disconnect();
            });
            websocketThread.Start();

            DiscordClient = new DiscordSocketClient();
            DiscordClient.LoggedIn += App.DiscordClient_OnLoggedIn;
            DiscordClient.LoggedOut += App.DiscordClient_OnLoggedOut;
            DiscordClient.Disconnected += App.DiscordClient_OnDisconnected;
            Task loginTask = DiscordClient.LoginAsync(TokenType.Bot, Config.DiscordToken);

            while (!loginTask.IsCompleted) {
                Task.Delay(1000);
            }
        }

        static void Run() {
            Task runtime = App.Run();
            while (!runtime.IsCompleted) {
                Task.Delay(1000);
            }
        }
    }
}