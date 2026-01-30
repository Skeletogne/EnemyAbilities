using System;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;

//the ghost of boilerplate still lingers here

//update checklist
//update version number in BOTH manifest.json AND EnemyAbilities.cs
//add any dependencies if new ones have appeared
//update README.md to include changelog
//make sure git repository is up-to-date

//todo

namespace EnemyAbilities
{

    [BepInDependency("com.rune580.riskofoptions", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]

    public class EnemyAbilities : BaseUnityPlugin
    {
        public const string PluginGUID = PluginAuthor + "." + PluginName;
        public const string PluginAuthor = "Skeletogne";
        public const string PluginName = "EnemyAbilities";
        public const string PluginVersion = "1.3.0";

        internal static bool RooInstalled => Chainloader.PluginInfos.ContainsKey("com.rune580.riskofoptions");
        public static EnemyAbilities Instance { get; private set; }
        internal string DirectoryName => System.IO.Path.GetDirectoryName(Info.Location);
        public Dictionary<Type, ConfigEntry<bool>> configEntries = new Dictionary<Type,ConfigEntry<bool>>();

        public class ModuleInfoAttribute : Attribute
        {
            public string ModuleName { get; }
            public string Description { get; }
            public string Section { get; }
            public bool RequiresRestart { get; }
            public ModuleInfoAttribute(string moduleName, string description, string section, bool requiresRestart)
            {
                ModuleName = moduleName;
                Description = description;
                Section = section;
                RequiresRestart = requiresRestart;
            }
        }
        public void Awake()
        {
            Instance = this;
            Log.Init(Logger);
            PluginConfig.Init(Config);

            foreach(var kvp in configEntries)
            {
                ConfigEntry<bool> entry = kvp.Value;
                Type type = kvp.Key;
                if (entry.Value == true)
                {
                    Instance.gameObject.AddComponent(type);
                }
            }

        }
    }
}

