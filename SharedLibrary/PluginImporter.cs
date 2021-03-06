﻿using System;
using System.IO;
using System.Collections.Generic;
using System.Reflection;
using SharedLibrary.Interfaces;

namespace SharedLibrary.Plugins
{
    public class PluginImporter
    {
        public static List<Command> ActiveCommands = new List<Command>();
        public static List<IPlugin> ActivePlugins = new List<IPlugin>();

        public static bool Load(IManager Manager)
        {
            string[] dllFileNames = Directory.GetFiles(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location) + "\\plugins", "*.dll");

            if (dllFileNames.Length == 0)
            {
                Manager.GetLogger().WriteDebug("No plugins found to load");
                return true;
            }

            ICollection<Assembly> assemblies = new List<Assembly>(dllFileNames.Length);
            foreach (string dllFile in dllFileNames)
            {
               // byte[] rawDLL = File.ReadAllBytes(dllFile);
                //Assembly assembly = Assembly.Load(rawDLL);
                assemblies.Add(Assembly.LoadFrom(dllFile));
            }

            int LoadedPlugins = 0;
            int LoadedCommands = 0;
            foreach (Assembly Plugin in assemblies)
            {
                if (Plugin != null)
                {
                    Type[] types = Plugin.GetTypes();
                    foreach (Type assemblyType in types)
                    {
                        if (assemblyType.IsClass && assemblyType.BaseType.Name == "Command")
                        {
                            Object commandObject = Activator.CreateInstance(assemblyType);
                            Command newCommand = (Command)commandObject;
                            ActiveCommands.Add(newCommand);
                            Manager.GetLogger().WriteDebug("Registered command \"" + newCommand.Name + "\"");
                            LoadedCommands++;
                            continue;
                        }

                        try
                        {
                            if (assemblyType.GetInterface("IPlugin", false) == null)
                                continue;

                            Object notifyObject = Activator.CreateInstance(assemblyType);
                            IPlugin newNotify = (IPlugin)notifyObject;
                            if (ActivePlugins.Find(x => x.Name == newNotify.Name) == null)
                            {
                                ActivePlugins.Add(newNotify);
                                Manager.GetLogger().WriteDebug($"Loaded plugin \"{ newNotify.Name }\" [{newNotify.Version}]");
                                LoadedPlugins++;
                            }
                        }

                        catch (Exception E)
                        {
                            Manager.GetLogger().WriteWarning($"Could not load plugin {Plugin.Location} - {E.Message}");
                        }
                    }
                }
            }

            Manager.GetLogger().WriteInfo($"Loaded {LoadedPlugins} plugins and registered {LoadedCommands} commands.");
            return true;
        }
    }
}
