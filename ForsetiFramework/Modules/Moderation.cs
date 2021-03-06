using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using Discord;
using Discord.Commands;
using Discord.WebSocket;

namespace ForsetiFramework.Modules
{
    public class Moderation : ModuleBase<SocketCommandContext>
    {
        static string[] HardNoWords => File.ReadAllText(Config.Path + "badwords.txt").ToLower().Replace("\r", "")
            .Split('\n').Select(s => s.Trim()).Where(s => s != "").ToArray();
        static string[] SoftNo => File.ReadAllText(Config.Path + "softwords.txt").ToLower().Replace("\r", "")
            .Split('\n').Select(s => s.Trim()).Where(s => s != "").ToArray();

        public static SocketTextChannel ModLogs => BotManager.Client.GetChannel(814327531216961616) as SocketTextChannel;
        // User Info, This Is Fine, Warn & Delete, Mute & Delete
        static Emoji[] CardReactions = new[] { "👤", "✅", "⚠", "🔇" }.Select(s => new Emoji(s)).ToArray();

        static Moderation()
        {
            if (!Config.Debug) // Only do moderation if not debug bot, to prevent duplicates
            {
                BotManager.Client.MessageReceived += async (msg) => { if (msg is SocketUserMessage msg2) { await CheckMessage(msg2); } };
                BotManager.Client.MessageUpdated += async (a, msg, c) => { if (msg is SocketUserMessage msg2) { await CheckMessage(msg2); } };
                BotManager.Client.UserBanned += async (usr, guild) => await ModLogs.SendMessageAsync($"{usr.Mention} ({usr.Id}) has been banned.");
                BotManager.Client.UserUnbanned += async (usr, guild) => await ModLogs.SendMessageAsync($"{usr.Mention} ({usr.Id}) has been unbanned.");
                BotManager.Client.UserLeft += async (usr) => await ModLogs.SendMessageAsync($"{usr.Mention} has left or was kicked.");

                BotManager.Client.ReactionAdded += Client_ReactionAdded;
                BotManager.Client.MessagesBulkDeleted += Client_MessagesBulkDeleted;
            }
        }

        public static async Task CheckMessage(SocketUserMessage m)
        {
            if (m.Author.Id == BotManager.Client.CurrentUser.Id || m.Content is null) { return; }
            var guild = (m.Channel as SocketGuildChannel).Guild;

            var content = m.Content.Replace("`", " ").Replace("\n", " ");
            var card = new EmbedBuilder()
                .WithTitle("Forseti Entry")
                .WithDescription(content)
                .WithAuthor(m.Author)
                .WithColor(Color.Orange)
                .WithCurrentTimestamp()
                .AddField("Channel", $"<#{m.Channel.Id}>", true)
                .AddField("Jump To Post", $@"[Link](https://discord.com/channels/{guild.Id}/{m.Channel.Id}/{m.Id})", true)
                .AddField("User ID", m.Author.IsBot ? "Bot or Webhook" : m.Author.Id.ToString(), true);

            // Strict, auto-delete
            var clearedContent = Regex.Replace(content.ToLower(), "[^a-z1-9 -_]", string.Empty);
            var clearedParts = clearedContent.Split(new[] { " ", "-", "_" }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var b2 in clearedParts)
            {
                foreach (var b in HardNoWords)
                {
                    if (b2.Equals(b))
                    {
                        card.AddField("Reason", "`b`", true);
                        card.AddField("Auto-Deleted", "Yes", true);
                        card.Color = Color.Red;
                        card.Url = "";
                        await m.DeleteAsync();
                        await CreateModCard(card);
                        return;
                    }
                }
            }

            // Softer, don't auto-delete
            var filter = new ProfanityFilter.ProfanityFilter();
            filter.AddProfanity(SoftNo);
            var list = filter.DetectAllProfanities(content);
            if (list.Count > 0)
            {
                card.AddField("Reason", $"`{string.Join("`, `", list)}`", true);
                await CreateModCard(card);
                return;
            }

            if (m.Content.Length > 1000)
            {
                card.AddField("Reason", "Message length > 1000", true);
                await CreateModCard(card);
                return;
            }
        }

        private static async Task Client_MessagesBulkDeleted(IReadOnlyCollection<Cacheable<IMessage, ulong>> arg1, ISocketMessageChannel arg2)
        {
            var e = new EmbedBuilder()
                .WithTitle("Bulk Delete Detected")
                .WithCurrentTimestamp()
                .AddField("Count", arg1.Count, true)
                .AddField("Channel", $"<#{arg2.Id}>", true);

            await ModLogs.SendMessageAsync(embed: e.Build());
        }

        private static async Task Client_ReactionAdded(Cacheable<IUserMessage, ulong> cMsg, ISocketMessageChannel ch, SocketReaction r)
        {
            if (r.User.Value.IsBot || ch.Id != ModLogs.Id) { return; } // Ignore bot reactions, make sure reaction was in mod-logs
            var msg = await ch.GetMessageAsync(cMsg.Id) as IUserMessage;
            if (msg.Embeds.Count == 0) { return; } // Make sure the message has an embed
            var embed = msg.Embeds.First();
            if (embed.Fields.Any(f => f.Name == "Resolved")) { return; } // Make sure it's not already resolved

            var embedUserIDField = embed.Fields.First(f => f.Name == "User ID").Value;
            var usrId = embedUserIDField == "Bot or Webhook" ? ulong.MaxValue : ulong.Parse(embedUserIDField);
            var usr = usrId == ulong.MaxValue ? null : (ch as SocketGuildChannel).Guild.GetUser(usrId);

            async Task Resolve(string text)
            {
                await msg.ModifyAsync(m =>
                {
                    var e = msg.Embeds.First().ToEmbedBuilder()
                        .AddField("Resolved", $"{text} by {r.User.Value.Mention} at {DateTime.UtcNow}", false)
                        .WithColor(Color.Green);
                    m.Embed = new Optional<Embed>(e.Build());
                });
            }

            var url = embed.Fields.First(f => f.Name == "Jump To Post").Value.Replace(")", "");
            var (channel, message) = await url.GetMessageFromLink();

            if (r.Emote.Name == CardReactions[0].Name) // React Info
            {
                if (!(usr is null))
                {
                    await User.PostUserInfo(usr, ch as SocketTextChannel);
                }
                else
                {
                    await ch.SendMessageAsync("User is bot or webhook.");
                }
            }
            else if (r.Emote.Name == CardReactions[1].Name) // React OK
            {
                await Resolve("Marked as OK");
            }
            else if (r.Emote.Name == CardReactions[2].Name) // React Warn & Delete
            {
                if (!(message is null))
                {
                    await message.DeleteAsync();
                    await Resolve("Marked as *Warn & Delete*");
                    await usr?.SendMessageAsync($"{r.User.Value.Mention} has given you a warning for posting inappropriate language, " +
                        $"links, or other material.\n```{message.Content}\n``` ({message.Attachments.Count} attachment(s))");
                }
                else // Message was already auto-deleted, so content is unavailable
                {
                    await Resolve("Marked as *Warn* by " + r.User.Value.Mention);
                    await usr?.SendMessageAsync($"{r.User.Value.Mention} has given you a warning for posting inappropriate language, " +
                        $"links, or other material.");
                }
            }
            else if (r.Emote.Name == CardReactions[3].Name) // React Mute & Delete
            {
                if (!(usr is null))
                {
                    if (!usr.Roles.Any(r2 => r2.Name == "Muted"))
                    {
                        await usr.RemoveRoleAsync(channel.Guild.Roles.First(r2 => r2.Name == "Member"));
                        await usr.AddRoleAsync(channel.Guild.Roles.First(r2 => r2.Name == "Muted"));
                        await usr.SendMessageAsync($"You have been muted by {r.User.Value.Mention}.");
                        await ModLogs.SendMessageAsync($"{usr.Mention} was muted by {r.User.Value.Mention}.");
                    }
                }

                if (!(message is null))
                {
                    await message.DeleteAsync();
                    await Resolve("Marked as *Mute & Delete* by " + r.User.Value.Mention);
                }
                else
                {
                    await Resolve("Marked as *Mute* by " + r.User.Value.Mention);
                }
            }
        }

        private static async Task CreateModCard(EmbedBuilder e)
        {
            var eM = await ModLogs.SendMessageAsync(embed: e.Build());
            _ = Task.Run(async () =>
            {
                await eM.AddReactionsAsync(CardReactions);
            });
        }

        [Command("kick")]
        [RequireRole("staff")]
        [RequireProduction]
        [Syntax("kick <user>")]
        public async Task Kick(SocketGuildUser user, [Remainder]string reason = "violating the rules")
        {
            if (user.Id == Context.Message.Author.Id || user.IsBot) { await this.ReactError(); return; }
            reason = reason.EndsWith(".") ? reason : reason + ".";
            await user.KickAsync();
            await ModLogs.SendMessageAsync($"{user.Mention} was kicked by {Context.User}.");
            await Context.Message.DeleteAsync();
            await user.SendMessageAsync($"You have been kicked from {Context.Guild.Name} for {reason}.");
        }

        [Command("ban")]
        [RequireRole("staff")]
        [RequireProduction]
        [Syntax("ban <user>")]
        public async Task Ban(SocketGuildUser user, [Remainder]string reason = "violating the rules")
        {
            if (user.Id == Context.Message.Author.Id || user.IsBot) { await this.ReactError(); return; }
            reason = reason.EndsWith(".") ? reason : reason + ".";
            await user.BanAsync(0, reason);
            await Context.Message.DeleteAsync();
            await user.SendMessageAsync($"You have been banned from {Context.Guild.Name} for {reason}");
        }

        [Command("unban")]
        [Alias("pardon")]
        [RequireRole("staff")]
        [RequireProduction]
        [Syntax("unban <user>")]
        public async Task Unban(ulong user)
        {
            if (user == Context.Message.Author.Id) { await this.ReactError(); return; }
            await Context.Guild.RemoveBanAsync(user);
            await ModLogs.SendMessageAsync($"{user} unbanned by {Context.User.Mention}.");
            await Context.Message.DeleteAsync();
        }

        [Command("mute")]
        [RequireRole("staff")]
        [RequireProduction]
        [Syntax("mute <user>")]
        public async Task Mute(SocketGuildUser user)
        {
            if (user.Id == Context.Message.Author.Id || user.IsBot) { await this.ReactError(); return; }
            if (!user.Roles.Any(r => r.Name == "Muted"))
            {
                await user.RemoveRoleAsync(Context.Guild.Roles.First(r => r.Name == "Member"));
                await user.AddRoleAsync(Context.Guild.Roles.First(r => r.Name == "Muted"));
                await ModLogs.SendMessageAsync($"{user.Mention} was muted by {Context.User.Mention}.");
                await Context.Message.DeleteAsync();
                await user.SendMessageAsync($"You have been muted by {Context.User.Mention}.");
            }
        }

        [Command("unmute")]
        [RequireRole("staff")]
        [RequireProduction]
        [Syntax("unmute <user>")]
        public async Task Unmute(SocketGuildUser user)
        {
            if (user.Id == Context.Message.Author.Id || user.IsBot) { await this.ReactError(); return; }
            if (user.Roles.Any(r => r.Name == "Muted"))
            {
                await user.AddRoleAsync(Context.Guild.Roles.First(r => r.Name == "Member"));
                await user.RemoveRoleAsync(Context.Guild.Roles.First(r => r.Name == "Muted"));
                await ModLogs.SendMessageAsync($"{user.Mention} was unmuted by {Context.User.Mention}.");
                await Context.Message.DeleteAsync();
                await user.SendMessageAsync($"You have been unmuted by {Context.User.Mention}.");
            }
        }

        [Command("purge")]
        [RequireRole("staff")]
        [RequireProduction]
        [Summary("Purges messages of specified amount in the current channel.")]
        [Syntax("purge <count>")]
        public async Task Purge(int count)
        {
            var messages = await Context.Channel.GetMessagesAsync(count + 1).FlattenAsync();
            await ((ITextChannel)Context.Channel).DeleteMessagesAsync(messages);
            await ModLogs.SendMessageAsync($"{count} messages purged by {Context.User.Mention} in #{Context.Channel.Name}.");
        }
    }
}
