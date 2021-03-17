using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Numerics;
using System.Threading.Tasks;
using System.Windows.Forms;

using Discord;
using Discord.Commands;
using Discord.Rest;
using Discord.Webhook;
using Discord.WebSocket;
using ForsetiFramework.Utility;
using Processing;

namespace ForsetiFramework.Modules
{
    public partial class BotAdmin : ModuleBase<SocketCommandContext>
    {
        [Command("restart"), RequireOwner]
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

        [Command("testerror"), RequireOwner]
        [Summary("Trigger an error to test the error webhook.")]
        public async Task TestError()
        {
            Console.WriteLine("Throwing Test Error");
            throw new Exception("Test Error!");
        }

        [Command("sayas"), RequireRole("staff"), Syntax("sayas <user> <text>")]
        public async Task SayAs(SocketGuildUser usr, [Remainder]string text)
        {
            await Context.Message.DeleteAsync();

            var ch = Context.Channel as SocketTextChannel;
            RestWebhook webhook = null;

            var url = usr.GetAvatarUrl();
            url = url is null ? usr.GetDefaultAvatarUrl() : url;

            using (var client = new HttpClient())
            {
                var response = await client.GetAsync(url);
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

        [Command("say"), RequireRole("staff"), Syntax("say <text>"), Typing]
        public async Task Say([Remainder]string text)
        {
            await Context.Message.DeleteAsync();
            await Context.Channel.SendMessageAsync(text);
        }

        [Command("fib"), Alias("fibonnaci"), Syntax("fib <n>"), RequireRole("staff"), Typing]
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
            var parts = s.SplitWithLength(1950);

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

        [Command("setstatus"), RequireRole("staff")]
        public async Task SetStatus([Remainder]string text)
        {
            await BotManager.Client.SetGameAsync(text, null, ActivityType.CustomStatus);
        }

        [Command("mandelbrot")]
        public async Task Mandelbrot()
        {
            var maxToRun = 40;
            var frames = 3600;
            var path = @"C:\RenderingTemp\";
            var colors = Enumerable.Range(0, maxToRun + 1).Select(i => Paint.Lerp(Paint.White, Paint.Black, i / (float)maxToRun)).ToArray();
            if (Directory.Exists(path)) { try { Directory.Delete(path, true); } catch { } }
            Directory.CreateDirectory(path);

            float Mandelbrot(float x, float y, float max, float pow)
            {
                var c = new Complex(x, y);
                var z = Complex.Zero;
                var i = 0;
                while (Complex.Abs(z) <= 2 && i < max) { z = Complex.Pow(z, pow) + c; i++; }
                if (i == max) { return max; }
                return (float)(i + 1 - Math.Log(Math.Log(Complex.Abs(z))) / Math.Log(2));
            }

            Sprite Render(int j)
            {
                var s = new Sprite(4000, 4000);
                var pixels = new byte[s.Width * s.Height * 4];
                for (var q = 0; q < s.Width * s.Height; q++)
                {
                    (var x, var y) = (q % s.Width, q / s.Width);
                    var x2 = PMath.Map(x, 0, s.Width, -2.25f, 0.75f);
                    var y2 = PMath.Map(y, 0, s.Height, -1.5f, 1.5f);
                    var i = Mandelbrot(x2, y2, maxToRun, PMath.Map(j / (float)frames, 0, 1, 0, 4));
                    i = i == 0 ? 0.000001f : i;
                    var c = float.IsNaN(i) ? Paint.Red : Paint.LerpMultiple(colors, i / (float)maxToRun);
                    var pos = q * 4;
                    pixels[pos] = (byte)c.B;
                    pixels[pos + 1] = (byte)c.G;
                    pixels[pos + 2] = (byte)c.R;
                    pixels[pos + 3] = (byte)255;
                }
                s.Art.SetPixels(pixels);
                return s;
            }
            await ReplyAsync($"Rendering...");
            var time = DateTime.Now;
            var totalFinished = 0;
            Parallel.For(0, frames, new ParallelOptions() { MaxDegreeOfParallelism = 3 }, i =>
            {
                var s = Render(i);
                s.Save($@"{path}{i:000000}.png");
                s.Dispose();
                if (time.Add(new TimeSpan(0, 1, 0)) < DateTime.Now)
                {
                    time = DateTime.Now;
                    ReplyAsync($"🦥 `{((float)totalFinished / (float)frames) * 100f}%`, `{totalFinished} / {frames}`");
                }
                totalFinished++;
            });
            await ReplyAsync("Render complete, compiling into video...");
            await Extensions.FFMPEG(" -framerate 60 -i %06d.png -c:v libx264 -r 60 output.mp4", path);
            await ReplyAsync($"{Context.User.Mention}, uploading...");
            await Context.Channel.SendFileAsync($"{path}output.mp4");
        }
    }
}
