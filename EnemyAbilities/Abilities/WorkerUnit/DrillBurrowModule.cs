using RoR2;
using R2API;
using RoR2.Skills;
using UnityEngine;
using UnityEngine.AddressableAssets;
using RoR2BepInExPack.GameAssetPaths.Version_1_35_0;
using RoR2.CharacterAI;
using EntityStates;
using System.Linq;

namespace EnemyAbilities.Abilities.SolusProspector
{

    //issues:
    //if it loses los with it's target, it will just pop up where it burrowed

    [EnemyAbilities.ModuleInfo("Drill Burrow", "Gives Solus Prospectors a new Secondary ability: \n- Drill Burrow: Allows the Prospector to disappear underground to remain hidden for 1 second, before burrowing up beneath a nearby target.", "Solus Prospector", true)]
    public class DrillBurrowModule : BaseModule
    {
        private static GameObject bodyPrefab = Addressables.LoadAssetAsync<GameObject>(RoR2_DLC3_WorkerUnit.WorkerUnitBody_prefab).WaitForCompletion();
        private static GameObject masterPrefab = Addressables.LoadAssetAsync<GameObject>(RoR2_DLC3_WorkerUnit.WorkerUnitMaster_prefab).WaitForCompletion();

        public override void Awake()
        {
            base.Awake();
            CreateSkill();
        }
        public void CreateSkill()
        {
            SkillDef drillBurrowSkillDef = ScriptableObject.CreateInstance<SkillDef>();
            (drillBurrowSkillDef as ScriptableObject).name = "WorkerUnitBodyDrillBurrow";
            drillBurrowSkillDef.skillName = "WorkerUnitDrillBurrow";
            drillBurrowSkillDef.activationStateMachineName = "Weapon";
            drillBurrowSkillDef.activationState = ContentAddition.AddEntityState<DrillBurrow>(out _);
            drillBurrowSkillDef.baseRechargeInterval = 15f;
            drillBurrowSkillDef.cancelSprintingOnActivation = true;
            drillBurrowSkillDef.isCombatSkill = true;
            ContentAddition.AddSkillDef(drillBurrowSkillDef);

            SkillFamily skillFamily = ScriptableObject.CreateInstance<SkillFamily>();
            (skillFamily as ScriptableObject).name = "WorkerUnitSecondaryFamily";
            skillFamily.variants = [new SkillFamily.Variant() { skillDef = drillBurrowSkillDef }];

            GenericSkill skill = bodyPrefab.AddComponent<GenericSkill>();
            skill.skillName = "WorkerUnitDrillBurrow";
            skill._skillFamily = skillFamily;

            SkillLocator locator = bodyPrefab.GetComponent<SkillLocator>();
            locator.secondary = skill;

            ContentAddition.AddSkillFamily(skillFamily);

            AISkillDriver useSecondary = masterPrefab.AddComponent<AISkillDriver>();
            useSecondary.customName = "useSecondary";
            useSecondary.skillSlot = SkillSlot.Secondary;
            useSecondary.requireSkillReady = true;
            useSecondary.minDistance = 20f;
            useSecondary.maxDistance = 50f;
            useSecondary.selectionRequiresOnGround = true;
            useSecondary.movementType = AISkillDriver.MovementType.Stop;
            useSecondary.aimType = AISkillDriver.AimType.None;
            useSecondary.ignoreNodeGraph = true;
            useSecondary.maxUserHealthFraction = 1f;
            masterPrefab.ReorderSkillDrivers(useSecondary, 0);
        }
    }
    public class DrillBurrow : BaseSkillState
    {

        public static float baseWindupDuration = 1f;
        public static float baseBurrowDuration = 1f;
        public static float baseTelegraphDuration = 1.5f;
        public static float baseAttackDuration = 1f;
        public static float blastRadius = 6f;
        public static float force = 1500f;
        public static Vector3 bonusForce = new Vector3(0f, 2500f, 0f);
        public static float damageCoefficient = 3f;
        public static float exitSpeedMultiplier = 2.5f;
        private static GameObject indicatorPrefab = Addressables.LoadAssetAsync<GameObject>(RoR2_Base_Common.TeamAreaIndicator__GroundOnly_prefab).WaitForCompletion();
        private static GameObject explosionPrefab = Addressables.LoadAssetAsync<GameObject>(RoR2_Base_BeetleGuard.BeetleGuardGroundSlam_prefab).WaitForCompletion();
        private static GameObject burrowPrefab = Addressables.LoadAssetAsync<GameObject>(RoR2_Base_MiniMushroom.MiniMushroomPlantEffect_prefab).WaitForCompletion();
        private float windupDuration;
        private float burrowDuration;
        private float telegraphDuration;
        private float attackDuration;
        private float totalDuration;
        private float stopwatch = 0f;
        private int originalLayerIndex;
        private Vector3 targetPos;
        private GameObject indicatorInstance;
        private ChildLocator childLocator;
        private Animator animator;
        private bool reachedApex;
        private float burrowEffectTimer = 0f;
        private float burrowEffectInterval = 0.1f;
        private enum DrillBurrowState
        {
            None,
            Windup,
            Burrowed,
            Telegraphing,
            Attacking
        }
        private DrillBurrowState currentState = DrillBurrowState.None;

        public override void OnEnter()
        {
            base.OnEnter();
            animator = GetModelAnimator();
            childLocator = animator.GetComponent<ChildLocator>();
            originalLayerIndex = base.gameObject.layer;
            windupDuration = baseWindupDuration / attackSpeedStat;
            burrowDuration = baseBurrowDuration / attackSpeedStat;
            telegraphDuration = baseTelegraphDuration;
            attackDuration = baseAttackDuration / attackSpeedStat;
            totalDuration = windupDuration + burrowDuration + telegraphDuration + attackDuration;
            currentState = DrillBurrowState.Windup;
            //select target immediately
        }
        public override void OnExit()
        {
            base.OnExit();
            SetSprintEffectState(false);
            if (currentState != DrillBurrowState.Attacking)
            {
                Emerge();
            }
        }
        public void SpawnDirtEffect()
        {
            EffectData effectData = new EffectData();
            if (currentState == DrillBurrowState.Windup)
            {
                effectData._origin = characterBody.footPosition + UnityEngine.Random.insideUnitSphere * 3f;
            }
            else
            {
                effectData._origin = targetPos;
            }
            effectData.scale = 2f;
            EffectManager.SpawnEffect(burrowPrefab, effectData, transmit: true);
        }
        public override void FixedUpdate()
        {
            base.FixedUpdate();
            stopwatch += Time.fixedDeltaTime;
            if (currentState == DrillBurrowState.Windup)
            {
                characterMotor.velocity = Vector3.zero;
            }
            if (stopwatch > windupDuration && currentState == DrillBurrowState.Windup)
            {
                Burrow();
                currentState = DrillBurrowState.Burrowed;
            }
            if (stopwatch > windupDuration + burrowDuration && currentState == DrillBurrowState.Burrowed)
            {
                Telegraph();
                burrowEffectTimer = 0f;
                currentState = DrillBurrowState.Telegraphing;
            }
            if (stopwatch > windupDuration + burrowDuration + telegraphDuration && currentState == DrillBurrowState.Telegraphing)
            {
                FireAttack();
                currentState = DrillBurrowState.Attacking;
            }
            if (currentState == DrillBurrowState.Attacking && reachedApex == false)
            {
                if (base.characterMotor.velocity.y <= 0)
                {
                    reachedApex = true;
                    OnReachedApex();
                }
                else
                {
                    inputBank.aimDirection = Vector3.up;
                }
            }
            if (stopwatch > totalDuration && base.characterMotor.isGrounded)
            {
                outer.SetNextStateToMain();
            }
            if (currentState == DrillBurrowState.Windup || currentState == DrillBurrowState.Telegraphing)
            {
                burrowEffectTimer -= Time.fixedDeltaTime;
                if (burrowEffectTimer < 0f)
                {
                    burrowEffectTimer += burrowEffectInterval;
                    SpawnDirtEffect();
                }
            }
        }
        public void OnReachedApex()
        {
            SetSprintEffectState(false);
        }
        public void Burrow()
        {
            if (characterBody == null || characterMotor == null)
            {
                return;
            }
            characterMotor.walkSpeedPenaltyCoefficient = 0f;
            if (characterBody.GetBuffCount(RoR2Content.Buffs.HiddenInvincibility.buffIndex) < 1) 
            {
                characterBody.AddBuff(RoR2Content.Buffs.HiddenInvincibility.buffIndex);
            }
            originalLayerIndex = base.gameObject.layer;
            base.gameObject.layer = LayerIndex.GetAppropriateFakeLayerForTeam(teamComponent.teamIndex).intVal;
            characterMotor.Motor.RebuildCollidableLayers();
            if (modelLocator != null && modelLocator.modelTransform != null)
            {
                modelLocator.modelTransform.gameObject.SetActive(false);
            }
        }
        public void Telegraph()
        {

            BullseyeSearch bullseyeSearch = new BullseyeSearch();
            targetPos = base.transform.position;
            bullseyeSearch.viewer = characterBody;
            bullseyeSearch.teamMaskFilter = TeamMask.allButNeutral;
            bullseyeSearch.teamMaskFilter.RemoveTeam(characterBody.teamComponent.teamIndex);
            bullseyeSearch.sortMode = BullseyeSearch.SortMode.DistanceAndAngle;
            bullseyeSearch.minDistanceFilter = 0f;
            bullseyeSearch.maxDistanceFilter = 60f;
            bullseyeSearch.searchOrigin = base.inputBank.aimOrigin;
            bullseyeSearch.searchDirection = base.inputBank.aimDirection;
            bullseyeSearch.maxAngleFilter = 360f;
            bullseyeSearch.filterByLoS = false;
            bullseyeSearch.RefreshCandidates();
            HurtBox hurtBox = bullseyeSearch.GetResults().FirstOrDefault();
            if (hurtBox != null)
            {
                targetPos = hurtBox.healthComponent.body.footPosition;
                //probably need a better sound cue than this?
                Util.PlaySound("Play_GildedElite_Pillar_Spawn", hurtBox.healthComponent.body.gameObject);
            }
            if (Physics.Raycast(targetPos, Vector3.down, out RaycastHit hit, 1000f, LayerIndex.CommonMasks.bullet))
            {
                targetPos = hit.point;
            }
            indicatorInstance = UnityEngine.Object.Instantiate(indicatorPrefab, targetPos, Util.QuaternionSafeLookRotation(Vector3.up));
            indicatorInstance.transform.localScale = Vector3.one * blastRadius;
            TeamAreaIndicator teamAreaIndicator = indicatorInstance.GetComponent<TeamAreaIndicator>();
            if (teamAreaIndicator != null)
            {
                teamAreaIndicator.teamComponent = teamComponent;
            }
        }
        private void SetSprintEffectState(bool active)
        {
            if ((bool)childLocator)
            {
                childLocator.FindChild("SprintEffect")?.gameObject.SetActive(active);
            }
        }
        public void FireAttack()
        {
            TeleportHelper.TeleportBody(characterBody, targetPos, false);
            Util.PlaySound("Play_MULT_m2_secondary_explode", base.gameObject);
            Emerge();
            SetSprintEffectState(true);
            PlayAnimation("Gesture, Additive", "FrenziedMeleeAttack", "attack.playbackRate", 1f);
            BlastAttack blastAttack = new BlastAttack();
            blastAttack.attacker = base.gameObject;
            blastAttack.attackerFiltering = AttackerFiltering.NeverHitSelf;
            blastAttack.baseDamage = damageCoefficient * damageStat;
            blastAttack.baseForce = force;
            blastAttack.bonusForce = bonusForce;
            blastAttack.crit = RollCrit();
            blastAttack.damageColorIndex = DamageColorIndex.Default;
            blastAttack.damageType = DamageType.Generic;
            blastAttack.falloffModel = BlastAttack.FalloffModel.SweetSpot;
            blastAttack.inflictor = base.gameObject;
            blastAttack.position = targetPos;
            blastAttack.procCoefficient = 1f;
            blastAttack.procChainMask = default(ProcChainMask);
            blastAttack.radius = blastRadius;
            blastAttack.teamIndex = teamComponent.teamIndex;
            blastAttack.Fire();
            if (characterMotor != null)
            {
                characterMotor.Motor.ForceUnground();
                characterMotor.velocity = new Vector3(0f, moveSpeedStat * 2.5f, 0f);
            }
            EffectManager.SpawnEffect(explosionPrefab, new EffectData { origin = characterBody.transform.position, scale = blastRadius, }, true);
        }
        public void Emerge()
        {
            if (characterBody == null || characterMotor == null)
            {
                return;
            }
            if (indicatorInstance != null)
            {
                UnityEngine.Object.Destroy(indicatorInstance);
            }
            characterMotor.walkSpeedPenaltyCoefficient = 1f;
            characterBody.RemoveBuff(RoR2Content.Buffs.HiddenInvincibility.buffIndex);
            base.gameObject.layer = originalLayerIndex;
            characterMotor.Motor.RebuildCollidableLayers();
            if (modelLocator != null && modelLocator.modelTransform != null)
            {
                modelLocator.modelTransform.gameObject.SetActive(true);
            }
        }
        public override InterruptPriority GetMinimumInterruptPriority()
        {
            return InterruptPriority.PrioritySkill;
        }
    }
}
