using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx.Configuration;
using EntityStates;
using R2API;
using RoR2;
using RoR2.CharacterAI;
using RoR2.Skills;
using UnityEngine;

namespace EnemyAbilities.Abilities
{
    //handles a lot of the unpleasant gross icky boilerplate
    public class BaseModule : MonoBehaviour
    {
        protected ConfigFile config => EnemyAbilities.Instance.Config;
        private string _section;
        protected string Section 
        {
            get
            {
                if (_section == null)
                {
                    EnemyAbilities.ModuleInfoAttribute attr = GetType().GetCustomAttribute<EnemyAbilities.ModuleInfoAttribute>();
                    _section = attr?.Section ?? GetType().Name;
                }
                return _section;
            }
        }
        protected class SkillDefData
        {
            public string objectName;
            public string skillName;
            public string esmName;
            public SerializableEntityStateType activationState;
            public float cooldown;
            public int baseMaxStock = 1;
            public int rechargeStock = 1;
            public int requiredStock = 1;
            public int stockToConsume = 1;
            public InterruptPriority intPrio = InterruptPriority.Any;
            public bool resetCdOnUse = false;
            public bool cdOnEnd = false;
            public bool cdBlocked = false;
            public bool combatSkill = true;
        }
        protected class AISkillDriverData
        {
            public GameObject masterPrefab;
            public string customName;
            public SkillSlot skillSlot;
            public float minDistance = 0f;
            public float maxDistance = float.PositiveInfinity;
            public int desiredIndex = 0;
            public float moveInputScale = 1f;
            public AISkillDriver.MovementType movementType;
            public AISkillDriver.AimType aimType;
            public AISkillDriver.TargetType targetType;
            public bool ignoreNodeGraph = false;
            public float maxHealthFraction = float.PositiveInfinity;
            public float minHealthFraction = float.NegativeInfinity;
            public float maxTargetHealthFraction = float.PositiveInfinity;
            public float minTargetHealthFraction = float.NegativeInfinity;
            public bool requireReady = false;
            public SkillDef requiredSkillDef = null;
            public bool activationRequiresAimTargetLoS = false;
            public bool activationRequiresAimConfirmation = false;
            public bool activationRequiresTargetLoS = false;
            public bool selectionRequiresAimTarget = false;
            public bool selectionRequiresOnGround = false;
            public bool selectionRequiresTargetLoS = false;
            public bool selectionRequiresTargetNonFlier = false;
            public int maxTimesSelected = -1;
            public float driverUpdateTimerOverride = -1f;
            public bool noRepeat = false;
            public AISkillDriver nextHighPriorityOverride = null;
            public bool shouldSprint = false;
            public float aimVectorMaxSpeedOverride = -1f;
        }
        public class StatOverrides
        {
            public float? baseMaxHealth;
            public float? baseDamage;
            public float? baseMoveSpeed;
            public float? baseAcceleration;
            public float? baseArmor;
            public float? directorCost;
        }

        internal ConfigEntry<float> baseMaxHealthCfg;
        internal ConfigEntry<float> baseDamageCfg;
        internal ConfigEntry<float> baseMoveSpeedCfg;
        internal ConfigEntry<float> baseAccelerationCfg;
        internal ConfigEntry<float> baseArmorCfg;
        internal ConfigEntry<float> costCfg;

        protected GameObject cachedBodyPrefab;
        protected List<CharacterSpawnCard> cachedSpawnCards;

        protected void BindStats(GameObject prefab, List<CharacterSpawnCard> spawnCards = null, StatOverrides overrides = null)
        {
            cachedBodyPrefab = prefab;
            cachedSpawnCards = spawnCards;
            CharacterBody body = prefab.GetComponent<CharacterBody>();
            if (body == null)
            {
                Log.Error($"BodyPrefab has no CharacterBody!");
                return;
            }
            string bodyName = Language.GetString(body.baseNameToken);
            overrides = overrides ?? new StatOverrides();
            baseMaxHealthCfg = BindFloat("Base Max Health", overrides.baseMaxHealth ?? body.baseMaxHealth, $"Base Max Health of this enemy.\nValue without this mod is {body.baseMaxHealth}.{(overrides.baseMaxHealth.HasValue ? $"\n<style=cShrine>This mod rebalances this stat to {overrides.baseMaxHealth} by default.</style>" : "")}", body.baseMaxHealth / 5f, body.baseMaxHealth * 5f, 1f);
            baseDamageCfg = BindFloat("Base Damage", overrides.baseDamage ?? body.baseDamage, $"Base Damage of this enemy.\nValue without this mod is {body.baseDamage}.{(overrides.baseDamage.HasValue ? $"\n<style=cShrine>This mod rebalances this stat to {overrides.baseDamage} by default.</style>" : "")}", body.baseDamage / 5f, body.baseDamage * 5f, 0.1f);
            if (body.baseMoveSpeed > 0)
            {
                baseMoveSpeedCfg = BindFloat("Base Move Speed", overrides.baseMoveSpeed ?? body.baseMoveSpeed, $"Base Move Speed of this enemy.\nValue without this mod is {body.baseMoveSpeed}.{(overrides.baseMoveSpeed.HasValue ? $"\n<style=cShrine>This mod rebalances this stat to {overrides.baseMoveSpeed} by default.</style>" : "")}", body.baseMoveSpeed / 5f, body.baseMoveSpeed * 5f, 0.1f);
            }
            if (body.baseAcceleration > 0)
            {
                baseAccelerationCfg = BindFloat("Base Acceleration", overrides.baseAcceleration ?? body.baseAcceleration, $"Base Acceleration of this enemy.\nValue without this mod is {body.baseAcceleration}.{(overrides.baseAcceleration.HasValue ? $"\n<style=cShrine>This mod rebalances this stat to {overrides.baseAcceleration} by default.</style>" : "")}", body.baseAcceleration / 5f, body.baseAcceleration * 5f, 0.1f);
            }
            baseArmorCfg = BindFloat("Base Armor", overrides.baseArmor ?? body.baseArmor, $"Base Armor of this enemy.\nValue without this mod is {body.baseArmor}.{(overrides.baseArmor.HasValue ? $"\n<style=cShrine>This mod rebalances this stat to {overrides.baseArmor} by default.</style>" : "")}", 0, Mathf.Max(100, body.baseArmor), 1f);
            if (spawnCards != null)
            {
                costCfg = BindFloat("Director Cost", overrides.directorCost ?? spawnCards[0].directorCreditCost, $"The amount of credits required for the director to spawn this enemy.\nValue without this mod is {spawnCards[0].directorCreditCost}.{(overrides.directorCost.HasValue ? $"\n<style=cShrine>This mod rebalances this stat to {overrides.directorCost} by default.</style>" : "")}", spawnCards[0].directorCreditCost / 5f, spawnCards[0].directorCreditCost * 5f, 1f);
            }
        }
        protected void ApplyStats()
        {
            if (cachedBodyPrefab == null)
            {
                Log.Error($"CachedBodyPrefab is null! BindStats has to have been run already before ApplyStats is called!!!");
                return;
            }
            CharacterBody body = cachedBodyPrefab.GetComponent<CharacterBody>();
            if (body == null)
            {
                return;
            }
            string bodyName = Language.GetString(body.baseNameToken);
            body.baseMaxHealth = baseMaxHealthCfg.Value;
            body.levelMaxHealth = baseMaxHealthCfg.Value * 0.3f;
            body.baseDamage = baseDamageCfg.Value;
            body.levelDamage = baseDamageCfg.Value * 0.2f;
            if (baseMoveSpeedCfg != null)
            {
                body.baseMoveSpeed = baseMoveSpeedCfg.Value;
            }
            if (baseAccelerationCfg != null)
            {
                body.baseAcceleration = baseAccelerationCfg.Value;
            }
            if (baseArmorCfg != null)
            {
                body.baseArmor = baseArmorCfg.Value;
            }
            if (cachedSpawnCards != null && costCfg != null)
            {
                for (int i = 0; i < cachedSpawnCards.Count; i++)
                {
                    CharacterSpawnCard card = cachedSpawnCards[i];
                    card.directorCreditCost = (int)costCfg.Value;

                }
            }
        }
        public virtual void Awake()
        {
            RegisterConfig();
            if (IsModuleEnabled())
            {
                Initialise();
            }
        }
        private bool IsModuleEnabled()
        {
            if (EnemyAbilities.Instance.configEntries.TryGetValue(GetType(), out ConfigEntry<bool> entry))
            {
                return entry.Value;
            }
            return false;
        }
        protected ConfigEntry<float> BindFloat(string name, float defaultValue, string desc, float min, float max, float step = 0.1f, PluginConfig.FormatType format = PluginConfig.FormatType.None)
        {
            return config.BindOptionSteppedSlider(Section, name, defaultValue, step, desc, min, max, true, format);
        }
        protected ConfigEntry<bool> BindBool(string name, bool defaultValue, string desc)
        {
            return config.BindOption(Section, name, defaultValue, desc, true);
        }
        public virtual void RegisterConfig()
        {
            //stuff overwrites this
        }
        public virtual void Initialise()
        {
            ApplyStats();
            //stuff also overwrites this
        }
        protected T CreateSkillDef<T>(SkillDefData data) where T : SkillDef
        {
            T skillDef = ScriptableObject.CreateInstance<T>();
            (skillDef as ScriptableObject).name = data.objectName;
            skillDef.skillName = data.skillName;
            skillDef.activationStateMachineName = data.esmName;
            skillDef.activationState = data.activationState;
            skillDef.baseRechargeInterval = data.cooldown;
            skillDef.baseMaxStock = data.baseMaxStock;
            skillDef.rechargeStock = data.rechargeStock;
            skillDef.requiredStock = data.requiredStock;
            skillDef.stockToConsume = data.stockToConsume;
            skillDef.interruptPriority = data.intPrio;
            skillDef.resetCooldownTimerOnUse = data.resetCdOnUse;
            skillDef.beginSkillCooldownOnSkillEnd = data.cdOnEnd;
            skillDef.isCooldownBlockedUntilManuallyReset = data.cdBlocked;
            skillDef.isCombatSkill = data.combatSkill;
            ContentAddition.AddSkillDef(skillDef);
            return skillDef;
        }
        protected GenericSkill CreateGenericSkill(GameObject bodyPrefab, string skillName, string familyName, SkillDef skillDef, SkillSlot slot)
        {
            SkillFamily family = ScriptableObject.CreateInstance<SkillFamily>();
            (family as ScriptableObject).name = familyName;
            family.variants = [new SkillFamily.Variant() { skillDef = skillDef }];

            GenericSkill skill = bodyPrefab.AddComponent<GenericSkill>();
            skill.skillName = skillName;
            skill._skillFamily = family;

            SkillLocator locator = bodyPrefab.GetComponent<SkillLocator>();
            switch (slot)
            {
                case SkillSlot.None:
                    Log.Error($"SkillSlot.None detected in AddGenericSkill!"); break;
                case SkillSlot.Primary:
                    locator.primary = skill; break;
                case SkillSlot.Secondary:
                    locator.secondary = skill; break;
                case SkillSlot.Utility:
                    locator.utility = skill; break;
                case SkillSlot.Special:
                    locator.special = skill; break;
            }
            ContentAddition.AddSkillFamily(family);
            return skill;
        }
        protected EntityStateMachine CreateEntityStateMachine(GameObject bodyPrefab, string name, Type initialState = null, Type mainState = null)
        {
            EntityStateMachine esm = bodyPrefab.AddComponent<EntityStateMachine>();
            esm.customName = name;
            esm.initialStateType = new SerializableEntityStateType(initialState ?? typeof(Idle));
            esm.mainStateType = new SerializableEntityStateType(mainState ?? typeof(Idle));
            return esm;
        }
        protected AISkillDriver CreateAISkillDriver(AISkillDriverData data)
        {
            if (data == null || data.masterPrefab == null)
            {
                Log.Error($"Could not create AISkillDriver");
                return null;
            }
            AISkillDriver driver = data.masterPrefab.AddComponent<AISkillDriver>();
            driver.customName = data.customName;
            driver.skillSlot = data.skillSlot;
            driver.minDistance = data.minDistance;
            driver.maxDistance = data.maxDistance;
            driver.moveInputScale = data.moveInputScale;
            driver.movementType = data.movementType;
            driver.aimType = data.aimType;
            driver.moveTargetType = data.targetType;
            driver.ignoreNodeGraph = data.ignoreNodeGraph;
            driver.maxUserHealthFraction = data.maxHealthFraction;
            driver.minUserHealthFraction = data.minHealthFraction;
            driver.maxTargetHealthFraction = data.maxTargetHealthFraction;
            driver.minTargetHealthFraction = data.minTargetHealthFraction;
            driver.requireSkillReady = data.requireReady;
            driver.requiredSkill = data.requiredSkillDef;
            driver.activationRequiresAimConfirmation = data.activationRequiresAimConfirmation;
            driver.activationRequiresAimTargetLoS = data.activationRequiresAimTargetLoS;
            driver.activationRequiresTargetLoS = data.activationRequiresTargetLoS;
            driver.selectionRequiresAimTarget = data.selectionRequiresAimTarget;
            driver.selectionRequiresOnGround = data.selectionRequiresOnGround;
            driver.selectionRequiresTargetLoS = data.selectionRequiresTargetLoS;
            driver.selectionRequiresTargetNonFlier = data.selectionRequiresTargetNonFlier;
            driver.maxTimesSelected = data.maxTimesSelected;
            driver.driverUpdateTimerOverride = data.driverUpdateTimerOverride;
            driver.noRepeat = data.noRepeat;
            driver.nextHighPriorityOverride = data.nextHighPriorityOverride;
            driver.shouldSprint = data.shouldSprint;
            driver.aimVectorMaxSpeedOverride = data.aimVectorMaxSpeedOverride;
            data.masterPrefab.ReorderSkillDrivers(driver, data.desiredIndex);
            return driver;
        }
    }
}