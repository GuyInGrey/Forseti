using System;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

using Discord;
using Discord.WebSocket;
using ForsetiFramework.Constructs;
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

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
            Client.ChannelCreated += async (a) => DiscordEvent("ChannelCreated", a);
            Client.ChannelDestroyed += async (a) => DiscordEvent("ChannelDestroyed", a);
            Client.ChannelUpdated += async (a, b) => DiscordEvent("ChannelUpdated", a, b);
            Client.Connected += async () => DiscordEvent("Connected");
            Client.CurrentUserUpdated += async (a, b) => DiscordEvent("CurrentUserUpdated", a, b);
            Client.Disconnected += async (a) => DiscordEvent("Disconnected", a);
            Client.GuildAvailable += async (a) => DiscordEvent("GuildAvailable", a);
            Client.GuildMembersDownloaded += async (a) => DiscordEvent("GuildMembersDownloaded", a);
            Client.GuildMemberUpdated += async (a, b) => DiscordEvent("GuildMemberUpdated", a, b);
            Client.GuildUnavailable += async (a) => DiscordEvent("GuildUnavailable", a);
            Client.GuildUpdated += async (a, b) => DiscordEvent("GuildUpdated", a, b);
            Client.InviteCreated += async (a) => DiscordEvent("InviteCreated", a);
            Client.InviteDeleted += async (a, b) => DiscordEvent("InviteDeleted", a, b);
            Client.JoinedGuild += async (a) => DiscordEvent("JoinedGuild", a);
            Client.LatencyUpdated += async (a, b) => DiscordEvent("LatencyUpdated", a, b);
            Client.LeftGuild += async (a) => DiscordEvent("LeftGuild", a);
            Client.Log += async (a) => DiscordEvent("Log", a);
            Client.LoggedIn += async () => DiscordEvent("LoggedIn");
            Client.LoggedOut += async () => DiscordEvent("LoggedOut");
            Client.MessageDeleted += async (a, b) => DiscordEvent("MessageDeleted", a, b);
            Client.MessageReceived += async (a) => DiscordEvent("MessageReceived", a);
            Client.MessagesBulkDeleted += async (a, b) => DiscordEvent("MessagesBulkDeleted", a, b);
            Client.MessageUpdated += async (a, b, c) => DiscordEvent("MessageUpdated", a, b, c);
            Client.ReactionAdded += async (a, b, c) => DiscordEvent("ReactionAdded", a, b, c);
            Client.ReactionRemoved += async (a, b, c) => DiscordEvent("ReactionRemoved", a, b, c);
            Client.ReactionsCleared += async (a, b) => DiscordEvent("ReactionsCleared", a, b);
            Client.ReactionsRemovedForEmote += async (a, b, c) => DiscordEvent("ReactionsRemovedForEmote", a, b, c);
            Client.Ready += async () => DiscordEvent("Ready");
            Client.RecipientAdded += async (a) => DiscordEvent("RecipientAdded", a);
            Client.RecipientRemoved += async (a) => DiscordEvent("RecipientRemoved", a);
            Client.RoleCreated += async (a) => DiscordEvent("RoleCreated", a);
            Client.RoleDeleted += async (a) => DiscordEvent("RoleDeleted", a);
            Client.RoleUpdated += async (a, b) => DiscordEvent("RoleUpdated", a, b);
            Client.UserBanned += async (a, b) => DiscordEvent("UserBanned", a, b);
            Client.UserIsTyping += async (a, b) => DiscordEvent("UserIsTyping", a, b);
            Client.UserJoined += async (a) => DiscordEvent("UserJoined", a);
            Client.UserLeft += async (a) => DiscordEvent("UserLeft", a);
            Client.UserUnbanned += async (a, b) => DiscordEvent("UserUnbanned", a, b);
            Client.UserUpdated += async (a, b) => DiscordEvent("UserUpdated", a, b);
            Client.UserVoiceStateUpdated += async (a, b, c) => DiscordEvent("UserVoiceStateUpdated", a, b, c);
            Client.VoiceServerUpdated += async (a) => DiscordEvent("VoiceServerUpdated", a);

            CommandManager.Commands.CommandExecuted += async (a, b, c) => DiscordEvent("CommandExecuted", a, b, c);
            CommandManager.Commands.Log += async (a) => DiscordEvent("CommandExecuted", a);
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
        }

        public static async Task Start()
        {
            Database.Init();

            await Client.LoginAsync(TokenType.Bot, Config.Token);
            await Client.StartAsync();
            Config.Token = "";

            await Task.Delay(-1);
        }

        [Event(Events.Ready)]
        public static void OnReady()
        {
            Console.WriteLine(DateTime.Now + " > Ready");
        }

        [Event(Events.Ready)]
        [RequireProduction]
        public static async Task OnReadyProd()
        {
            Console.WriteLine(DateTime.Now + " > Ready Production");
            var q = Quote.Random(100);
            Console.WriteLine(DateTime.Now + " > Got quote");
            try
            {
                Console.WriteLine(DateTime.Now + " > In Try");
                if (!(q is null))
                {
                    Console.WriteLine(DateTime.Now + " > q not null");
                    var s = q.ToString();
                    await Client.SetActivityAsync(new Game(s, ActivityType.Playing));
                    Console.WriteLine("Set status to ` " + s + " `");
                }
            } catch (Exception e) { Console.WriteLine(e); }
            Console.WriteLine(DateTime.Now + " > Ready Production 2");

            try
            {
                var botTesting = Client.GetChannel(814330280969895936) as SocketTextChannel;
                var e = new EmbedBuilder()
                    .WithAuthor(Client.CurrentUser)
                    .WithTitle("Bot Ready!")
                    .AddField("Latency", Client.Latency + "ms", true)
                    .AddField("Guilds", $"`{string.Join("`, `", Client.Guilds.Select(g => g.Name))}`", true)
                    .AddField("Channels", Client.Guilds.SelectMany(g => g.Channels).Count(), true)
                    .AddField("Status", "`" + q.ToString() + "`", true)
                    .AddField("Auto Restart", $"In {(int)(BotAdmin.MSUntilRestart / 1000)} Seconds", true)
                    .WithCurrentTimestamp()
                    .WithColor(Color.Teal);
                await botTesting.SendMessageAsync(embed: e.Build());
            }
            catch (Exception e) { Console.WriteLine(e); }
            Console.WriteLine(DateTime.Now + " > Ready Production 3");
        }

        public static void DiscordEvent(string eventName, params object[] data)
        {
            foreach (var t in Assembly.GetExecutingAssembly().GetTypes())
            {
                RuntimeHelpers.RunClassConstructor(t.TypeHandle);

                foreach (var m in t.GetMethods().Where(m2 => m2.IsStatic))
                {
                    if (!m.HasAttribute<EventAttribute>(out var att)) { continue; }
                    if (att.EventName != eventName) { continue; }
                    if (Config.Debug && m.HasAttribute<RequireProductionAttribute>()) { continue; }

                    try
                    {
                        m?.Invoke(null, data);
                    }
                    catch { Console.WriteLine($"Invalid event: {t.Name}.{m.Name}"); }
                }
            }
        }
    }
}
