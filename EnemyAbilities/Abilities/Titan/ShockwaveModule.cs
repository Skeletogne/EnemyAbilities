using System.Collections.Generic;
using EntityStates;
using R2API;
using RoR2.Skills;
using RoR2BepInExPack.GameAssetPaths.Version_1_39_0;
using UnityEngine;
using UnityEngine.AddressableAssets;
using RoR2;
using UnityEngine.Networking;
using System.Linq;
using MiscFixes.Modules;
using EntityStates.TitanMonster;
using BepInEx.Configuration;

namespace EnemyAbilities.Abilities.Titan
{
    [EnemyAbilities.ModuleInfo("Seismic Shockwave", "Gives Stone Titans a new Primary ability:\n-Seismic Shockwave: Allows Stone Titans to jump and unleash a slow-moving radial shockwave that deals light damage and knocks up both allies and enemies. Also grants some buffs to Stone Titan's Geode ability.", "Stone Titan", true)]
    public class ShockwaveModule : BaseModule
    {
        private static GameObject bodyPrefab = Addressables.LoadAssetAsync<GameObject>(RoR2_Base_Titan.TitanBody_prefab).WaitForCompletion();
        private static GameObject masterPrefab = Addressables.LoadAssetAsync<GameObject>(RoR2_Base_Titan.TitanMaster_prefab).WaitForCompletion();
        private static GameObject landingEffect = Addressables.LoadAssetAsync<GameObject>(RoR2_Base_BeetleGuard.BeetleGuardGroundSlam_prefab).WaitForCompletion().InstantiateClone("stoneTitanLandingEffect");
        private static GameObject rumbleEffect = Addressables.LoadAssetAsync<GameObject>(RoR2_Base_SurvivorPod.PodGroundImpact_prefab).WaitForCompletion().InstantiateClone("stoneTitanRumbleEffect");
        private static GameObject pulseEffectPrefab = Addressables.LoadAssetAsync<GameObject>(RoR2_Base_Titan.ExplosionGolemClap_prefab).WaitForCompletion().InstantiateClone("pulseEffect");
        private static EntityStateConfiguration rechargeRocksESC = Addressables.LoadAssetAsync<EntityStateConfiguration>(RoR2_Base_Titan.EntityStates_TitanMonster_RechargeRocks_asset).WaitForCompletion();
        private static GameObject titanRockController = Addressables.LoadAssetAsync<GameObject>(RoR2_Base_Titan.TitanRockController_prefab).WaitForCompletion();

        private static ConfigEntry<float> stompCooldown;
        private static ConfigEntry<float> stompJumpVelocity;
        private static ConfigEntry<float> stompRingLifetime;
        private static ConfigEntry<float> stompRingWidth;
        private static ConfigEntry<float> stompRingHeightCutoff;
        private static ConfigEntry<float> stompRingSpeed;
        private static ConfigEntry<float> stompKnockupForce;
        private static ConfigEntry<float> stompDamageCoeff;
        private static ConfigEntry<float> stompEffectsMult;

        public override void RegisterConfig()
        {
            base.RegisterConfig();
            stompCooldown = BindFloat("Shockwave Cooldown", 15f, "The cooldown of the Seismic Shockwave", 8f, 30f, 0.1f, PluginConfig.FormatType.Time);
            stompJumpVelocity = BindFloat("Shockwave Jump Velocity", 20f, "The jump velocity of the Stone Titan when winding up for Seismic Shockwave. This is proportional to the wind-up duration. Bigger jump => more heads up.", 15f, 30f, 0.1f, PluginConfig.FormatType.Speed);
            stompRingLifetime = BindFloat("Shockwave Lifetime", 6f, "The lifetime of the shockwave before disappearing. Total shockwave radius is equal to Lifetime * Speed", 3f, 10f, 0.1f, PluginConfig.FormatType.Time);
            stompRingWidth = BindFloat("Shockwave Ring Width", 1.5f, "The width of the outwardly-expanding damage ring that causes damage and knock-up.", 0.5f, 4f, 0.1f, PluginConfig.FormatType.Distance);
            stompRingHeightCutoff = BindFloat("Shockwave Height Cutoff", 10f, "The difference in height from the epicenter that the shockwave's damage no longer applies. This can be seen by where particle effects no longer appear.", 5f, 15f, 0.1f, PluginConfig.FormatType.Distance);
            stompRingSpeed = BindFloat("Shockwave Ring Speed", 6f, "The speed at which the shockwave ring expands outwards.", 4f, 16f, 0.1f, PluginConfig.FormatType.Speed);
            stompKnockupForce = BindFloat("Shockwave Knockup Force", 4000f, "The force with which the shockwave knocks up both players and monsters.", 1000f, 6000f, 100f, PluginConfig.FormatType.None);
            stompDamageCoeff = BindFloat("Shockwave Damage Coefficient", 50f, "The damage coefficient of the shockwave. Stone Titans have very high base damage (40), so this is generally lower than most other skills.", 30f, 200f, 5f, PluginConfig.FormatType.Percentage);
            stompEffectsMult = BindFloat("Stomp Effects Mutliplier", 1f, "Affects the number of particles that appear when the ring expands outwards. Higher values may cause lag when multiple stone titans are present.", 0f, 1.5f, 0.1f, PluginConfig.FormatType.Multiplier);
        }
        public override void Initialise()
        {
            base.Initialise();
            CreateSkill();
            SetupParticleEffects();
            rechargeRocksESC.TryModifyFieldValue(nameof(RechargeRocks.baseDuration), 5f);
            TitanRockController rockController = titanRockController.GetComponent<TitanRockController>();
            rockController.startDelay = 3f;
            rockController.fireInterval = 0.75f;
        }
        public void SetupParticleEffects()
        {
            landingEffect.GetComponent<EffectComponent>().applyScale = true;
            landingEffect.GetComponent<EffectComponent>().disregardZScale = false;
            rumbleEffect.GetComponent<EffectComponent>().applyScale = true;
            rumbleEffect.GetComponent<EffectComponent>().disregardZScale = false;
            ParticleSystem[] landingParticleSystems = landingEffect.GetComponentsInChildren<ParticleSystem>();
            foreach (ParticleSystem particleSystem in landingParticleSystems)
            {
                ParticleSystem.ShapeModule shape = particleSystem.shape;
                shape.scale = Vector3.one * 3f;
                ParticleSystem.SizeOverLifetimeModule size = particleSystem.sizeOverLifetime;
                size.sizeMultiplier = 3f;
            }
            ParticleSystem[] rumbleParticleSystems = rumbleEffect.GetComponentsInChildren<ParticleSystem>();
            foreach (ParticleSystem particleSystem in rumbleParticleSystems)
            {
                ParticleSystem.ShapeModule shape = particleSystem.shape;
                shape.scale = Vector3.one * 1f;
                ParticleSystem.SizeOverLifetimeModule size = particleSystem.sizeOverLifetime;
                size.sizeMultiplier = 0.4f;
            }
            ContentAddition.AddEffect(landingEffect);
            ContentAddition.AddEffect(rumbleEffect);
            ShakeEmitter shakeEmitter = rumbleEffect.GetComponent<ShakeEmitter>();
            Light light = rumbleEffect.GetComponentInChildren<Light>();
            LightIntensityCurve curve = rumbleEffect.GetComponentInChildren<LightIntensityCurve>();
            DestroyImmediate(rumbleParticleSystems.Where(system => system.gameObject.name == "Flash").FirstOrDefault());
            DestroyImmediate(light);
            DestroyImmediate(curve);
            DestroyImmediate(shakeEmitter);

            Transform[] pulseEffectTransforms = pulseEffectPrefab.GetComponentsInChildren<Transform>();
            foreach (Transform transform in pulseEffectTransforms)
            {
                if (transform.gameObject.name == "Particles")
                {
                    transform.localRotation = Quaternion.Euler(0f, 0f, 0f);
                }
            }
            ParticleSystem[] pulseParticleSystems = pulseEffectPrefab.GetComponentsInChildren<ParticleSystem>();
            //REMEMBER THIS
            foreach (ParticleSystem particleSystem in pulseParticleSystems)
            {
                ParticleSystem.ShapeModule shape = particleSystem.shape;
                shape.rotation = new Vector3(90f, 0f, 0f);

                ParticleSystemRenderer renderer = particleSystem.GetComponent<ParticleSystemRenderer>();
                if (renderer != null && renderer.renderMode == ParticleSystemRenderMode.Mesh)
                {
                    ParticleSystem.MainModule main = particleSystem.main;
                    main.startRotation3D = true;
                    main.startRotationX = Mathf.PI / 2f;
                    main.startRotationY = 0f;
                    main.startRotationZ = 0f;
                }
            }
        }
        public void CreateSkill()
        {
            SkillDefData skillDefData = new SkillDefData
            {
                objectName = "TitanBodyShockwave",
                skillName = "TitanShockwave",
                esmName = "Stomp",
                activationState = ContentAddition.AddEntityState<ShockwaveStomp>(out _),
                cooldown = stompCooldown.Value,
                combatSkill = true
            };
            SkillDef shockwave = CreateSkillDef<SkillDef>(skillDefData);
            CreateGenericSkill(bodyPrefab, skillDefData.skillName, "TitanPrimaryFamily", shockwave, RoR2.SkillSlot.Primary);
            CreateEntityStateMachine(bodyPrefab, "Stomp");
            AISkillDriverData driverData = new AISkillDriverData
            {
                masterPrefab = masterPrefab,
                customName = "usePrimaryAtCloseRange",
                skillSlot = SkillSlot.Primary,
                minDistance = 0f,
                maxDistance = 50f,
                requireReady = true,
                targetType = RoR2.CharacterAI.AISkillDriver.TargetType.CurrentEnemy,
                aimType = RoR2.CharacterAI.AISkillDriver.AimType.AtCurrentEnemy,
                movementType = RoR2.CharacterAI.AISkillDriver.MovementType.Stop,
                driverUpdateTimerOverride = 2f,
                desiredIndex = 0
            };
            CreateAISkillDriver(driverData);

        }
        public class ShockwaveStomp : BaseSkillState
        {
            private static float baseDuration = 5f;
            private float duration;
            private GameObject shockwaveInstance;
            private int frames;
            private static int framesToWait = 3;
            private static GameObject chargeEffectPrefab = Addressables.LoadAssetAsync<GameObject>(RoR2_Base_Golem.GolemClapCharge_prefab).WaitForCompletion();

            private GameObject leftFootEffect;
            private GameObject rightFootEffect;
            private EffectManagerHelper _emh_leftFootEffect;
            private EffectManagerHelper _emh_rightFootEffect;
            private static float effectInterval = 0.25f;
            private float effectTimer = 0f;
            private Transform leftFootTransform;
            private Transform rightFootTransform;

            private static float jumpVelocity = stompJumpVelocity.Value;
            
            public override void OnEnter()
            {
                base.OnEnter();
                Util.PlaySound("Play_gravekeeper_jump", gameObject);
                duration = baseDuration / attackSpeedStat;
                if (characterMotor.isGrounded)
                {
                    characterMotor.velocity += new Vector3(0f, jumpVelocity, 0f);
                    characterMotor.Motor.ForceUnground();
                }
                ChildLocator childLocator = base.modelLocator.modelTransform.GetComponent<ChildLocator>();
                if (childLocator != null )
                {
                    leftFootTransform = childLocator.FindChild("FootL");
                    rightFootTransform = childLocator.FindChild("FootR");
                    if (leftFootTransform != null)
                    {
                        if (!EffectManager.ShouldUsePooledEffect(chargeEffectPrefab))
                        {
                            leftFootEffect = Instantiate(chargeEffectPrefab, leftFootTransform);
                        }
                        else
                        {
                            _emh_leftFootEffect = EffectManager.GetAndActivatePooledEffect(chargeEffectPrefab, leftFootTransform, true);
                            leftFootEffect = _emh_leftFootEffect.gameObject;
                        }
                    }
                    if (rightFootTransform != null)
                    {
                        if (!EffectManager.ShouldUsePooledEffect(chargeEffectPrefab))
                        {
                            rightFootEffect = Instantiate(chargeEffectPrefab, rightFootTransform);
                        }
                        else
                        {
                            _emh_rightFootEffect = EffectManager.GetAndActivatePooledEffect(chargeEffectPrefab, rightFootTransform, true);
                            rightFootEffect = _emh_rightFootEffect.gameObject;
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
                frames++;
                effectTimer -= Time.fixedDeltaTime;
                //this bastard particle effect will NOT face upwards no matter what I do, will revisit this when I'm less angry
                if (effectTimer < 0)
                {
                    effectTimer += effectInterval;
                    Quaternion rotation = Quaternion.identity;
                    if (leftFootTransform != null)
                    {
                        if (!EffectManager.ShouldUsePooledEffect(pulseEffectPrefab))
                        {
                            Instantiate(pulseEffectPrefab, leftFootTransform.position, rotation);
                        }
                        else
                        {
                            EffectManager.GetAndActivatePooledEffect(pulseEffectPrefab, leftFootTransform.position, rotation);
                        }
                    }
                    if (rightFootTransform != null)
                    {
                        if (!EffectManager.ShouldUsePooledEffect(pulseEffectPrefab))
                        {
                            Instantiate(pulseEffectPrefab, rightFootTransform.position, rotation);
                        }
                        else
                        {
                            EffectManager.GetAndActivatePooledEffect(pulseEffectPrefab, rightFootTransform.position, rotation);
                        }
                    }
                }
                if (characterMotor.isGrounded && frames > framesToWait)
                {
                    if (base.isAuthority)
                    {
                        shockwaveInstance = new GameObject();
                        shockwaveInstance.transform.position = characterBody.footPosition;
                        ShockwaveController controller = shockwaveInstance.AddComponent<ShockwaveController>();
                        controller.owner = characterBody.gameObject;
                        controller.ownerDamageStat = characterBody.damage;
                        controller.teamIndex = characterBody.teamComponent.teamIndex;
                        controller.Setup();
                        Util.PlaySound("Play_grandParent_attack1_boulderLarge_impact", this.gameObject);
                        NetworkServer.Spawn(shockwaveInstance);
                        EffectManager.SpawnEffect(landingEffect, new EffectData { origin = characterBody.footPosition, scale = 3f, }, true);
                        if (leftFootEffect != null)
                        {
                            if (_emh_leftFootEffect != null && _emh_leftFootEffect.OwningPool != null)
                            {
                                _emh_leftFootEffect.OwningPool.ReturnObject(_emh_leftFootEffect);
                            }
                            else
                            {
                                EntityState.Destroy(leftFootEffect);
                            }
                            leftFootEffect = null;
                            _emh_leftFootEffect = null;
                        }
                        if (rightFootEffect != null)
                        {
                            if (_emh_rightFootEffect != null && _emh_rightFootEffect.OwningPool != null)
                            {
                                _emh_rightFootEffect.OwningPool.ReturnObject(_emh_rightFootEffect);
                            }
                            else
                            {
                                EntityState.Destroy(rightFootEffect);
                            }
                            rightFootEffect = null;
                            _emh_rightFootEffect = null;
                        }
                    }
                    outer.SetNextStateToMain();
                    return;
                }
                if (base.fixedAge > duration)
                {
                    outer.SetNextStateToMain();
                    return;
                }
            }
            public override InterruptPriority GetMinimumInterruptPriority()
            {
                return InterruptPriority.PrioritySkill;
            }
        }
        public class ShockwaveController : MonoBehaviour
        {
            public GameObject owner;
            public float ownerDamageStat;
            public TeamIndex teamIndex;
            private float stopwatch;
            private GameObject indicatorPrefab = Addressables.LoadAssetAsync<GameObject>(RoR2_Base_Common.TeamAreaIndicator__GroundOnly_prefab).WaitForCompletion();
            private GameObject innerRingInstance;
            private GameObject outerRingInstance;
            private static float lifetime = stompRingLifetime.Value;
            private static float ringWidth = stompRingWidth.Value;
            private static float height = stompRingHeightCutoff.Value;
            private static float speed = stompRingSpeed.Value;
            private static float timeBetweenDamageTicksPerEnemy = 1f;
            private uint soundID;
            private float effectTimer;
            private static float effectInterval = 0.5f;
            private class HitInfo
            {
                public CharacterBody body;
                public float timeSinceHit;
            }
            private List<HitInfo> recentlyHitEnemies;
            public void Start()
            {
                stopwatch = 0f;
                recentlyHitEnemies = new List<HitInfo>();
                soundID = Util.PlaySound("Play_moonBrother_phaseJump_shockwave_loop", this.gameObject);
            }
            public void Setup()
            {
                innerRingInstance = Instantiate(indicatorPrefab, gameObject.transform.position, Util.QuaternionSafeLookRotation(Vector3.up));
                outerRingInstance = Instantiate(indicatorPrefab, gameObject.transform.position, Util.QuaternionSafeLookRotation(Vector3.up));
                TeamAreaIndicator innerIndicator = innerRingInstance.GetComponent<TeamAreaIndicator>();
                TeamAreaIndicator outerIndicator = outerRingInstance.GetComponent<TeamAreaIndicator>();
                TeamFilter teamFilter = gameObject.AddComponent<TeamFilter>();
                teamFilter.teamIndex = teamIndex;
                innerIndicator.teamFilter = teamFilter;
                outerIndicator.teamFilter = teamFilter;
                innerRingInstance.transform.localScale = Vector3.zero;
                outerRingInstance.transform.localScale = Vector3.one * ringWidth;
            }
            public void FixedUpdate()
            {

                stopwatch += Time.fixedDeltaTime;
                float radius = stopwatch * speed;
                Vector3 innerRingVector = Vector3.one * radius;
                Vector3 outerRingVector = Vector3.one * (radius + ringWidth);
                innerRingVector.y = height;
                outerRingVector.y = height;
                innerRingInstance.transform.localScale = Vector3.one * radius;
                outerRingInstance.transform.localScale = Vector3.one * (radius + ringWidth);
                if (stopwatch > lifetime)
                {
                    AkSoundEngine.StopPlayingID(soundID);
                    Util.PlaySound("Stop_moonBrother_phaseJump_shockwave_loop", this.gameObject);
                    Destroy(innerRingInstance);
                    Destroy(outerRingInstance);
                    Destroy(this.gameObject);
                }
                effectTimer -= Time.fixedDeltaTime;
                if (effectTimer < 0)
                {
                    effectTimer += effectInterval;
                    SpawnEffectWheel(radius);
                }
                CheckForEnemiesWithinRange(radius);
                for (int i = recentlyHitEnemies.Count - 1; i >= 0; i--)
                {
                    HitInfo hitInfo = recentlyHitEnemies[i];
                    if (hitInfo != null)
                    {
                        hitInfo.timeSinceHit += Time.fixedDeltaTime;
                        if (hitInfo.timeSinceHit > timeBetweenDamageTicksPerEnemy)
                        {
                            recentlyHitEnemies.Remove(hitInfo);
                        }
                    }
                }
            }
            public void CheckForEnemiesWithinRange(float radius)
            {
                foreach (CharacterBody body in CharacterBody.instancesList)
                {
                    if (body == null || body.healthComponent == null || !body.healthComponent.alive)
                    {
                        continue;
                    }
                    Vector2 bodyFootPosition = new Vector2(body.footPosition.x, body.footPosition.z);
                    Vector2 epicenter = new Vector2(transform.position.x, transform.position.z);
                    float distance = Vector2.Distance(bodyFootPosition, epicenter);
                    if (distance < radius || distance > radius + ringWidth)
                    {
                        continue;
                    }
                    float bodyYPos = body.footPosition.y;
                    float epicentreYPos = transform.position.y;
                    if (Mathf.Abs(bodyYPos - epicentreYPos) > height)
                    {
                        continue;
                    }
                    if (body.characterMotor == null || body.characterMotor.isGrounded == false)
                    {
                        continue;
                    }
                    if (recentlyHitEnemies.Where(hitInfo => hitInfo.body == body).Any())
                    {
                        continue;
                    }
                    if (body == null)
                    {
                        return;
                    }
                    HitInfo newHitInfo = new HitInfo
                    {
                        body = body,
                        timeSinceHit = 0f
                    };
                    if (body.characterMotor != null)
                    {
                        body.characterMotor.ApplyForce(new Vector3(0f, stompKnockupForce.Value, 0f), true);
                    }
                    recentlyHitEnemies.Add(newHitInfo);
                    if (body.teamComponent == null || body.teamComponent.teamIndex == teamIndex)
                    {
                        continue;
                    }
                    DamageTypeCombo combo = new DamageTypeCombo { damageType = DamageType.Generic, damageSource = DamageSource.Primary };
                    DamageInfo damageInfo = new DamageInfo
                    {
                        damage = ownerDamageStat * stompDamageCoeff.Value / 100f,
                        attacker = owner ?? gameObject,
                        inflictor = owner ?? gameObject,
                        position = body.transform.position,
                        crit = false,
                        force = new Vector3(0f, 0f, 0f),
                        damageType = DamageType.Generic,
                        procCoefficient = 1f,
                        damageColorIndex = DamageColorIndex.Default
                    };
                    body.healthComponent.TakeDamage(damageInfo);
                }
            }
            public void SpawnEffectWheel(float radius)
            {
                float outerRadius = radius + ringWidth;
                float surfaceArea = (Mathf.PI * outerRadius * outerRadius) - (Mathf.PI * radius * radius);
                float numberOfEffects = Mathf.Min(surfaceArea / 12f, 50f) * stompEffectsMult.Value;
                Vector3 pointAboveEpicenter = transform.position + new Vector3(0f, height, 0f);
                float startRadians = UnityEngine.Random.Range(0f, Mathf.PI * 2);
                float interval = Mathf.PI * 2 / numberOfEffects;
                float dist = radius + ringWidth;
                for (int i = 0; i < numberOfEffects; i++)
                {
                    float xModifier = Mathf.Cos(startRadians + (interval * i));
                    float zModifier = Mathf.Sin(startRadians + (interval * i));
                    Vector3 newPosition = pointAboveEpicenter + new Vector3(dist * xModifier, 0f, dist * zModifier);
                    bool success = Physics.Raycast(newPosition, Vector3.down, out RaycastHit hit, height * 2f, LayerIndex.world.mask);
                    if (success)
                    {
                        Vector3 effectPosition = hit.point;
                        EffectManager.SpawnEffect(rumbleEffect, new EffectData { origin = effectPosition, scale = 1f }, true);
                    }
                }
            }
        }
    }
}
