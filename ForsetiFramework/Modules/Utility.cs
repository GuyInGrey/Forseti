using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Discord;
using Discord.Commands;
using Discord.Rest;
using Discord.WebSocket;

namespace ForsetiFramework.Modules
{
    public class Utility : ModuleBase<SocketCommandContext>
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

        [Command("help")]
        [Alias("?", "gethelp")]
        [Summary("Get a list of tags and commands.")]
        [Syntax("help [command]")]
        public async Task HelpNew(string cmd = "")
        {
            cmd = cmd.ToLower();
            if (cmd == "")
            {
                await PostHelpEmbed(0, Context);
            }
            else
            {
                foreach (var module in Context.GetModules())
                {
                    if (module.Name.ToLower() == cmd)
                    {
                        var index = Context.GetModules().Where(m => m.Parent is null).ToList().IndexOf(module);
                        await PostHelpEmbed(index, Context);
                        return;
                    }

                    foreach (var c in module.Commands)
                    {
                        if (c.Name.ToLower() != cmd ||
                            !(await c.CheckPreconditionsAsync(Context)).IsSuccess) { continue; }

                        var syntaxAtt = (SyntaxAttribute)c.Attributes.FirstOrDefault(a => a is SyntaxAttribute);
                        var syntax = syntaxAtt is null ? "None" : $"`{syntaxAtt.Syntax}`";

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
                        return;
                    }
                }
                await Context.ReactError();
                return;
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
                var syntax = syntaxAtt is null ? "" : syntaxAtt.Syntax;

                var sumString =
                    $"{(command.Aliases.Count > 1 ? "Aliases: `" + string.Join("`, `", command.Aliases.Skip(1)) + "`" : "")}" +
                    $"{(command.Summary == "" ? "" : $"\n{command.Summary}")}";
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

        [Command("tag")]
        [RequireRole("staff")]
        [Summary("Sets or deletes tag commands.")]
        [Syntax("tag <name> [content]")]
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

        [Command("poll")]
        [RequireRole("staff")]
        [Summary("Create a poll for users to vote on. (Max 9 items)")]
        [Syntax("poll <name>\n<item1>\n<item2>\n[item3]\n...")]
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
    }
}
