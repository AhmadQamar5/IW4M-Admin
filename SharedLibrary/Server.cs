﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

using SharedLibrary.Network;
using SharedLibrary.Commands;
using System.Threading.Tasks;

namespace SharedLibrary
{
    [Guid("61d3829e-fcbe-44d3-bb7c-51db8c2d7ac5")]
    public abstract class Server
    {
        public Server(Interfaces.IManager mgr, string address, int port, string password)
        {
            Password = password;
            IP = address;
            Port = port;
            Manager = mgr;
            ClientNum = 0;
            logFile = new IFile($"Logs/{address}_{port}.log", true);
#if DEBUG
            Log = new Log(logFile, Log.Level.Debug, port);
#else
            Log = new Log(logFile, Log.Level.Production, port);
#endif
            clientDB = new ClientsDB("Database/clients.rm");
            aliasDB = new AliasesDB("Database/aliases.rm");

            Bans = new List<Penalty>();
            Players = new List<Player>(new Player[18]);
            events = new Queue<Event>();
            Macros = new Dictionary<String, Object>();
            Reports = new List<Report>();
            statusPlayers = new Dictionary<string, Player>();
            playerHistory = new Queue<PlayerHistory>();
            chatHistory = new List<Chat>();
            lastWebChat = DateTime.Now;
            nextMessage = 0;
            initMacros();
            initMessages();
            initMaps();
            initRules();

            var commands = mgr.GetCommands();

            owner = clientDB.getOwner();

            if (owner == null)
                commands.Add(new Owner("owner", "claim ownership of the server", "owner", Player.Permission.User, 0, false));

            commands.Add(new Quit("quit", "quit IW4MAdmin", "q", Player.Permission.Owner, 0, false));
            commands.Add(new Kick("kick", "kick a player by name. syntax: !kick <player> <reason>.", "k", Player.Permission.Trusted, 2, true));
            commands.Add(new Say("say", "broadcast message to all players. syntax: !say <message>.", "s", Player.Permission.Moderator, 1, false));
            commands.Add(new TempBan("tempban", "temporarily ban a player for 1 hour. syntax: !tempban <player> <reason>.", "tb", Player.Permission.Moderator, 2, true));
            commands.Add(new SBan("ban", "permanently ban a player from the server. syntax: !ban <player> <reason>", "b", Player.Permission.SeniorAdmin, 2, true));
            commands.Add(new WhoAmI("whoami", "give information about yourself. syntax: !whoami.", "who", Player.Permission.User, 0, false));
            commands.Add(new List("list", "list active clients syntax: !list.", "l", Player.Permission.Moderator, 0, false));
            commands.Add(new Help("help", "list all available commands. syntax: !help.", "h", Player.Permission.User, 0, false));
            commands.Add(new FastRestart("fastrestart", "fast restart current map. syntax: !fastrestart.", "fr", Player.Permission.Moderator, 0, false));
            commands.Add(new MapRotate("maprotate", "cycle to the next map in rotation. syntax: !maprotate.", "mr", Player.Permission.Administrator, 0, false));
            commands.Add(new SetLevel("setlevel", "set player to specified administration level. syntax: !setlevel <player> <level>.", "sl", Player.Permission.Owner, 2, true));
            commands.Add(new Usage("usage", "get current application memory usage. syntax: !usage.", "us", Player.Permission.Moderator, 0, false));
            commands.Add(new Uptime("uptime", "get current application running time. syntax: !uptime.", "up", Player.Permission.Moderator, 0, false));
            commands.Add(new Warn("warn", "warn player for infringing rules syntax: !warn <player> <reason>.", "w", Player.Permission.Trusted, 2, true));
            commands.Add(new WarnClear("warnclear", "remove all warning for a player syntax: !warnclear <player>.", "wc", Player.Permission.Trusted, 1, true));
            commands.Add(new Unban("unban", "unban player by database id. syntax: !unban @<id>.", "ub", Player.Permission.SeniorAdmin, 1, true));
            commands.Add(new Admins("admins", "list currently connected admins. syntax: !admins.", "a", Player.Permission.User, 0, false));
            commands.Add(new MapCMD("map", "change to specified map. syntax: !map", "m", Player.Permission.Administrator, 1, false));
            commands.Add(new Find("find", "find player in database. syntax: !find <player>", "f", Player.Permission.SeniorAdmin, 1, false));
            commands.Add(new Rules("rules", "list server rules. syntax: !rules", "r", Player.Permission.User, 0, false));
            commands.Add(new PrivateMessage("privatemessage", "send message to other player. syntax: !pm <player> <message>", "pm", Player.Permission.User, 2, true));
            commands.Add(new Flag("flag", "flag a suspicious player and announce to admins on join . syntax !flag <player>:", "flag", Player.Permission.Moderator, 1, true));
            commands.Add(new _Report("report", "report a player for suspicious behaivor. syntax !report <player> <reason>", "rep", Player.Permission.User, 2, true));
            commands.Add(new Reports("reports", "get most recent reports. syntax !reports", "reports", Player.Permission.Moderator, 0, false));
            commands.Add(new _Tell("tell", "send onscreen message to player. syntax !tell <player> <message>", "t", Player.Permission.Moderator, 2, true));
            commands.Add(new Mask("mask", "hide your online presence from online admin list. syntax: !mask", "mask", Player.Permission.Administrator, 0, false));
            commands.Add(new BanInfo("baninfo", "get information about a ban for a player. syntax: !baninfo <player>", "bi", Player.Permission.Moderator, 1, true));
            commands.Add(new Alias("alias", "get past aliases and ips of a player. syntax: !alias <player>", "known", Player.Permission.Moderator, 1, true));
            commands.Add(new _RCON("rcon", "send rcon command to server. syntax: !rcon <command>", "rcon", Player.Permission.Owner, 1, false));
            commands.Add(new FindAll("findall", "find a player by their aliase(s). syntax: !findall <player>", "fa", Player.Permission.Moderator, 1, false));
        }

        //Returns the current server name -- *STRING*
        public String getName()
        {
            return Hostname;
        }

        public String getGametype()
        {
            return Gametype;
        }

        //Returns current server IP set by `net_ip` -- *STRING*
        public String getIP()
        {
            return IP;
        }

        //Returns current server port set by `net_port` -- *INT*
        public int getPort()
        {
            return Port;
        }

        //Returns number of active clients on server -- *INT*
        public int getNumPlayers()
        {
            return ClientNum;
        }

        //Returns list of all current players
        public List<Player> getPlayers()
        {
            return Players.FindAll(x => x != null);
        }

        //Returns list of all active bans (loaded at runtime)
        public List<Penalty> getBans()
        {
            return Bans;
        }

        public int pID()
        {
            return this.PID;
        }

        /// <summary>
        /// Get any know aliases ( name or ip based ) from the database
        /// </summary>
        /// <param name="Origin">Player to scan for aliases</param>
        abstract public List<Aliases> getAliases(Player Origin);

        public List<Player> getPlayerAliases(Player Origin)
        {
            List<int> databaseIDs = new List<int>();

            foreach (Aliases A in getAliases(Origin))
                databaseIDs.Add(A.Number);

            return clientDB.getPlayers(databaseIDs);
        }

        /// <summary>
        /// Add a player to the server's player list
        /// </summary>
        /// <param name="P">Player pulled from memory reading</param>
        /// <returns>True if player added sucessfully, false otherwise</returns>
        abstract public Task<bool> AddPlayer(Player P);

        /// <summary>
        /// Remove player by client number
        /// </summary>
        /// <param name="cNum">Client ID of player to be removed</param>
        /// <returns>true if removal succeded, false otherwise</returns>
        abstract public Task RemovePlayer(int cNum);

        /// <summary>
        /// Get the player from the server's list by line from game long
        /// </summary>
        /// <param name="L">Game log line containing event</param>
        /// <param name="cIDPos">Position in the line where the cliet ID is written</param>
        /// <returns>Matching player if found</returns>
        abstract public Player clientFromEventLine(String[] L, int cIDPos);

        /// <summary>
        /// Get a player by name
        /// </summary>
        /// <param name="pName">Player name to search for</param>
        /// <returns>Matching player if found</returns>
        public Player clientFromName(String pName)
        {
            lock (Players)
            {
                foreach (var P in Players)
                {
                    if (P != null && P.Name.ToLower().Contains(pName.ToLower()))
                        return P;
                }
            }

            return null;
        }

        /// <summary>
        /// Check ban list for every banned player and return ban if match is found 
        /// </summary>
        /// <param name="C">Player to check if banned</param>
        /// <returns>Matching ban if found</returns>
        abstract public Penalty isBanned(Player C);

        /// <summary>
        /// Process requested command correlating to an event
        /// </summary>
        /// <param name="E">Event parameter</param>
        /// <param name="C">Command requested from the event</param>
        /// <returns></returns>
        abstract public Task<Command> ProcessCommand(Event E, Command C);

        virtual public Task<int> ProcessUpdatesAsync()
        {
            return null;
        }

        /// <summary>
        /// Set up the basic variables ( base path / hostname / etc ) that allow the monitor thread to work
        /// </summary>
        /// <returns>True if no issues initializing, false otherwise</returns>
        //abstract public bool intializeBasics();

        /// <summary>
        /// Process any server event
        /// </summary>
        /// <param name="E">Event</param>
        /// <returns>True on sucess</returns>
        abstract protected Task ProcessEvent(Event E);
        abstract public Task ExecuteEvent(Event E);

        /// <summary>
        /// Reloads all the server configurations
        /// </summary>
        /// <returns>True on sucess</returns>
        abstract public bool Reload();
        abstract public bool _Reload();

        /// <summary>
        /// Send a message to all players
        /// </summary>
        /// <param name="Message">Message to be sent to all players</param>
        public async Task Broadcast(String Message)
        {
            await this.ExecuteCommandAsync($"sayraw  {Message}");
        }

        /// <summary>
        /// Send a message to a particular players
        /// </summary>
        /// <param name="Message">Message to send</param>
        /// <param name="Target">Player to send message to</param>
        public async Task Tell(String Message, Player Target)
        {
            if (Target.clientID > -1 && Message.Length > 0 && Target.Level != Player.Permission.Console && !Target.lastEvent.Remote)
                await this.ExecuteCommandAsync($"tellraw {Target.clientID} {Message}^7");

            if (Target.Level == Player.Permission.Console)
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine(Utilities.StripColors(Message));
                Console.ForegroundColor = ConsoleColor.Gray;
            }

            if (Target.lastEvent.Remote)
                commandResult.Enqueue(Utilities.StripColors(Message));
        }

        /// <summary>
        /// Send a message to all admins on the server
        /// </summary>
        /// <param name="message">Message to send out</param>
        public async Task ToAdmins(String message)
        {
            foreach (Player P in Players)
            {
                if (P == null)
                    continue;

                if (P.Level > Player.Permission.Flagged)
                    await P.Tell(message);
            }
        }

        /// <summary>
        /// Kick a player from the server
        /// </summary>
        /// <param name="Reason">Reason for kicking</param>
        /// <param name="Target">Player to kick</param>
        abstract public Task Kick(String Reason, Player Target, Player Origin);

        /// <summary>
        /// Temporarily ban a player ( default 1 hour ) from the server
        /// </summary>
        /// <param name="Reason">Reason for banning the player</param>
        /// <param name="Target">The player to ban</param>
        abstract public Task TempBan(String Reason, Player Target, Player Origin);

        /// <summary>
        /// Perm ban a player from the server
        /// </summary>
        /// <param name="Reason">The reason for the ban</param>
        /// <param name="Target">The person to ban</param>
        /// <param name="Origin">The person who banned the target</param>
        abstract public Task Ban(String Reason, Player Target, Player Origin);

        abstract public Task Warn(String Reason, Player Target, Player Origin);

        /// <summary>
        /// Unban a player by npID / GUID
        /// </summary>
        /// <param name="npID">npID of the player</param>
        /// <param name="Target">I don't remember what this is for</param>
        /// <returns></returns>
        abstract public Task Unban(String npID, Player Target);

        /// <summary>
        /// Change the current searver map
        /// </summary>
        /// <param name="mapName">Non-localized map name</param>
        public async Task LoadMap(string mapName)
        {
            await this.ExecuteCommandAsync($"map {mapName}");
        }

        public async Task LoadMap(Map newMap)
        {
            await this.ExecuteCommandAsync($"map {newMap.Name}");
        }

        public void webChat(Player P, String Message)
        {
            DateTime requestTime = DateTime.Now;

            if ((requestTime - lastWebChat).TotalSeconds > 1)
            {
                Broadcast("^1[WEBCHAT] ^5" + P.Name + "^7 - " + Message);
                while (chatHistory.Count > Math.Ceiling((double)ClientNum / 2))
                    chatHistory.RemoveAt(0);

                if (Message.Length > 50)
                    Message = Message.Substring(0, 50) + "...";

                chatHistory.Add(new Chat(P.Name, Utilities.StripColors(Message), DateTime.Now));
                lastWebChat = DateTime.Now;
            }
        }

        /// <summary>
        /// Initalize the macro variables
        /// </summary>
        abstract public void initMacros();

        /// <summary>
        /// Read the map configuration
        /// </summary>
        protected void initMaps()
        {
            maps = new List<Map>();

            IFile mapfile = new IFile("config/maps.cfg");
            String[] _maps = mapfile.readAll();
            mapfile.Close();
            if (_maps.Length > 2) // readAll returns minimum one empty string
            {
                foreach (String m in _maps)
                {
                    String[] m2 = m.Split(':');
                    if (m2.Length > 1)
                    {
                        Map map = new Map(m2[0].Trim(), m2[1].Trim());
                        maps.Add(map);
                    }
                }
            }     
            else
                Log.Write("Maps configuration appears to be empty - skipping...", Log.Level.All);
        }

        /// <summary>
        /// Initialize the messages to be broadcasted
        /// </summary>
        protected void initMessages()
        {
            messages = new List<String>();

            IFile messageCFG = new IFile("config/messages.cfg");
            String[] lines = messageCFG.readAll();
            messageCFG.Close();

            if (lines.Length < 2) //readAll returns minimum one empty string
            {
                Log.Write("Messages configuration appears empty - skipping...", Log.Level.All);
                return;
            }

            int mTime = -1;
            int.TryParse(lines[0], out mTime);

            if (messageTime == -1)
                messageTime = 60;
            else
                messageTime = mTime;
            
            foreach (String l in lines)
            {
                if (lines[0] != l && l.Length > 1)
                    messages.Add(l);
            }

            messageCFG.Close();

            //if (Program.Version != Program.latestVersion && Program.latestVersion != 0)
              // messages.Add("^5IW4M Admin ^7is outdated. Please ^5update ^7to version " + Program.latestVersion);
        }

        /// <summary>
        /// Initialize the rules configuration
        /// </summary>
        protected void initRules()
        {
            rules = new List<String>();

            IFile ruleFile = new IFile("config/rules.cfg");
            String[] _rules = ruleFile.readAll();
            ruleFile.Close();
            if (_rules.Length > 2) // readAll returns minimum one empty string
            {
                foreach (String r in _rules)
                {
                    if (r.Length > 1)
                        rules.Add(r);
                }
            }
            else
                Log.Write("Rules configuration appears empty - skipping...", Log.Level.All);

            ruleFile.Close();
        }

        /// <summary>
        /// Load up the built in commands
        /// </summary>
        abstract public void initCommands();

        //Objects
        public Interfaces.IManager Manager { get; protected set; }
        public Log Log { get; private set; }
        public List<Penalty> Bans;
        public Player owner;
        public List<Map> maps;
        public List<String> rules;
        public Queue<Event> events;
        public String Website;
        public String Gametype;
        public int totalKills = 0;
        public List<Report> Reports;
        public List<Chat> chatHistory;
        public Queue<PlayerHistory> playerHistory { get; private set; }

        //Info
        protected String IP;
        protected int Port;
        public String Hostname { get; protected set; }
        public Map CurrentMap { get; protected set; }
        protected string FSGame;
        public int ClientNum { get; protected set; }
        public int MaxClients { get; protected set; }
        public List<Player> Players { get; protected set; }
        protected List<String> messages;
        protected int messageTime;
        protected TimeSpan lastMessage;
        protected DateTime lastPoll;
        protected int nextMessage;

        protected Dictionary<String, Object> Macros;
        protected DateTime lastWebChat;
        public string Password { get; private set; }
        public int Handle { get; private set; }
        protected int PID;
        protected IFile logFile;

        // Will probably move this later
        public Dictionary<String, Player> statusPlayers;
        public bool isRunning;

        // Log stuff
        protected String Mod;

        // Databases
        public ClientsDB clientDB;
        public AliasesDB aliasDB;

        //Remote
        public Queue<String> commandResult = new Queue<string>();
    }
}
