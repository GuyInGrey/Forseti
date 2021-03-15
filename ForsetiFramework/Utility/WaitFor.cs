using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Discord.WebSocket;

namespace ForsetiFramework.Utility
{
    public static class WaitFor
    {
        public static List<(ulong, Func<SocketUserMessage, Task<bool>>)> WaitingFor = new List<(ulong, Func<SocketUserMessage, Task<bool>>)>();

        public static void WaitForMessage(this ulong ch, Func<SocketUserMessage, Task<bool>> toRun) =>
            WaitingFor.Add((ch, toRun));

        [Event(Events.MessageReceived)]
        public static async Task OnMessage(SocketMessage arg)
        {
            if (!(arg is SocketUserMessage msg)) { return; }
            for (var i = 0; i < WaitingFor.Count; i++)
            {
                if (WaitingFor[i].Item1 == msg.Channel.Id)
                {
                    if (WaitingFor[i].Item2 is null || await WaitingFor[i].Item2.Invoke(msg))
                    {
                        WaitingFor.RemoveAt(i);
                        i--;
                    }
                }
            }
        }
    }
}
