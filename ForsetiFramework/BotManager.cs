using System;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

using Discord;
using Discord.Commands;
using Discord.WebSocket;
using ForsetiFramework.Constructs;
using ForsetiFramework.Modules;

namespace ForsetiFramework
{
    public static class BotManager
    {
        public static Config Config;
        public static DiscordSocketClient Client;

        public static void Init()
        {
            Config = Config.Load(Config.Path + "config.json");

            Client = new DiscordSocketClient(new DiscordSocketConfig()
            {
                AlwaysDownloadUsers = true,
                DefaultRetryMode = RetryMode.AlwaysRetry,
                LargeThreshold = 250,
                LogLevel = LogSeverity.Warning,
                RateLimitPrecision = RateLimitPrecision.Millisecond,
                ExclusiveBulkDelete = true,
            });
            Client.Log += async (msg) => await Log.FromLogMessage(msg).Post();
            Client.MessageReceived += CommandManager.HandleCommands;
            Client.MessageUpdated += async (a, b, c) => await CommandManager.HandleCommands(b);

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
        }

        public static async Task Start()
        {
            Database.Init();

            await Client.LoginAsync(TokenType.Bot, Config.Token);
            await Client.StartAsync();
            Config.Token = "";

            await Task.Delay(-1);
        }

        [Event(Events.Ready), Priority(1)]
        public static async Task OnReady()
        {
            try
            {
                var q = Quote.Random(100);
                if (!(q is null))
                {
                    var s = q.ToString();
                    await Client.SetActivityAsync(new Discord.Game(s, ActivityType.Playing));
                    Console.WriteLine("Set status to ` " + s + " `");
                }
            }
            catch (Exception e) { Console.WriteLine(e); }
        }

        [Event(Events.Ready), RequireProduction, Priority(0)]
        public static async Task OnReadyProd()
        {
            await new Log()
            {
                Title = "Bot ready!",
            }
                .WithField("Latency", Client.Latency + "ms")
                .WithField("Status", $"`{Client.Activity.Name}`")
                .Post();
        }

        public static void DiscordEvent(string eventName, params object[] data)
        {
            Console.WriteLine(eventName);

            foreach (var t in Assembly.GetExecutingAssembly().GetTypes())
            {
                RuntimeHelpers.RunClassConstructor(t.TypeHandle);

                t.GetMethods().Where(m =>
                {
                    if (!m.HasAttribute<EventAttribute>(out var att)) { return false; }
                    if (att.EventName != eventName) { return false; }
                    if (Config.Debug && m.HasAttribute<RequireProductionAttribute>()) { return false; }

                    return true;
                }).OrderBy(m =>
                {
                    if (m.HasAttribute<PriorityAttribute>(out var att))
                    {
                        return -att.Priority;
                    }
                    return 0;
                }).ToList().ForEach(m =>
                {
                    try
                    {
                        var r = m?.Invoke(null, data);
                    }
                    catch (Exception err) { Console.WriteLine($"Invalid event: {t.Name}.{m.Name}\n" + err); }
                });
            }
        }
    }
}
