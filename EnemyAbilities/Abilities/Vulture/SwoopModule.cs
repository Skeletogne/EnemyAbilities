using System.Linq;
using EntityStates;
using EntityStates.Vulture;
using RoR2.CharacterAI;
using JetBrains.Annotations;
using R2API;
using RoR2;
using RoR2.Skills;
using RoR2BepInExPack.GameAssetPaths.Version_1_35_0;
using UnityEngine;
using UnityEngine.AddressableAssets;
using static EnemyAbilities.PluginConfig;

namespace EnemyAbilities.Abilities.Vulture
{
    [EnemyAbilities.ModuleInfo("Swoop", "Gives Alloy Vultures a new secondary:\n- Swoop: Allows Alloy Vultures to swoop towards the player in an arc, dealing contact damage.", "Alloy Vulture", true)]
    public class SwoopModule : BaseModule
    {
        private static GameObject bodyPrefab = Addressables.LoadAssetAsync<GameObject>(RoR2_Base_Vulture.VultureBody_prefab).WaitForCompletion();
        private static GameObject masterPrefab = Addressables.LoadAssetAsync<GameObject>(RoR2_Base_Vulture.VultureMaster_prefab).WaitForCompletion();
        public override void Awake()
        {
            base.Awake();
            TargetingAndPredictionController controller = bodyPrefab.AddComponent<TargetingAndPredictionController>();
            controller.manualTrackingMaxDistance = 100f;
            controller.manualTrackingMaxAngle = 360f;
            CreateSkill();
            AddMeleeHitBox();
        }
        private void AddMeleeHitBox()
        {
            ModelLocator modelLocator = bodyPrefab.GetComponent<ModelLocator>();
            if (modelLocator != null && modelLocator.modelTransform != null)
            {
                CreateHitBoxAndGroup(modelLocator.modelTransform, "VultureMelee", new Vector3(0f, -2f, 0f), new Vector3(30f, 30f, 30f) * (vultureSwoopHitBoxScale.Value / 100f));
            }
        }
        private void CreateHitBoxAndGroup(Transform modelTransform, string hitBoxGroupName, Vector3 localPosition, Vector3 localScale, string hitBoxGroupObjName = "", string hitBoxObjName = "")
        {
            hitBoxGroupObjName = hitBoxGroupObjName == "" ? hitBoxGroupName : hitBoxGroupObjName;
            hitBoxObjName = hitBoxObjName == "" ? hitBoxGroupName : hitBoxObjName;

            GameObject hitBoxObj = new GameObject(hitBoxGroupObjName);
            hitBoxObj.transform.SetParent(modelTransform);
            hitBoxObj.transform.localPosition = localPosition;
            hitBoxObj.transform.localScale = localScale;
            hitBoxObj.transform.localRotation = Quaternion.identity;

            HitBox hitBox = hitBoxObj.AddComponent<HitBox>();
            hitBox.gameObject.name = hitBoxObjName;

            HitBoxGroup hitBoxGroup = modelTransform.gameObject.AddComponent<HitBoxGroup>();
            hitBoxGroup.groupName = hitBoxGroupName;
            hitBoxGroup.hitBoxes = new HitBox[] { hitBox };
        }
        public void CreateSkill()
        {
            SwoopSkillDef swoop = ScriptableObject.CreateInstance<SwoopSkillDef>();
            (swoop as ScriptableObject).name = "VultureBodySwoop";
            swoop.skillName = "VultureSwoop";
            swoop.activationStateMachineName = "Body";
            swoop.activationState = ContentAddition.AddEntityState<SwoopWindup>(out _);
            swoop.baseRechargeInterval = vultureSwoopCooldown.Value;
            swoop.cancelSprintingOnActivation = true;
            swoop.isCombatSkill = true;
            ContentAddition.AddSkillDef(swoop);

            SkillFamily skillFamily = ScriptableObject.CreateInstance<SkillFamily>();
            (skillFamily as ScriptableObject).name = "VultureSecondaryFamily";
            skillFamily.variants = [new SkillFamily.Variant(){ skillDef = swoop }];
            
            GenericSkill skill = bodyPrefab.AddComponent<GenericSkill>();
            skill.skillName = "VultureSwoop";
            skill._skillFamily = skillFamily;

            SkillLocator locator = bodyPrefab.GetComponent<SkillLocator>();
            locator.secondary = skill;

            ContentAddition.AddSkillFamily(skillFamily);

            AISkillDriver useSecondary = masterPrefab.AddComponent<AISkillDriver>();
            useSecondary.customName = "strafeAndUseSecondary";
            useSecondary.skillSlot = SkillSlot.Secondary;
            useSecondary.requiredSkill = swoop;
            useSecondary.requireSkillReady = true;
            useSecondary.minDistance = 25f;
            useSecondary.maxDistance = 50f;
            useSecondary.moveTargetType = AISkillDriver.TargetType.CurrentEnemy;
            useSecondary.movementType = AISkillDriver.MovementType.StrafeMovetarget;
            masterPrefab.ReorderSkillDrivers(useSecondary, 2);

            ContentAddition.AddEntityState<Swoop>(out _);
        }
    }
    public class SwoopSkillDef : SkillDef
    {
        private class InstanceData : BaseSkillInstanceData
        {
            public CharacterBody body;
            public EntityStateMachine flightEsm;
        }
        public override BaseSkillInstanceData OnAssigned(GenericSkill skillSlot)
        {
            CharacterBody body = skillSlot.characterBody;
            EntityStateMachine flightEsm = body.GetComponents<EntityStateMachine>().FirstOrDefault(esm => esm.customName == "Flight");
            return new InstanceData
            {
                body = body,
                flightEsm = flightEsm
            };
        }
        public override bool IsReady([NotNull] GenericSkill skillSlot)
        {
            var data = skillSlot.skillInstanceData as InstanceData;
            if (data?.flightEsm == null || data.body == null)
            {
                return false;
            }
            if (data.flightEsm.state is not Fly)
            {
                return false;
            }
            if (Physics.Raycast(data.body.corePosition, Vector3.down, 8f, LayerIndex.world.mask, QueryTriggerInteraction.Ignore))
            {
                return false;
            }
            if (data.body.characterMotor != null && data.body.characterMotor.isGrounded)
            {
                return false;
            }
            return base.IsReady(skillSlot);
        }
    }
    public class SwoopWindup : BaseSkillState
    {
        private float duration;
        private static float baseDuration = 0.5f;
        private Vector3 targetLocation;
        private BaseAI ai;
        private bool success;
        private GameObject jumpEffect = Addressables.LoadAssetAsync<GameObject>(RoR2_Base_Vulture.VultureJumpEffect_prefab).WaitForCompletion();
        public override void OnEnter()
        {
            base.OnEnter();
            duration = baseDuration / attackSpeedStat;
            skillLocator.utility.SetSkillOverride(gameObject, CharacterBody.CommonAssets.disabledSkill, GenericSkill.SkillOverridePriority.Contextual);
            CharacterMaster master = characterBody.master;
            if (master != null)
            {
                ai = master.gameObject.GetComponent<BaseAI>();
            }
            if (ai != null && ai.currentEnemy != null && ai.currentEnemy.characterBody != null)
            {
                targetLocation = ai.currentEnemy.characterBody.transform.position;
            }
            Vector3 movementDirection = characterBody.corePosition - targetLocation;
            movementDirection.y = 0f;
            movementDirection.Normalize();
            if (characterMotor != null)
            {
                EffectManager.SpawnEffect(jumpEffect, new EffectData { scale = 2f, origin = characterBody.footPosition, rotation = Util.QuaternionSafeLookRotation(Vector3.down) }, true);
                characterMotor.velocity = movementDirection * 10f + new Vector3(0f, 25f, 0f);
            }
        }
        public override void OnExit()
        {
            base.OnExit();
            if (!success)
            {
                skillLocator.utility.UnsetSkillOverride(gameObject, CharacterBody.CommonAssets.disabledSkill, GenericSkill.SkillOverridePriority.Contextual);
            }
        }
        public override void FixedUpdate()
        {
            base.FixedUpdate();
            if (characterMotor != null && characterMotor.isGrounded)
            {
                outer.SetNextStateToMain();
                return;
            }
            if (base.fixedAge > duration)
            {
                if (ai.currentEnemy.characterBody.transform.position.y > characterBody.transform.position.y)
                {
                    outer.SetNextStateToMain();
                    return;
                }
                success = true;
                outer.SetNextState(new Swoop());
            }
        }
        public override void Update()
        {
            base.Update();
        }
        public override InterruptPriority GetMinimumInterruptPriority()
        {
            return InterruptPriority.PrioritySkill;
        }
    }
    public class Swoop : BaseSkillState
    {
        //total time if uninterrupted is prediction + swoop * 2 + recover
        public static float predictionDuration = 0.5f;
        private static float swoopDurationTilTarget = vultureSwoopPredictionTime.Value;
        private static float recoverDuration = 0.5f;
        private static GameObject slashEffect = Addressables.LoadAssetAsync<GameObject>(RoR2_Base_Croco.CrocoSlash_prefab).WaitForCompletion();
        private static GameObject hitEffect = Addressables.LoadAssetAsync<GameObject>(RoR2_Base_Common_VFX.OmniImpactVFXSlash_prefab).WaitForCompletion();
        private GameObject slashEffectInstance;
        private EffectManagerHelper _emh_slashEffectInstance;
        private GameObject jumpEffect = Addressables.LoadAssetAsync<GameObject>(RoR2_Base_Vulture.VultureJumpEffect_prefab).WaitForCompletion();

        private static float effectInterval = 0.25f;
        private float effectTimer;

        private static int maxSwingCount = 5;
        private int swingCount = 0;
        private bool startLeftFoot;

        //time after swoop begins
        private static float firstSwingDelay = 1.1f;
        private static float swingInterval = 0.2f;
        private float nextSwingTime;
        private float recoverTimer = 0f;

        private float swoopStopwatch = 0f;
        private float swoopFullDuration = 3f; //changed to swoopDurationTilTarget * 2f;

        private TargetingAndPredictionController targetController;
        private Transform trackedTarget;
        private Vector3 vultureStartPosition;
        private Vector3 finalTarget;

        private bool madePrediction = false;
        private bool finishedSwoop = false;
        private bool pastTarget = false;

        private float damageCoefficient = vultureSwoopDamageCoefficient.Value / 100f;
        private float procCoefficient = 1f;

        private float crashAngle = 40f;

        private OverlapAttack attack;

        private int _origLayer;

        public override void OnEnter()
        {
            base.OnEnter();
            DisableCollision();
            if (characterMotor != null)
            {
                characterMotor.onMovementHit += OnMovementHit;
            }
            nextSwingTime = firstSwingDelay;
            if (characterMotor != null && characterMotor.isGrounded)
            {
                outer.SetNextStateToMain();
                return;
            }
            swoopFullDuration = swoopDurationTilTarget * 2f;
            targetController = GetComponent<TargetingAndPredictionController>();
            if (targetController != null && base.isAuthority)
            {
                trackedTarget = targetController.StartPredictTarget(OnTargetLost);
            }
            startLeftFoot = UnityEngine.Random.RandomRangeInt(0, 2) == 1;


            attack = new OverlapAttack();
            attack.attacker = base.gameObject;
            attack.inflictor = base.gameObject;
            attack.teamIndex = teamComponent.teamIndex;
            attack.damage = damageCoefficient * damageStat;
            attack.hitEffectPrefab = hitEffect;
            attack.isCrit = RollCrit();
            attack.procCoefficient = procCoefficient;
            DamageTypeCombo combo = new DamageTypeCombo { damageSource = DamageSource.Secondary, damageType = (vultureSwoopInflictsBleed.Value ? DamageType.BleedOnHit : DamageType.Generic) };
            attack.damageType = combo;
            attack.hitBoxGroup = FindHitBoxGroup("VultureMelee");
            attack.forceVector = base.characterDirection.forward * 500f;
        }
        private void OnMovementHit(ref CharacterMotor.MovementHitInfo movementHitInfo)
        {
            if (characterMotor.velocity.magnitude < 5f)
            {
                return;
            }
            Vector3 movementDirection = characterMotor.velocity.normalized;
            Vector3 hitNormal = movementHitInfo.hitNormal;
            float impactAngle = Vector3.Angle(movementDirection, hitNormal);
            if (impactAngle > 180f - crashAngle)
            {
                SetStateOnHurt state = characterBody.gameObject.GetComponent<SetStateOnHurt>();
                if (state != null)
                {
                    state.SetStun(vultureSwoopStunDurationOnImpact.Value);
                }
            }
        }
        public override void OnExit()
        {
            base.OnExit();
            skillLocator.utility.UnsetSkillOverride(gameObject, CharacterBody.CommonAssets.disabledSkill, GenericSkill.SkillOverridePriority.Contextual);
            characterMotor.velocity = new Vector3(0f, 0f, 0f);
            characterMotor.onMovementHit -= OnMovementHit;
            EnableCollision();

            if (_emh_slashEffectInstance != null && _emh_slashEffectInstance.OwningPool != null)
            {
                _emh_slashEffectInstance.OwningPool.ReturnObject(_emh_slashEffectInstance);
            }
            else if ((bool)slashEffectInstance)
            {
                EntityState.Destroy(slashEffectInstance);
            }
            slashEffectInstance = null;
            _emh_slashEffectInstance = null;
        }
        public override void FixedUpdate()
        {
            base.FixedUpdate();

            if (!madePrediction)
            {
                //dampens velocity
                characterMotor.velocity /= 1.2f;
            }

            if (base.fixedAge > predictionDuration && !madePrediction)
            {
                if (!targetController.GetPredictionPositionConsumePredictor(swoopDurationTilTarget, out Vector3 pos))
                {
                    pos = trackedTarget.position;
                }
                finalTarget = pos + new Vector3(0f, 4f, 0f);
                vultureStartPosition = characterBody.corePosition;
                if (finalTarget.y > vultureStartPosition.y)
                {
                    outer.SetNextStateToMain();
                    return;
                }
                madePrediction = true;
                swoopStopwatch = 0f;
            }
            if (madePrediction && !finishedSwoop)
            {
                if (characterMotor.isGrounded)
                {
                    characterMotor.Motor.ForceUnground();
                }
                swoopStopwatch += Time.fixedDeltaTime;

                float xDisplacement = finalTarget.x - vultureStartPosition.x;
                float yDisplacement = finalTarget.y - vultureStartPosition.y;
                float zDisplacement = finalTarget.z - vultureStartPosition.z;

                float xAcceleration = (2 * xDisplacement) / Mathf.Pow(swoopDurationTilTarget, 2f);
                float zAcceleration = (2 * zDisplacement) / Mathf.Pow(swoopDurationTilTarget, 2f);
                float yAcceleration = (2 * yDisplacement) / Mathf.Pow(swoopDurationTilTarget, 2f);

                float xVelocity = 0f;
                float yVelocity = 0f;
                float zVelocity = 0f;

                if (!pastTarget)
                {
                    xVelocity = xAcceleration * swoopStopwatch;
                    yVelocity = yAcceleration * (swoopDurationTilTarget - swoopStopwatch);
                    zVelocity = zAcceleration * swoopStopwatch;
                }
                if (pastTarget)
                {
                    xVelocity = xAcceleration * (swoopFullDuration - swoopStopwatch);
                    yVelocity = -(yAcceleration * (swoopStopwatch - swoopDurationTilTarget));
                    zVelocity = zAcceleration * (swoopFullDuration - swoopStopwatch);
                }
                if (base.isAuthority && swoopStopwatch > firstSwingDelay && swoopStopwatch < firstSwingDelay + maxSwingCount * swingInterval)
                {
                    attack.Fire();
                }
                if (swoopStopwatch > nextSwingTime && swingCount < maxSwingCount)
                {

                    nextSwingTime += swingInterval;
                    swingCount++;
                    SwingEffect();

                }
                Vector3 velocity = new Vector3(xVelocity, yVelocity, zVelocity);
                characterMotor.velocity = velocity;
                if (characterDirection != null)
                {
                    characterDirection.moveVector = velocity.normalized;
                }
                effectTimer -= Time.fixedDeltaTime;
                if (effectTimer < 0)
                {
                    effectTimer += effectInterval;
                    EffectManager.SpawnEffect(jumpEffect, new EffectData { scale = 2f, origin = characterBody.footPosition, rotation = Util.QuaternionSafeLookRotation(characterMotor.velocity) }, true);
                }

            }
            if (swoopStopwatch > swoopDurationTilTarget)
            {
                pastTarget = true;
            }
            if (swoopStopwatch > swoopFullDuration)
            {
                finishedSwoop = true;
                characterMotor.velocity /= 1.2f;
                recoverTimer += Time.fixedDeltaTime;
                if (recoverTimer > recoverDuration)
                {
                    outer.SetNextStateToMain();
                }
            }
        }
        private void OnTargetLost()
        {
            if (base.isAuthority)
            {
                outer.SetNextStateToMain();
            }
        }
        private void SwingEffect()
        {
            Util.PlaySound("Play_acrid_m1_slash", gameObject);
            string muzzle = "";
            int i = (swingCount - 1) % 2;
            if (i == (startLeftFoot ? 0 : 1))
            {
                muzzle = "FootL";
            }
            else
            {
                muzzle = "FootR";
            }
            if (!slashEffect)
            {
                return;
            }
            Transform transform = FindModelChild(muzzle);

            if ((bool)transform)
            {
                if (!EffectManager.ShouldUsePooledEffect(slashEffect))
                {
                    if (slashEffectInstance != null)
                    {
                        EntityState.Destroy(slashEffectInstance);
                        slashEffectInstance = null;
                    }
                    slashEffectInstance = UnityEngine.Object.Instantiate(slashEffect, transform);
                    slashEffectInstance.transform.localScale = Vector3.one;
                }
                else
                {
                    if (_emh_slashEffectInstance != null && _emh_slashEffectInstance.OwningPool != null)
                    {
                        _emh_slashEffectInstance.OwningPool.ReturnObject(_emh_slashEffectInstance);
                        _emh_slashEffectInstance = null;
                    }
                    _emh_slashEffectInstance = EffectManager.GetAndActivatePooledEffect(slashEffect, transform, inResetLocal: true);
                    slashEffectInstance = _emh_slashEffectInstance.gameObject;
                    if (slashEffectInstance == null)
                    {
                        return;
                    }
                    slashEffectInstance.transform.localScale = Vector3.one;
                }
                ScaleParticleSystemDuration component = slashEffectInstance.GetComponent<ScaleParticleSystemDuration>();
                if ((bool)component)
                {
                    component.newDuration = component.initialDuration;
                }
            }

        }
        public override InterruptPriority GetMinimumInterruptPriority()
        {
            return InterruptPriority.PrioritySkill;
        }
        private void DisableCollision()
        {
            if (characterMotor != null)
            {
                _origLayer = base.gameObject.layer;
                base.gameObject.layer = LayerIndex.GetAppropriateFakeLayerForTeam(base.teamComponent.teamIndex).intVal;
                base.characterMotor.Motor.RebuildCollidableLayers();
            }
        }
        private void EnableCollision()
        {
            if (characterMotor != null)
            {
                base.gameObject.layer = _origLayer;
                base.characterMotor.Motor.RebuildCollidableLayers();
            }
        }
    }
}
