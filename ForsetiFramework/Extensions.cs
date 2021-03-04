using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Discord;
using Discord.Commands;
using Discord.WebSocket;
using ForsetiFramework.Modules;

using MySql.Data.MySqlClient;

namespace ForsetiFramework
{
    public static class Extensions
    {
        public static async Task ReactOk(this ModuleBase<SocketCommandContext> c) =>
            await c.Context.Message.AddReactionAsync(new Emoji("👌"));

        public static async Task ReactError(this ModuleBase<SocketCommandContext> c) =>
            await c.Context.Message.AddReactionAsync(new Emoji("❌"));

        public static (string left, string right) SplitAt(this string s, int index) =>
            (s.Substring(0, index), s.Substring(index, s.Length - index));

        public static int NonQuery(this string s, params object[] param)
        {
            var cmd = new MySqlCommand(s, Database.Connection);
            var i = 0;
            foreach (var p in param)
            {
                cmd.Parameters.AddWithValue($"@p{i}", p);
                i++;
            }

            return cmd.ExecuteNonQuery();
        }

        public static MySqlDataReader Query(this string s, params object[] param)
        {
            var cmd = new MySqlCommand(s, Database.Connection);
            var i = 0;
            foreach (var p in param)
            {
                cmd.Parameters.AddWithValue($"@p{i}", p);
                i++;
            }

            return cmd.ExecuteReader();
        }

        public static async Task<List<ModuleInfo>> GetModules(this SocketCommandContext c)
        {
            var modules = BotManager.Commands.Modules.ToList();
            modules.RemoveAll(m =>
            {
                foreach (var cmd in m.Commands)
                {
                    if (cmd.CheckPreconditionsAsync(c).GetAwaiter().GetResult().IsSuccess)
                    {
                        return false;
                    }
                }
                return true;
            });

            return modules;
        }

        public static async Task<List<string>> SplitWithLength(this string s, int length)
        {
            var toReturn = new List<string>();

            while (s.Length > length)
            {
                var index = s.Substring(0, length).LastIndexOf("\n");
                if (index == -1)
                {
                    s = s.Insert(length / 2, "\n");
                    continue;
                }
                toReturn.Add(s.Substring(0, index));
                s = s.Substring(index, s.Length - index);
            }
            toReturn.Add(s);
            return toReturn;
        }

        public static async Task<(SocketGuildChannel channel, IMessage message)> GetMessageFromLink(this string url)
        {
            var parts = url.Split('/');
            var messageId = ulong.Parse(parts[parts.Length - 1]);
            var channelId = ulong.Parse(parts[parts.Length - 2]);

            var channel = BotManager.Client.GetChannel(channelId) as SocketGuildChannel;
            var message = await (channel as ITextChannel).GetMessageAsync(messageId);
            return (channel, message);
        }
    }
}
