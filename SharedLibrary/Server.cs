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
        public enum Game
        {
            UKN,
            IW3,
            IW4,
            IW5,
            T4,
            T5,
        }

        public Server(Interfaces.IManager mgr, ServerConfiguration config)
        {
            Password = config.Password;
            IP = config.IP;
            Port = config.Port;
            Manager = mgr;
            Logger = Manager.GetLogger();
            ClientNum = 0;
            Config = config;

            Players = new List<Player>(new Player[18]);
            Reports = new List<Report>();
            PlayerHistory = new Queue<Helpers.PlayerHistory>();
            ChatHistory = new List<Chat>();
            NextMessage = 0;
            InitializeTokens();
            InitializeAutoMessages();
            InitializeMaps();
            InitializeRules();
        }

        //Returns current server IP set by `net_ip` -- *STRING*
        public String GetIP()
        {
            return IP;
        }

        //Returns current server port set by `net_port` -- *INT*
        public int GetPort()
        {
            return Port;
        }

        //Returns list of all current players
        public List<Player> GetPlayersAsList()
        {
            return Players.FindAll(x => x != null);
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
        abstract public Player ParseClientFromString(String[] L, int cIDPos);

        /// <summary>
        /// Get a player by name
        /// </summary>
        /// <param name="pName">Player name to search for</param>
        /// <returns>Matching player if found</returns>
        public Player GetClientByName(String pName)
        {
            string[] QuoteSplit = pName.Split('"');
            if (QuoteSplit.Length > 1)
                pName = QuoteSplit[1];
            return Players.FirstOrDefault(p => p != null && p.Name.ToLower().Contains(pName.ToLower()));
        }

        /// <summary>
        /// Check ban list for every banned player and return ban if match is found 
        /// </summary>
        /// <param name="C">Player to check if banned</param>
        /// <returns>Matching ban if found</returns>
        abstract public Penalty IsBanned(Player C);

        /// <summary>
        /// Process requested command correlating to an event
        /// </summary>
        /// <param name="E">Event parameter</param>
        /// <param name="C">Command requested from the event</param>
        /// <returns></returns>
        abstract public Task<Command> ValidateCommand(Event E);

        virtual public Task ProcessUpdatesAsync(CancellationToken cts)
        {
            return null;
        }
        
        /// <summary>
        /// Legacy method for the alias command
        /// </summary>
        /// <param name="P"></param>
        /// <returns></returns>
        public IList<Aliases> GetAliases(Player P)
        {
            return Manager.GetAliases(P);
        }

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

        /// <summary>
        /// Send a message to all players
        /// </summary>
        /// <param name="Message">Message to be sent to all players</param>
        public async Task Broadcast(String Message)
        {
#if DEBUG
            //return;
#endif
            string sayCommand = (GameName == Game.IW4) ? "sayraw" : "say";
            await this.ExecuteCommandAsync($"{sayCommand} {Message}");
        }

        /// <summary>
        /// Send a message to a particular players
        /// </summary>
        /// <param name="Message">Message to send</param>
        /// <param name="Target">Player to send message to</param>
        public async Task Tell(String Message, Player Target)
        {
#if DEBUG
            //if (!Target.lastEvent.Remote)
             //  return;
#endif
            string tellCommand = (GameName == Game.IW4) ? "tellraw" : "tell";

            if (Target.ClientID > -1 && Message.Length > 0 && Target.Level != Player.Permission.Console && !Target.lastEvent.Remote)
                await this.ExecuteCommandAsync($"{tellCommand} {Target.ClientID} {Message}^7");

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
        abstract public Task Unban(Player Target);

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

        /// <summary>
        /// Initalize the macro variables
        /// </summary>
        abstract public void InitializeTokens();

        /// <summary>
        /// Read the map configuration
        /// </summary>
        protected void InitializeMaps()
        {
            Maps = new List<Map>();

            IFile mapfile = new IFile("config/maps.cfg");
            String[] _maps = mapfile.ReadAllLines();
            mapfile.Close();
            if (_maps.Length > 2) // readAll returns minimum one empty string
            {
                foreach (String m in _maps)
                {
                    String[] m2 = m.Split(':');
                    if (m2.Length > 1)
                    {
                        Map map = new Map(m2[0].Trim(), m2[1].Trim());
                        Maps.Add(map);
                    }
                }
            }     
            else
                Logger.WriteInfo("Maps configuration appears to be empty - skipping...");
        }

        /// <summary>
        /// Initialize the messages to be broadcasted
        /// todo: this needs to be a serialized file
        /// </summary>
        protected void InitializeAutoMessages()
        {
            BroadcastMessages = new List<String>();

            IFile messageCFG = new IFile("config/messages.cfg");
            String[] lines = messageCFG.ReadAllLines();
            messageCFG.Close();

            if (lines.Length < 2) //readAll returns minimum one empty string
            {
                Logger.WriteInfo("Messages configuration appears empty - skipping...");
                return;
            }

            int mTime = -1;
            int.TryParse(lines[0], out mTime);

            if (MessageTime == -1)
                MessageTime = 60;
            else
                MessageTime = mTime;
            
            foreach (String l in lines)
            {
                if (lines[0] != l && l.Length > 1)
                    BroadcastMessages.Add(l);
            }

            messageCFG.Close();

            //if (Program.Version != Program.latestVersion && Program.latestVersion != 0)
              // messages.Add("^5IW4M Admin ^7is outdated. Please ^5update ^7to version " + Program.latestVersion);
        }

        /// <summary>
        /// Initialize the rules configuration
        /// todo: this needs to be a serialized file
        /// </summary>
        protected void InitializeRules()
        {
            Rules = new List<String>();

            IFile ruleFile = new IFile("config/rules.cfg");
            String[] _rules = ruleFile.ReadAllLines();
            ruleFile.Close();
            if (_rules.Length > 2) // readAll returns minimum one empty string
            {
                foreach (String r in _rules)
                {
                    if (r.Length > 1)
                        Rules.Add(r);
                }
            }
            else
                Logger.WriteInfo("Rules configuration appears empty - skipping...");

            ruleFile.Close();
        }

        public override string ToString()
        {
            return $"{IP}_{Port}";
        }

        // Objects
        public Interfaces.IManager Manager { get; protected set; }
        public Interfaces.ILogger Logger { get; private set; }
        public ServerConfiguration Config { get; private set; }
        public List<Map> Maps { get; protected set; }
        public List<string> Rules { get; protected set; }
        public List<Report> Reports { get; set;  }
        public List<Chat> ChatHistory { get; protected set; }
        public Queue<Helpers.PlayerHistory> PlayerHistory { get; private set; }
        public Game GameName { get; protected set; }

        // Info
        public string Hostname { get; protected set; }
        public string Website { get; protected set; }
        public string Gametype { get; protected set; }
        public Map CurrentMap { get; protected set; }
        public int ClientNum { get; protected set; }
        public int MaxClients { get; protected set; }
        public List<Player> Players { get; protected set; }
        public string Password { get; private set; }
        public bool Throttled { get; protected set; }

        // Internal
        protected string IP;
        protected int Port;
        protected string FSGame;
        protected int MessageTime;
        protected int NextMessage;
        protected int ConnectionErrors;
        protected List<string> BroadcastMessages;
        protected TimeSpan LastMessage;
        protected IFile LogFile;
        protected DateTime LastPoll;

        //Remote
        public Queue<string> commandResult = new Queue<string>();
    }
}
