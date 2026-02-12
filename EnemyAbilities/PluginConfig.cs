using System;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx.Configuration;
using EnemyAbilities.Abilities;
using UnityEngine;

//this whole ass thing needs an overhaul

namespace EnemyAbilities
{
    internal static class PluginConfig
    {

        public static ConfigEntry<float> bisonRockCount { get; set; }
        public static ConfigEntry<float> bisonRockTimeToTargetNonPlayer { get; set; }
        public static ConfigEntry<float> bisonRockTimeToTargetPlayer { get; set; }
        public static ConfigEntry<float> bisonRockDamageCoefficient { get; set; }
        public static ConfigEntry<float> bisonRockExplosionRadius { get; set; }
        public static ConfigEntry<float> bisonRockCooldown { get; set; }



        public static ConfigEntry<float> templarGrenadeCount { get; set; }
        public static ConfigEntry<float> templarGrenadeDamageCoefficient { get; set; }
        public static ConfigEntry<float> templarGrenadeExplosionRadius { get; set; }
        public static ConfigEntry<float> templarGrenadeCooldown { get; set; }


        public static ConfigEntry<float> pestThwompWarningDuration { get; set; }
        public static ConfigEntry<float> pestThwompRecoveryDuration { get; set; }
        public static ConfigEntry<float> pestThwompGroundedDuration { get; set; }
        public static ConfigEntry<float> pestThwompDamageCoefficient { get; set; }
        public static ConfigEntry<float> pestThwompRadius { get; set; }
        public static ConfigEntry<float> pestThwompCooldown { get; set; } 


        public static ConfigEntry<float> transporterFlingMaxRange { get; set; }
        public static ConfigEntry<float> transporterFlingTimeToTarget { get; set; }
        public static ConfigEntry<float> transporterFlingBaseDamageCoefficient { get; set; }
        public static ConfigEntry<float> transporterFlingDamageCoefficientPerUnitMass { get; set; }
        public static ConfigEntry<float> transporterFlingRadius { get; set; }
        public static ConfigEntry<bool> canFlingElites { get; set; }
        public static ConfigEntry<bool> canFlingBosses { get; set; }
        public static ConfigEntry<float> transporterTractorBeamCooldown { get; set; }


        public static ConfigEntry<float> xiCoreDamageCoefficient { get; set; }
        public static ConfigEntry<float> xiCoreWindupDuration { get; set; }
        public static ConfigEntry<float> xiCoreWaitDuration { get; set; }
        public static ConfigEntry<float> xiCoreExplosionRadius { get; set; }
        public static ConfigEntry<bool> prioritisesPlayers { get; set; }
        public static ConfigEntry<float> xiCoreCooldown { get; set; }



        public static ConfigEntry<float> vultureSwoopDamageCoefficient { get; set; }
        public static ConfigEntry<float> vultureSwoopHitBoxScale { get; set; }
        public static ConfigEntry<bool> vultureSwoopInflictsBleed { get; set; }
        public static ConfigEntry<float> vultureSwoopPredictionTime { get; set; }
        public static ConfigEntry<float> vultureSwoopStunDurationOnImpact { get; set; }
        public static ConfigEntry<float> vultureSwoopCooldown { get; set; }




        public static ConfigEntry<float> prospectorBurrowEntryDuration { get; set; }
        public static ConfigEntry<float> prospectorBurrowWaitDuration { get; set; }
        public static ConfigEntry<float> prospectorBurrowTelegraphDuration { get; set; }
        public static ConfigEntry<float> prospectorBurrowDamageCoefficient { get; set; }
        public static ConfigEntry<float> prospectorBurrowRadius { get; set; }
        public static ConfigEntry<float> prospectorBurrowBaseVelocity { get; set; }
        public static ConfigEntry<float> prospectorBurrowCooldown { get; set; }


        public static ConfigEntry<bool> grovetenderResurrectWispsOnly { get; set; }
        public static ConfigEntry<float> grovetenderResurrectMaxGhosts { get; set; }
        public static ConfigEntry<float> grovetenderResurrectMaxGravestones { get; set; }
        public static ConfigEntry<float> grovetenderResurrectCooldown { get; set; }
        public static ConfigEntry<float> grovetenderResurrectGravestoneFuse { get; set; }
        public static ConfigEntry<float> grovetenderResurrectGravestoneRadius { get; set; }
        public static ConfigEntry<float> grovetenderResurrectGravestoneDamage { get; set; }
        public static ConfigEntry<float> grovetenderResurrectGhostLifetime { get; set; }
        public static ConfigEntry<float> grovetenderResurrectHealthThreshold { get; set; }
        public static ConfigEntry<float> grovetenderResurrectArmorPerGhost { get; set; }


        public static ConfigEntry<float> apothecaryDelugeCooldown { get; set; }
        public static ConfigEntry<float> apothecaryDelugeMinDamage { get; set; }
        public static ConfigEntry<float> apothecaryDelugeMaxDamage { get; set; }
        public static ConfigEntry<float> apothecaryDelugeMinRadius { get; set; }
        public static ConfigEntry<float> apothecaryDelugeMaxRadius { get; set; }
        public static ConfigEntry<float> apothecaryDelugeHealthPercentageRequiredForFullCharge { get; set; }
        public static ConfigEntry<float> apothecaryDelugeHealthThreshold { get; set; }
        public static ConfigEntry<float> apothecaryDelugeChargeTime { get; set; }
        public static ConfigEntry<float> apothecaryDelugeTravelTime { get; set; }
        public static ConfigEntry<float> apothecaryDelugeFuseTime { get; set; }
        public static ConfigEntry<float> apothecaryDelugePlayerDetonateDamage { get; set; }
        public static ConfigEntry<float> apothecaryDelugeAoEZoneDamagePercentage { get; set; }
        public static ConfigEntry<float> apothecaryDelugeAoEZoneDuration { get; set; }


        public static ConfigEntry<float> lunarGolemLaserCooldown { get; set; }
        public static ConfigEntry<float> lunarGolemLaserCharges { get; set; }
        public static ConfigEntry<float> lunarGolemHealthThreshold { get; set; }
        public static ConfigEntry<float> lunarGolemWindupDuration { get; set; }
        public static ConfigEntry<float> lunarGolemSweepDuration { get; set; }
        public static ConfigEntry<float> lunarGolemExplosionDelay { get; set; }
        public static ConfigEntry<float> lunarGolemExplosionRadius { get; set; }
        public static ConfigEntry<float> lunarGolemDoTDamageCoefficient { get; set; }
        public static ConfigEntry<float> lunarGolemExplosionDamageCoefficient { get; set; }


        public enum FormatType
        {
            None = 0,
            Percentage = 1,
            Time = 2,
            Distance = 3,
            Speed = 4
        }

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
            bisonRockCount = cfg.BindOptionSteppedSlider("Bighorn Bison", "Rock Count", 1f, 1f, "The number of rocks spawned. Rock counts beyond 1 are distributed in an arc, 45 degrees apart.", 1f, 8f, true, FormatType.None);
            bisonRockDamageCoefficient = cfg.BindOptionSteppedSlider("Bighorn Bison", "Rock Damage Coefficient", 300f, 5f, "Percentage multiplier to the Bison's Damage to get explosion damage. Uses falloff model sweet spot", 100f, 1000f, true, FormatType.Percentage);
            bisonRockTimeToTargetNonPlayer = cfg.BindOptionSteppedSlider("Bighorn Bison", "Rock Time to Target", 1.25f, 0.01f, "Duration of time between the rock being launched by a non-player to hitting it's target. Proportional to arc height.", 0.5f, 5f, true, FormatType.Time);
            bisonRockTimeToTargetPlayer = cfg.BindOptionSteppedSlider("Bighorn Bison", "Rock Time to Target (Launched by Player)", 0.5f, 0.01f, "Duration of time between the rock being launched by a player to hitting it's target. Proportional to arc height.", 0.5f, 5f, true, FormatType.Time);
            bisonRockExplosionRadius = cfg.BindOptionSteppedSlider("Bighorn Bison", "Rock Explosion Radius", 10f, 1f, "Explosion radius of the rock", 6f, 16f, false, FormatType.Distance);
            bisonRockCooldown = cfg.BindOptionSteppedSlider("Bighorn Bison", "Rock Cooldown", 15f, 0.1f, "Cooldown before the ability can be activated again.", 5f, 30f, true, FormatType.Time);

            templarGrenadeCount = cfg.BindOptionSteppedSlider("Clay Templar", "Grenade Count", 5f, 1f, "The number of grenades the Templar fires on ability use.", 1f, 10f, true, FormatType.None);
            templarGrenadeCooldown = cfg.BindOptionSteppedSlider("Clay Templar", "Grenade Cooldoown", 10f, 0.1f, "Cooldown before the ability can activated again.", 5f, 30f, true, FormatType.Time);
            templarGrenadeDamageCoefficient = cfg.BindOptionSteppedSlider("Clay Templar", "Grenade Damage Coefficient", 100f, 5f, "Percentage multiplier to the Templar's Damage to get explosion damage. Uses falloff model sweet spot", 50f, 500f, true, FormatType.Percentage);
            templarGrenadeExplosionRadius = cfg.BindOptionSteppedSlider("Clay Templar", "Grenade Explosion Radius", 6f, 1f, "Grenade Explosion radius in metres", 1f, 10f, true, FormatType.Distance);

            pestThwompWarningDuration = cfg.BindOptionSteppedSlider("Blind Pest", "Thwomp Warning Duration", 0.85f, 0.05f, "The duration that the indicator appears before the Blind Pest begins it's attack.", 0.5f, 1.5f, true, FormatType.Time);
            pestThwompRecoveryDuration = cfg.BindOptionSteppedSlider("Blind Pest", "Thwomp Recovery Duration", 1.25f, 0.05f, "The duration that the Blind Pest is unable to select another attack for after landing", 0.5f, 1.5f, true, FormatType.Time);
            pestThwompGroundedDuration = cfg.BindOptionSteppedSlider("Blind Pest", "Thwomp Grounded Duration", 3f, 0.1f, "The duration that the Blind Pest is forced into its grounded state for, before it becomes able to fly again", 1f, 10f, true, FormatType.Time);
            pestThwompDamageCoefficient = cfg.BindOptionSteppedSlider("Blind Pest", "Thwomp Damage Coefficient", 250f, 5f, "The damage multiplier to the Blind Pest's damage to get explosion damage. Uses falloff model sweet spot", 100f, 500f, true, FormatType.Percentage);
            pestThwompRadius = cfg.BindOptionSteppedSlider("Blind Pest", "Thwomp Radius", 6f, 1f, "The explosion radius of the poison blast", 4f, 12f, true, FormatType.Distance);
            pestThwompCooldown = cfg.BindOptionSteppedSlider("Blind Pest", "Thwomp Cooldown", 8f, 0.1f, "The cooldown of the ability", 4f, 20f, true, FormatType.Time);

            transporterFlingMaxRange = cfg.BindOptionSteppedSlider("Solus Transporter", "Max Fling Range", 150f, 1f, "The maximum range at which a Solus Transporter can use it's fling ability", 70f, 200f, true, FormatType.Distance);
            transporterFlingTimeToTarget = cfg.BindOptionSteppedSlider("Solus Transporter", "Fling Time to Target", 3f, 0.1f, "The amount of time it takes a flung enemy to reach it's intended target. Higher values will yield higher arc heights.", 1f, 5f, true, FormatType.Time);
            transporterFlingRadius = cfg.BindOptionSteppedSlider("Solus Transporter", "Explosion Radius", 12f, 0.1f, "The explosion radius of a flung enemy landing.", 8f, 20f, true, FormatType.Distance);
            transporterFlingBaseDamageCoefficient = cfg.BindOptionSteppedSlider("Solus Transporter", "Fling Damage Coefficient", 300f, 5f, "The base explosion damage of a flung enemy hitting the ground.", 100f, 600f, true, FormatType.Percentage);
            transporterFlingDamageCoefficientPerUnitMass = cfg.BindOptionSteppedSlider("Solus Transporter", "Fling Damage Coefficient per Unit Mass", 0.5f, 0.01f, "The value that is multiplied by the weight of the unit, before being added to the percentage damage coefficient. (e.g. a gup weighs 500, so a base damage coefficient of 200% with a damage per unit mass of 0.5 would yield 200% + 0.5 * 500% = 450%.", 0f, 1f, true, FormatType.Percentage);
            canFlingElites = cfg.BindOption("Solus Transporter", "Can Fling Elites", true, "Allows Solus Transporters to pick up and fling elite enemies", true);
            canFlingBosses = cfg.BindOption("Solus Transporter", "Can Fling Bosses", false, "Allows Solus Transporters to pick up and fling bosses. UNTESTED - ENABLE AT YOUR OWN PERIL!", true);
            transporterTractorBeamCooldown = cfg.BindOptionSteppedSlider("Solus Transporter", "Tractor Beam Cooldown", 12f, 0.1f, "The Cooldown for the Tractor Beam (which starts after an enemy is flung)", 5f, 30f, true, FormatType.Time);

            xiCoreDamageCoefficient = cfg.BindOptionSteppedSlider("Xi Construct", "Core Damage Coefficient", 250f, 5f, "The damage coefficient of the core attack", 100f, 500f, true, FormatType.Percentage);
            xiCoreExplosionRadius = cfg.BindOptionSteppedSlider("Xi Construct", "Core Explosion Radius", 10f, 0.1f, "The damage radius of the core attack", 6f, 20f, true, FormatType.Distance);
            xiCoreWaitDuration = cfg.BindOptionSteppedSlider("Xi Construct", "Core Wait Duration", 3.5f, 0.01f, "The amount of time that the Xi Construct will wait between firing and recalling it's Core.", 1f, 6f, true, FormatType.Time);
            xiCoreWindupDuration = cfg.BindOptionSteppedSlider("Xi Construct", "Core Windup Duration", 0.75f, 0.01f, "The amount of time the Xi Construct will spin up for before firing it's core.", 0.5f, 2f, true, FormatType.Time);
            prioritisesPlayers = cfg.BindOption("Xi Construct", "Core Prioritises Players", true, "The Xi Construct is much more likely to fire it's core at players.", true);
            xiCoreCooldown = cfg.BindOptionSteppedSlider("Xi Construct", "Core Cooldown", 15f, 0.1f, "The cooldown of the core launch", 10f, 30f, true, FormatType.Time);

            vultureSwoopPredictionTime = cfg.BindOptionSteppedSlider("Alloy Vulture", "Swoop Prediction Duration", 1.5f, 0.1f, "Affects how far into the future the Alloy Vulture will predict when swooping towards the player. Increasing the value increases the duration of the attack, and slows the speed at which the Vulture travels.", 1f, 3f, true, FormatType.Time);
            vultureSwoopHitBoxScale = cfg.BindOptionSteppedSlider("Alloy Vulture", "Swoop Hitbox Scale", 100f, 1f, "Affects how large the hitbox for swoop is.", 50f, 200f, true, FormatType.Percentage);
            vultureSwoopDamageCoefficient = cfg.BindOptionSteppedSlider("Alloy Vulture", "Swoop Damage Coefficient", 125f, 5f, "The damage coefficient of the swoop attack", 100f, 300f, true, FormatType.Percentage);
            vultureSwoopStunDurationOnImpact = cfg.BindOptionSteppedSlider("Alloy Vulture", "Swoop Stun Duration on Impact", 2f, 0.1f, "How long the Alloy Vulture stuns itself for upon hitting terrain at high speed.", 0f, 4f, true, FormatType.Time);
            vultureSwoopInflictsBleed = cfg.BindOption("Alloy Vulture", "Swoop Inflicts Bleed", true, "Allows Alloy Vultures to inflict 1 stack of bleed for 3 seconds when swoop hits.", true);
            vultureSwoopCooldown = cfg.BindOptionSteppedSlider("Alloy Vulture", "Swoop Cooldown", 12f, 0.1f, "The cooldown of the swoop ability", 8f, 30f, true, FormatType.Time);

            prospectorBurrowEntryDuration = cfg.BindOptionSteppedSlider("Solus Prospector", "Burrow Entry Duration", 1f, 0.1f, "How long it takes for the Solus Prospector to burrow underground.", 0.2f, 2f, true, FormatType.Time);
            prospectorBurrowWaitDuration = cfg.BindOptionSteppedSlider("Solus Prospector", "Burrow Wait Duration", 1f, 0.1f, "How long the Solus Prospector remains burrowed without attacking", 0.2f, 2f, true, FormatType.Time);
            prospectorBurrowTelegraphDuration = cfg.BindOptionSteppedSlider("Solus Prospector", "Burrow Telegraph Duration", 1f, 0.1f, "How long the Solus Prospector telegraphs before bursting up from the ground", 0.2f, 2f, true, FormatType.Time);
            prospectorBurrowRadius = cfg.BindOptionSteppedSlider("Solus Prospector", "Burrow Radius", 6f, 1f, "The radius of the burrow attack explosion", 6f, 16f, true, FormatType.Distance);
            prospectorBurrowDamageCoefficient = cfg.BindOptionSteppedSlider("Solus Prospector", "Burrow Damage Coefficient", 300f, 5f, "The damage coefficient of the burrow attack", 100f, 500f, true, FormatType.Percentage);
            prospectorBurrowBaseVelocity = cfg.BindOptionSteppedSlider("Solus Prospector", "Burrow Upwards Velocity Modifier", 250f, 10f, "The speed multiplier at which the Prospector is ejected from the ground", 100f, 500f, true, FormatType.Percentage);
            prospectorBurrowCooldown = cfg.BindOptionSteppedSlider("Solus Prospector", "Burrow Cooldown", 15f, 0.1f, "The cooldown of the burrow", 8f, 30f, true, FormatType.Time);

            grovetenderResurrectWispsOnly = cfg.BindOption("Grovetender", "Resurrect Spawns Wisps Only", false, "Enemies killed are resurrected as wisps when the Gravestones shatter.", true);
            grovetenderResurrectGhostLifetime = cfg.BindOptionSteppedSlider("Grovetender", "Resurrect Ghost Lifetime", 30f, 1f, "The duration over which resurrected ghosts will decay from full health to zero.", 10f, 120f, true, FormatType.Time);
            grovetenderResurrectGravestoneDamage = cfg.BindOptionSteppedSlider("Grovetender", "Resurrect Gravestone Damage", 200f, 5f, "The damage coefficient of the Gravestone explosion.", 100f, 400f, true, FormatType.Percentage);
            grovetenderResurrectGravestoneFuse = cfg.BindOptionSteppedSlider("Grovetender", "Resurrect Gravestone Fuse Duration", 1.25f, 0.01f, "The time between a gravestone embedding and exploding.", 0.5f, 3f, true, FormatType.Time);
            grovetenderResurrectGravestoneRadius = cfg.BindOptionSteppedSlider("Grovetender", "Resurrect Gravestone Explosion Radius", 9f, 0.1f, "The radius of the gravestone explosion", 5f, 16f, true, FormatType.Distance);
            grovetenderResurrectHealthThreshold = cfg.BindOptionSteppedSlider("Grovetender", "Resurrect Health Threshold", 50f, 1f, "The health threshold that the Grovetender must be under to use Mass Resurrect", 25f, 100f, true, FormatType.Percentage);
            grovetenderResurrectMaxGhosts = cfg.BindOptionSteppedSlider("Grovetender", "Resurrect Max Ghosts", 8f, 1f, "The maximum amount of ghosts a Grovetender can have active at once. Gravestones beyond this limit will fire and explode, but not spawn new enemies.", 2f, 20f, true, FormatType.None);
            grovetenderResurrectMaxGravestones = cfg.BindOptionSteppedSlider("Grovetender", "Resurrect Max Gravestones", 8f, 1f, "The maximum amount of gravestones a Grovetender can have active at once. Enemies that die beyond this limit will not create gravestones.", 2f, 20f, true, FormatType.None);
            grovetenderResurrectCooldown = cfg.BindOptionSteppedSlider("Grovetender", "Resurrect Cooldown", 60f, 0.1f, "The cooldown of Mass Resurrect", 30f, 120f, true, FormatType.Time);
            grovetenderResurrectArmorPerGhost = cfg.BindOptionSteppedSlider("Grovetender", "Resurrect Armor Per Ghost", 30f, 1f, "The amount of armour that the Grovetender gains per active ghost.", 5f, 100f, true, FormatType.None);

            apothecaryDelugeMinDamage = cfg.BindOptionSteppedSlider("Clay Apothecary", "Deluge Min Damage", 100f, 5f, "", 50f, 200f, true, FormatType.Percentage);
            apothecaryDelugeMaxDamage = cfg.BindOptionSteppedSlider("Clay Apothecary", "Deluge Max Damage", 200f, 5f, "", 250f, 800f, true, FormatType.Percentage);
            apothecaryDelugeMinRadius = cfg.BindOptionSteppedSlider("Clay Apothecary", "Deluge Min Radius", 10f, 0.1f, "", 6f, 12f, true, FormatType.Distance);
            apothecaryDelugeMaxRadius = cfg.BindOptionSteppedSlider("Clay Apothecary", "Deluge Max Radius", 24f, 0.1f, "", 14f, 30f, true, FormatType.Distance);
            apothecaryDelugeAoEZoneDamagePercentage = cfg.BindOptionSteppedSlider("Clay Apothecary", "Deluge AoE Damage Percentage", 5f, 1f, "The damage of the AoE per tick as a percentage of the Detonation's damage. This will naturally increase the more the tar ball is charged.", 1f, 20f, true, FormatType.Percentage);
            apothecaryDelugeHealthPercentageRequiredForFullCharge = cfg.BindOptionSteppedSlider("Clay Apothecary", "Health Percentage for Full Charge", 25f, 1f, "", 5f, 50f, true, FormatType.Percentage);
            apothecaryDelugeHealthThreshold = cfg.BindOptionSteppedSlider("Clay Apothecary", "Deluge Health Threshold", 60f, 1f, "The health threshold the Apothecary must be under to use Tar Deluge.", 10f, 100f, true, FormatType.Percentage);
            apothecaryDelugeAoEZoneDuration = cfg.BindOptionSteppedSlider("Clay Apothecary", "Deluge Zone Duration", 20f, 0.1f, "The duration of time that the Tar Zone lingers for.", 8f, 60f, true, FormatType.Time);
            apothecaryDelugeChargeTime = cfg.BindOptionSteppedSlider("Clay Apothecary", "Deluge Charge Time", 3f, 0.1f, "The time that the Apothecary spends charging the Tar Ball before firing it.", 1f, 5f, true, FormatType.Time);
            apothecaryDelugeTravelTime = cfg.BindOptionSteppedSlider("Clay Apothecary", "Deluge Travel Time", 3f, 0.1f, "The time that the Tar Ball spends in the air before reaching it's target.", 1f, 5f, true, FormatType.Time);
            apothecaryDelugeFuseTime = cfg.BindOptionSteppedSlider("Clay Apothecary", "Deluge Fuse Time", 1f, 0.1f, "The time that the Tar Ball takes to explode after landing.", 0.2f, 2f, true, FormatType.Time);
            apothecaryDelugePlayerDetonateDamage = cfg.BindOptionSteppedSlider("Clay Apothecary", "Player Detonation Damage", 800f, 5f, "The damage coefficient of the Tar Ball if it's detonated by a player (through killing the apothecary whilst it's charging - the tar ball cannot be killed once airborne).", 350f, 1600f, true, FormatType.Percentage);
            apothecaryDelugeCooldown = cfg.BindOptionSteppedSlider("Clay Apothecary", "Deluge Cooldown", 30f, 0.1f, "The cooldown of the Tar Deluge ability", 15f, 60f, true, FormatType.Time);

            lunarGolemLaserCooldown = cfg.BindOptionSteppedSlider("Lunar Golem", "Laser Cooldown", 20f, 0.1f, "", 10f, 40f, true, FormatType.Time);
            lunarGolemLaserCharges = cfg.BindOptionSteppedSlider("Lunar Golem", "Laser Charges", 2f, 1f, "", 1f, 4f, true, FormatType.None);
            lunarGolemHealthThreshold = cfg.BindOptionSteppedSlider("Lunar Golem", "Laser Health Threshold", 90f, 1f, "", 0f, 100f, true, FormatType.Percentage);
            lunarGolemWindupDuration = cfg.BindOptionSteppedSlider("Lunar Golem", "Laser Windup Duration", 0.75f, 0.01f, "", 0.25f, 2f, true, FormatType.Time);
            lunarGolemSweepDuration = cfg.BindOptionSteppedSlider("Lunar Golem", "Laser Sweep Duration", 1f, 0.01f, "", 0.25f, 3f, true, FormatType.Time);
            lunarGolemExplosionDelay = cfg.BindOptionSteppedSlider("Lunar Golem", "Laser Explosion Delay", 2f, 0.1f, "", 0.5f, 3f, true, FormatType.Time);
            lunarGolemExplosionRadius = cfg.BindOptionSteppedSlider("Lunar Golem", "Laser Explosion Radius", 8f, 0.1f, "", 4f, 12f, true, FormatType.Distance);
            lunarGolemDoTDamageCoefficient = cfg.BindOptionSteppedSlider("Lunar Golem", "Laser DoT Damage Coefficient", 10f, 1f, "", 1f, 50f, true, FormatType.Percentage);
            lunarGolemExplosionDamageCoefficient = cfg.BindOptionSteppedSlider("Lunar Golem", "Laser Explosion Damage Coefficient", 150f, 5f, "", 100f, 300f, true, FormatType.Percentage);
        }
        //absolutely ancient plugin config, .score gave this to me in like 2024 
        public static void InitRoO()
        {
            try
            {
                RiskOfOptions.ModSettingsManager.SetModDescription("Give enemies new abilities!", EnemyAbilities.PluginGUID, EnemyAbilities.PluginName);

                var iconStream = File.ReadAllBytes(Path.Combine(EnemyAbilities.Instance.DirectoryName, "icon.png"));
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
        public static ConfigEntry<T> BindOptionSteppedSlider<T>(this ConfigFile myConfig, string section, string name, T defaultValue, float increment = 1f, string description = "", float min = 0, float max = 20, bool restartRequired = false, FormatType formatType = FormatType.None)
        {
            if (string.IsNullOrEmpty(description))
                description = name;

            description += " (Default: " + defaultValue + ")";

            if (restartRequired)
                description += " (restart required)";

            var configEntry = myConfig.Bind(section, name, defaultValue, new ConfigDescription(description, new AcceptableValueRange<float>(min, max)));

            if (EnemyAbilities.RooInstalled)
                TryRegisterOptionSteppedSlider(configEntry, increment, min, max, restartRequired, formatType);

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
        public static void TryRegisterOptionSteppedSlider<T>(ConfigEntry<T> entry, float increment, float min, float max, bool restartRequired, FormatType formatType)
        {
            if (entry is ConfigEntry<float> floatEntry)
            {
                RiskOfOptions.ModSettingsManager.AddOption(new RiskOfOptions.Options.StepSliderOption(floatEntry, new RiskOfOptions.OptionConfigs.StepSliderConfig()
                {
                    increment = increment,
                    min = min,
                    max = max,
                    FormatString = GetStepSizePrecision((decimal)increment, formatType),
                    restartRequired = restartRequired
                }));
            }
            else
            {
                Log.Warning($"Config entry {entry.Definition.Key} in section {entry.Definition.Section} with type {typeof(T).Name} " +
                    $"could not be registered in Risk Of Options using {nameof(TryRegisterOptionSteppedSlider)}.");
            }
        }

        //i (skeleton, not score) made this over a year ago
        private static string GetStepSizePrecision(decimal n, FormatType formatType)
        {
            n = Math.Abs(n);
            n -= (int)n;
            int decimalPlaces = 0;
            string stringFormat = "{0:";
            while (n > 0)
            {
                decimalPlaces++;
                n *= 10;
                n -= (int)n;
            }
            if (decimalPlaces == 0)
            {
                return stringFormat + "0}" + AppendSuffix(formatType);
            }
            if (decimalPlaces > 0)
            {
                stringFormat += "0.";
                for (int i = 0; i < decimalPlaces; i++)
                {
                    stringFormat += "0";
                }
                stringFormat += "}";
                return stringFormat + AppendSuffix(formatType);
            }
            else
            {
                Log.Error($"Could not determine string format!");
                return "{0:000}";
            }
        }
        private static string AppendSuffix(FormatType formatType)
        {
            //type 1 = percentage
            if (formatType == FormatType.Percentage)
                return "%";
            //type 2 = seconds
            if (formatType == FormatType.Time)
                return "s";
            //type 3 = metres
            if (formatType == FormatType.Distance)
                return "m";
            if (formatType == FormatType.Speed)
                return "m/s";
            else
                return "";
        }
    }
}