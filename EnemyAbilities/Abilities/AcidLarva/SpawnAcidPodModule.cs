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

        public override void Awake()
        {
            base.Awake();
            CreateSkill();
            acidProjectilePrefab.AddComponent<ProjectileSpawnAcidPod>();
            ContentAddition.AddBody(acidPodPrefab);
            ModifyProjectile();

            //friendly-fire damage doubled for monster team
            larvaLeapESC.TryModifyFieldValue<float>(nameof(LarvaLeap.detonateSelfDamageFraction), (1f/6f));
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
        }
        public void CreateSkill()
        {
            SkillDef spawnAcidPod = ScriptableObject.CreateInstance<SkillDef>();
            (spawnAcidPod as ScriptableObject).name = "AcidLarvaBodySpawnAcidPod";
            spawnAcidPod.skillName = "AcidLarvaSpawnAcidPod";
            spawnAcidPod.activationStateMachineName = "Weapon";
            spawnAcidPod.activationState = ContentAddition.AddEntityState<SpawnAcidPod>(out _);
            spawnAcidPod.baseRechargeInterval = acidPodCooldown.Value;
            spawnAcidPod.canceledFromSprinting = false;
            spawnAcidPod.isCombatSkill = true;
            ContentAddition.AddSkillDef(spawnAcidPod);

            SkillFamily skillFamily = ScriptableObject.CreateInstance<SkillFamily>();
            (skillFamily as ScriptableObject).name = "AcidLarvaSecondaryFamily";
            skillFamily.variants = [new SkillFamily.Variant() { skillDef = spawnAcidPod }];

            GenericSkill skill = bodyPrefab.AddComponent<GenericSkill>();
            skill.skillName = "AcidLarvaSpawnAcidPod";
            skill._skillFamily = skillFamily;

            SkillLocator locator = bodyPrefab.GetComponent<SkillLocator>();
            locator.secondary = skill;

            ContentAddition.AddSkillFamily(skillFamily);

            AISkillDriver strafeWhileWaitingForLeap = masterPrefab.GetComponents<AISkillDriver>().Where(driver => driver.customName == "StrafeWhileWaitingForLeap").FirstOrDefault();
            if (strafeWhileWaitingForLeap != null)
            {
                strafeWhileWaitingForLeap.driverUpdateTimerOverride = -1f;
            }
            else
            {
                Log.Error($"Could not find StrafeWhileWaitingForLeap ai skill driver!");
            }

            AISkillDriver useSecondary = masterPrefab.AddComponent<AISkillDriver>();
            useSecondary.customName = "useSecondary";
            useSecondary.skillSlot = SkillSlot.Secondary;
            useSecondary.requireSkillReady = true;
            useSecondary.minDistance = 0f;
            useSecondary.maxDistance = acidPodMaxUseRange.Value;
            useSecondary.selectionRequiresTargetLoS = true;
            useSecondary.selectionRequiresAimTarget = true;
            useSecondary.moveTargetType = AISkillDriver.TargetType.CurrentEnemy;
            useSecondary.movementType = AISkillDriver.MovementType.ChaseMoveTarget;
            useSecondary.aimType = AISkillDriver.AimType.AtMoveTarget;
            useSecondary.shouldSprint = true;
            masterPrefab.ReorderSkillDrivers(useSecondary, 1);
        }
        public class SpawnAcidPod : BaseSkillState
        {
            private static float maxDistance = acidPodMaxUseRange.Value;
            private static float timeToTarget = acidPodTravelTime.Value;
            private static float baseDuration = 0.25f;
            public override void OnEnter()
            {
                base.OnEnter();
                PlayCrossfade("Gesture, Override", "LarvaLeap", 0.1f);
                Util.PlaySound("Play_acid_larva_spawn", characterBody.gameObject);
                Log.Debug($"Using SpawnAcidPod");
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

            public static float explosionRadius = acidPodRange.Value;

            public static float explosionDamageCoefficient = acidPodDamage.Value / 100f;

            public static float explosionProcCoefficient = acidPodPoisonDuration.Value / 10f;

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
