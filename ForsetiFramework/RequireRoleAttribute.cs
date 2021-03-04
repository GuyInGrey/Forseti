using System;
using System.Linq;
using System.Threading.Tasks;

using Discord.Commands;
using Discord.WebSocket;

namespace ForsetiFramework
{
    public class RequireRoleAttribute : PreconditionAttribute
    {
        private readonly string _name;

        public RequireRoleAttribute(string name) => _name = name;

        public override Task<PreconditionResult> CheckPermissionsAsync(ICommandContext context, CommandInfo command, IServiceProvider services)
        {
            if (context.User is SocketGuildUser gUser)
            {
                return gUser.Roles.Any(r => r.Name.ToLower() == _name.ToLower())
                    ? Task.FromResult(PreconditionResult.FromSuccess())
                    : Task.FromResult(PreconditionResult.FromError($"You must have the role `{_name}` to run this command."));
            }
            else
            {
                return Task.FromResult(PreconditionResult.FromError("You must be in a guild to run this command."));
            }
        }
    }
}
