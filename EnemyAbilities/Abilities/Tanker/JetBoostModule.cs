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
using EntityStates;
using R2API;
using RoR2.Skills;
using JetBrains.Annotations;

namespace EnemyAbilities.Abilities.Tanker
{
    [EnemyAbilities.ModuleInfo("Jet Boost", "Gives Solus Scorchers a new Utility:\n-Jet Boost: Allows Solus Scorchers to dash forwards towards their current target. This module also incorporates many changes to the AI of Solus Scorchers, making them prioritise oiled players over their oil blobs. Also fixes numerous odd inconsistencies with Scorcher damage team affiliation and oil blob team affiliation.", "Solus Scorcher", true)]

    public class JetBoostModule : BaseModule
    {
        private static GameObject bodyPrefab = Addressables.LoadAssetAsync<GameObject>(RoR2_DLC3_Tanker.TankerBody_prefab).WaitForCompletion();
        private static GameObject masterPrefab = Addressables.LoadAssetAsync<GameObject>(RoR2_DLC3_Tanker.TankerMaster_prefab).WaitForCompletion();
        private static EntityStateConfiguration escIgnite = Addressables.LoadAssetAsync<EntityStateConfiguration>(RoR2_DLC3_Tanker.EntityStates_Tanker_Ignite_asset).WaitForCompletion();
        private static GameObject greaseProjectilePrefab = Addressables.LoadAssetAsync<GameObject>(RoR2_DLC3_Tanker.TankerAccelerantPuddleBodyProjectile_prefab).WaitForCompletion();
        private static CharacterSpawnCard csc = Addressables.LoadAssetAsync<CharacterSpawnCard>(RoR2_DLC3_Tanker.cscTanker_asset).WaitForCompletion();
        private static SkillDef ignite = Addressables.LoadAssetAsync<SkillDef>(RoR2_DLC3_Tanker.TankerIgnite_asset).WaitForCompletion();
        private static EntityStateConfiguration escSpawn = Addressables.LoadAssetAsync<EntityStateConfiguration>(RoR2_DLC3_Tanker.EntityStates_Tanker_SpawnState_asset).WaitForCompletion();

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

        private static SkillDef jetBoost;
        private static ConfigEntry<float> jetBoostCooldown;
        private static ConfigEntry<float> jetBoostSpeedMult;
        private static ConfigEntry<float> accelerantEnemyDamage;
        private static ConfigEntry<float> accelerantPlayerDamage;

        public override void RegisterConfig()
        {
            base.RegisterConfig();
            BindStats(bodyPrefab, [csc], new StatOverrides { directorCost = 28 });
            jetBoostCooldown = BindFloat("Jet Boost Cooldown", 3f, "Cooldown of the skill. Note that Jet Boost is only selected when the Scorcher has an oil target.", 2f, 10f, 0.1f, PluginConfig.FormatType.Time);
            jetBoostSpeedMult = BindFloat("Jet Boost Speed", 300f, "The Solus Scorcher's top speed compared to it's base movement speed at the start of Jet Boost.", 200f, 400f, 1f, PluginConfig.FormatType.Percentage);
            accelerantEnemyDamage = BindFloat("Grease Explosion Damage (Enemy)", 200f, "The damage coefficient of grease puddles when ignited by an enemy.", 100f, 400f, 5f, PluginConfig.FormatType.Percentage);
            accelerantPlayerDamage = BindFloat("Grease Explosion Damage (Player)", 600f, "The damage coefficient of grease puddles when ignited by a player.", 100f, 1000f, 5f, PluginConfig.FormatType.Percentage);
        }
        public override void Initialise()
        {
            base.Initialise();
            escSpawn.TryModifyFieldValue(nameof(EntityStates.Tanker.SpawnState.duration), 1f);
            CreateSkillDef();
            bodyPrefab.AddComponent<TankerBehaviourController>();
            ProjectileSetOwnerAICustomTarget component = greaseProjectilePrefab.GetComponent<ProjectileSetOwnerAICustomTarget>();
            DestroyImmediate(component);
            AssignTeamFilterToTeamComponent assignFilter = greaseProjectilePrefab.GetComponent<AssignTeamFilterToTeamComponent>();
            if (assignFilter != null)
            {
                DestroyImmediate(component);
            }
            greaseProjectilePrefab.AddComponent<AccelerantController>();
            IL.RoR2.HealthComponent.TakeDamageProcess += ModifyAccelerantDamage;
            IL.EntityStates.Tanker.Ignite.FireFlamethrower += ChangeIgniteTeamIndexOverride;
            escIgnite.TryModifyFieldValue(nameof(Ignite.tickFrequency), 6f);
            escIgnite.TryModifyFieldValue(nameof(Ignite.totalDamageCoefficient), 2f);
            CreateNewDrivers();
            ignite.baseRechargeInterval = 1f;
            ignite.beginSkillCooldownOnSkillEnd = true;
        }
        private void ChangeIgniteTeamIndexOverride(ILContext il)
        {
            ILCursor c1 = new ILCursor(il);
            if (c1.TryGotoNext(
                x => x.MatchStfld<BulletAttack>(nameof(BulletAttack.teamIndexOverride))
            ))
            {
                c1.Emit(OpCodes.Ldarg_0);
                c1.EmitDelegate<Func<TeamIndex?, EntityState, TeamIndex?>>((originalValue, entityState) =>
                {
                    if (entityState != null && entityState.characterBody != null && entityState.characterBody.teamComponent != null)
                    {
                        return entityState.characterBody.teamComponent.teamIndex;
                    }
                    return originalValue;
                });
            }
            else
            {
                Log.Error($"ChangeIgniteTeamIndexOverride cursor c1 failed to match!");
            }
        }
        public void CreateSkillDef()
        {
            SkillDefData skillDefData = new SkillDefData
            {
                objectName = "TankerBodyJetBoost",
                skillName = "TankerJetBoost",
                esmName = "Weapon",
                activationState = ContentAddition.AddEntityState<JetBoost>(out _),
                cooldown = jetBoostCooldown.Value,
                baseMaxStock = 1,
                combatSkill = false,
                intPrio = InterruptPriority.Any
            };
            jetBoost = CreateSkillDef<JetBoostSkillDef>(skillDefData);
            CreateGenericSkill(bodyPrefab, jetBoost, "TankerUtilityFamily", SkillSlot.Utility);
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
                driverUpdateTimerOverride = 2.5f,
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
            AISkillDriverData chaseOilTargetWithUtil = new AISkillDriverData
            {
                masterPrefab = masterPrefab,
                customName = chaseOilTargetName,
                skillSlot = SkillSlot.Utility,
                requiredSkillDef = jetBoost,
                minDistance = 0f,
                maxDistance = 40f,
                targetType = AISkillDriver.TargetType.Custom,
                aimType = AISkillDriver.AimType.AtMoveTarget,
                movementType = AISkillDriver.MovementType.ChaseMoveTarget,
                desiredIndex = 3,
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
                desiredIndex = 4
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
                driverUpdateTimerOverride = 2.5f,
                noRepeat = true,
                desiredIndex = 5
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
                desiredIndex = 6
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
                desiredIndex = 7
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
                desiredIndex = 8
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
                desiredIndex = 9
            };
            AISkillDriverData chaseTargetWithUtil = new AISkillDriverData
            {
                masterPrefab = masterPrefab,
                customName = chaseTargetName,
                skillSlot = SkillSlot.Utility,
                selectionRequiresTargetLoS = true,
                minDistance = 60f,
                maxDistance = 100f,
                targetType = AISkillDriver.TargetType.Custom,
                aimType = AISkillDriver.AimType.AtMoveTarget,
                movementType = AISkillDriver.MovementType.ChaseMoveTarget,
                desiredIndex = 10
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
                desiredIndex = 11
            };
            CreateAISkillDriver(fireAtOilTarget);
            CreateAISkillDriver(fleeOilTarget);
            CreateAISkillDriver(strafeOilTarget);
            CreateAISkillDriver(chaseOilTarget);
            CreateAISkillDriver(chaseOilTargetWithUtil);
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
            else
            {
                Log.Error($"ModifyAccelerantDamage ILCursor c1 failed to match!");
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
            else
            {
                Log.Error($"ModifyAccelerantDamage ILCursor c2 failed to match!");
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
                damage = attackerBody.damage * (isPlayer ? accelerantPlayerDamage.Value / 100f : accelerantEnemyDamage.Value / 100f);
                return true;
            }
            return false;
        }
        public class JetBoostSkillDef : SkillDef
        {
            public override bool IsReady([NotNull] GenericSkill skillSlot)
            {
                return base.IsReady(skillSlot);
            }
        }
        public class JetBoost : BaseSkillState
        {
            private float duration;
            private static float baseDuration = 0.5f;
            private float jetBoostSpeedMultiplier = jetBoostSpeedMult.Value / 100f;
            private float jetBoostSpeed;
            private Vector3 startAimVector;
            private float startMoveSpeed;
            private static GameObject fireEffect = Addressables.LoadAssetAsync<GameObject>(RoR2_DLC2_FalseSon.FalseSonLaserMuzzleFlashFire_prefab).WaitForCompletion();
            public override void OnEnter()
            {
                base.OnEnter();
                Util.PlaySound("Play_mage_m1_shoot", this.gameObject);
                duration = baseDuration / attackSpeedStat;
                startMoveSpeed = moveSpeedStat;
                jetBoostSpeed = startMoveSpeed * jetBoostSpeedMultiplier;
                if (inputBank == null)
                {
                    return;
                }
                Vector3 aimDirection = inputBank.aimDirection;
                startAimVector = aimDirection;
                EffectManager.SimpleEffect(fireEffect, transform.position, Util.QuaternionSafeLookRotation(-aimDirection), true);
            }
            public override void OnExit()
            {
                base.OnExit();
            }
            public override void FixedUpdate()
            {
                base.FixedUpdate();
                float scaling = Mathf.Pow(1 - Mathf.Clamp01(base.fixedAge / duration), 0.5f);
                if (rigidbody != null)
                {
                    Vector3 targetVelocity = startAimVector * (startMoveSpeed + (scaling * (jetBoostSpeed - startMoveSpeed)));
                    Vector3 newVelocity = Vector3.Lerp(rigidbody.velocity, targetVelocity, 0.5f);
                    rigidbody.velocity = newVelocity;
                }
                if (base.fixedAge > duration)
                {
                    outer.SetNextStateToMain();
                }
            }
            public override InterruptPriority GetMinimumInterruptPriority()
            {
                return InterruptPriority.PrioritySkill;
            }
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
            public void Start()
            {
                TeamComponent component = GetComponent<TeamComponent>();
                component.teamIndex = TeamIndex.Neutral;
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
