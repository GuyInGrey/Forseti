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
        static string[] HardNoWords = File.ReadAllText(Config.Path + "badwords.txt").Replace("\r", "").Split('\n');

        public static SocketTextChannel ModLogs => BotManager.Instance.Client.GetChannel(814327531216961616) as SocketTextChannel;
        public static SocketTextChannel General => BotManager.Instance.Client.GetChannel(814328175881355304) as SocketTextChannel;
        // User Info, This Is Fine, Warn & Delete, Mute & Delete
        static Emoji[] CardReactions = new[] { "👤", "✅", "⚠", "🔇" }.Select(s => new Emoji(s)).ToArray();

        static Moderation()
        {
            if (!Config.Debug)
            {
                BotManager.Instance.Client.MessageReceived += async (msg) => { if (msg is SocketUserMessage msg2) { await CheckMessage(msg2); } };
                BotManager.Instance.Client.MessageUpdated += async (a, msg, c) => { if (msg is SocketUserMessage msg2) { await CheckMessage(msg2); } };
                BotManager.Instance.Client.UserBanned += async (usr, guild) => await ModLogs.SendMessageAsync($"{usr.Mention} ({usr.Id}) has been banned.");
                BotManager.Instance.Client.UserUnbanned += async (usr, guild) => await ModLogs.SendMessageAsync($"{usr.Mention} ({usr.Id}) has been unbanned.");
                BotManager.Instance.Client.UserLeft += async (usr) => await ModLogs.SendMessageAsync($"{usr.Mention} has left or was kicked.");

                BotManager.Instance.Client.ReactionAdded += Client_ReactionAdded;
                BotManager.Instance.Client.MessagesBulkDeleted += Client_MessagesBulkDeleted;
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
            if (r.User.Value.IsBot) { return; } // Ignore bot reactions
            if (ch.Id != ModLogs.Id) { return; } // Make sure reaction was in mod-logs
            var msg = await ch.GetMessageAsync(cMsg.Id) as IUserMessage;
            if (msg.Embeds.Count <= 0) { return; } // Make sure the message has an embed
            var embed = msg.Embeds.First();
            if (embed.Color == Color.Green || embed.Fields.Any(f => f.Name == "Resolved")) { return; } // Make sure it's not already resolved

            var usrId = ulong.Parse(embed.Fields.First(f => f.Name == "User ID").Value);
            var usr = (ch as SocketGuildChannel).Guild.GetUser(usrId);

            async Task Resolve(string type)
            {
                await msg.ModifyAsync(m =>
                {
                    var e = msg.Embeds.First().ToEmbedBuilder()
                        .AddField("Resolved", type + " at " + DateTime.UtcNow, false)
                        .WithColor(Color.Green);
                    m.Embed = new Optional<Embed>(e.Build());
                });
            }

            async Task<(SocketGuildChannel channel, IMessage message)> GetInfo(string url)
            {
                var parts = url.Replace(")", "").Split('/');
                Console.WriteLine(string.Join(" | ", parts));
                var messageId = ulong.Parse(parts[parts.Length - 1]);
                var channelId = ulong.Parse(parts[parts.Length - 2]);

                var channel = BotManager.Instance.Client.GetChannel(channelId) as SocketGuildChannel;
                var message = await (channel as ITextChannel).GetMessageAsync(messageId);
                return (channel, message);
            }

            if (r.Emote.Name == CardReactions[0].Name)
            {
                await User.PostUserInfo(usr, ch as SocketTextChannel);
            }
            else if (r.Emote.Name == CardReactions[1].Name)
            {
                await Resolve("Marked as OK by " + r.User.Value.Mention);
            }
            else if (r.Emote.Name == CardReactions[2].Name)
            {
                var url = embed.Fields.First(f => f.Name == "Jump To Post").Value;
                var (channel, message) = await GetInfo(url);

                if (!(message is null))
                {
                    await message.DeleteAsync();
                    await Resolve("Marked as *Warn & Delete* by " + r.User.Value.Mention);
                    await usr.SendMessageAsync($"{r.User.Value.Mention} has given you a warning for posting inappropriate language, " +
                        $"links, or other material.\n```{message.Content}\n``` ({message.Attachments.Count} attachment(s))");
                }
                else // Message was already auto-deleted, so content is unavailable
                {
                    await Resolve("Marked as *Warn* by " + r.User.Value.Mention);
                    await usr.SendMessageAsync($"{r.User.Value.Mention} has given you a warning for posting inappropriate language, " +
                        $"links, or other material.");
                }
            }
            else if (r.Emote.Name == CardReactions[3].Name)
            {
                var url = embed.Fields.First(f => f.Name == "Jump To Post").Value;
                var (channel, message) = await GetInfo(url);

                if (!usr.Roles.Any(r2 => r2.Name == "Muted"))
                {
                    await usr.RemoveRoleAsync(channel.Guild.Roles.First(r2 => r2.Name == "Member"));
                    await usr.AddRoleAsync(channel.Guild.Roles.First(r2 => r2.Name == "Muted"));
                    await usr.SendMessageAsync($"You have been muted by {r.User.Value.Mention}.");
                    await ModLogs.SendMessageAsync($"{usr.Mention} was muted by {r.User.Value.Mention}.");
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

        public static async Task CheckMessage(SocketUserMessage m)
        {
            if (m.Author.IsBot || m.Content is null) { return; }

            var guild = (m.Channel as SocketGuildChannel).Guild;

            var e = new EmbedBuilder()
                .WithTitle("Forseti Entry")
                .WithDescription(m.Content)
                .WithAuthor(m.Author)
                .WithColor(Color.Orange)
                .WithCurrentTimestamp()
                .AddField("Channel", $"<#{m.Channel.Id}>", true)
                .AddField("Jump To Post", $@"[Link](https://discord.com/channels/{guild.Id}/{m.Channel.Id}/{m.Id})", true)
                .AddField("User ID", m.Author.Id, true);

            // Strict, auto-delete
            var clearedContent = Regex.Replace(m.Content.ToLower(), "[^a-z1-9 -_]", string.Empty);
            var clearedParts = clearedContent.Split(new[] { " ", "-", "_" }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var b2 in clearedParts)
            {
                foreach (var b in HardNoWords)
                {
                    if (b2.Equals(b))
                    {
                        e.AddField("Flagged", b, true);
                        e.AddField("Auto-Deleted", "Yes", true);
                        e.Color = Color.Red;
                        e.Url = "";
                        await m.DeleteAsync();
                        await CreateModCard(e);
                        return;
                    }
                }
            }

            // Softer, don't auto-delete
            var filter = new ProfanityFilter.ProfanityFilter();
            var list = filter.DetectAllProfanities(m.Content);
            if (list.Count > 0)
            {
                e.AddField("Flagged", string.Join(", ", list), true);
                e.AddField("Auto-Deleted", "No", true);
                await CreateModCard(e);
                return;
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
