using System;
using EntityStates.Tanker;
using RoR2.CharacterAI;
using MiscFixes.Modules;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;
using RoR2.Projectile;
using RoR2BepInExPack.GameAssetPaths.Version_1_39_0;
using UnityEngine;
using UnityEngine.AddressableAssets;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;

namespace EnemyAbilities.Abilities.Tanker
{
    [EnemyAbilities.ModuleInfo("Smarter AI", "Gives Solus Scorchers smarter AI. They'll now prioritise using their flamethrower directly onto oiled targets before using it on oil puddles. They will also prioritise firing their oil puddle at non-flying targets. Oil puddles will now use the damage of their activator.", "Solus Scorcher", true)]

    public class SmarterScorcherModule : BaseModule
    {
        private static GameObject bodyPrefab = Addressables.LoadAssetAsync<GameObject>(RoR2_DLC3_Tanker.TankerBody_prefab).WaitForCompletion();
        private static GameObject masterPrefab = Addressables.LoadAssetAsync<GameObject>(RoR2_DLC3_Tanker.TankerMaster_prefab).WaitForCompletion();
        private static EntityStateConfiguration escIgnite = Addressables.LoadAssetAsync<EntityStateConfiguration>(RoR2_DLC3_Tanker.EntityStates_Tanker_Ignite_asset).WaitForCompletion();
        private static GameObject greaseProjectilePrefab = Addressables.LoadAssetAsync<GameObject>(RoR2_DLC3_Tanker.TankerAccelerantPuddleBodyProjectile_prefab).WaitForCompletion();
        private static CharacterSpawnCard csc = Addressables.LoadAssetAsync<CharacterSpawnCard>(RoR2_DLC3_Tanker.cscTanker_asset).WaitForCompletion();

        private static readonly string fireAtOilTargetName = "fireAtOilTarget";
        private static readonly string fleeOilTargetName = "fleeOilTarget";
        private static readonly string chaseOilTargetName = "chaseOilTarget";
        private static readonly string strafeOilTargetName = "strafeOilTarget";
        private static readonly string fireAtTargetName = "fireAtTarget";
        private static readonly string fleeTargetName = "fleeTarget";
        private static readonly string fireAccelerantName = "fireAccelerant";
        private static readonly string strafeTargetName = "strafeTarget";
        private static readonly string chaseTargetName = "chaseTarget";
        private static readonly string chaseTargetFarName = "chaseTargetFar";

        public override void RegisterConfig()
        {
            base.RegisterConfig();
            BindStats(bodyPrefab, [csc], new StatOverrides { directorCost = 28 });
        }
        public override void Initialise()
        {
            base.Initialise();
            bodyPrefab.AddComponent<TankerBehaviourController>();
            ProjectileSetOwnerAICustomTarget component = greaseProjectilePrefab.GetComponent<ProjectileSetOwnerAICustomTarget>();
            DestroyImmediate(component);
            greaseProjectilePrefab.AddComponent<AccelerantController>();
            IL.RoR2.HealthComponent.TakeDamageProcess += ModifyAccelerantDamage;
            escIgnite.TryModifyFieldValue(nameof(Ignite.tickFrequency), 6f);
            escIgnite.TryModifyFieldValue(nameof(Ignite.totalDamageCoefficient), 2f);
            CreateNewDrivers();
        }
        public void CreateNewDrivers()
        {
            AISkillDriverData fireAtOilTarget = new AISkillDriverData
            {
                masterPrefab = masterPrefab,
                customName = fireAtOilTargetName,
                skillSlot = SkillSlot.Primary,
                requireReady = true,
                minDistance = 0f,
                maxDistance = 20f,
                targetType = AISkillDriver.TargetType.Custom,
                aimType = AISkillDriver.AimType.AtMoveTarget,
                movementType = AISkillDriver.MovementType.ChaseMoveTarget,
                driverUpdateTimerOverride = 2f,
                moveInputScale = 0.5f,
                desiredIndex = 0,
            };
            AISkillDriverData fleeOilTarget = new AISkillDriverData
            {
                masterPrefab = masterPrefab,
                customName = fleeOilTargetName,
                skillSlot = SkillSlot.None,
                minDistance = 0f,
                maxDistance = 10f,
                targetType = AISkillDriver.TargetType.Custom,
                aimType = AISkillDriver.AimType.AtMoveTarget,
                movementType = AISkillDriver.MovementType.FleeMoveTarget,
                desiredIndex = 1
            };
            AISkillDriverData strafeOilTarget = new AISkillDriverData
            {
                masterPrefab = masterPrefab,
                customName = strafeOilTargetName,
                skillSlot = SkillSlot.None,
                minDistance = 0f,
                maxDistance = 20f,
                targetType = AISkillDriver.TargetType.Custom,
                aimType = AISkillDriver.AimType.AtMoveTarget,
                movementType = AISkillDriver.MovementType.StrafeMovetarget,
                desiredIndex = 2
            };
            AISkillDriverData chaseOilTarget = new AISkillDriverData
            {
                masterPrefab = masterPrefab,
                customName = chaseOilTargetName,
                skillSlot = SkillSlot.None,
                minDistance = 0f,
                maxDistance = Mathf.Infinity,
                targetType = AISkillDriver.TargetType.Custom,
                aimType = AISkillDriver.AimType.AtMoveTarget,
                movementType = AISkillDriver.MovementType.ChaseMoveTarget,
                desiredIndex = 3
            };
            AISkillDriverData fireAtTarget = new AISkillDriverData
            {
                masterPrefab = masterPrefab,
                customName = fireAtTargetName,
                skillSlot = SkillSlot.Primary,
                requireReady = true,
                minDistance = 0f,
                maxDistance = 20f,
                targetType = AISkillDriver.TargetType.Custom,
                aimType = AISkillDriver.AimType.AtMoveTarget,
                movementType = AISkillDriver.MovementType.ChaseMoveTarget,
                driverUpdateTimerOverride = 2f,
                noRepeat = true,
                desiredIndex = 4
            };
            AISkillDriverData fleeTarget = new AISkillDriverData
            {
                masterPrefab = masterPrefab,
                customName = fleeTargetName,
                skillSlot = SkillSlot.None,
                minDistance = 0f,
                maxDistance = 20f,
                targetType = AISkillDriver.TargetType.Custom,
                aimType = AISkillDriver.AimType.AtMoveTarget,
                movementType = AISkillDriver.MovementType.FleeMoveTarget,
                desiredIndex = 5
            };
            AISkillDriverData fireAccelerant = new AISkillDriverData
            {
                masterPrefab = masterPrefab,
                customName = fireAccelerantName,
                skillSlot = SkillSlot.Secondary,
                requireReady = true,
                minDistance = 0f,
                maxDistance = 40f,
                targetType = AISkillDriver.TargetType.Custom,
                aimType = AISkillDriver.AimType.AtMoveTarget,
                movementType = AISkillDriver.MovementType.StrafeMovetarget,
                desiredIndex = 6
            };
            AISkillDriverData strafeTarget = new AISkillDriverData
            {
                masterPrefab = masterPrefab,
                customName = strafeTargetName,
                skillSlot = SkillSlot.None,
                minDistance = 0f,
                maxDistance = 40f,
                targetType = AISkillDriver.TargetType.Custom,
                aimType = AISkillDriver.AimType.AtMoveTarget,
                movementType = AISkillDriver.MovementType.StrafeMovetarget,
                desiredIndex = 7
            };
            AISkillDriverData chaseTarget = new AISkillDriverData
            {
                masterPrefab = masterPrefab,
                customName = chaseTargetName,
                skillSlot = SkillSlot.None,
                minDistance = 0f,
                maxDistance = 60f,
                targetType = AISkillDriver.TargetType.Custom,
                aimType = AISkillDriver.AimType.AtMoveTarget,
                movementType = AISkillDriver.MovementType.ChaseMoveTarget,
                desiredIndex = 8
            };
            AISkillDriverData chaseTargetFar = new AISkillDriverData
            {
                masterPrefab = masterPrefab,
                customName = chaseTargetFarName,
                skillSlot = SkillSlot.None,
                minDistance = 0f,
                maxDistance = Mathf.Infinity,
                targetType = AISkillDriver.TargetType.CurrentEnemy,
                aimType = AISkillDriver.AimType.AtCurrentEnemy,
                movementType = AISkillDriver.MovementType.ChaseMoveTarget,
                desiredIndex = 9
            };
            CreateAISkillDriver(fireAtOilTarget);
            CreateAISkillDriver(fleeOilTarget);
            CreateAISkillDriver(strafeOilTarget);
            CreateAISkillDriver(chaseOilTarget);
            CreateAISkillDriver(fireAtTarget);
            CreateAISkillDriver(fleeTarget);
            CreateAISkillDriver(fireAccelerant);
            CreateAISkillDriver(strafeTarget);
            CreateAISkillDriver(chaseTarget);
            CreateAISkillDriver(chaseTargetFar);
        }
        private void ModifyAccelerantDamage(ILContext il)
        {
            ILCursor c1 = new ILCursor(il);
            if (c1.TryGotoNext(
                x => x.MatchLdsfld<Ignite>(nameof(Ignite.accelerantBlastBaseDamage)),
                x => x.MatchMul(),
                x => x.MatchAdd(),
                x => x.MatchStfld<DelayBlast>(nameof(DelayBlast.baseDamage))
            ))
            {
                c1.Index += 3;
                c1.Emit(OpCodes.Ldarg_0);
                c1.Emit(OpCodes.Ldarg_1);
                c1.EmitDelegate<Func<float, HealthComponent, DamageInfo, float>>((originalValue, healthComponent, damageInfo) =>
                {
                    GameObject attacker = null;
                    if (damageInfo != null && damageInfo.attacker != null)
                    {
                        attacker = damageInfo.attacker;
                    }
                    if (healthComponent != null && healthComponent.body != null)
                    {
                        bool success = TryCalculateIgnitionDamage(attacker, out float ignitionDamage);
                        if (success)
                        {
                            return ignitionDamage;
                        }
                    }
                    return originalValue;
                });
            }
            ILCursor c2 = new ILCursor(il);
            if (c2.TryGotoNext(
                x => x.MatchLdloc(62),
                x => x.MatchLdcI4(32768)
            ))
            {
                c2.Index += 3;
                c2.EmitDelegate<Func<DamageTypeCombo, DamageTypeCombo>>((combo) =>
                {
                    return new DamageTypeCombo { damageSource = DamageSource.Secondary, damageType = DamageType.IgniteOnHit };
                });
            }
        }
        public bool TryCalculateIgnitionDamage(GameObject attacker, out float damage)
        {
            damage = 0f;
            if (attacker == null)
            {
                return false;
            }
            CharacterBody attackerBody = attacker.GetComponent<CharacterBody>();
            TeamComponent teamComponent = attackerBody.GetComponent<TeamComponent>();
            if (attackerBody != null && teamComponent != null)
            {
                bool isPlayer = teamComponent.teamIndex == TeamIndex.Player;
                damage = attackerBody.damage * (isPlayer ? 6f : 2f);
                return true;
            }
            return false;
        }
        public class TankerBehaviourController : MonoBehaviour
        {
            private CharacterBody tankerBody;
            private GameObject enemyTarget;
            private GameObject accelerantTarget;
            private static float searchInterval = 0.25f;
            private static float oilCheckDistance = 16f;
            private float searchTimer;
            private BaseAI baseAI;

            private List<AISkillDriver> oilTargetDrivers = new List<AISkillDriver>();

            public void Start()
            {
                tankerBody = GetComponent<CharacterBody>();
                searchTimer = searchInterval;
                CharacterMaster master = tankerBody.master;
                if (master != null)
                {
                    baseAI = master.GetComponent<BaseAI>();
                    AISkillDriver[] drivers = baseAI.skillDrivers;
                    foreach ( AISkillDriver driver in drivers )
                    {
                        if (driver.customName.Contains("OilTarget"))
                        {
                            oilTargetDrivers.Add(driver);
                        }
                    }
                }
            }
            public void FixedUpdate()
            {
                searchTimer -= Time.fixedDeltaTime;
                if (searchTimer < 0)
                {
                    searchTimer += searchInterval;
                    PerformSearch();
                }
            }
            public void PerformSearch()
            {
                if (tankerBody == null)
                {
                    return;
                }
                TeamComponent teamComponent = tankerBody.teamComponent;
                if (teamComponent == null)
                {
                    return;
                }
                TeamMask mask = TeamMask.GetEnemyTeams(tankerBody.teamComponent.teamIndex);
                mask.RemoveTeam(TeamIndex.Neutral);
                BullseyeSearch search = new BullseyeSearch();
                search.viewer = tankerBody;
                search.searchOrigin = tankerBody.transform.position;
                search.filterByLoS = true;
                search.filterByDistinctEntity = true;
                search.minAngleFilter = 0f;
                search.maxAngleFilter = 360f;
                search.minDistanceFilter = 0f;
                search.maxDistanceFilter = 60f;
                search.searchDirection = tankerBody.transform.forward;
                search.teamMaskFilter = mask;
                search.sortMode = BullseyeSearch.SortMode.DistanceAndAngle;
                search.RefreshCandidates();
                List<HurtBox> enemyHurtBoxes = search.GetResults().Where(hurtBox => HurtBoxValidForCustomTargetSelection(hurtBox)).ToList();
                List<HurtBox> nonFlierEnemyHurtBoxes = enemyHurtBoxes.Where(hurtBox => HurtBoxIsNonFlier(hurtBox)).ToList();
                List<HurtBox> oiledEnemyHurtBoxes = enemyHurtBoxes.Where(hurtBox => HurtBoxHasAccelerantDebuff(hurtBox)).ToList();
                List<HurtBox> enemyHurtBoxesInRangeOfOil = enemyHurtBoxes.Where(hurtBox => HurtBoxInOilRange(hurtBox)).ToList();
                GameObject target = enemyHurtBoxes.FirstOrDefault()?.healthComponent.body.gameObject;
                bool lookForAccelerantPuddle = false;
                bool shouldActivateOilDrivers = false;

                //priroity - oiled enemies, enemies near oil puddles, non-fliers, all enemies

                if (oiledEnemyHurtBoxes.Any())
                {
                    target = oiledEnemyHurtBoxes.FirstOrDefault().healthComponent.body.gameObject;
                    shouldActivateOilDrivers = target != null;
                }
                else if (enemyHurtBoxesInRangeOfOil.Any())
                {
                    lookForAccelerantPuddle = true;
                    target = enemyHurtBoxesInRangeOfOil.FirstOrDefault().healthComponent.body.gameObject;
                }
                else if (nonFlierEnemyHurtBoxes.Any())
                {
                    target = nonFlierEnemyHurtBoxes.FirstOrDefault().healthComponent.body.gameObject;
                }
                enemyTarget = target;
                if (lookForAccelerantPuddle)
                {
                    bool foundAccelerantPuddle = TryFindClosestOil(enemyTarget, out GameObject accelerant);
                    shouldActivateOilDrivers = foundAccelerantPuddle;
                    accelerantTarget = foundAccelerantPuddle ? accelerant : null;
                }
                else
                {
                    accelerantTarget = null;
                }
                if (baseAI == null || baseAI.customTarget == null)
                {
                    return;
                }
                if (enemyTarget != null)
                {
                    baseAI.SetCustomTargetGameObject(enemyTarget);
                }
                if (accelerantTarget != null)
                {
                    baseAI.SetCustomTargetGameObject(accelerantTarget);
                }
                foreach (AISkillDriver driver in oilTargetDrivers)
                {
                    driver.enabled = shouldActivateOilDrivers;
                }
                AISkillDriver dominantSkillDriver = baseAI.skillDriverEvaluation.dominantSkillDriver;
            }
            public bool HurtBoxIsNonFlier(HurtBox hurtBox)
            {
                CharacterBody body = hurtBox.healthComponent.body;
                CharacterMotor motor = body.characterMotor;
                Rigidbody rigidbody = body.rigidbody;
                if (motor == null && rigidbody != null && rigidbody.useGravity == false)
                {
                    return false;
                }
                if (motor != null && motor.isFlying == true)
                {
                    return false;
                }
                return true;
            }
            public bool HurtBoxInOilRange(HurtBox hurtBox)
            {
                Vector3 bodyPosition = hurtBox.healthComponent.body.transform.position;
                foreach (AccelerantController accelerantController in InstanceTracker.GetInstancesList<AccelerantController>())
                {
                    Vector3 accelerantPosition = accelerantController.gameObject.transform.position;
                    float distance = Vector3.Distance(bodyPosition, accelerantPosition);
                    if (distance < oilCheckDistance)
                    {
                        return true;
                    }
                }
                return false;
            }
            public bool HurtBoxValidForCustomTargetSelection(HurtBox hurtBox)
            {
                if (hurtBox == null || hurtBox.healthComponent == null)
                {
                    return false;
                }
                CharacterBody body = hurtBox.healthComponent.body;
                if (body == null)
                {
                    return false;
                }
                TeamComponent teamComponent = body.teamComponent;
                if (teamComponent == null)
                {
                    return false;
                }
                TeamIndex teamIndex = teamComponent.teamIndex;
                if (teamIndex == TeamIndex.Neutral)
                {
                    return false;
                }
                return true;

            }
            public bool HurtBoxHasAccelerantDebuff(HurtBox hurtBox)
            {
                if (hurtBox.healthComponent.body.HasBuff(DLC3Content.Buffs.Accelerant))
                {
                    return true;
                }
                return false;
            }
            public bool TryFindClosestOil(GameObject target, out GameObject closestOil)
            {
                Vector3 targetPosition = target.transform.position;
                closestOil = null;
                float closestDistance = Mathf.Infinity;
                foreach (AccelerantController controller in InstanceTracker.GetInstancesList<AccelerantController>())
                {
                    Vector3 oilPosition = controller.gameObject.transform.position;
                    float distance = Vector3.Distance(oilPosition, targetPosition);
                    if (distance < oilCheckDistance)
                    {
                        if (distance <  closestDistance)
                        {
                            closestDistance = distance;
                            closestOil = controller.gameObject;
                        }
                    }
                }
                if (closestOil != null)
                {
                    return true;
                }
                return false;
            }
        }
        public class AccelerantController : MonoBehaviour
        {
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
