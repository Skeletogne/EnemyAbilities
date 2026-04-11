using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using RoR2BepInExPack;
using BepInEx;
using R2API;
using UnityEngine.AddressableAssets;
using RoR2BepInExPack.GameAssetPaths.Version_1_39_0;
using EntityStates;
using RoR2.Skills;
using RoR2.CharacterAI;
using RoR2;
using static EnemyAbilities.Abilities.Nullifier.SummonAnglerEyeModule;
using System.Linq;
using BepInEx.Configuration;

namespace EnemyAbilities.Abilities.Nullifier
{
    [EnemyAbilities.ModuleInfo("Summon Void Angler", "Gives Void Reavers a new Special Ability:\n-Summon Void Angler: This summons forth a Void Angler that takes incredibly long range shots at the player. (This module is still a work in progress!)", "Void Reaver", true)]
    public class SummonAnglerEyeModule : BaseModule
    {
        private static GameObject anglerEyeBodyPrefab = EnemyAbilities.Instance.assetBundle.LoadAsset<GameObject>("AnglerEyeBody");
        private static GameObject anglerEyeMasterPrefab = EnemyAbilities.Instance.assetBundle.LoadAsset<GameObject>("AnglerEyeMaster");
        private static GameObject nullifierBodyPrefab = Addressables.LoadAssetAsync<GameObject>(RoR2_Base_Nullifier.NullifierBody_prefab).WaitForCompletion();
        private static GameObject nullifierMasterPrefab = Addressables.LoadAssetAsync<GameObject>(RoR2_Base_Nullifier.NullifierMaster_prefab).WaitForCompletion();
        private static GameObject anglerEyeLaserIndicator = Addressables.LoadAssetAsync<GameObject>(RoR2_Base_Golem.LaserGolem_prefab).WaitForCompletion().InstantiateClone("VoidAnglerLaserIndicator");
        private static GameObject portalVFX = Addressables.LoadAssetAsync<GameObject>(RoR2_Base_Nullifier.NullifierSpawnEffect_prefab).WaitForCompletion().InstantiateClone("AnglerEyeSpawnEffect");
        private static CharacterSpawnCard cscNullifier = Addressables.LoadAssetAsync<CharacterSpawnCard>(RoR2_Base_Nullifier.cscNullifier_asset).WaitForCompletion();

        private static ConfigEntry<float> summonCooldown;
        private static ConfigEntry<float> maxAnglers;
        private static ConfigEntry<float> anglerHealth;
        private static ConfigEntry<float> anglerMovespeed;
        private static ConfigEntry<float> anglerMaxRange;
        private static ConfigEntry<float> anglerBeamDamage;
        private static ConfigEntry<float> anglerBeamCooldown;
        private static ConfigEntry<float> anglerAimSpeed;
        public override void RegisterConfig()
        {
            base.RegisterConfig();
            summonCooldown = BindFloat("Summon Cooldown", 12f, "The cooldown of the Summon Void Angler ability.", 6f, 30f, 0.1f, PluginConfig.FormatType.Time);
            maxAnglers = BindFloat("Max Summons", 2f, "The maximum number of Void Anglers that can be active per Void Reaver.", 1f, 5f, 1f, PluginConfig.FormatType.None);
            anglerHealth = BindFloat("Angler Health", 40f, "The base health of Void Anglers at level 1. Increases by 30% per level.", 20f, 100f, 1f, PluginConfig.FormatType.None);
            anglerMovespeed = BindFloat("Angler Speed", 10f, "The base movement speed of Void Anglers.", 4f, 20f, 1f, PluginConfig.FormatType.Speed);
            anglerMaxRange = BindFloat("Angler Max Range", 500f, "The maximum attack range of Void Anglers", 50f, 1000f, 10f, PluginConfig.FormatType.Distance);
            anglerBeamCooldown = BindFloat("Angler Beam Cooldown", 6f, "The cooldown of the Void Angler's beam attack.", 4f, 12f, 1f, PluginConfig.FormatType.Time);
            anglerBeamDamage = BindFloat("Angler Beam Damage", 200f, "The damage coefficient of the Void Angler's beam attack.", 100f, 400f, 5f, PluginConfig.FormatType.Percentage);
            anglerAimSpeed = BindFloat("Angler Aim Speed", 120f, "The max aim vector speed of the Void Angler. Higher values mean it's easier for the Void Angler to track the player and land it's shots.", 60f, 180f, 1f, PluginConfig.FormatType.None);
            BindStats(nullifierBodyPrefab, [cscNullifier]);
        }
        public override void Initialise()
        {
            base.Initialise();
            GlobalEventManager.onCharacterDeathGlobal += KillLinkedAnglerEyes;
            ContentAddition.AddBody(anglerEyeBodyPrefab);
            ContentAddition.AddMaster(anglerEyeMasterPrefab);
            ContentAddition.AddEffect(portalVFX);
            nullifierBodyPrefab.AddComponent<NullifierSummonController>();
            LanguageAPI.Add("SKELETOGNE_ANGLEREYE_BODY_NAME", "Void Angler");
            CreateNullifierSkill();
            CreateAnglerEyeSkill();
            HurtBox hurtBox = anglerEyeBodyPrefab.GetComponentInChildren<HurtBox>();
            hurtBox.gameObject.layer = LayerIndex.entityPrecise.intVal;

            CharacterBody anglerBody = anglerEyeBodyPrefab.GetComponent<CharacterBody>();
            anglerBody.baseMaxHealth = anglerHealth.Value;
            anglerBody.levelMaxHealth = anglerHealth.Value * 0.3f;
            anglerBody.baseMoveSpeed = anglerMovespeed.Value;
            anglerBody.baseAcceleration = anglerMovespeed.Value * 2f;

            EntityStateMachine bodyEsm = anglerEyeBodyPrefab.GetComponents<EntityStateMachine>().Where(esm => esm.customName == "Body").FirstOrDefault();
            if (bodyEsm != null)
            {
                bodyEsm.initialStateType = new SerializableEntityStateType(typeof(AnglerEyeSpawnState));
                bodyEsm.mainStateType = new SerializableEntityStateType(typeof(AnglerEyeFlyState));
            }

            CharacterDeathBehavior deathBehaviour = anglerEyeBodyPrefab.GetComponent<CharacterDeathBehavior>();
            deathBehaviour.deathState = new EntityStates.SerializableEntityStateType(typeof(AnglerEyeDeath));

            EntityStateMachine masterEsm = anglerEyeMasterPrefab.GetComponent<EntityStateMachine>();
            masterEsm.customName = "AI";
            masterEsm.initialStateType = new EntityStates.SerializableEntityStateType(typeof(EntityStates.AI.Walker.Wander));
            masterEsm.mainStateType = new EntityStates.SerializableEntityStateType(typeof(EntityStates.AI.Walker.Wander));

            BaseAI ai = anglerEyeMasterPrefab.GetComponent<BaseAI>();
            ai.scanState = new EntityStates.SerializableEntityStateType(typeof(EntityStates.AI.Walker.Wander));
            ai.aimVectorMaxSpeed = anglerAimSpeed.Value;

            RigidbodyMotor motor = anglerEyeBodyPrefab.GetComponent<RigidbodyMotor>();
            RigidbodyDirection direction = anglerEyeBodyPrefab.GetComponent<RigidbodyDirection>();
            VectorPID[] vectorPIDs = anglerEyeBodyPrefab.GetComponents<VectorPID>();
            QuaternionPID quaternionPID = anglerEyeBodyPrefab.GetComponent<QuaternionPID>();
            motor.forcePID = vectorPIDs[0]; 
            direction.torquePID = vectorPIDs[1]; 
            direction.angularVelocityPID = quaternionPID;

            LineRenderer lineRenderer = anglerEyeLaserIndicator.GetComponent<LineRenderer>();
            lineRenderer.startWidth = 0.5f;
            lineRenderer.endWidth = 0.5f;
            lineRenderer.startColor = new Color(1f, 0f, 1f);
            lineRenderer.endColor = new Color(1f, 0f, 1f);
            Material laserMaterial = Addressables.LoadAssetAsync<Material>(RoR2_Base_Nullifier.matNullifierStarTrail_mat).WaitForCompletion();
            lineRenderer.material = laserMaterial;
            lineRenderer.materials = [laserMaterial];

            CharacterModel characterModel = anglerEyeBodyPrefab.GetComponentInChildren<CharacterModel>();

            portalVFX.GetComponent<Transform>().localScale = new Vector3(0.5f, 0.5f, 0.5f);

        }
        private void KillLinkedAnglerEyes(DamageReport report)
        {
            if (report != null && report.victimBody != null)
            {
                NullifierSummonController controller = report.victimBody.GetComponent<NullifierSummonController>();
                if (controller != null)
                {
                    controller.KillAllSumons();
                }
            }
        }
        public void CreateNullifierSkill()
        {
            SkillDefData skillDefData = new SkillDefData
            {
                objectName = "NullifierBodySummonAnglerEye",
                skillName = "NullifierSummonAnglerEye",
                esmName = "Weapon",
                activationState = ContentAddition.AddEntityState<SummonAnglerEye>(out _),
                cooldown = summonCooldown.Value,
                combatSkill = true
            };
            SkillDef summonAnglerEye = CreateSkillDef<SkillDef>(skillDefData);
            CreateGenericSkill(nullifierBodyPrefab, summonAnglerEye.skillName, "NullifierSpecialFamily", summonAnglerEye, RoR2.SkillSlot.Special);
            AISkillDriverData driverData = new AISkillDriverData
            {
                masterPrefab = nullifierMasterPrefab,
                customName = "useSpecial",
                skillSlot = SkillSlot.Special,
                requireReady = true,
                requiredSkillDef = summonAnglerEye,
                minDistance = 0f,
                maxDistance = Mathf.Infinity,
                selectionRequiresTargetLoS = false,
                aimType = AISkillDriver.AimType.AtCurrentEnemy,
                movementType = AISkillDriver.MovementType.ChaseMoveTarget,
                targetType = AISkillDriver.TargetType.CurrentEnemy,
                desiredIndex = 0
            };
            CreateAISkillDriver(driverData);
        }
        public void CreateAnglerEyeSkill()
        {
            SkillDefData skillDefData = new SkillDefData
            {
                objectName = "AnglerEyeBodyShoot",
                skillName = "AnglerEyeShoot",
                esmName = "Weapon",
                activationState = ContentAddition.AddEntityState<AnglerEyeShoot>(out _),
                cooldown = anglerBeamCooldown.Value,
                combatSkill = true
            };
            SkillDef shoot = CreateSkillDef<SkillDef>(skillDefData);
            CreateGenericSkill(anglerEyeBodyPrefab, shoot.skillName, "AnglerEyePrimaryFamily", shoot, SkillSlot.Primary);
            AISkillDriverData fleeTarget = new AISkillDriverData
            {
                masterPrefab = anglerEyeMasterPrefab,
                customName = "fleeTarget",
                skillSlot = SkillSlot.None,
                minDistance = 0f,
                maxDistance = 30f,
                selectionRequiresTargetLoS = false,
                aimType = AISkillDriver.AimType.AtCurrentEnemy,
                movementType = AISkillDriver.MovementType.FleeMoveTarget,
                targetType = AISkillDriver.TargetType.CurrentEnemy,
                desiredIndex = 0
            };
            AISkillDriverData usePrimary = new AISkillDriverData
            {
                masterPrefab = anglerEyeMasterPrefab,
                customName = "usePrimary",
                skillSlot = SkillSlot.Primary,
                requireReady = true,
                minDistance = 0f,
                maxDistance = anglerMaxRange.Value,
                selectionRequiresTargetLoS = true,
                aimType = AISkillDriver.AimType.AtCurrentEnemy,
                movementType = AISkillDriver.MovementType.Stop,
                targetType = AISkillDriver.TargetType.CurrentEnemy,
                desiredIndex = 1,
                driverUpdateTimerOverride = 2.5f
            };
            AISkillDriverData strafeTarget = new AISkillDriverData
            {
                masterPrefab = anglerEyeMasterPrefab,
                customName = "strafeAndShootTarget",
                skillSlot = SkillSlot.None,
                minDistance = 0f,
                maxDistance = anglerMaxRange.Value / 2f,
                selectionRequiresTargetLoS = false,
                aimType = AISkillDriver.AimType.AtCurrentEnemy,
                movementType = AISkillDriver.MovementType.StrafeMovetarget,
                targetType = AISkillDriver.TargetType.CurrentEnemy,
                desiredIndex = 2
            };
            AISkillDriverData chaseTarget = new AISkillDriverData
            {
                masterPrefab = anglerEyeMasterPrefab,
                customName = "chaseTarget",
                skillSlot = SkillSlot.None,
                minDistance = 0f,
                maxDistance = Mathf.Infinity,
                selectionRequiresTargetLoS = false,
                aimType = AISkillDriver.AimType.AtCurrentEnemy,
                movementType = AISkillDriver.MovementType.ChaseMoveTarget,
                targetType = AISkillDriver.TargetType.CurrentEnemy,
                desiredIndex = 3
            };
            CreateAISkillDriver(fleeTarget);
            CreateAISkillDriver(usePrimary);
            CreateAISkillDriver(strafeTarget);
            CreateAISkillDriver(chaseTarget);
            ContentAddition.AddEntityState<AnglerEyeSpawnState>(out _);
            ContentAddition.AddEntityState<AnglerEyeFlyState>(out _);
            ContentAddition.AddEntityState<AnglerEyeDeath>(out _);
        }
        public class SummonAnglerEye : BaseSkillState
        {
            private static float baseDuration = 0.25f;
            private float duration;
            private NullifierSummonController controller;
            private int maxSummons = (int)maxAnglers.Value;
            public override void OnEnter()
            {
                base.OnEnter();
                controller = characterBody.GetComponent<NullifierSummonController>();
                if (controller.livingSummons.Count >= maxSummons)
                {
                    outer.SetNextStateToMain();
                    return;
                }
                duration = baseDuration / attackSpeedStat;
                SpawnVoidEye();
            }
            public void SpawnVoidEye()
            {
                if (!base.isAuthority)
                {
                    return;
                }
                MasterSummon summon = new MasterSummon
                {
                    masterPrefab = anglerEyeMasterPrefab,
                    ignoreTeamMemberLimit = true,
                    position = characterBody.transform.position + new Vector3(0f, 12f, 0f)
                };
                summon.rotation = Util.QuaternionSafeLookRotation(inputBank.aimDirection);
                summon.summonerBodyObject = this.gameObject;
                summon.inventoryToCopy = characterBody.inventory;
                summon.useAmbientLevel = null;
                CharacterMaster master = summon.Perform();
                CharacterBody summonedBody = master.GetBody();
                controller.livingSummons.Add(summonedBody);
            }
            public override void OnExit()
            {
                base.OnExit();
            }
            public override void FixedUpdate()
            {
                base.FixedUpdate();
                if (base.fixedAge > duration)
                {
                    outer.SetNextStateToMain();
                }
            }
            public override InterruptPriority GetMinimumInterruptPriority()
            {
                return InterruptPriority.Skill;
            }
        }
        public class AnglerEyeSpawnState : BaseState
        {
            public static float duration = 2.75f;
            public static string spawnSoundString = "Play_nullifier_spawn";
            public static string spawnStateString = "Spawn";
            public override void OnEnter()
            {
                base.OnEnter();
                PlayAnimation("Body", spawnStateString);
                Util.PlayAttackSpeedSound(spawnSoundString, this.gameObject, 2f);
                EffectManager.SimpleMuzzleFlash(portalVFX, this.gameObject, "PortalMuzzle", true);
            }
            public override void FixedUpdate()
            {
                base.FixedUpdate();
                if (base.fixedAge > duration && base.isAuthority)
                {
                    outer.SetNextStateToMain();
                }
            }
            public override InterruptPriority GetMinimumInterruptPriority()
            {
                return InterruptPriority.Death;
            }
        }
        public class AnglerEyeFlyState : FlyState
        {
            private BaseAI baseAI;
            private EntityStateMachine weaponESM;
            private bool currentlyAttacking;
            private bool canSeeTarget;
            private bool idle;
            public override void OnEnter()
            {
                base.OnEnter();
                idle = true;
                baseAI = characterBody.master.GetComponent<BaseAI>();
                weaponESM = characterBody.GetComponents<EntityStateMachine>().Where(esm => esm.customName == "Weapon").FirstOrDefault();
            }
            public override void Update()
            {
                base.Update();
            }
            public override void FixedUpdate()
            {
                base.FixedUpdate();

                if (weaponESM != null)
                {
                    if (weaponESM.state is not EntityStates.Idle)
                    {
                        currentlyAttacking = true;
                        idle = true;
                    }
                    else
                    {
                        currentlyAttacking = false;
                    }
                }
                if (baseAI != null && baseAI.currentEnemy != null && baseAI.currentEnemy.hasLoS)
                {
                    canSeeTarget = true;
                }
                else
                {
                    canSeeTarget = false;
                }
                if (!currentlyAttacking)
                {
                    if (canSeeTarget && idle)
                    {
                        idle = false;
                        PlayCrossfade("Body", "Move", 0.2f);
                    }
                    if (!canSeeTarget && !idle)
                    {
                        idle = true;
                        PlayCrossfade("Body", "Idle", 0.2f);
                    }
                }
            }
            public override bool CanExecuteSkill(GenericSkill skillSlot)
            {
                return base.CanExecuteSkill(skillSlot);
            }
            public override void PerformInputs()
            {
                base.PerformInputs();
            }
        }
        public class AnglerEyeShoot : BaseSkillState
        {
            public static float baseAttackDuration = 1.5f;
            public float attackDuration;
            public static float baseTotalDuration = 2.667f;
            public static float damageCoefficient = anglerBeamDamage.Value / 100f;
            private float totalDuration;
            private BaseAI baseAI;
            private CharacterBody targetBody;
            private GameObject laserIndicatorInstance;
            private Transform eyeMuzzle;
            private LineRenderer laserLineComponent;
            private Vector3 laserEndPosition;
            private float maxRange = anglerMaxRange.Value;
            private bool fired = false;
            private static GameObject muzzleFlashEffectPrefab = Addressables.LoadAssetAsync<GameObject>(RoR2_DLC1_VoidSurvivor.VoidSurvivorBeamMuzzleflash_prefab).WaitForCompletion();
            private static GameObject impactEffectPrefab = Addressables.LoadAssetAsync<GameObject>(RoR2_DLC1_VoidSurvivor.VoidSurvivorBeamImpact_prefab).WaitForCompletion();
            private static GameObject tracerEffectPrefab = Addressables.LoadAssetAsync<GameObject>(RoR2_DLC1_VoidSurvivor.VoidSurvivorBeamTracer_prefab).WaitForCompletion();
            public override void OnEnter()
            {
                base.OnEnter();
                attackDuration = baseAttackDuration / attackSpeedStat;
                totalDuration = baseTotalDuration / attackSpeedStat;
                if (characterBody != null && characterBody.master != null)
                {
                    baseAI = characterBody.master.GetComponent<BaseAI>();
                    if (baseAI != null && baseAI.currentEnemy != null && baseAI.currentEnemy.characterBody != null)
                    {
                        targetBody = baseAI.currentEnemy.characterBody;
                    }
                }
                PlayCrossfade("Body", "Attack", "PlaybackRate.attack1", totalDuration, 0.2f);
                ChildLocator locator = modelLocator.modelTransform.GetComponent<ChildLocator>();
                if (locator != null)
                {
                    eyeMuzzle = locator.FindChild("EyeMuzzle");
                }
                if (eyeMuzzle != null)
                {
                    laserIndicatorInstance = Instantiate(anglerEyeLaserIndicator, eyeMuzzle.position, eyeMuzzle.rotation);
                }
                if (laserIndicatorInstance != null)
                {
                    if (!laserIndicatorInstance.activeInHierarchy)
                    {
                        laserIndicatorInstance.SetActive(true);
                    }
                    if (laserIndicatorInstance.transform.parent != eyeMuzzle)
                    {
                        laserIndicatorInstance.transform.SetParent(eyeMuzzle);
                    }
                    laserLineComponent = laserIndicatorInstance.GetComponent<LineRenderer>();
                    laserLineComponent.startWidth = 0.4f;
                    laserLineComponent.endWidth = 0.4f;
                    UpdateLaserEndPoint();
                    UpdateLaserVisualPosition();
                }
            }
            public override void FixedUpdate()
            {
                base.FixedUpdate();
                UpdateLaserEndPoint();
                if (base.fixedAge > attackDuration && !fired)
                {
                    fired = true;
                    Destroy(laserIndicatorInstance);
                    laserIndicatorInstance = null;
                    laserLineComponent = null;
                    Util.PlaySound("Play_voidman_m1_shoot", gameObject);
                    if (muzzleFlashEffectPrefab != null)
                    {
                        EffectManager.SimpleMuzzleFlash(muzzleFlashEffectPrefab, base.gameObject, "EyeMuzzle", false);
                    }
                    if (base.isAuthority)
                    {
                        DamageTypeCombo combo = new DamageTypeCombo { damageSource = DamageSource.Primary, damageType = DamageType.Generic };
                        Ray aimRay = GetAimRay();
                        BulletAttack bulletAttack = new BulletAttack
                        {
                            owner = base.gameObject,
                            weapon = base.gameObject,
                            origin = aimRay.origin,
                            aimVector = aimRay.direction,
                            muzzleName = "EyeMuzzle",
                            maxDistance = maxRange,
                            minSpread = 0f,
                            maxSpread = 0f,
                            radius = 0.1f,
                            falloffModel = BulletAttack.FalloffModel.None,
                            smartCollision = true,
                            damage = damageCoefficient * damageStat,
                            procCoefficient = 1f,
                            force = 1000f,
                            isCrit = RollCrit(),
                            damageType = combo,
                            tracerEffectPrefab = tracerEffectPrefab,
                            hitEffectPrefab = impactEffectPrefab
                        };
                        bulletAttack.Fire();
                    }
                }
                if (base.fixedAge > totalDuration)
                {
                    outer.SetNextStateToMain();
                }
            }
            public override void Update()
            {
                base.Update();
                UpdateLaserVisualPosition();
            }
            public void UpdateLaserVisualPosition()
            {
                if (laserLineComponent == null || eyeMuzzle == null)
                {
                    return;
                }
                if (base.fixedAge > attackDuration / 2f)
                {
                    bool flash = base.fixedAge % 0.1f > 0.05f;
                    float lineWidth = flash ? 0.1f : 0.5f;
                    laserLineComponent.startWidth = lineWidth;
                    laserLineComponent.endWidth = lineWidth;
                }

                laserLineComponent.SetPosition(0, eyeMuzzle.position);
                laserLineComponent.SetPosition(1, laserEndPosition);
            }
            public void UpdateLaserEndPoint()
            {
                if (targetBody != null && laserIndicatorInstance != null)
                {
                    Ray aimRay = GetAimRay();
                    Vector3 targetBodyPosition = targetBody.corePosition;
                    bool success = Physics.Raycast(aimRay.origin, aimRay.direction, out RaycastHit hit, maxRange, LayerIndex.CommonMasks.laser);
                    if (success)
                    {
                        laserEndPosition = hit.point;
                    }
                    else
                    {
                        laserEndPosition = aimRay.GetPoint(maxRange);
                    }
                }
            }
            public override void OnExit()
            {
                base.OnExit();
                if (laserIndicatorInstance != null)
                {
                    Destroy(laserIndicatorInstance);
                }
            }
            public override InterruptPriority GetMinimumInterruptPriority()
            {
                return InterruptPriority.PrioritySkill;
            }
        }
        public class AnglerEyeDeath : GenericCharacterDeath
        {
            private static GameObject deathVFX = Addressables.LoadAssetAsync<GameObject>(RoR2_Base_Jellyfish.JellyfishDeath_prefab).WaitForCompletion();
            public override void OnEnter()
            {
                base.OnEnter();
                maxFallDuration = 3f;
                ChildLocator locator = modelLocator.modelTransform.GetComponent<ChildLocator>();
                Transform eyeBeamTransform = locator.FindChild("EyeBeam");
                Light light = eyeBeamTransform.gameObject.GetComponent<Light>();
                light.enabled = false;
                Transform lureBaseTransform = locator.FindChild("LureBase");
                DynamicBone bone = lureBaseTransform.gameObject.GetComponent<DynamicBone>();
                bone.m_Stiffness = 0.01f;
                bone.m_Damping = 0.01f;
                bone.m_Elasticity = 0.05f;
            }
            public override void FixedUpdate()
            {
                base.FixedUpdate();
                float xVelocity = rigidbody.velocity.x;
                float yVelocity = rigidbody.velocity.y;
                float zVelocity = rigidbody.velocity.z;

                float newXVelocity = xVelocity * 0.8f;
                float newZVelocity = zVelocity * 0.8f;
                float newYVelocity = yVelocity < 4f ? yVelocity + 1f : yVelocity;

                rigidbody.velocity = new Vector3(newXVelocity, newYVelocity, newZVelocity);
            }
            public override void OnPreDestroyBodyServer()
            {
                base.OnPreDestroyBodyServer();
                Util.PlaySound("Play_jellyfish_death", gameObject);
                EffectManager.SimpleEffect(deathVFX, characterBody.corePosition, characterBody.transform.rotation, false);

            }
        }
        public class NullifierSummonController : MonoBehaviour
        {
            public List<CharacterBody> livingSummons;
            public void FixedUpdate()
            {
                for (int i = livingSummons.Count - 1; i >= 0; i--)
                {
                    CharacterBody body = livingSummons[i];
                    if (body == null || body.healthComponent == null || body.healthComponent.alive == false)
                    {
                        if (body == null || body.healthComponent == null || body.healthComponent.alive == false)
                        {
                            livingSummons.Remove(body);
                        }
                    }
                }
            }
            public void KillAllSumons()
            {
                if (livingSummons.Count > 0)
                {
                    for (int i = livingSummons.Count - 1; i >= 0; i--)
                    {
                        CharacterBody body = livingSummons[i];
                        if (body != null && body.healthComponent != null)
                        {
                            body.healthComponent.Suicide();
                        }
                    }
                }
            }
        }
    }
}
