using EntityStates;
using RoR2;
using R2API;
using RoR2.Skills;
using RoR2BepInExPack.GameAssetPaths.Version_1_35_0;
using UnityEngine;
using UnityEngine.AddressableAssets;
using RoR2.CharacterAI;
using JetBrains.Annotations;
using RoR2.Projectile;

namespace EnemyAbilities.Abilities.ClayBruiser
{

    //issues: deviation means at tight angles sometimes 4/5 of the grenades will hit a surface whilst flying
    //need a visual cue/animation, maybe firing tar blast directly up?

    //made this before I learnt Trajectory exists :(((

    [EnemyAbilities.ModuleInfo("Cluster Grenade", "Gives Clay Templars a new utility:\n- Cluster Grenade: Used when a Clay Templar has seen a player recently but doesn't have line of sight, fires a barrage of five Tar Grenades in an arc towards the player.", "Clay Templar", true)]
    public class ClusterGrenadeModule : BaseModule
    {
        public static GameObject projectilePrefab = Addressables.LoadAssetAsync<GameObject>(RoR2_Base_Commando.CommandoGrenadeProjectile_prefab).WaitForCompletion().InstantiateClone("clayGrenadeProjectile");

        private static GameObject bodyPrefab = Addressables.LoadAssetAsync<GameObject>(RoR2_Base_ClayBruiser.ClayBruiserBody_prefab).WaitForCompletion();
        private static GameObject masterPrefab = Addressables.LoadAssetAsync<GameObject>(RoR2_Base_ClayBruiser.ClayBruiserMaster_prefab).WaitForCompletion();
        private static GameObject ghostPrefab = Addressables.LoadAssetAsync<GameObject>(RoR2_Base_Commando.CommandoGrenadeGhost_prefab).WaitForCompletion().InstantiateClone("clayGrenadeProjectileGhost");
        private static GameObject explosionPrefab = Addressables.LoadAssetAsync<GameObject>(RoR2_DLC1_ClayGrenadier.ClayGrenadierBarrelExplosion_prefab).WaitForCompletion();

        public override void Awake()
        {
            base.Awake();
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
            impactExplosion.blastRadius = 6f;
            impactExplosion.impactEffect = explosionPrefab;
        }
        private void CreateSkill()
        {
            ClusterGrenadeSkillDef clusterGrenade = ScriptableObject.CreateInstance<ClusterGrenadeSkillDef>();
            (clusterGrenade as ScriptableObject).name = "ClayBruiserBodyClusterGrenade";
            clusterGrenade.skillName = "ClayBruiserClusterGrenade";
            clusterGrenade.activationStateMachineName = "Weapon";
            clusterGrenade.activationState = ContentAddition.AddEntityState<FireClusterGrenades>(out _);
            clusterGrenade.baseRechargeInterval = 10f;
            clusterGrenade.cancelSprintingOnActivation = true;
            clusterGrenade.isCombatSkill = true;
            ContentAddition.AddSkillDef(clusterGrenade);

            SkillFamily skillFamily = ScriptableObject.CreateInstance<SkillFamily>();
            (skillFamily as ScriptableObject).name = "ClayBruiserUtilityFamily";
            skillFamily.variants = [new SkillFamily.Variant() { skillDef = clusterGrenade }];

            GenericSkill skill = bodyPrefab.AddComponent<GenericSkill>();
            skill.skillName = "ClayBruiserClusterGrenade";
            skill._skillFamily = skillFamily;

            SkillLocator locator = bodyPrefab.GetComponent<SkillLocator>();
            locator.utility = skill;

            ContentAddition.AddSkillFamily(skillFamily);

            //skillDriver: when we don't have line of sight but are in range to shoot - fire

            AISkillDriver useUtility = masterPrefab.AddComponent<AISkillDriver>();
            useUtility.customName = "chaseAndUseUtility";
            useUtility.skillSlot = SkillSlot.Utility;
            useUtility.requireSkillReady = true;
            useUtility.minDistance = 10f;
            useUtility.maxDistance = 75f;
            useUtility.selectionRequiresTargetLoS = false;
            useUtility.moveTargetType = AISkillDriver.TargetType.CurrentEnemy;
            useUtility.movementType = AISkillDriver.MovementType.ChaseMoveTarget;
            masterPrefab.ReorderSkillDrivers(useUtility, 2);
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
            if (!data.controller.validArcFound || data.controller.recentlySawTarget || data.controller.hasLoS)
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
        private int grenadeCount = 5;
        private int grenadeIndex = 0;
        private float grenadeFireTimer = 0f;
        private float grenadeFireInterval;
        private static float baseGrenadeFireInterval = 0.15f;
        public override void OnEnter()
        {
            base.OnEnter();
            grenadeFireInterval = baseGrenadeFireInterval / attackSpeedStat;
            duration = baseGrenadeFireInterval * grenadeCount;
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

                //could maybe do an "updatearc" for the controller to avoid this?
                Vector3 velocity = controller.FindBallisticVelocity(characterBody.aimOrigin, controller.ai.currentEnemy.characterBody.transform.position, controller.currentLowestArcTime);

                Vector3 direction = velocity.normalized;
                float speedOverride = velocity.magnitude;
                Vector3 randomDeviation = Vector3.zero;
                if (grenadeIndex != 0)
                {
                    randomDeviation = UnityEngine.Random.insideUnitSphere * 0.025f;
                }
                ProjectileManager.instance.FireProjectile(ClusterGrenadeModule.projectilePrefab, characterBody.aimOrigin, Util.QuaternionSafeLookRotation(direction + randomDeviation), characterBody.gameObject, damageStat * 1f, 1000f, RollCrit(), DamageColorIndex.Default, null, speedOverride, DamageType.ClayGoo);
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
            }
            skillLocator = bruiserBody.skillLocator;
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
                for (int t = travelTimeMin; t < travelTimeMax + 1 ; t += travelTimeInterval)
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

            float simulationInterval = 0.5f;
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
