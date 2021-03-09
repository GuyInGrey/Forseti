using System;
using System.Diagnostics;
using System.Net.Http;
using System.Numerics;
using System.Threading.Tasks;
using System.Windows.Forms;
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
            await Context.ReactOk();
            await BotManager.Client.StopAsync();
            Program.Icon.Visible = false;

            if (!Config.Debug)
            {
                var p = new Process
                {
                    StartInfo = new ProcessStartInfo()
                    {
                        FileName = update ? "update.bat" : "restart.bat",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                    }
                };
                p.Start();
            }

            Application.Exit();
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
        [Typing]
        public async Task Say([Remainder]string text)
        {
            await Context.Message.DeleteAsync();
            await Context.Channel.SendMessageAsync(text);
        }

        [Command("fib")]
        [Alias("fibonnaci")]
        [Syntax("fib <n>")]
        [RequireRole("staff")]
        [Typing]
        public async Task Fib(int n)
        {
            if (n <= 0) { await ReplyAsync("No."); return; }

            if (n == 1) { await ReplyAsync("0"); return; }
            if (n == 2) { await ReplyAsync("1"); return; }

            var startTime = HighResolutionDateTime.UtcNow;
            (var a, var b) = ((BigInteger)1, (BigInteger)1); for (var i = 0; i < n - 3; i++) 
            {
                b = a + b; a = b - a; 
                if ((HighResolutionDateTime.UtcNow - startTime).TotalMilliseconds > 1000 * 60 * 5)
                {
                    await Context.ReactError();
                    await ReplyAsync("Computation took more than 2 minutes, cancelled. Sorry!");
                    return;
                }
            }
            var took = (HighResolutionDateTime.UtcNow - startTime).TotalMilliseconds;
            await Context.ReactOk();

            var s = b.ToString();
            var parts = await s.SplitWithLength(1950);

            if (parts.Count > 5)
            {
                await ReplyAsync($"__Fibonacci Results__\nn = {n}\nTook {took} ms.\nfib(n) has {s.Length} digits.\n" +
                    $"Did not post due to requiring {parts.Count} messages.");
                return;
            }

            foreach (var p in parts)
            {
                await ReplyAsync("```\n" + p + "\n```");
            }

            await ReplyAsync($"__Fibonacci Results__\nn = {n}\nTook {took} ms.\nfib(n) has {s.Length} digits.");
        }
    }
}
