using System.Collections.Generic;
using EntityStates;
using R2API;
using RoR2;
using RoR2.CharacterAI;
using RoR2.Skills;
using RoR2BepInExPack.GameAssetPaths.Version_1_39_0;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Networking;
using static EnemyAbilities.PluginConfig;

namespace EnemyAbilities.Abilities.LunarGolem
{
    [EnemyAbilities.ModuleInfo("Laser Sweep", "Gives Lunar Golems a new Special ability:\n- Laser Sweep: Fires a sweeping laser towards its target that ignites terrain. Ignited terrain explodes after a few seconds.", "Lunar Golem", true)]
    public class LaserSweepModule : BaseModule
    {
        private static GameObject laserPrefab = Addressables.LoadAssetAsync<GameObject>(RoR2_Base_Golem.LaserGolem_prefab).WaitForCompletion().InstantiateClone("laserLunarGolem");
        private static GameObject masterPrefab = Addressables.LoadAssetAsync<GameObject>(RoR2_Base_LunarGolem.LunarGolemMaster_prefab).WaitForCompletion();
        private static GameObject bodyPrefab = Addressables.LoadAssetAsync<GameObject>(RoR2_Base_LunarGolem.LunarGolemBody_prefab).WaitForCompletion();
        public static GameObject segmentPrefab = Addressables.LoadAssetAsync<GameObject>(RoR2_Base_Common.FireTrailSegment_prefab).WaitForCompletion().InstantiateClone("lunarGolemFireTrail");

        public override void Awake()
        {
            base.Awake();
            CreateSkill();
            ModifyLaserPrefab();
            ModifyFireSegmentPrefab();
        }
        public void CreateSkill()
        {
            SkillDef laserSweep = ScriptableObject.CreateInstance<SkillDef>();
            (laserSweep as ScriptableObject).name = "LunarGolemBodyLaserSweep";
            laserSweep.skillName = "LunarGolemLaserSweep";
            laserSweep.activationStateMachineName = "Weapon";
            laserSweep.activationState = ContentAddition.AddEntityState<LaserSweep>(out _);
            laserSweep.baseRechargeInterval = lasersweepCooldown.Value;
            laserSweep.cancelSprintingOnActivation = true;
            laserSweep.isCombatSkill = true;
            laserSweep.baseMaxStock = (int)lasersweepCharges.Value;
            laserSweep.rechargeStock = (int)lasersweepCharges.Value;
            laserSweep.requiredStock = 1;
            laserSweep.stockToConsume = 1;
            laserSweep.resetCooldownTimerOnUse = false;
            laserSweep.interruptPriority = InterruptPriority.Any;
            ContentAddition.AddSkillDef(laserSweep);

            SkillFamily skillFamily = ScriptableObject.CreateInstance<SkillFamily>();
            (skillFamily as ScriptableObject).name = "LunarGolemSpecialFamily";
            skillFamily.variants = [new SkillFamily.Variant() { skillDef = laserSweep }];

            GenericSkill skill = bodyPrefab.AddComponent<GenericSkill>();
            skill.skillName = "LunarGolemLaserSweep";
            skill._skillFamily = skillFamily;

            SkillLocator locator = bodyPrefab.GetComponent<SkillLocator>();
            locator.special = skill;

            ContentAddition.AddSkillFamily(skillFamily);

            AISkillDriver useSpecial = masterPrefab.AddComponent<AISkillDriver>();
            useSpecial.customName = "UseSpecialAndStrafe";
            useSpecial.skillSlot = SkillSlot.Special;
            useSpecial.minDistance = 0f;
            useSpecial.maxDistance = 100f;
            useSpecial.maxUserHealthFraction = lasersweepHealthThreshold.Value / 100f;
            useSpecial.requireSkillReady = true;
            useSpecial.moveTargetType = AISkillDriver.TargetType.CurrentEnemy;
            useSpecial.aimType = AISkillDriver.AimType.AtCurrentEnemy;
            useSpecial.movementType = AISkillDriver.MovementType.StrafeMovetarget;
            masterPrefab.ReorderSkillDrivers(useSpecial, 0);
        }
        public void ModifyLaserPrefab()
        {
            LineRenderer lineRenderer = laserPrefab.GetComponent<LineRenderer>();
            lineRenderer.startWidth = 0.4f;
            lineRenderer.endWidth = 0.8f;
            lineRenderer.startColor = Color.red;
            lineRenderer.endColor = Color.red;
        }
        public void ModifyFireSegmentPrefab()
        {
            ParticleSystem particleSystem = segmentPrefab.GetComponent<ParticleSystem>();
            ParticleSystem.MainModule main = particleSystem.main;
            main.loop = true;
            main.startLifetimeMultiplier *= 5f;
            main.maxParticles = 15;
            ParticleSystem.EmissionModule emission = particleSystem.emission;
            emission.rateOverDistanceMultiplier *= 5f;
            emission.rateOverTimeMultiplier *= 5f;
            ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = particleSystem.sizeOverLifetime;
            ParticleSystem.ShapeModule shape = particleSystem.shape;
            shape.scale = new Vector3(shape.scale.x * 1f, shape.scale.y * .2f, shape.scale.z * 1f);
            sizeOverLifetime.yMultiplier *= 2f;
            ParticleSystemRenderer particleSystemRenderer = segmentPrefab.GetComponent<ParticleSystemRenderer>();
            particleSystemRenderer.material = Addressables.LoadAssetAsync<Material>(RoR2_Base_Common_VFX.matFireStaticBlueLarge_mat).WaitForCompletion();
            particleSystemRenderer.materials = [particleSystemRenderer.material];
        }
        public class LaserSweep : BaseSkillState
        {
            private static float baseWindupDuration = lasersweepWindupDuration.Value;
            private float windupDuration;
            private static float baseSweepDuration = lasersweepSweepDuration.Value;
            private float sweepDuration;
            private static float laserMaxDistance = 200f;
            private int cannonIndex;
            private static float overshootAngle = 35f;

            private GameObject impactEffect = Addressables.LoadAssetAsync<GameObject>(RoR2_Base_Common_VFX.OmniImpactVFXLarge_prefab).WaitForCompletion();

            private GameObject laserInstance;
            private LineRenderer laserLineComponent;
            private Vector3 targetPos;
            private Vector3 startDirection;
            private Vector3 targetDirection;
            private Vector3 laserEndPosition;

            private bool sweeping = false;
            private TrailDetonate trailDetonate;
            private bool shouldAddPoint;
            private BaseAI baseAI;
            private uint soundID;

            public override void OnEnter()
            {
                base.OnEnter();
                sweepDuration = baseSweepDuration / attackSpeedStat;
                windupDuration = baseWindupDuration / attackSpeedStat;
                soundID = Util.PlayAttackSpeedSound("Play_golem_laser_charge", characterBody.gameObject, attackSpeedStat);
                if (characterBody.master == null)
                {
                    return;
                }
                baseAI = characterBody.master.GetComponent<BaseAI>();
                if (activatorSkillSlot != null)
                {
                    int maxCharges = activatorSkillSlot.maxStock;

                    if (maxCharges >= 3)
                    {
                        cannonIndex = activatorSkillSlot.stock % 4;
                    }
                    else if (maxCharges == 2)
                    {
                        cannonIndex = activatorSkillSlot.stock % 2;
                    }
                    else if (maxCharges == 1)
                    {
                        cannonIndex = UnityEngine.Random.RandomRangeInt(0, 4);
                    }
                }
                Ray aimRay = GetAimRay();
                Vector3 aimDirection = aimRay.direction;
                float angle = 25f;
                if (cannonIndex % 2 == 1)
                {
                    angle = -angle;
                }
                Vector3 adjustedDirection = Quaternion.AngleAxis(angle, characterBody.transform.up) * aimDirection;
                laserEndPosition = aimRay.origin + (adjustedDirection * laserMaxDistance);
                bool success = Physics.Raycast(aimRay.origin, adjustedDirection, out RaycastHit hit, laserMaxDistance, LayerIndex.world.mask);
                if (success)
                {
                    laserEndPosition = hit.point;
                }
                string muzzleName = FindMuzzleName();
                if (muzzleName == "")
                {
                    Log.Error($"cannonIndex out of bounds!");
                }
                Transform modelTransform = GetModelTransform();
                if (modelTransform != null)
                {
                    ChildLocator childLocator = modelTransform.GetComponent<ChildLocator>();
                    if (childLocator != null)
                    {
                        Transform muzzleTransform = childLocator.FindChild(muzzleName);
                        if (muzzleTransform != null)
                        {
                            laserInstance = Instantiate(laserPrefab, muzzleTransform.position, muzzleTransform.rotation);
                        }
                        if (laserInstance != null)
                        {
                            if (!laserInstance.activeInHierarchy)
                            {
                                laserInstance.SetActive(true);
                            }
                            if (laserInstance.transform.parent != muzzleTransform)
                            {
                                laserInstance.transform.parent = muzzleTransform;
                            }
                            laserLineComponent = laserInstance.GetComponent<LineRenderer>();
                        }
                    }
                }

            }
            public override void OnExit()
            {
                base.OnExit();
                AkSoundEngine.StopPlayingID(soundID);
                if (laserInstance != null)
                {
                    Destroy(laserInstance);
                }
            }
            public override void Update()
            {
                base.Update();
                if (laserInstance == null || laserLineComponent == null)
                {
                    return;
                }
                Vector3 muzzlePosition = laserInstance.transform.parent.position;
                laserLineComponent.SetPosition(0, muzzlePosition);
                laserLineComponent.SetPosition(1, laserEndPosition);
            }
            public override void FixedUpdate()
            {
                base.FixedUpdate();
                EffectManager.SpawnEffect(impactEffect, new EffectData { scale = 1f, origin = laserEndPosition }, true);
                Ray aimRay = GetAimRay();
                Vector3 normal = Vector3.zero;
                if (base.fixedAge > windupDuration && !sweeping)
                {
                    sweeping = true;
                    targetPos = aimRay.GetPoint(laserMaxDistance);
                    bool success = Physics.Raycast(aimRay.origin, aimRay.direction, out RaycastHit hit, laserMaxDistance, LayerIndex.world.mask);
                    if (success)
                    {
                        targetPos = hit.point;
                    }
                    if (baseAI != null)
                    {
                        if (baseAI.currentEnemy != null && baseAI.currentEnemy.characterBody != null)
                        {
                            //it likes feet
                            targetPos = baseAI.currentEnemy.characterBody.footPosition;
                        }
                    }

                    startDirection = (laserEndPosition - aimRay.origin).normalized;
                    targetDirection = (targetPos - aimRay.origin).normalized;

                    GameObject trailObject = new GameObject();
                    trailDetonate = trailObject.AddComponent<TrailDetonate>();
                    trailDetonate.owner = characterBody.gameObject;
                    trailDetonate.teamIndex = characterBody.teamComponent.teamIndex;
                    trailDetonate.ownerDamage = characterBody.damage;
                }
                if (sweeping)
                {
                    shouldAddPoint = true;
                    targetDirection = (targetPos - aimRay.origin).normalized;
                    Vector3 newDirection = RotateAndOvershoot(base.fixedAge - windupDuration);
                    Vector3 newPosition = aimRay.origin + newDirection * laserMaxDistance;
                    bool success = Physics.Raycast(aimRay.origin, newDirection, out RaycastHit hit, laserMaxDistance, LayerIndex.world.mask);
                    if (success)
                    {
                        newPosition = hit.point;
                        normal = hit.normal;
                    }
                    else
                    {
                        shouldAddPoint = false;
                    }
                    laserEndPosition = newPosition;
                }
                if (sweeping && trailDetonate != null && shouldAddPoint == true)
                {
                    trailDetonate.UpdateTrailWithNewPoint(laserEndPosition, normal);
                }
                if (base.fixedAge > sweepDuration + windupDuration && base.isAuthority)
                {
                    outer.SetNextStateToMain();
                }
            }
            public string FindMuzzleName()
            {
                switch (cannonIndex)
                {
                    case 0: return "MuzzleRT";
                    case 1: return "MuzzleLT";
                    case 2: return "MuzzleRB";
                    case 3: return "MuzzleLB";
                }
                return "";
            }
            public override InterruptPriority GetMinimumInterruptPriority()
            {
                return InterruptPriority.PrioritySkill;
            }
            public Vector3 RotateAndOvershoot(float time)
            {

                float sweepPercentage = Mathf.Clamp01(time / sweepDuration);

                Vector3 rotationAxis = Vector3.Cross(startDirection, targetDirection).normalized;
                if (rotationAxis == Vector3.zero)
                {
                    rotationAxis = Vector3.up;
                }
                float totalAngle = Vector3.Angle(startDirection, targetDirection) + overshootAngle;
                float squared = sweepPercentage * sweepPercentage;
                float currentAngle = totalAngle * squared;
                Quaternion rotation = Quaternion.AngleAxis(currentAngle, rotationAxis);
                return rotation * startDirection;
            }
        }
        //similar to RoR2.DamageTrail
        public class TrailDetonate : MonoBehaviour
        {
            public GameObject owner;
            public float damageTickInterval = 0.2f;
            public float radius = 2.5f;
            public float height = 0.5f;
            private float pointLifeTime = lasersweepExplosionDelay.Value;
            private float stopwatch = 0f;
            private static float dotDamageCoefficient = lasersweepDoTDamageCoeff.Value / 100f;
            private static float explosionDamageCoefficient = lasersweepExplosionDamageCoeff.Value / 100f;

            private float nextDamageTrailUpdate;
            private List<GameObject> ignoredObjects = new List<GameObject>();
            public TeamIndex teamIndex;
            public float ownerDamage;

            private static GameObject explosionEffect = Addressables.LoadAssetAsync<GameObject>(RoR2_Junk_ArchWisp.OmniExplosionVFXArchWispCannonImpact_prefab).WaitForCompletion();

            private struct TrailPoint
            {
                public Vector3 position;
                public float localStartTime;
                public float localEndTime;
                public Transform segmentTransform;
            }
            private List<TrailPoint> trailPoints = new List<TrailPoint>();
            public void Start()
            {
                stopwatch = 0f;
                nextDamageTrailUpdate = damageTickInterval;
            }
            public void FixedUpdate()
            {
                stopwatch += Time.fixedDeltaTime;
                while (trailPoints.Count > 0 && trailPoints[0].localEndTime <= stopwatch)
                {
                    RemovePoint(0);
                }
                if (nextDamageTrailUpdate <= stopwatch)
                {
                    nextDamageTrailUpdate += damageTickInterval;
                    DoDamage();
                }
                if (stopwatch > 3f && trailPoints.Count == 0)
                {
                    Destroy(this.gameObject);
                }
            }
            public void DoDamage()
            {
                if (!NetworkServer.active || trailPoints.Count == 0)
                {
                    return;
                }
                ignoredObjects.Clear();
                Vector3 vector = trailPoints[^1].position;
                TeamIndex attackerTeamIndex = teamIndex;
                if (owner != null)
                {
                    ignoredObjects.Add(owner);
                }
                DamageInfo damageInfo = new DamageInfo();
                damageInfo.attacker = owner;
                damageInfo.inflictor = base.gameObject;
                damageInfo.crit = false;
                damageInfo.damage = ownerDamage * dotDamageCoefficient;
                damageInfo.damageColorIndex = DamageColorIndex.Default;
                damageInfo.damageType = new DamageTypeCombo { damageType = DamageType.Generic, damageSource = DamageSource.Special };
                damageInfo.force = Vector3.zero;
                damageInfo.procCoefficient = 0f;
                for (int i = trailPoints.Count - 1; i >= 0; i--)
                {
                    Vector3 position = trailPoints[i].position;
                    Vector3 forward = position - vector;
                    Vector3 halfExtents = new Vector3(radius, height, forward.magnitude);
                    Vector3 center = Vector3.Lerp(position, vector, 0.5f);
                    Quaternion orientation = Util.QuaternionSafeLookRotation(forward);
                    Collider[] colliders;
                    int num2 = HGPhysics.OverlapBox(out colliders, center, halfExtents, orientation, LayerIndex.entityPrecise.mask);
                    for (int j = 0; j < num2; j++)
                    {
                        HurtBox component = colliders[j].GetComponent<HurtBox>();
                        if (!component)
                        {
                            continue;
                        }
                        HealthComponent healthComponent = component.healthComponent;
                        if ((bool)healthComponent)
                        {
                            GameObject item = healthComponent.gameObject;
                            if (!ignoredObjects.Contains(item) && FriendlyFireManager.ShouldSplashHitProceed(healthComponent, attackerTeamIndex))
                            {
                                ignoredObjects.Add(item);
                                damageInfo.position = colliders[j].transform.position;
                                damageInfo.inflictedHurtbox = component;
                                healthComponent.TakeDamage(damageInfo);
                            }
                        }
                    }
                    HGPhysics.ReturnResults(colliders);
                    vector = position;
                }
            }
            public void UpdateTrailWithNewPoint(Vector3 position, Vector3 normal)
            {
                if (trailPoints.Count > 0)
                {
                    TrailPoint mostRecentPoint = trailPoints[^1];
                    float distance = Vector3.Distance(position, mostRecentPoint.position);
                    if (distance > 3.5)
                    {
                        AddPoint(position, normal);
                    }
                }
                else
                {
                    AddPoint(position, normal);
                }
            }
            public void AddPoint(Vector3 position, Vector3 normal)
            {
                TrailPoint point = default(TrailPoint);
                point.position = position;
                point.localStartTime = stopwatch;
                point.localEndTime = stopwatch + pointLifeTime;
                Vector3 tangent = Vector3.ProjectOnPlane(Vector3.forward, normal);
                if (segmentPrefab != null)
                {
                    if (!EffectManager.ShouldUsePooledEffect(segmentPrefab))
                    {
                        point.segmentTransform = Instantiate(segmentPrefab, position, Util.QuaternionSafeLookRotation(tangent)).transform;
                    }
                    else
                    {
                        EffectManagerHelper activatePoolEffect = EffectManager.GetAndActivatePooledEffect(segmentPrefab, position, Util.QuaternionSafeLookRotation(tangent));
                        point.segmentTransform = activatePoolEffect.gameObject.transform;
                    }
                }
                trailPoints.Add(point);
            }
            public void RemovePoint(int pointIndex) 
            {
                EffectManager.SpawnEffect(explosionEffect, new EffectData { origin = trailPoints[pointIndex].position, scale = 8f }, true);
                if (NetworkServer.active)
                {
                    BlastAttack attack = new BlastAttack();
                    attack.attacker = owner;
                    attack.baseDamage = ownerDamage * explosionDamageCoefficient;
                    attack.baseForce = 3000f;
                    attack.damageColorIndex = DamageColorIndex.Default;
                    attack.position = trailPoints[pointIndex].position;
                    attack.radius = lasersweepExplosionRadius.Value;
                    attack.damageType = new DamageTypeCombo { damageSource = DamageSource.Special, damageType = DamageType.Generic };
                    attack.procCoefficient = 0f;
                    attack.crit = false;
                    attack.falloffModel = BlastAttack.FalloffModel.SweetSpot;
                    attack.teamIndex = teamIndex;
                    attack.Fire();
                }
                if (trailPoints[pointIndex].segmentTransform != null)
                {
                    if (!EffectManager.UsePools)
                    {
                        Destroy(trailPoints[pointIndex].segmentTransform.gameObject);
                    }
                    else
                    {
                        GameObject segmentObject = trailPoints[pointIndex].segmentTransform.gameObject;
                        EffectManagerHelper efh = segmentObject.GetComponent<EffectManagerHelper>();
                        if (efh != null && efh.OwningPool != null)
                        {
                            efh.OwningPool.ReturnObject(efh);
                        }
                        else
                        {
                            Destroy(segmentObject);
                        }

                    }
                }
                trailPoints.RemoveAt(pointIndex);
            }
            public void DestroyTrail()
            {
                Destroy(this.gameObject);
            }
        }
    }
}
