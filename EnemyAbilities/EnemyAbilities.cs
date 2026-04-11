using System;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using RoR2BepInExPack.GameAssetPaths.Version_1_39_0;
using UnityEngine;
using UnityEngine.AddressableAssets;

//the ghost of boilerplate still lingers here

//update checklist
//update version number in BOTH manifest.json AND EnemyAbilities.cs
//add any dependencies if new ones have appeared
//update README.md and CHANGELOG.md
//make sure git repository is up-to-date
//MAKE SURE THE DLL IS IN THE PLUGINS FOLDER WHEN MAKING THE MOD BUILD
//Make sure you include any asset bundles in the mod build!
//Make sure you include any sound banks as well!

namespace EnemyAbilities
{

    [BepInDependency("com.rune580.riskofoptions", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]

    public class EnemyAbilities : BaseUnityPlugin
    {
        public const string PluginGUID = PluginAuthor + "." + PluginName;
        public const string PluginAuthor = "Skeletogne";
        public const string PluginName = "EnemyAbilities";
        public const string PluginVersion = "1.13.0";

        internal static bool RooInstalled => Chainloader.PluginInfos.ContainsKey("com.rune580.riskofoptions");
        public static EnemyAbilities Instance { get; private set; }
        internal string DirectoryName => System.IO.Path.GetDirectoryName(Info.Location);
        public Dictionary<Type, ConfigEntry<bool>> configEntries = new Dictionary<Type,ConfigEntry<bool>>();

        public AssetBundle assetBundle;

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
            assetBundle = AssetBundle.LoadFromFile(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Info.Location), "anglereye"));

            Shader realShader = Addressables.LoadAssetAsync<Shader>(RoR2_Base_Shaders.HGStandard_shader).WaitForCompletion();

            foreach (Material mat in assetBundle.LoadAllAssets<Material>())
            {
                mat.shader = realShader;
            }

            Instance = this;
            Log.Init(Logger);
            Utils.Init();
            PluginConfig.Init(Config);
            foreach(var kvp in configEntries)
            {
                Type type = kvp.Key;
                Instance.gameObject.AddComponent(type);
            }
        }
    }
}

