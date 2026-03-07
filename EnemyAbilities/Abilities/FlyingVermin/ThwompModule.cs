using RoR2.CharacterAI;
using JetBrains.Annotations;
using RoR2;
using RoR2.Skills;
using UnityEngine;
using UnityEngine.AddressableAssets;
using RoR2BepInExPack.GameAssetPaths.Version_1_39_0;
using EntityStates;
using R2API;
using BepInEx.Configuration;

namespace EnemyAbilities.Abilities.FlyingVermin
{

    [EnemyAbilities.ModuleInfo("Thwomp Stomp", "Gives Blind Pests a new secondary:\n- Thwomp Stomp: Allows them to fly downwards quickly, releasing an explosion when they land. They enter a grounded state for 3 seconds afterwards.", "Blind Pest", true)]
    public class ThwompModule : BaseModule
    {
        private static GameObject bodyPrefab = Addressables.LoadAssetAsync<GameObject>(RoR2_DLC1_FlyingVermin.FlyingVerminBody_prefab).WaitForCompletion();
        private static GameObject masterPrefab = Addressables.LoadAssetAsync<GameObject>(RoR2_DLC1_FlyingVermin.FlyingVerminMaster_prefab).WaitForCompletion();

        internal static ConfigEntry<float> warningDuration;
        internal static ConfigEntry<float> recoveryDuration;
        internal static ConfigEntry<float> groundedDuration;
        internal static ConfigEntry<float> damageCoeff;
        internal static ConfigEntry<float> radius;
        internal static ConfigEntry<float> cooldown;

        public override void RegisterConfig()
        {
            base.RegisterConfig();
            warningDuration = BindFloat("Warning Duration", 0.85f, "The duration that the indicator appears before the Blind Pest begins it's attack.", 0.5f, 1.5f, 0.05f, PluginConfig.FormatType.Time);
            recoveryDuration = BindFloat("Recovery Duration", 1.25f, "The duration that the Blind Pest is unable to select another attack for after landing", 0.5f, 1.5f, 0.05f, PluginConfig.FormatType.Time);
            groundedDuration = BindFloat("Grounded Duration", 3f, "The duration that the Blind Pest is forced into its grounded state for, before it becomes able to fly again", 1f, 10f, 0.1f, PluginConfig.FormatType.Time);
            damageCoeff = BindFloat("Damage Coefficient", 250f, "The damage multiplier to the Blind Pest's damage to get explosion damage. Uses falloff model sweet spot", 100f, 500f, 5f, PluginConfig.FormatType.Percentage);
            radius = BindFloat("Radius", 6f, "The explosion radius of the poison blast", 4f, 12f, 1f, PluginConfig.FormatType.Distance);
            cooldown = BindFloat("Cooldown", 8f, "The cooldown of the ability", 4f, 20f, 0.1f, PluginConfig.FormatType.Time);
        }
        public override void Initialise()
        {
            base.Initialise();
            bodyPrefab.AddComponent<ThwompController>();
            CreateSkill();
        }
        public void CreateSkill()
        {
            SkillDefData skillDefData = new SkillDefData
            {
                objectName = "FlyingVerminBodyThwompStomp",
                skillName = "FlyingVerminThwompStomp",
                esmName = "Body",
                activationState = ContentAddition.AddEntityState<ThwompStomp>(out _),
                cooldown = cooldown.Value,
                combatSkill = true
            };
            ThwompSkillDef thwomp = CreateSkillDef<ThwompSkillDef>(skillDefData);
            CreateGenericSkill(bodyPrefab, thwomp.skillName, "FlyingVerminSecondaryFamily", thwomp, SkillSlot.Secondary);
            AISkillDriverData driverData1 = new AISkillDriverData
            {
                masterPrefab = masterPrefab,
                customName = "useSecondary",
                skillSlot = SkillSlot.Secondary,
                requireReady = true,
                minDistance = 10f,
                maxDistance = 40f,
                movementType = AISkillDriver.MovementType.StrafeMovetarget,
                aimType = AISkillDriver.AimType.AtCurrentEnemy,
                targetType = AISkillDriver.TargetType.CurrentEnemy,
                desiredIndex = 1
            };
            CreateAISkillDriver(driverData1);
            AISkillDriverData driverData2 = new AISkillDriverData
            {
                masterPrefab = masterPrefab,
                customName = "fleeAtCloseRange",
                skillSlot = SkillSlot.None,
                minDistance = 0f,
                maxDistance = 5f,
                targetType = AISkillDriver.TargetType.CurrentEnemy,
                movementType = AISkillDriver.MovementType.FleeMoveTarget,
                aimType = AISkillDriver.AimType.AtCurrentEnemy,
                desiredIndex = 0
            };
            CreateAISkillDriver(driverData2);
        }
    }
    public class ThwompSkillDef : SkillDef
    {
        private class InstanceData : BaseSkillInstanceData
        {
            public ThwompController controller;
        }
        public override BaseSkillInstanceData OnAssigned([NotNull] GenericSkill skillSlot)
        {
            return new InstanceData
            {
                controller = skillSlot.characterBody.gameObject.GetComponent<ThwompController>()
            };
        }
        public override bool IsReady([NotNull] GenericSkill skillSlot)
        {
            InstanceData data = skillSlot.skillInstanceData as InstanceData;
            if (data?.controller == null)
            {
                return false;
            }
            return data.controller.foundTarget && base.IsReady(skillSlot);
        }
    }
    public class ThwompStomp : BaseSkillState
    {
        private float warningDuration;
        private static float baseWarningDuration = ThwompModule.warningDuration.Value;
        private bool thwompingIt;
        private bool hitGround;
        private static float baseRecoveryDuration = ThwompModule.recoveryDuration.Value;
        private float recoveryDuration;
        private float recoveryStopwatch = 0f;
        private static float damageCoefficient = ThwompModule.damageCoeff.Value / 100f;
        private static float force = 1000f;
        private static Vector3 bonusForce = new Vector3(0f, 1000f, 0f);
        private static float procCoefficient = 1f;
        private static float radius = ThwompModule.radius.Value;
        private ThwompController controller;
        private static GameObject blastEffect = Addressables.LoadAssetAsync<GameObject>(RoR2_Base_Croco.CrocoLeapExplosion_prefab).WaitForCompletion();
        private static GameObject indicatorPrefab = Addressables.LoadAssetAsync<GameObject>(RoR2_Base_Common.TeamAreaIndicator__GroundOnly_prefab).WaitForCompletion();
        private GameObject indicatorInstance;
        public override void OnEnter()
        {
            base.OnEnter();
            Util.PlaySound("Play_flyingVermin_spawn", base.gameObject);
            warningDuration = baseWarningDuration / attackSpeedStat;
            recoveryDuration = baseRecoveryDuration / attackSpeedStat;
            controller = characterBody.gameObject.GetComponent<ThwompController>();
            indicatorInstance = UnityEngine.Object.Instantiate(indicatorPrefab, GetIndicatorPos(), Util.QuaternionSafeLookRotation(Vector3.up));
            UpdateIndicator();
            TeamAreaIndicator teamAreaIndicator = indicatorInstance.GetComponent<TeamAreaIndicator>();
            if (teamAreaIndicator != null)
            {
                teamAreaIndicator.teamComponent = teamComponent;
            }
            //create indicator?
        }
        public override void OnExit()
        {
            base.OnExit();
            if (thwompingIt)
            {
                characterMotor.onMovementHit -= OnMovementHit;
            }
            TryDestroyIndicator();
        }
        public void TryDestroyIndicator()
        {
            if (indicatorInstance != null)
            {
                UnityEngine.Object.Destroy(indicatorInstance);
            }
        }
        public void UpdateIndicator()
        {
            if (indicatorInstance != null)
            {
                Vector3 newPosition = GetIndicatorPos();
                indicatorInstance.transform.position = newPosition;
                float scaleModifier = 0.1f + Mathf.Sqrt(0.9f * Mathf.Clamp01(base.fixedAge / warningDuration));
                indicatorInstance.transform.localScale = Vector3.one * radius * scaleModifier;
            }
        }
        public Vector3 GetIndicatorPos()
        {
            Vector3 vector = characterBody.transform.position;
            bool success = Physics.Raycast(characterBody.transform.position, Vector3.down, out RaycastHit hit, 1000f, LayerIndex.world.mask, QueryTriggerInteraction.Ignore);
            if (success)
            {
                vector = hit.point;
            }
            return vector;
        }
        public override void FixedUpdate()
        {
            base.FixedUpdate();
            if (indicatorInstance != null)
            {
                UpdateIndicator();
            }
            if (base.fixedAge > warningDuration && !thwompingIt)
            {

                thwompingIt = true;
                characterMotor.onMovementHit += OnMovementHit;
            }
            if (thwompingIt && !hitGround)
            {
                if (characterMotor.velocity.y > -50f)
                {
                    characterMotor.velocity.y -= 5f;
                }
            }
            if (hitGround == true)
            {
                recoveryStopwatch += Time.fixedDeltaTime;
                if (recoveryStopwatch > recoveryDuration)
                {
                    outer.SetNextStateToMain();
                }
            }
        }
        private void OnMovementHit(ref CharacterMotor.MovementHitInfo movementHitInfo)
        {
            if (thwompingIt && !hitGround)
            {
                TryDestroyIndicator();
                hitGround = true;
                characterMotor.velocity = Vector3.zero;
                if (base.isAuthority)
                {
                    DamageTypeCombo combo = new DamageTypeCombo { damageSource = DamageSource.Secondary, damageType = DamageType.Generic };
                    BlastAttack blastAttack = new BlastAttack();
                    blastAttack.attacker = characterBody.gameObject;
                    blastAttack.inflictor = characterBody.gameObject;
                    blastAttack.position = characterBody.corePosition;
                    blastAttack.baseForce = force;
                    blastAttack.bonusForce = bonusForce;
                    blastAttack.radius = radius;
                    blastAttack.attackerFiltering = AttackerFiltering.NeverHitSelf;
                    blastAttack.procCoefficient = procCoefficient;
                    blastAttack.baseDamage = damageCoefficient * damageStat;
                    blastAttack.crit = RollCrit();
                    blastAttack.damageColorIndex = DamageColorIndex.Default;
                    blastAttack.damageType = combo;
                    blastAttack.teamIndex = teamComponent.teamIndex;
                    blastAttack.falloffModel = BlastAttack.FalloffModel.SweetSpot;
                    blastAttack.Fire();
                }
                controller.savedBaseJumpCount = characterBody.baseJumpCount;
                controller.savedMaxJumpCount = characterBody.maxJumpCount;
                characterBody.baseJumpCount = 0;
                characterBody.maxJumpCount = 0;
                characterMotor.useGravity = true;
                controller.shouldObeyGravity = true;
                EffectManager.SpawnEffect(blastEffect, new EffectData { origin = characterBody.corePosition, scale = radius }, true);
            }
        }

        public override InterruptPriority GetMinimumInterruptPriority()
        {
            return InterruptPriority.PrioritySkill;
        }
    }
    public class ThwompController : MonoBehaviour
    {
        private CharacterBody pestBody;
        //starts at 1 second so it doesn't instantly try to thwomp the player
        private float checkTimer = 1f;
        private float checkInterval = 0.1f;

        private float minimumDistance = 10f;
        private float maximumDistance = 40f;
        public bool foundTarget = false;
        //slightly bigger than actual ability radius for baiting
        private float radius = 3f + ThwompModule.radius.Value;
        private float gravityTimer;
        private float gravityDuration = ThwompModule.groundedDuration.Value;
        public int savedBaseJumpCount;
        public int savedMaxJumpCount;

        public bool shouldObeyGravity;

        public void Awake()
        {
            pestBody = GetComponent<CharacterBody>();
        }
        public void FixedUpdate()
        {
            checkTimer -= Time.fixedDeltaTime;
            if (checkTimer < 0)
            {
                foundTarget = false;
                bool groundInRange = false;
                checkTimer += checkInterval;
                bool raycastSuccess = Physics.Raycast(pestBody.transform.position, Vector3.down, out RaycastHit hit, maximumDistance, LayerIndex.world.mask, QueryTriggerInteraction.Ignore);
                if (raycastSuccess && hit.distance > minimumDistance)
                {
                    groundInRange = true;
                }
                if (groundInRange)
                {
                    Vector3 origin = hit.point;
                    TeamMask teamMask = TeamMask.GetEnemyTeams(pestBody.teamComponent.teamIndex);
                    SphereSearch sphereSearch = new SphereSearch();
                    sphereSearch.origin = origin;
                    sphereSearch.radius = radius;
                    sphereSearch.queryTriggerInteraction = QueryTriggerInteraction.Ignore;
                    sphereSearch.mask = LayerIndex.entityPrecise.mask;
                    sphereSearch.RefreshCandidates();
                    sphereSearch.FilterCandidatesByDistinctHurtBoxEntities();
                    sphereSearch.FilterCandidatesByHurtBoxTeam(teamMask);
                    HurtBox[] hurtBoxes = sphereSearch.GetHurtBoxes();
                    if (hurtBoxes.Length > 0)
                    {
                        foundTarget = true;
                    }
                }
            }
            if (shouldObeyGravity)
            {
                gravityTimer += Time.fixedDeltaTime;
                if (gravityTimer > gravityDuration)
                {
                    gravityTimer = 0f;
                    pestBody.characterMotor.useGravity = false;
                    shouldObeyGravity = false;
                    pestBody.characterMotor.Motor.ForceUnground();
                    pestBody.characterMotor.velocity = new Vector3(0f, 25f, 0f);
                    pestBody.baseJumpCount = savedBaseJumpCount;
                    pestBody.maxJumpCount = savedMaxJumpCount;
                }
            }
        }
    }
}
