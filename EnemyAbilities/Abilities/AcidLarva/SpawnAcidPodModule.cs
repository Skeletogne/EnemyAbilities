using System.Linq;
using EntityStates;
using R2API;
using RoR2;
using RoR2.CharacterAI;
using RoR2.Projectile;
using RoR2.Skills;
using RoR2BepInExPack.GameAssetPaths.Version_1_39_0;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Networking;
using MiscFixes.Modules;
using static EnemyAbilities.PluginConfig;
using EntityStates.AcidLarva;
using BepInEx.Configuration;

namespace EnemyAbilities.Abilities.AcidLarva
{
    [EnemyAbilities.ModuleInfo("Caustic Pod", "Gives Larvae a new secondary:\n- Caustic Pod: Fires off a short-range caustic pod that deals no impact damage, but leaves an Acid Pod at the impact location which detonates on taking damage from any source.\nEnabling this option reduces Larva self-damage from 1/2 of their max health to 1/3.", "Larva", true)]
    public class SpawnAcidPodModule : BaseModule
    {

        private static GameObject bodyPrefab = Addressables.LoadAssetAsync<GameObject>(RoR2_DLC1_AcidLarva.AcidLarvaBody_prefab).WaitForCompletion();
        private static GameObject masterPrefab = Addressables.LoadAssetAsync<GameObject>(RoR2_DLC1_AcidLarva.AcidLarvaMaster_prefab).WaitForCompletion();
        private static GameObject acidProjectilePrefab = Addressables.LoadAssetAsync<GameObject>(RoR2_Base_BeetleQueen.BeetleQueenSpit_prefab).WaitForCompletion().InstantiateClone("acidLarvaProjectile");
        private static GameObject acidProjectileGhostPrefab = Addressables.LoadAssetAsync<GameObject>(RoR2_Base_BeetleQueen.BeetleQueenSpitGhost_prefab).WaitForCompletion().InstantiateClone("acidLarvaProjectileGhost");
        private static GameObject acidPodPrefab = Addressables.LoadAssetAsync<GameObject>(RoR2_DLC1_SulfurPod.SulfurPodBody_prefab).WaitForCompletion().InstantiateClone("acidLarvaPod");
        private static EntityStateConfiguration larvaLeapESC = Addressables.LoadAssetAsync<EntityStateConfiguration>(RoR2_DLC1_AcidLarva.EntityStates_AcidLarva_LarvaLeap_asset).WaitForCompletion();

        internal static ConfigEntry<float> cooldown;
        internal static ConfigEntry<float> damage;
        internal static ConfigEntry<float> lifetime;
        internal static ConfigEntry<float> useRange;
        internal static ConfigEntry<float> poisonDuration;
        internal static ConfigEntry<float> explosionRadius;
        internal static ConfigEntry<float> travelTime;

        public override void RegisterConfig()
        {
            base.RegisterConfig();
            cooldown = BindFloat("Caustic Pod Cooldown", 15f, "Cooldown of the ability", 5f, 30f, 1f, FormatType.Time);
            damage = BindFloat("Caustic Pod Damage", 100f, "Damage coefficient of the pod explosion", 25f, 300f, 1f, FormatType.Percentage);
            lifetime = BindFloat("Caustic Pod Lifetime", 30f, "How long the pod lasts before despawning", 10f, 60f, 1f, FormatType.Time);
            useRange = BindFloat("Caustic Pod Use Range", 30f, "Max range to use the ability", 15f, 50f, 0.1f, FormatType.Distance);
            poisonDuration = BindFloat("Caustic Pod Poison Duration", 5f, "Duration of the poison effect", 0f, 10f, 0.1f, FormatType.Time);
            explosionRadius = BindFloat("Caustic Pod Explosion Radius", 12f, "Radius of the pod explosion", 6f, 18f, 0.1f, FormatType.Distance);
            travelTime = BindFloat("Caustic Pod Travel Time", 1.5f, "Time for the projectile to reach its target", 0.5f, 3f, 0.1f, FormatType.Time);
        }
        public override void Initialise()
        {
            base.Initialise();
            CreateSkill();
            acidProjectilePrefab.AddComponent<ProjectileSpawnAcidPod>();
            ContentAddition.AddBody(acidPodPrefab);
            ModifyProjectile();
            //friendly-fire damage doubled for monster team, so this is 1/3
            larvaLeapESC.TryModifyFieldValue<float>(nameof(LarvaLeap.detonateSelfDamageFraction), (1f / 6f));
        }
        public void ModifyProjectile()
        {
            Light light = acidPodPrefab.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = Color.yellow;
            light.range = 8f;
            light.intensity = 20f;
            light.renderMode = LightRenderMode.Auto;
            ProjectileController controller = acidProjectilePrefab.GetComponent<ProjectileController>();
            controller.ghostPrefab = acidProjectileGhostPrefab;
            ProjectileImpactExplosion impactExplosion = acidProjectilePrefab.GetComponent<ProjectileImpactExplosion>();
            impactExplosion.fireChildren = false;
            impactExplosion.destroyOnEnemy = true;
            impactExplosion.destroyOnWorld = true;
            ParticleSystemRenderer renderer = acidProjectileGhostPrefab.GetComponentInChildren<ParticleSystemRenderer>();
            renderer.material = Addressables.LoadAssetAsync<Material>(RoR2_DLC1_AcidLarva.matAcidLarvaSacs_mat).WaitForCompletion();
            renderer.materials = [renderer.material];
            CharacterDeathBehavior deathBehavior = acidPodPrefab.GetComponent<CharacterDeathBehavior>();
            if (deathBehavior != null)
            {
                deathBehavior.deathState = ContentAddition.AddEntityState<AcidLarvaPodDeath>(out _);
            }
            DestroyOnTimer destroyOnTimer = acidPodPrefab.AddComponent<DestroyOnTimer>();
            destroyOnTimer.duration = 30f;
            ModelLocator modelLocator = acidPodPrefab.GetComponent<ModelLocator>();
            modelLocator.modelBaseTransform.localScale = Vector3.one * 1.5f;
            CharacterBody body = acidPodPrefab.GetComponent<CharacterBody>();
            body.baseDamage = 12f;
            body.levelDamage = 2.4f;
            body.baseNameToken = "SKELETOGNE_ACIDPOD_BODY_NAME";
            LanguageAPI.Add("SKELETOGNE_ACIDPOD_BODY_NAME", "Caustic Pod");
        }
        public void CreateSkill()
        {
            SkillDefData skillData = new SkillDefData
            {
                objectName = "AcidLarvaBodySpawnAcidPod",
                skillName = "AcidLarvaSpawnAcidPod",
                esmName = "Weapon",
                activationState = ContentAddition.AddEntityState<SpawnAcidPod>(out _),
                cooldown = cooldown.Value,
                combatSkill = true
            };
            SkillDef spawnAcidPod = CreateSkillDef<SkillDef>(skillData);
            CreateGenericSkill(bodyPrefab, skillData.skillName, "AcidLarvaSecondaryFamily", spawnAcidPod, SkillSlot.Secondary);
            AISkillDriverData driverData = new AISkillDriverData
            {
                masterPrefab = masterPrefab,
                customName = "useSecondary",
                skillSlot = SkillSlot.Secondary,
                requireReady = true,
                requiredSkillDef = spawnAcidPod,
                maxDistance = useRange.Value,
                selectionRequiresTargetLoS = true,
                selectionRequiresAimTarget = true,
                targetType = AISkillDriver.TargetType.CurrentEnemy,
                movementType = AISkillDriver.MovementType.ChaseMoveTarget,
                aimType = AISkillDriver.AimType.AtMoveTarget,
                shouldSprint = true,
                desiredIndex = 1
            };
            CreateAISkillDriver(driverData);
            AISkillDriver strafeWhileWaitingForLeap = masterPrefab.GetComponents<AISkillDriver>().Where(driver => driver.customName == "StrafeWhileWaitingForLeap").FirstOrDefault();
            if (strafeWhileWaitingForLeap != null)
            {
                strafeWhileWaitingForLeap.driverUpdateTimerOverride = -1f;
            }
            else
            {
                Log.Error($"Could not find StrafeWhileWaitingForLeap ai skill driver!");
            }
        }
        public class SpawnAcidPod : BaseSkillState
        {
            private static float maxDistance = useRange.Value;
            private static float timeToTarget = travelTime.Value;
            private static float baseDuration = 0.25f;
            public override void OnEnter()
            {
                base.OnEnter();
                PlayCrossfade("Gesture, Override", "LarvaLeap", 0.1f);
                Util.PlaySound("Play_acid_larva_spawn", characterBody.gameObject);
                Ray aimRay = GetAimRay();
                Vector3 targetPosition = aimRay.GetPoint(maxDistance);
                bool foundTarget = false;
                if (characterBody.master != null)
                {
                    BaseAI ai = characterBody.master.GetComponent<BaseAI>();
                    if (ai != null && ai.currentEnemy != null && ai.currentEnemy.characterBody != null)
                    {
                        foundTarget = true;
                        targetPosition = ai.currentEnemy.characterBody.footPosition;
                    }
                }
                if (!foundTarget)
                {
                    bool success = Physics.Raycast(aimRay.origin, aimRay.direction, out RaycastHit hit, maxDistance, LayerIndex.CommonMasks.bullet);
                    if (success)
                    {
                        targetPosition = hit.point;
                    }
                }
                if (base.isAuthority)
                {
                    Vector3 initialVelocity = Trajectory.CalculateInitialVelocityFromTime(aimRay.origin, targetPosition, timeToTarget);
                    float magnitude = initialVelocity.magnitude;
                    Quaternion rotation = Util.QuaternionSafeLookRotation(initialVelocity.normalized);
                    DamageTypeCombo combo = new DamageTypeCombo { damageType = DamageType.Generic, damageSource = DamageSource.Secondary };
                    ProjectileManager.instance.FireProjectile(acidProjectilePrefab, aimRay.origin, rotation, characterBody.gameObject, 0f, 1000f, RollCrit(), speedOverride: magnitude, damageType: combo);
                }
            }
            public override void FixedUpdate()
            {
                base.FixedUpdate();
                if (base.fixedAge > baseDuration)
                {
                    outer.SetNextStateToMain();
                }
            }
            public override void OnExit()
            {
                base.OnExit();
                PlayAnimation("Gesture, Override", Animator.StringToHash("Empty"));
            }
            public override InterruptPriority GetMinimumInterruptPriority()
            {
                return InterruptPriority.PrioritySkill;
            }
        }
        public class ProjectileSpawnAcidPod : MonoBehaviour, IProjectileImpactBehavior
        {
            private bool spawnedPod;
            public void OnProjectileImpact(ProjectileImpactInfo info)
            {

                if (!spawnedPod)
                {
                    spawnedPod = true;
                    Vector3 spawnPodPosition = gameObject.transform.position;
                    Quaternion spawnPodRotation = Quaternion.identity;
                    bool success = Physics.Raycast(gameObject.transform.position, Vector3.down, out RaycastHit hit, 1000f, LayerIndex.world.mask);
                    if (success)
                    {
                        spawnPodPosition = hit.point;
                        Vector3 tangent = Vector3.ProjectOnPlane(Vector3.forward, hit.normal);
                        spawnPodRotation = Util.QuaternionSafeLookRotation(tangent);
                    }
                    GameObject pod = Instantiate(acidPodPrefab, spawnPodPosition + (success ? hit.normal * 0.25f : Vector3.zero), spawnPodRotation);
                    NetworkServer.Spawn(pod);
                }
            }
        }
        public class AcidLarvaPodDeath : GenericCharacterDeath
        {

            public static GameObject explosionEffectPrefab = Addressables.LoadAssetAsync<GameObject>(RoR2_DLC1_SulfurPod.SulfurPodExplosion_prefab).WaitForCompletion();

            public static float explosionRadius = useRange.Value;

            public static float explosionDamageCoefficient = damage.Value / 100f;

            public static float explosionProcCoefficient = poisonDuration.Value / 10f;

            public static float explosionForce = 1500f;

            private bool hasExploded;

            public override void OnEnter()
            {
                base.OnEnter();
                Explode();
            }
            private void Explode()
            {
                if (!hasExploded)
                {
                    hasExploded = true;
                    if ((bool)explosionEffectPrefab)
                    {
                        EffectManager.SpawnEffect(explosionEffectPrefab, new EffectData
                        {
                            origin = base.transform.position,
                            scale = explosionRadius,
                            rotation = Quaternion.identity
                        }, transmit: true);
                    }
                    DestroyModel();
                    if (NetworkServer.active)
                    {
                        BlastAttack blastAttack = new BlastAttack();
                        blastAttack.attacker = base.gameObject;
                        blastAttack.damageColorIndex = DamageColorIndex.Poison;
                        blastAttack.baseDamage = damageStat * explosionDamageCoefficient * Run.instance.teamlessDamageCoefficient;
                        blastAttack.radius = explosionRadius;
                        blastAttack.falloffModel = BlastAttack.FalloffModel.None;
                        blastAttack.procCoefficient = explosionProcCoefficient;
                        blastAttack.teamIndex = TeamIndex.None;
                        blastAttack.damageType = DamageType.PoisonOnHit;
                        blastAttack.position = base.transform.position;
                        blastAttack.baseForce = explosionForce;
                        blastAttack.attackerFiltering = AttackerFiltering.NeverHitSelf;
                        blastAttack.Fire();
                        DestroyBodyAsapServer();
                    }
                }
            }
            public override InterruptPriority GetMinimumInterruptPriority()
            {
                return InterruptPriority.Death;
            }
        }
    }
}
