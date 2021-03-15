using System;
using System.Threading.Tasks;

using Discord;
using Discord.Commands;
using Discord.Webhook;
using Discord.WebSocket;

namespace ForsetiFramework
{
    [Obsolete("Use the Log class now.", true)]
    public class LoggingService
    {
        public DiscordWebhookClient ErrorsClient;

        public LoggingService()
        {
            ErrorsClient = new DiscordWebhookClient(BotManager.Config.ErrorWebhookUrl);
            ErrorsClient.Log += Client_Log;
            Console.WriteLine("Created webhook client.");
        }

        public async Task Client_Log(LogMessage arg)
        {
            Console.WriteLine(arg);

            if (arg.Exception is null)
            {
                var logEmbed = new EmbedBuilder()
                    .WithColor(Color.Green)
                    .WithCurrentTimestamp()
                    .WithTitle(arg.Message)
                    .AddField("Severity", arg.Severity)
                    .AddField("Debug?", Config.Debug, true);

                await ErrorsClient.SendMessageAsync(embeds: new[] { logEmbed.Build() });
                return;
            }

            // Errors to ignore
            if (arg.Exception is GatewayReconnectException) { return; }
            if (arg.Exception.Message.Equals("WebSocket connection was closed")) { return; }

            var color = arg.Severity == LogSeverity.Critical ? Color.Red : Color.Orange;
            var stackParts = (arg.Exception.InnerException?.StackTrace ?? arg.Exception.StackTrace).SplitWithLength(1000);
            var e = new EmbedBuilder()
                    .WithColor(color)
                    .WithCurrentTimestamp();

            if (arg.Exception is CommandException ex)
            {
                e = e.WithTitle(ex.InnerException.GetType().Name)
                    .AddField("User", ex.Context.User.Username + "#" + ex.Context.User.Discriminator, true)
                    .AddField("Location", ex.Context.Guild.Name + " > " + ex.Context.Channel.Name, true)
                    .AddField("Command", ex.Context.Message.Content, true)
                    .AddField("Exception Message", ex.InnerException.Message)
                    .AddField("Debug?", Config.Debug, true);
                stackParts.ForEach(s => e.AddField("Stack Trace", $"```\n{s}\n```"));

                await ErrorsClient.SendMessageAsync(embeds: new[] { e.Build() });
            }
            else
            {
                e = e.WithTitle(arg.Exception.GetType().Name)
                    .AddField("Exception Message", arg.Exception.Message)
                    .AddField("Debug?", Config.Debug, true);
                stackParts.ForEach(s => e.AddField("Stack Trace", $"```\n{s}\n```"));

                await ErrorsClient.SendMessageAsync(embeds: new[] { e.Build() });
            }
        }
    }
}
