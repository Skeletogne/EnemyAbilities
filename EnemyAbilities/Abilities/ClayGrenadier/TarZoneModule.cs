using EntityStates;
using RoR2.CharacterAI;
using R2API;
using RoR2;
using RoR2.Skills;
using RoR2BepInExPack.GameAssetPaths.Version_1_39_0;
using UnityEngine;
using UnityEngine.AddressableAssets;
using ThreeEyedGames;
using RoR2.Projectile;
using UnityEngine.Networking;
using MonoMod.Cil;
using Mono.Cecil.Cil;
using System;
using static EnemyAbilities.PluginConfig;
using BepInEx.Configuration;

namespace EnemyAbilities.Abilities.ClayGrenadier
{

    //just need to sort out some good visuals

    [EnemyAbilities.ModuleInfo("Tar Deluge", "Gives Clay Apothecaries a new Special:\n- Tar Deluge: Charges up a tar bomb that gains size and damage the more it's damaged during the charge. Flings it after 3 seconds, creating a huge AoE tar zone shortly after landing.\nEnabling this option increases the health of Clay Apothecaries from 1050 to 1400, and increases their director cost from 150 to 180.", "Clay Apothecary", true)]

    public class TarZoneModule : BaseModule
    {
        private static GameObject bodyPrefab = Addressables.LoadAssetAsync<GameObject>(RoR2_DLC1_ClayGrenadier.ClayGrenadierBody_prefab).WaitForCompletion();
        private static GameObject masterPrefab = Addressables.LoadAssetAsync<GameObject>(RoR2_DLC1_ClayGrenadier.ClayGrenadierMaster_prefab).WaitForCompletion();
        public static GameObject tarZoneProjectile = Addressables.LoadAssetAsync<GameObject>(RoR2_Base_BeetleQueen.BeetleQueenAcid_prefab).WaitForCompletion().InstantiateClone("tarZoneProjectile");
        public static GameObject tarZoneGhost = Addressables.LoadAssetAsync<GameObject>(RoR2_Base_BeetleQueen.BeetleQueenAcidGhost_prefab).WaitForCompletion().InstantiateClone("tarZoneGhost");
        public static GameObject tarBubblesPrefab = Addressables.LoadAssetAsync<GameObject>(RoR2_Base_ClayBoss.ChargeClayBossBombardment_prefab).WaitForCompletion().InstantiateClone("tarBubblesClone");
        public static GameObject bigTarBallProjectile = Addressables.LoadAssetAsync<GameObject>(RoR2_DLC1_ClayGrenadier.ClayGrenadierBarrelProjectile_prefab).WaitForCompletion().InstantiateClone("bigTarBallProjectile");
        public static GameObject bigTarBallGhost = Addressables.LoadAssetAsync<GameObject>(RoR2_DLC1_ClayGrenadier.ClayGrenadierBarrelGhost_prefab).WaitForCompletion().InstantiateClone("bigTarBallGhost");
        public static GameObject tarImpactEffect = Addressables.LoadAssetAsync<GameObject>(RoR2_Base_ClayBruiser.ClayShockwaveEffect_prefab).WaitForCompletion().InstantiateClone("scalableTarImpactEffect");
        private static CharacterSpawnCard cscApothecary = Addressables.LoadAssetAsync<CharacterSpawnCard>(RoR2_DLC1_ClayGrenadier.cscClayGrenadier_asset).WaitForCompletion();
        private static GameObject chargeEffect = Addressables.LoadAssetAsync<GameObject>(RoR2_Base_Common_VFX.OmniImpactVFXLarge_prefab).WaitForCompletion();
        private static Material tarMaterial = Addressables.LoadAssetAsync<Material>(RoR2_Base_ClayBoss.matGooTrail_mat).WaitForCompletion();
        private static GameObject explosionVFX = Addressables.LoadAssetAsync<GameObject>(RoR2_DLC1_ClayGrenadier.ClayGrenadierBarrelExplosion_prefab).WaitForCompletion();

        internal static ConfigEntry<float> cooldown;
        internal static ConfigEntry<float> minDamage;
        internal static ConfigEntry<float> maxDamage;
        internal static ConfigEntry<float> minRadius;
        internal static ConfigEntry<float> maxRadius;
        internal static ConfigEntry<float> healthPercentForFullCharge;
        internal static ConfigEntry<float> healthThreshold;
        internal static ConfigEntry<float> chargeTime;
        internal static ConfigEntry<float> travelTime;
        internal static ConfigEntry<float> fuseTime;
        internal static ConfigEntry<float> playerDetonateDamage;
        internal static ConfigEntry<float> dotZoneDamagePercent;
        internal static ConfigEntry<float> dotDuration;

        public override void RegisterConfig()
        {
            base.RegisterConfig();
            minDamage = BindFloat("Deluge Min Damage", 100f, "Minimum damage coefficient at zero charge", 50f, 200f, 5f, FormatType.Percentage);
            maxDamage = BindFloat("Deluge Max Damage", 200f, "Maximum damage coefficient at full charge", 250f, 800f, 5f, FormatType.Percentage);
            minRadius = BindFloat("Deluge Min Radius", 10f, "Minimum explosion radius at zero charge", 6f, 12f, 0.1f, FormatType.Distance);
            maxRadius = BindFloat("Deluge Max Radius", 24f, "Maximum explosion radius at full charge", 14f, 30f, 0.1f, FormatType.Distance);
            dotZoneDamagePercent = BindFloat("Deluge AoE Damage Percentage", 5f, "The damage of the AoE per tick as a percentage of the Detonation's damage. This will naturally increase the more the tar ball is charged.", 1f, 20f, 1f, FormatType.Percentage);
            healthPercentForFullCharge = BindFloat("Health Percentage for Full Charge", 25f, "The percentage of max health that must be taken as damage during charging to reach full charge", 5f, 50f, 1f, FormatType.Percentage);
            healthThreshold = BindFloat("Deluge Health Threshold", 60f, "The health threshold the Apothecary must be under to use Tar Deluge.", 10f, 100f, 1f, FormatType.Percentage);
            dotDuration = BindFloat("Deluge Zone Duration", 20f, "The duration of time that the Tar Zone lingers for.", 8f, 60f, 0.1f, FormatType.Time);
            chargeTime = BindFloat("Deluge Charge Time", 3f, "The time that the Apothecary spends charging the Tar Ball before firing it.", 1f, 5f, 0.1f, FormatType.Time);
            travelTime = BindFloat("Deluge Travel Time", 3f, "The time that the Tar Ball spends in the air before reaching it's target.", 1f, 5f, 0.1f, FormatType.Time);
            fuseTime = BindFloat("Deluge Fuse Time", 1f, "The time that the Tar Ball takes to explode after landing.", 0.2f, 2f, 0.1f, FormatType.Time);
            playerDetonateDamage = BindFloat("Player Detonation Damage", 800f, "The damage coefficient of the Tar Ball if it's detonated by a player (through killing the apothecary whilst it's charging - the tar ball cannot be killed once airborne).", 350f, 1600f, 5f, FormatType.Percentage);
            cooldown = BindFloat("Deluge Cooldown", 30f, "The cooldown of the Tar Deluge ability", 15f, 60f, 0.1f, FormatType.Time);
        }
        public override void Initialise()
        {
            base.Initialise();
            SkillSetup();
            ModifyTarZonePrefab();
            ModifyBigTarBallPrefab();
            bodyPrefab.AddComponent<ClayGrenadierChargeController>();
            GlobalEventManager.onServerDamageDealt += ChargeTarBlob;
            GlobalEventManager.onCharacterDeathGlobal += ExplodeBlobOnDeath;

            tarImpactEffect.GetComponent<EffectComponent>().applyScale = true;
            ContentAddition.AddEffect(tarImpactEffect);

            BodyCatalog.availability.onAvailable += UpdateMaxHealth;
            CharacterBody grenadier = DLC1Content.BodyPrefabs.ClayGrenadierBody;
            cscApothecary.directorCreditCost = 180; //+30
            IL.RoR2.Projectile.ProjectileExplosion.FireChild += (il) =>
            {
                ILCursor c1 = new ILCursor(il);
                if (c1.TryGotoNext(
                    x => x.MatchDup(),
                    x => x.MatchCallOrCallvirt<GameObject>(nameof(GameObject.GetComponent)),
                    x => x.MatchStloc(2)
                ))
                {
                    c1.Emit(OpCodes.Dup);
                    c1.Emit(OpCodes.Ldarg_0);
                    c1.EmitDelegate<Action<GameObject, ProjectileExplosion>>((childObject, projectileExplosion) =>
                    {
                        if (projectileExplosion != null && projectileExplosion.projectileController != null)
                        {
                            ProjectileBigTarBlob tarBlob = projectileExplosion.projectileController.gameObject.GetComponent<ProjectileBigTarBlob>();
                            if (tarBlob != null)
                            {
                                ProjectileTarZone tarZone = childObject.GetComponent<ProjectileTarZone>();
                                if (tarZone != null)
                                {
                                    tarZone.chargePercentage = tarBlob.chargePercentage;
                                }
                            }
                        }
                    });
                }
                else
                {
                    Log.Error($"ProjectileExplosion.FireChild ILCursor c1 failed to match!");
                }
            };
        }
        private void UpdateMaxHealth()
        {
            CharacterBody grenadier = DLC1Content.BodyPrefabs.ClayGrenadierBody;
            if (grenadier != null)
            {
                grenadier.baseMaxHealth = 1300f;
                grenadier.levelMaxHealth = 390f;
            }
        }
        private void ExplodeBlobOnDeath(DamageReport damageReport)
        {
            if (damageReport != null)
            {
                CharacterBody victimBody = damageReport.victimBody;
                if (victimBody != null && victimBody.bodyIndex == BodyCatalog.FindBodyIndex(DLC1Content.BodyPrefabs.ClayGrenadierBody))
                {
                    ClayGrenadierChargeController charge = victimBody.gameObject.GetComponent<ClayGrenadierChargeController>();
                    if (charge != null && charge.charging == true)
                    {
                        charge.DetonateOnKill(damageReport);
                    }
                }
            }
        }
        private void ChargeTarBlob(DamageReport damageReport)
        {
            if (damageReport != null)
            {
                CharacterBody victimBody = damageReport.victimBody;
                if (victimBody != null && victimBody.bodyIndex == BodyCatalog.FindBodyIndex(DLC1Content.BodyPrefabs.ClayGrenadierBody))
                {
                    ClayGrenadierChargeController charge = victimBody.gameObject.GetComponent<ClayGrenadierChargeController>();
                    if (charge != null && charge.charging == true && damageReport.damageDealt > 0f)
                    {
                        if (victimBody.healthComponent != null)
                        {
                            float fullHealth = victimBody.healthComponent.fullHealth;
                            float damage = damageReport.damageDealt;
                            float damagePercentage = damage / fullHealth;
                            charge.IncreaseCharge(damagePercentage);
                        }
                    }
                }
            }
        }
        public void SkillSetup()
        {
            EntityStateMachine weaponESM = CreateEntityStateMachine(bodyPrefab, "Weapon");
            SkillDefData skillData = new SkillDefData
            {
                objectName = "ClayGrenadierBodyTarZone",
                skillName = "ClayGrenadierTarZone",
                esmName = weaponESM.customName,
                activationState = ContentAddition.AddEntityState<FireBigBlob>(out _),
                cooldown = cooldown.Value,
                combatSkill = true
            };
            SkillDef tarZone = CreateSkillDef<SkillDef>(skillData);
            CreateGenericSkill(bodyPrefab, tarZone.skillName, "ClayGrenadierSpecialFamily", tarZone, SkillSlot.Special);
            AISkillDriverData driverData = new AISkillDriverData
            {
                masterPrefab = masterPrefab,
                customName = "useSpecial",
                skillSlot = SkillSlot.Special,
                requireReady = true,
                minDistance = 0f,
                maxDistance = 100f,
                maxHealthFraction = (healthThreshold.Value / 100f),
                selectionRequiresTargetLoS = true,
                targetType = AISkillDriver.TargetType.CurrentEnemy,
                movementType = AISkillDriver.MovementType.Stop,
                aimType = AISkillDriver.AimType.AtCurrentEnemy,
                desiredIndex = 0
            };
            CreateAISkillDriver(driverData);

        }
        public void ModifyTarZonePrefab()
        {
            ProjectileController controller = tarZoneProjectile.GetComponent<ProjectileController>();
            controller.ghostPrefab = tarZoneGhost;
            Transform ghostTransform = tarZoneGhost.GetComponent<Transform>();
            ghostTransform.localScale = Vector3.one * 10f;
            ghostTransform.localPosition = new Vector3(0f, -0.5f, 0f);
            ghostTransform.localRotation = Quaternion.identity;
            Decal ghostDecal = tarZoneGhost.GetComponentInChildren<Decal>();
            Material material = Addressables.LoadAssetAsync<Material>(RoR2_Base_ClayBruiser.matClayGooDecalSplat_mat).WaitForCompletion();
            ghostDecal.Material = material;
            ghostDecal.Fade = 1.7f;
            tarZoneProjectile.AddComponent<ProjectileTarZone>();
            Transform decalTransform = ghostDecal.gameObject.GetComponent<Transform>();
            decalTransform.localRotation = Util.QuaternionSafeLookRotation(Vector3.up);

            ParticleSystem[] systems = tarZoneGhost.GetComponentsInChildren<ParticleSystem>();
            for (int i = systems.Length - 1; i >= 0; i--)
            {
                Destroy(systems[i]);
            }
            ParticleSystemRenderer[] renderers = tarZoneGhost.GetComponentsInChildren<ParticleSystemRenderer>();
            for (int i =  renderers.Length - 1;i >= 0;i--)
            {
                Destroy(renderers[i]);
            }
            Light light = tarZoneGhost.GetComponentInChildren<Light>();
            if (light != null)
            {
                Destroy(light);
            }
            FlickerLight flicker = tarZoneGhost.GetComponentInChildren<FlickerLight>();
            if (flicker != null)
            {
                Destroy(flicker);
            }
            ProjectileDotZone dotZone = tarZoneProjectile.GetComponent<ProjectileDotZone>();
            if (dotZone != null)
            {
                Destroy(dotZone);
            }
            SyncFlickerLightToAnimateShaderAlpha syncFlicker = tarZoneGhost.GetComponentInChildren<SyncFlickerLightToAnimateShaderAlpha>();
            if (syncFlicker != null)
            {
                Destroy(syncFlicker);
            }
        }
        public void ModifyBigTarBallPrefab()
        {
            ProjectileController controller = bigTarBallProjectile.GetComponent<ProjectileController>();
            controller.ghostPrefab = bigTarBallGhost;

            ProjectileSimple simple = bigTarBallProjectile.GetComponent<ProjectileSimple>();
            simple.lifetime = 12f;

            ProjectileImpactExplosion impact = bigTarBallProjectile.GetComponent<ProjectileImpactExplosion>();
            impact.blastRadius = 24f;
            impact.blastDamageCoefficient = 1f;
            impact.falloffModel = BlastAttack.FalloffModel.SweetSpot;
            impact.childrenCount = 1;
            impact.childrenProjectilePrefab = tarZoneProjectile;
            impact.fireChildren = true;
            impact.childrenDamageCoefficient = dotZoneDamagePercent.Value / 100f;
            impact.preserveExplosionOrientation = false;
            impact.useChildRotation = true;
            impact.destroyOnWorld = false;
            impact.destroyOnEnemy = false;
            impact.impactOnWorld = true;
            impact.timerAfterImpact = true;
            impact.lifetimeAfterImpact = fuseTime.Value;
            impact.explodeOnLifeTimeExpiration = false;
            impact.lifetime = 20f;

            bigTarBallGhost.GetComponent<Transform>().localScale = Vector3.one * 3f;

            Rigidbody rigid = bigTarBallProjectile.GetComponent<Rigidbody>();
            rigid.useGravity = false;
            ProjectileBigTarBlob bigTarBlob = bigTarBallProjectile.AddComponent<ProjectileBigTarBlob>();
            bigTarBlob.gravityModifier = 0.5f;

            ProjectileStickOnImpact stick = bigTarBallProjectile.AddComponent<ProjectileStickOnImpact>();
            stick.ignoreWorld = false;
            stick.ignoreSteepSlopes = false;
            stick.ignoreCharacters = false;
            stick.alignNormals = false;
        }
        [RequireComponent(typeof(Rigidbody))]
        public class ProjectileBigTarBlob : MonoBehaviour
        {
            public float gravityModifier;
            private Vector3 newGravity;
            private Rigidbody rigid;
            public float chargePercentage;
            private ProjectileStickOnImpact stick;
            private bool playedImpactSoundEffect;
            private static GameObject indicatorPrefab = Addressables.LoadAssetAsync<GameObject>(RoR2_Base_Common.TeamAreaIndicator__GroundOnly_prefab).WaitForCompletion();
            private GameObject indicatorInstance;
            private TeamFilter teamFilter;
            public void Awake()
            {
                newGravity = Physics.gravity * gravityModifier;
            }
            public void Start()
            {
                teamFilter = GetComponent<TeamFilter>();    
                rigid = GetComponent<Rigidbody>();
                if (rigid.useGravity)
                {
                    Log.Error($"ProjectileBigTarBlob start on a with rigidbody.useGravity = true!");
                }
                stick = GetComponent<ProjectileStickOnImpact>();
            }
            public void FixedUpdate()
            {
                if (!rigid.useGravity)
                {
                    rigid.velocity += newGravity * Time.fixedDeltaTime;
                }
                if (stick != null && stick.stuck == true && !playedImpactSoundEffect)
                {
                    playedImpactSoundEffect = true;
                    Util.PlaySound("Play_clayGrenadier_attack1_launch", gameObject);
                    indicatorInstance = Instantiate(indicatorPrefab, gameObject.transform.position, Util.QuaternionSafeLookRotation(Vector3.up));
                    TeamAreaIndicator component = indicatorInstance.GetComponent<TeamAreaIndicator>();
                    component.teamFilter = teamFilter;
                    float radius = TarZoneModule.minRadius.Value + chargePercentage * (TarZoneModule.maxRadius.Value - TarZoneModule.minRadius.Value);
                    indicatorInstance.transform.localScale = Vector3.one * radius;
                    EffectManager.SpawnEffect(explosionVFX, new EffectData { origin = gameObject.transform.position, rotation = Quaternion.identity, scale = 6f + 6f * chargePercentage }, true);
                }
            }
            public void DestroySelf()
            {
                Destroy(GetComponent<ProjectileController>().gameObject);
            }
            public void OnDisable()
            {
                Destroy(indicatorInstance);
                indicatorInstance = null;
            }
        }
        public class FireBigBlob : BaseSkillState
        {
            private static float baseWindupDuration = 0.2f;
            private float windupDuration;
            private static float baseChargeDuration = chargeTime.Value;
            private float chargeDuration;
            private bool spawnedProjectile;
            private bool launchedProjectile;

            private ClayGrenadierChargeController chargeController;
            public override void OnEnter()
            {
                base.OnEnter();
                Util.PlayAttackSpeedSound("Play_clayGrenadier_attack2_chargeup", characterBody.gameObject, attackSpeedStat);
                characterBody.SetAimTimer(baseChargeDuration + baseWindupDuration);

                windupDuration = baseWindupDuration / attackSpeedStat;
                chargeDuration = baseChargeDuration;
                characterMotor.walkSpeedPenaltyCoefficient = 0f;

                skillLocator.primary.SetSkillOverride(this.gameObject, CharacterBody.CommonAssets.disabledSkill, GenericSkill.SkillOverridePriority.Contextual);
                skillLocator.secondary.SetSkillOverride(this.gameObject, CharacterBody.CommonAssets.disabledSkill, GenericSkill.SkillOverridePriority.Contextual);

                if ((bool)characterDirection)
                {
                    base.characterDirection.moveVector = GetAimRay().direction;
                }
                chargeController = characterBody.gameObject.GetComponent<ClayGrenadierChargeController>();  
            }
            public override void FixedUpdate()
            {
                base.FixedUpdate();
                if (characterBody != null && base.fixedAge >= windupDuration)
                {
                    characterBody.SetBuffCount(RoR2Content.Buffs.ArmorBoost.buffIndex, 1);
                    if (!chargeController.charging && !spawnedProjectile)
                    {
                        spawnedProjectile = true;
                        chargeController.StartChargingAndSpawnProjectile();
                    }
                }
                if (base.fixedAge >= windupDuration + chargeDuration && !launchedProjectile)
                {
                    launchedProjectile = true;
                    Util.PlayAttackSpeedSound("Play_clayGrenadier_attack2_throw", characterBody.gameObject, attackSpeedStat / 2f);
                    LaunchProjectile();
                }
                if (launchedProjectile)
                {
                    outer.SetNextStateToMain();
                }
            }
            public void LaunchProjectile()
            {
                Ray aimRay = GetAimRay();
                Vector3 startPosition = characterBody.transform.position + characterBody.transform.up * 8f;
                Vector3 endPosition = startPosition + aimRay.direction * 50f;
                bool success = Physics.Raycast(aimRay.origin, aimRay.direction, out RaycastHit hit, 1000f, LayerIndex.CommonMasks.bullet);
                if (success)
                {
                    endPosition = hit.point;
                }
                Vector3 velocity = Trajectory.CalculateInitialVelocityFromTime(startPosition, endPosition, travelTime.Value, gravity: Physics.gravity.y * 0.5f);
                if (chargeController.charging)
                {
                    chargeController.StopChargingAndLaunchProjectile(velocity);
                }
            }
            public override void OnExit()
            {
                base.OnExit();
                characterMotor.walkSpeedPenaltyCoefficient = 1f;
                skillLocator.primary.UnsetSkillOverride(this.gameObject, CharacterBody.CommonAssets.disabledSkill, GenericSkill.SkillOverridePriority.Contextual);
                skillLocator.secondary.UnsetSkillOverride(this.gameObject, CharacterBody.CommonAssets.disabledSkill, GenericSkill.SkillOverridePriority.Contextual);
                characterBody.SetBuffCount(RoR2Content.Buffs.ArmorBoost.buffIndex, 0);
            }
            public override InterruptPriority GetMinimumInterruptPriority()
            {
                return InterruptPriority.Death;
            }
        }
        [RequireComponent(typeof(ProjectileController))]
        public class ProjectileTarZone : MonoBehaviour
        {
            private TeamFilter filter;
            private static GameObject indicator = Addressables.LoadAssetAsync<GameObject>(RoR2_Base_Common.TeamAreaIndicator__GroundOnly_prefab).WaitForCompletion();
            private GameObject indicatorInstance;
            private static float attackInterval = 0.25f;
            private float attackTimer;
            private GameObject tarBubblesInstance;
            private EffectManagerHelper _emh_bubblesInstance;
            private Vector3 effectOffset = new Vector3(0f, -4f, 0f);
            public float chargePercentage;
            private static float lifetime = dotDuration.Value;
            private float stopwatch;
            private float minRadius = TarZoneModule.minRadius.Value; //confusing variable names, might want to change
            private float maxRadius = TarZoneModule.maxRadius.Value;
            private float radius;
            public void Start()
            {
                radius = minRadius + chargePercentage * (maxRadius - minRadius);
                filter = GetComponent<TeamFilter>();
                indicatorInstance = UnityEngine.Object.Instantiate(indicator, gameObject.transform.position, Util.QuaternionSafeLookRotation(Vector3.up));
                TeamAreaIndicator teamAreaIndicator = indicatorInstance.GetComponent<TeamAreaIndicator>();
                teamAreaIndicator.transform.localScale = Vector3.one * radius;
                teamAreaIndicator.teamFilter = filter;
                ProjectileController controller = GetComponent<ProjectileController>();
                if (controller != null && controller.ghost != null)
                {
                    controller.ghost.transform.localScale = Vector3.one * (radius / 2f);
                }
                AttachVisualEffect();
            }
            private void AttachVisualEffect()
            {
                if (!EffectManager.ShouldUsePooledEffect(tarBubblesPrefab))
                {
                    tarBubblesInstance = Instantiate(tarBubblesPrefab, transform.position + effectOffset, Quaternion.identity);
                }
                else
                {
                    _emh_bubblesInstance = EffectManager.GetAndActivatePooledEffect(tarBubblesPrefab, transform.position + effectOffset, Quaternion.identity);
                    tarBubblesInstance = _emh_bubblesInstance.gameObject;
                }
                ParticleSystemRenderer[] particleSystemRenderers = tarBubblesInstance.GetComponentsInChildren<ParticleSystemRenderer>();
                ParticleSystemRenderer bubblesRenderer = particleSystemRenderers[0];
                ParticleSystem[] particleSystems = tarBubblesInstance.GetComponentsInChildren<ParticleSystem>();
                ParticleSystem bubblesSystem = particleSystems[0];
                //destroy the scourge that is: weird spinny thing
                if (particleSystemRenderers.Length > 1)
                {
                    UnityEngine.Object.Destroy(particleSystemRenderers[1]);
                    UnityEngine.Object.Destroy(particleSystems[1]);
                }
                ParticleSystem.MainModule main1 = bubblesSystem.main;
                ParticleSystem.ShapeModule shape = bubblesSystem.shape;
                ParticleSystem.SizeOverLifetimeModule size = bubblesSystem.sizeOverLifetime;
                ParticleSystem.EmissionModule emission = bubblesSystem.emission;
                ParticleSystem.VelocityOverLifetimeModule velocity = bubblesSystem.velocityOverLifetime;

                bubblesSystem.gameObject.transform.rotation = Util.QuaternionSafeLookRotation(Vector3.up);

                main1.startLifetime = 2.8f;
                main1.scalingMode = ParticleSystemScalingMode.Hierarchy;
                size.sizeMultiplier = 1.5f;
                emission.rateOverTimeMultiplier = 15f + 15f * chargePercentage;
                shape.shapeType = ParticleSystemShapeType.Sphere;
                shape.radius = radius;
                shape.scale = new Vector3(1f, 0.2f, 1f);
                velocity.space = ParticleSystemSimulationSpace.World;
                velocity.enabled = true;
                velocity.speedModifier = 1f;
                velocity.speedModifierMultiplier = 5f;
                velocity.yMultiplier = 1f;
            }
            public void DestroyVisualEffect()
            {
                if (_emh_bubblesInstance != null && tarBubblesInstance != null)
                {
                    EffectManager.ReturnToPoolOrDestroyInstance(_emh_bubblesInstance, ref tarBubblesInstance);
                    tarBubblesInstance = null;
                    _emh_bubblesInstance = null;
                }
            }
            public void OnDestroy()
            {
                UnityEngine.Object.Destroy(indicatorInstance);
                indicatorInstance = null;
                DestroyVisualEffect();
            }
            public void FixedUpdate()
            {
                attackTimer -= Time.fixedDeltaTime;
                stopwatch += Time.fixedDeltaTime;
                if (attackTimer < 0)
                {
                    attackTimer += attackInterval;
                    FireAttack();
                }
                if (stopwatch > lifetime)
                {
                    Destroy(this.gameObject);
                }
            }
            public void FireAttack()
            {
                ProjectileController controller = GetComponent<ProjectileController>();
                ProjectileDamage damageComponent = GetComponent<ProjectileDamage>();
                SphereSearch sphereSearch = new SphereSearch();
                sphereSearch.radius = radius;
                sphereSearch.origin = gameObject.transform.position;
                sphereSearch.mask = LayerIndex.entityPrecise.mask;
                sphereSearch.queryTriggerInteraction = QueryTriggerInteraction.Collide;
                TeamMask teamMask = TeamMask.GetEnemyTeams(filter.teamIndex);
                sphereSearch.RefreshCandidates();
                sphereSearch.FilterCandidatesByDistinctHurtBoxEntities();
                sphereSearch.FilterCandidatesByHurtBoxTeam(teamMask);
                HurtBox[] hurtBoxes = sphereSearch.GetHurtBoxes();
                foreach (var hurtBox in hurtBoxes)
                {
                    if (hurtBox == null || hurtBox.healthComponent == null || hurtBox.healthComponent.body == null)
                    {
                        continue;
                    }
                    CharacterBody targetBody = hurtBox.healthComponent.body;
                    float bodyPositionY = targetBody.transform.position.y;
                    float tarPositionY = gameObject.transform.position.y;
                    if (Mathf.Abs(tarPositionY - bodyPositionY) > 8)
                    {
                        continue;
                    }
                    GameObject owner = controller.owner;
                    DamageInfo damageInfo = new DamageInfo();
                    damageInfo.inflictor = this.gameObject;
                    damageInfo.attacker = owner;
                    damageInfo.crit = false;
                    damageInfo.damage = damageComponent.damage * 1f;
                    damageInfo.force = Vector3.zero;
                    damageInfo.damageColorIndex = DamageColorIndex.DeathMark;
                    damageInfo.damageType = new DamageTypeCombo { damageType = DamageType.ClayGoo, damageSource = DamageSource.Special };
                    damageInfo.position = targetBody.transform.position;
                    damageInfo.procCoefficient = 0f;
                    damageInfo.procChainMask = default(ProcChainMask);
                    targetBody.healthComponent.TakeDamage(damageInfo);
                }
            }
        }
        public class ClayGrenadierChargeController : MonoBehaviour
        {
            public float percentageHealthTaken = 0f;
            private static float maxPercentageHealthTaken = TarZoneModule.healthPercentForFullCharge.Value / 100f;
            public bool charging = false;
            public Transform tarBallTransform;
            private CharacterBody body;
            private GameObject tarBallInstance;
            private ProjectileImpactExplosion impact;
            private ProjectileStickOnImpact stick;
            private ProjectileBigTarBlob bigTarBlob;
            private ProjectileController controller;
            private ProjectileDamage damageComponent;
            public float percentageCharge;
            private SphereCollider collider;
            public void Start()
            {
                body = GetComponent<CharacterBody>();
                GameObject gameObject = new GameObject();
                gameObject.transform.parent = body.transform;
                gameObject.transform.localPosition = body.transform.up * 8f;
                tarBallTransform = gameObject.transform;
            }
            public void IncreaseCharge(float damagePercentage)
            {
                if (percentageHealthTaken < maxPercentageHealthTaken)
                {
                    percentageHealthTaken += damagePercentage;
                    if (controller != null)
                    {
                        Vector3 origin = controller.transform.position;
                        Util.PlaySound("Play_engi_M1_chargeStock", body.gameObject);
                        EffectManager.SpawnEffect(chargeEffect, new EffectData { origin = origin, rotation = Util.QuaternionSafeLookRotation(UnityEngine.Random.onUnitSphere), scale = 2 + damagePercentage * 4 }, true);

                        if (percentageCharge < 1)
                        {
                            int totalFliers = Mathf.Min((int)(damagePercentage / 0.004f), 10);
                            Log.Debug(totalFliers);
                            for (int i = 0; i < totalFliers; i++)
                            {
                                GameObject trailGameObject = new GameObject();
                                Vector3 randomOnUnitSphere = UnityEngine.Random.onUnitSphere * (8f + 8f * percentageCharge);
                                trailGameObject.transform.position = tarBallTransform.position + randomOnUnitSphere;
                                TarFlier tarFlier = trailGameObject.AddComponent<TarFlier>();
                                tarFlier.associatedController = this;
                            }
                        }
                    }
                }
            }
            public void StartChargingAndSpawnProjectile()
            {
                if (!charging)
                {
                    if (NetworkServer.active)
                    {
                        charging = true;
                        FireProjectileInfo info = new FireProjectileInfo
                        {
                            projectilePrefab = bigTarBallProjectile,
                            position = tarBallTransform.position,
                            rotation = Quaternion.identity,
                            owner = body.gameObject,
                            damage = 4f * body.damage,
                            crit = false,
                            force = 0f,
                            speedOverride = 0f,
                        };
                        EffectManager.SpawnEffect(explosionVFX, new EffectData { origin = tarBallTransform.position, rotation = Quaternion.identity, scale = 6f }, true);
                        tarBallInstance = ProjectileManager.instance.FireProjectileImmediateServer(info);
                        controller = tarBallInstance.GetComponent<ProjectileController>();
                        impact = tarBallInstance.GetComponent<ProjectileImpactExplosion>();
                        stick = tarBallInstance.GetComponent<ProjectileStickOnImpact>();
                        damageComponent = tarBallInstance.GetComponent <ProjectileDamage>();
                        bigTarBlob = tarBallInstance.GetComponent<ProjectileBigTarBlob>();
                        collider = tarBallInstance.GetComponent<SphereCollider>();
                        impact.enabled = false;
                        stick.ignoreCharacters = true;
                        stick.ignoreWorld = true;
                        stick.enabled = false;
                        bigTarBlob.enabled = false;
                        collider.enabled = false;
                    }
                }
            }
            public void FixedUpdate()
            { 
                if (tarBallInstance != null)
                {
                    tarBallInstance.transform.position = tarBallTransform.position;
                    percentageCharge = Mathf.Clamp01(percentageHealthTaken / maxPercentageHealthTaken);
                    if (controller != null && controller.ghost != null)
                    {
                        controller.ghost.transform.localScale = Vector3.one * (2f + 3f * percentageCharge);
                    }
                }
            }
            public void DetonateOnKill(DamageReport report)
            {
                CharacterBody attackerBody = report.attackerBody;
                if (bigTarBlob == null)
                {
                    return;
                }
                if (attackerBody != null && attackerBody.teamComponent != null)
                {
                    float radius = minRadius.Value + percentageCharge * (maxRadius.Value - minRadius.Value);
                    TeamIndex teamIndex = attackerBody.teamComponent.teamIndex;
                    float playerDamage = (playerDetonateDamage.Value / 100f) * attackerBody.damage;
                    float nonPlayerDamage = 1f * attackerBody.damage;
                    BlastAttack attack = new BlastAttack();
                    attack.radius = radius;
                    attack.position = tarBallTransform.position;
                    attack.crit = attackerBody != null ? attackerBody.RollCrit() : false;
                    attack.baseDamage = teamIndex == TeamIndex.Player ? playerDamage : nonPlayerDamage;
                    attack.inflictor = attackerBody.gameObject;
                    attack.attacker = attackerBody.gameObject;
                    attack.attackerFiltering = AttackerFiltering.NeverHitSelf;
                    attack.baseForce = 1000f + 4000f * percentageCharge;
                    attack.procCoefficient = 1f;
                    attack.teamIndex = teamIndex;
                    attack.damageType = new DamageTypeCombo { damageSource = DamageSource.Special, damageType = DamageType.ClayGoo };
                    attack.damageColorIndex = DamageColorIndex.DeathMark;
                    attack.Fire();
                    EffectManager.SpawnEffect(explosionVFX, new EffectData { origin = tarBallTransform.position, scale = radius }, true);
                }
                bigTarBlob.enabled = true;
                bigTarBlob.DestroySelf();
                percentageHealthTaken = 0;
                stick = null;
                impact = null;
                bigTarBlob = null;
                tarBallInstance = null;
                percentageCharge = 0f;
                charging = false;
            }
            public void StopChargingAndLaunchProjectile(Vector3 velocity)
            {
                if (charging && tarBallInstance != null && body != null)
                {
                    charging = false;
                    impact.enabled = true;
                    stick.enabled = true;
                    stick.ignoreWorld = false;
                    bigTarBlob.enabled = true;
                    collider.enabled = true;
                    bigTarBlob.chargePercentage = percentageCharge;

                    float radius = minRadius.Value + (percentageCharge * (maxRadius.Value - minRadius.Value));
                    float damage = minDamage.Value / 100f + (percentageCharge * (maxDamage.Value / 100f - minDamage.Value / 100f));

                    impact.blastRadius = radius;
                    damageComponent.damage = damage * body.damage;
                    damageComponent.force = 1000f + 4000f * percentageCharge;

                    tarBallInstance.GetComponent<Rigidbody>().velocity = velocity;
                    tarBallInstance = null;
                    percentageHealthTaken = 0;
                    stick = null;
                    impact = null;
                    bigTarBlob = null;
                    percentageCharge = 0f;
                }
            }
        }
        public class TarFlier : MonoBehaviour
        {
            public ClayGrenadierChargeController associatedController;
            private TrailRenderer trailRenderer;
            private float trailSpeed = 20f;
            public void Start()
            {
                trailRenderer = gameObject.AddComponent<TrailRenderer>();
                trailRenderer.material = tarMaterial;
                trailRenderer.materials = [tarMaterial];
                trailRenderer.startWidth = 0.8f;
                trailRenderer.endWidth = 0.3f;
                trailRenderer.time = 0.2f;
                trailRenderer.emitting = true;
                trailRenderer.enabled = true;
                trailRenderer.startColor = Color.black;
                trailRenderer.endColor = Color.black;
            }
            public void FixedUpdate()
            {
                if (trailRenderer != null && associatedController != null)
                {
                    Vector3 currentPosition = gameObject.transform.position;
                    Vector3 targetPosition = associatedController.tarBallTransform.position;
                    float distance = Vector3.Distance(currentPosition, targetPosition);
                    if (distance < 1f)
                    {
                        Destroy(gameObject);
                        return;
                    }
                    else
                    {
                        Vector3 direction = (targetPosition - currentPosition).normalized;
                        Vector3 newPosition = currentPosition + (direction * Time.fixedDeltaTime * trailSpeed);
                        gameObject.transform.position = newPosition;
                    }
                }
                else
                {
                    Destroy(gameObject);
                    return;
                }
            }
        }
    }
}
