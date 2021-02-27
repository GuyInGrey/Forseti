﻿using System;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;

namespace Forseti.Modules
{
    public class User : ModuleBase<SocketCommandContext>
    {
        public static async Task PostUserInfo(SocketGuildUser usr, SocketTextChannel channel)
        {
            var roles = usr.Roles.Where(r => !r.Name.Contains("everyone")).ToList();
            roles.Sort((a, b) => a.CompareTo(b));

            var e = new EmbedBuilder()
                .WithTitle(usr.Username + "#" + usr.Discriminator)
                .AddField("ID", usr.Id, true)
                .AddField("Joined", usr.JoinedAt, true)
                .AddField("Account Created", usr.CreatedAt, true)
                .AddField("Roles", string.Join(", ", roles.Select(r => r.Name)), true)
                .WithCurrentTimestamp()
                .WithThumbnailUrl(usr.GetAvatarUrl());
            await channel.SendMessageAsync(embed: e.Build());
        }

        [Command("info")]
        [Summary("Gets info about a given user.")]
        [Syntax("info [user]")]
        public async Task Info(SocketGuildUser usr = null)
        {
            usr = usr is null ? Context.Message.Author as SocketGuildUser : usr;
            await PostUserInfo(usr, Context.Channel as SocketTextChannel);
        }
    }
}
