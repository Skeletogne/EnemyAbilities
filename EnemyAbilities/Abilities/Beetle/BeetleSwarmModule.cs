using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BepInEx.Configuration;
using EntityStates;
using EntityStates.BeetleMonster;
using JetBrains.Annotations;
using R2API;
using R2API.Utils;
using RoR2;
using RoR2.CharacterAI;
using RoR2.Navigation;
using RoR2.Skills;
using RoR2BepInExPack.GameAssetPaths.Version_1_39_0;
using UnityEngine;
using UnityEngine.AddressableAssets;
using static EnemyAbilities.Utils;

namespace EnemyAbilities.Abilities.Beetle
{
    [EnemyAbilities.ModuleInfo("Beetle Swarm", "Gives Beetles a new Special ability:\n-Summon Reinforcements: This allows lonely beetles to call out for aid, causing them to spawn beetles nearby until they have a bigger group. Also grants Beetles a new passive, Strength in Numbers, which gives them bonus stats dependent on the number of nearby Beetles, Beetle Guards and Queens. Also grants a lunge to their Headbutt attack, which can also scale with Strength in Numbers.", "Beetle", true)]
    public class BeetleSwarmModule : BaseModule
    {
        private static GameObject bodyPrefab = Addressables.LoadAssetAsync<GameObject>(RoR2_Base_Beetle.BeetleBody_prefab).WaitForCompletion();
        private static GameObject masterPrefab = Addressables.LoadAssetAsync<GameObject>(RoR2_Base_Beetle.BeetleMaster_prefab).WaitForCompletion();
        private static CharacterSpawnCard cscBeetle = Addressables.LoadAssetAsync<CharacterSpawnCard>(RoR2_Base_Beetle.cscBeetle_asset).WaitForCompletion();
        private static CharacterSpawnCard cscBeetleSulfur = Addressables.LoadAssetAsync<CharacterSpawnCard>(RoR2_Base_Beetle.cscBeetleSulfur_asset).WaitForCompletion();
        public static BuffDef beetleSwarmBuff;
        private static GameObject jumpEffect = Addressables.LoadAssetAsync<GameObject>(RoR2_Base_JumpBoost.BoostJumpEffect_prefab).WaitForCompletion();
        private static GameObject beetleGuardBodyPrefab = Addressables.LoadAssetAsync<GameObject>(RoR2_Base_BeetleGuard.BeetleGuardBody_prefab).WaitForCompletion();
        private static GameObject beetleQueenBodyPrefab = Addressables.LoadAssetAsync<GameObject>(RoR2_Base_BeetleQueen.BeetleQueen2Body_prefab).WaitForCompletion();
        private static SkillDef beetlePrimarySkill = Addressables.LoadAssetAsync<SkillDef>(RoR2_Base_Beetle.BeetleBodyHeadbutt_asset).WaitForCompletion();

        private static ConfigEntry<float> summonCooldown;
        private static ConfigEntry<float> summonMinDistance;
        private static ConfigEntry<float> summonRequiredCrowdScore;
        private static ConfigEntry<float> summonInventoryCloneChance;
        private static ConfigEntry<float> summonMaxRecursionDepth;

        private static ConfigEntry<bool> lungeEnabled;
        private static ConfigEntry<float> lungeCooldown;
        private static ConfigEntry<float> lungeBaseDistance;
        private static ConfigEntry<float> lungeBonusDistancePerBuffStack;

        private static ConfigEntry<float> buffScalingModifier;
        private static ConfigEntry<float> armorPerStack;
        private static ConfigEntry<float> movespeedPerStack;
        private static ConfigEntry<float> attackSpeedPerStack;
        private static ConfigEntry<float> damagePerStack;
        private static ConfigEntry<float> maxHealthPerStack;
        private static ConfigEntry<float> maxBuffStacks;
        private static ConfigEntry<float> maxBeetleBuffDistance;
        private static ConfigEntry<float> beetleGuardContribution;
        private static ConfigEntry<float> beetleQueenContribution;



        public override void RegisterConfig()
        {
            base.RegisterConfig();
            BindStats(bodyPrefab, [cscBeetle, cscBeetleSulfur], new StatOverrides { directorCost = 7, baseMoveSpeed = 8f });
            summonCooldown = BindFloat("Summon Cooldown", 30f, "The cooldown of the summon ability. Note that there are other requirements for this ability to be used.", 10f, 120f, 1f, PluginConfig.FormatType.Time);
            summonMinDistance = BindFloat("Summon Min Distance from Player", 40f, "The minimum distance a beetle has to be from an enemy to be able to use Summon Reinforcements.", 20f, 80f, 1f, PluginConfig.FormatType.Distance);
            summonRequiredCrowdScore = BindFloat("Summon Max Crowd Score", 10f, "Crowd score is a metric of how big a group of beetles is. Each Beetle has a crowd score determined by the sum of 100 / distance to each other beetle. When the crowd score is below the required crowd score, beetles gain the ability to use Summon Reinforcements.\nSetting this number higher will increase the number of beetles that a group must have before they no longer summon additional beetles.", 5f, 20f, 0.1f, PluginConfig.FormatType.None);
            summonInventoryCloneChance = BindFloat("Summon Inventory Clone Chance", 0f, "The chance that a beetle will clone it's inventory (including elite affixes) to it's summoned beetle.", 0f, 100f, 1f, PluginConfig.FormatType.Percentage);
            summonMaxRecursionDepth = BindFloat("Summon Max Recursion Depth", 3f, "Determines how many times beetles can chain-summon. If set to 1, summoned beetles cannot summon new beetles. If set to 2, summoned beetles CAN summon new beetles, but beetles summoned by a summoned beetle cannot.", 1f, 5f, 1f, PluginConfig.FormatType.None);
            lungeEnabled = BindBool("Lunge Enabled", true, "Grants Beetles a short lunge on their primary headbutt, which can scale with their Strength in Numbers stacks. Disable if you're already using a mod that grants beetles a lunge attack.");
            lungeCooldown = BindFloat("Lunge Cooldown", 2f, "Applies a cooldown to the Beetle's headbutt attack to prevent them from repeatedly dashing towards you. If set shorter than 1.5s, Beetles can use the ability again the moment it finishes.", 1f, 5f, 0.1f, PluginConfig.FormatType.Time);
            lungeBaseDistance = BindFloat("Lunge Base Distance", 7f, "The base distance that Beetles will attempt to lunge at their target from.", 2f, 10f, 0.1f, PluginConfig.FormatType.Distance);
            lungeBonusDistancePerBuffStack = BindFloat("Lunge Distance Per Buff Stack", 0.5f, "The additional distance that Beetles will attempt to lunge at their target from, per stack of Strength In Numbers.", 0f, 1f, 0.1f, PluginConfig.FormatType.Distance);
            buffScalingModifier = BindFloat("Strength In Numbers Scaling Modifier", 1f, "The power that the number of beetles will be raised to before applying to their boosted stats. Less than 1 causes diminishing returns for more beetles, greater than 1 causes exponential returns for more beetles. Equal to 1 scales stats linearly.", 0.75f, 1.25f, 0.01f, PluginConfig.FormatType.None);
            armorPerStack = BindFloat("Strength In Numbers Armor", 10f, "The armor per stack that Strength in Numbers grants.", 5f, 50f, 1f, PluginConfig.FormatType.None);
            movespeedPerStack = BindFloat("Strength in Numbers Move Speed Bonus", 15f, "The bonus move speed per stack that Strength in Numbers grants.", 0f, 30f, 1f, PluginConfig.FormatType.Percentage);
            attackSpeedPerStack = BindFloat("Strength In Numbers Attack Speed Bonus", 0f, "The bonus attack speed per stack that Strength in Numbers grants.", 0f, 10f, 1f, PluginConfig.FormatType.Percentage);
            damagePerStack = BindFloat("Strength in Numbers Damage Bonus", 0f, "The bonus damage per stack that Strength in Numbers grants.", 0f, 20f, 1f, PluginConfig.FormatType.Percentage);
            maxHealthPerStack = BindFloat("Strength in Numbers Health Bonus", 0f, "The bonus max health per stack that Strength in Numbers grants", 0f, 20f, 1f, PluginConfig.FormatType.Percentage);
            maxBuffStacks = BindFloat("Strength in Numbers Max Stacks", 10f, "The maximum number of stacks of Strength in Numbers that beetles can gain from nearby beetles.", 5f, 20f, 1f, PluginConfig.FormatType.None);
            maxBeetleBuffDistance = BindFloat("Strength in Numbers Buff Distance", 50f, "The distance at which beetles gain Strength in Numbers from other beetles.", 20f, 200f, 1f, PluginConfig.FormatType.Distance);
            beetleGuardContribution = BindFloat("Beetle Guard Buff Contribution", 2f, "The amount of stacks of Strength in Numbers that Beetle Guards grant to nearby Beetles.", 0f, 5f, 1f, PluginConfig.FormatType.None);
            beetleQueenContribution = BindFloat("Beetle Queen Buff Contribution", 3f, "The amount of stacks of Strength in Numbers that Beetle Queens grant to nearby Beetles.", 0f, 5f, 1f, PluginConfig.FormatType.None);
        }
        public override void Initialise()
        {
            base.Initialise();
            CreateSkillDef();
            CreateBuffDef();
            bodyPrefab.AddComponent<BeetleSwarmController>();
            bodyPrefab.AddComponent<BeetleSwarmContributor>().beetleType = BeetleSwarmContributor.BeetleType.Worker;
            if (beetleGuardContribution.Value > 0)
            {
                beetleGuardBodyPrefab.AddComponent<BeetleSwarmContributor>().beetleType = BeetleSwarmContributor.BeetleType.Guard;
            }
            if (beetleQueenContribution.Value > 0)
            {
                beetleQueenBodyPrefab.AddComponent<BeetleSwarmContributor>().beetleType = BeetleSwarmContributor.BeetleType.Queen;
            }
            RecalculateStatsAPI.GetStatCoefficients += ApplyBeetleBuffStats;
            ModifyBeetlePrimary();
            if (lungeEnabled.Value == true)
            {
                beetlePrimarySkill.activationState = ContentAddition.AddEntityState<BeetleHeadbuttLeap>(out _);
                beetlePrimarySkill.baseRechargeInterval = lungeCooldown.Value;
            }
            GlobalEventManager.onServerDamageDealt += OnDamageDealt;
        }

        private void OnDamageDealt(DamageReport report)
        {
            if (report == null || report.victimBody == null || report.damageDealt <= 0)
            {
                return;
            }
            if (report.victimBody.bodyIndex != RoR2Content.BodyPrefabs.BeetleBody.bodyIndex)
            {
                return;
            }
            BeetleSwarmController controller = report.victimBody.gameObject.GetComponent<BeetleSwarmController>();
            if (controller == null)
            {
                return;
            }
            controller.DamageReceived();
        }

        private void ModifyBeetlePrimary()
        {
            if (lungeEnabled.Value == true)
            {
                AISkillDriver headbuttDriver = masterPrefab.GetComponents<AISkillDriver>().Where(driver => driver.customName == "HeadbuttOffNodegraph").FirstOrDefault();
                if (headbuttDriver != null)
                {
                    headbuttDriver.maxDistance = lungeBaseDistance.Value;
                }
            }
        }
        private void ApplyBeetleBuffStats(CharacterBody sender, RecalculateStatsAPI.StatHookEventArgs args)
        {
            if (lungeEnabled.Value == true)
            {
                if (sender != null && sender.HasBuff(beetleSwarmBuff))
                {
                    int buffCount = sender.GetBuffCount(beetleSwarmBuff);
                    float scalingModifier = buffScalingModifier.Value;
                    float coefficient = Mathf.Pow((float)buffCount, scalingModifier);
                    args.armorAdd += armorPerStack.Value * coefficient;
                    args.moveSpeedMultAdd += (movespeedPerStack.Value / 100f) * coefficient;
                    args.attackSpeedMultAdd += (attackSpeedPerStack.Value / 100f) * coefficient;
                    args.healthMultAdd += (maxHealthPerStack.Value / 100f) * coefficient;
                    args.damageMultAdd += (damagePerStack.Value / 100f) * coefficient;
                }
            }
        }
        private void CreateSkillDef()
        {
            SkillDefData skillDefData = new SkillDefData
            {
                objectName = "BeetleBodySwarm",
                skillName = "BeetleSwarm",
                esmName = "Body",
                activationState = ContentAddition.AddEntityState<BeetleSwarm>(out _),
                cooldown = 20f,
                combatSkill = true,
                intPrio = InterruptPriority.Any
            };
            SwarmSkillDef swarm = CreateSkillDef<SwarmSkillDef>(skillDefData);
            CreateGenericSkill(bodyPrefab, swarm, "BeetleSpecialFamily", SkillSlot.Special);
            AISkillDriverData driverData = new AISkillDriverData
            {
                masterPrefab = masterPrefab,
                customName = "useSpecial",
                skillSlot = SkillSlot.Special,
                requiredSkillDef = swarm,
                requireReady = true,
                minDistance = summonMinDistance.Value,
                maxDistance = Mathf.Infinity,
                targetType = AISkillDriver.TargetType.CurrentEnemy,
                movementType = AISkillDriver.MovementType.Stop,
                aimType = AISkillDriver.AimType.MoveDirection,
                desiredIndex = 0,
                driverUpdateTimerOverride = 1f
            };
            CreateAISkillDriver(driverData);
        }
        private void CreateBuffDef()
        {
            Texture2D texture = EnemyAbilities.Instance.assetBundle.LoadAsset<Texture2D>("texStrengthInNumbersIcon");
            beetleSwarmBuff = ScriptableObject.CreateInstance<BuffDef>();
            beetleSwarmBuff.name = "BeetleSwarmBuff";
            beetleSwarmBuff.iconSprite = CreateSprite(texture);
            beetleSwarmBuff.canStack = true;
            beetleSwarmBuff.isCooldown = false;
            beetleSwarmBuff.isDOT = false;
            beetleSwarmBuff.isHidden = false;
            ContentAddition.AddBuffDef(beetleSwarmBuff);
        }
        public class BeetleHeadbuttLeap : HeadbuttState
        {
            private bool hitBoxActive;
            public static float baseHeadbuttDistance = lungeBaseDistance.Value;
            public static float headButtDistancePerBeetle = lungeBonusDistancePerBuffStack.Value;
            public override void OnEnter()
            {
                base.OnEnter();
                CharacterBody beetleBody = characterBody;
                if (beetleBody == null)
                {
                    return;
                }
                CharacterMaster master = beetleBody.master;
                if (master == null)
                {
                    return;
                }
                BaseAI ai = master.gameObject.GetComponent<BaseAI>();
                if (ai == null || ai.currentEnemy == null || ai.currentEnemy.characterBody == null)
                {
                    return;
                }
                CharacterMotor motor = beetleBody.characterMotor;
                if (motor == null)
                {
                    return;
                }
                Vector3 targetPosition = ai.currentEnemy.characterBody.corePosition;
                Vector3 beetlePosition = beetleBody.corePosition;
                float distance = Vector3.Distance(targetPosition, beetlePosition);
                float maxDistance = baseHeadbuttDistance;
                AISkillDriver attackDriver = master.GetComponents<AISkillDriver>().Where(driver => driver.customName == "HeadbuttOffNodegraph").FirstOrDefault();
                if (attackDriver != null)
                {
                    maxDistance = attackDriver.maxDistance;
                }
                if (distance < maxDistance)
                {
                    Vector3 direction = (targetPosition - beetlePosition).normalized;
                    targetPosition = beetlePosition + direction * maxDistance;
                }
                float time = 0.5f / beetleBody.attackSpeed;
                Vector3 velocity = Trajectory.CalculateInitialVelocityFromTime(beetlePosition, targetPosition, time);
                motor.velocity = velocity;
                motor.Motor.ForceUnground();
                EffectManager.SpawnEffect(jumpEffect, new EffectData { origin = beetlePosition, rotation = Util.QuaternionSafeLookRotation(velocity.normalized) }, false);
            }
            public override void FixedUpdate()
            {
                base.FixedUpdate();
                //to stop them overshooting
                if (modelAnimator != null && modelAnimator.GetFloat("Headbutt.hitBoxActive") > 0.5f && !hitBoxActive)
                {
                    hitBoxActive = true;
                    if (characterMotor != null)
                    {
                        characterMotor.velocity = Vector3.zero;
                    }
                }
            }
            public override void OnExit()
            {
                base.OnExit();
            }
            public override InterruptPriority GetMinimumInterruptPriority()
            {
                return InterruptPriority.PrioritySkill;
            }
        }
        public class SwarmSkillDef : SkillDef
        {
            public override bool IsReady([NotNull] GenericSkill skillSlot)
            {
                bool baseIsReady = base.IsReady(skillSlot);
                if (baseIsReady)
                {
                    CharacterBody body = skillSlot.characterBody;
                    if (body != null)
                    {
                        BeetleSwarmController controller = body.GetComponent<BeetleSwarmController>();
                        if (controller != null && controller.CanUseSummon)
                        {
                            return true;
                        }
                    }
                    return false;
                }
                else
                {
                    return false;
                }
            }
        }
        public class BeetleSwarm : BaseSkillState
        {
            private static float baseDurationTilSummon = 2f;
            private static float baseDurationAfterSummon = 2f;
            private float durationTilSummon;
            private float durationAfterSummon;
            private float totalDuration;
            private bool performedSummon;
            private BeetleSwarmController ownerController;
            public static float chanceToPropagateInventory = summonInventoryCloneChance.Value / 100f;
            private static GameObject summonEffect = Addressables.LoadAssetAsync<GameObject>(RoR2_Base_DeathProjectile.DeathProjectileTickEffect_prefab).WaitForCompletion();
            public override void OnEnter()
            {
                base.OnEnter();
                Util.PlayAttackSpeedSound("Play_beetle_worker_attack", this.gameObject, 0.8f);
                PlayAnimation("Body", "EmoteSurprise");
                durationTilSummon = baseDurationTilSummon / attackSpeedStat;
                durationAfterSummon = baseDurationAfterSummon / attackSpeedStat;
                totalDuration = durationTilSummon + durationAfterSummon;
                ownerController = characterBody.gameObject.GetComponent<BeetleSwarmController>();
                EffectManager.SpawnEffect(summonEffect, new EffectData { origin = characterBody.corePosition, rotation = Quaternion.identity }, true);
            }
            public override void OnExit()
            {
                base.OnExit();
            }
            public override void FixedUpdate()
            {
                base.FixedUpdate();
                if (base.fixedAge > durationTilSummon && performedSummon == false)
                {
                    performedSummon = true;
                    SummonAlly();
                }
                if (base.fixedAge > totalDuration)
                {
                    outer.SetNextStateToMain();
                }
            }
            public void SummonAlly()
            {
                MasterSummon summon = new MasterSummon();
                summon.summonerBodyObject = characterBody.gameObject;
                summon.masterPrefab = masterPrefab;
                Vector3 spawnLocation = characterBody.transform.position;
                NodeGraph nodeGraph = SceneInfo.instance.groundNodes;
                var availableNodes = nodeGraph.FindNodesInRange(characterBody.transform.position, 0f, 20f, HullMask.Human);
                if (availableNodes != null && availableNodes.Count > 0)
                {
                    int totalNodes = availableNodes.Count;
                    int randomIndex = UnityEngine.Random.RandomRangeInt(0, totalNodes - 1);
                    NodeGraph.NodeIndex selectedNode = availableNodes[randomIndex];
                    nodeGraph.GetNodePosition(selectedNode, out spawnLocation);
                }
                summon.position = spawnLocation;
                summon.rotation = characterBody.transform.rotation;
                summon.ignoreTeamMemberLimit = true;
                summon.inventoryToCopy = UnityEngine.Random.value <= chanceToPropagateInventory ? characterBody.inventory : null;
                summon.teamIndexOverride = characterBody.teamComponent.teamIndex;
                CharacterMaster master = summon.Perform();
                if (master != null)
                {
                    CharacterBody body = master.GetBody();
                    if (body != null)
                    {
                        BeetleSwarmController controller = body.gameObject.GetComponent<BeetleSwarmController>();
                        if (controller != null && ownerController != null)
                        {
                            controller.recursionDepth = ownerController.recursionDepth + 1;
                        }
                        DeathRewards ownerDeathRewards = characterBody.gameObject.GetComponent<DeathRewards>();
                        DeathRewards summonDeathRewards = body.gameObject.GetComponent<DeathRewards>();
                        uint newGoldReward = (ownerDeathRewards.goldReward / 2) + 1;
                        uint newExpReward = (ownerDeathRewards.expReward / 2) + 1;
                        summonDeathRewards.goldReward = newGoldReward;
                        summonDeathRewards.expReward += newExpReward;
                    }

                }
            }
            public override InterruptPriority GetMinimumInterruptPriority()
            {
                return InterruptPriority.Pain;
            }
        }
        public class BeetleSwarmController : MonoBehaviour
        {
            private CharacterBody characterBody;
            private HealthComponent healthComponent;
            private float checkTimer;
            private static float checkInterval = 0.5f;
            private static float maxBuffDistance = maxBeetleBuffDistance.Value;
            //higher means it needs more beetles closer to be satisfied
            private static float maxCrowdScoreToUseSpecial = summonRequiredCrowdScore.Value;
            private float crowdScore;
            public int recursionDepth;
            private BeetleSwarmContributor ownContributor;
            private AISkillDriver attackDriver;
            private bool nearEnemies;
            public bool CanUseSummon => (crowdScore < maxCrowdScoreToUseSpecial) && (recursionDepth < summonMaxRecursionDepth.Value) && (!nearEnemies) && (!attackedRecently);
            private int maxStrengthInNumbersCount = (int)maxBuffStacks.Value;
            private float attackedCountdown;
            private static float attackedCountdownDuration = 10f;
            private bool attackedRecently => attackedCountdown > 0;
            public void Start()
            {
                characterBody = GetComponent<CharacterBody>();
                ownContributor = GetComponent<BeetleSwarmContributor>();
                if (characterBody != null)
                {
                    healthComponent = characterBody.healthComponent;
                    if (characterBody.skillLocator != null && characterBody.skillLocator.special != null)
                    {
                        characterBody.skillLocator.special.RemoveAllStocks();
                        characterBody.skillLocator.special.rechargeStopwatch = UnityEngine.Random.Range(0f, 10f);
                    }
                    CharacterMaster master = characterBody.master;
                    if (master != null)
                    {
                        attackDriver = master.gameObject.GetComponents<AISkillDriver>().Where(driver => driver.customName == "HeadbuttOffNodegraph").FirstOrDefault();
                    }
                }
            }
            public void FixedUpdate()
            {
                if (attackedCountdown > 0)
                {
                    attackedCountdown -= Time.fixedDeltaTime;
                }
                checkTimer -= Time.fixedDeltaTime;
                if (checkTimer < 0)
                {
                    checkTimer += checkInterval;
                    int beetleCount = GetNearbyBeetleCount();
                    int cappedBeetleCount = Mathf.Min(beetleCount, maxStrengthInNumbersCount);
                    if (characterBody != null)
                    {
                        characterBody.SetBuffCount(beetleSwarmBuff.buffIndex, cappedBeetleCount);
                    }
                    if (attackDriver != null)
                    {
                        float driverDistance = BeetleHeadbuttLeap.baseHeadbuttDistance + BeetleHeadbuttLeap.headButtDistancePerBeetle * cappedBeetleCount;
                        attackDriver.maxDistance = driverDistance;
                    }
                    TeamComponent teamComponent = characterBody.teamComponent;
                    if (teamComponent != null)
                    {
                        TeamIndex beetleTeamIndex = teamComponent.teamIndex;
                        nearEnemies = false;
                        foreach (CharacterBody body in CharacterBody.instancesList)
                        {
                            if (body == null || body.teamComponent == null || body.teamComponent.teamIndex == TeamIndex.Neutral || body.teamComponent.teamIndex == beetleTeamIndex)
                            {
                                continue;
                            }
                            float distance = Vector3.Distance(body.corePosition, characterBody.corePosition);
                            if (distance < summonMinDistance.Value)
                            {
                                nearEnemies = true;
                                break;
                            }
                        }
                    }
                }
            }
            public int GetNearbyBeetleCount()
            {
                int beetleCount = 0;
                float newCrowdScore = 0;
                if (characterBody != null && healthComponent != null && healthComponent.alive == true)
                {
                    List<BeetleSwarmContributor> contributors = InstanceTracker.GetInstancesList<BeetleSwarmContributor>();
                    foreach (var contributor in contributors)
                    {
                        if (contributor == null)
                        {
                            continue;
                        }
                        if (contributor.characterBody == null || contributor.healthComponent == null || contributor.healthComponent.alive == false)
                        {
                            continue;
                        }
                        if (contributor == ownContributor)
                        {
                            continue;
                        }
                        int contribution = contributor.GetContribution();
                        Vector3 beetle1Position = characterBody.footPosition;
                        Vector3 beetle2Position = contributor.characterBody.footPosition;
                        float distance = Vector3.Distance(beetle1Position, beetle2Position);
                        newCrowdScore += contribution * (100f / Mathf.Max(distance, 1f));
                        if (distance > maxBuffDistance)
                        {
                            continue;
                        }
                        beetleCount += contribution;
                    }
                }
                crowdScore = newCrowdScore;
                return beetleCount;
            }
            public void DamageReceived()
            {
                attackedCountdown = attackedCountdownDuration;
            }
        }
        public class BeetleSwarmContributor : MonoBehaviour
        {
            public enum BeetleType
            {
                Worker,
                Guard,
                Queen
            }
            public BeetleType beetleType;
            public HealthComponent healthComponent;
            public CharacterBody characterBody;
            public void Start()
            {
                characterBody = GetComponent<CharacterBody>();
                if (characterBody != null)
                {
                    healthComponent = characterBody.healthComponent;
                }
            }
            public int GetContribution()
            {
                switch (beetleType)
                {
                    case BeetleType.Worker:
                        return 1;
                    case BeetleType.Guard:
                        return (int)beetleGuardContribution.Value;
                    case BeetleType.Queen:
                        return (int)beetleQueenContribution.Value;
                }
                return 0;
            }
            public void OnEnable()
            {
                InstanceTracker.Add(this);
            }
            public void OnDisable()
            {
                InstanceTracker.Remove(this);
            }
        }
    }
}
