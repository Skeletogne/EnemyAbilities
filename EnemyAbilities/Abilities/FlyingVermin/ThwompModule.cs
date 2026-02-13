using RoR2.CharacterAI;
using JetBrains.Annotations;
using RoR2;
using RoR2.Skills;
using UnityEngine;
using UnityEngine.AddressableAssets;
using RoR2BepInExPack.GameAssetPaths.Version_1_35_0;
using EntityStates;
using static EnemyAbilities.PluginConfig;
using R2API;

namespace EnemyAbilities.Abilities.FlyingVermin
{

    [EnemyAbilities.ModuleInfo("Thwomp Stomp", "Gives Blind Pests a new secondary:\n- Thwomp Stomp: Allows them to fly downwards quickly, releasing an explosion when they land. They enter a grounded state for 3 seconds afterwards.", "Blind Pest", true)]
    public class ThwompModule : BaseModule
    {
        private static GameObject bodyPrefab = Addressables.LoadAssetAsync<GameObject>(RoR2_DLC1_FlyingVermin.FlyingVerminBody_prefab).WaitForCompletion();
        private static GameObject masterPrefab = Addressables.LoadAssetAsync<GameObject>(RoR2_DLC1_FlyingVermin.FlyingVerminMaster_prefab).WaitForCompletion();
       
        public override void Awake()
        {
            base.Awake();
            bodyPrefab.AddComponent<ThwompController>();
            CreateSkill();
        }
        public void CreateSkill()
        {
            ThwompSkillDef thwompStomp = ScriptableObject.CreateInstance<ThwompSkillDef>();
            (thwompStomp as ScriptableObject).name = "FlyingVerminBodyThwompStomp";
            thwompStomp.skillName = "FlyingVerminThwompStomp";
            thwompStomp.activationStateMachineName = "Body";
            thwompStomp.activationState = ContentAddition.AddEntityState<ThwompStomp>(out _);
            thwompStomp.baseRechargeInterval = pestThwompCooldown.Value;
            thwompStomp.cancelSprintingOnActivation = true;
            thwompStomp.isCombatSkill = true;
            ContentAddition.AddSkillDef(thwompStomp);

            SkillFamily skillFamily = ScriptableObject.CreateInstance<SkillFamily>();
            (skillFamily as ScriptableObject).name = "FlyingVerminSecondaryFamily";
            skillFamily.variants = [new SkillFamily.Variant() { skillDef = thwompStomp }];

            GenericSkill skill = bodyPrefab.AddComponent<GenericSkill>();
            skill.skillName = "FlyingVerminThwompStomp";
            skill._skillFamily = skillFamily;

            SkillLocator locator = bodyPrefab.GetComponent<SkillLocator>();
            locator.secondary = skill;

            ContentAddition.AddSkillFamily(skillFamily);

            AISkillDriver useSecondary = masterPrefab.AddComponent<AISkillDriver>();
            useSecondary.customName = "useSecondary";
            useSecondary.skillSlot = SkillSlot.Secondary;
            useSecondary.requireSkillReady = true;
            useSecondary.minDistance = 10f;
            useSecondary.maxDistance = 40f;
            useSecondary.moveTargetType = AISkillDriver.TargetType.CurrentEnemy;
            useSecondary.movementType = AISkillDriver.MovementType.StrafeMovetarget;
            masterPrefab.ReorderSkillDrivers(useSecondary, 1);

            //makes it so they stop attacking when a player is on top of them
            AISkillDriver fleeAtCloseRange = masterPrefab.AddComponent<AISkillDriver>();
            fleeAtCloseRange.customName = "fleeAtCloseRange";
            fleeAtCloseRange.skillSlot = SkillSlot.None;
            fleeAtCloseRange.minDistance = 0f;
            fleeAtCloseRange.maxDistance = 5f;
            fleeAtCloseRange.moveTargetType = AISkillDriver.TargetType.CurrentEnemy;
            fleeAtCloseRange.movementType = AISkillDriver.MovementType.FleeMoveTarget;
            masterPrefab.ReorderSkillDrivers(fleeAtCloseRange, 0);
            
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
        private static float baseWarningDuration = pestThwompWarningDuration.Value;
        private bool thwompingIt;
        private bool hitGround;
        private static float baseRecoveryDuration = pestThwompRecoveryDuration.Value;
        private float recoveryDuration;
        private float recoveryStopwatch = 0f;
        private static float damageCoefficient = pestThwompDamageCoefficient.Value / 100f;
        private static float force = 1000f;
        private static Vector3 bonusForce = new Vector3(0f, 1000f, 0f);
        private static float procCoefficient = 1f;
        private static float radius = pestThwompRadius.Value;
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
        private float radius = 3f + pestThwompRadius.Value;
        private float gravityTimer;
        private float gravityDuration = pestThwompGroundedDuration.Value;
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
