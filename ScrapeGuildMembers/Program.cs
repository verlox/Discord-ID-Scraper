using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Veylib.CLIUI;
using Discord.Gateway;
using System.Drawing;
using System.IO;
using System.Net.WebSockets;
using WebSocketSharp;
using System.Diagnostics;
using System.Threading;
using Newtonsoft.Json;

namespace ScrapeGuildMembers
{
    class Program
    {
        public static WebSocketSharp.WebSocket Socket;
        public static string Token = string.Empty;
        public static ulong GuildId = 0;
        public static ulong ChannelId = 0;
        public static int Members = 0;

        public static List<string> Ids = new List<string>();
        public static int Sequence = 0;


        public static void Heartbeat(int interval)
        {
            while (true)
            {
                Thread.Sleep(interval - 1000);

                if (!Socket.IsAlive)
                {
                    Core.GetInstance().WriteLine("Socket is not alive, cancelling heartbeat");
                    return;
                }
                Socket.Send(Encoding.UTF8.GetBytes("{\"op\":1, \"d\":" + Sequence + "}"));
                Core.GetInstance().WriteLine(new MessageProperties { Label = new MessagePropertyLabel { Text = "ok" } }, "Sent heartbeat to gateway");
                
                File.AppendAllText("socket.log", $"Sent heartbeat with sequence {Sequence} to gateway\n");
                Debug.WriteLine($"Sent heartbeat with sequence {Sequence}");
            }
        }

        public static void Identify()
        {
            string data = "{\"op\":2,\"d\":{\"token\":\"" + Token + "\",\"intents\":2,\"properties\":{\"$os\":\"linux\",\"$browser\":\"custom\",\"$device\":\"gucci toilet\"}}}";
            Socket?.Send(Encoding.UTF8.GetBytes(data));
        }

        private static string createRange(int start)
        {
            var ranges = new List<string> { "[0,99]" };
            for (var x = start; x < start + 2; x++)
            {
                ranges.Add($"[{x}00,{x}99]");
            }

            return string.Join(", ", ranges);
        }


        public static void GetGuildMembers()
        {
            for (var x = 1;x < int.Parse(Members.ToString().Substring(0, Members.ToString().Length - 2));x++)
            {
                string data = "{\"op\":14,\"d\":{\"guild_id\":\"" + GuildId + "\",\"channels\":{\"" + ChannelId + "\":[" + string.Join(", ", createRange(x)) + "]}}}";
                Socket?.Send(Encoding.UTF8.GetBytes(data));
                Debug.WriteLine(data);
            }
        }

        public static void FormatMembers(dynamic Json)
        {
            foreach (dynamic op in Json.d.ops)
            {
                if (op.op == "SYNC")
                {
                    foreach (dynamic user in op.items)
                    {
                        try
                        {
                            if (user.member == null)
                                continue;

                            Ids.Add((string)user.member.user.id);
                            Core.GetInstance().WriteLine("Scraped ID ", (string)user.member.user.id);
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine(ex);
                        }
                    }

                    File.WriteAllText("scraped-ids.txt", string.Join("\n", Ids));
                    Core.GetInstance().WriteLine($"Scraped ", Color.Green, Ids.Count.ToString(), null, " IDs");
                } else if (op.op == "UPDATE")
                {
                    if (Ids.Contains(op.item.member.user.id.ToString()))
                        continue;

                    Ids.Add((string)op.item.member.user.id);
                    Core.GetInstance().WriteLine("Scraped ID ", (string)op.item.member.user.id);
                }
            }
        }

        public static void ParseEvent(dynamic Json)
        {
            switch ((string)Json.t)
            {
                case "READY":
                    Core.GetInstance().WriteLine($"READY event received. Logged into ", Color.White, $"{Json.d.user.username}#{Json.d.user.discriminator}");
                    GetGuildMembers();
                    break;
                case "GUILD_MEMBER_LIST_UPDATE":
                    FormatMembers(Json);
                    break;
            }
        }

        static void Main(string[] args)
        {
            Console.CursorVisible = false;

            Core core = Core.GetInstance();
            core.Start(new StartupProperties { SilentStart = true });

            for (var x = 0;x < args.Length;x++)
            {
                if (args[x].Contains("--token"))
                    Token = args[x + 1];
                else if (args[x].Contains("--guild"))
                    GuildId = ulong.Parse(args[x + 1]);
                else if (args[x].Contains("--channel"))
                    ChannelId = ulong.Parse(args[x + 1]);
                else if (args[x].Contains("--members"))
                    Members = int.Parse(args[x + 1]);
            }

            if (Token == string.Empty)
                Token = core.ReadLine("Token? ");
            if (GuildId == 0)
                GuildId = ulong.Parse(core.ReadLine("Guild ID? "));
            if (ChannelId == 0)
                ChannelId = ulong.Parse(core.ReadLine("Channel ID? "));
            if (Members == 0)
                Members = Math.Max(int.Parse(core.ReadLine("Members to scrape? ")), 100);

            // open a socket on gateway version 9 with encoding type of json
            Socket = new WebSocketSharp.WebSocket("wss://gateway.discord.gg/?v=9&encoding=json");

            string data = string.Empty;

            // message from gateway
            Socket.OnMessage += (sender, e) =>
            {
                File.AppendAllText("socket.log", $"Incoming -> {e.Data}\n");
                Debug.WriteLine($"Incoming -> {e.Data}");

                var json = JsonConvert.DeserializeObject<dynamic>(e.Data);

                if (json.s != null)
                    Sequence = (int)json.s;

                switch ((int)json["op"])
                {
                    case 0:
                        ParseEvent(json);
                        break;
                    case 9:
                        Identify();
                        break;
                    case 10:
                        new Thread(() => Heartbeat((int)json["d"]["heartbeat_interval"])).Start();
                        break;
                }
            };
            Socket.OnError += (sender, e) =>
            {
                Debug.WriteLine(e.Exception);
                core.WriteLine("Socket error. Check ", Color.White, "socket.log", null, " for the full error");
                File.AppendAllText("socket.log", $"{e}\n");
            };
            Socket.OnOpen += (sender, e) =>
            {
                core.WriteLine("Socket was ", Color.LimeGreen, "opened");
                File.AppendAllText("socket.log", $"Socket to {Socket.Url} was opened\n");
            };
            Socket.OnClose += (sender, e) =>
            {
                core.WriteLine("Socket was ", Color.Red, "closed");
                File.AppendAllText("socket.log", $"Socket to {Socket.Url} was closed\n");
            };

            // connect
            Socket.Connect();

            // say hello and identify yourself to the gateway
            Identify();
        }
    }
}
