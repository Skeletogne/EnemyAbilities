using System.Collections.Generic;
using System.Linq;
using EntityStates;
using R2API;
using RoR2;
using RoR2.Audio;
using RoR2.CharacterAI;
using RoR2.Projectile;
using RoR2.Skills;
using RoR2BepInExPack.GameAssetPaths.Version_1_39_0;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Networking;
using ThreeEyedGames;
using static EnemyAbilities.PluginConfig;
using BepInEx.Configuration;

//make config options

namespace EnemyAbilities.Abilities.GreaterWisp
{
    [EnemyAbilities.ModuleInfo("Inferno Wheel", "Gives Greater Wisps a new special:\n-Inferno Wheel: Charges up a ring of fireballs that spin faster and faster until they fire off at their target. Each fireball leaves behind a large AoE. The fireballs can be destroyed both whilst charging and mid-flight, and will not leave an AoE if destroyed in this manner.\nActivating this module gives Greater Wisps a health boost, and slightly reduces its director cost.", "Greater Wisp", true)]
    public class FireballCarouselModule : BaseModule
    {
        //projectiles need to be MEAN to make up for the fact that they're easy to destroy
        private static GameObject bodyPrefab = Addressables.LoadAssetAsync<GameObject>(RoR2_Base_GreaterWisp.GreaterWispBody_prefab).WaitForCompletion();
        private static GameObject masterPrefab = Addressables.LoadAssetAsync<GameObject>(RoR2_Base_GreaterWisp.GreaterWispMaster_prefab).WaitForCompletion();
        private static GameObject fireballProjectile = Addressables.LoadAssetAsync<GameObject>(RoR2_Base_GreaterWisp.WispCannon_prefab).WaitForCompletion().InstantiateClone("carouselFireballProjectile");
        private static GameObject fireballGhost = Addressables.LoadAssetAsync<GameObject>(RoR2_DLC2_Scorchling.ScorchlingBombGhost_prefab).WaitForCompletion().InstantiateClone("carouselFireballGhost");
        private static GameObject fireballDotZone = Addressables.LoadAssetAsync<GameObject>(RoR2_Base_LunarExploder.LunarExploderProjectileDotZone_prefab).WaitForCompletion().InstantiateClone("greaterWispCarouselDotZone");
        private static CharacterSpawnCard cscGreaterWisp = Addressables.LoadAssetAsync<CharacterSpawnCard>(RoR2_Base_GreaterWisp.cscGreaterWisp_asset).WaitForCompletion();

        internal static ConfigEntry<float> cooldown;
        internal static ConfigEntry<float> healthThreshold;
        internal static ConfigEntry<float> blastRadius;
        internal static ConfigEntry<float> damageCoeff;
        internal static ConfigEntry<float> childDamageCoeff;
        internal static ConfigEntry<float> projectileHealth;
        internal static ConfigEntry<float> projectileCount;
        internal static ConfigEntry<float> onKillChance;
        internal static ConfigEntry<float> spinupDuration;
        internal static ConfigEntry<bool> igniteOnHit;
        internal static ConfigEntry<float> projectileSpeed;
        internal static ConfigEntry<bool> leavesDotZone;
        public override void RegisterConfig()
        {
            base.RegisterConfig();
            cooldown = BindFloat("Inferno Wheel Cooldown", 20f, "Cooldown of the ability", 10f, 60f, 1f, FormatType.Time);
            healthThreshold = BindFloat("Inferno Wheel Health Threshold", 90f, "Health percentage threshold to use the ability", 0f, 100f, 1f, FormatType.Percentage);
            blastRadius = BindFloat("Inferno Wheel Explosion Radius", 12f, "Radius of each fireball explosion", 8f, 16f, 0.1f, FormatType.Distance);
            damageCoeff = BindFloat("Inferno Wheel Damage Coefficient", 400f, "Damage coefficient of each fireball", 200f, 600f, 5f, FormatType.Percentage);
            childDamageCoeff = BindFloat("Inferno Wheel DoT Damage %", 5f, "Damage per tick of the DoT zone as a percentage", 1f, 10f, 1f, FormatType.Percentage);
            projectileHealth = BindFloat("Inferno Wheel Projectile Health", 130f, "Base health of each fireball projectile. Gains 30% of this value per level.", 100f, 200f, 10f);
            projectileCount = BindFloat("Inferno Wheel Projectile Count", 3f, "Number of fireballs in the ring", 1f, 6f, 1f);
            spinupDuration = BindFloat("Inferno Wheel Spinup Duration", 7f, "Duration of the spinup phase before fireballs launch", 4f, 10f, 0.1f, FormatType.Time);
            igniteOnHit = BindBool("Inferno Wheel Ignite on Hit", true, "Whether fireballs ignite targets on hit");
            leavesDotZone = BindBool("Inferno Wheel DoT Zone", true, "Whether fireballs leave behind a damage zone on impact");
            projectileSpeed = BindFloat("Inferno Wheel Projectile Speed", 60f, "Speed of fireballs when launched", 40f, 100f, 5f, FormatType.Speed);
            onKillChance = BindFloat("Inferno Wheel On-Kill Chance", 100f, "Chance for a fireball to trigger on-kill effects", 0f, 100f, 5f, FormatType.Percentage);
        }
        public override void Initialise()
        {
            base.Initialise();
            bodyPrefab.AddComponent<CarouselController>();
            CreateSkill();
            CreateProjectilePrefab();
            ModifyProjectileGhost();
            CharacterBody greaterWispBody = bodyPrefab.GetComponent<CharacterBody>();
            greaterWispBody.baseMaxHealth += 100f;
            greaterWispBody.levelMaxHealth += 30f;
            cscGreaterWisp.directorCreditCost = 175;
            Utils.AddHealthOverride((originalMaxHealth, body) =>
            {
                if (body == null || body.master != null)
                {
                    return originalMaxHealth;
                }
                ProjectileCarouselFireball carouselComponent = body.gameObject.GetComponent<ProjectileCarouselFireball>();
                if (carouselComponent == null)
                {
                    return originalMaxHealth;
                }
                float healthPerLevel = projectileHealth.Value * 0.3f;
                float ambientLevel = Run.instance.ambientLevel;
                float newMaxHealth = projectileHealth.Value + (healthPerLevel * (ambientLevel - 1));
                return newMaxHealth;
            });
        }
        public void CreateSkill()
        {
            EntityStateMachine carouselESM = CreateEntityStateMachine(bodyPrefab, "Carousel");
            SkillDefData skillData = new SkillDefData
            {
                objectName = "GreaterWispBodyFireballCarousel",
                skillName = "GreaterWispFireballCarousel",
                esmName = carouselESM.customName,
                activationState = ContentAddition.AddEntityState<FireballCarousel>(out _),
                cooldown = cooldown.Value,
                combatSkill = true
            };
            SkillDef carousel = CreateSkillDef<SkillDef>(skillData);
            CreateGenericSkill(bodyPrefab, carousel.skillName, "GreaterWispSpecialFamily", carousel, SkillSlot.Special);
            AISkillDriverData driverData = new AISkillDriverData
            {
                masterPrefab = masterPrefab,
                customName = "useSecondary",
                skillSlot = SkillSlot.Special,
                requiredSkillDef = carousel,
                requireReady = true,
                minDistance = 0f,
                maxDistance = 75f,
                maxHealthFraction = healthThreshold.Value / 100f,
                targetType = AISkillDriver.TargetType.CurrentEnemy,
                movementType = AISkillDriver.MovementType.Stop,
                driverUpdateTimerOverride = 2f,
                aimType = AISkillDriver.AimType.AtCurrentEnemy,
                desiredIndex = 1
            };
            CreateAISkillDriver(driverData);
        }
        public void CreateProjectilePrefab()
        {
            ProjectileController controller = fireballProjectile.GetComponent<ProjectileController>();
            controller.ghostPrefab = fireballGhost;
            Transform ghostTransform = fireballGhost.GetComponent<Transform>();
            ghostTransform.localScale = Vector3.one * 3f;
            ProjectileImpactExplosion impactExplosion = fireballProjectile.GetComponent<ProjectileImpactExplosion>();
            Destroy(impactExplosion);
            ProjectileSimple simple = fireballProjectile.GetComponent<ProjectileSimple>();
            simple.desiredForwardSpeed = 0f;
            simple.lifetime = 99f;
            ProjectileCarouselFireball carouselFireball = fireballProjectile.AddComponent<ProjectileCarouselFireball>();
            carouselFireball.falloffModel = BlastAttack.FalloffModel.SweetSpot;
            carouselFireball.blastRadius = blastRadius.Value;
            carouselFireball.blastDamageCoefficient = 1f;
            carouselFireball.blastProcCoefficient = 1f;
            carouselFireball.blastAttackerFiltering = AttackerFiltering.Default;
            carouselFireball.canRejectForce = true;
            carouselFireball.impactEffect = Addressables.LoadAssetAsync<GameObject>(RoR2_Base_MagmaWorm.MagmaOrbExplosion_prefab).WaitForCompletion();
            carouselFireball.fireChildren = false;
            carouselFireball.childrenCount = 1;
            carouselFireball.childrenDamageCoefficient = childDamageCoeff.Value / 100f;
            carouselFireball.childrenInheritDamageType = false;
            carouselFireball.childrenProjectilePrefab = fireballDotZone;
            carouselFireball.useChildRotation = true;
            carouselFireball.shouldNotExplode = true;
            carouselFireball.lifetime = 99f;
            carouselFireball.explodeOnLifeTimeExpiration = false;

            Transform[] fireDotZoneTransforms = fireballDotZone.GetComponentsInChildren<Transform>();
            for (int i = fireDotZoneTransforms.Length - 1; i >= 0; i--)
            {
                Transform t = fireDotZoneTransforms[i];
                if (t.gameObject.name == "Decal")
                {
                    Decal decal = t.gameObject.GetComponent<Decal>();
                    if (decal != null)
                    {
                        decal.Material = Addressables.LoadAssetAsync<Material>(RoR2_DLC2_Chef.matChefOilPoolFireDecal_mat).WaitForCompletion();
                    }
                }
                if (t.gameObject.name == "Point Light")
                {
                    Light light = t.gameObject.GetComponent<Light>();
                    if (light != null)
                    {
                        light.color = new Color(r: 1.000f, g: 0.500f, b: 0.100f, a: 1.000f);

                    }
                }
                if (t.gameObject.name == "Spores")
                {
                    DestroyImmediate(t.gameObject);
                    continue;
                }
                if (t.gameObject.name == "Fire, Billboard")
                {
                    ParticleSystemRenderer renderer = t.gameObject.GetComponent<ParticleSystemRenderer>();
                    if (renderer != null)
                    {
                        Material material = new Material(Addressables.LoadAssetAsync<Material>(RoR2_Base_Common_VFX.matGenericFire_mat).WaitForCompletion());
                        material.color = Color.green; // :(
                        renderer.material = material;
                        renderer.sharedMaterial = material;
                    }
                }
                if (t.gameObject.name == "Fire, Stretched")
                {
                    ParticleSystemRenderer renderer = t.gameObject.GetComponent<ParticleSystemRenderer>();
                    if (renderer != null)
                    {
                        Material material = new Material(Addressables.LoadAssetAsync<Material>(RoR2_Base_Common_VFX.matFireStaticLarge_mat).WaitForCompletion());
                        material.color = Color.green; 
                        renderer.material = material;
                        renderer.sharedMaterial = material;
                    }
                }
            }

            Rigidbody rigidbody = fireballProjectile.GetComponent<Rigidbody>();
            rigidbody.mass = 9999f;

            GameObject childGameObject = new GameObject();
            childGameObject.transform.SetParent(fireballProjectile.transform);
            Transform childTransform = childGameObject.transform;

            ModelLocator modelLocator = fireballProjectile.AddComponent<ModelLocator>();
            modelLocator.modelTransform = childTransform;

            modelLocator.autoUpdateModelTransform = true;
            modelLocator.dontDetatchFromParent = true;
            modelLocator.noCorpse = true;
            modelLocator.dontReleaseModelOnDeath = false;
            modelLocator.normalizeToFloor = false;
            modelLocator.normalSmoothdampTime = 0.1f;
            modelLocator.normalMaxAngleDelta = 90f;

            TeamComponent teamComponent = fireballProjectile.AddComponent<TeamComponent>();
            teamComponent.hideAllyCardDisplay = true;
            teamComponent.teamIndex = TeamIndex.Neutral;

            LanguageAPI.Add("SKELETOGNE_GWISPFIREBALL_BODY_NAME", "Inferno Wheel Fireball");
            CharacterBody characterBody = fireballProjectile.AddComponent<CharacterBody>();
            characterBody.baseVisionDistance = Mathf.Infinity;
            characterBody.sprintingSpeedMultiplier = 1.45f;
            characterBody.hullClassification = HullClassification.Human;
            characterBody.baseMaxHealth = projectileHealth.Value;
            characterBody.levelMaxHealth = projectileHealth.Value * 0.3f;
            characterBody.SetSpreadBloom(0f);
            characterBody.bodyFlags |= CharacterBody.BodyFlags.Ungrabbable;
            characterBody.baseNameToken = "SKELETOGNE_GWISPFIREBALL_BODY_NAME";


            HealthComponent healthComponent = fireballProjectile.AddComponent<HealthComponent>();
            healthComponent.body = characterBody;
            healthComponent.dontShowHealthbar = false;
            healthComponent.globalDeathEventChanceCoefficient = onKillChance.Value / 100f;

            carouselFireball.projectileHealthComponent = healthComponent;

            GameObject hurtBoxObject = new GameObject();
            hurtBoxObject.transform.SetParent(modelLocator.modelTransform);

            SphereCollider collider = hurtBoxObject.AddComponent<SphereCollider>();
            collider.isTrigger = true;
            collider.contactOffset = 0.01f;
            collider.radius = 3f;
            collider.sharedMaterial = Addressables.LoadAssetAsync<PhysicMaterial>(RoR2_Base_Common.physmatSuperFriction_physicMaterial).WaitForCompletion();
            collider.material = collider.sharedMaterial;

            HurtBox hurtBox = hurtBoxObject.gameObject.AddComponent<HurtBox>();
            if (hurtBox == null)
            {
                Log.Error($"hurtBox is null!");
            }
            if (hurtBox.gameObject == null)
            {
                Log.Error($"hurtBox.gameObject is null!");
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

            DisableCollisionsBetweenColliders dcbc = fireballProjectile.AddComponent<DisableCollisionsBetweenColliders>();
            dcbc.collidersA = [fireballProjectile.GetComponent<SphereCollider>()];
            dcbc.collidersB = [collider];

            AssignTeamFilterToTeamComponent teamFilterToTeamComponent = fireballProjectile.AddComponent<AssignTeamFilterToTeamComponent>();
        }
        public void ModifyProjectileGhost()
        {
            Transform[] transforms = fireballGhost.GetComponentsInChildren<Transform>();
            Transform magmaMeatballGeo = transforms.Where(t => t.gameObject.name == "MagmaMeatball_Geo").FirstOrDefault();
            Transform pointLightTransform = transforms.Where(t => t.gameObject.name == "Point Light").FirstOrDefault();
            pointLightTransform.localScale = Vector3.one;
            magmaMeatballGeo.transform.localScale = Vector3.one * 0.5f;
            ObjectScaleCurve scaleCurve = fireballGhost.GetComponent<ObjectScaleCurve>();
            AnimationCurve curve = new AnimationCurve { keys = [new Keyframe { time = 0f, value = 0.1f }, new Keyframe { time = 1f, value = 1f }] };
            scaleCurve.curveX = curve;
            scaleCurve.curveY = curve;
            scaleCurve.curveZ = curve;
            scaleCurve.timeMax = 1f;
            if (magmaMeatballGeo != null)
            {
                GameObject magmaMeatballGeoObject = magmaMeatballGeo.gameObject;
                MeshFilter meshFilter = magmaMeatballGeo.GetComponent<MeshFilter>();
                Mesh mesh = Addressables.LoadAssetAsync<GameObject>(RoR2_Base_Common_VFX.mdlVFXIcosphere_fbx).WaitForCompletion().GetComponent<MeshFilter>().mesh;
                meshFilter.mesh = mesh;
                meshFilter.sharedMesh = mesh;
            }
        }
        public class FireballCarousel : BaseSkillState
        {
            private static float baseSpinupDuration = FireballCarouselModule.spinupDuration.Value;
            private float spinupDuration;
            private static float baseFireDuration = 1f;
            private float fireDuration;
            private CarouselController controller;
            private static float fallbackDuration = baseSpinupDuration + baseFireDuration + 3f;

            private static float recoveryDuration = 0.25f;

            private static float startRadius = 14f;
            private static float endRadius = 7f;
            private float radius;
            private static float startDegreesPerSecond = 0f;
            private static float endDegreesPerSecond = 360f;
            private float degreesPerSecond;
            private float currentAngle;
            private bool allFireballsLaunched = false;
            private float damageCoefficient = (damageCoeff.Value / 100f);
            private float projectileSpeed = FireballCarouselModule.projectileSpeed.Value;

            private int projectileCount = (int)FireballCarouselModule.projectileCount.Value;

            private bool readyToLaunch = false;

            public override void OnEnter()
            {
                base.OnEnter();
                spinupDuration = baseSpinupDuration / attackSpeedStat;
                fireDuration = baseFireDuration / attackSpeedStat;
                for (int i = 0; i < projectileCount; i++)
                {
                    Vector3 centerPosition = characterBody.corePosition;
                    Vector3 aimDirection = inputBank.aimDirection;
                    Vector3 localUp = characterBody.transform.up;
                    Vector3 localRight = characterBody.transform.right;

                    float angleInterval = 360f / projectileCount;
                    float angleRad = angleInterval * i * Mathf.Deg2Rad;

                    Vector3 positionOffset = (localRight * Mathf.Sin(angleRad) + localUp * Mathf.Cos(angleRad)) * startRadius;
                    Vector3 spawnPosition = centerPosition + positionOffset;

                    DamageTypeCombo combo = new DamageTypeCombo { damageSource = DamageSource.Secondary, damageType = DamageType.Generic };
                    if (igniteOnHit.Value)
                    {
                        combo.damageType = DamageType.IgniteOnHit;
                    }
                    ProjectileManager.instance.FireProjectile(fireballProjectile, spawnPosition, Quaternion.identity, characterBody.gameObject, damageCoefficient * damageStat, 0f, RollCrit(), DamageColorIndex.Default, null, 0f, combo);
                }
                controller = characterBody.gameObject.GetComponent<CarouselController>();
            }
            public override void OnExit()
            {
                base.OnExit();
            }
            public override void FixedUpdate()
            {
                base.FixedUpdate();
                if (base.fixedAge > spinupDuration && !readyToLaunch)
                {
                    readyToLaunch = true;
                }
                float percentageCompletion = Mathf.Clamp01(base.fixedAge / spinupDuration);
                radius = startRadius + (endRadius - startRadius) * percentageCompletion;
                degreesPerSecond = startDegreesPerSecond + (endDegreesPerSecond - startDegreesPerSecond) * percentageCompletion;
                if (controller != null && controller.fireballs.Count > 0)
                {
                    float prevAngle = currentAngle;
                    currentAngle += degreesPerSecond * Time.fixedDeltaTime;
                    allFireballsLaunched = true;
                    for (int i = controller.fireballs.Count - 1; i >= 0; i--)
                    {
                        ProjectileCarouselFireball fireball = controller.fireballs[i];
                        if (fireball == null)
                        {
                            controller.fireballs.RemoveAt(i);
                            continue;
                        }
                        if (fireball.launched == false)
                        {
                            allFireballsLaunched = false;
                        }
                        UpdateFireballPosition(fireball, prevAngle, projectileCount - 1);
                    }
                }
                if (base.fixedAge > spinupDuration + fireDuration + recoveryDuration && allFireballsLaunched || base.fixedAge > fallbackDuration)
                {
                    outer.SetNextStateToMain();
                }
            }
            public void LaunchProjectile(ProjectileCarouselFireball fireball)
            {
                Util.PlaySound("Play_lunar_wisp_attack2_launch", fireball.gameObject); 
                fireball.destroyOnWorld = true;
                fireball.destroyOnEnemy = true;
                fireball.GetComponent<SphereCollider>().enabled = true;
                fireball.GetComponentInChildren<SphereCollider>().enabled = true;
                fireball.shouldNotExplode = false;
                fireball.fireChildren = leavesDotZone.Value;
                Rigidbody rigidbody = fireball.gameObject.GetComponent<Rigidbody>();

                Ray aimRay = GetAimRay();

                Vector3 aimDirection = aimRay.direction;

                bool success = Physics.Raycast(aimRay.origin, aimRay.direction, out RaycastHit hitInfo, 1000f, LayerIndex.CommonMasks.bullet);
                if (success)
                {
                    aimDirection = (hitInfo.point - fireball.gameObject.transform.position).normalized;
                }
                Vector3 forward = Util.ApplySpread(aimDirection, 0f, 5f, 1f, 1f);
                rigidbody.velocity = forward * projectileSpeed;
                fireball.transform.rotation = Util.QuaternionSafeLookRotation(forward);

            }
            public void UpdateFireballPosition(ProjectileCarouselFireball fireball, float prevAngleDeg, int maxIndex)
            {
                if (fireball.launched == true)
                {
                    return;
                }
                float offsetInterval = 360f / (maxIndex + 1);
                float offset = fireball.fireballIndex * offsetInterval;

                Vector3 centerPosition = characterBody.corePosition;
                Vector3 aimDirection = inputBank.aimDirection;
                Vector3 localUp = characterBody.transform.up;
                Vector3 localRight = characterBody.transform.right;

                float prevAngle = (prevAngleDeg + offset) * Mathf.Deg2Rad;
                float angle = (currentAngle + offset) * Mathf.Deg2Rad;

                if ((int)(angle / (Mathf.PI * 2)) != (int)(prevAngle / (Mathf.PI * 2)))
                {
                    if (readyToLaunch)
                    {
                        LaunchProjectile(fireball);
                        fireball.launched = true;
                        return;
                    }
                }
                Vector3 positionOffset = (localRight * Mathf.Sin(angle) + localUp * Mathf.Cos(angle)) * radius;
                Vector3 newPosition = centerPosition + positionOffset;
                Vector3 moveDirection = newPosition - fireball.transform.position;
                Quaternion rotation = Util.QuaternionSafeLookRotation(moveDirection);
                fireball.gameObject.transform.rotation = rotation;
                fireball.gameObject.transform.position = newPosition; 
            }
            public override InterruptPriority GetMinimumInterruptPriority()
            {
                return InterruptPriority.Frozen;
            }
        }
        public class CarouselController : MonoBehaviour
        {
            private CharacterBody body;
            public List<ProjectileCarouselFireball> fireballs = new List<ProjectileCarouselFireball>();
            public float stopwatch;

            public void Awake()
            {
                body = GetComponent<CharacterBody>();
            }
            public void AddFireball(ProjectileCarouselFireball fireball)
            {
                fireballs.Add(fireball);
                fireball.fireballIndex = fireballs.Count - 1;
            }
            public void RemoveFireball(ProjectileCarouselFireball fireball)
            {
                fireballs.Remove(fireball);
            }
        }
        [RequireComponent(typeof(ProjectileController))]
        public class ProjectileCarouselFireball : ProjectileExplosion, IProjectileImpactBehavior
        {
            public enum TransformSpace
            {
                World,
                Local,
                Normal
            }

            private Vector3 impactNormal = Vector3.up;

            public GameObject impactEffect;

            public string lifetimeExpiredSoundString;

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

            private CarouselController carouselController;

            public bool launched = false;

            public int fireballIndex;

            public bool shouldNotExplode = true;
            private bool inert = false;

            public override void Awake()
            {
                base.Awake();
            }

            public void Start()
            {
                startPos = base.transform.position;
                maxDistanceSqr = maxDistance * maxDistance;
                if (projectileController != null && projectileController.owner != null)
                {
                    carouselController = projectileController.owner.GetComponent<CarouselController>();
                    if (carouselController != null)
                    {
                        carouselController.AddFireball(this);
                    }
                }
            }

            public void FixedUpdate()
            {
                stopwatch += Time.fixedDeltaTime;
                if (!NetworkServer.active && !projectileController.isPrediction)
                {
                    return;
                }
                if ((projectileController == null || projectileController.owner == null) && launched == false)
                {
                    IgnoreProjectileDamageAndChildren();
                    Detonate();
                    return;
                }
                HealthComponent ownerHealthComponent = projectileController.owner.GetComponent<HealthComponent>();
                if (ownerHealthComponent == null || ownerHealthComponent.alive == false && launched == false)
                {
                    IgnoreProjectileDamageAndChildren();
                    Detonate();
                    return;
                }
                if (explodeOnLifeTimeExpiration && alive && stopwatch >= lifetime)
                {
                    explosionEffect = impactEffect ?? explosionEffect;
                    Detonate();
                    return;
                }
                if (timerAfterImpact && hasImpact)
                {
                    stopwatchAfterImpact += Time.fixedDeltaTime;
                }
                bool num = stopwatch >= lifetime;
                bool flag = timerAfterImpact && stopwatchAfterImpact > lifetimeAfterImpact;
                bool flag2 = (bool)projectileHealthComponent && !projectileHealthComponent.alive;
                bool flag3 = false;
                if (destroyOnDistance && (base.transform.position - startPos).sqrMagnitude >= maxDistanceSqr)
                {
                    flag3 = true;
                }
                if (num || flag || flag2 || flag3)
                {
                    alive = false;
                }
                if (flag2)
                {
                    IgnoreProjectileDamageAndChildren();
                }
                if (timerAfterImpact && hasImpact && minimumPhysicsStepsToKeepAliveAfterImpact != 0)
                {
                    minimumPhysicsStepsToKeepAliveAfterImpact--;
                    alive = true;
                }
                if (alive && !hasPlayedLifetimeExpiredSound)
                {
                    bool flag4 = stopwatch > lifetime - offsetForLifetimeExpiredSound;
                    if (timerAfterImpact)
                    {
                        flag4 |= stopwatchAfterImpact > lifetimeAfterImpact - offsetForLifetimeExpiredSound;
                    }
                    if (flag4)
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
                    return;
                }
            }
            public void IgnoreProjectileDamageAndChildren()
            {
                if (!inert)
                {
                    inert = true;
                    ProjectileDamage projectileDamage = GetComponent<ProjectileDamage>();
                    if (projectileDamage != null)
                    {
                        projectileDamage.damage = 0f;
                        projectileDamage.damageType = new DamageTypeCombo { damageType = DamageType.Generic, damageSource = DamageSource.Secondary };
                    }
                    fireChildren = false;
                }
            }
            public override Quaternion GetRandomDirectionForChild()
            {
                Quaternion randomChildRollPitch = GetRandomChildRollPitch();
                return transformSpace switch
                {
                    TransformSpace.Local => base.transform.rotation * randomChildRollPitch,
                    TransformSpace.Normal => Quaternion.FromToRotation(Vector3.forward, impactNormal) * randomChildRollPitch,
                    _ => randomChildRollPitch,
                };
            }

            public void OnProjectileImpact(ProjectileImpactInfo impactInfo)
            {
                if (shouldNotExplode)
                {
                    return;
                }
                if (!alive)
                {
                    return;
                }
                Collider collider = impactInfo.collider;
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

            public void OnDestroy()
            {
                if (carouselController != null)
                {
                    carouselController.RemoveFireball(this);
                }
            }
        }
    }
}
