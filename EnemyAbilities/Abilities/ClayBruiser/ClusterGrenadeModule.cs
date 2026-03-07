using EntityStates;
using RoR2;
using R2API;
using RoR2.Skills;
using RoR2BepInExPack.GameAssetPaths.Version_1_39_0;
using UnityEngine;
using UnityEngine.AddressableAssets;
using RoR2.CharacterAI;
using JetBrains.Annotations;
using RoR2.Projectile;
using BepInEx.Configuration;

namespace EnemyAbilities.Abilities.ClayBruiser
{
    [EnemyAbilities.ModuleInfo("Cluster Grenade", "Gives Clay Templars a new utility:\n- Cluster Grenade: Used when a Clay Templar has seen a player recently but doesn't have line of sight, fires a barrage of five Tar Grenades in an arc towards the player.", "Clay Templar", true)]
    public class ClusterGrenadeModule : BaseModule
    {
        public static GameObject projectilePrefab = Addressables.LoadAssetAsync<GameObject>(RoR2_Base_Commando.CommandoGrenadeProjectile_prefab).WaitForCompletion().InstantiateClone("clayGrenadeProjectile");
        private static GameObject bodyPrefab = Addressables.LoadAssetAsync<GameObject>(RoR2_Base_ClayBruiser.ClayBruiserBody_prefab).WaitForCompletion();
        private static GameObject masterPrefab = Addressables.LoadAssetAsync<GameObject>(RoR2_Base_ClayBruiser.ClayBruiserMaster_prefab).WaitForCompletion();
        private static GameObject ghostPrefab = Addressables.LoadAssetAsync<GameObject>(RoR2_Base_Commando.CommandoGrenadeGhost_prefab).WaitForCompletion().InstantiateClone("clayGrenadeProjectileGhost");
        private static GameObject explosionPrefab = Addressables.LoadAssetAsync<GameObject>(RoR2_DLC1_ClayGrenadier.ClayGrenadierBarrelExplosion_prefab).WaitForCompletion();

        internal static ConfigEntry<float> grenadeCount;
        internal static ConfigEntry<float> grenadeDamageCoeff;
        internal static ConfigEntry<float> grenadeExplosionRadius;
        internal static ConfigEntry<float> grenadeCooldown;

        public override void RegisterConfig()
        {
            base.RegisterConfig();
            grenadeCount = BindFloat("Grenade Count", 5f, "The number of grenades the Templar fires on ability use.", 1f, 10f, 1f);
            grenadeCooldown = BindFloat("Grenade Cooldown", 10f, "Cooldown before the ability can activated again.", 5f, 30f, 0.1f, PluginConfig.FormatType.Time);
            grenadeDamageCoeff = BindFloat("Grenade Damage Coefficient", 100f, "Percentage multiplier to the Templar's Damage to get explosion damage. Uses falloff model sweet spot", 50f, 500f, 5f, PluginConfig.FormatType.Percentage);
            grenadeExplosionRadius = BindFloat("Grenade Explosion Radius", 6f, "Grenade Explosion radius in metres", 1f, 10f, 1f, PluginConfig.FormatType.Distance);
        }
        public override void Initialise()
        {
            base.Initialise();
            CreateSkill();
            SetUpProjectilePrefab();
            bodyPrefab.AddComponent<ClayBruiserUtilityController>();
        }
        private void SetUpProjectilePrefab()
        {
            Rigidbody rigid = projectilePrefab.GetComponent<Rigidbody>();
            ProjectileSimple simple = projectilePrefab.GetComponent<ProjectileSimple>();
            simple.lifetime = 10f;
            ProjectileController controller = projectilePrefab.GetComponent<ProjectileController>();
            SphereCollider collider = projectilePrefab.GetComponent<SphereCollider>();
            PhysicMaterial material = new PhysicMaterial
            {
                bounciness = 0f,
                bounceCombine = PhysicMaterialCombine.Minimum,
                dynamicFriction = 1000f,
                staticFriction = 1000f,
                frictionCombine = PhysicMaterialCombine.Maximum,
            };
            collider.material = material;
            collider.sharedMaterial = material;

            MeshRenderer renderer = ghostPrefab.GetComponentInChildren<MeshRenderer>();
            Material rendererMaterial = Addressables.LoadAssetAsync<Material>(RoR2_DLC1_ClayGrenadier.matClayGrenadierGrenade_mat).WaitForCompletion();
            renderer.material = rendererMaterial;
            renderer.sharedMaterial = rendererMaterial;

            controller.ghostPrefab = ghostPrefab;
            ProjectileImpactExplosion impactExplosion = projectilePrefab.GetComponent<ProjectileImpactExplosion>();
            impactExplosion.blastRadius = grenadeExplosionRadius.Value;
            impactExplosion.impactEffect = explosionPrefab;
        }
        private void CreateSkill()
        {
            SkillDefData skillData = new SkillDefData
            {
                objectName = "ClayBruiserBodyClusterGrenade",
                skillName = "ClayBruiserClusterGrenade",
                esmName = "Weapon",
                activationState = ContentAddition.AddEntityState<FireClusterGrenades>(out _),
                cooldown = grenadeCooldown.Value,
                combatSkill = true
            };
            ClusterGrenadeSkillDef clusterGrenade = CreateSkillDef<ClusterGrenadeSkillDef>(skillData);
            CreateGenericSkill(bodyPrefab, clusterGrenade.skillName, "ClayBruiserUtilityFamily", clusterGrenade, SkillSlot.Utility);
            AISkillDriverData driverData = new AISkillDriverData
            {
                masterPrefab = masterPrefab,
                customName = "chaseAndUseUtility",
                skillSlot = SkillSlot.Utility,
                requireReady = true,
                minDistance = 10f,
                maxDistance = 75f,
                selectionRequiresTargetLoS = false,
                aimType = AISkillDriver.AimType.AtCurrentEnemy,
                movementType = AISkillDriver.MovementType.ChaseMoveTarget,
                targetType = AISkillDriver.TargetType.CurrentEnemy,
                desiredIndex = 2
            };
            CreateAISkillDriver(driverData);
        }

    }
    public class ClusterGrenadeSkillDef : SkillDef
    {
        private class InstanceData : BaseSkillInstanceData
        {
            public ClayBruiserUtilityController controller;
        }

        public override BaseSkillInstanceData OnAssigned(GenericSkill skillSlot)
        {
            return new InstanceData
            {
                controller = skillSlot.characterBody.GetComponent<ClayBruiserUtilityController>()
            };
        }
        public override bool IsReady([NotNull] GenericSkill skillSlot)
        {
            InstanceData data = skillSlot.skillInstanceData as InstanceData;
            if (data?.controller == null)
            {
                return false;
            }
            bool usableNormally = base.IsReady(skillSlot);
            if (!data.controller.validArcFound || !data.controller.recentlySawTarget || data.controller.hasLoS)
            {
                return false;
            }
            return usableNormally;
        }
    }
    public class FireClusterGrenades : BaseSkillState
    {

        private float duration;
        private ClayBruiserUtilityController controller;
        private int grenadeCount = (int)ClusterGrenadeModule.grenadeCount.Value;
        private int grenadeIndex = 0;
        private float grenadeFireTimer = 0f;
        private float grenadeFireInterval;
        private static float baseGrenadeFireInterval = 0.15f;
        public override void OnEnter()
        {
            base.OnEnter();
            grenadeFireInterval = baseGrenadeFireInterval / attackSpeedStat;
            duration = grenadeFireInterval * grenadeCount;
            controller = characterBody.gameObject.GetComponent<ClayBruiserUtilityController>();
        }
        public override void OnExit()
        {
            base.OnExit();
        }
        public override void FixedUpdate()
        {
            base.FixedUpdate();

            grenadeFireTimer -= Time.fixedDeltaTime;
            if (grenadeFireTimer <= 0f && grenadeIndex < grenadeCount)
            {
                Util.PlaySound("Play_clayBruiser_attack2_shoot", characterBody.gameObject);
                grenadeFireTimer += grenadeFireInterval;
                if (controller.ai == null || controller.ai.currentEnemy == null || controller.ai.currentEnemy.characterBody == null)
                {
                    outer.SetNextStateToMain();
                    return;
                }
                Vector3 velocity = controller.FindBallisticVelocity(characterBody.aimOrigin, controller.ai.currentEnemy.characterBody.transform.position, controller.currentLowestArcTime);

                Vector3 direction = velocity.normalized;
                float speedOverride = velocity.magnitude;
                Vector3 randomDeviation = Vector3.zero;
                if (grenadeIndex != 0)
                {
                    randomDeviation = UnityEngine.Random.insideUnitSphere * 0.025f;
                }
                if (base.isAuthority)
                {
                    DamageTypeCombo combo = new DamageTypeCombo { damageSource = DamageSource.Utility, damageType = DamageType.ClayGoo };
                    ProjectileManager.instance.FireProjectile(ClusterGrenadeModule.projectilePrefab, characterBody.aimOrigin, Util.QuaternionSafeLookRotation(direction + randomDeviation), characterBody.gameObject, damageStat * (ClusterGrenadeModule.grenadeDamageCoeff.Value / 100f), 1000f, RollCrit(), DamageColorIndex.Default, null, speedOverride, combo);
                }
                grenadeIndex++;
            }
            if (base.fixedAge > duration && grenadeIndex == grenadeCount)
            {
                outer.SetNextStateToMain();
            }
        }
        public override InterruptPriority GetMinimumInterruptPriority()
        {
            return InterruptPriority.PrioritySkill;
        }
    }
    public class ClayBruiserUtilityController : MonoBehaviour
    {
        private CharacterBody bruiserBody;
        public BaseAI ai;
        private float arcCheckTimer = 0f;
        private static float arcCheckInterval = 0.25f;
        private SkillLocator skillLocator;
        public float currentLowestArcTime;
        public bool validArcFound;
        public Vector3 lowestArcVelocity;
        private static int travelTimeMin = 3;
        private static int travelTimeMax = 4;
        private static int travelTimeInterval = 1;
        private float timeSinceLastSawTarget = 0f;
        private static float maxTimeSinceLastSawTarget = 10f;
        public bool recentlySawTarget = false;
        public bool hasLoS = false;
        public void Awake()
        {
            bruiserBody = GetComponent<CharacterBody>();
        }
        public void Start()
        {
            if (bruiserBody != null && bruiserBody.master != null)
            {
                ai = bruiserBody.master.GetComponent<BaseAI>();
                skillLocator = bruiserBody.skillLocator;
            }
        }
        public void FixedUpdate()
        {

            if (ai != null && ai.currentEnemy != null)
            {
                if (ai.currentEnemy.hasLoS)
                {
                    hasLoS = true;
                    timeSinceLastSawTarget = 0f;
                }
                else
                {
                    hasLoS = false;
                    timeSinceLastSawTarget += Time.fixedDeltaTime;
                }
            }
            recentlySawTarget = timeSinceLastSawTarget < maxTimeSinceLastSawTarget;
            arcCheckTimer -= Time.fixedDeltaTime;
            if (arcCheckTimer <= 0)
            {
                bool success = false;
                arcCheckTimer += arcCheckInterval;
                for (int t = travelTimeMin; t < travelTimeMax + 1; t += travelTimeInterval)
                {
                    Vector3 launchVelocity;
                    bool arcValid = IsBallisticArcValid(t, out launchVelocity);
                    if (arcValid)
                    {
                        currentLowestArcTime = t;
                        lowestArcVelocity = launchVelocity;
                        success = true;
                        break;
                    }
                }
                validArcFound = success;
            }
        }
        private bool IsBallisticArcValid(float time, out Vector3 launchVelocty)
        {
            launchVelocty = Vector3.zero;
            if (ai == null || ai.currentEnemy == null || ai.currentEnemy.characterBody == null)
            {
                return false;
            }
            Vector3 targetPosition = ai.currentEnemy.characterBody.transform.position;
            Vector3 startPosition = bruiserBody.aimOrigin;
            Vector3 startVelocity = FindBallisticVelocity(startPosition, targetPosition, time);
            launchVelocty = startVelocity;

            float simulationInterval = 0.25f;
            float simulationTime = 0f;

            while (simulationTime < time)
            {
                Vector3 point1 = GetArcPoint(startPosition, startVelocity, simulationTime);
                Vector3 point2 = GetArcPoint(startPosition, startVelocity, Mathf.Min(simulationTime + simulationInterval, time));
                Vector3 direction = (point2 - point1).normalized;
                float distance = Vector3.Distance(point1, point2);
                if (Physics.Raycast(point1, direction, out RaycastHit hit, distance, LayerIndex.world.mask, QueryTriggerInteraction.Ignore))
                {
                    return false;
                }
                simulationTime += simulationInterval;
            }
            return true;

        }
        public Vector3 FindBallisticVelocity(Vector3 start, Vector3 target, float time)
        {
            Vector3 gravity = Physics.gravity;
            Vector3 displacement = target - start;
            return (displacement / time) - (0.5f * gravity * time);
        }
        private Vector3 GetArcPoint(Vector3 startPosition, Vector3 startVelocity, float time)
        {
            return (startPosition + time * startVelocity + 0.5f * Physics.gravity * Mathf.Pow(time, 2f));
        }
    }
}
