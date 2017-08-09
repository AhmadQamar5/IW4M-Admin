﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.IO;
using System.Threading.Tasks;

using SharedLibrary;
using SharedLibrary.Interfaces;
using SharedLibrary.Commands;
using SharedLibrary.Helpers;
using SharedLibrary.Exceptions;

namespace IW4MAdmin
{
    class ApplicationManager : IManager
    {
        public List<Server> Servers { get; private set; }
        public ILogger Logger { get; private set; }
        public bool Running { get; private set; }

        static ApplicationManager Instance;
        List<AsyncStatus> TaskStatuses;
        Database ClientDatabase;
        Database AliasesDatabase;
        IPenaltyList ClientPenalties;
        List<Command> Commands;
        List<MessageToken> MessageTokens;
        Kayak.IScheduler webServiceTask;
        Thread WebThread;
#if FTP_LOG
        const int UPDATE_FREQUENCY = 15000;
#else
        const int UPDATE_FREQUENCY = 300;
#endif

        private ApplicationManager()
        {
            Logger = new Logger("Logs/IW4MAdmin.log");
            Servers = new List<Server>();
            Commands = new List<Command>();
            TaskStatuses = new List<AsyncStatus>();
            MessageTokens = new List<MessageToken>();

            ClientDatabase = new ClientsDB("Database/clients.rm");
            AliasesDatabase = new AliasesDB("Database/aliases.rm");
            ClientPenalties = new PenaltyList();
        }

        public List<Server> GetServers()
        {
            return Servers;
        }

        public List<Command> GetCommands()
        {
            return Commands;
        }

        public static ApplicationManager GetInstance()
        {
            return Instance ?? (Instance = new ApplicationManager());
        }

        public void Init()
        {
            #region CONFIG
            var Configs = Directory.EnumerateFiles("config/servers").Where(x => x.Contains(".cfg"));

            if (Configs.Count() == 0)
                ServerConfigurationGenerator.Generate();

            foreach (var file in Configs)
            {
                var Conf = ServerConfiguration.Read(file);
                var ServerInstance = new IW4MServer(this, Conf);

                Task.Run(async () =>
                {
                    try
                    {
                        await ServerInstance.Initialize();
                        Servers.Add(ServerInstance);

                        // this way we can keep track of execution time and see if problems arise.
                        var Status = new AsyncStatus(ServerInstance, UPDATE_FREQUENCY);
                        TaskStatuses.Add(Status);

                        Logger.WriteVerbose($"Now monitoring {ServerInstance.Hostname}");
                    }

                    catch (ServerException e)
                    {
                        Logger.WriteWarning($"Not monitoring server {Conf.IP}:{Conf.Port} due to uncorrectable errors");
                        if (e.GetType() == typeof(DvarException))
                            Logger.WriteError($"Could not get the dvar value for {(e as DvarException).Data["dvar_name"]} (ensure the server has a map loaded)");
                        else if (e.GetType() == typeof(NetworkException))
                            Logger.WriteError(e.Message);
                    }
                });
            }
            #endregion

            #region PLUGINS
            SharedLibrary.Plugins.PluginImporter.Load(this);

            foreach (var Plugin in SharedLibrary.Plugins.PluginImporter.ActivePlugins)
            {
                try
                {
                    Plugin.OnLoadAsync();
                }

                catch (Exception e)
                {
                    Logger.WriteError($"An error occured loading plugin {Plugin.Name}");
                    Logger.WriteDebug($"Exception: {e.Message}");
                    Logger.WriteDebug($"Stack Trace: {e.StackTrace}");
                }
            }
            #endregion

            #region COMMANDS
            if ((ClientDatabase as ClientsDB).GetOwner() == null)
                Commands.Add(new COwner("owner", "claim ownership of the server", "owner", Player.Permission.User, 0, false));

            Commands.Add(new CQuit("quit", "quit IW4MAdmin", "q", Player.Permission.Owner, 0, false));
            Commands.Add(new CKick("kick", "kick a player by name. syntax: !kick <player> <reason>.", "k", Player.Permission.Trusted, 2, true));
            Commands.Add(new CSay("say", "broadcast message to all players. syntax: !say <message>.", "s", Player.Permission.Moderator, 1, false));
            Commands.Add(new CTempBan("tempban", "temporarily ban a player for 1 hour. syntax: !tempban <player> <reason>.", "tb", Player.Permission.Moderator, 2, true));
            Commands.Add(new CBan("ban", "permanently ban a player from the server. syntax: !ban <player> <reason>", "b", Player.Permission.SeniorAdmin, 2, true));
            Commands.Add(new CWhoAmI("whoami", "give information about yourself. syntax: !whoami.", "who", Player.Permission.User, 0, false));
            Commands.Add(new CList("list", "list active clients syntax: !list.", "l", Player.Permission.Moderator, 0, false));
            Commands.Add(new CHelp("help", "list all available commands. syntax: !help.", "h", Player.Permission.User, 0, false));
            Commands.Add(new CFastRestart("fastrestart", "fast restart current map. syntax: !fastrestart.", "fr", Player.Permission.Moderator, 0, false));
            Commands.Add(new CMapRotate("maprotate", "cycle to the next map in rotation. syntax: !maprotate.", "mr", Player.Permission.Administrator, 0, false));
            Commands.Add(new CSetLevel("setlevel", "set player to specified administration level. syntax: !setlevel <player> <level>.", "sl", Player.Permission.Owner, 2, true));
            Commands.Add(new CUsage("usage", "get current application memory usage. syntax: !usage.", "us", Player.Permission.Moderator, 0, false));
            Commands.Add(new CUptime("uptime", "get current application running time. syntax: !uptime.", "up", Player.Permission.Moderator, 0, false));
            Commands.Add(new CWarn("warn", "warn player for infringing rules syntax: !warn <player> <reason>.", "w", Player.Permission.Trusted, 2, true));
            Commands.Add(new CWarnClear("warnclear", "remove all warning for a player syntax: !warnclear <player>.", "wc", Player.Permission.Trusted, 1, true));
            Commands.Add(new CUnban("unban", "unban player by database id. syntax: !unban @<id>.", "ub", Player.Permission.SeniorAdmin, 1, true));
            Commands.Add(new CListAdmins("admins", "list currently connected admins. syntax: !admins.", "a", Player.Permission.User, 0, false));
            Commands.Add(new CLoadMap("map", "change to specified map. syntax: !map", "m", Player.Permission.Administrator, 1, false));
            Commands.Add(new CFindPlayer("find", "find player in database. syntax: !find <player>", "f", Player.Permission.SeniorAdmin, 1, false));
            Commands.Add(new CListRules("rules", "list server rules. syntax: !rules", "r", Player.Permission.User, 0, false));
            Commands.Add(new CPrivateMessage("privatemessage", "send message to other player. syntax: !pm <player> <message>", "pm", Player.Permission.User, 2, true));
            Commands.Add(new CFlag("flag", "flag a suspicious player and announce to admins on join . syntax !flag <player> <reason>:", "flag", Player.Permission.Moderator, 2, true));
            Commands.Add(new CReport("report", "report a player for suspicious behaivor. syntax !report <player> <reason>", "rep", Player.Permission.User, 2, true));
            Commands.Add(new CListReports("reports", "get most recent reports. syntax !reports", "reports", Player.Permission.Moderator, 0, false));
            Commands.Add(new CMask("mask", "hide your online presence from online admin list. syntax: !mask", "mask", Player.Permission.Administrator, 0, false));
            Commands.Add(new CListBanInfo("baninfo", "get information about a ban for a player. syntax: !baninfo <player>", "bi", Player.Permission.Moderator, 1, true));
            Commands.Add(new CListAlias("alias", "get past aliases and ips of a player. syntax: !alias <player>", "known", Player.Permission.Moderator, 1, true));
            Commands.Add(new CExecuteRCON("rcon", "send rcon command to server. syntax: !rcon <command>", "rcon", Player.Permission.Owner, 1, false));
            Commands.Add(new CFindAllPlayers("findall", "find a player by their aliase(s). syntax: !findall <player>", "fa", Player.Permission.Moderator, 1, false));
            Commands.Add(new CPlugins("plugins", "view all loaded plugins. syntax: !plugins", "p", Player.Permission.Administrator, 0, false));

            foreach (Command C in SharedLibrary.Plugins.PluginImporter.ActiveCommands)
                Commands.Add(C);
            #endregion

            #region WEBSERVICE
            SharedLibrary.WebService.Init();
            webServiceTask = WebService.GetScheduler();

            WebThread = new Thread(webServiceTask.Start)
            {
                Name = "Web Thread"
            };

            WebThread.Start();
            #endregion

            Running = true;
        }
        
        public void Start()
        {
            while (Running)
            {
                foreach (var Status in TaskStatuses)
                {
                    if (Status.RequestedTask == null || Status.RequestedTask.IsCompleted)
                    {
                        Status.Update(new Task(() => (Status.Dependant as Server).ProcessUpdatesAsync(Status.GetToken())));
                        if (Status.RunAverage > 500)
                            Logger.WriteWarning($"Update task average execution is longer than desired for {(Status.Dependant as Server).GetIP()}::{(Status.Dependant as Server).GetPort()} [{Status.RunAverage}ms]");
                    }
                }

                Thread.Sleep(UPDATE_FREQUENCY);
            }
#if !DEBUG
            foreach (var S in Servers)
                S.Broadcast("^1IW4MAdmin going offline!");
#endif
            Servers.Clear();
            WebThread.Abort();
            webServiceTask.Stop();
        }

           
        public void Stop()
        {
            Running = false;
        }

        public ClientsDB GetClientDatabase()
        {
            return ClientDatabase as ClientsDB;
        }

        public AliasesDB GetAliasesDatabase()
        {
            return AliasesDatabase as AliasesDB;
        }

        public IPenaltyList GetClientPenalties()
        {
            return ClientPenalties;
        }

        public ILogger GetLogger()
        {
            return Logger;
        }

        public IList<MessageToken> GetMessageTokens()
        {
            return MessageTokens;
        }

        public IList<Player> GetActiveClients()
        {
            var ActiveClients = new List<Player>();

            foreach (var server in Servers)
                ActiveClients.AddRange(server.Players.Where(p => p != null));

            return ActiveClients;
        }
    }
}
