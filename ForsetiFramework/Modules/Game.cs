using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using ForsetiFramework.Utility;

namespace ForsetiFramework.Modules
{
    [Group("Game")]
    public class Game : ModuleBase<SocketCommandContext>
    {
        [Command("hangman")]
        [Summary("Play a game of hangman!")]
        [Syntax("hangman [lives]")]
        public async Task Hangman(int lives = 5)
        {
            if (WaitFor.WaitingFor.Any(w => w.Item1 == Context.Channel.Id))
            {
                await ReplyAsync(Context.User.Mention + ", there's something going here already.");
                return;
            }
            var filter = ModerationEvents.NewFilter();
            var randomWords = "https://www.wordgenerator.net/application/p.php?type=1&id=dictionary_words&spaceflag=true".DownloadString().Split(new[] { "," }, StringSplitOptions.RemoveEmptyEntries).ToList();
            randomWords.RemoveAll(w => filter.DetectAllProfanities(w).Count > 0);
            var word = randomWords[Extensions.Random.Next(0, randomWords.Count)].ToLower();
            var guessed = new string('_', word.Length);
            var guessedLetters = new List<char>();


            void PostGuessed()
            {
                ReplyAsync($"__Hangman (guess letters)__\n" +
                    $"`{guessed.Replace("_", "_ ")}`\n" +
                    $"Lives: {lives}\nGuessed letters: {string.Join(", ", guessedLetters.Distinct())}");
            }

            PostGuessed();

            Context.Channel.Id.WaitForMessage(async (msg) =>
            {
                if (msg.Content.ToLower().Contains("give up") && !msg.Content.StartsWith("!eval")) { await ReplyAsync("Word was: `" + word + "`"); return true; }
                var wordGuessed = false;

                if (msg.Content.ToLower() == word) { wordGuessed = true; guessed = word; }
                if (msg.Content.Length != 1 && !wordGuessed) { return false; }

                guessedLetters.Add(msg.Content.ToLower()[0]);
                for (var i = 0; i < word.Length; i++)
                {
                    if (word[i] == msg.Content.ToLower()[0]) { guessed = guessed.Replace(i, word[i]); }
                }
                if (!word.Contains(msg.Content.ToLower()))
                {
                    lives--;
                }

                if (word == guessed)
                {
                    await ReplyAsync("You got it! Word was **" + word + "**.\n" + lives + " lives to spare.");
                    return true;
                }
                else if (lives <= 0)
                {
                    await ReplyAsync("You lose! Word was **" + word + "**, you guessed " + guessed.Replace("_", "\\_ "));
                    return true;
                }
                PostGuessed();
                return false;
            });
        }

        [Command("mastermind")]
        [Summary("Play a game of `Mastermind`!")]
        [Syntax("mastermind <bot (Bot generates a grid)>")]
        public async Task Mastermind(string option)
        {
            option = option.ToLower();
            if (option == "bot")
            {
                await Mastermind(4, 10, 6, true);
            }
            else
            {
                await ReplyAsync("Invalid option.");
            }
        }

        [Command("mastermind")]
        [Summary("Play a game of `Mastermind`!")]
        [Syntax("mastermind [width(2-8, def 4)] [height(2-20, def 10)] [colors(2-8, def 6)] [botCreatesCode(false)]")]
        public async Task Mastermind(int width = 4, int height = 10, int colorCount = 6, bool botCreatesCode = false)
        {
            if (width < 2 || width > 8) { await ReplyAsync("Invalid width."); return; }
            if (height < 2 || width > 20) { await ReplyAsync("Invalid height."); return; }
            if (colorCount < 2 || colorCount > 8) { await ReplyAsync("Invalid color count."); return; }

            var dm = await Context.User.GetOrCreateDMChannelAsync();
            if (WaitFor.WaitingFor.Any(w => w.Item1 == Context.Channel.Id || w.Item1 == dm.Id)) 
            {
                await ReplyAsync(Context.User.Mention + ", there's something going here already."); 
                return; 
            }
            var colors = new Dictionary<char, string>()
            {
                { 'R', "🔴" },
                { 'B', "🔵" },
                { 'P', "🟣" },
                { 'G', "🟢" },
                { 'W', "⚪" },
                { 'Y', "🟡" },
                { 'O', "🟠" },
                { 'N', "🟤" },
            };
            colors = new Dictionary<char, string>(colors.ToList().Take(colorCount).ToDictionary(p => p.Key, p => p.Value));
            var BL = "⚫";
            var code = "";
            var codeFancy = "";

            var validColors = $"`{string.Join("`, `", colors.Select(s => $"{s.Key} - {s.Value}"))}`";

            if (!botCreatesCode)
            {
                await ReplyAsync($"Please DM me your color pattern, {width} long. Example: `RBYY`\n" +
                    "Valid colors are: " + validColors);
            }

            Func<SocketUserMessage, Task<bool>> game = null;

            if (!botCreatesCode)
            {
                dm.Id.WaitForMessage(async (msg) =>
                {
                    var con = msg.Content.ToUpper();
                    if (con.Length != width) { return false; }
                    var failed = false;
                    con.ToList().ForEach(c => { if (!colors.Keys.Contains(c)) { failed = true; } });
                    if (failed) { return false; }
                    code = con;
                    codeFancy = string.Join("", code.Select(s2 => colors[s2]));

                    Context.Channel.Id.WaitForMessage(game);
                    await ReplyAsync("The puzzle maker has chosen a code.");
                    return true;
                });
            }

            var steps = new List<(string, string, string)>();

            IUserMessage message = null;
            async Task Post(Color color)
            {
                var c = steps.Select(s =>
                {
                    var left = string.Join("", s.Item1.Select(s2 => colors[s2]));
                    var right = string.Join("", s.Item2.Select(s2 => colors.ContainsKey(s2) ? colors[s2] : BL));
                    return $"|{left}|{right}|{s.Item3}";
                });
                var c2 = $"```\n{string.Join("\n", c)}";
                var l = new string('⚫', width);
                for (var i = 0; i < height - c.Count(); i++)
                {
                    c2 += $"\n|{l}|{l}|";
                }
                c2 += "\n```";

                var e = new EmbedBuilder()
                    .WithCurrentTimestamp()
                    .WithTitle("Mastermind")
                    .AddField("Colors", validColors.Replace(", ", "\n"), true)
                    .AddField("Colors", "`🔴 - Correct place, correct color\n" +
                    "⚪ - Wrong place, correct color\n" +
                    "⚫ - Wrong place, wrong color`", true)
                    .WithDescription(c2)
                    .WithColor(color);

                if (message is null)
                {
                    message = await ReplyAsync(embed: e.Build());
                }
                else
                {
                    await message.ModifyAsync(m =>
                    {
                        m.Embed = e.Build();
                    });
                }
            }
            await Post(Color.Blue);

            game = new Func<SocketUserMessage, Task<bool>>(async (msg) =>
            {
                var con = msg.Content.ToUpper();
                if (con.Length != width) { return false; }
                var failed = false;
                con.ToList().ForEach(c => { if (!colors.Keys.Contains(c)) { failed = true; } });
                if (failed) { return false; }


                var correct = 0;
                var misplaced = 0;
                for (var i = 0; i < width; i++)
                {
                    if (con[i] == code[i])
                    {
                        correct++;
                        continue;
                    }
                    for (var j = 0; j < width; j++)
                    {
                        if (con[i] == code[j])
                        {
                            misplaced++;
                            break;
                        }
                    }
                }
                var s = new string('R', correct) + new string('W', misplaced);
                s = s.PadRight(width, ' ');
                steps.Add((con, s, msg.Author.ToString()));

                await msg.DeleteAsync();
                if (con == code)
                {
                    await Post(Color.Green);
                    await ReplyAsync($"You got the code! `{codeFancy}`");
                    return true;
                }

                if (steps.Count >= height)
                {
                    await Post(Color.Red);
                    await ReplyAsync($"You've failed to crack the code. It was `{codeFancy}`. Sorry!");
                    return true;
                }
                else
                {
                    await Post(Color.Blue);
                }

                return false;
            });

            if (botCreatesCode)
            {
                code = new string(Enumerable.Repeat(colors.Keys.ToArray(), width)
                    .Select(s => s[Extensions.Random.Next(s.Length)]).ToArray());
                codeFancy = string.Join("", code.Select(s2 => colors[s2]));

                Context.Channel.Id.WaitForMessage(game);
                await ReplyAsync("The bot has chosen a code.");
            }
        }
    }
}
