using EntityStates;
using RoR2.CharacterAI;
using R2API;
using RoR2;
using RoR2.Skills;
using RoR2BepInExPack.GameAssetPaths.Version_1_35_0;
using UnityEngine;
using UnityEngine.AddressableAssets;
using RoR2.Projectile;
using UnityEngine.Networking;
using RoR2.Audio;
using Rewired.ComponentControls.Effects;
using System.Linq;
using static R2API.DamageAPI;
using static EnemyAbilities.PluginConfig;

namespace EnemyAbilities.Abilities.Bison
{
    [EnemyAbilities.ModuleInfo("Unearth Boulder", "Gives Bison a new Secondary\n- Unearth Boulder: The Bison Unearths a large boulders nearby. These do nothing until hit with a melee attack, at which point they launch towards a nearby target, dealing high damage on impact. Activating this module causes Charge to activate from a longer range, and changes the max health damage needed to stun a Bison from 15% -> 30% to match Beetle Guards and Stone Golems.", "Bighorn Bison", true)]

    public class RockModule : BaseModule
    {
        private static GameObject bodyPrefab = Addressables.LoadAssetAsync<GameObject>(RoR2_Base_Bison.BisonBody_prefab).WaitForCompletion();
        private static GameObject masterPrefab = Addressables.LoadAssetAsync<GameObject>(RoR2_Base_Bison.BisonMaster_prefab).WaitForCompletion();
        private SkillDef bisonChargeSkill = Addressables.LoadAssetAsync<SkillDef>(RoR2_Base_Bison.BisonBodyCharge_asset).WaitForCompletion();
        private EntityStateConfiguration escBisonCharge = Addressables.LoadAssetAsync<EntityStateConfiguration>(RoR2_Base_Bison.EntityStates_Bison_Charge_asset).WaitForCompletion();
        public static GameObject rockProjectile = Addressables.LoadAssetAsync<GameObject>(RoR2_Base_Grandparent.GrandparentBoulder_prefab).WaitForCompletion().InstantiateClone("bisonBoulder");
        public static GameObject rockGhost = Addressables.LoadAssetAsync<GameObject>(RoR2_Base_Grandparent.GrandparentBoulderGhost_prefab).WaitForCompletion().InstantiateClone("bisonBoulderGhost");
        public static ModdedDamageType rockDamageType;

        public override void Awake()
        {
            base.Awake();
            CreateSkill();
            ModifyProjectilePrefab();
            On.RoR2.HealthComponent.TakeDamageProcess += TakeDamageProcess;
            rockDamageType = ReserveDamageType();
            bodyPrefab.GetComponent<SetStateOnHurt>().hitThreshold = 0.3f;
        }

        private void TakeDamageProcess(On.RoR2.HealthComponent.orig_TakeDamageProcess orig, HealthComponent self, DamageInfo damageInfo)
        {
            orig(self, damageInfo);
            ProjectileBisonRock component = self.gameObject.GetComponent<ProjectileBisonRock>();
            if (component != null)
            {
                if (self != null && damageInfo != null && damageInfo.attacker != null)
                {
                    CharacterBody victimBody = self.body;
                    CharacterBody attackerBody = damageInfo.attacker.GetComponent<CharacterBody>();
                    if (attackerBody != null && victimBody != null)
                    {
                        bool launchValid = false;
                        if (attackerBody.bodyIndex == RoR2Content.BodyPrefabs.BisonBody.bodyIndex && !damageInfo.damageType.HasModdedDamageType(rockDamageType))
                        {
                            launchValid = true;
                        }
                        //can also be moved by sufficiently strong attacks
                        if (damageInfo.damage >= attackerBody.damage * 10f)
                        {
                            launchValid = true;
                        }
                        if (launchValid)
                        {

                            component.TryLaunch(attackerBody);
                        }
                    }
                }
            }
        }

        public void CreateSkill()
        {
            SkillDef spawnRock = ScriptableObject.CreateInstance<SkillDef>();
            (spawnRock as ScriptableObject).name = "BisonBodySpawnRock";
            spawnRock.skillName = "BisonSpawnRock";
            spawnRock.activationStateMachineName = "Body";
            spawnRock.activationState = ContentAddition.AddEntityState<SpawnRock>(out _);
            spawnRock.baseRechargeInterval = bisonRockCooldown.Value;
            spawnRock.canceledFromSprinting = false;
            spawnRock.isCombatSkill = false;
            ContentAddition.AddSkillDef(spawnRock);

            SkillFamily skillFamily = ScriptableObject.CreateInstance<SkillFamily>();
            (skillFamily as ScriptableObject).name = "BisonSecondaryFamily";
            skillFamily.variants = [new SkillFamily.Variant() { skillDef = spawnRock }];

            GenericSkill skill = bodyPrefab.AddComponent<GenericSkill>();
            skill.skillName = "BisonSpawnRock";
            skill._skillFamily = skillFamily;

            SkillLocator locator = bodyPrefab.GetComponent<SkillLocator>();
            locator.secondary = skill;

            ContentAddition.AddSkillFamily(skillFamily);

            AISkillDriver useSecondary = masterPrefab.AddComponent<AISkillDriver>();
            useSecondary.customName = "useSecondary";
            useSecondary.skillSlot = SkillSlot.Secondary;
            useSecondary.requiredSkill = spawnRock;
            useSecondary.requireSkillReady = true;
            useSecondary.minDistance = 18f;
            useSecondary.maxDistance = 80f;
            useSecondary.selectionRequiresOnGround = true;
            useSecondary.moveTargetType = AISkillDriver.TargetType.CurrentEnemy;
            useSecondary.movementType = AISkillDriver.MovementType.ChaseMoveTarget;
            masterPrefab.ReorderSkillDrivers(useSecondary, 1);

            AISkillDriver chargeDriver = masterPrefab.GetComponents<AISkillDriver>()[2];
            chargeDriver.maxDistance = 80f;
        }
        public void ModifyProjectilePrefab()
        {
            ProjectileController controller = rockProjectile.GetComponent<ProjectileController>();
            rockProjectile.layer = LayerIndex.projectileWorldOnly.intVal;
            controller.flightSoundLoop = null;
            controller.ghostPrefab = rockGhost;
            rockGhost.GetComponent<Transform>().localScale = Vector3.one * 0.75f;
            ProjectileImpactExplosion impact = rockProjectile.GetComponent<ProjectileImpactExplosion>();
            ProjectileSimple simple = rockProjectile.GetComponent<ProjectileSimple>();
            simple.lifetime = 20f;
            Destroy(impact);
            ProjectileBisonRock rock = rockProjectile.AddComponent<ProjectileBisonRock>();
            rock.falloffModel = BlastAttack.FalloffModel.SweetSpot;
            rock.blastRadius = bisonRockExplosionRadius.Value;
            rock.blastDamageCoefficient = 1f;
            rock.blastProcCoefficient = 1f;
            rock.blastAttackerFiltering = AttackerFiltering.Default;
            rock.bonusBlastForce = new Vector3(0f, 2000f, 0f);
            rock.canRejectForce = true;
            rock.impactEffect = Addressables.LoadAssetAsync<GameObject>(RoR2_Base_Parent.ParentSlamEffect_prefab).WaitForCompletion();
            rock.fireChildren = false;
            rock.destroyOnEnemy = true;
            rock.destroyOnWorld = true;
            rock.destroyOnDistance = false;
            rock.lifetime = 20f;
            rock.explodeOnLifeTimeExpiration = false;
            ProjectileStickOnImpact stick = rockProjectile.AddComponent<ProjectileStickOnImpact>();
            stick.alignNormals = true;
            stick.ignoreCharacters = true;
            stick.ignoreWorld = false;
            stick.ignoreSteepSlopes = false;

            SphereCollider projectileCollider = rockProjectile.GetComponent<SphereCollider>();
            projectileCollider.radius = 1.5f;

            GameObject childGameObject = new GameObject();
            childGameObject.transform.SetParent(rockProjectile.transform);
            Transform childTransform = childGameObject.transform;

            ModelLocator modelLocator = rockProjectile.AddComponent<ModelLocator>();
            modelLocator.modelTransform = childTransform;

            modelLocator.autoUpdateModelTransform = true;
            modelLocator.dontDetatchFromParent = true;
            modelLocator.noCorpse = true;
            modelLocator.dontReleaseModelOnDeath = false;
            modelLocator.normalizeToFloor = false;
            modelLocator.normalSmoothdampTime = 0.1f;
            modelLocator.normalMaxAngleDelta = 90f;

            TeamComponent teamComponent = rockProjectile.AddComponent<TeamComponent>();
            teamComponent.hideAllyCardDisplay = true;
            teamComponent.teamIndex = TeamIndex.Neutral;

            CharacterBody characterBody = rockProjectile.AddComponent<CharacterBody>();
            characterBody.baseVisionDistance = Mathf.Infinity;
            characterBody.sprintingSpeedMultiplier = 1.45f;
            characterBody.hullClassification = HullClassification.Human;
            characterBody.baseMaxHealth = 1000000f;
            characterBody.levelMaxHealth = 300000f;
            characterBody.SetSpreadBloom(0f);

            HealthComponent healthComponent = rockProjectile.AddComponent<HealthComponent>();
            healthComponent.body = characterBody;
            healthComponent.dontShowHealthbar = true;
            healthComponent.globalDeathEventChanceCoefficient = 0f;

            GameObject hurtBoxObject = new GameObject();
            hurtBoxObject.transform.SetParent(modelLocator.modelTransform);

            SphereCollider collider = hurtBoxObject.AddComponent<SphereCollider>();
            collider.enabled = true;
            collider.isTrigger = true;
            collider.contactOffset = 0.01f;
            collider.radius = 3f;
            collider.sharedMaterial = Addressables.LoadAssetAsync<PhysicMaterial>(RoR2_Base_Common.physmatSuperFriction_physicMaterial).WaitForCompletion();
            collider.material = collider.sharedMaterial;

            HurtBox hurtBox = hurtBoxObject.gameObject.AddComponent<HurtBox>();
            if (hurtBox == null)
            {
                Log.Error($"hurtBox is null");
            }
            if (hurtBox.gameObject == null)
            {
                Log.Error($"hurtBox.gameObject == null");
            }
            hurtBox.gameObject.layer = LayerIndex.entityPrecise.intVal;
            hurtBox.healthComponent = healthComponent;
            hurtBox.isBullseye = true;
            hurtBox.isSniperTarget = false;
            hurtBox.damageModifier = HurtBox.DamageModifier.Normal;
            hurtBox.collider = collider;

            hurtBox.hurtBoxGroup = modelLocator.modelTransform.gameObject.AddComponent<HurtBoxGroup>();
            hurtBox.hurtBoxGroup.hurtBoxes = [hurtBox];
            hurtBox.hurtBoxGroup.mainHurtBox = hurtBox;
            hurtBox.hurtBoxGroup.bullseyeCount = 1;

            characterBody.hurtBoxGroup = hurtBox.hurtBoxGroup;

            DisableCollisionsBetweenColliders dcbc = rockProjectile.AddComponent<DisableCollisionsBetweenColliders>();
            dcbc.collidersA = [rockProjectile.GetComponent<SphereCollider>()];
            dcbc.collidersB = [collider];

            ContentAddition.AddProjectile(rockProjectile);
            ContentAddition.AddBody(rockProjectile);
            
        }
    }
    public class SpawnRock : BaseSkillState
    {
        private static float baseDuration = 0.25f;
        private float duration;

        private static float rockSpawnDistance = 10f;
        private int rockSpawnCount = (int)bisonRockCount.Value;
        private float rockIntervalInDegrees = 45f;

        private static GameObject projectilePrefab = RockModule.rockProjectile;

        public override void OnEnter()
        {
            base.OnEnter();
            duration = baseDuration / attackSpeedStat;
            Vector3 bisonPosition = characterBody.transform.position;
            Vector3 forward = characterBody.transform.forward;
            forward.y = 0f;
            forward = forward.normalized;
            CharacterMaster master = characterBody.master;
            if (master != null)
            {
                BaseAI baseAI = master.gameObject.GetComponent<BaseAI>();
                if (baseAI != null && baseAI.currentEnemy != null && baseAI.currentEnemy.characterBody != null)
                {
                    Vector3 targetPosition = baseAI.currentEnemy.characterBody.transform.position;
                    Vector3 direction = targetPosition - bisonPosition;
                    direction.y = 0f;
                    forward = direction.normalized;
                }
            }
            for (int i = 0; i < rockSpawnCount; i++)
            {
                if (base.isAuthority)
                {
                    float angleInDegrees = (360 - ((rockSpawnCount - 1) * rockIntervalInDegrees) / 2) + i * rockIntervalInDegrees;
                    Vector3 rotatedVector = Quaternion.AngleAxis(angleInDegrees, Vector3.up) * forward;
                    Vector3 raycastStartPosition = (characterBody.transform.position + rotatedVector * rockSpawnDistance) + new Vector3(0f, 15f, 0f);
                    bool success = Physics.Raycast(raycastStartPosition, Vector3.down, out RaycastHit hit, 30f, LayerIndex.world.mask, QueryTriggerInteraction.Ignore);
                    if (success)
                    {
                        Vector3 rockSpawnPosition = hit.point + new Vector3(0f, 1.75f, 0f);
                        DamageTypeCombo combo = new DamageTypeCombo { damageSource = DamageSource.Secondary, damageType = DamageType.Generic };
                        combo.AddModdedDamageType(RockModule.rockDamageType);
                        ProjectileManager.instance.FireProjectile(projectilePrefab, rockSpawnPosition, Util.QuaternionSafeLookRotation(UnityEngine.Random.onUnitSphere), characterBody.gameObject, (bisonRockDamageCoefficient.Value / 100f) * damageStat, 2000f, RollCrit(), DamageColorIndex.Default, null, 0f, combo);
                    }
                }
            }

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
    }
    [RequireComponent(typeof(ProjectileController))]
    public class ProjectileBisonRock : ProjectileExplosion, IProjectileImpactBehavior
    {
        public enum TransformSpace
        {
            World,
            Local,
            Normal
        }
        private Vector3 impactNormal = Vector3.up;
        public GameObject impactEffect;
        public NetworkSoundEventDef lifetimeExpiredSound;
        public float offsetForLifetimeExpiredSound;
        public bool destroyOnEnemy = true;
        public bool detonateOnEnemy;
        public bool destroyOnWorld;
        public bool destroyOnDistance;
        public float maxDistance;
        private float maxDistanceSqr;
        public bool impactOnWorld = true;
        public bool timerAfterImpact;
        public float lifetime;
        public float lifetimeAfterImpact;
        private float stopwatch;
        public uint minimumPhysicsStepsToKeepAliveAfterImpact;
        private float stopwatchAfterImpact;
        private bool hasImpact;
        private bool hasPlayedLifetimeExpiredSound;
        public TransformSpace transformSpace;
        private Vector3 startPos;
        public bool explodeOnLifeTimeExpiration;
        private static float launchMaxSearchRange = 100f;
        private static float launchMaxSearchAngle = 75f;
        private CharacterBody mostRecentLauncher = null;
        private enum RockState
        {
            None,
            JustSpawned,
            Embedded,
            Launched
        }
        private RockState rockState = RockState.JustSpawned;
        private float phaseOutTimer;
        private static float phaseOutDuration = 0.1f;

        public override void Awake()
        {
            base.Awake();
        }
        public void TryLaunch(CharacterBody attackerBody)
        {
            if (rockState == RockState.JustSpawned)
            {
                return;
            }
            if (mostRecentLauncher == attackerBody)
            {
                return;
            }
            mostRecentLauncher = attackerBody;
            bool foundTarget = false;
            HurtBox targetHurtBox = null;
            ProjectileStickOnImpact stick = GetComponent<ProjectileStickOnImpact>();
            TeamFilter filter = GetComponent<TeamFilter>();
            filter.teamIndex = attackerBody.teamComponent.teamIndex;
            projectileController.owner = attackerBody.gameObject;
            ProjectileDamage damage = GetComponent<ProjectileDamage>();
            stick.enabled = false;
            Vector3 attackerPosition = attackerBody.transform.position;
            Vector3 rockPosition = transform.position;
            Vector3 searchDirection = (rockPosition - attackerPosition).normalized;

            BullseyeSearch search = new BullseyeSearch();
            search.searchOrigin = rockPosition;
            search.searchDirection = searchDirection;
            search.minDistanceFilter = 0f;
            search.maxDistanceFilter = launchMaxSearchRange;
            search.maxAngleFilter = launchMaxSearchAngle;
            search.sortMode = BullseyeSearch.SortMode.Distance;
            search.filterByDistinctEntity = true;
            search.filterByLoS = true;
            search.queryTriggerInteraction = QueryTriggerInteraction.UseGlobal;
            search.teamMaskFilter = TeamMask.GetEnemyTeams(filter.teamIndex);
            search.RefreshCandidates();
            search.FilterOutGameObject(this.gameObject);
            var results = search.GetResults();
            if (results != null && results.Any())
            {
                targetHurtBox = results.Where(hurtBox =>hurtBox != null && hurtBox.healthComponent != null && hurtBox.healthComponent.health > 0 && hurtBox.healthComponent.body != null && hurtBox.healthComponent.body.transform != null).FirstOrDefault();
                if (targetHurtBox != null)
                {
                    foundTarget = true;
                }
            }
            Vector3 startVelocity = Vector3.zero;
            if (foundTarget == true)
            {
                startVelocity = Trajectory.CalculateInitialVelocityFromTime(rockPosition, targetHurtBox.healthComponent.body.transform.position, filter.teamIndex == TeamIndex.Player ? bisonRockTimeToTargetPlayer.Value : bisonRockTimeToTargetNonPlayer.Value, minHDistance: 10f, maxHDistance: 100f); 
            }
            else
            {
                startVelocity = Trajectory.CalculateInitialVelocityFromTime(rockPosition, rockPosition + searchDirection * 40f, bisonRockTimeToTargetNonPlayer.Value);
            }
            if (rockState == RockState.Embedded)
            {
                phaseOutTimer = 0f;
                rockState = RockState.Launched;
                RotateAroundAxis[] components = projectileController.ghost.gameObject.GetComponentsInChildren<RotateAroundAxis>();
                foreach (RotateAroundAxis axis in components)
                {
                    if (axis != null)
                    {
                        axis.enabled = true;
                    }
                }
            }
            Rigidbody rigidBody = GetComponent<Rigidbody>();
            lifetime += 5f;
            rigidBody.velocity = startVelocity;
        }

        protected void Start()
        {
            startPos = base.transform.position;
            maxDistanceSqr = maxDistance * maxDistance;
        }

        protected void FixedUpdate()
        {
            stopwatch += Time.fixedDeltaTime;
            if (rockState == RockState.Launched)
            {
                phaseOutTimer += Time.fixedDeltaTime;
            }
            if (!NetworkServer.active && !projectileController.isPrediction)
            {
                return;
            }
            if (explodeOnLifeTimeExpiration && alive && stopwatch >= lifetime)
            {
                explosionEffect = impactEffect ?? explosionEffect;
                Detonate();
            }
            if (timerAfterImpact && hasImpact)
            {
                stopwatchAfterImpact += Time.fixedDeltaTime;
            }
            bool lifetimeExpired = stopwatch >= lifetime;
            bool lifetimeAfterImpactExpired = timerAfterImpact && stopwatchAfterImpact > lifetimeAfterImpact;
            bool beyondMaxDistance = false;
            if (destroyOnDistance && (base.transform.position - startPos).sqrMagnitude >= maxDistanceSqr)
            {
                beyondMaxDistance = true;
            }
            if (lifetimeExpired || lifetimeAfterImpactExpired || beyondMaxDistance)
            {
                alive = false;
            }
            if (timerAfterImpact && hasImpact && minimumPhysicsStepsToKeepAliveAfterImpact != 0)
            {
                minimumPhysicsStepsToKeepAliveAfterImpact--;
                alive = true;
            }
            if (alive && !hasPlayedLifetimeExpiredSound)
            {
                bool shouldPlayLifetimeExpiredSound = stopwatch > lifetime - offsetForLifetimeExpiredSound;
                if (timerAfterImpact)
                {
                    shouldPlayLifetimeExpiredSound |= stopwatchAfterImpact > lifetimeAfterImpact - offsetForLifetimeExpiredSound;
                }
                if (shouldPlayLifetimeExpiredSound)
                {
                    hasPlayedLifetimeExpiredSound = true;
                    if (NetworkServer.active && (bool)lifetimeExpiredSound)
                    {
                        PointSoundManager.EmitSoundServer(lifetimeExpiredSound.index, base.transform.position);
                    }
                }
            }
            if (!alive)
            {
                explosionEffect = impactEffect ?? explosionEffect;
                Detonate();
            }
        }
        public void OnProjectileImpact(ProjectileImpactInfo impactInfo)
        {
            if (rockState == RockState.JustSpawned)
            {
                RotateAroundAxis[] components = projectileController.ghost.gameObject.GetComponentsInChildren<RotateAroundAxis>();
                EffectManager.SpawnEffect(impactEffect, new EffectData { origin = transform.position, scale = blastRadius }, true);
                foreach (RotateAroundAxis axis in components)
                {
                    if (axis != null)
                    {
                        axis.enabled = false;
                    }
                }
                rockState = RockState.Embedded;
                return;
            }
            Collider collider = impactInfo.collider;
            if (impactInfo.collider != null)
            {
                HurtBox hurtBox = impactInfo.collider.GetComponent<HurtBox>();
                if (hurtBox != null && hurtBox.healthComponent != null && hurtBox.healthComponent.body != null && hurtBox.healthComponent.body == mostRecentLauncher)
                {
                    return;
                }
                if (impactInfo.collider.GetComponentInParent<ProjectileBisonRock>() != null)
                {
                    return;
                }
            }
            if (!alive)
            {
                return;
            }
            if (rockState != RockState.Launched)
            {
                return;
            }
            if (phaseOutTimer < phaseOutDuration)
            {
                return;
            }

            impactNormal = impactInfo.estimatedImpactNormal;
            if (!collider)
            {
                return;
            }
            DamageInfo damageInfo = new DamageInfo();
            if ((bool)projectileDamage)
            {
                damageInfo.damage = projectileDamage.damage;
                damageInfo.crit = projectileDamage.crit;
                damageInfo.attacker = (projectileController.owner ? projectileController.owner.gameObject : null);
                damageInfo.inflictor = base.gameObject;
                damageInfo.damageType = projectileDamage.damageType;
                damageInfo.inflictor = base.gameObject;
                damageInfo.position = impactInfo.estimatedPointOfImpact;
                damageInfo.force = projectileDamage.force * base.transform.forward;
                damageInfo.procChainMask = projectileController.procChainMask;
                damageInfo.procCoefficient = projectileController.procCoefficient;
                damageInfo.damageType = projectileDamage.damageType;
            }
            HurtBox component = collider.GetComponent<HurtBox>();
            if ((bool)component)
            {
                if (destroyOnEnemy)
                {
                    HealthComponent healthComponent = component.healthComponent;
                    if ((bool)healthComponent)
                    {
                        if (healthComponent.gameObject == projectileController.owner || ((bool)projectileHealthComponent && healthComponent == projectileHealthComponent))
                        {
                            return;
                        }
                        alive = false;
                    }
                }
                else if (detonateOnEnemy)
                {
                    HealthComponent healthComponent2 = component.healthComponent;
                    if ((bool)healthComponent2 && healthComponent2.gameObject != projectileController.owner && healthComponent2 != projectileHealthComponent)
                    {
                        DetonateNoDestroy();
                    }
                }
            }
            else if (destroyOnWorld)
            {
                alive = false;
            }
            hasImpact = (bool)component || impactOnWorld;
            if (NetworkServer.active && hasImpact)
            {
                GlobalEventManager.instance.OnHitAll(damageInfo, collider.gameObject);
            }
        }
        public override void OnValidate()
        {
            if (!Application.IsPlaying(this))
            {
                base.OnValidate();
            }
        }
    }
}
