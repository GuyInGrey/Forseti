using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;

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
    }
}
