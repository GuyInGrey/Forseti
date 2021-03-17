using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

using Discord;
using Discord.Commands;

namespace ForsetiFramework.Modules
{
    [Group("Statistics"), Alias("stats")]
    public class Statistics : ModuleBase<SocketCommandContext>
    {
        public static PerformanceCounter NetworkTraffic;
        public static float PulsedBytes;

        //[Clockwork(5000)]
        //public static void H1()
        //{
        //    //Console.WriteLine(DateTime.Now + " : Test");
        //    PulsedBytes += NetworkTraffic.NextValue();
        //     += val;
        //    Moderation.ModLogs.SendMessageAsync(val + "\n" + PulsedBytes);
        //}

        //[Clockwork(5000)]
        //public static void H2()
        //{
        //    //Console.WriteLine(DateTime.Now + " : Test");
        //    var val = NetworkTraffic.NextValue();
        //    PulsedBytes += val;
        //    Moderation.ModLogs.SendMessageAsync(val + "\n" + PulsedBytes);
        //}

        [Command("pulse"), RequireRole("staff")]
        public async Task Pulse()
        {
            start:;

            var q = "SELECT COUNT(*) FROM `forseti`.`commandhistory` WHERE time > @p0;"
                .Query(DateTime.Now.Subtract(new TimeSpan(24, 0, 0)));
            if (q is null) { goto start; }
            q.Read();
            var lastDayCount = int.Parse(q["COUNT(*)"].ToString());
            q.Dispose();
            q = "SELECT COUNT(*) FROM `forseti`.`commandhistory` WHERE time > @p0;"
                .Query(Process.GetCurrentProcess().StartTime);
            if (q is null) { goto start; }
            q.Read();
            var botStartCount = int.Parse(q["COUNT(*)"].ToString());
            q.Dispose();

            var processUptime = DateTime.Now - Process.GetCurrentProcess().StartTime;
            var uptime = $"{processUptime.Days} days, {processUptime.Hours} hours, {processUptime.Minutes} minutes";
            var serverCount = BotManager.Client.Guilds.Count;
            var channelCount = BotManager.Client.Guilds.SelectMany(g => g.Channels).Count();
            var userCount = BotManager.Client.Guilds.Select(g => g.MemberCount).Sum();
            var memoryMB = Process.GetCurrentProcess().WorkingSet64 / 1000000;

            var e = new EmbedBuilder()
                .AddField("Process Uptime", uptime, true)
                .AddField("Reach", $"{serverCount} server(s)\n{channelCount} channels\n{userCount} users", true)
                .AddField("Command Usage", $"Last Day: {lastDayCount}\nSince Bot Start: {botStartCount}", true)
                .AddField("Memory (MB)", memoryMB, true)
                .WithCurrentTimestamp()
                .WithThumbnailUrl(BotManager.Client.CurrentUser.GetAvatarUrl())
                .WithColor(Color.Green);

            await ReplyAsync(embed: e.Build());
        }

        [Command("commands"), Alias("cmds"), RequireRole("staff")]
        public async Task Commands()
        {
            var counts = new Dictionary<string, int>();
            var q = "SELECT * FROM `forseti`.`commandhistory` WHERE debug=0;".Query();
            if (q is null) { return; }
            while (q.Read())
            {
                var name = q["commandName"].ToString();
                if (!counts.ContainsKey(name)) { counts.Add(name, 0); }
                counts[name]++;
            }
            q.Dispose();
            var total = counts.Select(c => c.Value).Sum();

            var ordered = counts.Select(s => (s.Key, (s.Value / (double)total) * 100)).OrderBy(c => c.Item2).Reverse();
            var lines = ordered.Select(o => $"{o.Item2:0.00} - `{o.Key}`");
            var desc = string.Join("\n", lines);

            var e = new EmbedBuilder()
                .WithCurrentTimestamp()
                .WithColor(Color.Green)
                .WithTitle("Command Statistics")
                .WithThumbnailUrl(BotManager.Client.CurrentUser.GetAvatarUrl())
                .WithDescription(desc)
                .WithFooter("Total Commands Ran: " + total);

            await ReplyAsync(embed: e.Build());
        }

        [Event(Events.Ready)]
        public static void Init()
        {
            @"CREATE TABLE IF NOT EXISTS `forseti`.`commandhistory` (
  `time` DATETIME NULL,
  `commandName` VARCHAR(150) NULL,
  `success` TINYINT NULL,
  `channel` BIGINT(18) NULL,
  `error` VARCHAR(150) NULL);".NonQuery();

            NetworkTraffic = new PerformanceCounter("Network Adapter", "Bytes Total/sec", "Intel[R] Ethernet Connection [2] I219-V");
        }

        [Event(Events.CommandExecuted), RequireRole("staff")]
        public static void CommandExecuted(Optional<CommandInfo> cmd, ICommandContext context, IResult r)
        {
            if (!cmd.IsSpecified) { return; }
            "INSERT INTO `forseti`.`commandhistory` (time, commandName, success, channel, error, debug) values (@p0, @p1, @p2, @p3, @p4, @p5);"
                .NonQuery(DateTime.Now, cmd.Value.GetCommandString(), r.IsSuccess, context.Channel.Id, r.Error.ToString(), Config.Debug);
        }
    }
}
