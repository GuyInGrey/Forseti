﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using ForsetiFramework;
using ForsetiFramework.Modules;

namespace ForsetiFramework.Modules
{
    public class Help : ModuleBase<SocketCommandContext>
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

                    Console.WriteLine(command.Summary);
                    var sumString =
                        $"{(command.Aliases.Count > 1 ? "Aliases: `" + string.Join("`, `", command.Aliases.Skip(1)) + "`" : "")}" +
                        $"{(command.Summary == "" ? "" : $"\n{command.Summary}")}";
                    sumString = sumString.Trim();
                    builders.Last().AddField(Config.Prefix + command.Name.ToLower(), sumString == string.Empty ? ":)" : sumString, true);
                }
            }

            foreach (var b in builders)
            {
                await this.ReactOk();
                await Context.Message.Author.SendMessageAsync(embed: b.Build());
            }
        }
    }
}
