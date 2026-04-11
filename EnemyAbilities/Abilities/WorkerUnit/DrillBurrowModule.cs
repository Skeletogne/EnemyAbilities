using RoR2;
using R2API;
using RoR2.Skills;
using UnityEngine;
using UnityEngine.AddressableAssets;
using RoR2BepInExPack.GameAssetPaths.Version_1_39_0;
using RoR2.CharacterAI;
using EntityStates;
using System.Linq;
using static EnemyAbilities.PluginConfig;
using BepInEx.Configuration;

namespace EnemyAbilities.Abilities.SolusProspector
{

    [EnemyAbilities.ModuleInfo("Drill Burrow", "Gives Solus Prospectors a new Secondary ability: \n- Drill Burrow: Allows the Prospector to disappear underground to remain hidden for 1 second, before burrowing up beneath a nearby target.", "Solus Prospector", true)]
    public class DrillBurrowModule : BaseModule
    {
        private static GameObject bodyPrefab = Addressables.LoadAssetAsync<GameObject>(RoR2_DLC3_WorkerUnit.WorkerUnitBody_prefab).WaitForCompletion();
        private static GameObject masterPrefab = Addressables.LoadAssetAsync<GameObject>(RoR2_DLC3_WorkerUnit.WorkerUnitMaster_prefab).WaitForCompletion();

        internal static ConfigEntry<float> entryDuration;
        internal static ConfigEntry<float> waitDuration;
        internal static ConfigEntry<float> telegraphDuration;
        internal static ConfigEntry<float> damageCoeff;
        internal static ConfigEntry<float> radius;
        internal static ConfigEntry<float> baseVelocity;
        internal static ConfigEntry<float> cooldown;

        private static CharacterSpawnCard csc = Addressables.LoadAssetAsync<CharacterSpawnCard>(RoR2_DLC3_WorkerUnit.cscWorkerUnit_asset).WaitForCompletion();

        public override void RegisterConfig()
        {
            base.RegisterConfig();
            entryDuration = BindFloat("Burrow Entry Duration", 1f, "How long it takes for the Solus Prospector to burrow underground.", 0.2f, 2f, 0.1f, FormatType.Time);
            waitDuration = BindFloat("Burrow Wait Duration", 1f, "How long the Solus Prospector remains burrowed without attacking", 0.2f, 2f, 0.1f, FormatType.Time);
            telegraphDuration = BindFloat("Burrow Telegraph Duration", 1f, "How long the Solus Prospector telegraphs before bursting up from the ground", 0.2f, 2f, 0.1f, FormatType.Time);
            radius = BindFloat("Burrow Radius", 6f, "The radius of the burrow attack explosion", 6f, 16f, 1f, FormatType.Distance);
            damageCoeff = BindFloat("Burrow Damage Coefficient", 300f, "The damage coefficient of the burrow attack", 100f, 500f, 5f, FormatType.Percentage);
            baseVelocity = BindFloat("Burrow Upwards Velocity Modifier", 250f, "The speed multiplier at which the Prospector is ejected from the ground", 100f, 500f, 10f, FormatType.Percentage);
            cooldown = BindFloat("Burrow Cooldown", 15f, "The cooldown of the burrow", 8f, 30f, 0.1f, FormatType.Time);
            BindStats(bodyPrefab, [csc], new StatOverrides { directorCost = 16 });
        }
        public override void Initialise()
        {
            base.Initialise();
            CreateSkill();
        }
        public void CreateSkill()
        {
            SkillDefData skillData = new SkillDefData
            {
                objectName = "WorkerUnitBodyDrillBurrow",
                skillName = "WorkerUnitDrillBurrow",
                esmName = "Weapon",
                activationState = ContentAddition.AddEntityState<DrillBurrow>(out _),
                cooldown = cooldown.Value,
                combatSkill = true
            };
            SkillDef drillBurrow = CreateSkillDef<SkillDef>(skillData);
            CreateGenericSkill(bodyPrefab, drillBurrow.skillName, "WorkerUnitSecondaryFamily", drillBurrow, SkillSlot.Secondary);
            AISkillDriverData driverData = new AISkillDriverData
            {
                masterPrefab = masterPrefab,
                customName = "useSecondary",
                skillSlot = SkillSlot.Secondary,
                requireReady = true,
                minDistance = 20f,
                maxDistance = 50f,
                selectionRequiresOnGround = true,
                selectionRequiresTargetNonFlier = true,
                movementType = AISkillDriver.MovementType.Stop,
                aimType = AISkillDriver.AimType.None,
                ignoreNodeGraph = true,
                desiredIndex = 0
            };
            CreateAISkillDriver(driverData);
        }
    }
    public class DrillBurrow : BaseSkillState
    {

        public static float baseWindupDuration = DrillBurrowModule.entryDuration.Value;
        public static float baseBurrowDuration = DrillBurrowModule.waitDuration.Value;
        public static float baseTelegraphDuration = DrillBurrowModule.telegraphDuration.Value;
        public static float baseAttackDuration = 1f;
        public static float blastRadius = DrillBurrowModule.radius.Value;
        public static float force = 1500f;
        public static Vector3 bonusForce = new Vector3(0f, 2500f, 0f);
        public static float damageCoefficient = DrillBurrowModule.damageCoeff.Value / 100f;
        public static float exitSpeedMultiplier = DrillBurrowModule.baseVelocity.Value / 100f;
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
        private float burrowEffectInterval = 0.2f;
        private Vector3 startPos;
        private HurtBox target;
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

            startPos = characterBody.transform.position;
            BullseyeSearch bullseyeSearch = new BullseyeSearch();
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
                target = hurtBox;
            }

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
            Vector3 randomOffset = UnityEngine.Random.insideUnitSphere * 1f;
            if (currentState == DrillBurrowState.Windup)
            {
                effectData._origin = characterBody.footPosition + randomOffset;
            }
            else if (currentState == DrillBurrowState.Telegraphing)
            {
                effectData._origin = targetPos + randomOffset;
            }
            else if (currentState == DrillBurrowState.Burrowed)
            {
                Vector3 targetPosition = characterBody.footPosition;
                if (target != null && target.healthComponent != null && target.healthComponent.body != null)
                {
                    targetPosition = target.healthComponent.body.transform.position;
                }
                Vector3 startPosition = startPos;
                if (targetPosition.y > startPos.y)
                {
                    startPosition.y = targetPosition.y;
                }
                else
                {
                    targetPosition.y = startPosition.y;
                }
                Vector3 raycastStart = Vector3.Lerp(startPosition, targetPosition, Mathf.Clamp01((stopwatch - windupDuration) / burrowDuration));
                bool success = Physics.Raycast(raycastStart, Vector3.down, out RaycastHit hitInfo, 1000f, LayerIndex.world.mask);
                if (success)
                {
                    effectData._origin = hitInfo.point + randomOffset;
                }
            }
            effectData.scale = currentState == DrillBurrowState.Burrowed ? 1f : 2f;
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
                burrowEffectTimer = 0f;
                burrowEffectInterval = 0.05f;
            }
            if (stopwatch > windupDuration + burrowDuration && currentState == DrillBurrowState.Burrowed)
            {
                Telegraph();
                burrowEffectTimer = 0f;
                burrowEffectInterval = 0.2f;
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
            if ((stopwatch > totalDuration && base.characterMotor.isGrounded) || stopwatch > totalDuration + 3f)
            {
                outer.SetNextStateToMain();
            }
            if (currentState == DrillBurrowState.Windup || currentState == DrillBurrowState.Telegraphing || currentState == DrillBurrowState.Burrowed)
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
            base.gameObject.layer = LayerIndex.GetAppropriateFakeLayerForTeam(teamComponent.teamIndex).intVal;
            characterMotor.Motor.RebuildCollidableLayers();
            if (modelLocator != null && modelLocator.modelTransform != null)
            {
                modelLocator.modelTransform.gameObject.SetActive(false);
            }
        }
        public void Telegraph()
        {
            targetPos = base.transform.position;
            if (target != null && target.healthComponent != null && target.healthComponent.body != null)
            {
                targetPos = target.healthComponent.body.footPosition;
                //probably need a better sound cue than this?
                Util.PlaySound("Play_GildedElite_Pillar_Spawn", target.healthComponent.body.gameObject);
            }
            if (Physics.Raycast(targetPos, Vector3.down, out RaycastHit hit, 1000f, LayerIndex.world.mask))
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
            DamageTypeCombo combo = new DamageTypeCombo { damageSource = DamageSource.Secondary, damageType = DamageType.Generic };
            BlastAttack blastAttack = new BlastAttack();
            blastAttack.attacker = base.gameObject;
            blastAttack.attackerFiltering = AttackerFiltering.NeverHitSelf;
            blastAttack.baseDamage = damageCoefficient * damageStat;
            blastAttack.baseForce = force;
            blastAttack.bonusForce = bonusForce;
            blastAttack.crit = RollCrit();
            blastAttack.damageColorIndex = DamageColorIndex.Default;
            blastAttack.damageType = combo;
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
            if (currentState == DrillBurrowState.Windup || currentState == DrillBurrowState.Attacking)
            {
                return InterruptPriority.PrioritySkill;
            }
            else
            {
                return InterruptPriority.Death;
            }
        }
    }
}
