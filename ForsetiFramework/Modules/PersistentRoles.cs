using System;
using System.Linq;
using System.Threading.Tasks;
using Discord.WebSocket;
using MySql.Data.MySqlClient;

namespace ForsetiFramework.Modules
{
    public class PersistentRoles
    {
        static MySqlConnection Connection;
        static bool Enabled;

        public static void Initialize() { }

        static PersistentRoles()
        {
            Connection = new MySqlConnection(BotManager.Instance.Config.DatabaseConnectionString);
            try
            {
                Connection.Open();
                Enabled = true;
            }
            catch (Exception e) { Console.WriteLine(e); return; }

            @"CREATE TABLE IF NOT EXISTS `forseti`.`persistentroles` (
  `userID` BIGINT(18) NOT NULL,
  `roles` TEXT NULL,
  PRIMARY KEY (`userID`),
  UNIQUE INDEX `userID_UNIQUE` (`userID` ASC) VISIBLE);".NonQuery(Connection);

            //BotManager.Instance.Client.UserUpdated += Client_UserUpdated;
            BotManager.Instance.Client.UserJoined += Client_UserJoined;
            BotManager.Instance.Client.GuildMemberUpdated += Client_UserUpdated;
        }

        private static async Task Client_UserUpdated(SocketGuildUser arg1, SocketGuildUser arg2)
        {
            var roleString = string.Join(",", arg2.Roles.Where(r => !r.Name.Contains("everyone")).Select(r => r.Id));

            "DELETE FROM `forseti`.`persistentroles` WHERE userID=@p0;".NonQuery(Connection, arg2.Id.ToString());
            "INSERT INTO `forseti`.`persistentroles` (userID, roles) VALUES (@p0, @p1);".NonQuery(Connection, arg2.Id.ToString(), roleString);
            Console.WriteLine("Updated entry for " + arg2.Username);
        }

        private static async Task Client_UserJoined(SocketGuildUser arg)
        {
            var guild = BotManager.Instance.Client.GetGuild(769057370646511628);

            var q = "SELECT * FROM `forseti`.`persistentroles` WHERE userID=@p0".Query(Connection, arg.Id);
            try
            {
                if (q.HasRows)
                {
                    await q.ReadAsync();
                    var roles = q["roles"].ToString().Split(',').Select(r => ulong.Parse(r)).ToList();
                    roles.ForEach(r =>
                    {
                        var role = guild.GetRole(r);
                        if (!(role is null))
                        {
                            arg.AddRoleAsync(role);
                        }
                    });

                    await Moderation.General.SendMessageAsync($"Welcome back, {arg.Mention}! Please make sure to read through <#814326414618132521>.");
                    await Moderation.ModLogs.SendMessageAsync("Added old roles from rejoined user " + arg.Username);
                }
                else
                {
                    var memberRole = guild.Roles.FirstOrDefault(r => r.Name == "Member");
                    await arg.AddRoleAsync(memberRole);
                    await Moderation.General.SendMessageAsync($"Welcome to the server, {arg.Mention}! Please make sure to read through <#814326414618132521>.");
                }
            }
            finally
            {
                q.Close();
            }
        }
    }
}
