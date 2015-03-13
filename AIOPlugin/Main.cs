using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.IO;
using Terraria;
using TerrariaApi.Server;
using TShockAPI;
using TShockAPI.Hooks;
using Newtonsoft.Json;

using TSDB;
using MySql.Data.MySqlClient;
using TShockAPI.DB;


namespace AIOPlugin
{
    [ApiVersion(1, 16)]

    public class AIOPlugin : TerrariaPlugin
    {
        public List<Player>  Players = new List<Player>();
        public List<Grief> Griefs = new List<Grief>();
        public List<Suggestion> Suggestions = new List<Suggestion>();
        Config Config = new Config();
        string path = Path.Combine(TShock.SavePath, "AIOPlugin.json");
        DateTime LastCheck = DateTime.UtcNow;
        private Database Db { get; set; }

        public override string Name
        {
            get
            {
                return "AIOPlugin";
            }
        }
        public override string Author
        {
            get
            {
                return "Loganizer";
            }
        }
        public override string Description
        {
            get
            {
                return "This is of no importance.";
            }
        }
        public override Version Version
        {
            get
            {
                return new Version("1.0");
            }
        }
        public AIOPlugin(Main game)
            : base(game)
        {
            Order = 1;
        }
        public override void Initialize()
        {
            ServerApi.Hooks.GameInitialize.Register(this, OnInitialize);
            ServerApi.Hooks.NetGreetPlayer.Register(this, OnGreet);
            ServerApi.Hooks.ServerLeave.Register(this, OnLeave);
            ServerApi.Hooks.ServerChat.Register(this, OnChat);     
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ServerApi.Hooks.GameInitialize.Deregister(this, OnInitialize);
                ServerApi.Hooks.NetGreetPlayer.Deregister(this, OnGreet);
                ServerApi.Hooks.ServerLeave.Deregister(this, OnLeave);
                ServerApi.Hooks.ServerChat.Register(this, OnChat);              
            }
            base.Dispose(disposing);
        }
        private void OnInitialize(EventArgs e)
        {         
            Commands.ChatCommands.Add(new Command("", Beep, "beep"));
            if (Config.SugestionsEnabled)
            {
                Commands.ChatCommands.Add(new Command("aio.suggest", Suggestion, "suggest"));
                Commands.ChatCommands.Add(new Command("aio.checksuggest", CheckSuggestion, "csuggest"));
            }
            if (Config.StaffChatEnabled)
            {
                Commands.ChatCommands.Add(new Command(StaffChat, "s"));
                Commands.ChatCommands.Add(new Command("aio.staffchat.kick", StaffChatKick, "skick"));
                Commands.ChatCommands.Add(new Command("aio.staffchat.invite", StaffChatInvite, "sinvite"));
                Commands.ChatCommands.Add(new Command("tshock.world.modify", StaffChatList, "slist"));
            }
            if (Config.GriefReporterEnabled)
            {
                Commands.ChatCommands.Add(new Command("tshock.world.modify", ReportGrief, "reportgrief") { AllowServer = false });
                Commands.ChatCommands.Add(new Command("aio.checkgrief", CheckGrief, "checkgrief") { AllowServer = false });
            }

            if (!File.Exists(path))
            {
                Config.Write(path);
            }
            Config = Config.Read(path);
            Griefs = Config.Griefs;
            Suggestions = Config.Suggestions;
            bool afkenabled = Config.AFKEnabled;
        }
        private void OnGreet(GreetPlayerEventArgs e)
        {
            lock (Players)
                Players.Add(new Player(e.Who));
        }
        public void OnLeave(LeaveEventArgs e)
        {
            Players.Remove(Players[e.Who]);
        }
        #region Chat Things
        public void OnChat(ServerChatEventArgs e)
        {
            if (Config.ChatToolsEnabled)
            {
                if (e.Handled)
                    return;

                Player player = Players[e.Who];
                if (player.IdleTime > 0)
                    player.IdleTime = 0;

                if (TShock.Players[e.Who].Group.Name == "guest")
                {
                    {
                        TShock.Players[e.Who].SendInfoMessage("You have not registered! In order to register, type the following commands:");
                        TShock.Players[e.Who].SendInfoMessage("To register your account: /register <password>");
                        TShock.Players[e.Who].SendInfoMessage("Then, to login to your account: /login <password>");
                    }
                }
                var plr = TShock.Players[e.Who];
                //Racial slur, kick
                if (e.Text.ToLower().Contains("nigger") || e.Text.ToLower().Contains("nigga"))
                {
                    TShock.Utils.Kick(plr, "Saying a racial slur in chat is against the rules.");
                    e.Handled = true;
                }
                //CAPS prevention
                if ((e.Text.ToUpper() == e.Text) && (e.Text.Length > 10))
                {
                    plr.SendErrorMessage("Please do not speak in ALL CAPS, as it is against the rules.");
                    e.Handled = true;
                }
            }
        }
     #endregion
        #region Grief Reporter
        public void ReportGrief(CommandArgs e)
        {
            if (Config.GriefReporterEnabled)
            {
                int x = e.Player.TileX;
                int y = e.Player.TileY;
                foreach (Grief g in Griefs)
                {
                    int lx = g.X;
                    int ly = g.Y;
                    if (lx > x - 50 && ly > y - 50 && lx < x + 50 && ly < y + 50)
                    {
                        e.Player.SendInfoMessage("This location has already been reported!");
                        return;
                    }
                }
                Griefs.Add(new Grief(e.Player.TileX, e.Player.TileY, e.Player.Name, DateTime.UtcNow));
                Config.Griefs = Griefs;
                Config.Write(path);
                e.Player.SendInfoMessage("Your grief has been reported! Thanks!");
                Console.WriteLine(string.Format("{0} has sent in a grief report at: {1}, {2}", e.Player.Name, e.Player.TileX, e.Player.TileY));
                foreach (TSPlayer ts in TShock.Players)
                {
                    if (ts != null)
                    {
                        if (ts.Group.HasPermission("tshock.admin.kick"))
                        { ts.SendInfoMessage(string.Format("{0} has sent in a grief report at: {1}, {2}", e.Player.Name, e.Player.TileX, e.Player.TileY)); }
                    }
                }
            }
        }
        public void CheckGrief(CommandArgs e)
        {
            if (Config.GriefReporterEnabled)
            {
                if (Griefs.Count == 0)
                {
                    e.Player.SendInfoMessage("There currently aren't any reported griefs.");
                    return;
                }
                for (int i = 0; i < Griefs.Count; i++)
                {
                    Grief Re = Griefs[i];
                    e.Player.Teleport(Re.X, Re.Y);
                    e.Player.SendInfoMessage(string.Format("Reported by: {0} at {1}", Re.Name, Re.Date));
                    Griefs.Remove(Re);
                }
            }
        }
        #endregion
        #region Staff Chat
        public void StaffChat(CommandArgs e)
        {
            if (Config.StaffChatEnabled)
            {
                if (Players[e.Player.UserID].StaffChat)
                {
                    if (e.Parameters.Count >= 1)
                    {
                        foreach (TSPlayer ts in TShock.Players)
                        {
                            if (ts != null)
                            {
                                if (Players[ts.UserID].StaffChat)
                                {
                                    string message = string.Join(" ", e.Parameters);
                                    ts.SendMessage("[Staffchat] " + e.Player.Name + ": " + message, Color.Pink);
                                }
                            }
                        }
                    }
                    else
                    {
                        e.Player.SendMessage("/s \"[Message]\" is the right format.", Color.Pink);
                    }
                }
                else
                {
                    e.Player.SendErrorMessage("You do not have access to that command because you haven't been invited to the staff chat.");
                }
            }
        }
        public void StaffChatKick(CommandArgs e)
        {
            if (Config.StaffChatEnabled)
            {
                if (e.Parameters.Count < 1)
                {
                    e.Player.SendMessage("Invalid syntax! Syntax: /skick <player>", Color.Red);
                    return;
                }
                var foundplr = TShock.Utils.FindPlayer(e.Parameters[0]);
                if (foundplr.Count == 0)
                {
                    e.Player.SendMessage("Invalid player!", Color.Red);
                }
                else if (foundplr.Count > 1)
                {
                    e.Player.SendMessage(string.Format("More than one ({0}) player matched!", foundplr.Count), Color.Red);
                }
                var plr = foundplr[0];
                {
                    if (Players[e.Player.UserID].StaffChat)
                    {
                        Players[plr.UserID].StaffChat = false;
                        plr.SendInfoMessage("You have been removed from the staffchat.");
                        foreach (TSPlayer ts in TShock.Players)
                        {
                            if (ts != null)
                            {
                                if (Players[e.Player.UserID].StaffChat)
                                {
                                    ts.SendInfoMessage(plr.Name + " has been removed from the staffchat.");
                                }
                            }
                        }
                    }
                    else
                    {
                        e.Player.SendInfoMessage("You can't kick a player that isn't in the chat!");
                    }
                }
            }
        }
        public void StaffChatInvite(CommandArgs e)
        {
            if (Config.StaffChatEnabled)
            {
                if (e.Parameters.Count < 1)
                {
                    e.Player.SendMessage("Invalid syntax! Syntax: /sinvite <player>", Color.Red);
                    return;
                }
                var foundplr = TShock.Utils.FindPlayer(e.Parameters[0]);
                if (foundplr.Count == 0)
                {
                    e.Player.SendMessage("Invalid player!", Color.Red);
                }
                else if (foundplr.Count > 1)
                {
                    e.Player.SendMessage(string.Format("More than one ({0}) player matched!", foundplr.Count), Color.Red);
                }
                var plr = foundplr[0];
                {
                    if (Players[e.Player.UserID].StaffChat)
                    {
                        Players[plr.UserID].StaffChat = true;
                        plr.SendInfoMessage("You have been invited to the staffchat, type /s [message] to talk.");
                        foreach (TSPlayer ts in TShock.Players)
                        {
                            if (ts != null)
                            {
                                if (Players[e.Player.UserID].StaffChat)
                                {
                                    ts.SendInfoMessage(plr.Name + " has been invited to the staffchat.");
                                }
                            }
                        }
                    }
                    else
                    {
                        e.Player.SendInfoMessage("Player is already in the staffchat.");
                    }
                }
            }
        }
        public void StaffChatList(CommandArgs e)
        {
            if (Config.StaffChatEnabled)
            {
                string staffchatlist = "";
                foreach (TSPlayer ts in TShock.Players)
                {
                    if (ts != null)
                    {
                        if (Players[e.Player.UserID].StaffChat)
                        {
                            staffchatlist = staffchatlist + ts.Name + ", ";
                        }
                    }
                }
                e.Player.SendInfoMessage("Players in staffchat: " + staffchatlist);
            }
        }
        #endregion
        #region AFK
        bool afkenabled = true;

        private async void OnUpdate(EventArgs e)
        {
            if (afkenabled)
            {
                if ((DateTime.UtcNow - LastCheck).TotalSeconds >= 5)
                {
                    LastCheck = DateTime.UtcNow;
                    foreach (Player p in Players)
                        if (p != null && p.TSPlayer != null)
                        {
                            if (p.TSPlayer.TileX == p.LastX && p.TSPlayer.TileY == p.LastY)
                            {
                                p.IdleTime = p.IdleTime + 5;
                            }
                            else //if moved
                            {
                                if (p.TSPlayer.Group.HasPermission("tshock.admin.kick")) //if staff
                                {
                                    var sqlVal = new SqlValue("Name", p.TSPlayer.Name); //where name = player name;
                                    var retrievals = await Db.RetrieveValues("StaffTime", "Time", sqlVal); //get current time spent then
                                    int addTime = Convert.ToInt32(retrievals[0]) + 5; //add 5 seconds that were active then
                                    var values = new List<SqlValue> { new SqlValue("Time", addTime) }; //update time                            
                                    var wheres = new List<SqlValue> { new SqlValue("Name", p.TSPlayer.Name) };  //where name = player name
                                    await Db.UpdateValues("StaffTime", values, wheres);

                                }
                                p.IdleTime = 0;
                            }
                            p.LastX = p.TSPlayer.TileX;
                            p.LastY = p.TSPlayer.TileY;
                            //Warping
                            if (p.IdleTime > 120)//5 mins
                            {
                                var warp = TShock.Warps.Find("AFK");
                                p.TSPlayer.Teleport(warp.Position.X, warp.Position.Y);
                            }
                            //Kicking
                            if (p.IdleTime > 600 && !p.TSPlayer.Group.HasPermission("tshock.admin.nokick"))//10 mins
                            {
                                TShock.Utils.Kick(p.TSPlayer, "You have been inactive too long.");
                            }
                            else if (p.IdleTime > 540 && !p.TSPlayer.Group.HasPermission("tshock.admin.nokick"))
                            {
                                p.TSPlayer.SendInfoMessage("You will be kicked for being inactive in " + (600 - p.IdleTime) + " seconds.");
                            }
                        }
                }
            }
        }
        #endregion
        #region Beep Command
        void Beep(CommandArgs e)
        {
            e.Player.SendInfoMessage("Boop!");
            return;
        }
        #endregion
        #region Suggestion Command
        public void Suggestion(CommandArgs e)
        {
            if (Config.SugestionsEnabled)
            {
                if (e.Parameters.Count == 0)
                {
                    e.Player.SendErrorMessage("Incorrect syntax! Proper syntax: /suggest \"Suggestion\"");
                    return;
                }
                if (e.Parameters.Count < 5)
                {
                    e.Player.SendErrorMessage("Your suggestion must have at least 5 words.");
                    return;
                }
                string message = string.Join(" ", e.Parameters);
                //blah, balh, blah, add suggestion to config, blah blah blah.
                Suggestion sug = new Suggestion(e.Player.Name, message);
                Suggestions.Add(sug);
                Config.Suggestions = Suggestions;
                Config.Write(path);
                foreach (TSPlayer p in TShock.Players)
                {
                    if (p.Group.HasPermission("aio.getsuggestion"))
                    {
                        p.SendInfoMessage(e.Player.Name + " has sent in a suggestion: " + message);
                    }
                }
                e.Player.SendSuccessMessage("Thanks for the suggestion! If you would like to describe it in more detail, post in on bunnyville.org.");
            }
        }
        public void CheckSuggestion(CommandArgs e)
        {
            if (Config.SugestionsEnabled)
            {
                if (Suggestions.Count == 0)
                {
                    e.Player.SendErrorMessage("There are currently no suggestions.");
                    return;
                }
                for (int i = 0; i < Suggestions.Count; i++)
                {
                    Suggestion sug = Suggestions[i];
                    e.Player.SendInfoMessage(sug.Message + ". Suggested by " + sug.Name + ".");
                    Suggestions.Remove(sug);
                    Config.Suggestions = Suggestions;
                    Config.Write(path);
                }
            }
        }
        #endregion       

    }      
}

