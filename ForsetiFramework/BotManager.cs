using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Remoting.Contexts;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using ForsetiFramework.Modules;

namespace ForsetiFramework
{
    public class BotManager
    {
        public static BotManager Instance;

        public readonly Config Config;
        public readonly LoggingService Logger;
        public readonly DiscordSocketClient Client;
        public readonly CommandService Commands;
        public List<(ulong, IDisposable)> TypingStates = new List<(ulong, IDisposable)>();

        public BotManager()
        {
            Instance = this;
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
                IgnoreExtraArgs = true,
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

        public async Task Start()
        {
            await Commands.AddModulesAsync(Assembly.GetEntryAssembly(), null);
            Database.Initialize();
            PersistentRoles.Initialize();

            await Client.LoginAsync(TokenType.Bot, Config.Token);
            await Client.StartAsync();
            await Task.Delay(-1);
        }

        private async Task Commands_CommandExecuted(Optional<CommandInfo> arg1, ICommandContext context, IResult result)
        {
            if (Config.DoTypingStatus)
            {
                TypingStates.Where(t => t.Item1 == context.Message.Id).ToList().ForEach(t => t.Item2.Dispose());
                TypingStates.RemoveAll(t => t.Item1 == context.Message.Id);
            }

            if (!(result.ErrorReason is null))
            {
                // If unknown command or no permission, react with ❓
                if (result.ErrorReason == "Unknown command." 
                    || result.ErrorReason.Contains("must have the role")
                    || result.ErrorReason.Contains("owner of the bot."))
                {
                    await context.Message.AddReactionAsync(new Emoji("❓"));
                    return;
                }
                else if (result.ErrorReason.ToLower().Contains("must be in a guild")
                    || result.ErrorReason.Equals("User not found."))
                {
                    await context.Message.Channel.SendMessageAsync(result.ErrorReason);
                }
                else if (result.ErrorReason != null)
                {
                    await context.Message.Channel.SendMessageAsync("I've run into an error. I've let staff know.");
                    //await context.Message.Channel.SendMessageAsync(result.ErrorReason);
                }
            }

            // Log that the command was run in #bot-command-log
            var commandLog = Client.GetChannel(814970218555768882) as SocketTextChannel;
            var msg = context.Message;

            // e.g.
            // ✅ [2/28/2021 5:37:04 PM +00:00] [GuyInGrey#4066] [#bot-testing]> $ping
            var toSend = (result.IsSuccess ? "✅ " : "❌ ") +
                $"[{DateTimeOffset.UtcNow}] " +
                $"[{msg.Author.Username}#{msg.Author.Discriminator}] " +
                $"<#{msg.Channel.Id}>> `{msg.Content}`";

            await commandLog.SendMessageAsync(toSend);
        }

        private async Task HandleCommands(SocketMessage arg)
        {
            if (!(arg is SocketUserMessage msg)) { return; }

            // Make sure it's prefixed (with ! or bot mention), and that caller isn't a bot
            var argPos = 0;
            var hasPrefix = msg.HasStringPrefix(Config.Prefix, ref argPos) || msg.HasMentionPrefix(Client.CurrentUser, ref argPos);
            if (!(hasPrefix) || msg.Author.IsBot) { return; }

            (_, var Remainder) = msg.Content.SplitAt(argPos);
            var commandName = Remainder.Split(' ')[0];
            var suffix = string.Join(" ", Remainder.Split(' ').Skip(1));

            var tag = await Tags.GetTag(commandName);
            if (tag is null) // Normal command handling
            {
                if (Config.DoTypingStatus)
                {
                    TypingStates.Add((msg.Id, msg.Channel.EnterTypingState()));
                }
                var context = new SocketCommandContext(Client, msg);
                await Commands.ExecuteAsync(context, argPos, null);
            }
            else // Tag command handling
            {
                _ = PostTag(tag, arg.Channel, suffix, arg.Author);
            }
        }

        private async Task Client_Ready()
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

        private async Task PostTag(Tag tag, ISocketMessageChannel channel, string suffix, SocketUser author)
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
