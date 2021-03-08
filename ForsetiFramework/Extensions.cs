using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Input;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using ForsetiFramework.Modules;

using MySql.Data.MySqlClient;

namespace ForsetiFramework
{
    public static class Extensions
    {
        public static async Task ReactOk(this ICommandContext c) =>
            await c.Message.AddReactionAsync(new Emoji("👌"));

        public static async Task ReactError(this ICommandContext c) =>
            await c.Message.AddReactionAsync(new Emoji("❌"));

        public static async Task ReactQuestion(this ICommandContext c) =>
            await c.Message.AddReactionAsync(new Emoji("❓"));

        public static (string left, string right) SplitAt(this string s, int index) =>
            index == -1 ? (s, "") : (s.Substring(0, index), s.Substring(index, s.Length - index));

        public static int NonQuery(this string s, params object[] param)
        {
            try
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
            catch
            {
                Database.ForceReconnect();
                return NonQuery(s, param);
            }
        }

        public static MySqlDataReader Query(this string s, params object[] param)
        {
            try
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
            catch
            {
                Database.ForceReconnect();
                return Query(s, param);
            }
        }

        public static async Task<List<ModuleInfo>> GetModules(this SocketCommandContext c)
        {
            var modules = BotManager.Commands.Modules.ToList();

            var modulesToRemove = BotManager.Commands.Modules.ToList();
            modulesToRemove.RemoveAll(m =>
            {
                return !(m.IsAvailable(modules, c).GetAwaiter().GetResult());
            });

            return modulesToRemove;
        }

        public static async Task<bool> IsAvailable(this ModuleInfo m, List<ModuleInfo> modules, SocketCommandContext c)
        {
            foreach (var cmd in m.Commands)
            {
                if (cmd.CheckPreconditionsAsync(c).GetAwaiter().GetResult().IsSuccess)
                {
                    return true;
                }
            }

            foreach (var child in modules.Where(m2 => m2.Parent == m))
            {
                if (await IsAvailable(child, modules, c)) { return true; }
            }

            return false;
        }

        public static async Task<List<string>> SplitWithLength(this string s, int length)
        {
            var toReturn = new List<string>();

            while (s.Length > length)
            {
                var index = s.Substring(0, length).LastIndexOf("\n");
                if (index == -1)
                {
                    s = s.Insert(length - 2, "\n");
                    continue;
                }
                else if (index == 0)
                {
                    s = s.Insert(length - 2, "\n");
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

        public static string GetCommand(this IUserMessage msg)
        {
            var argPos = 0;
            var hasPrefix = msg.HasStringPrefix(Config.Prefix, ref argPos) || msg.HasMentionPrefix(BotManager.Client.CurrentUser, ref argPos);
            if (!(hasPrefix) || msg.Author.IsBot) { return null; }

            (_, var Remainder) = msg.Content.SplitAt(argPos);
            return Remainder.Split(' ')[0];
        }
    }
}
