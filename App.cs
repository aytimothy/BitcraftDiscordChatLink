using Discord.WebSocket;
using SpacetimeDB;
using SpacetimeDB.Types;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Rest;
using Newtonsoft.Json;

namespace MyApp {
    public class App {
        public static App Instance = null;
        public TextWriter LogWriter;

        private bool DiscordLoggedIn = false;
        private bool SpacetimeDbConnected = false;
        public bool SpacetimeDbCaughtUp = false;

        public App() {
            if (Instance == null)
                Instance = this;
            else if (Instance != null && Instance != this)
                throw new InvalidOperationException("App instance already exists.");

            LogWriter = new StreamWriter(Program.LogFile);
        }

        public async Task Run() {
            Console.WriteLine("Running app...");
            await Program.DiscordClient.StartAsync();

            int count = 29;
            while (!DiscordLoggedIn || !SpacetimeDbConnected) {
                count++;
                if (count >= 30) {
                    if (!DiscordLoggedIn && !SpacetimeDbConnected) {
                        Console.WriteLine("Waiting for Discord and SpacetimeDb to log in...");
                    }
                    else if (!DiscordLoggedIn && SpacetimeDbConnected) {
                        Console.WriteLine("Waiting for Discord to log in...");
                    }
                    else if (DiscordLoggedIn && !SpacetimeDbConnected) {
                        Console.WriteLine("Waiting for SpacetimeDb to log in...");
                    }
                    else {
                        Console.WriteLine("ERROR: Can't be waiting when both Discord and SpacetimeDb are logged in!");
                    }

                    count = 0;
                }

                await Task.Delay(1000);
            }

            // Never exit...
            await Task.Delay(-1);
        }

        private void SpacetimeDb_OnApplied(SubscriptionEventContext obj) {
            Console.WriteLine("Caught up to latest messages...");
            SpacetimeDbCaughtUp = true;
        }

        SocketChannel? outputChannel;

        void SpacetimeDb_Bitcraft_ChatMessageState_OnInsert(EventContext context, ChatMessageState row) {
            string GetChannelName(int channelId) {
                switch (channelId) {
                    case 1: return "System";
                    case 2: return "Local";
                    case 3: return "Region";
                    case 4: return "Claim";
                    case 5: return "Empire";
                    default: return $"Unknown ({channelId})";
                }
            }

            if (!SpacetimeDbCaughtUp)
                return; // ignore all previous data.
            DateTimeOffset timestamp = DateTimeOffset.FromUnixTimeSeconds(row.Timestamp);
            string logLine = $"[{timestamp:G}] [{GetChannelName(row.ChannelId)}] {row.Username}: {row.Text}";
            LogWriter.WriteLine(logLine);
            LogWriter.Flush();
            Console.WriteLine(logLine);
            bool isRegionChat = row.ChannelId == 3;
            if (!isRegionChat && !Program.Config.OutputEverything)
                return; // don't output it.

            if (outputChannel == null)
                outputChannel = Program.DiscordClient.GetChannel(ulong.Parse(Program.Config.DiscordOutputChannel));
            string discordLine = $"[{GetChannelName(row.ChannelId)}] {row.Username}: {Format.Sanitize(row.Text)}";
            if (outputChannel != null && outputChannel is IMessageChannel textChannel)
                textChannel.SendMessageAsync(discordLine);
            if (outputChannel == null)
                Console.WriteLine("[DEBUG] outputChannel is null.");
        }

        public async Task DiscordClient_OnLoggedIn() {
            Console.WriteLine($"Logged into Discord.");
            RestApplication discordApp = await Program.DiscordClient.GetApplicationInfoAsync();
            if (discordApp != null)
                Console.WriteLine($"To add the bot to your server, use this URL: https://discord.com/api/oauth2/authorize?client_id={discordApp.Id}&permissions=2048&scope=bot%20applications.commands");

            Instance.DiscordLoggedIn = true;
        }

        public Task DiscordClient_OnLoggedOut() {
            Console.WriteLine($"Logged out of Discord.");
            return Task.CompletedTask;
        }

        public static void SpacetimeDb_OnError(Exception e) {
            Console.WriteLine("An unhandled error has occurred in the SpacetimeDb connection.");
            Console.WriteLine(e.ToString());
        }

        public static void SpacetimeDb_OnDisconnect(DbConnection conn, Exception? e) {
            Console.WriteLine("Disconnected from SpacetimeDb Database...");

            Reconnect();
        }

        static async void Reconnect() {
            App.Instance.SpacetimeDbCaughtUp = false;
            Program.SpacetimeDbConnection = DbConnection.Builder()
                .OnConnect(App.Instance.SpacetimeDb_OnConnect)
                .OnDisconnect(App.SpacetimeDb_OnDisconnect)
                .OnConnectError(App.SpacetimeDb_OnError)
                .WithUri(Program.Config.SpacetimeDbUrl)
                .WithModuleName(Program.Config.SpacetimeDbName)
                .WithToken(Program.Config.SpacetimeDbAccessToken)
                .Build();
            CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
            Thread websocketThread = new Thread(() => {
                while (!cancellationTokenSource.IsCancellationRequested) {
                    Program.SpacetimeDbConnection.FrameTick();
                    Thread.Sleep(100);
                }
                Program.SpacetimeDbConnection.Disconnect();
            });
            websocketThread.Start();
        }

        public void SpacetimeDb_OnConnect(DbConnection conn, Identity identity, string token) {
            Console.WriteLine($"Connected to SpacetimeDb Database with Identity {identity} ({conn.ConnectionId})");

            // Save the token back for future use.
            string currentDirectory = Environment.CurrentDirectory;
            string configurationFile = Path.Combine(currentDirectory, "config.json");
            Program.Config.SpacetimeDbLastAccessToken = token;
            string configJson = JsonConvert.SerializeObject(Program.Config, Formatting.Indented);
            File.WriteAllText(configurationFile, configJson);

            Instance.SpacetimeDbConnected = true;

            // Setup Event Handlers
            Program.SpacetimeDbConnection.SubscriptionBuilder()
                .OnApplied(SpacetimeDb_OnApplied)
                // .SubscribeToAllTables();
                .Subscribe(new string[] { "SELECT * FROM chat_message_state" });
            Program.SpacetimeDbConnection.Db.ChatMessageState.OnInsert += SpacetimeDb_Bitcraft_ChatMessageState_OnInsert;
            Program.SpacetimeDbConnection.OnUnhandledReducerError += SpacetimeDb_OnReducerError;
        }

        private void SpacetimeDb_OnReducerError(ReducerEventContext ctx, Exception ex) {
            Console.WriteLine($"REDUCER ERROR: {ctx.ConnectionId}");
            Console.WriteLine($"Reducer: {ex}");
        }
    }
}
