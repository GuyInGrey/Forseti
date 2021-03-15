using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;

namespace ForsetiFramework
{
    [Serializable]
    public class Log
    {
        public string Title = "";
        public string Content = "";
        public Color Color = Color.Green;
        public Dictionary<string, string> Fields = new Dictionary<string, string>();
        public ulong ChannelToPost = LogChannel.BotTesting;
        public DateTime Timestamp = DateTime.Now;
        public IUser Author = BotManager.Client.CurrentUser;

        public async Task Post()
        {
            PostToConsole();
            var e = ToEmbed().Build();

            // Get text channel
            if (!(BotManager.Client.GetChannel(ChannelToPost) is SocketChannel channel)) { return; }
            if (!(channel is ITextChannel ch)) { return; }

            await ch.SendMessageAsync(embed: e);
        }

        public Log WithField(string name, object content)
        {
            Fields.Add(name, content.ToString());
            return this;
        }

        public EmbedBuilder ToEmbed()
        {
            var e = new EmbedBuilder()
                .WithTimestamp(new DateTimeOffset(Timestamp))
                .WithTitle(Title)
                .WithColor(Color)
                .WithAuthor(Author);

            var con = Content.Trim();
            if (con.Length == 0) { }
            else if (con.Length <= 2040)
            {
                e.WithDescription("```\n" + con + "\n```");
            }
            else
            {
                var contentParts = con.SplitWithLength(1010);
                contentParts.ForEach(c => e.AddField("Content", $"```\n{c}\n```"));
            }

            var fields = Fields.Select(f => new EmbedFieldBuilder()
                .WithName(f.Key)
                .WithValue(f.Value)
                .WithIsInline(true));
            e.WithFields(fields);

            if (Config.Debug)
            {
                e.WithFooter("Debug Instance");
            }

            return e;
        }

        private void PostToConsole()
        {
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write($"[{Timestamp}] ");
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine(Title);
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine(Content);
            Fields.ToList().ForEach(f =>
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine(f.Key);
                Console.ForegroundColor = ConsoleColor.White;
                Console.WriteLine(f.Value);
            });
            Console.WriteLine();
        }

        public static Log FromException(Exception e)
        {
            var stackParts = (e.InnerException?.StackTrace ?? e.StackTrace).SplitWithLength(1000);
            if (e is CommandException ex)
            {
                var logEx = new Log()
                {
                    Title = ex.InnerException.GetType().Name,
                    Content = ex.InnerException.Message,
                    Color = Color.Orange,
                    Fields = new Dictionary<string, string>()
                    {
                        { "User", ex.Context.User.Username + "#" + ex.Context.User.Discriminator },
                        { "Location", ex.Context.Guild.Name + " > " + ex.Context.Channel.Name },
                        { "Command", ex.Context.Message.Content },
                    }
                };

                stackParts.ForEach(s => logEx.Fields.Add("Stack Trace", $"```\n{s}\n```"));
                return logEx;
            }

            var log = new Log()
            {
                Title = e.GetType().Name,
                Content = e.Message,
                Color = Color.Orange,
            };
            stackParts.ForEach(s => log.Fields.Add("Stack Trace", $"```\n{s}\n```"));
            return log;
        }

        public static Log InformationFromContext(ICommandContext c, string info)
        {
            return new Log()
            {
                Title = "Info",
                Content = info,
                Color = Color.Blue,
                Fields = new Dictionary<string, string>()
                {
                    { "User", c.User.ToString() },
                    { "Location", c.Guild.Name + " > " + c.Channel.Name },
                    { "Command", c.Message.Content },
                },
            };
        }

        public static Log FromLogMessage(LogMessage m)
        {
            if (m.Exception is null)
            {
                return new Log()
                {
                    Title = m.Message,
                }.WithField("Severity", m.Severity);
            }

            if (m.Exception is GatewayReconnectException) { return null; }
            if (m.Exception.Message.Equals("WebSocket connection was closed")) { return null; }

            return FromException(m.Exception);
        }
    }

    public class LogChannel
    {
        public static ulong ModLogs = 814327531216961616;
        public static ulong BotTesting = 814330280969895936;
        public static ulong General = 814328175881355304;
    }
}
