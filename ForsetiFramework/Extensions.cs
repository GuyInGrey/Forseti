using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading.Tasks;

using Discord;
using Discord.Commands;
using Discord.WebSocket;

using ForsetiFramework.Modules;

using MySql.Data.MySqlClient;
using Newtonsoft.Json.Linq;
using Processing;

namespace ForsetiFramework
{
    public static class Extensions
    {
        public static Random Random = new Random(new Random().Next(0, 1000));

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
            if (s is null || s.Trim() == "") { return -1; }
            if (Database.State == System.Data.ConnectionState.Closed || Database.State == System.Data.ConnectionState.Broken)
            { Database.ForceReconnect(); }

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
            catch { return -3; }
        }

        public static MySqlDataReader Query(this string s, params object[] param)
        {
            if (s is null || s.Trim() == "") { return null; }
            if (Database.State == System.Data.ConnectionState.Closed || Database.State == System.Data.ConnectionState.Broken) 
            { Database.ForceReconnect(); }

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
            } catch { return null; }
        }

        public static List<ModuleInfo> GetModules(this SocketCommandContext c)
        {
            var modules = CommandManager.Commands.Modules.ToList();

            var modulesToRemove = CommandManager.Commands.Modules.ToList();
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

        public static List<string> SplitWithLength(this string s, int length)
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

        public static string NameDesc(this IUser usr) => usr.Username + "#" + usr.Discriminator;

        public static bool HasAttribute<T>(this MemberInfo e) where T : Attribute =>
            !(e.GetCustomAttribute<T>() is null);

        public static bool HasAttribute<T>(this MemberInfo e, out T att) where T : Attribute
        {
            att = e.GetCustomAttribute<T>();
            return !(att is null);
        }

        public static string GetCommandString(this CommandInfo c)
        {
            var s = c.Name.ToLower();
            var mod = c.Module;
            while (!(mod is null))
            {
                if (!(mod.Group?.ToLower() is null || mod.Group?.ToLower() == ""))
                {
                    s = mod.Group.ToLower() + " " + s;
                }
                mod = mod.Parent;
            }
            return s;
        }

        public static string DownloadString(this string url)
        {
            using (var client = new WebClient())
            {
                return client.DownloadString(url.Trim());
            }
        }

        public static dynamic DownloadJson(this string url)
        {
            using (var client = new WebClient())
            {
                return JObject.Parse(client.DownloadString(url.Trim()));
            }
        }

        public static Sprite DownloadImage(this string url)
        {
            using (var client = new WebClient())
            {
                client.DownloadFile(url, "temp.png");
                var s = Sprite.FromFilePath("temp.png");
                File.Delete("temp.png");
                return s;
            }
        }
        
        public static async Task Send(this Sprite s, SocketCommandContext c)
        {
            s.Save("temp.png");
            await c.Channel.SendFileAsync("temp.png");
            File.Delete("temp.png");
        }

        public static void ForeachPixel(this Sprite s, Action<int, int> toRun)
        {
            for (var y = 0; y < s.Height; y++)
            {
                for (var x = 0; x < s.Width; x++)
                {
                    toRun?.Invoke(x, y);
                }
            }
        }

        public static string Replace(this string str, int pos, char replacement)
        {
            str = str.Substring(0, pos) + replacement + str.Substring(pos + 1);
            return str;
        }

        public static Sprite ForeachPixel(this Sprite s, Func<int, int, Paint, Paint> toRun)
        {
            for (var y= 0; y < s.Height; y++)
            {
                for (var x = 0; x < s.Width; x++)
                {
                    s.Art.Set(x, y, toRun.Invoke(x, y, s.Art.GetPixel(x, y)));
                }
            }

            return s;
        }

        public static Sprite GetAvatarSprite(this IUser user, ushort size)
        {
            var av = user.GetAvatarUrl(ImageFormat.Png, size);
            return av is null ? user.GetDefaultAvatarUrl().DownloadImage() : av.DownloadImage();
        }

        public static string Shuffle(this String str)
        {
            var list = new SortedList<int, char>();
            foreach (var c in str)
            {
                list.Add(Random.Next(), c);
            }
            return new string(list.Values.ToArray());
        }

        public static async Task<IEnumerable<IMessage>> DownloadAllMessages(this IMessageChannel ch)
        {
            var m = await ch.GetMessagesAsync(100).FlattenAsync();
            while (m.Count() > 0)
            {
                Console.WriteLine($"Downloading messages: {ch.Name} - {m.Count()}");
                var m2 = await ch.GetMessagesAsync(m.Last(), Direction.Before, 100).FlattenAsync();
                if (m2.Count() == 0) { break; }
                m = m.Concat(m2);
            }
            return m.Reverse();
        }

        public static async Task SendTextFile(this IMessageChannel ch, string text, string name, string content = null)
        {
            File.WriteAllText(name, text);
            await ch.SendFileAsync(name, content);
            File.Delete(name);
        }

        public static async Task<string> Format(this IEnumerable<IMessage> messages)
        {
            var toReturn = "";
            foreach (var m in messages)
            {
                toReturn += $"[{m.Author}][{m.Timestamp}]{(m.Embeds.Count > 0 ? "[Contains Embeds]" : "")} > {m.Content}\n\n";
            }
            return toReturn;
        }
    }
}
