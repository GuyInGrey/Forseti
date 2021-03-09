﻿using System;
using System.Threading.Tasks;
using Discord.Commands;
using Discord.WebSocket;

namespace ForsetiFramework.Modules
{
    public class Statistics : ModuleBase<SocketCommandContext>
    {
        [Clockwork(60 * 60 * 1000)]
        public static void Hourly()
        {
            Console.WriteLine(DateTime.Now + " : Test");
        }

        [Event(Events.InviteCreated)]
        public static void InviteMade(SocketInvite i)
        {
            Console.WriteLine("Invite: " + i.Url);
        }
    }
}
