using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using EntityStates;
using EntityStates.MajorConstruct.Weapon;
using HarmonyLib;
using JetBrains.Annotations;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using R2API;
using Rewired.ComponentControls.Effects;
using RoR2;
using RoR2.CharacterAI;
using RoR2.Orbs;
using RoR2.Projectile;
using RoR2.Skills;
using RoR2BepInExPack.GameAssetPaths.Version_1_35_0;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Rendering;
using static EnemyAbilities.Utils;
using static RoR2.CharacterAI.BaseAI;
using static UnityEngine.UI.GridLayoutGroup;
using static EnemyAbilities.PluginConfig;

namespace EnemyAbilities.Abilities.XiConstruct
{
    [EnemyAbilities.ModuleInfo("Core Launch", "Gives Xi Constructs a new Secondary:\n-Core Launch: The Xi Construct spins up to launch it's core at a player, then retracts it after a short delay. Damaging the core returns the damage to the Xi Construct.", "Xi Construct", true)]
    public class DetachEyeModule : BaseModule
    {
        private static GameObject bodyPrefab = Addressables.LoadAssetAsync<GameObject>(RoR2_DLC1_MajorAndMinorConstruct.MegaConstructBody_prefab).WaitForCompletion();
        private static GameObject masterPrefab = Addressables.LoadAssetAsync<GameObject>(RoR2_DLC1_MajorAndMinorConstruct.MegaConstructMaster_prefab).WaitForCompletion();
        private static GameObject projectileGhost = Addressables.LoadAssetAsync<GameObject>(RoR2_DLC1_ClayGrenadier.ClayGrenadierBarrelGhost_prefab).WaitForCompletion().InstantiateClone("MegaConstructEyeProjectileGhost");
        private static GameObject modelPrefab = Addressables.LoadAssetAsync<GameObject>(RoR2_DLC1_MajorAndMinorConstruct.mdlMegaConstruct_fbx).WaitForCompletion();
        //major instead of mega since mega has some weird rendering issue I don't know how to fix
        //major doesn't look great, but it's better than the orb being half-invisible
        private static Material eyeMaterial = Addressables.LoadAssetAsync<Material>(RoR2_DLC1_MajorAndMinorConstruct.matMajorConstructEye_mat).WaitForCompletion();
        private static GameObject impactEffect = Addressables.LoadAssetAsync<GameObject>(RoR2_Base_Parent.ParentSlamEffect_prefab).WaitForCompletion();
        public static GameObject vagrantProjectile = Addressables.LoadAssetAsync<GameObject>(RoR2_Base_Vagrant.VagrantTrackingBomb_prefab).WaitForCompletion().InstantiateClone("ClonedVagrantProjectile");
        private static Material trailMaterial = Addressables.LoadAssetAsync<Material>(RoR2_DLC1_MajorAndMinorConstruct.matConstructBeamInitial_mat).WaitForCompletion();
        private static GameObject transferDamageImpactEffect = Addressables.LoadAssetAsync<GameObject>(RoR2_Base_Common_VFX.OmniImpactVFXLarge_prefab).WaitForCompletion().InstantiateClone("scalableImpactEffect");

        public override void Awake()
        {
            base.Awake();
            ModifyProjectilePrefab();
            CreateSkill();
            bodyPrefab.AddComponent<MegaConstructUtilityController>();
            IL.RoR2.CharacterBody.RecalculateStats += (il) =>
            {
                ILCursor c1 = new ILCursor(il);
                MethodInfo methodInfo = AccessTools.PropertySetter(typeof(CharacterBody), nameof(CharacterBody.maxHealth));
                if (c1.TryGotoNext(
                    x => x.MatchCallOrCallvirt(methodInfo)
                ))
                {
                    c1.Emit(OpCodes.Ldarg_0);
                    c1.EmitDelegate<Func<float, CharacterBody, float>>((originalMaxHealth, characterBody) =>
                    {
                        //filters out most normal enemies, stops us from doing GetComponent EVERY time recalc stats is called
                        if (characterBody.master == null)
                        {
                            ProjectileMegaConstructEye component = characterBody.gameObject.GetComponent<ProjectileMegaConstructEye>();
                            if (component != null && component.ownerBody != null)
                            {
                                characterBody.isElite = component.ownerBody.isElite;
                                return component.ownerBody.maxHealth;
                            }
                        }
                        return originalMaxHealth;
                    });
                }
                else
                {

                }
            };
            On.RoR2.HealthComponent.TakeDamageProcess += CreateDamageEffectOrb;
            transferDamageImpactEffect.GetComponent<EffectComponent>().applyScale = true;
            ContentAddition.AddEffect(transferDamageImpactEffect);
        }

        private void CreateDamageEffectOrb(On.RoR2.HealthComponent.orig_TakeDamageProcess orig, HealthComponent self, DamageInfo damageInfo)
        {
            if (self != null && damageInfo != null && damageInfo.damage > 0)
            {
                if (self.body != null && self.body.bodyIndex == DLC1Content.BodyPrefabs.MegaConstructBody.bodyIndex)
                {
                    if (damageInfo.inflictedHurtbox != null && damageInfo.inflictedHurtbox.enabled == true)
                    {
                        MegaConstructUtilityController controller = self.body.gameObject.GetComponent<MegaConstructUtilityController>();
                        if (controller != null)
                        {
                            if (controller.eyeProjectile != null)
                            {
                                DetachEyeDamageEffectOrb orb = new DetachEyeDamageEffectOrb();
                                orb.origin = controller.eyeProjectile.transform.position;
                                orb.target = self.body.mainHurtBox;
                                OrbManager.instance.AddOrb(orb);
                            }
                        }
                    }
                }
            }
            orig(self, damageInfo);
        }

        public void ModifyProjectilePrefab()
        {
            ProjectileDirectionalTargetFinder finder = vagrantProjectile.GetComponent<ProjectileDirectionalTargetFinder>();
            Destroy(finder);
            ProjectileSteerTowardTarget steer = vagrantProjectile.GetComponent<ProjectileSteerTowardTarget>();
            Destroy(steer);
            ProjectileTargetComponent target = vagrantProjectile.GetComponent<ProjectileTargetComponent>();
            Destroy(target);
            Transform[] transformList = vagrantProjectile.GetComponentsInChildren<Transform>();
            List<string> blacklistedStrings = ["ProximityDetonator", "AuthorityEffect", "PredictionEffect"];
            if (transformList != null && transformList.Length > 0)
            {
                for (int i = transformList.Length - 1; i >= 0; i--)
                {
                    if (transformList[i] != null && blacklistedStrings.Contains(transformList[i].gameObject.name))
                    {
                        Destroy(transformList[i].gameObject);
                    }
                }
            }
            Transform[] transforms = modelPrefab.GetComponentsInChildren<Transform>();
            foreach (Transform t in transforms)
            {
                if (t.name == "MegaConstructEyeMesh")
                {
                    //this section is super messy and filled with various attempts to get the mega construct eye to render properly
                    Transform vagrantProjectileTransform = vagrantProjectile.GetComponent<Transform>();
                    vagrantProjectileTransform.localScale = Vector3.one;
                    Transform eyeTransform = t;
                    GameObject gameObject = eyeTransform.gameObject;
                    MeshFilter eyeMeshFilter = gameObject.GetComponent<MeshFilter>();
                    MeshRenderer eyeMeshRenderer = gameObject.GetComponent<MeshRenderer>();
                    eyeMeshRenderer.localBounds = new Bounds
                    {
                        center = new Vector3(0f, 0f, 0f),
                        extents = new Vector3(5f, 5f, 10f)
                    };
                    Material eyeMaterialClone = new Material(eyeMaterial);
                    ProjectileController controller = vagrantProjectile.GetComponent<ProjectileController>();
                    MeshFilter ghostMeshFilter = projectileGhost.GetComponentInChildren<MeshFilter>();
                    ParticleSystem[] systems = projectileGhost.GetComponentsInChildren<ParticleSystem>();
                    MeshRenderer ghostMeshRenderer = projectileGhost.GetComponentInChildren<MeshRenderer>();
                    RotateAroundAxis rotateAroundAxis = projectileGhost.GetComponentInChildren<RotateAroundAxis>();
                    Rigidbody rigidbody = vagrantProjectile.GetComponent<Rigidbody>();
                    SphereCollider sphere = rigidbody.GetComponent<SphereCollider>();
                    sphere.radius = 0.2f;
                    rigidbody.mass = 1f;
                    rotateAroundAxis.fastRotationSpeed = 0f;
                    rotateAroundAxis.slowRotationSpeed = 0f;
                    rotateAroundAxis.rotateAroundAxis = RotateAroundAxis.RotationAxis.Y;
                    ghostMeshRenderer.material = eyeMaterialClone;
                    ghostMeshRenderer.forceRenderingOff = false;
                    controller.ghostPrefab = projectileGhost;
                    controller.flightSoundLoop = null;
                    controller.cannotBeDeleted = true;
                    Transform ghostMeshFilterTransform = ghostMeshFilter.GetComponent<Transform>();
                    ghostMeshFilterTransform.localScale = Vector3.one;
                    ghostMeshFilterTransform.localPosition = Vector3.zero;
                    ghostMeshFilterTransform.localRotation = Quaternion.identity;

                    for (int i = systems.Length - 1; i >= 0; i--)
                    {
                        Destroy(systems[i]);
                    }
                    Mesh mesh = eyeMeshFilter.sharedMesh;
                    ghostMeshFilter.sharedMesh = mesh;
                    ghostMeshFilter.mesh = mesh;

                    Transform transform = projectileGhost.GetComponent<Transform>();
                    transform.localScale = new Vector3(5f, 5f, 2.5f);

                    ProjectileSimple simple = vagrantProjectile.GetComponent<ProjectileSimple>();
                    simple.lifetime = 20f;

                    ProjectileImpactExplosion impact = vagrantProjectile.GetComponent<ProjectileImpactExplosion>();
                    UnityEngine.Object.Destroy(impact);
                    ProjectileDetonateOnImpact detonate = vagrantProjectile.AddComponent<ProjectileDetonateOnImpact>();
                    detonate.gameObject.layer = LayerIndex.projectileWorldOnly.intVal;
                    if (detonate != null)
                    {
                        detonate.impactEffect = impactEffect;
                        detonate.blastRadius = xiCoreExplosionRadius.Value;
                        detonate.blastDamageCoefficient = 1f;
                        detonate.falloffModel = BlastAttack.FalloffModel.SweetSpot;
                        detonate.bonusBlastForce = new Vector3(0f, 2000f, 0f);
                        detonate.destroyOnEnemy = false;
                        detonate.destroyOnWorld = false;
                        detonate.impactOnWorld = true;
                        detonate.lifetimeAfterImpact = 60f;
                        detonate.timerAfterImpact = true;
                        detonate.detonateOnEnemy = false;
                        detonate.detonateOnWorld = true;
                        detonate.lifetime = 60f;
                        detonate.explosionEffect = impactEffect;
                        detonate.blastProcCoefficient = 1f;
                        detonate.transformSpace = ProjectileDetonateOnImpact.TransformSpace.World;
                        detonate.explodeOnLifeTimeExpiration = false;
                    }
                    ProjectileStickOnImpact stick = vagrantProjectile.AddComponent<ProjectileStickOnImpact>();
                    stick.ignoreCharacters = true;
                    stick.ignoreWorld = false;
                    stick.ignoreSteepSlopes = false;
                    SkillLocator locator = vagrantProjectile.GetComponent<SkillLocator>();

                    CharacterBody xiConstructBody = bodyPrefab.GetComponent<CharacterBody>();

                    CharacterBody body = vagrantProjectile.GetComponent<CharacterBody>();
                    body.baseMaxHealth = xiConstructBody.baseMaxHealth;
                    body.levelMaxHealth = xiConstructBody.levelMaxHealth;
                    body.baseArmor = xiConstructBody.baseArmor;
                    body.isChampion = xiConstructBody.isChampion;

                    ProjectileMegaConstructEye projectileEye = vagrantProjectile.AddComponent<ProjectileMegaConstructEye>();

                    HurtBox hurtBox = vagrantProjectile.GetComponentInChildren<HurtBox>();
                    hurtBox.healthComponent = null;

                    TrailRenderer trailRenderer = projectileGhost.AddComponent<TrailRenderer>();
                    trailRenderer.bounds = default(Bounds);
                    trailRenderer.localBounds = default(Bounds);
                    trailRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                    trailRenderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                    trailRenderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
                    trailRenderer.time = 0.2f;
                    trailRenderer.startWidth = 1f;
                    trailRenderer.endWidth = 1f;
                    trailRenderer.material = trailMaterial;
                    trailRenderer.sharedMaterial = trailMaterial;

                    Light light = projectileGhost.AddComponent<Light>();
                    light.type = LightType.Point;
                    light.spotAngle = 30f;
                    light.innerSpotAngle = 21.80208f;
                    light.color = new Color(r: 1.000f, g: 0.509f, b: 0.000f, a: 1.000f);
                    light.colorTemperature = 6570f;
                    light.intensity = 242.27f;
                    light.bounceIntensity = 1f;
                    light.shadowBias = 0.05f;
                    light.shadowNormalBias = 0.4f;
                    light.shadowNearPlane = 0.2f;
                    light.range = 7.62f;
                    light.shadowStrength = 1f;
                    light.shadowResolution = UnityEngine.Rendering.LightShadowResolution.FromQualitySettings;

                    ContentAddition.AddBody(vagrantProjectile);
                }
            }
        }
        public void CreateSkill()
        {
            FireEyeSkillDef detachEyeDef = ScriptableObject.CreateInstance<FireEyeSkillDef>();
            (detachEyeDef as ScriptableObject).name = "MegaConstructBodyDetachEye";
            detachEyeDef.skillName = "MegaConstructDetachEye";
            detachEyeDef.activationStateMachineName = "Body";
            detachEyeDef.activationState = ContentAddition.AddEntityState<DetachEye>(out _);
            detachEyeDef.baseRechargeInterval = xiCoreCooldown.Value;   
            detachEyeDef.cancelSprintingOnActivation = true;
            detachEyeDef.isCombatSkill = true;
            ContentAddition.AddSkillDef(detachEyeDef);

            SkillFamily skillFamily = ScriptableObject.CreateInstance<SkillFamily>();
            (skillFamily as ScriptableObject).name = "MegaConstructSecondaryFamily";
            skillFamily.variants = [new SkillFamily.Variant() { skillDef =  detachEyeDef }];

            GenericSkill skill = bodyPrefab.AddComponent<GenericSkill>();
            skill.skillName = "MegaConstructDetachEye";
            skill._skillFamily = skillFamily;

            SkillLocator locator = bodyPrefab.GetComponent<SkillLocator>();
            locator.secondary = skill;

            ContentAddition.AddSkillFamily(skillFamily);



            AISkillDriver useSecondary = masterPrefab.AddComponent<AISkillDriver>();
            useSecondary.customName = "useSecondary";
            useSecondary.skillSlot = SkillSlot.Secondary;
            useSecondary.requireSkillReady = true;
            useSecondary.minDistance = 0f;
            useSecondary.maxDistance = 150f;
            useSecondary.moveTargetType = AISkillDriver.TargetType.Custom;
            useSecondary.movementType = AISkillDriver.MovementType.Stop;
            useSecondary.aimType = AISkillDriver.AimType.AtMoveTarget;
            useSecondary.maxUserHealthFraction = 1f;
            masterPrefab.ReorderSkillDrivers(useSecondary, 7);

            //band aid fix
            foreach (AISkillDriver aiSkillDriver in masterPrefab.GetComponents<AISkillDriver>())
            {
                aiSkillDriver.aimType = AISkillDriver.AimType.AtMoveTarget;
                aiSkillDriver.moveTargetType = AISkillDriver.TargetType.Custom;
                if (aiSkillDriver.customName == "StopStep")
                {
                    aiSkillDriver.nextHighPriorityOverride = useSecondary;
                }
                if (aiSkillDriver.customName == "ShootStep")
                {
                    aiSkillDriver.selectionRequiresTargetLoS = false;
                    aiSkillDriver.activationRequiresTargetLoS = true;
                }
            }
        }
        public class DetachEyeDamageEffectOrb : GenericDamageOrb
        {
            public override void Begin()
            {
                speed = 240f;
                damageValue = 0f;
                procCoefficient = 0f;
                base.Begin();
                
            }
            public override GameObject GetOrbEffect()
            {
                return OrbStorageUtility.Get("Prefabs/Effects/OrbEffects/ClayGooOrbEffect");
            }
            public override void OnArrival()
            {
                EffectManager.SpawnEffect(transferDamageImpactEffect, new EffectData { scale = 4f, origin = target.transform.position }, transmit: true);
            }
        }
    }
    public class FireEyeSkillDef : SkillDef
    {
        private class InstanceData : BaseSkillInstanceData
        {
            public CharacterBody body;
            public EntityStateMachine weaponESM;
            public MegaConstructUtilityController utilController;
        }
        public override BaseSkillInstanceData OnAssigned(GenericSkill skillSlot)
        {
            CharacterBody body = skillSlot.characterBody;
            EntityStateMachine selectedESM = body.GetComponents<EntityStateMachine>().FirstOrDefault(esm => esm.customName == "Weapon");
            MegaConstructUtilityController utilController = body.gameObject.GetComponent<MegaConstructUtilityController>();
            return new InstanceData
            {
                body = body,
                weaponESM = selectedESM,
                utilController = utilController
            };
        }
        public override bool IsReady([NotNull] GenericSkill skillSlot)
        {
            var data = skillSlot.skillInstanceData as InstanceData;
            if (data?.weaponESM == null || data.body == null)
            {
                return false;
            }
            if (data.weaponESM.state is FireLaser || data.weaponESM.state is ChargeLaser || data.weaponESM.state is TerminateLaser)
            {
                return false;
            }
            if (!data.utilController.validToUseSkill)
            {
                return false;
            }
            bool readyToUse = base.IsReady(skillSlot);
            return readyToUse;
        }
    }
    public class DetachEye : BaseSkillState
    {
        private float duration;
        private static float baseDuration = 1f;
        private static float speedOverride = 100f;
        private static float force = 2000f;
        private GameObject projectilePrefab = DetachEyeModule.vagrantProjectile;
        private enum AbilityState
        {
            None,
            Windup,
            Wait,
            Recall
        }

        private static float baseWindupDuration = xiCoreWindupDuration.Value;
        private float windupDuration;
        private float windupTimer;
        private static float baseWaitDuration = xiCoreWaitDuration.Value;
        private float waitDuration;
        private float waitTimer;
        private AbilityState abilityState;
        private Target target;
        private Vector3 rotationAxis;
        private QuaternionPID angularPID;
        private VectorPID torquePID;
        private float spinSpeedPerFixedUpdate = 0.5f;

        private Transform modelEyeTransform;

        private MegaConstructUtilityController utilController;

        public override void OnEnter()
        {
            base.OnEnter();
            if (UnityEngine.Random.RandomRangeInt(0, 2) == 1)
            {
                spinSpeedPerFixedUpdate = -spinSpeedPerFixedUpdate;
            }
            duration = baseDuration / attackSpeedStat;
            if (modelLocator != null && modelLocator.modelTransform != null)
            {
                rotationAxis = inputBank.aimDirection;
            }
            abilityState = AbilityState.Windup;
            windupDuration = baseWindupDuration / attackSpeedStat;
            PlayAnimation("Gesture, Additive", "ChargeLaser", "Laser.playbackRate", windupDuration);
            waitDuration = baseWaitDuration;
            utilController = characterBody.gameObject.GetComponent<MegaConstructUtilityController>();
            ChildLocator childLocator = modelLocator.modelTransform.GetComponent<ChildLocator>();
            if (childLocator != null)
            {
                foreach (var pair in childLocator.transformPairs)
                {
                    if (pair.name == "Eye")
                    {
                        modelEyeTransform = pair.transform;
                    }
                }
            }
            target = characterBody.master.GetComponent<BaseAI>().customTarget;
            torquePID = characterBody.GetComponents<VectorPID>().Where(pid => pid.customName == "torquePID").FirstOrDefault();
            angularPID = characterBody.GetComponent<QuaternionPID>();
            torquePID.ResetPID();
            angularPID.ResetPID();
            torquePID.enabled = false;
            angularPID.enabled = false;
        }
        public override void OnExit()
        {
            base.OnExit();
            UnityEngine.Object.Destroy(utilController.eyeProjectile);
            utilController.recalled = false;
            utilController.eyeProjectile = null;
            if (healthComponent != null && healthComponent.health > 0)
            {
                ToggleEyeVisual(true);
            }
            ToggleHurtBoxes(true);
            torquePID.enabled = true;
            angularPID.enabled = true;
            PlayAnimation("Gesture, Additive", "TerminateLaser", "Laser.playbackRate", 1f);
        }
        public override void FixedUpdate()
        {
            base.FixedUpdate();
            if (abilityState == AbilityState.Windup || abilityState == AbilityState.Wait)
            {
                if (rigidbody != null)
                {
                    rigidbody.maxAngularVelocity = 15f;
                    rigidbody.angularVelocity += spinSpeedPerFixedUpdate * rotationAxis;
                }
            }
            if (abilityState == AbilityState.Recall)
            {
                if (rigidbody != null)
                {
                    rigidbody.maxAngularVelocity = Mathf.Max(rigidbody.maxAngularVelocity-(Mathf.Abs(spinSpeedPerFixedUpdate)), 5f);
                    rigidbody.angularVelocity -= spinSpeedPerFixedUpdate * rotationAxis;
                }
            }
            if (abilityState == AbilityState.Windup)
            {
                windupTimer += Time.fixedDeltaTime;
                if (windupTimer > windupDuration)
                {
                    Fire();
                    abilityState = AbilityState.Wait;
                    PlayAnimation("Gesture, Additive", "FireLaser", "Laser.playbackRate", 5f);
                }
            }
            if (abilityState == AbilityState.Wait)
            {
                if (utilController != null && utilController.eyeProjectile != null)
                {
                    waitTimer += Time.fixedDeltaTime;
                }
                if (waitTimer > waitDuration)
                {
                    StartRecall();
                }
                if (utilController.eyeProjectile == null || utilController.eyeProjectile.transform == null || characterBody == null)
                {
                    return;
                }
                if (Vector3.Distance(utilController.eyeProjectile.transform.position, characterBody.transform.position) > 150f)
                {
                    StartRecall();
                }
                //need a case for if it gets out of bounds?
            }
            if (abilityState == AbilityState.Recall)
            {
                if (utilController.recalled)
                {
                    outer.SetNextStateToMain();
                }
            }
        }
        public void StartRecall()
        {
            if (utilController.eyeProjectile != null)
            {
                ProjectileDetonateOnImpact detonate = utilController.eyeProjectile.GetComponent<ProjectileDetonateOnImpact>();
                if (detonate != null)
                {
                    detonate.nullifyExplosions = true;
                }
                ProjectileStickOnImpact stick = utilController.eyeProjectile.GetComponent<ProjectileStickOnImpact>();
                if (stick != null)
                {
                    stick.enabled = false;
                }
                ProjectileMegaConstructEye eye = utilController.eyeProjectile.GetComponent<ProjectileMegaConstructEye>();
                if (eye != null)
                {
                    eye.recalling = true;
                }
                abilityState = AbilityState.Recall;
            }
        }
        public void Fire()
        {
            if (base.isAuthority)
            {
                Ray aimRay = GetAimRay();
                Quaternion modelRotation = modelLocator.modelTransform.rotation;
                DamageTypeCombo combo = new DamageTypeCombo { damageSource = DamageSource.Secondary, damageType = DamageType.Generic };
                ProjectileManager.instance.FireProjectile(projectilePrefab, aimRay.origin, modelRotation, characterBody.gameObject, (xiCoreDamageCoefficient.Value / 100f) * damageStat, force, RollCrit(), DamageColorIndex.Default, null, speedOverride, combo);
            }
            ToggleEyeVisual(false);
            ToggleHurtBoxes(false);
        }
        public void ToggleEyeVisual(bool state)
        {
            modelEyeTransform.gameObject.SetActive(state);
        }
        public void ToggleHurtBoxes(bool state)
        {
            foreach (HurtBox hurtBox in characterBody.hurtBoxGroup.hurtBoxes)
            {
                hurtBox.enabled = state;
            }
        }
        public override InterruptPriority GetMinimumInterruptPriority()
        {
            return InterruptPriority.Death;
        }
    }
    public class ProjectileMegaConstructEye : MonoBehaviour
    {
        private ProjectileController controller;
        private GameObject owner;
        private HealthComponent healthComponent;
        public HealthComponent ownerHealthComponent;
        public bool recalling;
        public CharacterBody ownerBody;
        private MegaConstructUtilityController utilController;
        private Rigidbody projectileRigidbody;
        private float retractTimer;
        private static GameObject explosionEffect = Addressables.LoadAssetAsync<GameObject>(RoR2_DLC1_MajorAndMinorConstruct.MegaConstructDeathExplosion_prefab).WaitForCompletion();
        public void Awake()
        {
            controller = GetComponent<ProjectileController>();
            if (controller == null)
            {
                return;
            }
            healthComponent = GetComponent<HealthComponent>();
            if (healthComponent == null)
            {
                return;
            }
        }
        public void Start()
        {
            owner = controller.owner;
            if (owner == null)
            {
                return;
            }
            ownerBody = owner.GetComponent<CharacterBody>();
            if (ownerBody == null)
            {
                return;
            }
            ownerHealthComponent = ownerBody.healthComponent;
            if (ownerHealthComponent == null)
            {
                return;
            }
            CharacterBody projectileBody = healthComponent.body;
            if (projectileBody == null)
            {
                return;
            }
            projectileBody.RecalculateStats();
            projectileBody.hurtBoxGroup.hurtBoxes[0].healthComponent = ownerHealthComponent;
            healthComponent.health = ownerHealthComponent.health;
            utilController = ownerBody.gameObject.GetComponent<MegaConstructUtilityController>();
            if (utilController == null)
            {
                return;
            }
            utilController.OnProjectileSetup(this.gameObject);
        }
        public void FixedUpdate()
        {
            //ownerHealthComponent.health = healthComponent.health;
            if (healthComponent.health <= 0)
            {
                //big explosion!!!
                EffectManager.SpawnEffect(explosionEffect, new EffectData { scale = 10f, origin = this.transform.position }, true);
                UnityEngine.Object.Destroy(this.gameObject);
            }
            if (recalling)
            {
                retractTimer += Time.fixedDeltaTime;
                if (projectileRigidbody == null)
                {
                    projectileRigidbody = utilController.eyeProjectile.GetComponent<Rigidbody>();
                    projectileRigidbody.useGravity = false;
                    ProjectileSimple controller = utilController.eyeProjectile.GetComponent<ProjectileSimple>();
                    controller.desiredForwardSpeed = 0f;
                }
                Vector3 projectilePos = projectileRigidbody.position;
                Vector3 ownerPos = ownerBody.transform.position;
                float distance = Vector3.Distance(projectilePos, ownerPos);
                if (distance >= 1f)
                {
                    projectileRigidbody.MovePosition(Vector3.MoveTowards(projectilePos, ownerPos, Mathf.Min(retractTimer * 0.5f, 2f)));
                }
                else
                {
                    utilController.recalled = true;
                }
            }
        }
    }
    public class MegaConstructUtilityController : MonoBehaviour
    {
        private CharacterBody body;
        public bool recalled;
        public GameObject eyeProjectile;
        private BaseAI baseAI;
        public bool validToUseSkill;
        private float targetCheckTimer;
        private static float targetCheckInterval = 0.25f;
        private AISkillDriver last;

        public void Awake()
        {
            body = GetComponent<CharacterBody>();
        }
        public void Start()
        {
            CharacterMaster master = body.master;   
            if (master != null)
            {
                baseAI = master.gameObject.GetComponent<BaseAI>();
            }
        }
        public void OnProjectileSetup(GameObject projectile)
        {
            eyeProjectile = projectile;
        }
        public void FixedUpdate()
        {
            if (baseAI != null && baseAI.skillDriverEvaluation.dominantSkillDriver != null)
            {
                AISkillDriver current = baseAI.skillDriverEvaluation.dominantSkillDriver;
                if (current != last)
                {
                    //Log.Debug($"DRIVER CHANGE: {(last != null ? last.customName : "N/A")} => {current.customName}");
                    last = current;
                }
            }


            targetCheckTimer -= Time.fixedDeltaTime;
            if (targetCheckTimer < 0)
            {
                targetCheckTimer += targetCheckInterval;
                if (body == null || body.inputBank == null)
                {
                    return;
                }
                Ray aimRay = body.inputBank.GetAimRay();
                targetCheckTimer += targetCheckInterval;
                BullseyeSearch search = new BullseyeSearch();
                search.viewer = body;
                search.filterByDistinctEntity = true;
                search.filterByLoS = true;
                search.maxDistanceFilter = Mathf.Infinity;
                search.minDistanceFilter = 0f;
                search.maxAngleFilter = 360f;
                search.searchOrigin = aimRay.origin;
                search.searchDirection = aimRay.direction;
                search.maxAngleFilter = 360f;
                search.sortMode = BullseyeSearch.SortMode.Distance;
                search.queryTriggerInteraction = QueryTriggerInteraction.UseGlobal;
                TeamMask teamMask = TeamMask.GetEnemyTeams(body.teamComponent.teamIndex);
                search.teamMaskFilter = teamMask;
                search.RefreshCandidates();
                IEnumerable<HurtBox> source = search.GetResults().Where(hurtBox => hurtBox.healthComponent != null && hurtBox.healthComponent.body != null && hurtBox.healthComponent.body.isPlayerControlled);
                if (source.Any())
                {
                    baseAI.customTarget.gameObject = source.FirstOrDefault()?.healthComponent.body.gameObject;
                    validToUseSkill = true;
                }
                else
                {
                    validToUseSkill = false;
                }
            }
        }
    }
}
