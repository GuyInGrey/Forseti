using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using Discord;
using Discord.WebSocket;
using ProfanityFilter;

namespace ForsetiFramework.Modules
{
    public class ModerationEvents
    {
        static string[] HardNoWords => File.ReadAllText(Config.Path + "badwords.txt").ToLower().Replace("\r", "")
            .Split('\n').Select(s => s.Trim()).Where(s => s != "").ToArray();
        static string[] SoftNo => File.ReadAllText(Config.Path + "softwords.txt").ToLower().Replace("\r", "")
            .Split('\n').Select(s => s.Trim()).Where(s => s != "").ToArray();

        // User Info, This Is Fine, Warn & Delete, Mute & Delete
        static Emoji[] CardReactions = new[] { "👤", "✅", "⚠", "🔇" }.Select(s => new Emoji(s)).ToArray();

        [Event(Events.MessageReceived), RequireProduction]
        public static async Task MessageReceived(SocketMessage msg) { if (msg is SocketUserMessage msg2) { await CheckMessage(msg2); } }

        [Event(Events.MessageUpdated), RequireProduction]
        public static async Task MessageUpdated(Cacheable<IMessage, ulong> a, SocketMessage msg, ISocketMessageChannel c)
        { if (msg is SocketUserMessage msg2) { await CheckMessage(msg2); } }

        [Event(Events.UserLeft), RequireProduction]
        public static async Task UserLeft(SocketGuildUser usr) =>
            await Moderation.ModLogs.SendMessageAsync($"{usr.Mention} has left or was kicked.");

        [Event(Events.UserBanned), RequireProduction]
        public static async Task UserBanned(SocketUser usr, SocketGuild guild) =>
            await Moderation.ModLogs.SendMessageAsync($"{usr.Mention} ({usr.Id}) has been banned.");

        [Event(Events.UserUnbanned), RequireProduction]
        public static async Task UserUnbanned(SocketUser usr, SocketGuild guild) =>
            await Moderation.ModLogs.SendMessageAsync($"{usr.Mention} ({usr.Id}) has been unbanned.");

        [Event(Events.MessagesBulkDeleted), RequireProduction]
        public static async Task MessagesBulkDeleted(IReadOnlyCollection<Cacheable<IMessage, ulong>> arg1, ISocketMessageChannel arg2)
        {
            var e = new EmbedBuilder()
                .WithTitle("Bulk Delete Detected")
                .WithCurrentTimestamp()
                .AddField("Count", arg1.Count, true)
                .AddField("Channel", $"<#{arg2.Id}>", true);

            await Moderation.ModLogs.SendMessageAsync(embed: e.Build());
        }

        [Event(Events.UserUpdated), RequireProduction]
        public static async Task UserUpdated(SocketUser arg1, SocketUser arg2)
        {
            if (arg1.NameDesc() != arg2.NameDesc())
            {
                await Moderation.ModLogs.SendMessageAsync($"Username changed for {arg1.NameDesc()} -> {arg2.NameDesc()} ({arg2.Id})");
            }
            if (arg1.AvatarId != arg2.AvatarId)
            {
                var url = arg2.GetAvatarUrl();
                url = url == "" ? "`None`" : $"\n{url}";
                await Moderation.ModLogs.SendMessageAsync($"Avatar changed for {arg2.NameDesc()}: ({arg2.Id}) {url}");
            }
        }

        [Event(Events.GuildMemberUpdated), RequireProduction]
        public static async Task GuildMemberUpdated(SocketGuildUser arg1, SocketGuildUser arg2)
        {
            if (arg1.Nickname != arg2.Nickname)
            {
                var nick1 = arg1.Nickname == "" ? "`None`" : arg1.Nickname;
                var nick2 = arg2.Nickname == "" ? "`None`" : arg2.Nickname;
                await Moderation.ModLogs.SendMessageAsync($"Nickname changed for {arg2.NameDesc()}: {nick1} -> {nick2} ({arg2.Id})");
            }
        }

        [Event(Events.ReactionAdded), RequireProduction]
        public static async Task ReactionAdded(Cacheable<IUserMessage, ulong> cMsg, ISocketMessageChannel ch, SocketReaction r)
        {
            if (r.User.Value.IsBot || ch.Id != Moderation.ModLogs.Id) { return; } // Ignore bot reactions, make sure reaction was in mod-logs
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
                        await Moderation.ModLogs.SendMessageAsync($"{usr.Mention} was muted by {r.User.Value.Mention}.");
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

        public static async Task CheckMessage(SocketUserMessage m)
        {
            if (m.Author.IsBot || m.Content is null) { return; }
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
            var filter = NewFilter();
            var list = filter.DetectAllProfanities(content);
            if (list.Count > 0)
            {
                card.AddField("Reason", $"`{string.Join("`, `", list)}`", true);
                await CreateModCard(card);
                return;
            }

            if (m.Content.Length > 1900)
            {
                card.AddField("Reason", "Message length > 1900", true);
                await CreateModCard(card);
                return;
            }
        }

        private static async Task CreateModCard(EmbedBuilder e)
        {
            var eM = await Moderation.ModLogs.SendMessageAsync(embed: e.Build());
            _ = Task.Run(async () =>
            {
                await eM.AddReactionsAsync(CardReactions);
            });
        }

        public static ProfanityFilter.ProfanityFilter NewFilter()
        {
            var filter = new ProfanityFilter.ProfanityFilter();
            filter.AddProfanity(SoftNo);
            return filter;
        }
    }
}
