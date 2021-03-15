using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using ForsetiFramework.Constructs;

namespace ForsetiFramework.Modules
{
    [Group("Party")]
    public class Party : ModuleBase<SocketCommandContext>
    {
        public static ulong Category = 820800555982913556;
        public static int MaxPartiesPerPerson = 2;
        public static SocketRole Everyone => BotManager.Client.Guilds.First().GetRole(769057370646511628);

        [Command("create")]
        [Summary("Create a new party and associated channels.")]
        [Syntax("create [name]")]
        public async Task Create([Remainder]string name)
        {
            // Can't be in more than 2 parties at once
            if (PartyInfo.GetAll().Count(p => p.Host == Context.User.Id ||
            p.Guests.Contains(Context.User.Id)) >= MaxPartiesPerPerson ||
            name.Length > 50 ||
            ModerationEvents.NewFilter().DetectAllProfanities(name).Count > 0) 
            {
                await Context.ReactError(); return;
            }
            await Context.ReactOk();

            var party = new PartyInfo
            {
                Host = Context.User.Id,
                Guests = new ulong[0],
            };

            var channel = await Context.Guild.CreateTextChannelAsync(name.ToLower().Replace(" ", "-"), prop =>
            {
                prop.Topic = "Party Chat";
                prop.CategoryId = Category; 
                prop.PermissionOverwrites = new List<Overwrite>()
                {
                    new Overwrite(Everyone.Id, PermissionTarget.Role,
                        new OverwritePermissions(viewChannel: PermValue.Deny)),
                    new Overwrite(Context.User.Id, PermissionTarget.User,
                        new OverwritePermissions(viewChannel: PermValue.Allow)),
                    new Overwrite(BotManager.Client.CurrentUser.Id, PermissionTarget.User,
                        new OverwritePermissions(viewChannel: PermValue.Allow)),
                };
            });

            party.TextChannel = channel.Id;

            var vc = await Context.Guild.CreateVoiceChannelAsync(name, prop =>
            {
                prop.CategoryId = Category;
                prop.PermissionOverwrites = new List<Overwrite>()
                {
                    new Overwrite(Everyone.Id, PermissionTarget.Role, 
                        new OverwritePermissions(viewChannel: PermValue.Deny, connect: PermValue.Deny)),
                    new Overwrite(Context.User.Id, PermissionTarget.User, 
                        new OverwritePermissions(viewChannel: PermValue.Allow, connect: PermValue.Allow)),
                    new Overwrite(BotManager.Client.CurrentUser.Id, PermissionTarget.User, 
                        new OverwritePermissions(viewChannel: PermValue.Allow, connect: PermValue.Allow)),
                };
            });

            party.VoiceChannel = vc.Id;

            var all = PartyInfo.GetAll();
            all.Add(party);
            PartyInfo.SaveParties(all);
            await ReplyAsync($"Created your new party, {Context.User.Mention}! {channel.Mention}");
        }

        [Command("delete")]
        [Summary("Deletes the current party.")]
        public async Task Delete(ulong id = 0)
        {
            var party = PartyInfo.GetAll().FirstOrDefault(p => p.Host == Context.User.Id && p.TextChannel == Context.Channel.Id);
            if (party is null && Context.User.Id == 126481324017057792)
            {
                party = PartyInfo.GetAll().FirstOrDefault(p => p.TextChannel == Context.Channel.Id);
            }

            if (party is null)
            {
                await Context.ReactError();
                return;
            }

            var parties = PartyInfo.GetAll();
            parties.RemoveAll(p => p.TextChannel == party.TextChannel);
            PartyInfo.SaveParties(parties);

            var name = Context.Channel.Name;
            var vc = Context.Guild.GetVoiceChannel(party.VoiceChannel);
            await (Context.Channel as SocketGuildChannel).DeleteAsync();
            await vc.DeleteAsync();
            await Context.User.SendMessageAsync($"Your party, {name}, has been deleted.");
        }

        [Command("addguest")]
        [Summary("Add someone to the party!")]
        [Syntax("addguest <user>")]
        public async Task AddGuest(SocketGuildUser usr)
        {
            var party = PartyInfo.GetAll().FirstOrDefault(p => p.Host == Context.User.Id && p.TextChannel == Context.Channel.Id);
            if (party is null)
            {
                await Context.ReactError();
                return;
            }

            if (party.Guests.Contains(usr.Id))
            {
                await ReplyAsync($"{usr} user is already in the party.");
                return;
            }

            var guests = party.Guests.ToList();
            guests.Add(usr.Id);
            party.Guests = guests.ToArray();

            var vc = Context.Guild.GetVoiceChannel(party.VoiceChannel);
            await vc.AddPermissionOverwriteAsync(usr, 
                new OverwritePermissions(viewChannel: PermValue.Allow, connect: PermValue.Allow));
            await (Context.Channel as SocketGuildChannel).AddPermissionOverwriteAsync(usr, 
                new OverwritePermissions(viewChannel: PermValue.Allow, connect: PermValue.Allow));

            var parties = PartyInfo.GetAll();
            parties.RemoveAll(p => p.TextChannel == party.TextChannel);
            parties.Add(party);
            PartyInfo.SaveParties(parties);

            await Context.ReactOk();
        }

        [Command("removeguest")]
        [Summary("Remove someone from the party.")]
        [Syntax("removeguest <user>")]
        public async Task RemoveGuest(SocketGuildUser usr)
        {
            var party = PartyInfo.GetAll().FirstOrDefault(p => p.Host == Context.User.Id && p.TextChannel == Context.Channel.Id);
            if (party is null)
            {
                await Context.ReactError();
                return;
            }

            if (!party.Guests.Contains(usr.Id))
            {
                await ReplyAsync($"{usr} user is not in the party.");
                return;
            }

            var guests = party.Guests.ToList();
            guests.Remove(usr.Id);
            party.Guests = guests.ToArray();

            var vc = Context.Guild.GetVoiceChannel(party.VoiceChannel);
            await vc.AddPermissionOverwriteAsync(usr, 
                new OverwritePermissions(viewChannel: PermValue.Deny, connect: PermValue.Deny));
            await (Context.Channel as SocketGuildChannel).AddPermissionOverwriteAsync(usr, 
                new OverwritePermissions(viewChannel: PermValue.Deny, connect: PermValue.Deny));

            var parties = PartyInfo.GetAll();
            parties.RemoveAll(p => p.TextChannel == party.TextChannel);
            parties.Add(party);
            PartyInfo.SaveParties(parties);

            await Context.ReactOk();
        }
    }
}
