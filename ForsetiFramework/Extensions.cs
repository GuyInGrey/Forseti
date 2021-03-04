using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using ForsetiFramework.Modules;
using MySql.Data.MySqlClient;

namespace ForsetiFramework
{
    public static class Extensions
    {
        public static async Task ReactOk(this ModuleBase<SocketCommandContext> c)
        {
            await c.Context.Message.AddReactionAsync(new Emoji("👌"));
        }

        public static async Task ReactError(this ModuleBase<SocketCommandContext> c)
        {
            await c.Context.Message.AddReactionAsync(new Emoji("❌"));
        }

        public static (string Left, string Right) SplitAt(this string s, int index)
        {
            var left = s.Substring(0, index);
            var right = s.Substring(index, s.Length - index);
            return (left, right);
        }

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
            var modules = BotManager.Instance.Commands.Modules.ToList();
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
    }
}
