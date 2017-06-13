﻿using System;
using System.Threading.Tasks;
using System.Collections.Generic;

using SharedLibrary;
using SharedLibrary.Interfaces;
using SharedLibrary.Network;
using SharedLibrary.Helpers;

namespace Plugin
{
    public class FastRestartConfig : Serialize<FastRestartConfig>
    {
        public bool Enabled;
    }

    public class CEnableFastRestart : Command
    {
        public CEnableFastRestart() : base("frenable", "enable fast restarting at the end of a map. syntax: !fre", "fre", Player.Permission.SeniorAdmin, 0, false) { }

        public override async Task ExecuteAsync(Event E)
        {
            FastRestartPlugin.ConfigManager.UpdateProperty(E.Owner, new KeyValuePair<string, object>("Enabled", true));
            await E.Origin.Tell("Fast restarting is now enabled for this server");
        }
    }

    public class CDisableFastRestart : Command
    {
        public CDisableFastRestart() : base("fredisable", "disable fast restarting at the end of a map. syntax: !frd", "frd", Player.Permission.SeniorAdmin, 0, false) { }

        public override async Task ExecuteAsync(Event E)
        {
            FastRestartPlugin.ConfigManager.UpdateProperty(E.Owner, new KeyValuePair<string, object>("Enabled", false));
            await E.Origin.Tell("Fast restarting is now disabled for this server");
        }
    }

    public class FastRestartPlugin : IPlugin
    {
        bool MatchEnded;
        DateTime MatchEndTime;

        public static ConfigurationManager ConfigManager { get; private set; }

        public string Name { get { return "Fast Restarter"; } }

        public float Version { get { return 1.0f; } }

        public string Author { get { return "RaidMax"; } }

        public async Task OnEventAsync(Event E, Server S)
        {
           if (E.Type == Event.GType.Start)
            {
                try
                {
                    await S.GetDvarAsync<int>("scr_intermission_time");
                }

                catch (SharedLibrary.Exceptions.DvarException)
                {
                    await S.SetDvarAsync("scr_intermission_time", 20);
                }
            }
        }

        public async Task OnLoadAsync(Server S)
        {
            ConfigManager = new ConfigurationManager(typeof(FastRestartPlugin));
            ConfigManager.AddConfiguration(S);

            if (ConfigManager.GetConfiguration(S).Keys.Count == 0)
                ConfigManager.AddProperty(S, new KeyValuePair<string, object>("Enabled", false));
        }

        public async Task OnTickAsync(Server S)
        {
            if ((bool)ConfigManager.GetConfiguration(S)["Enabled"] == false)
                return;

            MatchEnded = (await S.GetDvarAsync<int>("scr_gameended")).Value == 1;

            if (MatchEnded && MatchEndTime == DateTime.MinValue)
                MatchEndTime = DateTime.Now;

            // I'm pretty sure the timelength from game ended to scoreboard popup is 2000ms
            if (MatchEnded && (DateTime.Now - MatchEndTime).TotalSeconds >= ((await S.GetDvarAsync<int>("scr_intermission_time")).Value - 2))
            {
                await S.ExecuteCommandAsync("fast_restart");
                MatchEndTime = DateTime.MinValue;
            }
        }

        public Task OnUnloadAsync(Server S)
        {
            return null;
        }
    }
}
