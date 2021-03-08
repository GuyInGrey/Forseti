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

        static Utility()
        {
            BotManager.Client.ReactionAdded += Client_ReactionAdded;
        }

        private static async Task Client_ReactionAdded(Cacheable<IUserMessage, ulong> arg1, ISocketMessageChannel arg2, SocketReaction arg3)
        {
            if (arg3.UserId == BotManager.Client.CurrentUser.Id) { return; }
            if (!(HelpMenus.ContainsKey(arg1.Id))) { return; }
            var (context, helpMenuMsg) = HelpMenus[arg1.Id];

            await helpMenuMsg.RemoveReactionAsync(arg3.Emote, arg3.UserId);

            if (context.Message.Author.Id != arg3.UserId) { return; }

            var embed = helpMenuMsg.Embeds.First();
            var index = (await context.GetModules()).Where(m => m.Parent is null).ToList()
                .IndexOf((await context.GetModules()).FirstOrDefault(m => m.Name == embed.Title));
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
                foreach (var module in await Context.GetModules())
                {
                    if (module.Name.ToLower() == cmd)
                    {
                        var index = (await Context.GetModules()).Where(m => m.Parent is null).ToList().IndexOf(module);
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
                            .WithTitle(Config.Prefix + GetCommandString(c))
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
                await this.ReactError();
                return;
            }
        }

        public static string GetCommandString(CommandInfo c)
        {
            var s = c.Name.ToLower();
            var mod = c.Module;
            while (!(mod is null))
            {
                if (!(mod.Group?.ToLower() is null || mod.Group?.ToLower() == ""))
                {
                    s = mod.Group.ToLower() + " " + s;
                }
                mod = mod.Parent;
            }
            return s;
        }

        public static async Task PostHelpEmbed(int index, SocketCommandContext context, RestUserMessage toEdit = null)
        {
            var allModules = await context.GetModules();
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
                .Where(c => c.Module == module || (!(module.Group is null) && GetCommandString(c).StartsWith(module.Group.ToLower() + " "))))
            {
                if (!(await command.CheckPreconditionsAsync(context)).IsSuccess) { continue; }
                var syntaxAtt = (SyntaxAttribute)command.Attributes.FirstOrDefault(a => a is SyntaxAttribute);
                var syntax = syntaxAtt is null ? "" : syntaxAtt.Syntax;

                var sumString =
                    $"{(command.Aliases.Count > 1 ? "Aliases: `" + string.Join("`, `", command.Aliases.Skip(1)) + "`" : "")}" +
                    $"{(command.Summary == "" ? "" : $"\n{command.Summary}")}";
                sumString = sumString.Trim();
                builder.AddField(Config.Prefix + GetCommandString(command), sumString == string.Empty ? ":)" : sumString, true);
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
                    await this.ReactError();
                    return;
                }
            }
            else
            {
                if (name.Length > 255) { await this.ReactError(); return; }
                var t = new Tag()
                {
                    Name = name.ToLower(),
                    Content = con,
                    AttachmentURLs = Context.Message.Attachments.Select(a => a.Url).ToArray(),
                };
                await Tags.SetTag(t);
            }
            await this.ReactOk();
        }

        [Command("poll")]
        [RequireRole("staff")]
        [Summary("Create a poll for users to vote on. (Max 9 items)")]
        [Syntax("poll <name>\n<item1>\n<item2>\n[item3]\n...")]
        public async Task Poll([Remainder]string suffix)
        {
            var items = suffix.Split('\n');
            if (items.Length < 3) { await this.ReactError(); return; }
            if (items.Length > 10) { await this.ReactError(); return; }
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

        public async Task HelpOld(string cmd = "")
        {
            if (cmd != "")
            {
                foreach (var module in BotManager.Commands.Modules)
                {
                    foreach (var c in module.Commands)
                    {
                        if (c.Name.ToLower() != cmd.ToLower() ||
                            !(await c.CheckPreconditionsAsync(Context)).IsSuccess) { continue; }

                        var syntaxAtt = (SyntaxAttribute)c.Attributes.FirstOrDefault(a => a is SyntaxAttribute);
                        var syntax = syntaxAtt is null ? "None" : $"`{syntaxAtt.Syntax}`";

                        var e = new EmbedBuilder()
                            .WithTitle(Config.Prefix + c.Name.ToLower())
                            .WithCurrentTimestamp()
                            .AddField("Aliases", c.Aliases.Count == 1 ? "None" : $"`{string.Join("`, `", c.Aliases.Skip(1))}`")
                            .AddField("Summary", (c.Summary is null || c.Summary == "") ? "None" : c.Summary)
                            .AddField("Syntax", syntax)
                            .WithColor(Color.Blue);

                        await this.ReactOk();
                        await Context.Message.Author.SendMessageAsync(embed: e.Build());
                        return;
                    }
                }
                await this.ReactError();
                return;
            }

            var builders = new List<EmbedBuilder>();

            void checkBuilders(string moduleName)
            {
                if (builders.Count == 0 || builders.Last().Fields.Count >= 25 || builders.Last().Title != moduleName)
                {
                    builders.Add(new EmbedBuilder()
                        .WithTitle(moduleName)
                        .WithColor(moduleName != "Tag List" ? Color.Blue : Color.Green)
                        .WithCurrentTimestamp());
                    return;
                }
            }

            foreach (var tag in await Tags.GetTags())
            {
                checkBuilders("Tag List");

                var content = tag.Content is null ? "" : tag.Content;
                if (!(tag.Content is null) && tag.Content.Length > 100)
                {
                    content = tag.Content.Substring(0, 100) + "...";
                }
                content += $"\n__{tag.AttachmentURLs.Length} attachment{(tag.AttachmentURLs.Length == 1 ? "" : "s")}__";

                builders.Last().AddField(tag.Name, content);
            }

            foreach (var module in BotManager.Commands.Modules)
            {
                foreach (var command in module.Commands)
                {
                    if (!(await command.CheckPreconditionsAsync(Context)).IsSuccess) { continue; }
                    checkBuilders(module.Name);

                    var syntaxAtt = (SyntaxAttribute)command.Attributes.FirstOrDefault(a => a is SyntaxAttribute);
                    var syntax = syntaxAtt is null ? "" : syntaxAtt.Syntax;

                    var sumString =
                        $"{(command.Aliases.Count > 1 ? "Aliases: `" + string.Join("`, `", command.Aliases.Skip(1)) + "`" : "")}" +
                        $"{(command.Summary == "" ? "" : $"\n{command.Summary}")}";
                    sumString = sumString.Trim();
                    builders.Last().AddField(Config.Prefix + command.Name.ToLower(), sumString == string.Empty ? ":)" : sumString, true);
                }
            }

            await this.ReactOk();
            foreach (var b in builders)
            {
                _ = Context.Message.Author.SendMessageAsync(embed: b.Build());
            }
        }
    }
}
