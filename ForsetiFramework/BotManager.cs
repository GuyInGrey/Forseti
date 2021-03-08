using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

using Discord;
using Discord.Commands;
using Discord.WebSocket;

using ForsetiFramework.Modules;

namespace ForsetiFramework
{
    public static class BotManager
    {
        public static Config Config;
        public static LoggingService Logger;
        public static DiscordSocketClient Client;
        public static CommandService Commands;
        public static List<(ulong, IDisposable)> TypingStates = new List<(ulong, IDisposable)>();

        public static void Instantiate()
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
            Commands = new CommandService(new CommandServiceConfig()
            {
                CaseSensitiveCommands = false,
                DefaultRunMode = RunMode.Async,
                IgnoreExtraArgs = false,
                LogLevel = LogSeverity.Warning,
                SeparatorChar = ' ',
                ThrowOnError = false,
            });

            Commands.Log += Logger.Client_Log;
            Commands.CommandExecuted += Commands_CommandExecuted;
            Client.Log += Logger.Client_Log;
            Client.MessageReceived += HandleCommands;
            Client.Ready += Client_Ready;
        }

        public static async Task Start()
        {
            await Commands.AddModulesAsync(Assembly.GetEntryAssembly(), null);
            Database.Initialize();
            PersistentRoles.Initialize();

            await Client.LoginAsync(TokenType.Bot, Config.Token);
            await Client.StartAsync();
            Config.Token = "";
            await Task.Delay(-1);
        }

        private static async Task Commands_CommandExecuted(Optional<CommandInfo> arg1, ICommandContext context, IResult result)
        {
            TypingStates.Where(t => t.Item1 == context.Message.Id).ToList().ForEach(t => t.Item2.Dispose());
            TypingStates.RemoveAll(t => t.Item1 == context.Message.Id);

            if (!result.IsSuccess)
            {
                if (result.Error == CommandError.UnknownCommand || result.Error == CommandError.UnmetPrecondition)
                {
                    await context.ReactQuestion();
                }
                else if (result.Error == CommandError.ParseFailed || 
                    result.Error == CommandError.ObjectNotFound ||
                    result.Error == CommandError.BadArgCount)
                {
                    var cmd = context.Message.GetCommand();
                    await context.Channel.SendMessageAsync($"Invalid, see `!help {cmd}`.");
                }
                else
                {
                    await context.Channel.SendMessageAsync($"I've run into an error ({result.Error}). I've let staff know.");
                    var e = new EmbedBuilder()
                        .WithTitle("Failed command.")
                        .WithDescription($"`{context.Message.Content}` - {context.User.Username}#{context.User.Discriminator}")
                        .AddField("Error", $"`{result.Error}`")
                        .AddField("Error Reason", $"`{result.ErrorReason}`")
                        .WithCurrentTimestamp();
                    await Logger.ErrorsClient.SendMessageAsync(embeds: new[] { e.Build() });
                }
            }
        }

        private static async Task HandleCommands(SocketMessage arg)
        {
            if (!(arg is SocketUserMessage msg)) { return; }

            // Make sure it's prefixed (with ! or bot mention), and that caller isn't a bot
            var argPos = 0;
            var hasPrefix = msg.HasStringPrefix(Config.Prefix, ref argPos) || msg.HasMentionPrefix(Client.CurrentUser, ref argPos);
            if (!(hasPrefix) || msg.Author.IsBot) { return; }

            var remainder = msg.Content.SplitAt(argPos).right;
            (var commandName, var suffix) = remainder.SplitAt(remainder.IndexOf(' '));

            var tag = await Tags.GetTag(commandName);
            if (tag is null) // Normal command handling
            {
                var context = new SocketCommandContext(Client, msg);
                var cmd = Commands.Commands.FirstOrDefault(c => c.Name == commandName);
                if (!(cmd is null))
                {
                    if (!(cmd.Attributes.FirstOrDefault(a => a is TypingAttribute) is null))
                    {
                        TypingStates.Add((msg.Id, msg.Channel.EnterTypingState()));
                    }
                }
                await Commands.ExecuteAsync(context, argPos, null);
            }
            else // Tag command handling
            {
                _ = PostTag(tag, arg.Channel, suffix, arg.Author);
            }
        }

        private static async Task Client_Ready()
        {
            Console.WriteLine("In guilds: " + string.Join(", ", Client.Guilds.Select(g => g.Name)));

            if (Config.Debug) { return; } // Don't post debug ready messages
            var botTesting = Client.GetChannel(814330280969895936) as SocketTextChannel;
            var e = new EmbedBuilder()
                .WithAuthor(Client.CurrentUser)
                .WithTitle("Bot Ready!")
                .WithCurrentTimestamp()
                .WithColor(Color.Teal);
            await botTesting.SendMessageAsync(embed: e.Build());
        }

        private static async Task PostTag(Tag tag, ISocketMessageChannel channel, string suffix, SocketUser author)
        {
            if (tag.Content != string.Empty)
            {
                await channel.SendMessageAsync(tag.Content.Replace("{author}", author.Mention).Replace("{suffix}", suffix));
            }
            if (!(tag.AttachmentURLs is null || tag.AttachmentURLs.Length == 0))
            {
                foreach (var a in tag.AttachmentURLs)
                {
                    await channel.SendMessageAsync(a);
                }
            }
        }
    }
}
