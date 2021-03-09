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
        public static SocketTextChannel ModLogs => BotManager.Client.GetChannel(814327531216961616) as SocketTextChannel;

        [Command("kick")]
        [RequireRole("staff")]
        [RequireProduction]
        [Syntax("kick <user>")]
        public async Task Kick(SocketGuildUser user, [Remainder]string reason = "violating the rules")
        {
            if (user.Id == Context.Message.Author.Id || user.IsBot) { await Context.ReactError(); return; }
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
            if (user.Id == Context.Message.Author.Id || user.IsBot) { await Context.ReactError(); return; }
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
            if (user == Context.Message.Author.Id) { await Context.ReactError(); return; }
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
            if (user.Id == Context.Message.Author.Id || user.IsBot) { await Context.ReactError(); return; }
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
            if (user.Id == Context.Message.Author.Id || user.IsBot) { await Context.ReactError(); return; }
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
        [Alias("clean")]
        [RequireRole("staff")]
        [RequireProduction]
        [Summary("Purges messages of specified amount in the current channel.")]
        [Syntax("purge <count>")]
        public async Task Purge(int count)
        {
            if (Context.Channel.Id == ModLogs.Id) { await Context.ReactError(); return; }

            var messages = await Context.Channel.GetMessagesAsync(count + 1).FlattenAsync();
            await ((ITextChannel)Context.Channel).DeleteMessagesAsync(messages);
            await ModLogs.SendMessageAsync($"{count} messages purged by {Context.User.Mention} in #{Context.Channel.Name}.");
        }
    }
}
