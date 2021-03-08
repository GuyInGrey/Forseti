using System;
using System.Threading.Tasks;

using Discord.Commands;

namespace ForsetiFramework
{
    public class RequireProductionAttribute : PreconditionAttribute
    {
        public override Task<PreconditionResult> CheckPermissionsAsync(ICommandContext context, CommandInfo command, IServiceProvider services)
        {
            return Config.Debug ? Task.FromResult(PreconditionResult.FromError("This command is only available in the production bot.")) :
                Task.FromResult(PreconditionResult.FromSuccess());
        }
    }
}
