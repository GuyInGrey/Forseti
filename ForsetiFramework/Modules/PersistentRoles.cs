using System.Linq;
using System.Threading.Tasks;

using Discord.WebSocket;

namespace ForsetiFramework.Modules
{
    public class PersistentRoles
    {
        public static void Init() { }
        public static SocketTextChannel General => BotManager.Client.GetChannel(814328175881355304) as SocketTextChannel;

        static PersistentRoles()
        {
            @"CREATE TABLE IF NOT EXISTS `forseti`.`persistentroles` (
  `userID` BIGINT(18) NOT NULL,
  `roles` TEXT NULL,
  PRIMARY KEY (`userID`),
  UNIQUE INDEX `userID_UNIQUE` (`userID` ASC) VISIBLE);".NonQuery();

            BotManager.Client.UserJoined += Client_UserJoined;
            BotManager.Client.GuildMemberUpdated += Client_UserUpdated;
        }

        private static async Task Client_UserUpdated(SocketGuildUser arg1, SocketGuildUser arg2)
        {
            var roleString = string.Join(",", arg2.Roles.Where(r => !r.Name.Contains("everyone")).Select(r => r.Id));
            "DELETE FROM `forseti`.`persistentroles` WHERE userID=@p0;".NonQuery(arg2.Id.ToString());
            "INSERT INTO `forseti`.`persistentroles` (userID, roles) VALUES (@p0, @p1);".NonQuery(arg2.Id.ToString(), roleString);
        }

        private static async Task Client_UserJoined(SocketGuildUser arg)
        {
            var guild = BotManager.Client.GetGuild(769057370646511628);

            var q = "SELECT * FROM `forseti`.`persistentroles` WHERE userID=@p0".Query(arg.Id);
            try
            {
                if (q.HasRows)
                {
                    await q.ReadAsync();
                    q["roles"].ToString().Split(',')
                        .Select(r => guild.Roles.FirstOrDefault(r2 => r2.Id == ulong.Parse(r)))
                        .Where(r => !(r is null)).ToList().ForEach(role => arg.AddRoleAsync(role));

                    await General.SendMessageAsync($"Welcome back, {arg.Mention}! Please make sure to read through <#814326414618132521>.");
                    await Moderation.ModLogs.SendMessageAsync("Added old roles from rejoined user " + arg.Username);
                }
                else
                {
                    var memberRole = guild.Roles.FirstOrDefault(r => r.Name == "Member");
                    await arg.AddRoleAsync(memberRole);
                    await General.SendMessageAsync($"Welcome to the server, {arg.Mention}! Please make sure to read through <#814326414618132521>.");
                }
            }
            finally
            {
                q.Close();
            }
        }
    }
}
