using System;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx.Configuration;
using EnemyAbilities.Abilities;
using UnityEngine;

namespace EnemyAbilities
{
    internal static class PluginConfig
    {
        public static void Init(ConfigFile cfg)
        {
            if (EnemyAbilities.RooInstalled)
                InitRoO();

            Assembly assembly = Assembly.GetExecutingAssembly();
            Type[] types = assembly.GetTypes().Where(type => type.IsClass && !type.IsAbstract && type.IsSubclassOf(typeof(BaseModule))).ToArray();
            foreach (Type type in types)
            {
                EnemyAbilities.ModuleInfoAttribute moduleInfo = type.GetCustomAttribute<EnemyAbilities.ModuleInfoAttribute>();
                if (moduleInfo != null)
                {
                    ConfigEntry<bool> configEntry = cfg.BindOption(
                        $"{moduleInfo.Section}",
                        $"{moduleInfo.ModuleName}",
                        true,
                        $"{moduleInfo.Description}",
                        moduleInfo.RequiresRestart);
                    EnemyAbilities.Instance.configEntries.Add(type, configEntry);
                }
                else
                {
                    Log.Error($"ModuleInfo for {type.FullName} does not exist! No PluginConfig entry has been created.");
                }
            }
        }

        //absolutely ancient plugin config, .score gave this to me in like 2024 

        public static void InitRoO()
        {
            try
            {
                RiskOfOptions.ModSettingsManager.SetModDescription("Give enemies new abilities!", EnemyAbilities.PluginGUID, EnemyAbilities.PluginName);

                var iconStream = File.ReadAllBytes(Path.Combine(EnemyAbilities.Instance.DirectoryName, "enemyAbilitiesIcon.png"));
                var tex = new Texture2D(256, 256);
                tex.LoadImage(iconStream);
                var icon = Sprite.Create(tex, new Rect(0, 0, 256, 256), new Vector2(0.5f, 0.5f));

                RiskOfOptions.ModSettingsManager.SetModIcon(icon);
            }
            catch (Exception e)
            {
                Log.Debug(e.ToString());
            }
        }
        public static ConfigEntry<T> BindOption<T>(this ConfigFile myConfig, string section, string name, T defaultValue, string description = "", bool restartRequired = false)
        {
            if (defaultValue is int or float && !typeof(T).IsEnum)
            {
#if DEBUG
                Log.Warning($"Config entry {name} in section {section} is a numeric {typeof(T).Name} type, " +
                    $"but has been registered without using {nameof(BindOptionSlider)}. " +
                    $"Lower and upper bounds will be set to the defaults [0, 20]. Was this intentional?");
#endif
                return myConfig.BindOptionSlider(section, name, defaultValue, description, 0, 20, restartRequired);
            }
            if (string.IsNullOrEmpty(description))
                description = name;

            if (restartRequired)
                description += " (restart required)";

            AcceptableValueBase range = null;
            if (typeof(T).IsEnum)
                range = new AcceptableValueList<string>(Enum.GetNames(typeof(T)));

            var configEntry = myConfig.Bind(section, name, defaultValue, new ConfigDescription(description, range));

            if (EnemyAbilities.RooInstalled)
                TryRegisterOption(configEntry, restartRequired);

            return configEntry;
        }
        public static ConfigEntry<T> BindOptionSlider<T>(this ConfigFile myConfig, string section, string name, T defaultValue, string description = "", float min = 0, float max = 20, bool restartRequired = false)
        {
            if (!(defaultValue is int or float && !typeof(T).IsEnum))
            {
                Log.Warning($"Config entry {name} in section {section} is a not a numeric {typeof(T).Name} type, " +
                    $"but has been registered as a slider option using {nameof(BindOptionSlider)}. Was this intentional?");
                return myConfig.BindOption(section, name, defaultValue, description, restartRequired);
            }

            if (string.IsNullOrEmpty(description))
                description = name;

            description += " (Default: " + defaultValue + ")";

            if (restartRequired)
                description += " (restart required)";

            AcceptableValueBase range = typeof(T) == typeof(int)
                ? new AcceptableValueRange<int>((int)min, (int)max)
                : new AcceptableValueRange<float>(min, max);

            var configEntry = myConfig.Bind(section, name, defaultValue, new ConfigDescription(description, range));

            if (EnemyAbilities.RooInstalled)
                TryRegisterOptionSlider(configEntry, min, max, restartRequired);

            return configEntry;
        }
        public static ConfigEntry<T> BindOptionSteppedSlider<T>(this ConfigFile myConfig, string section, string name, T defaultValue, float increment = 1f, string description = "", float min = 0, float max = 20, bool restartRequired = false)
        {
            if (string.IsNullOrEmpty(description))
                description = name;

            description += " (Default: " + defaultValue + ")";

            if (restartRequired)
                description += " (restart required)";

            var configEntry = myConfig.Bind(section, name, defaultValue, new ConfigDescription(description, new AcceptableValueRange<float>(min, max)));

            if (EnemyAbilities.RooInstalled)
                TryRegisterOptionSteppedSlider(configEntry, increment, min, max, restartRequired);

            return configEntry;
        }
        public static void TryRegisterOption<T>(ConfigEntry<T> entry, bool restartRequired)
        {
            if (entry is ConfigEntry<string> stringEntry)
            {
                RiskOfOptions.ModSettingsManager.AddOption(new RiskOfOptions.Options.StringInputFieldOption(stringEntry, new RiskOfOptions.OptionConfigs.InputFieldConfig()
                {
                    submitOn = RiskOfOptions.OptionConfigs.InputFieldConfig.SubmitEnum.OnExitOrSubmit,
                    restartRequired = restartRequired
                }));
            }
            else if (entry is ConfigEntry<bool> boolEntry)
            {
                RiskOfOptions.ModSettingsManager.AddOption(new RiskOfOptions.Options.CheckBoxOption(boolEntry, restartRequired));
            }
            else if (entry is ConfigEntry<KeyboardShortcut> shortCutEntry)
            {
                RiskOfOptions.ModSettingsManager.AddOption(new RiskOfOptions.Options.KeyBindOption(shortCutEntry, restartRequired));
            }
            else if (typeof(T).IsEnum)
            {
                RiskOfOptions.ModSettingsManager.AddOption(new RiskOfOptions.Options.ChoiceOption(entry, restartRequired));
            }
            else
            {
                Log.Warning($"Config entry {entry.Definition.Key} in section {entry.Definition.Section} with type {typeof(T).Name} " +
                    $"could not be registered in Risk Of Options using {nameof(TryRegisterOption)}.");
            }
        }
        public static void TryRegisterOptionSlider<T>(ConfigEntry<T> entry, float min, float max, bool restartRequired)
        {
            if (entry is ConfigEntry<int> intEntry)
            {
                RiskOfOptions.ModSettingsManager.AddOption(new RiskOfOptions.Options.IntSliderOption(intEntry, new RiskOfOptions.OptionConfigs.IntSliderConfig()
                {
                    min = (int)min,
                    max = (int)max,
                    formatString = "{0:0}",
                    restartRequired = restartRequired
                }));
            }
            else if (entry is ConfigEntry<float> floatEntry)
            {
                RiskOfOptions.ModSettingsManager.AddOption(new RiskOfOptions.Options.SliderOption(floatEntry, new RiskOfOptions.OptionConfigs.SliderConfig()
                {
                    min = min,
                    max = max,
                    FormatString = "{0:0.000}",
                    restartRequired = restartRequired
                }));
            }
            else
            {
                Log.Warning($"Config entry {entry.Definition.Key} in section {entry.Definition.Section} with type {typeof(T).Name} " +
                    $"could not be registered in Risk Of Options using {nameof(TryRegisterOptionSlider)}.");
            }
        }
        public static void TryRegisterOptionSteppedSlider<T>(ConfigEntry<T> entry, float increment, float min, float max, bool restartRequired)
        {
            if (entry is ConfigEntry<float> floatEntry)
            {
                RiskOfOptions.ModSettingsManager.AddOption(new RiskOfOptions.Options.StepSliderOption(floatEntry, new RiskOfOptions.OptionConfigs.StepSliderConfig()
                {
                    increment = increment,
                    min = min,
                    max = max,
                    FormatString = "{0:0}",
                    restartRequired = restartRequired
                }));
            }
            else
            {
                Log.Warning($"Config entry {entry.Definition.Key} in section {entry.Definition.Section} with type {typeof(T).Name} " +
                    $"could not be registered in Risk Of Options using {nameof(TryRegisterOptionSteppedSlider)}.");
            }
        }
    }
}