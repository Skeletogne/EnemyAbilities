using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using EntityStates;
using R2API;
using R2API.Utils;
using RoR2;
using RoR2.CharacterAI;
using RoR2.Projectile;
using RoR2.Skills;
using RoR2BepInExPack.GameAssetPaths.Version_1_39_0;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace EnemyAbilities.Abilities.ExtractorUnit
{
    [EnemyAbilities.ModuleInfo("Tendril Tether", "Gives Solus Extractors a new Utility:\n-Tendril Tether: The Extractor shoots out a long mechanical tendril that attaches to both units and terrain and pulls the extractor towards the point of impact. If it hits an enemy, it deals damage, slows and provides the Extractor an attack speed buff. Enabling this module also slightly increases Solus Extractor move speed, and makes them prioritise enemies with items that they can steal.", "Solus Extractor", true)]
    public class HookModule : BaseModule
    {
        private static GameObject bodyPrefab = Addressables.LoadAssetAsync<GameObject>(RoR2_DLC3_ExtractorUnit.ExtractorUnitBody_prefab).WaitForCompletion();
        private static GameObject masterPrefab = Addressables.LoadAssetAsync<GameObject>(RoR2_DLC3_ExtractorUnit.ExtractorUnitMaster_prefab).WaitForCompletion();
        private static GameObject projectilePrefab = Addressables.LoadAssetAsync<GameObject>(RoR2_Base_Lemurian.Fireball_prefab).WaitForCompletion().InstantiateClone("extractorHook");
        private static GameObject hookTetherPrefab = Addressables.LoadAssetAsync<GameObject>(RoR2_Base_moon2.BloodSiphonTetherVFX_prefab).WaitForCompletion().InstantiateClone("extractorHookTetherPrefab");
        private static CharacterSpawnCard cscExtractor = Addressables.LoadAssetAsync<CharacterSpawnCard>(RoR2_DLC3_ExtractorUnit.cscExtractorUnit_asset).WaitForCompletion();

        private static BuffDef extractorBuff;

        internal static ConfigEntry<float> cooldown;
        internal static ConfigEntry<float> maxUseDistance;
        internal static ConfigEntry<float> telegraphDuration;
        internal static ConfigEntry<float> projectileSpeed;
        internal static ConfigEntry<bool> isPredictive;
        internal static ConfigEntry<float> damageCoeff;
        internal static ConfigEntry<float> projectileDistanceLimit;
        internal static ConfigEntry<float> berserkBuffDuration;
        internal static ConfigEntry<bool> resetPrimary;
        internal static ConfigEntry<float> pullSpeed;
        internal static ConfigEntry<bool> applySlow;
        internal static ConfigEntry<bool> prioritiseInventories;

        public override void RegisterConfig()
        {
            base.RegisterConfig();
            cooldown = BindFloat("Tether Cooldown", 6f, "Cooldown of the tether ability", 5f, 20f, 0.1f, PluginConfig.FormatType.Time);
            maxUseDistance = BindFloat("Tether Max Use Distance", 70f, "The maximum distance that Extractors will attempt to tether towards their target.", 40f, 100f, 1f, PluginConfig.FormatType.Distance);
            telegraphDuration = BindFloat("Tether Telegraph Duration", 0.8f, "The duration of time that Extractors will spend charging their tether.", 0.5f, 3f, 0.1f, PluginConfig.FormatType.Time);
            projectileSpeed = BindFloat("Tether Projectile Speed", 100f, "The movement speed of the tether projectile. Lower values are recommended if using predictive aiming.", 40f, 160f, 1f, PluginConfig.FormatType.Speed);
            isPredictive = BindBool("Tether Uses Prediction", false, "If enabled, Extractors will attempt to lead their shots in order to more often hit the player.\nI'll be honest, the prediction I designed for this is kinda ass, hence why it only exists as a config option now. Enable at your own peril.");
            damageCoeff = BindFloat("Tether Damage Coefficient", 200f, "The damage coefficient of the tether upon hitting a target.", 100f, 400f, 5f, PluginConfig.FormatType.Percentage);
            projectileDistanceLimit = BindFloat("Tether Max Projectile Distance", 80f, "The maximum distance that the tether can travel before disappearing. Setting this lower than max use distance will cause Extractors to sometimes fire whilst out of range.", 40f, 200f, 1f, PluginConfig.FormatType.Distance);
            berserkBuffDuration = BindFloat("Tether Hit Buff Duration", 5f, "The duration of time that Extractors gain move speed and attack speed after successfully landing their tether on an enemy.", 0f, 10f, 0.1f, PluginConfig.FormatType.Time);
            resetPrimary = BindBool("Tether Hit Reset Primary", true, "If enabled, Extractors will immediately be able to use Extract after landing their Tether on an enemy.");
            pullSpeed = BindFloat("Tether Pull Travel Speed", 65f, "The speed at with the Extractor is pulled towards its tether upon a hit. Higher values may start flinging the Extractor!", 40f, 100f, 1f, PluginConfig.FormatType.Speed);
            applySlow = BindBool("Tether Hit Apply Slow", true, "If enabled, Extractors will apply a slow upon landing a successful tether.");
            prioritiseInventories = BindBool("Prioritise Inventories", true, "If enabled, Extractors will prioritise attacking enemies that have items that are not on their blacklist.");
            BindStats(bodyPrefab, [cscExtractor], new StatOverrides { baseMoveSpeed = 18f, baseAcceleration = 60f, directorCost = 30f });
        }
        public void CreateSkill()
        {
            SkillDefData skillDefData = new SkillDefData
            {
                objectName = "ExtractorUnitBodyHook",
                skillName = "ExtractorUnitHook",
                esmName = "Weapon",
                activationState = ContentAddition.AddEntityState<Hook>(out _),
                cooldown = cooldown.Value,
                combatSkill = true,
                intPrio = InterruptPriority.Any
            };
            SkillDef hook = CreateSkillDef<SkillDef>(skillDefData);
            CreateGenericSkill(bodyPrefab, hook.skillName, "ExtractorUnitUtilityFamily", hook, SkillSlot.Utility);

            AISkillDriverData driverData = new AISkillDriverData
            {
                masterPrefab = masterPrefab,
                customName = "useUtility",
                skillSlot = SkillSlot.Utility,
                requiredSkillDef = hook,
                requireReady = true,
                minDistance = 16f,
                maxDistance = maxUseDistance.Value,
                targetType = prioritiseInventories.Value == true ? AISkillDriver.TargetType.Custom : AISkillDriver.TargetType.CurrentEnemy,
                movementType = AISkillDriver.MovementType.ChaseMoveTarget,
                aimType = AISkillDriver.AimType.AtMoveTarget,
                desiredIndex = 3,
                driverUpdateTimerOverride = 2f,
                moveInputScale = 0.5f
            };
            CreateAISkillDriver(driverData);

            if (prioritiseInventories.Value)
            {
                foreach (AISkillDriver driver in masterPrefab.GetComponents<AISkillDriver>())
                {
                    if (driver.customName == "StrafeSlowExtractWhenTooClose" || driver.customName == "SlowWhenClose" || driver.customName == "WarningBeforeExtract" || driver.customName == "PathFromAfar" || driver.customName == "Flee")
                    {
                        driver.moveTargetType = AISkillDriver.TargetType.Custom;
                        driver.aimType = AISkillDriver.AimType.AtMoveTarget;
                    }
                }
            }
        }
        public override void Initialise()
        {
            base.Initialise();
            CreateSkill();
            CreateBuffDef();
            SetUpTetherPrefab();
            ProjectileController controller = projectilePrefab.GetComponent<ProjectileController>();
            ProjectileExtractorHook hook = projectilePrefab.AddComponent<ProjectileExtractorHook>();
            controller.ghostPrefab = null;
            SphereCollider collider = projectilePrefab.GetComponent<SphereCollider>();
            collider.radius = 0.5f;
            ProjectileSingleTargetImpact impact = projectilePrefab.GetComponent<ProjectileSingleTargetImpact>();
            impact.impactEffect = Addressables.LoadAssetAsync<GameObject>(RoR2_DLC3_ExtractorUnit.ExtractorUnitHitEffectVFX_prefab).WaitForCompletion();
            if (prioritiseInventories.Value)
            {
                ExtractorUnitTargetController targetController = bodyPrefab.AddComponent<ExtractorUnitTargetController>();
            }
            RecalculateStatsAPI.GetStatCoefficients += ApplyBuffStats;
        }

        private void ApplyBuffStats(CharacterBody sender, RecalculateStatsAPI.StatHookEventArgs args)
        {
            if (sender != null && sender.HasBuff(extractorBuff))
            {
                //+25% move speed, +100% attack speed.
                args.moveSpeedMultAdd += 0.25f;
                args.attackSpeedMultAdd += 1f;
            }
        }

        public void SetUpTetherPrefab()
        {
            Material material = Addressables.LoadAssetAsync<Material>(RoR2_DLC3_ExtractorUnit.ExtractorLeg_mat).WaitForCompletion();
            Material materialClone = new Material(material);
            material.mainTextureScale = Vector2.one;
            LineRenderer lineRenderer = hookTetherPrefab.GetComponent<LineRenderer>();
            lineRenderer.material = material;
            lineRenderer.sharedMaterial = material;
        }
        public void CreateBuffDef()
        {
            //could maybe add create buff def to baseModule...
            extractorBuff = ScriptableObject.CreateInstance<BuffDef>();
            extractorBuff.name = "ExtractorHookBuff";
            extractorBuff.buffColor = new Color(0.494f, 0.784f, 0.890f, 1f);
            extractorBuff.iconSprite = Addressables.LoadAssetAsync<Sprite>(RoR2_Base_Common_MiscIcons.texAttackIcon_png).WaitForCompletion();
            extractorBuff.canStack = false;
            extractorBuff.isCooldown = false;
            extractorBuff.isDOT = false;
            extractorBuff.isHidden = false;
            ContentAddition.AddBuffDef(extractorBuff);
        }
        public class Hook : BaseSkillState
        {
            private static float baseDuration = telegraphDuration.Value;
            private float duration;
            private static float hookProjectileSpeed = projectileSpeed.Value;
            private static float hookDamageCoefficient = damageCoeff.Value / 100f;
            private bool hookIsPredictive = isPredictive.Value;
            private BaseAI baseAI;
            private static GameObject chargePrefab = Addressables.LoadAssetAsync<GameObject>(RoR2_Base_RoboBallBoss.ChargeEyeBlast_prefab).WaitForCompletion();
            private GameObject chargeEffectInstance;
            private EffectManagerHelper _emh_chargeEffectInstance;
            private Transform muzzleTransform;
            public override void OnEnter()
            {

                base.OnEnter();
                Util.PlaySound("Play_MULT_m1_snipe_charge", this.gameObject);
                Transform modelTransform = GetModelTransform();
                if (modelLocator != null)
                {
                    ChildLocator childLocator = modelTransform.GetComponent<ChildLocator>();
                    if (childLocator != null)
                    {
                        muzzleTransform = childLocator.FindChild("Muzzle");
                    }
                }
                duration = baseDuration / attackSpeedStat;
                if (hookIsPredictive && characterBody != null && characterBody.master != null)
                {
                    baseAI = characterBody.master.GetComponent<BaseAI>();
                }
                if (chargePrefab != null)
                {
                    if (muzzleTransform != null && chargePrefab != null)
                    {
                        if (!EffectManager.ShouldUsePooledEffect(chargePrefab))
                        {
                            chargeEffectInstance = Object.Instantiate(chargePrefab, muzzleTransform.position, muzzleTransform.rotation);
                        }
                        else
                        {
                            _emh_chargeEffectInstance = EffectManager.GetAndActivatePooledEffect(chargePrefab, muzzleTransform.position, muzzleTransform.rotation);
                            chargeEffectInstance = _emh_chargeEffectInstance.gameObject;
                        }
                        chargeEffectInstance.transform.parent = muzzleTransform;
                        ScaleParticleSystemDuration particleDuration = chargeEffectInstance.GetComponent<ScaleParticleSystemDuration>();
                        if (particleDuration != null)
                        {
                            particleDuration.newDuration = duration;
                        }
                    }
                }
            }
            public override void OnExit()
            {
                base.OnExit();
                if (chargeEffectInstance != null)
                {
                    EffectManager.ReturnToPoolOrDestroyInstance(_emh_chargeEffectInstance, ref chargeEffectInstance);
                    chargeEffectInstance = null;
                    _emh_chargeEffectInstance = null;
                }
            }
            public void OnTargetLost()
            {
                if (base.isAuthority)
                {
                    outer.SetNextStateToMain();
                }
            }
            public override void FixedUpdate()
            {
                base.FixedUpdate();
                if (base.fixedAge > duration)
                {
                    Ray aimRay = GetAimRay();
                    Vector3 position = aimRay.origin;
                    Vector3 direction = aimRay.direction;
                    if (hookIsPredictive && baseAI != null && baseAI.customTarget != null)
                    {
                        CharacterBody targetBody = baseAI.customTarget.characterBody;
                        if (targetBody != null)
                        {
                            Rigidbody targetRigid = targetBody.gameObject.GetComponent<Rigidbody>();
                            CharacterMotor targetMotor = targetBody.gameObject.GetComponent<CharacterMotor>();
                            Vector3 targetVelocity = Vector3.zero;
                            if (targetRigid != null)
                            {
                                targetVelocity = targetRigid.velocity;
                            }
                            if (targetMotor != null)
                            {
                                targetVelocity = targetMotor.velocity;
                            }
                            Vector3 targetPosition = targetBody.transform.position;
                            Vector3 distance = targetPosition - position;
                            //its time to FINALLY use the QUADRATIC EQUATION BAYBEE
                            float a = Vector3.Dot(targetVelocity, targetVelocity) - hookProjectileSpeed * hookProjectileSpeed;
                            float b = 2f * Vector3.Dot(distance, targetVelocity);
                            float c = Vector3.Dot(distance, distance);
                            float time = SolveQuadratic(a, b, c);
                            if (time > 0f)
                            {
                                Vector3 intercept = targetPosition + targetVelocity * time;
                                Vector3 raycastStart = new Vector3(intercept.x, intercept.y + 20f, intercept.z);
                                bool success = Physics.Raycast(raycastStart, Vector3.down, out RaycastHit hit, 100f, LayerIndex.world.mask);
                                if (success)
                                {
                                    intercept = hit.point + Vector3.up * targetBody.radius * 2f;
                                }
                                else
                                {
                                    intercept.y = targetPosition.y;
                                }
                                direction = (intercept - position).normalized;
                            }
                        }
                    }
                    DamageTypeCombo combo = new DamageTypeCombo { damageType = DamageType.Generic, damageSource = DamageSource.Utility };
                    Util.PlaySound("Play_MULT_m1_grenade_launcher_shoot", this.gameObject);
                    ProjectileManager.instance.FireProjectile(projectilePrefab, position, Util.QuaternionSafeLookRotation(direction), characterBody.gameObject, hookDamageCoefficient * damageStat, 500f, RollCrit(), DamageColorIndex.Default, null, hookProjectileSpeed, combo);
                    outer.SetNextStateToMain();
                }
            }
            public override InterruptPriority GetMinimumInterruptPriority()
            {
                return InterruptPriority.Skill;
            }
            public float SolveQuadratic(float a, float b, float c)
            {
                if (Mathf.Approximately(a, 0f))
                {
                    if (Mathf.Approximately(b, 0f))
                    {
                        return -1f;
                    }
                    float t = -c / b;
                    return t > 0f ? t : -1f;
                }
                float disc = b * b - 4f * a * c;
                if (disc < 0f)
                {
                    return -1f;
                }
                float sqrtDisc = Mathf.Sqrt(disc);
                float t1 = (-b - sqrtDisc) / (2f * a);
                float t2 = (-b + sqrtDisc) / (2f * a);

                if (t1 > 0f && t2 > 0f)
                {
                    return Mathf.Min(t1, t2);
                }
                if (t1 > 0f)
                {
                    return t1;
                }
                if (t2 > 0f)
                {
                    return t2;
                }
                //both solutions are ass
                return -1f;
            }
        }
        public class ProjectileExtractorHook : MonoBehaviour, IProjectileImpactBehavior
        {
            private ProjectileController controller;
            private CharacterBody ownerBody;
            private static float maxHookRange = projectileDistanceLimit.Value;
            private GameObject hookTetherObject;
            private HookTether hookTether;
            private float dropStopwatch;
            private static float projectileDropStartTime = 0.5f;
            private Rigidbody rigidbody;
            private static GameObject speedBoostEffect = Addressables.LoadAssetAsync<GameObject>(RoR2_DLC3_FriendUnit.KineticAuraLaunch_prefab).WaitForCompletion();
            public void Start()
            {
                controller = GetComponent<ProjectileController>();
                if (controller != null && controller.owner != null)
                {
                    ownerBody = controller.owner.GetComponent<CharacterBody>();
                }
                hookTetherObject = new GameObject();
                hookTether = hookTetherObject.AddComponent<HookTether>();
                if (ownerBody != null && ownerBody.master)
                {
                    hookTether.ownerTransform = ownerBody.transform;
                    hookTether.ownerBody = ownerBody;
                    BaseAI baseAI = ownerBody.master.GetComponent<BaseAI>();
                    if (baseAI != null && baseAI.customTarget != null)
                    {
                        hookTether.ownerTargetBody = baseAI.customTarget.characterBody;
                    }
                }
                rigidbody = GetComponent<Rigidbody>();
            }
            public void OnProjectileImpact(ProjectileImpactInfo info)
            {
                hookTether.AttachHookEndOnImpact(info);
                Util.PlaySound("Play_gravekeeper_attack2_impact", this.gameObject);
                if (info.collider == null)
                {
                    return;
                }
                HurtBox hurtBox = info.collider.GetComponent<HurtBox>();
                if (hurtBox == null)
                {
                    return;
                }
                if (controller == null)
                {
                    return;
                }
                if (controller.owner == null)
                {
                    return;
                }
                if (ownerBody != null)
                {
                    if (hurtBox.healthComponent != null && hurtBox.healthComponent.body != null)
                    {
                        CharacterBody victimBody = hurtBox.healthComponent.body;
                        if (victimBody.teamComponent != null && ownerBody.teamComponent != null && ownerBody.teamComponent.teamIndex != victimBody.teamComponent.teamIndex)
                        {
                            EffectData effectData = new EffectData
                            {

                                origin = ownerBody.transform.position,
                                scale = ownerBody.radius,
                                rotation = Util.QuaternionSafeLookRotation(ownerBody.inputBank.aimDirection)
                            };
                            effectData.SetHurtBoxReference(ownerBody.mainHurtBox);
                            EffectManager.SpawnEffect(speedBoostEffect, effectData, true);
                            ownerBody.AddTimedBuff(extractorBuff, berserkBuffDuration.Value);
                            if (resetPrimary.Value)
                            {
                                ownerBody.skillLocator.primary.Reset();
                            }
                        }
                    }
                }
            }
            public void FixedUpdate()
            {
                dropStopwatch += Time.fixedDeltaTime;
                if (dropStopwatch > projectileDropStartTime && rigidbody != null && rigidbody.useGravity == false)
                {
                    rigidbody.useGravity = true;
                }
                if (hookTetherObject != null)
                {
                    hookTetherObject.transform.position = transform.position;
                }
                if (ownerBody != null)
                {
                    float distance = Vector3.Distance(ownerBody.transform.position, gameObject.transform.position);
                    if (distance > maxHookRange)
                    {
                        Destroy(hookTetherObject);
                        Destroy(this.gameObject);
                    }
                }
            }
        }
        public class HookTether : MonoBehaviour
        {
            public Transform ownerTransform;
            public CharacterBody ownerBody;
            public CharacterBody ownerTargetBody;
            private TetherVfxOrigin tetherVFXOrigin;
            private bool impact;
            private static float timeAfterImpact = 3f;
            private float impactStopwatch;
            private CharacterBody hitBody;
            private static float hookSpeed = pullSpeed.Value;
            private static float rangeToUntether = 6f;
            private static float maxLifetime = 8f;
            private float stopwatch;
            private bool tetherCreated;

            public void Start()
            {
                tetherVFXOrigin = gameObject.AddComponent<TetherVfxOrigin>();
                tetherVFXOrigin.transform = gameObject.transform;
                tetherVFXOrigin.tetherPrefab = hookTetherPrefab;
            }
            public void AttachHookEndOnImpact(ProjectileImpactInfo info)
            {
                impact = true;
                if (info.collider == null)
                {
                    return;
                }
                HurtBox hurtBox = info.collider.GetComponent<HurtBox>();
                if (hurtBox != null && hurtBox.healthComponent != null && hurtBox.healthComponent.body != null)
                {
                    hitBody = hurtBox.healthComponent.body;
                }
                transform.position = info.estimatedPointOfImpact;
            }
            public void FixedUpdate()
            {
                stopwatch += Time.fixedDeltaTime;
                if (stopwatch > maxLifetime || ownerBody == null || ownerTransform == null || gameObject == null || gameObject.transform == null)
                {
                    Destroy(this.gameObject);
                }
                if (ownerTransform != null && tetherVFXOrigin != null && !tetherCreated)
                {
                    tetherVFXOrigin.SetTetheredTransforms([ownerTransform]);
                    tetherCreated = true;
                }
                if (impact && ownerBody != null)
                {
                    Vector3 direction = (gameObject.transform.position - ownerBody.transform.position).normalized;
                    Vector3 velocity = direction * hookSpeed;
                    float distanceToTetherTarget = Vector3.Distance(transform.position, ownerBody.transform.position);
                    CharacterMotor motor = ownerBody.characterMotor;
                    if (motor != null)
                    {
                        motor.Motor.ForceUnground();
                        float t = 1f - rangeToUntether / distanceToTetherTarget;
                        float damping = 0.25f + 0.75f * Mathf.Pow(t, 1f);
                        motor.velocity = velocity * damping;
                    }
                    if (hitBody != null)
                    {
                        transform.position = hitBody.transform.position;
                        if (applySlow.Value == true)
                        {
                            hitBody.AddTimedBuff(RoR2Content.Buffs.Slow60, 0.5f);
                        }
                    }
                    impactStopwatch += Time.fixedDeltaTime;
                    bool ownerTargetBodyInRange = ownerTargetBody != null && (Vector3.Distance(ownerTargetBody.transform.position, ownerBody.transform.position) < rangeToUntether);
                    bool tetherTargetInRange = distanceToTetherTarget < rangeToUntether;
                    //detaches if: hooking for longer than 1s. the end of it's hook is within 5m. it's current target is within 5m.

                    if (impactStopwatch > timeAfterImpact || ownerTargetBodyInRange || tetherTargetInRange)
                    {
                        if (motor != null)
                        {
                            motor.velocity = Vector3.zero;
                        }
                        Destroy(this.gameObject);
                    }
                }
            }
        }
        public class ExtractorUnitTargetController : MonoBehaviour
        {
            private BaseAI baseAI;
            private CharacterBody body;
            private static float searchInterval = 0.25f;
            private float timer;
            public void Start()
            {
                body = GetComponent<CharacterBody>();
                if (body == null)
                {
                    return;
                }
                CharacterMaster master = body.master;
                if (master == null)
                {
                    return;
                }
                baseAI = master.gameObject.GetComponent<BaseAI>();
                if (baseAI == null)
                {
                    return;
                }
            }
            public void FixedUpdate()
            {
                timer -= Time.fixedDeltaTime;
                if (timer < 0)
                {
                    timer += searchInterval;
                    if (body == null || body.inputBank == null || body.healthComponent == null || body.healthComponent.alive == false || baseAI == null || body.teamComponent == null)
                    {
                        return;
                    }
                    TeamMask mask = TeamMask.GetEnemyTeams(body.teamComponent.teamIndex);
                    Ray aimRay = body.inputBank.GetAimRay();
                    BullseyeSearch search = new BullseyeSearch();
                    search.viewer = body;
                    search.filterByDistinctEntity = true;
                    search.filterByLoS = false;
                    search.maxDistanceFilter = Mathf.Infinity;
                    search.minDistanceFilter = 0f;
                    search.searchOrigin = aimRay.origin;
                    search.searchDirection = aimRay.direction;
                    search.maxAngleFilter = 360f;
                    search.sortMode = BullseyeSearch.SortMode.DistanceAndAngle;
                    search.teamMaskFilter = mask;
                    search.RefreshCandidates();
                    IEnumerable<HurtBox> allTargets = search.GetResults();
                    if (!allTargets.Any())
                    {
                        return;
                    }
                    IEnumerable<HurtBox> extractTargets = allTargets.Where(hurtBox => HasInventoryWithValidItems(hurtBox) == true);
                    GameObject targetObject;
                    if (extractTargets.Any())
                    {
                        targetObject = extractTargets.FirstOrDefault()?.healthComponent.body.gameObject;
                    }
                    else
                    {
                        targetObject = allTargets.FirstOrDefault()?.healthComponent.body.gameObject;
                    }
                    baseAI.customTarget.gameObject = targetObject;
                }
            }
            public bool HasInventoryWithValidItems(HurtBox hurtBox)
            {
                if (hurtBox == null)
                {
                    return false;
                }
                if (hurtBox.healthComponent == null)
                {
                    return false;
                }
                if (hurtBox.healthComponent.alive == false)
                {
                    return false;
                }
                CharacterBody victimBody = hurtBox.healthComponent.body;
                if (victimBody == null)
                {
                    return false;
                }
                Inventory inventory = hurtBox.healthComponent.body.inventory;
                if (inventory == null)
                {
                    return false;
                }
                foreach (ItemIndex itemIndex in inventory.itemAcquisitionOrder)
                {
                    int permanentItemCount = inventory.GetItemCountPermanent(itemIndex);
                    if (permanentItemCount == 0)
                    {
                        continue;
                    }
                    ItemDef itemDef = ItemCatalog.GetItemDef(itemIndex);
                    if (itemDef == null)
                    {
                        continue;
                    }
                    bool itemIsBlacklisted = !itemDef.canRemove || itemDef.ContainsTag(ItemTag.ExtractorUnitBlacklist) || itemDef.ContainsTag(ItemTag.AIBlacklist);
                    if (!itemIsBlacklisted)
                    {
                        return true;
                    }
                }
                return false;
            }
        }
    }
}
