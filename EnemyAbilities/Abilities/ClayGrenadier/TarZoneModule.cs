using EntityStates;
using RoR2.CharacterAI;
using R2API;
using RoR2;
using RoR2.Skills;
using RoR2BepInExPack.GameAssetPaths.Version_1_35_0;
using UnityEngine;
using UnityEngine.AddressableAssets;
using ThreeEyedGames;
using RoR2.Projectile;
using UnityEngine.Networking;
using MonoMod.Cil;
using Mono.Cecil.Cil;
using System;
using static EnemyAbilities.PluginConfig;

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
        private static GameObject chargeEffect = Addressables.LoadAssetAsync<GameObject>(RoR2_Base_ClayBoss.TarballExplosion_prefab).WaitForCompletion();
        public override void Awake()
        {
            CreateSkill();
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
                grenadier.baseMaxHealth = 1400f;
                grenadier.levelMaxHealth = 420f;
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
        public void CreateSkill()
        {
            EntityStateMachine weaponESM = bodyPrefab.AddComponent<EntityStateMachine>();
            weaponESM.customName = "Weapon";
            weaponESM.initialStateType = new global::EntityStates.SerializableEntityStateType(typeof(global::EntityStates.Idle));
            weaponESM.mainStateType = new global::EntityStates.SerializableEntityStateType(typeof(global::EntityStates.Idle));

            SkillDef tarZoneSkill = ScriptableObject.CreateInstance<SkillDef>();
            (tarZoneSkill as ScriptableObject).name = "ClayGrenadierBodyTarZone";
            tarZoneSkill.skillName = "ClayGrenadierTarZone";
            tarZoneSkill.activationStateMachineName = "Weapon";
            tarZoneSkill.activationState = ContentAddition.AddEntityState<FireBigBlob>(out _);
            tarZoneSkill.baseRechargeInterval = apothecaryDelugeCooldown.Value;
            tarZoneSkill.cancelSprintingOnActivation = true;
            tarZoneSkill.isCombatSkill = true;
            ContentAddition.AddSkillDef(tarZoneSkill);

            SkillFamily skillFamily = ScriptableObject.CreateInstance<SkillFamily>();
            (skillFamily as ScriptableObject).name = "ClayGrenadierSpecialFamily";
            skillFamily.variants = [new SkillFamily.Variant() { skillDef = tarZoneSkill }];

            GenericSkill skill = bodyPrefab.AddComponent<GenericSkill>();
            skill.skillName = "ClayGrenadierTarZone";
            skill._skillFamily = skillFamily;

            SkillLocator locator = bodyPrefab.GetComponent<SkillLocator>();
            locator.special = skill;

            ContentAddition.AddSkillFamily(skillFamily);

            AISkillDriver useSpecial = masterPrefab.AddComponent<AISkillDriver>();
            useSpecial.customName = "useSpecial";
            useSpecial.skillSlot = SkillSlot.Special;
            useSpecial.requireSkillReady = true;
            useSpecial.minDistance = 0f;
            useSpecial.maxDistance = 100f;
            useSpecial.maxUserHealthFraction = (apothecaryDelugeHealthThreshold.Value / 100f);
            useSpecial.selectionRequiresTargetLoS = true;
            useSpecial.moveTargetType = AISkillDriver.TargetType.CurrentEnemy;
            useSpecial.movementType = AISkillDriver.MovementType.ChaseMoveTarget;
            useSpecial.aimType = AISkillDriver.AimType.AtCurrentEnemy;
            masterPrefab.ReorderSkillDrivers(useSpecial, 0);
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
            impact.childrenDamageCoefficient = apothecaryDelugeAoEZoneDamagePercentage.Value / 100f;
            impact.preserveExplosionOrientation = false;
            impact.useChildRotation = true;
            impact.destroyOnWorld = false;
            impact.destroyOnEnemy = false;
            impact.impactOnWorld = true;
            impact.timerAfterImpact = true;
            impact.lifetimeAfterImpact = apothecaryDelugeFuseTime.Value;
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
            public void Awake()
            {
                newGravity = Physics.gravity * gravityModifier;
            }
            public void Start()
            {
                rigid = GetComponent<Rigidbody>();
                if (rigid.useGravity)
                {
                    Log.Error($"ProjectileBigTarBlob start on a with rigidbody.useGravity = true!");
                }
            }
            public void FixedUpdate()
            {
                if (!rigid.useGravity)
                {
                    rigid.velocity += newGravity * Time.fixedDeltaTime;
                }
            }
            public void DestroySelf()
            {
                Destroy(GetComponent<ProjectileController>().gameObject);
            }
        }
        public class FireBigBlob : BaseSkillState
        {
            private static float baseWindupDuration = 0.2f;
            private float windupDuration;
            private static float baseChargeDuration = apothecaryDelugeChargeTime.Value;
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
                Vector3 velocity = Trajectory.CalculateInitialVelocityFromTime(startPosition, endPosition, apothecaryDelugeTravelTime.Value, gravity: Physics.gravity.y * 0.5f);
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
            private static float lifetime = apothecaryDelugeAoEZoneDuration.Value;
            private float stopwatch;
            private float minRadius = apothecaryDelugeMinRadius.Value;
            private float maxRadius = apothecaryDelugeMaxRadius.Value;
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
                UnityEngine.Object.Destroy(particleSystemRenderers[1]);
                UnityEngine.Object.Destroy(particleSystems[1]);
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
            private static float maxPercentageHealthTaken = apothecaryDelugeHealthPercentageRequiredForFullCharge.Value / 100f;
            public bool charging = false;
            private Transform tarBallTransform;
            private CharacterBody body;
            private GameObject tarBallInstance;
            private ProjectileImpactExplosion impact;
            private ProjectileStickOnImpact stick;
            private ProjectileBigTarBlob bigTarBlob;
            private ProjectileController controller;
            private ProjectileDamage damageComponent;
            public float percentageCharge;
            private static GameObject explosionVFX = Addressables.LoadAssetAsync<GameObject>(RoR2_DLC1_ClayGrenadier.ClayGrenadierBarrelExplosion_prefab).WaitForCompletion();
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
                        EffectManager.SpawnEffect(chargeEffect, new EffectData { origin = origin, rotation = Util.QuaternionSafeLookRotation(UnityEngine.Random.onUnitSphere), scale = 2 + damagePercentage * 4 }, true);
                    }
                }
            }
            public void ResetInstances()
            {
                percentageHealthTaken = 0;
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
                        tarBallInstance = ProjectileManager.instance.FireProjectileImmediateServer(info);
                        controller = tarBallInstance.GetComponent<ProjectileController>();
                        impact = tarBallInstance.GetComponent<ProjectileImpactExplosion>();
                        stick = tarBallInstance.GetComponent<ProjectileStickOnImpact>();
                        damageComponent = tarBallInstance.GetComponent <ProjectileDamage>();
                        bigTarBlob = tarBallInstance.GetComponent<ProjectileBigTarBlob>();
                        impact.enabled = false;
                        stick.enabled = false;
                        bigTarBlob.enabled = false;
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
                    float radius = apothecaryDelugeMinRadius.Value + percentageCharge * (apothecaryDelugeMaxRadius.Value - apothecaryDelugeMinRadius.Value);
                    TeamIndex teamIndex = attackerBody.teamComponent.teamIndex;
                    float playerDamage = (apothecaryDelugePlayerDetonateDamage.Value / 100f) * attackerBody.damage;
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
                    bigTarBlob.enabled = true;
                    bigTarBlob.chargePercentage = percentageCharge;

                    float radius = apothecaryDelugeMinRadius.Value + (percentageCharge * (apothecaryDelugeMaxRadius.Value - apothecaryDelugeMinRadius.Value));
                    float damage = apothecaryDelugeMinDamage.Value / 100f + (percentageCharge * (apothecaryDelugeMaxDamage.Value / 100f - apothecaryDelugeMinDamage.Value / 100f));

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
    }
}
