using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Discord;
using Discord.Commands;
using Discord.Rest;
using Discord.WebSocket;

namespace ForsetiFramework.Modules
{
    public class UtilityCmd : ModuleBase<SocketCommandContext>
    {
        static Dictionary<ulong, (SocketCommandContext context, RestUserMessage helpMenuMsg)> HelpMenus =
            new Dictionary<ulong, (SocketCommandContext, RestUserMessage)>();

        [Event(Events.ReactionAdded)]
        public static async Task ReactionAdded(Cacheable<IUserMessage, ulong> arg1, ISocketMessageChannel arg2, SocketReaction arg3)
        {
            if (arg3.UserId == BotManager.Client.CurrentUser.Id) { return; }
            if (!(HelpMenus.ContainsKey(arg1.Id))) { return; }
            var (context, helpMenuMsg) = HelpMenus[arg1.Id];

            await helpMenuMsg.RemoveReactionAsync(arg3.Emote, arg3.UserId);

            if (context.Message.Author.Id != arg3.UserId) { return; }

            var embed = helpMenuMsg.Embeds.First();
            var index = context.GetModules().Where(m => m.Parent is null).ToList()
                .IndexOf(context.GetModules().FirstOrDefault(m => m.Name == embed.Title));
            var mod = arg3.Emote.Name == "⬅️" ? -1 : arg3.Emote.Name == "➡️" ? 1 : 0;
            await PostHelpEmbed(index + mod, context, helpMenuMsg);
        }

        [Command("help"), Alias("?", "gethelp"), Syntax("help [command]")]
        [Summary("Get a list of tags and commands.")]
        public async Task HelpNew([Remainder]string cmd = "")
        {
            cmd = cmd.ToLower();
            if (cmd == "")
            {
                await PostHelpEmbed(0, Context);
            }
            else
            {
                var showed = false;
                foreach (var module in Context.GetModules())
                {
                    if (module.Name.ToLower() == cmd)
                    {
                        var index = Context.GetModules().Where(m => m.Parent is null).ToList().IndexOf(module);
                        await PostHelpEmbed(index, Context);
                        showed = true;
                    }

                    foreach (var c in module.Commands)
                    {
                        if (!c.Aliases.Select(s => s.ToLower()).Contains(cmd.ToLower()) ||
                            !(await c.CheckPreconditionsAsync(Context)).IsSuccess) { continue; }

                        var syntaxAtt = (SyntaxAttribute)c.Attributes.FirstOrDefault(a => a is SyntaxAttribute);
                        var syntax = syntaxAtt is null ? $"`{c.GetCommandString()}`" : $"`{syntaxAtt.Syntax}`";

                        var e = new EmbedBuilder()
                            .WithTitle(Config.Prefix + c.GetCommandString())
                            .WithCurrentTimestamp()
                            .WithDescription((c.Summary is null || c.Summary == "") ? "No Summary" : c.Summary)
                            .AddField("Syntax", syntax, true)
                            .WithColor(Color.Blue);

                        if (c.Aliases.Count > 1)
                        {
                            e.AddField("Aliases", $"`{string.Join("`, `", c.Aliases.Skip(1))}`", true);
                        }

                        await Context.Channel.SendMessageAsync(embed: e.Build());
                        showed = true;
                    }
                }
                if (!showed)
                {
                    await Context.ReactError();
                }
            }
        }

        public static async Task PostHelpEmbed(int index, SocketCommandContext context, RestUserMessage toEdit = null)
        {
            var allModules = context.GetModules();
            var modules = allModules.Where(m => m.Parent is null).ToList();

            index = (index + modules.Count) % modules.Count;
            var module = modules[index];

            var desc = "";
            var i = 0;
            foreach (var m in modules)
            {
                if (m.Name == module.Name) { desc += "**"; }
                desc += m.Name;
                if (m.Name == module.Name) { desc += "**"; }
                if (i != modules.Count - 1) { desc += " | "; }
                i++;
            }

            var builder = new EmbedBuilder()
                .WithTitle(module.Name)
                .WithColor(Color.Blue)
                .WithCurrentTimestamp()
                .WithDescription(desc.Trim());

            foreach (var command in allModules.SelectMany(m => m.Commands)
                .Where(c => c.Module == module || (!(module.Group is null) && c.GetCommandString().StartsWith(module.Group.ToLower() + " "))))
            {
                if (!(await command.CheckPreconditionsAsync(context)).IsSuccess) { continue; }

                var syntaxAtt = (SyntaxAttribute)command.Attributes.FirstOrDefault(a => a is SyntaxAttribute);
                var syntax = syntaxAtt is null ? $"`{command.GetCommandString()}`" : $"`{syntaxAtt.Syntax}`";

                var aliases = command.Aliases.Skip(1).OrderBy(s => s.Length).ToList();
                if (aliases.Count() > 2) { aliases = aliases.Take(2).ToList(); }
                var sumString =
                    $"{(command.Aliases.Count > 1 ? "Aliases: `" + string.Join("`, `", aliases) + "`" : "")}" +
                    $"{(command.Summary == "" ? "" : $"\n{command.Summary}")}" +
                    $"\n\n{syntax}";
                sumString = sumString.Trim();
                builder.AddField(Config.Prefix + command.GetCommandString(), sumString == string.Empty ? ":)" : sumString, true);
            }

            if (!(toEdit is null))
            {
                await toEdit.ModifyAsync(m =>
                {
                    m.Embed = builder.Build();
                });
            }
            else
            {
                var m = await context.Channel.SendMessageAsync(embed: builder.Build());
                HelpMenus.Add(m.Id, (context, m));
                await m.AddReactionsAsync(new[] { new Emoji("⬅️"), new Emoji("➡️") });
            }
        }

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

        [Command("tag"), RequireRole("staff"), Syntax("tag <name> [content]")]
        [Summary("Sets or deletes tag commands.")]
        public async Task Tag(string name, [Remainder]string con = "")
        {
            if (con == "" && Context.Message.Attachments.Count == 0)
            {
                if (!await Tags.RemoveTag(name))
                {
                    await Context.ReactError();
                    return;
                }
            }
            else
            {
                if (name.Length > 255) { await Context.ReactError(); return; }
                var t = new Tag()
                {
                    Name = name.ToLower(),
                    Content = con,
                    AttachmentURLs = Context.Message.Attachments.Select(a => a.Url).ToArray(),
                };
                await Tags.SetTag(t);
            }
            await Context.ReactOk();
        }

        [Command("poll"), RequireRole("staff"), Syntax("poll <name>\n<item1>\n<item2>\n[item3]\n...")]
        [Summary("Create a poll for users to vote on. (Max 9 items)")]
        public async Task Poll([Remainder]string suffix)
        {
            var items = suffix.Split('\n');
            if (items.Length < 3) { await Context.ReactError(); return; }
            if (items.Length > 10) { await Context.ReactError(); return; }
            await Context.Message.DeleteAsync();

            // Most places don't render this right (including in Visual Studio), 
            // but these are the unicode keycap digits. 1⃣ is the same as :one: in Discord.
            var nums = "1⃣ 2⃣ 3⃣ 4⃣ 5⃣ 6⃣ 7⃣ 8⃣ 9⃣".Split(' ');

            var pollTitle = items[0];
            var pollItems = items.Skip(1).ToList();
            var itemStrings = pollItems.Select(p => $"{nums[pollItems.IndexOf(p)]} {p}");

            var e = new EmbedBuilder()
                .WithTitle(pollTitle)
                .WithDescription(string.Join("\n", itemStrings))
                .WithColor(Color.Green)
                .WithCurrentTimestamp();
            var m = await ReplyAsync(embed: e.Build());
            for (var i = 0; i < pollItems.Count; i++)
            {
                await m.AddReactionAsync(new Emoji(nums[i]));
            }
        }

        public enum TimeUnit { Minutes, Hours, Days, Minute, Hour, Day }

        [Command("remindmein")]
        [Syntax("remindmein <number> <minutes/hours/days> <reminder>")]
        [Summary("Want a reminder? The bot will send you a message at the give time.")]
        public async Task RemindMeIn(int num, TimeUnit unit, [Remainder]string text)
        {
            var span = new TimeSpan(
                unit == TimeUnit.Days || unit == TimeUnit.Day ? num : 0, 
                unit == TimeUnit.Hours || unit == TimeUnit.Hour ? num : 0, 
                unit == TimeUnit.Minutes || unit == TimeUnit.Minute ? num : 0, 0);
            var newTime = DateTime.Now.Add(span);
            "INSERT INTO reminders (user, time, reminder) VALUES (@p0, @p1, @p2)".NonQuery(Context.User.Id, newTime, text);
            await Context.ReactOk();
        }

        [Clockwork(5000)]
        public static async Task RemindMeClockwork()
        {
            var now = DateTime.Now;
            var q = "SELECT * FROM reminders WHERE time < @p0".Query(now);
            try
            {
                while (q.Read())
                {
                    var time = DateTime.Parse(q["time"].ToString());
                    var userId = ulong.Parse(q["user"].ToString());
                    var text = q["reminder"].ToString();
                    var user = BotManager.Client.GetUser(userId);
                    if (user is null) { continue; }
                    try
                    {
                        await user.SendMessageAsync($"Reminder for {time}: `{text}`");
                    } catch { }
                }
            }
            finally { q?.Dispose(); }

            "DELETE FROM reminders WHERE time < @p0".NonQuery(now);
        }
    }
}
