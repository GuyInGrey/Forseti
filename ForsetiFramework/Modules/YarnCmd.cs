using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Remoting.Contexts;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using ForsetiFramework.Utility;
using Yarn;
using Yarn.Compiler;

namespace ForsetiFramework.Modules
{
    public class YarnCmd : ModuleBase<SocketCommandContext>
    {
        [Command("yarntest")]
        [Summary("Run a yarn file. Requires attachment .yarn file.")]
        [Syntax("yarntest [startNodeName]")]
        public async Task YarnTest(string startNode = Dialogue.DefaultStartNodeName)
        {
            if (Context.Message.Attachments.Count <= 0) { await Context.ReactError(); return; }
            var source = Context.Message.Attachments.First().Url.DownloadString();

            try
            {
                var job = new CompilationJob()
                {
                    CompilationType = CompilationJob.Type.FullCompilation,
                    Files = new List<CompilationJob.File>()
                    {
                        new CompilationJob.File() { FileName = "TestFile", Source = source, },
                    },
                    VariableDeclarations = new List<Declaration>()
                    { 
                        Declaration.CreateVariable("$gold", BotManager.Client.Guilds.Count),
                    },
                };
                var result = Compiler.Compile(job);
                if (!result.Program.Nodes.ContainsKey(startNode))
                {
                    await ReplyAsync("I couldn't find that starting node.");
                    return;
                }


                await ReplyAsync($"Successfully compiled, {result.Program.Nodes.Count} nodes.");
                await RunProgram(result, startNode, Context);
            }
            catch (Exception e) { await ReplyAsync("I couldn't compile that."); throw e; }
        }

        public static async Task RunProgram(CompilationResult res, string startNode, SocketCommandContext con)
        {
            string TextForLine(string lineID)
            {
                var text = res.StringTable[lineID];
                return text.text ?? lineID;
            }

            var storage = new MemoryVariableStore();
            storage.SetValue("$gold", 5);
            var dialogue = new Dialogue(storage)
            {
                //LogDebugMessage = async m => { await new Log() { Content = m, Title = "Yarn Debug" }.Post(); },
                LogDebugMessage = async m => { },
                LogErrorMessage = async m => { await new Log() { Content = m, Color = Color.Red, Title = "Yarn Error" }.Post(); }
            };

            dialogue.SetProgram(res.Program);
            dialogue.SetNode(startNode);

            dialogue.LineHandler = async line =>
            {
                await con.Channel.SendMessageAsync(TextForLine(line.ID));
                Thread.Sleep(1000);
                dialogue.Continue();
            };

            dialogue.CommandHandler = async cmd =>
            {
                await con.Channel.SendMessageAsync("Command: " + cmd.Text);
                dialogue.Continue();
            };

            dialogue.OptionsHandler = async op =>
            {
                var i = 0;
                var desc = string.Join("\n", op.Options.Select(o =>
                {
                    i++;
                    return $"{i}: {TextForLine(o.Line.ID)}";
                }));

                var e = new EmbedBuilder()
                    .WithTitle("Option")
                    .WithColor(Color.Green)
                    .WithDescription(desc);
                await con.Channel.SendMessageAsync(embed: e.Build());

                con.Channel.Id.WaitForMessage(async m =>
                {
                    if (!int.TryParse(m.Content, out var num)) { return false; }
                    if (num < 1 || num > op.Options.Length) { return false; }

                    dialogue.SetSelectedOption(num - 1);
                    dialogue.Continue();
                    return true;
                });
            };

            dialogue.DialogueCompleteHandler = () =>
            {
                con.Channel.SendMessageAsync("Finished.");
            };

            dialogue.NodeCompleteHandler = n => 
            {
                dialogue.Continue();
            };

            dialogue.Continue();
        }
    }
}
