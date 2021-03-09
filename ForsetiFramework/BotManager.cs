using System.Threading.Tasks;

using Discord;
using Discord.WebSocket;

using ForsetiFramework.Modules;

namespace ForsetiFramework
{
    public static class BotManager
    {
        public static Config Config;
        public static LoggingService Logger;
        public static DiscordSocketClient Client;

        public static void Init()
        {
            Config = Config.Load(Config.Path + "config.json");
            Logger = new LoggingService();

            Client = new DiscordSocketClient(new DiscordSocketConfig()
            {
                AlwaysDownloadUsers = true,
                DefaultRetryMode = RetryMode.AlwaysRetry,
                LargeThreshold = 250,
                LogLevel = LogSeverity.Warning,
                RateLimitPrecision = RateLimitPrecision.Millisecond,
                ExclusiveBulkDelete = true,
            });
            Client.Log += Logger.Client_Log;
            Client.MessageReceived += CommandManager.HandleCommands;
            Client.Ready += Client_Ready;
        }

        public static async Task Start()
        {
            Database.Init();
            PersistentRoles.Init();
            ClockworkManager.Init();

            await Client.LoginAsync(TokenType.Bot, Config.Token);
            await Client.StartAsync();
            Config.Token = "";

            await Task.Delay(-1);
        }

        private static async Task Client_Ready()
        {
            if (Config.Debug) { return; } // Don't post debug ready messages
            var botTesting = Client.GetChannel(814330280969895936) as SocketTextChannel;
            var e = new EmbedBuilder()
                .WithAuthor(Client.CurrentUser)
                .WithTitle("Bot Ready!")
                .WithCurrentTimestamp()
                .WithColor(Color.Teal);
            await botTesting.SendMessageAsync(embed: e.Build());
        }
    }
}
