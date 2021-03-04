using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Discord;
using Discord.Commands;
using Discord.Webhook;
using Discord.WebSocket;

namespace ForsetiFramework
{
    public class LoggingService
    {
        public DiscordWebhookClient ErrorsClient;

        public LoggingService()
        {
            ErrorsClient = new DiscordWebhookClient(BotManager.Instance.Config.ErrorWebhookUrl);
            ErrorsClient.Log += Client_Log;
            Console.WriteLine("Created webhook client.");
        }

        public async Task Client_Log(LogMessage arg)
        {
            Console.WriteLine(arg);

            if (!(arg.Exception is null))
            {
                // Errors to ignore
                if (arg.Exception is GatewayReconnectException) { return; }
                if (arg.Exception.Message.Equals("WebSocket connection was closed")) { return; }

                var color = arg.Severity == LogSeverity.Critical ? Color.Red : Color.Orange;

                var stack = arg.Exception.InnerException?.StackTrace;
                stack = stack is null ? arg.Exception.StackTrace : stack;
                var stackParts = new List<string>();

                while (stack.Length > 1000)
                {
                    var index = stack.Substring(0, 1000).LastIndexOf("\n");
                    if (index == -1)
                    {
                        stack = stack.Insert(500, "\n");
                        continue;
                    }
                    stackParts.Add(stack.Substring(0, index));
                    stack = stack.Substring(index, stack.Length - index);
                }
                stackParts.Add(stack);

                if (arg.Exception is CommandException ex)
                {

                    var e = new EmbedBuilder()
                        .WithColor(color)
                        .WithCurrentTimestamp()
                        .WithTitle(ex.InnerException.GetType().Name)
                        .AddField("User", ex.Context.User.Username + "#" + ex.Context.User.Discriminator, true)
                        .AddField("Location", ex.Context.Guild.Name + " > " + ex.Context.Channel.Name, true)
                        .AddField("Command", ex.Context.Message.Content, true)
                        .AddField("Exception Message", ex.InnerException.Message);
                    //.AddField("Exception Stack Trace", "```\n" + stack.Replace("\n", "\n ") + "\n```");

                    foreach (var s in stackParts)
                    {
                        e.AddField("Stack Trace", $"```\n{s}\n```");
                    }

                    await ErrorsClient.SendMessageAsync(embeds: new[] { e.Build() });
                }
                else
                {
                    var e = new EmbedBuilder()
                        .WithColor(color)
                        .WithCurrentTimestamp()
                        .WithTitle(arg.Exception.GetType().Name)
                        .AddField("Exception Message", arg.Exception.Message);
                    //.AddField("Exception Stack Trace", "```\n" + arg.Exception.StackTrace.Replace("\n", "\n ") + "\n```");

                    foreach (var s in stackParts)
                    {
                        e.AddField("Stack Trace", $"```\n{s}\n```");
                    }

                    await ErrorsClient.SendMessageAsync(embeds: new[] { e.Build() });
                }
            }
            else
            {
                var e = new EmbedBuilder()
                    .WithColor(Color.Green)
                    .WithCurrentTimestamp()
                    .WithTitle(arg.Message)
                    .AddField("Severity", arg.Severity);

                await ErrorsClient.SendMessageAsync(embeds: new[] { e.Build() });
            }
        }
    }
}
