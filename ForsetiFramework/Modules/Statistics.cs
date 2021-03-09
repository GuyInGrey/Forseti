using System;
using System.Threading.Tasks;
using Discord.Commands;

namespace ForsetiFramework.Modules
{
    public class Statistics : ModuleBase<SocketCommandContext>
    {
        [Clockwork(1)]
        public static async Task Hourly()
        {
            "".NonQuery();
        }
    }
}
