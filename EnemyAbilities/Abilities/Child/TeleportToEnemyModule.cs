using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BepInEx.Configuration;
using EntityStates;
using JetBrains.Annotations;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using R2API;
using RoR2;
using RoR2.Navigation;
using RoR2.Skills;
using RoR2BepInExPack.GameAssetPaths.Version_1_39_0;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.UI;

namespace EnemyAbilities.Abilities.Child
{
    [EnemyAbilities.ModuleInfo("Spatial Mischief", "Charges up a teleport that takes players with it, teleporting to the most dangerous nearby enemy the Child can find, prioritising Parents.", "Child", true)]
    public class TeleportToEnemyModule : BaseModule
    {
        private static GameObject bodyPrefab = Addressables.LoadAssetAsync<GameObject>(RoR2_DLC2_Child.ChildBody_prefab).WaitForCompletion();
        private static GameObject masterPrefab = Addressables.LoadAssetAsync<GameObject>(RoR2_DLC2_Child.ChildMaster_prefab).WaitForCompletion();
        private static CharacterSpawnCard cscChild = Addressables.LoadAssetAsync<CharacterSpawnCard>(RoR2_DLC2_Child.cscChild_asset).WaitForCompletion();
        private static GameObject indicatorPrefab = Addressables.LoadAssetAsync<GameObject>(RoR2_Base_Common.TeamAreaIndicator__FullSphere_prefab).WaitForCompletion().InstantiateClone("childIndicatorPrefab");
        private static Texture2D childIndicatorRamp = Addressables.LoadAssetAsync<Texture2D>(RoR2_Base_Common_ColorRamps.texRampParentTeleport_png).WaitForCompletion();
        private static Material areaIndicatorMaterialMonster;

        private static ConfigEntry<float> mischiefCooldown;
        private static ConfigEntry<float> mischiefUseRange;
        private static ConfigEntry<float> mischiefExplosionRadius;
        private static ConfigEntry<float> mischiefTeleportRange;
        private static ConfigEntry<float> mischiefDamageCoeff;
        private static ConfigEntry<float> mischiefSlowDuration;
        private static ConfigEntry<float> mischiefChargeDuration;
        private static ConfigEntry<bool> mischiefParentsOnly;
        public override void RegisterConfig()
        {
            base.RegisterConfig();
            mischiefCooldown = BindFloat("Mischief Cooldown", 12f, "Cooldown of the ability", 6f, 30f, 1f, PluginConfig.FormatType.Time);
            mischiefUseRange = BindFloat("Mischief Use Range", 14f, "The range at which a child must be from its target to use Spatial Mischief.", 8f, 20f, 1f, PluginConfig.FormatType.Distance);
            mischiefExplosionRadius = BindFloat("Mischief Radius", 12f, "The explosion radius of the attack that which, if caught in, players will be teleported.", 6f, 20f, 1f, PluginConfig.FormatType.Distance);
            mischiefTeleportRange = BindFloat("Mischief Teleport Range", 100f, "The maximum distance that a child can teleport an affected target.", 50f, 500f, 10f, PluginConfig.FormatType.Distance);
            mischiefDamageCoeff = BindFloat("Mischief Damage Coefficient", 80f, "The damage coefficient of the blast attack that happens just before teleportation.", 0f, 200f, 5f, PluginConfig.FormatType.Percentage);
            mischiefSlowDuration = BindFloat("Mischief Slow Duration", 1.5f, "The duration of the slow applied by the blast attack.", 0f, 5f, 0.1f, PluginConfig.FormatType.Time);
            mischiefChargeDuration = BindFloat("Mischief Charge Duration", 2f, "The time it takes for a child to channel the ability.", 1f, 4f, 0.1f, PluginConfig.FormatType.Time);
            mischiefParentsOnly = BindBool("Mischief Parents Only", false, "Spatial Mischief will only be used if the child can teleport the player to a nearby Parent.");
            BindStats(bodyPrefab, [cscChild]);
        }
        public override void Initialise()
        {
            base.Initialise();
            CreateSkill();
            SetUpIndicatorPrefab();
            bodyPrefab.AddComponent<ChildParentDetector>();
            IL.EntityStates.ChildMonster.FrolicAway.TeleportAway += OverwriteTeleportLocation;
        }
        private void OverwriteTeleportLocation(ILContext il)
        {
            ILCursor c1 = new ILCursor(il);
            if (c1.TryGotoNext(
                x => x.MatchLdloc(1),
                x => x.MatchCallOrCallvirt(typeof(TeleportHelper), nameof(TeleportHelper.TeleportBody))
            ))
            {
                c1.Index++;
                c1.Emit(OpCodes.Ldarg_0);
                c1.EmitDelegate<Func<Vector3, EntityState, Vector3>>((originalVector, entityState) =>
                {
                    if (entityState == null)
                    {
                        return originalVector;
                    }
                    CharacterBody body = entityState.characterBody;
                    if (body == null)
                    {
                        return originalVector;
                    }
                    Vector3 pointToCheckNodesFrom = body.transform.position;
                    float minimumRange = 25f;
                    bool raycastSuccess = Physics.Raycast(body.transform.position, Vector3.down, out RaycastHit hit, 100f, LayerIndex.world.mask, QueryTriggerInteraction.Ignore);
                    if (raycastSuccess)
                    {
                        pointToCheckNodesFrom = hit.point;
                        minimumRange = 5f;
                    }
                    if (FindClosestNodeWithinRangeBoundaries(SceneInfo.instance.groundNodes, pointToCheckNodesFrom, minimumRange, 200f, out NodeGraph.Node closestNode, out _))
                    {
                        return closestNode.position;
                    }
                    return originalVector;
                });
            }
            else
            {
                Log.Error($"Cursor c1 in OverwriteTeleportPosition failed to match!");
            }
        }
        private void SetUpIndicatorPrefab()
        {
            TeamAreaIndicator areaIndicator = indicatorPrefab.GetComponent<TeamAreaIndicator>();
            areaIndicatorMaterialMonster = new Material(areaIndicator.teamMaterialPairs[0].sharedMaterial);

            areaIndicatorMaterialMonster.SetTexture("_RemapTex", childIndicatorRamp);
            areaIndicatorMaterialMonster.SetFloat("_IntersectionStrength", 2.5f);
            areaIndicatorMaterialMonster.SetFloat("_RimPower", 1f);
            areaIndicatorMaterialMonster.SetFloat("_AlphaBoost", 1.2f);
            areaIndicatorMaterialMonster.SetFloat("_BrightnessBoost", 1.63f);

            areaIndicator.teamMaterialPairs[0].sharedMaterial = areaIndicatorMaterialMonster;
        }
        private void CreateSkill()
        {
            SkillDefData skillData = new SkillDefData
            {
                objectName = "ChildBodyTeleportToEnemy",
                skillName = "ChildTeleportToEnemy",
                esmName = "Body",
                activationState = ContentAddition.AddEntityState<TeleportToEnemy>(out _),
                cooldown = mischiefCooldown.Value,
                combatSkill = true
            };
            TeleportToEnemySkillDef teleportToEnemy = CreateSkillDef<TeleportToEnemySkillDef>(skillData);
            CreateGenericSkill(bodyPrefab, teleportToEnemy, "ChildUtilityFamily", SkillSlot.Utility);
            AISkillDriverData driverData = new AISkillDriverData
            {
                masterPrefab = masterPrefab,
                customName = "useUtility",
                skillSlot = SkillSlot.Utility,
                requiredSkillDef = teleportToEnemy,
                requireReady = true,
                minDistance = 0f,
                maxDistance = mischiefUseRange.Value,
                selectionRequiresOnGround = true,
                aimType = RoR2.CharacterAI.AISkillDriver.AimType.AtCurrentEnemy,
                movementType = RoR2.CharacterAI.AISkillDriver.MovementType.Stop,
                targetType = RoR2.CharacterAI.AISkillDriver.TargetType.CurrentEnemy,
                desiredIndex = 0
            };
            CreateAISkillDriver(driverData);
        }
        public static bool IsHurtBoxParent(HurtBox hb)
        {
            if (hb == null || hb.healthComponent == null)
            {
                return false;
            }
            CharacterBody body = hb.healthComponent.body;
            if (body == null)
            {
                return false;
            }
            if (body.bodyIndex == RoR2Content.BodyPrefabs.ParentBody.bodyIndex)
            {
                return true;
            }
            return false;
        }
        public static bool FindClosestNodeWithinRangeBoundaries(NodeGraph nodeGraph, Vector3 targetPosition, float minRange, float maxRange, out NodeGraph.Node closestNode, out float closestNodeDistance)
        {
            bool found = false;
            closestNode = default;
            closestNodeDistance = Mathf.Infinity;
            List<NodeGraph.NodeIndex> nodes = nodeGraph.FindNodesInRange(targetPosition, minRange, maxRange, HullMask.Human);
            if (nodes.Count == 0)
            {
                return false;
            }
            foreach (NodeGraph.NodeIndex nodeIndex in nodes)
            {
                nodeGraph.GetNodePosition(nodeIndex, out Vector3 nodePosition);
                float distance = Vector3.Distance(targetPosition, nodePosition);
                if (distance < closestNodeDistance)
                {
                    closestNodeDistance = distance;
                    closestNode = nodeGraph.nodes[nodeIndex.nodeIndex];
                    found = true;
                }
            }
            return found;
        }
        public class TeleportToEnemySkillDef : SkillDef
            {
                private class InstanceData : BaseSkillInstanceData
                {
                    public ChildParentDetector detector;
                }
                public override BaseSkillInstanceData OnAssigned([NotNull] GenericSkill skillSlot)
                {
                    return new InstanceData
                    {
                        detector = skillSlot.characterBody.GetComponent<ChildParentDetector>()
                    };
                }
                public override bool IsReady([NotNull] GenericSkill skillSlot)
                {
                    InstanceData data = skillSlot.skillInstanceData as InstanceData;
                    if (data?.detector == null)
                    {
                        return false;
                    }
                    ChildParentDetector detector = data.detector;
                    bool canUseAbility = mischiefParentsOnly.Value ? detector.parentInRange : detector.allyInRange;
                    return base.IsReady(skillSlot) && canUseAbility;
                }
            }
        public class TeleportToEnemy : BaseSkillState
        {
            private static float baseChargeDuration = mischiefChargeDuration.Value;
            private float chargeDuration;
            private static float baseExitDuration = 1f;
            private float exitDuration;
            private float totalDuration;
            private GameObject indicatorInstance;
            private static GameObject teleportEffectPrefab = Addressables.LoadAssetAsync<GameObject>(RoR2_DLC2_Child.FrolicTeleportVFX_prefab).WaitForCompletion();
            private static string enterSoundString = "Play_child_attack2_teleport";
            private static string exitSoundString = "Play_child_attack2_reappear";
            private bool teleported;
            private CharacterBody targetEnemy;
            private CharacterBody targetAlly;
            private Vector3 playerTargetTeleportPosition;
            private Vector3 childTargetTeleportPosition;
            private bool startedAnim;
            private bool readyToTeleport;
            private Transform childMuzzleTransform;
            private CharacterModel childModel;
            private static float radius = mischiefExplosionRadius.Value;
            private static float damageCoefficient = mischiefDamageCoeff.Value / 100f;
            private static float slowDuration = mischiefSlowDuration.Value;
            private static float teleportRange = mischiefTeleportRange.Value;
            public override void OnEnter()
            {
                base.OnEnter();
                teleported = false;
                chargeDuration = baseChargeDuration / attackSpeedStat;
                PlayCrossfade("Gesture, Override", "SparkEnter", "SparkEnter.playbackRate", chargeDuration / 2f, 0.33f);
                exitDuration = baseExitDuration / attackSpeedStat;
                totalDuration = chargeDuration + exitDuration;
                childMuzzleTransform = FindModelChild("Chest");
                indicatorInstance = Instantiate(indicatorPrefab, characterBody.transform);
                childModel = modelLocator.modelTransform.GetComponent<CharacterModel>();
                if (indicatorInstance != null)
                {
                    TeamAreaIndicator indicator = indicatorInstance.GetComponent<TeamAreaIndicator>();
                    if (indicator != null)
                    {
                        indicator.teamComponent = characterBody.teamComponent;
                        indicator.transform.localScale = Vector3.zero;
                    }
                }
            }
            public override void OnExit()
            {
                base.OnExit();
                DestroyIndicator();
                PlayAnimation("Gesture, Override", "BufferEmpty");
            }
            private void DestroyIndicator()
            {
                if (indicatorInstance != null)
                {
                    Destroy(indicatorInstance);
                }
            }
            public override void FixedUpdate()
            {
                base.FixedUpdate();
                if (base.fixedAge < chargeDuration)
                {
                    float clamp = Mathf.Clamp01(base.fixedAge / chargeDuration);
                    float t = Mathf.Pow(clamp, 0.33f);
                    indicatorInstance.transform.localScale = Vector3.one * radius * t;
                }
                if (base.fixedAge > chargeDuration - 0.2f && !startedAnim)
                {
                    startedAnim = true;
                    PlayAnimation("Gesture, Override", "FrolicEnter", "FrolicEnter.playbackRate", 1f);
                    Util.PlaySound(enterSoundString, characterBody.gameObject);
                }
                if (base.fixedAge > chargeDuration && !teleported)
                {
                    teleported = true;
                    DestroyIndicator();
                    FindEnemyTarget();
                    if (targetEnemy == null)
                    {
                        return;
                    }
                    FireAttack();
                    FindAllyTarget();
                    if (targetAlly == null)
                    {
                        return;
                    }
                    FindNodePairNearAlly();
                    if (!readyToTeleport)
                    {
                        return;
                    }
                    AttemptTeleport();
                }
                if (base.fixedAge > totalDuration)
                {
                    outer.SetNextStateToMain();
                }
            }
            public void FindEnemyTarget()
            {
                TeamMask mask = TeamMask.GetEnemyTeams(characterBody.teamComponent.teamIndex);
                SphereSearch search = new SphereSearch
                {
                    mask = LayerIndex.entityPrecise.mask,
                    origin = characterBody.transform.position,
                    radius = radius,
                    queryTriggerInteraction = QueryTriggerInteraction.Ignore
                };
                search.RefreshCandidates();
                search.FilterCandidatesByDistinctHurtBoxEntities();
                search.FilterCandidatesByHurtBoxTeam(mask);
                search.OrderCandidatesByDistance();

                HurtBox[] hurtBoxes = search.GetHurtBoxes();
                if (hurtBoxes.Length > 0)
                {
                    CharacterBody body = hurtBoxes[0].healthComponent.body;
                    if (body != null)
                    {
                        targetEnemy = body;
                    }
                    else
                    {
                        outer.SetNextStateToMain();
                        return;
                    }
                }
                else
                {
                    outer.SetNextStateToMain();
                    return;
                }
            }
            public void FireAttack()
            {
                BlastAttack blastAttack = new BlastAttack
                {
                    attacker = characterBody.gameObject,
                    inflictor = characterBody.gameObject,
                    attackerFiltering = AttackerFiltering.NeverHitSelf,
                    baseDamage = damageCoefficient * characterBody.damage,
                    baseForce = 0f,
                    crit = RollCrit(),
                    damageColorIndex = DamageColorIndex.Default,
                    damageType = new DamageTypeCombo { damageType = DamageType.Generic, damageSource = DamageSource.Utility },
                    falloffModel = BlastAttack.FalloffModel.None,
                    position = characterBody.corePosition,
                    procCoefficient = 1f,
                    procChainMask = default(ProcChainMask),
                    radius = radius,
                    teamIndex = teamComponent.teamIndex,
                };
                targetEnemy.AddTimedBuff(RoR2Content.Buffs.Slow60, slowDuration);
                blastAttack.Fire();
            }
            public void FindAllyTarget()
            {
                TeamMask mask = new TeamMask();
                mask.AddTeam(characterBody.teamComponent.teamIndex);
                SphereSearch search = new SphereSearch
                {
                    mask = LayerIndex.entityPrecise.mask,
                    origin = characterBody.transform.position,
                    radius = teleportRange,
                    queryTriggerInteraction = QueryTriggerInteraction.Ignore
                };
                search.RefreshCandidates();
                search.FilterCandidatesByDistinctHurtBoxEntities();
                search.FilterCandidatesByHurtBoxTeam(mask);
                HurtBox[] hurtBoxes = search.GetHurtBoxes();
                HurtBox[] parentHurtBoxes = hurtBoxes.Where(hurtBox => IsHurtBoxParent(hurtBox)).ToArray();
                if (parentHurtBoxes.Length > 0)
                {
                    hurtBoxes = parentHurtBoxes;
                }
                HurtBox highestHealthHurtBox = null;
                float highestHealth = 0f;
                foreach (HurtBox hurtBox in hurtBoxes)
                {
                    if (hurtBox == null || hurtBox.healthComponent == null || hurtBox.healthComponent.body == null)
                    {
                        continue;
                    }
                    if (hurtBox.healthComponent.body == characterBody)
                    {
                        continue;
                    }
                    float maxHealth = hurtBox.healthComponent.body.maxHealth;
                    if (maxHealth > highestHealth)
                    {
                        highestHealth = maxHealth;
                        highestHealthHurtBox = hurtBox;
                    }
                }
                if (highestHealthHurtBox != null)
                {
                    targetAlly = highestHealthHurtBox.healthComponent.body;
                }
                else
                {
                    outer.SetNextStateToMain();
                    return;
                }
            }
            public void FindNodePairNearAlly()
            {
                if (targetAlly == null)
                {
                    return;
                }
                NodeGraph groundNodes = SceneInfo.instance.groundNodes;
                NodeGraph airNodes = SceneInfo.instance.airNodes;
                float bodyRadius = targetAlly.radius;
                Vector3 allyPosition = targetAlly.transform.position;
                bool foundGroundNode = FindClosestNodeWithinRangeBoundaries(groundNodes, allyPosition, bodyRadius, 25f, out NodeGraph.Node closestGroundNode, out float closestGroundNodeDistance);
                bool foundAirNode = FindClosestNodeWithinRangeBoundaries(airNodes, allyPosition, bodyRadius, 25f, out NodeGraph.Node closestAirNode, out float closestAirNodeDistance);
                bool usingGroundNodes = false;
                if (!foundGroundNode && !foundAirNode)
                {
                    outer.SetNextStateToMain();
                    return;
                }
                if (foundGroundNode && foundAirNode)
                {
                    usingGroundNodes = closestGroundNodeDistance < closestAirNodeDistance;
                }
                else if (foundGroundNode)
                {
                    usingGroundNodes = true;
                }
                bool foundAllyNode = FindClosestNodeWithinRangeBoundaries(usingGroundNodes ? groundNodes : airNodes, usingGroundNodes ? closestGroundNode.position : closestAirNode.position, 0.5f, 25f, out NodeGraph.Node closestPairedNode, out float closestPairedNodeDistance);
                if (!foundAllyNode)
                {
                    outer.SetNextStateToMain();
                    return;
                }
                playerTargetTeleportPosition = usingGroundNodes ? closestGroundNode.position : closestAirNode.position;
                childTargetTeleportPosition = closestPairedNode.position;
                readyToTeleport = true;
            }
            public void AttemptTeleport()
            {
                Util.PlaySound(exitSoundString, characterBody.gameObject);
                CreateTeleportEffect(childMuzzleTransform);
                CreateTeleportEffect(targetEnemy.coreTransform);
                TeleportHelper.TeleportBody(characterBody, childTargetTeleportPosition + new Vector3(0f, 1f, 0f), false);
                TeleportHelper.TeleportBody(targetEnemy, playerTargetTeleportPosition + new Vector3(0f, 1f, 0f), false);
                CharacterModel playerModel = targetEnemy.modelLocator.modelTransform.GetComponent<CharacterModel>();
                TeleportOutController.AddTPOutEffect(playerModel, 1f, 0f, 1f);
                TeleportOutController.AddTPOutEffect(childModel, 1f, 0f, 1f);
                CreateTeleportEffect(childMuzzleTransform);
                CreateTeleportEffect(targetEnemy.coreTransform);
                Util.PlaySound(exitSoundString, characterBody.gameObject);

            }
            public void CreateTeleportEffect(Transform muzzleTransform)
            {
                if (muzzleTransform != null)
                {
                    Vector3 position = muzzleTransform.position;
                    EffectManager.SpawnEffect(teleportEffectPrefab, new EffectData { origin = position, scale = 1f }, transmit: true);
                }
                else
                {
                    Log.Error($"MuzzleTransform not found!");
                }
            }
            public override InterruptPriority GetMinimumInterruptPriority()
            {
                return InterruptPriority.Stun;
            }
        }
        public class ChildParentDetector : MonoBehaviour
        {
            private CharacterBody body;
            private static float searchInterval = 0.25f;
            private float searchTimer;
            public bool parentInRange = false;
            public bool allyInRange = false;
            public void Awake()
            {
                body = GetComponent<CharacterBody>();
                searchTimer = searchInterval;
            }
            public void FixedUpdate()
            {
                if (body == null || body.healthComponent == null || !body.healthComponent.alive)
                {
                    parentInRange = false;
                    allyInRange = false;
                    return;
                }
                searchTimer -= Time.fixedDeltaTime;
                if (searchTimer < 0)
                {
                    searchTimer += searchInterval;
                    SphereSearch sphereSearch = new SphereSearch
                    {
                        mask = LayerIndex.entityPrecise.mask,
                        origin = body.transform.position,
                        radius = 100f,
                        queryTriggerInteraction = QueryTriggerInteraction.Ignore
                    };
                    TeamMask mask = new TeamMask();
                    mask.AddTeam(body.teamComponent.teamIndex);
                    sphereSearch.RefreshCandidates();
                    sphereSearch.FilterCandidatesByDistinctHurtBoxEntities();
                    sphereSearch.FilterCandidatesByHurtBoxTeam(mask);
                    HurtBox[] hurtBoxes = sphereSearch.GetHurtBoxes();
                    if (hurtBoxes.Where(hb => hb != null && hb.healthComponent != null && hb.healthComponent.body != body).Any())
                    {
                        allyInRange = true;
                    }
                    else
                    {
                        allyInRange = false;
                        parentInRange = false;
                    }
                    if (allyInRange)
                    {
                        if (hurtBoxes.Where(hurtBox => IsHurtBoxParent(hurtBox)).Any())
                        {
                            parentInRange = true;
                        }
                        else
                        {
                            parentInRange = false;
                        }
                    }
                    Log.Debug($"AllyInRange: {allyInRange}");
                    Log.Debug($"ParentInRange: {parentInRange}");
                }
            }
        }
    }
}
