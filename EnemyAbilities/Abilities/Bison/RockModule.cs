using EntityStates;
using RoR2.CharacterAI;
using R2API;
using RoR2;
using RoR2.Skills;
using RoR2BepInExPack.GameAssetPaths.Version_1_39_0;
using UnityEngine;
using UnityEngine.AddressableAssets;
using RoR2.Projectile;
using Rewired.ComponentControls.Effects;
using System.Linq;
using static R2API.DamageAPI;
using BepInEx.Configuration;
using System.Collections.Generic;
using JetBrains.Annotations;

namespace EnemyAbilities.Abilities.Bison
{
    [EnemyAbilities.ModuleInfo("Unearth Boulder", "Gives Bison a new Secondary\n- Unearth Boulder: The Bison Unearths a large boulder nearby. When destroyed, the boulder explodes in a wide radius and launches 3 mini-boulders in a spread towards the nearest enemy of it's killer. Melee attacks from Bison break the boulder instantly. Activating this module causes Charge to activate from a longer range, and changes the max health damage needed to stun a Bison from 15% -> 30% to match Beetle Guards and Stone Golems.", "Bighorn Bison", true)]

    public class RockModule : BaseModule
    {
        private static GameObject bodyPrefab = Addressables.LoadAssetAsync<GameObject>(RoR2_Base_Bison.BisonBody_prefab).WaitForCompletion();
        private static GameObject masterPrefab = Addressables.LoadAssetAsync<GameObject>(RoR2_Base_Bison.BisonMaster_prefab).WaitForCompletion();
        public static GameObject rockProjectile = Addressables.LoadAssetAsync<GameObject>(RoR2_Base_Grandparent.GrandparentBoulder_prefab).WaitForCompletion().InstantiateClone("bisonMegaBoulder");
        public static GameObject rockGhost = Addressables.LoadAssetAsync<GameObject>(RoR2_Base_Grandparent.GrandparentBoulderGhost_prefab).WaitForCompletion().InstantiateClone("bisonMegaBoulderGhost");
        public static GameObject miniRockProjectile = Addressables.LoadAssetAsync<GameObject>(RoR2_Base_Grandparent.GrandparentBoulder_prefab).WaitForCompletion().InstantiateClone("bisonMiniBoulder");
        public static GameObject miniRockGhost = Addressables.LoadAssetAsync<GameObject>(RoR2_Base_Grandparent.GrandparentBoulderGhost_prefab).WaitForCompletion().InstantiateClone("bisonMiniBoulderGhost");
        public static ModdedDamageType rockDamageType;
        private static CharacterSpawnCard cscBison = Addressables.LoadAssetAsync<CharacterSpawnCard>(RoR2_Base_Bison.cscBison_asset).WaitForCompletion();

        internal static ConfigEntry<float> rockCount;
        internal static ConfigEntry<float> rockChildDamageCoeff;
        internal static ConfigEntry<float> rockExplosionCoeff;
        internal static ConfigEntry<float> rockTravelTime;  
        internal static ConfigEntry<float> rockExplosionRadius;
        internal static ConfigEntry<float> rockChildExplosionRadius;
        internal static ConfigEntry<float> rockMaxHealth;
        internal static ConfigEntry<float> rockCooldown;
        internal static ConfigEntry<float> rockSpreadAngle;
        public override void RegisterConfig()
        {
            base.RegisterConfig();
            rockCount = BindFloat("Rock Count", 3f, "The number of rocks spawned.", 1f, 5f, 1f);
            rockChildDamageCoeff = BindFloat("Rock Fragment Damage Coefficient", 300f, "Damage coeff of the mini-boulders that get launched", 100f, 1000f, 5f, PluginConfig.FormatType.Percentage);
            rockExplosionCoeff = BindFloat("Rock Explosion Damage Coefficient", 400f, "Damage coeff of the explosion when the big rock is killed", 100f, 1000f, 5f, PluginConfig.FormatType.Percentage);
            rockTravelTime = BindFloat("Rock Time to Target", 1.25f, "Duration of time between the rock being destroyed and the mini rock(s) reaching it's target. Proportional to arc height", 0.5f, 5f, 0.01f, PluginConfig.FormatType.Time);
            rockExplosionRadius = BindFloat("Rock Explosion Radius", 16f, "Explosion radius of the big rock", 8f, 20f, 1f, PluginConfig.FormatType.Distance);
            rockChildExplosionRadius = BindFloat("Rock Fragment Explosion Radius", 8f, "Explosion radius of the rock fragments", 5f, 12f, 1f, PluginConfig.FormatType.Distance);
            rockCooldown = BindFloat("Rock Cooldown", 15f, "Cooldown before the ability can be activated again", 5f, 30f, 0.1f, PluginConfig.FormatType.Time);
            rockMaxHealth = BindFloat("Rock Health", 400f, "Max health of the rock at Level 1. Gains 30% of this value per level", 100f, 600f, 10f, PluginConfig.FormatType.None);
            rockSpreadAngle = BindFloat("Rock Spread Angle", 22f, "The total spread angle of rocks beyond 1 launched by this ability.", 10f, 50f, 1f, PluginConfig.FormatType.None);
            BindStats(bodyPrefab, [cscBison]);
        }
        public override void Initialise()
        {
            base.Initialise();
            CreateSkill();
            ModifyProjectiles();
            rockDamageType = ReserveDamageType();
            bodyPrefab.GetComponent<SetStateOnHurt>().hitThreshold = 0.3f;
            On.RoR2.HealthComponent.TakeDamageProcess += ModifyBisonDamageAgainstRock;
            GlobalEventManager.onCharacterDeathGlobal += OnCharacterDeath;
            Utils.AddHealthOverride((originalMaxHealth, body) =>
            {
                if (body == null || body.master != null)
                {
                    return originalMaxHealth;
                }
                ProjectileMegaBisonRock rockComponent = body.gameObject.GetComponent<ProjectileMegaBisonRock>();
                if (rockComponent == null)
                {
                    return originalMaxHealth;
                }
                float healthPerLevel = rockMaxHealth.Value * 0.3f;
                float ambientLevel = Run.instance.ambientLevel;
                float newMaxHealth = rockMaxHealth.Value + (healthPerLevel * (ambientLevel - 1));
                return newMaxHealth;
            });
        }
        private void OnCharacterDeath(DamageReport report)
        {
            if (report == null)
            {
                return;
            }
            if (report.victim == null)
            {
                return;
            }
            ProjectileMegaBisonRock megaRockComponent = report.victim.GetComponent<ProjectileMegaBisonRock>();
            if (megaRockComponent != null)
            {
                megaRockComponent.OnDeath(report);
            }
        }
        private void ModifyBisonDamageAgainstRock(On.RoR2.HealthComponent.orig_TakeDamageProcess orig, HealthComponent self, DamageInfo damageInfo)
        {
            if (self.GetComponent<ProjectileMegaBisonRock>() != null)
            {
                if (damageInfo.attacker != null)
                {
                    CharacterBody attackerBody = damageInfo.attacker.GetComponent<CharacterBody>();
                    if (attackerBody.bodyIndex == RoR2Content.BodyPrefabs.BisonBody.bodyIndex)
                    {
                        damageInfo.damage = self.fullHealth;
                        damageInfo.procCoefficient = 0f;
                    }
                    //boulders don't damage boulders, avoids chain-reactions
                    if (damageInfo.damageType.HasModdedDamageType(rockDamageType))
                    {
                        damageInfo.damage = 0f;
                        damageInfo.procCoefficient = 0f;
                    }
                }
            }
            orig(self, damageInfo);
        }
        private void CreateSkill()
        {
            SkillDefData skillData = new SkillDefData
            {
                objectName = "BisonBodySpawnRock",
                skillName = "BisonSpawnRock",
                esmName = "Body",
                activationState = ContentAddition.AddEntityState<SpawnRock>(out _),
                cooldown = rockCooldown.Value,
                combatSkill = true
            };
            SkillDef spawnRock = CreateSkillDef<SpawnRockSkillDef>(skillData);

            CreateGenericSkill(bodyPrefab, skillData.skillName, "BisonSecondaryFamily", spawnRock, SkillSlot.Secondary);

            AISkillDriverData driverData = new AISkillDriverData
            {
                masterPrefab = masterPrefab,
                customName = "useSecondary",
                skillSlot = SkillSlot.Secondary,
                requiredSkillDef = spawnRock,
                requireReady = true,
                minDistance = 10f,
                maxDistance = 80f,
                selectionRequiresOnGround = true,
                aimType = AISkillDriver.AimType.AtCurrentEnemy,
                movementType = AISkillDriver.MovementType.ChaseMoveTarget,
                targetType = AISkillDriver.TargetType.CurrentEnemy,
                desiredIndex = 1
            };
            CreateAISkillDriver(driverData);
            AISkillDriver chargeDriver = masterPrefab.GetComponents<AISkillDriver>().Where(driver => driver.skillSlot == SkillSlot.Utility).FirstOrDefault();
            if (chargeDriver != null)
            {
                chargeDriver.maxDistance = 80f;
            }
            else
            {
                Log.Error($"Could not find charge driver!");
            }
        }
        private void ModifyProjectiles()
        {

            ProjectileController controller = rockProjectile.GetComponent<ProjectileController>();
            controller.flightSoundLoop = null;
            controller.ghostPrefab = rockGhost;
            rockGhost.GetComponent<Transform>().localScale = Vector3.one * 0.75f;
            ProjectileImpactExplosion impact = rockProjectile.GetComponent<ProjectileImpactExplosion>();
            DestroyImmediate(impact);
            ProjectileSimple simple = rockProjectile.GetComponent<ProjectileSimple>();
            simple.lifetime = 99f;
            simple.desiredForwardSpeed = 0f;

            Rigidbody rigid = rockProjectile.GetComponent<Rigidbody>();
            rigid.mass = 9999f;

            SphereCollider projectileCollider = rockProjectile.GetComponent<SphereCollider>();
            projectileCollider.radius = 1.5f;

            GameObject childGameObject = new GameObject();
            childGameObject.transform.SetParent(rockProjectile.transform);
            Transform childTransform = childGameObject.transform;

            ModelLocator modelLocator = rockProjectile.AddComponent<ModelLocator>();
            modelLocator.modelTransform = childTransform;

            modelLocator.autoUpdateModelTransform = true;
            modelLocator.dontDetatchFromParent = true;
            modelLocator.noCorpse = true;
            modelLocator.dontReleaseModelOnDeath = false;
            modelLocator.normalizeToFloor = false;
            modelLocator.normalSmoothdampTime = 0.1f;
            modelLocator.normalMaxAngleDelta = 90f;

            TeamComponent teamComponent = rockProjectile.AddComponent<TeamComponent>();
            teamComponent.hideAllyCardDisplay = true;
            teamComponent.teamIndex = TeamIndex.Neutral;

            LanguageAPI.Add("SKELETOGNE_BISONROCK_BODY_NAME", "Bison Boulder");
            CharacterBody characterBody = rockProjectile.AddComponent<CharacterBody>();
            characterBody.baseVisionDistance = Mathf.Infinity;
            characterBody.sprintingSpeedMultiplier = 1.45f;
            characterBody.hullClassification = HullClassification.Human;
            characterBody.baseMaxHealth = rockMaxHealth.Value;
            characterBody.levelMaxHealth = rockMaxHealth.Value * 0.3f; //doesn't actually scale with level properly due to being spawned on neutral team, handled by IL hook
            characterBody.SetSpreadBloom(0f);
            characterBody.bodyFlags |= CharacterBody.BodyFlags.Ungrabbable;
            characterBody.baseNameToken = "SKELETOGNE_BISONROCK_BODY_NAME";

            HealthComponent healthComponent = rockProjectile.AddComponent<HealthComponent>();
            healthComponent.body = characterBody;
            healthComponent.dontShowHealthbar = false;
            healthComponent.globalDeathEventChanceCoefficient = 1f;

            //explode.projectileHealthComponent = healthComponent;

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
                Log.Error($"hurtBox is null");
            }
            if (hurtBox.gameObject == null)
            {
                Log.Error($"hurtBox.gameObject == null");
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

            DisableCollisionsBetweenColliders dcbc = rockProjectile.AddComponent<DisableCollisionsBetweenColliders>();
            dcbc.collidersA = [rockProjectile.GetComponent<SphereCollider>()];
            dcbc.collidersB = [collider];

            ProjectileMegaBisonRock stick = rockProjectile.AddComponent<ProjectileMegaBisonRock>();

            ProjectileImpactExplosion miniRockExplosion = miniRockProjectile.GetComponent<ProjectileImpactExplosion>();
            miniRockExplosion.fireChildren = false;
            miniRockExplosion.childrenCount = 0;
            miniRockExplosion.blastRadius = rockChildExplosionRadius.Value;
            ProjectileController miniRockController = miniRockProjectile.GetComponent<ProjectileController>();
            miniRockController.ghostPrefab = miniRockGhost;
            miniRockGhost.GetComponent<Transform>().localScale = Vector3.one * 0.4f;
            SphereCollider miniRockCollider = miniRockProjectile.GetComponent<SphereCollider>();
            miniRockCollider.radius = 0.75f;

            Transform[] transformList = miniRockGhost.GetComponentsInChildren<Transform>();
            foreach (Transform t in transformList)
            {
                if (t.gameObject.name == "Rotator")
                {
                    SetRandomRotation randomRotation = t.gameObject.AddComponent<SetRandomRotation>();
                    randomRotation.setRandomXRotation = true;
                    randomRotation.setRandomYRotation = true;
                    randomRotation.setRandomZRotation = true;
                }
            }



        }
        public static bool TryGetRockSpawnPosition(CharacterBody bisonBody, out Vector3 rockSpawnPosition)
        {
            rockSpawnPosition = Vector3.zero;
            if (bisonBody == null)
            {
                return false;
            }
            Vector3 bisonPosition = bisonBody.transform.position;
            Vector3 bisonForward = bisonBody.transform.forward;
            bisonForward.y = 0f;
            bisonForward.Normalize();
            CharacterMaster master = bisonBody.master;
            if (master != null)
            {
                BaseAI baseAI = master.gameObject.GetComponent<BaseAI>();
                if (baseAI != null && baseAI.currentEnemy != null && baseAI.currentEnemy.characterBody != null)
                {
                    Vector3 targetPosition = baseAI.currentEnemy.characterBody.transform.position;
                    Vector3 direction = targetPosition - bisonPosition;
                    direction.y = 0f;
                    bisonForward = direction.normalized;
                }
            }
            Vector3 raycastStartPosition = (bisonBody.transform.position + bisonForward * SpawnRock.rockSpawnDistance) + new Vector3(0f, 15f, 0f);
            bool success = Physics.Raycast(raycastStartPosition, Vector3.down, out RaycastHit hit, 30f, LayerIndex.world.mask, QueryTriggerInteraction.Ignore);
            if (success)
            {
                rockSpawnPosition = hit.point + new Vector3(0f, 2f, 0f);
                return true;
            }
            else
            {
                return false;
            }
        }
        public class SpawnRockSkillDef : SkillDef
        {
            private static float minDistanceFromAnotherRock = 5f;
            public override bool IsReady([NotNull] GenericSkill skillSlot)
            {
                CharacterBody body = skillSlot.characterBody;
                if (body == null)
                {
                    return false;
                }
                bool foundPosition = TryGetRockSpawnPosition(body, out Vector3 rockSpawnPosition);
                if (foundPosition == true)
                {
                    List<ProjectileMegaBisonRock> rocks = InstanceTracker.GetInstancesList<ProjectileMegaBisonRock>();
                    foreach (ProjectileMegaBisonRock rock in rocks)
                    {
                        Vector3 rockPosition = rock.transform.position;
                        float distance = Vector3.Distance(rockSpawnPosition, rockPosition);
                        if (distance < minDistanceFromAnotherRock)
                        {
                            return false;
                        }
                    }
                    return base.IsReady(skillSlot);
                }
                else
                {
                    return false;
                }
            }
        }
        public class SpawnRock : BaseSkillState
        {
            private static float baseDuration = 0.25f;
            private float duration;
            public static float rockSpawnDistance = 10f;
            private static GameObject projectilePrefab = RockModule.rockProjectile;

            public override void OnEnter()
            {
                base.OnEnter();
                duration = baseDuration / attackSpeedStat;
                bool foundPosition = TryGetRockSpawnPosition(characterBody, out Vector3 rockSpawnPosition);
                if (foundPosition && base.isAuthority)
                {
                    ProjectileManager.instance.FireProjectile(projectilePrefab, rockSpawnPosition, Util.QuaternionSafeLookRotation(UnityEngine.Random.onUnitSphere), characterBody.gameObject, 0f, 0f, false, DamageColorIndex.Default, null, 0f, DamageType.Generic);
                }
            }
            public override void OnExit()
            {
                base.OnExit();
            }
            public override void FixedUpdate()
            {
                base.FixedUpdate();
                if (base.fixedAge > duration)
                {
                    outer.SetNextStateToMain();
                }
            }
        }
        public class ProjectileMegaBisonRock : MonoBehaviour, IProjectileImpactBehavior
        {
            private Rigidbody rigidbody;
            public HealthComponent healthComponent;
            private bool hit;
            private GameObject impactEffect = Addressables.LoadAssetAsync<GameObject>(RoR2_Base_BeetleQueen.BeetleQueenDeathImpact_prefab).WaitForCompletion();
            private GameObject explosionEffect = Addressables.LoadAssetAsync<GameObject>(RoR2_Base_Common_VFX.OmniExplosionVFX_prefab).WaitForCompletion();
            public int childCount = (int)rockCount.Value;
            private float maxSpreadAngle = rockSpreadAngle.Value;
            public void Start()
            {
                rigidbody = GetComponent<Rigidbody>();
                healthComponent = GetComponent<HealthComponent>();
            }
            public void OnEnable()
            {
                InstanceTracker.Add(this);
            }
            public void OnDisable()
            {
                InstanceTracker.Remove(this);
            }
            public void OnProjectileImpact(ProjectileImpactInfo info)
            {
                if (hit)
                {
                    return;
                }
                if (info.collider == null)
                {
                    return;
                }
                HurtBox hurtBox = info.collider.GetComponent<HurtBox>();
                if (hurtBox != null)
                {
                    return;
                }
                if (rigidbody == null)
                {
                    return;
                }
                hit = true;
                EffectManager.SpawnEffect(impactEffect, new EffectData { origin = gameObject.transform.position, rotation = Quaternion.identity, scale = 1f }, true);
                rigidbody.velocity = Vector3.zero;
                rigidbody.angularVelocity = Vector3.zero;
                rigidbody.constraints = RigidbodyConstraints.FreezeAll;
                Util.PlaySound("Play_titanboss_step", this.gameObject);

                ProjectileController controller = GetComponent<ProjectileController>();
                if (controller != null)
                {
                    RotateAroundAxis[] components = controller.ghost.GetComponentsInChildren<RotateAroundAxis>();
                    foreach (RotateAroundAxis component in components)
                    {
                        if (component != null)
                        {
                            component.enabled = false;
                        }
                    }
                }
            }
            public void OnDeath(DamageReport report)
            {
                //if there's no killer, it creates an explosion effect but deals no damage and does not launch mini-boulders
                Detonate(report);
                Destroy(this.gameObject);
                EffectManager.SpawnEffect(explosionEffect, new EffectData { origin = gameObject.transform.position, rotation = Quaternion .identity, scale = rockExplosionRadius.Value }, true);
            }
            private void Detonate(DamageReport report)
            {

                CharacterBody attackerBody = report.attackerBody;
                if (attackerBody == null)
                {
                    return;
                }
                TeamComponent teamComponent = attackerBody.gameObject.GetComponent<TeamComponent>();
                if (teamComponent == null)
                {
                    return;
                }
                InputBankTest inputBank = attackerBody.inputBank;
                if (inputBank == null)
                {
                    return;
                }
                Vector3 origin = gameObject.transform.position;
                Vector3 direction = inputBank.aimDirection;
                TeamMask teamMask = TeamMask.GetEnemyTeams(teamComponent.teamIndex);
                teamMask.RemoveTeam(TeamIndex.Neutral);
                BullseyeSearch search = new BullseyeSearch();
                search.searchOrigin = origin;
                search.searchDirection = direction;
                search.maxAngleFilter = 60f;
                search.maxDistanceFilter = 100f;
                search.teamMaskFilter = teamMask;
                search.sortMode = BullseyeSearch.SortMode.Distance;
                search.RefreshCandidates();
                List<HurtBox> results = search.GetResults().ToList();
                HurtBox targetHurtBox = results.Where(hurtBox => hurtBox != null && hurtBox.healthComponent != null && hurtBox.healthComponent.body != null).FirstOrDefault();
                bool noTarget = false;
                if (targetHurtBox == null || targetHurtBox.healthComponent == null || targetHurtBox.healthComponent.body == null)
                {
                    noTarget = true;
                }
                Vector3 projectileDirection = (direction.normalized + Vector3.up).normalized;
                float magnitude = 40f;
                if (!noTarget)
                {
                    Vector3 targetPosition = targetHurtBox.healthComponent.body.transform.position;
                    Vector3 startVelocity = Trajectory.CalculateInitialVelocityFromTime(gameObject.transform.position, targetPosition, rockTravelTime.Value);
                    magnitude = startVelocity.magnitude;
                    projectileDirection = startVelocity.normalized;
                }
                DamageTypeCombo combo = new DamageTypeCombo { damageType = DamageType.Generic, damageSource = DamageSource.Secondary };
                combo.AddModdedDamageType(rockDamageType);
                for (int i = 0; i < childCount; i++)
                {
                    float totalSpread = maxSpreadAngle * 2f;
                    float spreadInterval = childCount > 1 ? totalSpread / (childCount - 1) : 0f;
                    float startSpread = childCount > 1 ? -maxSpreadAngle : 0f;
                    float currentSpread = startSpread + spreadInterval * i;
                    Vector3 updatedDirection = Util.ApplySpread(projectileDirection, 0f, 0f, 1f, 1f, currentSpread);
                    ProjectileManager.instance.FireProjectile(miniRockProjectile, transform.position + new Vector3(0f, 1f, 0f), Util.QuaternionSafeLookRotation(updatedDirection), attackerBody.gameObject, (rockChildDamageCoeff.Value / 100f) * attackerBody.damage, 1000f, attackerBody.RollCrit(), DamageColorIndex.Default, null, magnitude, combo);
                }
                BlastAttack blastAttack = new BlastAttack();
                blastAttack.position = transform.position;
                blastAttack.radius = rockExplosionRadius.Value;
                blastAttack.attacker = attackerBody.gameObject;
                blastAttack.crit = attackerBody.RollCrit();
                blastAttack.procCoefficient = 1f;
                blastAttack.attackerFiltering = AttackerFiltering.NeverHitSelf;
                blastAttack.baseDamage = (rockExplosionCoeff.Value / 100f) * attackerBody.damage;
                blastAttack.baseForce = 2000f;
                blastAttack.bonusForce = new Vector3(0f, 1000f, 0f);
                blastAttack.damageColorIndex = DamageColorIndex.Default;
                blastAttack.damageType = combo;
                blastAttack.falloffModel = BlastAttack.FalloffModel.SweetSpot;
                blastAttack.procChainMask = default(ProcChainMask);
                blastAttack.teamIndex = teamComponent.teamIndex;
                blastAttack.Fire();
                Destroy(this.gameObject);
            }
        }
    }
}
