using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Veylib.CLIUI;
using Discord.Gateway;
using System.Drawing;
using System.IO;

namespace ScrapeGuildMembers
{
    class Program
    {
        static void Main(string[] args)
        {
            Core core = Core.GetInstance();
            core.Start(new StartupProperties { });

            System.Threading.Thread.Sleep(500);

            var token = core.ReadLine("Token? ");
            var guildid = ulong.Parse(core.ReadLine("Guild ID? "));
            var count = int.Parse(core.ReadLine("How many users to scrape? "));

            var client = new DiscordSocketClient();
            client.Login(token);
            client.OnLoggedIn += (_, _2) =>
            {
                var random = true;

                List<string> ids = new List<string>();
                Random rand = new Random();

                SocketGuild guild = client.GetCachedGuild(guildid);

                core.WriteLine(Color.Yellow, "Fetching guild members, this will take a while");
                var members = guild.GetMembers();
                for (var x = 0; x < count; x++)
                {
                    int randIndex = rand.Next(members.Count - 1);
                    if (!ids.Contains(members[randIndex].User.Id.ToString()))
                    {
                        ids.Add(members[randIndex].User.Id.ToString());
                        core.WriteLine(Color.Lime, $"Added {members[randIndex].User.Username}#{members[randIndex].User.Discriminator} to list");
                    }
                }

                File.WriteAllText("scraped-ids.txt", string.Join("\n", ids));
            };
        }
    }
}
