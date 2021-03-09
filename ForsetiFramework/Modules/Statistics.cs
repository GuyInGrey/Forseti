using System;
using System.Threading.Tasks;
using Discord.Commands;

namespace ForsetiFramework.Modules
{
    public class Statistics : ModuleBase<SocketCommandContext>
    {
        [Clockwork(60 * 60 * 1000)]
        public static async Task Hourly()
        {
            Console.WriteLine(DateTime.Now + " : Test");
        }
    }
}
