using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;

namespace ForsetiFramework.Modules
{
    public class Utility : ModuleBase<SocketCommandContext>
    {
        [Command("help")]
        [Summary("Get a list of tags and commands.")]
        [Syntax("help [command]")]
        public async Task GetHelp(string cmd = "")
        {
            if (cmd != "")
            {
                foreach (var module in BotManager.Instance.Commands.Modules)
                {
                    foreach (var c in module.Commands)
                    {
                        if (c.Name.ToLower() != cmd.ToLower()) { continue; }

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

            foreach (var module in BotManager.Instance.Commands.Modules)
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
    }
}
