using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using EntityStates;
using EntityStates.Bell.BellWeapon;
using JetBrains.Annotations;
using R2API;
using Rewired.ComponentControls.Effects;
using RoR2;
using RoR2.CharacterAI;
using RoR2.Projectile;
using RoR2.Skills;
using RoR2BepInExPack.GameAssetPaths.Version_1_39_0;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Networking;

namespace EnemyAbilities.Abilities.Bell
{
    [EnemyAbilities.ModuleInfo("Explosive Toll", "Gives Brass Contraptions a new Special ability:\n-Explosive Toll: Brass Contraptions can remotely detonate nearby embedded spike-balls, sending out a signal that causes them to explode after a brief delay.", "Brass Contraption", true)]
    public class RemoteDetonationModule : BaseModule
    {
        private static GameObject bodyPrefab = Addressables.LoadAssetAsync<GameObject>(RoR2_Base_Bell.BellBody_prefab).WaitForCompletion();
        private static GameObject masterPrefab = Addressables.LoadAssetAsync<GameObject>(RoR2_Base_Bell.BellMaster_prefab).WaitForCompletion();
        private static CharacterSpawnCard cscBell = Addressables.LoadAssetAsync<CharacterSpawnCard>(RoR2_Base_Bell.cscBell_asset).WaitForCompletion();
        private static GameObject bellProjectile = Addressables.LoadAssetAsync<GameObject>(RoR2_Base_Bell.BellBall_prefab).WaitForCompletion();
        private static GameObject bellProjectileGhost = Addressables.LoadAssetAsync<GameObject>(RoR2_Base_Bell.BellBallGhost_prefab).WaitForCompletion();
        private static GameObject bellDetonateUseEffect = Addressables.LoadAssetAsync<GameObject>(RoR2_Base_DeathProjectile.DeathProjectileTickEffect_prefab).WaitForCompletion();
        private static GameObject fusionCellObject = Addressables.LoadAssetAsync<GameObject>(RoR2_Base_FusionCellDestructible.FusionCellDestructibleBody_prefab).WaitForCompletion();
        private static SkillDef bellSkill = Addressables.LoadAssetAsync<SkillDef>(RoR2_Base_Bell.BellBodyBellBlast_asset).WaitForCompletion();
        private static GameObject chargeEffect;

        private static ConfigEntry<bool> tacoBell;
        private static ConfigEntry<float> detonateCooldown;
        private static ConfigEntry<float> detonateMaxBombsToDetonate;
        private static ConfigEntry<float> detonateRange;
        private static ConfigEntry<float> detonateFlierSpeed;
        private static ConfigEntry<float> detonateBellWarningDuration;
        private static ConfigEntry<float> detonatePrimeWarningDuration;
        private static ConfigEntry<float> detonateExplosionRaidus;
        private static ConfigEntry<float> detonateDamageCoefficient;

        public override void RegisterConfig()
        {
            base.RegisterConfig();
            detonateCooldown = BindFloat("Toll Cooldown", 10f, "The cooldown of the Explosive Toll ability.", 5f, 30f, 0.1f, PluginConfig.FormatType.Time);
            detonateMaxBombsToDetonate = BindFloat("Toll Max Detonations", 5f, "The maximum number of spiked bombs that the ability can detonate per use.", 1f, 20f, 1f, PluginConfig.FormatType.None);
            detonateRange = BindFloat("Toll Max Range", 60f, "The max distance that a Brass Contraption can detonate a bomb at. This is also the distance that the Brass Contraption will attempt to use the ability.", 30f, 100f, 1f, PluginConfig.FormatType.Distance);
            detonateFlierSpeed = BindFloat("Toll Signal Speed", 60f, "The speed of the signal sent from Brass Contraptions to the bomb. The faster the signal travels, the sooner the bombs will activate.", 20f, 120f, 1f, PluginConfig.FormatType.Speed);
            detonateBellWarningDuration = BindFloat("Toll Warning Duration", 1f, "The duration of time between the Brass Contraption tolling and it sending out the bomb signals.", 0.5f, 3f, 0.1f, PluginConfig.FormatType.Time);
            detonatePrimeWarningDuration = BindFloat("Toll Bomb Prime Duration", 1f, "The duration of time between a signal arriving at a bomb, and the bomb exploding.", 0.5f, 3f, 0.1f, PluginConfig.FormatType.Time);
            detonateExplosionRaidus = BindFloat("Toll Explosion Radius", 8f, "The radius of the bomb explosion.", 4f, 12f, 0.1f, PluginConfig.FormatType.Distance);
            detonateDamageCoefficient = BindFloat("Toll Damage Coefficient", 300f, "The damage coefficient of the explosion.", 100f, 500f, 5f, PluginConfig.FormatType.Percentage);
            tacoBell = BindBool("Taco Bell", false, "Don't.");
            BindStats(bodyPrefab, [cscBell], new StatOverrides { baseMaxHealth = 380f });
        }
        public override void Initialise()
        {
            base.Initialise();
            CreateSkill();
            SetUpProjectile();
            Transform[] fusionCellTransforms = fusionCellObject.GetComponentsInChildren<Transform>();
            foreach (Transform t in fusionCellTransforms)
            {
                if (t.gameObject.name == "Mesh")
                {
                    ChildLocator childLocator = t.gameObject.GetComponent<ChildLocator>();
                    Transform transform = childLocator.FindChild("Charge");
                    if (transform != null)
                    {
                        chargeEffect = transform.gameObject.InstantiateClone("bellBombChargeEffect");
                        chargeEffect.transform.localPosition = Vector3.zero;
                        chargeEffect.gameObject.SetActive(true);
                    }
                }
            }
            bellSkill.activationState = ContentAddition.AddEntityState<ChargeTrioBombWithInterruptPriority>(out _);
        }
        public void SetUpProjectile()
        {
            ProjectileImpactExplosion impact = bellProjectile.GetComponent<ProjectileImpactExplosion>();
            DestroyImmediate(impact);
            ProjectileSimple simple = bellProjectile.GetComponent<ProjectileSimple>();
            simple.lifetime = 20f;
            DestroyOnTimer destroyOnTimer = bellProjectile.GetComponent<DestroyOnTimer>();
            destroyOnTimer.duration = 20f;
            ObjectScaleCurve curve = bellProjectileGhost.GetComponentInChildren<ObjectScaleCurve>();
            Destroy(curve);
            ProjectileBellRemoteDetonation remoteDetonation = bellProjectile.AddComponent<ProjectileBellRemoteDetonation>();
        }
        private void CreateSkill()
        {
            SkillDefData skillDefData = new SkillDefData
            {
                objectName = "BellBodyRemoteDetonation",
                skillName = "BellRemoteDetonation",
                esmName = "Weapon",
                activationState = ContentAddition.AddEntityState<RemoteDetonation>(out _),
                cooldown = detonateCooldown.Value,
                combatSkill = true
            };
            RemoteDetonationSkillDef remoteDetonation = CreateSkillDef<RemoteDetonationSkillDef>(skillDefData);
            CreateGenericSkill(bodyPrefab, remoteDetonation.skillName, "BellSpecialFamily", remoteDetonation, SkillSlot.Special);
            AISkillDriverData driverData = new AISkillDriverData
            {
                masterPrefab = masterPrefab,
                customName = "useSpecial",
                skillSlot = SkillSlot.Special,
                requiredSkillDef = remoteDetonation,
                requireReady = true,
                minDistance = 0f,
                maxDistance = detonateRange.Value,
                aimType = RoR2.CharacterAI.AISkillDriver.AimType.AtCurrentEnemy,
                movementType = RoR2.CharacterAI.AISkillDriver.MovementType.ChaseMoveTarget,
                targetType = RoR2.CharacterAI.AISkillDriver.TargetType.CurrentEnemy,
                desiredIndex = 0
            };
            CreateAISkillDriver(driverData);
        }
        public class RemoteDetonationSkillDef : SkillDef
        {
            public override bool IsReady([NotNull] GenericSkill skillSlot)
            {
                CharacterBody body = skillSlot.characterBody;
                if (body == null)
                {
                    return false;
                }
                List<ProjectileBellRemoteDetonation> detonationComponents = InstanceTracker.GetInstancesList<ProjectileBellRemoteDetonation>();
                if (!detonationComponents.Any())
                {
                    return false;
                }
                if (base.IsReady(skillSlot))
                {
                    int i = 0;
                    foreach (ProjectileBellRemoteDetonation detonationComponent in detonationComponents)
                    {
                        bool validForDetonation = BombIsValidForDetonation(body, detonationComponent);
                        if (validForDetonation)
                        {
                            i++;
                            if (i >= 3)
                            {
                                return true;
                            }
                        }
                    }
                }
                return false;
            }
        }
        public class RemoteDetonation : BaseSkillState
        {
            private static float baseDuration = detonateBellWarningDuration.Value;
            public static float detonationRange = detonateRange.Value;
            private float duration;
            private GameObject effectInstance;
            private EffectManagerHelper _emh_effectInstance;
            private List<ProjectileBellRemoteDetonation> reservedBombs = new List<ProjectileBellRemoteDetonation>();
            private bool signalSent;
            private static int maxBombsToDetonate = (int)detonateMaxBombsToDetonate.Value;
            public override void OnEnter()
            {
                base.OnEnter();
                duration = baseDuration / attackSpeedStat;
                Util.PlaySound(tacoBell.Value == false ? "Play_bell_attack2_warning" : "Play_bell_attack2_warning_taco", this.gameObject);
                if (!EffectManager.ShouldUsePooledEffect(bellDetonateUseEffect))
                {
                    effectInstance = Instantiate(bellDetonateUseEffect, characterBody.corePosition, Quaternion.identity);
                }
                else
                {
                    _emh_effectInstance = EffectManager.GetAndActivatePooledEffect(bellDetonateUseEffect, characterBody.corePosition, Quaternion.identity);
                    effectInstance = _emh_effectInstance.gameObject;
                }
                effectInstance.transform.parent = characterBody.coreTransform;

                var detonationComponents = InstanceTracker.GetInstancesList<ProjectileBellRemoteDetonation>();
                if (!detonationComponents.Any())
                {
                    Log.Error($"No detonation components!");
                    outer.SetNextStateToMain();
                    return;
                }
                var validBombs = new List<ProjectileBellRemoteDetonation>();
                foreach (var component in detonationComponents)
                {
                    if (BombIsValidForDetonation(characterBody, component))
                    {
                        validBombs.Add(component);
                    }
                }
                Vector3 targetPosition = characterBody.corePosition;
                if (characterBody.master != null)
                {
                    BaseAI ai = characterBody.master.gameObject.GetComponent<BaseAI>();
                    if (ai != null && ai.currentEnemy.characterBody != null)
                    {
                        targetPosition = ai.currentEnemy.characterBody.corePosition;
                    }
                }
                var bombsByDistance = validBombs.OrderBy(bomb => Vector3.Distance(targetPosition, bomb.transform.position)).Take(maxBombsToDetonate);
                foreach (var bomb in bombsByDistance)
                {
                    reservedBombs.Add(bomb);
                    bomb.ReserveBomb();
                }
            }
            public override void OnExit()
            {
                base.OnExit();
                if (effectInstance != null)
                {
                    if (_emh_effectInstance != null && _emh_effectInstance.OwningPool != null)
                    {
                        _emh_effectInstance.ReturnToPool();
                        _emh_effectInstance = null;
                    }
                    else
                    {
                        Destroy(effectInstance);
                    }
                }
                if (signalSent == false)
                {
                    foreach (var component in reservedBombs)
                    {
                        component?.RemoveReservation();
                    }
                }
            }
            public override void FixedUpdate()
            {
                base.FixedUpdate();
                if (base.fixedAge > duration)
                {
                    foreach (var component in reservedBombs)
                    {
                        if (component == null)
                        {
                            continue;
                        }
                        component.SignalBomb();
                        GameObject trailGameObject = new GameObject();
                        trailGameObject.transform.position = characterBody.corePosition;
                        BellPrimerFlier flier = trailGameObject.AddComponent<BellPrimerFlier>();
                        flier.targetTransform = component.transform;
                        signalSent = true;
                    }
                    Util.PlaySound("Play_mage_m1_cast_lightning", this.gameObject);
                    outer.SetNextStateToMain();
                }
            }
            public override InterruptPriority GetMinimumInterruptPriority()
            {
                return InterruptPriority.Skill;
            }
        }
        public class ChargeTrioBombWithInterruptPriority : ChargeTrioBomb
        {
            public override InterruptPriority GetMinimumInterruptPriority()
            {
                return InterruptPriority.PrioritySkill;
            }
        }
        public class ProjectileBellRemoteDetonation : MonoBehaviour
        {
            public ProjectileStickOnImpact stick;
            public enum BombState
            {
                Free,
                Reserved,
                Signaled,
                Primed
            }
            public BombState state;
            private Vector3 startPosition;
            private Vector3 targetPosition;
            private static float explodeHeight = 3f;
            private static float explosionDelay = detonatePrimeWarningDuration.Value;
            private float explosionStopwatch;
            private static GameObject indicatorPrefab = Addressables.LoadAssetAsync<GameObject>(RoR2_Base_Common.TeamAreaIndicator__FullSphere_prefab).WaitForCompletion();
            private static GameObject explosionPrefab = Addressables.LoadAssetAsync<GameObject>(RoR2_DLC2_Chef.BoostedSearFireballProjectileExplosionVFX_prefab).WaitForCompletion();
            private GameObject indicatorInstance;
            private static float explosionRadius = detonateExplosionRaidus.Value;
            private static float primeDelay = 0.2f;
            private float primeTimer;
            public TeamFilter filter;
            private ProjectileController controller;
            private ProjectileDamage projectileDamage;
            private GameObject chargeInstance;
            private float bellDamage = 0f;
            private static float damageCoefficient = detonateDamageCoefficient.Value / 100f;
            private DestroyOnTimer destroyOnTimer;
            private ProjectileSimple projectileSimple;
            private static float lifetimeExtensionOnReserve = 10f;
            public void Start()
            {
                stick = GetComponent<ProjectileStickOnImpact>();
                filter = GetComponent<TeamFilter>();
                controller = GetComponent<ProjectileController>();
                projectileDamage = GetComponent<ProjectileDamage>();
                destroyOnTimer = GetComponent<DestroyOnTimer>();
                projectileSimple = GetComponent<ProjectileSimple>();
                if (controller != null)
                {
                    CharacterBody ownerBody = controller.owner.GetComponent<CharacterBody>();
                    if (ownerBody != null)
                    {
                        bellDamage = ownerBody.damage;
                    }
                }
                state = BombState.Free;
            }
            public void OnEnable()
            {
                InstanceTracker.Add(this);
            }
            public void OnDisable()
            {
                InstanceTracker.Remove(this);
                if (indicatorInstance != null)
                {
                    Destroy(indicatorInstance);
                }
                if (chargeInstance != null)
                {
                    Destroy(chargeInstance);
                }
            }
            public void SignalBomb()
            {
                state = BombState.Signaled;
            }
            public void RemoveReservation()
            {
                state = BombState.Free;
            }
            public void ReserveBomb()
            {
                state = BombState.Reserved;
                if (destroyOnTimer != null)
                {
                    destroyOnTimer.duration += lifetimeExtensionOnReserve;
                }
                if (projectileSimple != null)
                {
                    projectileSimple.lifetime += lifetimeExtensionOnReserve;
                }
            }
            public void Prime()
            {
                state = BombState.Primed;
                startPosition = transform.position;
                targetPosition = transform.position + new Vector3(0f, explodeHeight, 0f);
                stick.enabled = false;
                indicatorInstance = Instantiate(indicatorPrefab, transform.position, Quaternion.identity);
                if (indicatorInstance != null)
                {
                    TeamAreaIndicator indicator = indicatorInstance.GetComponent<TeamAreaIndicator>();
                    indicator.teamFilter = GetComponent<TeamFilter>();
                    indicator.transform.localScale = Vector3.one * 5f;
                    indicator.transform.SetParent(transform);
                }
                ProjectileController controller = GetComponent<ProjectileController>();

                if (controller != null && controller.ghost != null)
                {
                    ProjectileGhostController ghost = controller.ghost;
                    ObjectScaleCurve objectScale = ghost.gameObject.AddComponent<ObjectScaleCurve>();
                    objectScale.overallCurve = new AnimationCurve();
                    for (int i = 0; i < 11; i++)
                    {
                        float t = 0.1f * i;
                        float s = 1f + 0.5f * (t * t);
                        objectScale.overallCurve.AddKey(t, s);
                    }
                    objectScale.timeMax = primeDelay + explosionDelay;
                    objectScale.useOverallCurveOnly = true;
                    RotateAroundAxis rotate = gameObject.AddComponent<RotateAroundAxis>();
                    rotate.speed = RotateAroundAxis.Speed.Fast;
                    rotate.fastRotationSpeed = 720f;
                    int axis = UnityEngine.Random.RandomRangeInt(0, 3);
                    rotate.rotateAroundAxis = axis == 0 ? RotateAroundAxis.RotationAxis.X : (axis == 1 ? RotateAroundAxis.RotationAxis.Y : RotateAroundAxis.RotationAxis.Z);
                    rotate.relativeTo = Space.Self;
                    chargeInstance = Instantiate(chargeEffect, transform.position, transform.rotation);
                }
            }
            public void FixedUpdate()
            {
                if (state == BombState.Primed)
                {
                    chargeInstance.transform.position = transform.position;
                    if (primeTimer <= primeDelay)
                    {
                        primeTimer += Time.fixedDeltaTime;
                        return;
                    }
                    explosionStopwatch += Time.fixedDeltaTime;
                    float progress = Mathf.Clamp01(explosionStopwatch / explosionDelay);
                    float progressScaled = Mathf.Pow(progress, 0.4f);
                    Vector3 newPosition = Vector3.Lerp(startPosition, targetPosition, progressScaled);
                    transform.position = newPosition;
                    if (explosionStopwatch > explosionDelay)
                    {

                        BlastAttack blastAttack = new BlastAttack
                        {
                            attacker = controller.owner,
                            inflictor = controller.owner,
                            teamIndex = filter.teamIndex,
                            attackerFiltering = AttackerFiltering.Default,
                            position = transform.position,
                            radius = explosionRadius,
                            falloffModel = BlastAttack.FalloffModel.SweetSpot,
                            baseDamage = bellDamage * damageCoefficient,
                            baseForce = 3000f,
                            crit = projectileDamage.crit,
                            damageType = projectileDamage.damageType,
                            damageColorIndex = projectileDamage.damageColorIndex,
                            procChainMask = default(ProcChainMask),
                            procCoefficient = 1f
                        };
                        if (NetworkServer.active)
                        {
                            blastAttack.Fire();
                        }
                        EffectManager.SpawnEffect(explosionPrefab, new EffectData { origin = transform.position, rotation = transform.rotation, scale = explosionRadius }, true);
                        Destroy(this.gameObject);
                    }
                }
            }
        }
        public class BellPrimerFlier : MonoBehaviour
        {

            public Transform targetTransform;
            private TrailRenderer renderer;
            private static Material trailMaterial = Addressables.LoadAssetAsync<Material>(RoR2_Base_LunarGolem.matLunarGolemShieldTrails_mat).WaitForCompletion();
            private static float trailTime = 0.3f;
            private static float trailSpeed = detonateFlierSpeed.Value; 
            private bool madePrimeAttempt = false;
            private float postArrivalTimer;
            private float stopwatch;
            public void Start()
            {
                renderer = gameObject.AddComponent<TrailRenderer>();
                renderer.material = trailMaterial;
                renderer.materials = [trailMaterial];
                renderer.startWidth = 0.4f;
                renderer.endWidth = 0.2f;
                renderer.time = trailTime;
                renderer.emitting = true;
                renderer.enabled = true;
            }
            public void FixedUpdate()
            {
                if (targetTransform == null)
                {
                    Destroy(gameObject);
                    return;
                }
                stopwatch += Time.fixedDeltaTime;
                Vector3 currentPosition = transform.position;
                Vector3 projectilePosition = targetTransform.position;
                Vector3 direction = (projectilePosition - currentPosition).normalized;
                Vector3 newPosition = currentPosition + (direction * trailSpeed * Time.fixedDeltaTime);
                transform.position = newPosition;
                float distance = Vector3.Distance(newPosition, projectilePosition);
                if (distance < 1f && !madePrimeAttempt)
                {
                    madePrimeAttempt = true;
                    ProjectileBellRemoteDetonation detonationComponent = targetTransform.gameObject.GetComponent<ProjectileBellRemoteDetonation>();
                    if (detonationComponent.state == ProjectileBellRemoteDetonation.BombState.Signaled)
                    {
                        detonationComponent.Prime();
                    }
                }
                if (stopwatch > 5f)
                {
                    if (targetTransform != null)
                    {
                        ProjectileBellRemoteDetonation component = targetTransform.gameObject.GetComponent<ProjectileBellRemoteDetonation>();
                        if (component != null && component.state == ProjectileBellRemoteDetonation.BombState.Signaled)
                        {
                            component.RemoveReservation();
                        }
                    }
                    Destroy(gameObject);
                    return;
                }
                if (madePrimeAttempt)
                {
                    postArrivalTimer += Time.fixedDeltaTime;
                    if (postArrivalTimer > trailTime)
                    {
                        Destroy(gameObject);
                        return;
                    }
                }
            }
        }
        public static bool BombIsValidForDetonation(CharacterBody bellBody, ProjectileBellRemoteDetonation detonationComponent)
        {
            if (bellBody == null || bellBody.healthComponent == null || bellBody.healthComponent.alive == false)
            {
                return false;
            }
            if (detonationComponent == null || detonationComponent.state != ProjectileBellRemoteDetonation.BombState.Free)
            {
                return false;
            }
            Vector3 bodyPosition = bellBody.corePosition;
            Vector3 projectilePosition = detonationComponent.transform.position;
            float distance = Vector3.Distance(bodyPosition, projectilePosition);
            if (distance > RemoteDetonation.detonationRange)
            {
                return false;
            }
            TeamFilter filter = detonationComponent.filter;
            TeamComponent teamComponent = bellBody.teamComponent;
            if (filter == null || bellBody.teamComponent == null || filter.teamIndex != bellBody.teamComponent.teamIndex)
            {
                return false;
            }
            if (detonationComponent.stick == null || detonationComponent.stick.stuck == false)
            {
                return false;
            }
            return true;
        }
    }
}
