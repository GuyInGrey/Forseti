using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.Rest;
using Discord.Webhook;
using Discord.WebSocket;

namespace ForsetiFramework.Modules
{
    public class BotAdmin : ModuleBase<SocketCommandContext>
    {
        [Command("ping")]
        [Summary("Pong!")]
        public async Task Ping()
        {
            var ping = Context.Client.Latency;

            var e = new EmbedBuilder()
                .WithTitle("Pong!")
                .WithDescription(ping + "ms")
                .WithColor(ping < 200 ? Color.Green : ping < 500 ? Color.LightOrange : Color.Red)
                .WithCurrentTimestamp();

            await ReplyAsync(embed: e.Build());
        }

        [Command("restart")]
        [RequireOwner]
        [Summary("Restarts the bot.")]
        public async Task Restart(bool update = true)
        {
            await this.ReactOk();
            await BotManager.Instance.Client.StopAsync();
            Program.Icon.Visible = false;

            if (!Config.Debug && update)
            {
                Process.Start("update.bat");
            }
            else if (!Config.Debug && !update)
            {
                Process.Start("restart.bat");
            }
            Environment.Exit(0);
        }

        [Command("testerror")]
        [RequireOwner]
        [Summary("Trigger an error to test the error webhook.")]
        public async Task TestError()
        {
            Console.WriteLine("Throwing Test Error");
            throw new Exception("Test Error!");
        }

        [Command("sayas")]
        [RequireRole("staff")]
        [Syntax("sayas <user> <text>")]
        public async Task SayAs(SocketGuildUser usr, [Remainder]string text)
        {
            await Context.Message.DeleteAsync();

            var ch = Context.Channel as SocketTextChannel;
            RestWebhook webhook = null;

            using (var client = new HttpClient())
            {
                var response = await client.GetAsync(usr.GetAvatarUrl());
                using (var stream = await response.Content.ReadAsStreamAsync())
                {
                    var name = usr.Nickname is null ? usr.Username : usr.Nickname;
                    webhook = await ch.CreateWebhookAsync(name, stream);
                }
            }

            var whclient = new DiscordWebhookClient(webhook);
            await whclient.SendMessageAsync(text);
            await whclient.DeleteWebhookAsync();
        }

        [Command("say")]
        [RequireRole("staff")]
        [Syntax("say <text>")]
        public async Task Say([Remainder]string text)
        {
            await Context.Message.DeleteAsync();
            await Context.Channel.SendMessageAsync(text);
        }
    }
}
