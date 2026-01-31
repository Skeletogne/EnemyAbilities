using System;
using System.Collections.Generic;
using EntityStates;
using RoR2.CharacterAI;
using JetBrains.Annotations;
using R2API;
using RoR2;
using RoR2.Skills;
using RoR2BepInExPack.GameAssetPaths.Version_1_35_0;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Networking;
using RoR2.Projectile;
using Rewired.ComponentControls.Effects;
using System.Linq;
using HarmonyLib;
using MonoMod.Cil;
using System.Reflection;
using Mono.Cecil.Cil;
using static EnemyAbilities.PluginConfig;

namespace EnemyAbilities.Abilities.Gravekeeper
{

    [EnemyAbilities.ModuleInfo("Mass Resurrect", "Gives Grovetenders a new Special Ability:\n- Mass Resurrect: Grovetenders passively collect Gravestones from nearby fallen enemies. When used, Grovetenders fire the Gravestones at the player, which detonate after a short delay, spawning a ghostly version of the enemy killed. Ghosts provide armour to their Grovetender, and slowly decay over time.", "Grovetender", true)]
    public class MassResurrectModule : BaseModule
    {
        private static GameObject bodyPrefab = Addressables.LoadAssetAsync<GameObject>(RoR2_Base_Gravekeeper.GravekeeperBody_prefab).WaitForCompletion();
        private static GameObject masterPrefab = Addressables.LoadAssetAsync<GameObject>(RoR2_Base_Gravekeeper.GravekeeperMaster_prefab).WaitForCompletion();
        public static GameObject gravestonePrefab = Addressables.LoadAssetAsync<GameObject>(RoR2_Base_wispgraveyard.WPObeliskSmall_prefab).WaitForCompletion().InstantiateClone("gravestonePrefab");
        public static GameObject gravestoneProjectile = Addressables.LoadAssetAsync<GameObject>(RoR2_DLC1_ClayGrenadier.ClayGrenadierBarrelProjectile_prefab).WaitForCompletion().InstantiateClone("gravestoneProjectilePrefab");
        public static GameObject gravestoneProjectileGhost = Addressables.LoadAssetAsync<GameObject>(RoR2_DLC1_ClayGrenadier.ClayGrenadierBarrelGhost_prefab).WaitForCompletion().InstantiateClone("gravestoneProjectileGhostPrefab");
        public static GameObject gravestoneModel = Addressables.LoadAssetAsync<GameObject>(RoR2_Base_Common_Props.mdlIcoRockM_fbx).WaitForCompletion();
        public static Material gravestoneMaterial = Addressables.LoadAssetAsync<Material>(RoR2_Base_wispgraveyard.matTempleObelisk_mat).WaitForCompletion();
        public static Material chainMaterial = Addressables.LoadAssetAsync<Material>(RoR2_Base_Gravekeeper.matGravekeeperHookChain_mat).WaitForCompletion();
        public static GameObject tetherPrefab = Addressables.LoadAssetAsync<GameObject>(RoR2_Base_moon2.BloodSiphonTetherVFX_prefab).WaitForCompletion();
        public static GameObject muzzleFlashWinch = Addressables.LoadAssetAsync<GameObject>(RoR2_Base_Gravekeeper.MuzzleflashWinch_prefab).WaitForCompletion();

        public static ItemDef GravekeeperGhostItem;
        public static BuffDef GravekeeperArmorBuff;

        public override void Awake()
        {
            base.Awake();
            CreateSkill();
            CreateGhostItem();
            CreateGravekeeperBuff();
            SetUpTetherPrefab();
            SetUpProjectile();
            GlobalEventManager.onCharacterDeathGlobal += CheckToSpawnGravestone;
            bodyPrefab.AddComponent<GravekeeperResurrectController>();
            R2API.RecalculateStatsAPI.GetStatCoefficients += RecalculateStatsAPI_GetStatCoefficients;
        }

        private void RecalculateStatsAPI_GetStatCoefficients(CharacterBody sender, RecalculateStatsAPI.StatHookEventArgs args)
        {
            if (sender.HasBuff(GravekeeperArmorBuff))
            {
                int count = sender.GetBuffCount(GravekeeperArmorBuff);
                if (count > 0)
                {
                    args.armorAdd += grovetenderResurrectArmorPerGhost.Value * count;
                }
            }
        }

        private void CreateGravekeeperBuff()
        {
            GravekeeperArmorBuff = ScriptableObject.CreateInstance<BuffDef>();
            GravekeeperArmorBuff.canStack = true;
            GravekeeperArmorBuff.isHidden = true;
            ContentAddition.AddBuffDef(GravekeeperArmorBuff);

            IL.RoR2.CharacterBody.UpdateAllTemporaryVisualEffects += (il) =>
            {
                ILCursor c1 = new ILCursor(il);
                if (c1.TryGotoNext(
                    x => x.MatchLdsfld(typeof(RoR2Content.Buffs), nameof(RoR2Content.Buffs.LunarShell)),
                    x => x.MatchCallOrCallvirt<CharacterBody>(nameof(CharacterBody.HasBuff)),
                    x => x.MatchLdstr("")
                ))
                {
                    c1.Index += 2;
                    c1.Emit(OpCodes.Ldarg_0);
                    c1.EmitDelegate<Func<bool, CharacterBody, bool>>((originalValue, body) =>
                    {
                        if (body != null)
                        {
                            if (body.HasBuff(GravekeeperArmorBuff))
                            {
                                return true;
                            }
                        }
                        return originalValue;
                    });
                }
                else
                {
                    Log.Error($"CharacterBody.UpdateAllTemporaryVisualEffects cursor c1 failed to match! Grovetender armor buff VFX affected.");
                }
            };
        }
        private void SetUpTetherPrefab()
        {
            LineRenderer lineRenderer = tetherPrefab.GetComponent<LineRenderer>();
            Material chainMaterialClone = new Material(chainMaterial);
            chainMaterialClone.mainTextureScale = Vector2.one;
            lineRenderer.sharedMaterial = chainMaterialClone;
        }
        private void CreateGhostItem()
        {
            GravekeeperGhostItem = ScriptableObject.CreateInstance<ItemDef>();
            (GravekeeperGhostItem as ScriptableObject).name = "GravekeeperGhostItem";
            GravekeeperGhostItem.tier = ItemTier.NoTier;
#pragma warning disable CS0618 // Type or member is obsolete
            GravekeeperGhostItem.deprecatedTier = ItemTier.NoTier;
#pragma warning restore CS0618 // Type or member is obsolete
            GravekeeperGhostItem.nameToken = "ITEM_SKELETOGNE_ENEMYGHOST_NAME";
            GravekeeperGhostItem.pickupToken = "ITEM_SKELETOGNE_ENEMYGHOST_PICKUP";
            GravekeeperGhostItem.descriptionToken = "ITEM_SKELETOGNE_ENEMYGHOST_DESC";
            GravekeeperGhostItem.loreToken = "ITEM_SKELETOGNE_ENEMYGHOST_LORE";
            GravekeeperGhostItem.pickupIconSprite = null;
            GravekeeperGhostItem.hidden = true;

            ContentAddition.AddItemDef(GravekeeperGhostItem);
            IL.RoR2.CharacterModel.UpdateOverlays += (il) =>
            {
                ILCursor c1 = new ILCursor(il);
                MethodBase setIsGhost = AccessTools.PropertySetter(typeof(CharacterModel), nameof(CharacterModel.isGhost));
                if (c1.TryGotoNext(
                    x => x.MatchCallOrCallvirt(setIsGhost)
                ))
                {
                    c1.Emit(OpCodes.Ldarg_0);
                    c1.EmitDelegate<Func<int, CharacterModel, int>>((originalValue, model) =>
                    {
                        if (model != null && model.body != null)
                        {
                            if (model.body.inventory != null && model.body.inventory.GetItemCountPermanent(GravekeeperGhostItem) > 0)
                            {
                                return 1;
                            }
                        }
                        return originalValue;
                    });
                }
                else
                {
                    Log.Error($"IL.RoR2.CharacterModel.UpdateOverlays cursor c1 failed to match! Happiest Mask visuals affected.");
                }
            };
            IL.RoR2.CharacterModel.UpdateOverlayStates += (il) =>
            {
                ILCursor c1 = new ILCursor(il);
                MethodBase setIsGhost = AccessTools.PropertySetter(typeof(CharacterModel), nameof(CharacterModel.isGhost));
                if (c1.TryGotoNext(
                    x => x.MatchCallOrCallvirt(setIsGhost)
                ))
                {
                    c1.Emit(OpCodes.Ldarg_0);
                    c1.EmitDelegate<Func<int, CharacterModel, int>>((originalValue, model) =>
                    {
                        if (model != null && model.body != null)
                        {
                            if (model.body.inventory != null && model.body.inventory.GetItemCountPermanent(GravekeeperGhostItem) > 0)
                            {
                                return 1;
                            }
                        }
                        return originalValue;
                    });
                }
                else
                {
                    Log.Error($"IL.RoR2.CharacterModel.UpdateOverlayStates cursor c1 failed to match! Happiest Mask visuals affected.");
                }
            };
        }
        private void CreateSkill()
        {
            ResurrectSkillDef massResurrect = ScriptableObject.CreateInstance<ResurrectSkillDef>();
            (massResurrect as ScriptableObject).name = "GravekeeperBodyMassResurrect";
            massResurrect.skillName = "GravekeeperMassResurrect";
            massResurrect.activationStateMachineName = "Weapon";
            massResurrect.activationState = ContentAddition.AddEntityState<MassResurrect>(out _);
            massResurrect.baseRechargeInterval = grovetenderResurrectCooldown.Value;
            massResurrect.canceledFromSprinting = false;
            massResurrect.isCombatSkill = true;
            ContentAddition.AddSkillDef(massResurrect);

            SkillFamily skillFamily = ScriptableObject.CreateInstance<SkillFamily>();
            (skillFamily as ScriptableObject).name = "GravekeeperSpecialFamily";
            skillFamily.variants = [new SkillFamily.Variant() { skillDef = massResurrect }];

            GenericSkill skill = bodyPrefab.AddComponent<GenericSkill>();
            skill.skillName = "GravekeeperMassResurrect";
            skill._skillFamily = skillFamily;

            SkillLocator locator = bodyPrefab.GetComponent<SkillLocator>();
            locator.special = skill;

            ContentAddition.AddSkillFamily(skillFamily);

            AISkillDriver useSpecial = masterPrefab.AddComponent<AISkillDriver>();
            useSpecial.customName = "useSpecial";
            useSpecial.skillSlot = SkillSlot.Special;
            useSpecial.requiredSkill = massResurrect;
            useSpecial.requireSkillReady = true;
            useSpecial.minDistance = 0f;
            useSpecial.maxDistance = 85f;
            useSpecial.maxUserHealthFraction = (grovetenderResurrectHealthThreshold.Value / 100f);
            useSpecial.selectionRequiresOnGround = true;
            useSpecial.moveTargetType = AISkillDriver.TargetType.CurrentEnemy;
            useSpecial.movementType = AISkillDriver.MovementType.Stop;
            masterPrefab.ReorderSkillDrivers(useSpecial, 0);
        }
        private void SetUpProjectile()
        {
            gravestonePrefab.AddComponent<FloatingGravestoneController>();
            Transform mainTransform = gravestonePrefab.GetComponent<Transform>();
            mainTransform.localScale = Vector3.one;
            Transform[] gravestoneTransforms = gravestonePrefab.GetComponentsInChildren<Transform>();
            for (int i = gravestoneTransforms.Length - 1; i >= 0; i--)
            {
                if (gravestoneTransforms[i].gameObject.name == "FSRuinRingCollision" || gravestoneTransforms[i].name == "DirtMesh")
                {
                    DestroyImmediate(gravestoneTransforms[i].gameObject);
                }
            }
            MeshCollider[] meshColliders = gravestonePrefab.GetComponentsInChildren<MeshCollider>();
            for (int j = meshColliders.Length - 1; j >= 0; j--)
            {
                DestroyImmediate(meshColliders[j]);
            }
            ParticleSystem[] particleSystems = gravestoneProjectileGhost.GetComponentsInChildren<ParticleSystem>();
            for (int k = particleSystems.Length - 1; k >= 0; k--)
            {
                DestroyImmediate(particleSystems[k]);
            }
            ParticleSystemRenderer[] renderers = gravestoneProjectileGhost.GetComponentsInChildren<ParticleSystemRenderer>();
            for (int m = renderers.Length - 1; m >= 0; m--)
            {
                DestroyImmediate(renderers[m]);
            }
            RotateAroundAxis axis = gravestoneProjectileGhost.GetComponentInChildren<RotateAroundAxis>();
            DestroyImmediate(axis);
            MeshFilter modelMeshFilter = gravestoneModel.GetComponent<MeshFilter>();
            MeshFilter ghostMeshFilter = gravestoneProjectileGhost.GetComponentInChildren<MeshFilter>();
            ghostMeshFilter.sharedMesh = modelMeshFilter.sharedMesh;
            ghostMeshFilter.mesh = modelMeshFilter.mesh;
            MeshRenderer ghostMeshRenderer = gravestoneProjectileGhost.GetComponentInChildren<MeshRenderer>();
            ghostMeshRenderer.material = gravestoneMaterial;
            ghostMeshRenderer.sharedMaterial = gravestoneMaterial;
            Transform meshFilterTransform = ghostMeshFilter.gameObject.GetComponent<Transform>();
            meshFilterTransform.localScale = new Vector3(0.85f, 0.41f, 0.37f);
            meshFilterTransform.localRotation = Quaternion.Euler(0f, 90f, 0f);
            ApplyTorqueOnStart torque = gravestoneProjectile.GetComponent<ApplyTorqueOnStart>();
            DestroyImmediate(torque);

            ProjectileController controller = gravestoneProjectile.GetComponent<ProjectileController>();
            controller.ghostPrefab = gravestoneProjectileGhost;
            ProjectileImpactExplosion impact = gravestoneProjectile.GetComponent<ProjectileImpactExplosion>();
            impact.gameObject.layer = LayerIndex.projectileWorldOnly.intVal;
            impact.lifetime = 10f;
            impact.destroyOnDistance = true;
            impact.destroyOnWorld = false;
            impact.destroyOnEnemy = false;
            impact.impactOnWorld = true;
            impact.timerAfterImpact = true;
            impact.lifetimeAfterImpact = grovetenderResurrectGravestoneFuse.Value;
            impact.impactEffect = Addressables.LoadAssetAsync<GameObject>(RoR2_Base_LemurianBruiser.OmniExplosionVFXLemurianBruiserFireballImpact_prefab).WaitForCompletion();
            impact.blastRadius = grovetenderResurrectGravestoneRadius.Value;
            impact.maxDistance = 125f;
            ProjectileSimple simple = gravestoneProjectile.GetComponent<ProjectileSimple>();
            simple.lifetime = 10f;
            Rigidbody rigidbody = gravestoneProjectile.GetComponent<Rigidbody>();
            rigidbody.useGravity = true;
            ProjectileStickOnImpact stick = gravestoneProjectile.AddComponent<ProjectileStickOnImpact>();
            stick.alignNormals = true;
            stick.ignoreCharacters = true;
            stick.ignoreSteepSlopes = false;
            stick.ignoreWorld = false;
            ProjectileDamage damage = gravestoneProjectile.GetComponent<ProjectileDamage>();
            damage.damageType = DamageTypeCombo.Generic;
            gravestoneProjectile.AddComponent<ProjectileGravestone>();
        }
        public class ResurrectSkillDef : SkillDef
        {
            public override BaseSkillInstanceData OnAssigned([NotNull] GenericSkill skillSlot)
            {
                return base.OnAssigned(skillSlot);
            }
            public override bool IsReady([NotNull] GenericSkill skillSlot)
            {
                return base.IsReady(skillSlot); 
            }
        }
        public class MassResurrect : BaseSkillState
        {
            public static float baseDuration = 5f;
            private float duration;
            private static float basePrepDuration = 1f;
            private float prepDuration = 1f;
            private static float baseFireDuration = 3f;
            private float fireDuration;
            private GravekeeperResurrectController resurrectController;
            private int gravestoneCount;
            private int currentCount;
            private float nextFireTime = 0f;
            private float fireInterval;
            private static float baseRecoveryDuration = 1f;
            private float recoveryDuration;
            private bool firing;
            public override void OnEnter()
            {
                base.OnEnter();
                duration = baseDuration;
                resurrectController = characterBody.gameObject.GetComponent<GravekeeperResurrectController>();
                resurrectController.gravestoneMode = GravekeeperResurrectController.GravestoneMode.Firing;
                resurrectController.prepStopwatch = 0f;
                prepDuration = basePrepDuration / attackSpeedStat;
                fireDuration = baseFireDuration / attackSpeedStat;
                recoveryDuration = baseRecoveryDuration / attackSpeedStat;
                if (resurrectController.gravestoneList.Count == 0)
                {
                    for (int i = 0; i < 2; i++)
                    {
                        GameObject gravestoneInstance = Instantiate(MassResurrectModule.gravestonePrefab, characterBody.transform.position + new Vector3(0f, 8f, 0f), Quaternion.identity);
                        FloatingGravestoneController gravestoneController = gravestoneInstance.GetComponent<FloatingGravestoneController>();
                        gravestoneController.boundResurrectController = resurrectController;
                        gravestoneController.bodyIndex = BodyCatalog.FindBodyIndexCaseInsensitive("GreaterWispBody");
                        resurrectController.gravestoneList.Add(gravestoneController);
                    }
                }
                foreach (FloatingGravestoneController gravestone in resurrectController.gravestoneList)
                {
                    resurrectController.queuedBodyIndices.Add(gravestone.bodyIndex);
                }
            }
            public override void OnExit()
            {
                base.OnExit();
                List<FloatingGravestoneController> gravestones = resurrectController.gravestoneList;
                for (int i = gravestones.Count - 1; i >= 0; i--)
                {
                    Destroy(gravestones[i].gameObject);
                }
                resurrectController.gravestoneMode = GravekeeperResurrectController.GravestoneMode.Floating;
                resurrectController.prepStopwatch = 0f;
                resurrectController.queuedBodyIndices.Clear();
            }
            public override void FixedUpdate()
            {
                base.FixedUpdate();
                if (base.fixedAge > prepDuration && !firing)
                {
                    firing = true;
                    gravestoneCount = resurrectController.gravestoneList.Count;
                    fireInterval = fireDuration / gravestoneCount;
                    currentCount = 0;
                }
                if (firing && base.fixedAge > nextFireTime + prepDuration && currentCount < gravestoneCount)
                {
                    FloatingGravestoneController controller = resurrectController.gravestoneList[currentCount];
                    Vector3 projectilePosition = controller.gameObject.transform.position;
                    Vector3 targetPosition = resurrectController.ai.currentEnemy.characterBody.transform.position;
                    Vector3 direction = (targetPosition - projectilePosition).normalized;
                    Quaternion rotation = Util.QuaternionSafeLookRotation(direction + (UnityEngine.Random.insideUnitSphere * 0.05f));
                    DamageTypeCombo combo = new DamageTypeCombo { damageSource = DamageSource.Special, damageType = DamageType.Generic };
                    FireProjectileInfo info = new FireProjectileInfo
                    {
                        projectilePrefab = gravestoneProjectile,
                        crit = RollCrit(),
                        damage = (grovetenderResurrectGravestoneDamage.Value / 100f) * damageStat,
                        damageColorIndex = DamageColorIndex.Default,
                        damageTypeOverride = DamageTypeCombo.GenericSpecial,
                        force = 1500f,
                        maxDistance = 125f,
                        owner = characterBody.gameObject,
                        rotation = rotation,
                        speedOverride = 60f,
                        useSpeedOverride = true,
                        position = projectilePosition,
                    };
                    Util.PlaySound("Play_bellBody_attackShoot", controller.gameObject);
                    EffectManager.SpawnEffect(muzzleFlashWinch, new EffectData { rotation = rotation, origin = projectilePosition, scale = 1f }, true);
                    if (base.isAuthority)
                    {
                        ProjectileManager.instance.FireProjectile(info);
                    }
                    resurrectController.gravestoneList[currentCount].gameObject.GetComponentInChildren<MeshRenderer>().enabled = false;
                    nextFireTime += fireInterval;
                    currentCount++;
                }
                if (base.fixedAge > prepDuration + fireDuration + recoveryDuration && currentCount == gravestoneCount)
                {
                    outer.SetNextStateToMain();
                }
            }
            public override InterruptPriority GetMinimumInterruptPriority()
            {
                return InterruptPriority.PrioritySkill;
            }
        }
        private void CheckToSpawnGravestone(DamageReport damageReport)
        {
            if (damageReport == null)
            {
                return;
            }
            CharacterBody victimBody = damageReport.victimBody;
            if (victimBody == null || victimBody.master == null || victimBody.isChampion || victimBody.isPlayerControlled)
            {
                return;
            }
            List<GravekeeperResurrectController> controllers = InstanceTracker.GetInstancesList<GravekeeperResurrectController>();
            if (controllers.Count == 0)
            {
                return;
            }
            GravekeeperResurrectController highestPrioController = null;
            float highestPrioDistance = Mathf.Infinity;
            foreach (GravekeeperResurrectController controller in controllers)
            {
                if (controller == null || controller.body == null)
                {
                    continue;
                }
                if (controller.gravestoneList.Count >= (int)grovetenderResurrectMaxGravestones.Value)
                {
                    continue;
                }
                CharacterBody gravekeeperBody = controller.body;
                if (victimBody.transform == null || gravekeeperBody.transform == null)
                {
                    continue;
                }
                if (victimBody.inventory == null || victimBody.inventory.GetItemCountPermanent(GravekeeperGhostItem) > 0)
                {
                    continue;
                }
                float distance = Vector3.Distance(gravekeeperBody.transform.position, victimBody.transform.position);
                if (distance > 100f)
                {
                    continue;
                }
                if (gravekeeperBody.teamComponent == null || victimBody.teamComponent == null)
                {
                    continue;
                }
                if (gravekeeperBody.teamComponent.teamIndex != victimBody.teamComponent.teamIndex)
                {
                    continue;
                }
                EntityStateMachine weaponESM = gravekeeperBody.GetComponents<EntityStateMachine>().Where(esm => esm.customName == "Weapon").FirstOrDefault();
                if (weaponESM == null)
                {
                    continue;
                }
                if (weaponESM.state is MassResurrect)
                {
                    continue;
                }
                SkillLocator skillLocator = gravekeeperBody.skillLocator;
                if (skillLocator == null || skillLocator.special == null || skillLocator.special.cooldownRemaining > skillLocator.special.rechargeStopwatch)
                {
                    continue;
                }
                if (distance < highestPrioDistance)
                {
                    highestPrioController = controller;
                    highestPrioDistance = distance;
                }
            }
            if (highestPrioController == null)
            {
                return;
            }
            highestPrioController.TrySpawnGravestone(victimBody);
        }
    }
    public class GravekeeperResurrectController : MonoBehaviour
    {
        public CharacterBody body;
        public BaseAI ai;
        public List<FloatingGravestoneController> gravestoneList = new List<FloatingGravestoneController>();
        public float prepStopwatch;
        public List<BodyIndex> queuedBodyIndices = new List<BodyIndex>();
        public List<CharacterBody> boundGhosts;
        public TetherVfxOrigin tetherVfxOrigin;
        public enum GravestoneMode
        {
            None,
            Floating,
            Firing
        }
        public GravestoneMode gravestoneMode;
        public void Awake()
        {
            body = GetComponent<CharacterBody>();
            gravestoneMode = GravestoneMode.Floating;
            tetherVfxOrigin = body.healthComponent.gameObject.AddComponent<TetherVfxOrigin>();
            tetherVfxOrigin.transform = body.transform;
            tetherVfxOrigin.tetherPrefab = MassResurrectModule.tetherPrefab;
        }
        public void Start()
        {
            if (body == null || body.master == null)
            {
                return;
            }
            ai = body.master.gameObject.GetComponent<BaseAI>();
            if (ai == null)
            {
                return;
            }
        }
        public void OnEnable()
        {
            InstanceTracker.Add(this);
        }
        public void OnDisable()
        {
            InstanceTracker.Remove(this);
        }
        public void TrySpawnGravestone(CharacterBody victimBody)
        {
            GameObject gravestoneInstance = Instantiate(MassResurrectModule.gravestonePrefab, victimBody.corePosition, Quaternion.identity);
            FloatingGravestoneController gravestoneController = gravestoneInstance.GetComponent<FloatingGravestoneController>();
            gravestoneController.boundResurrectController = this;
            BodyIndex bodyIndex = victimBody.bodyIndex;
            if (grovetenderResurrectWispsOnly.Value == true)
            {
                HullClassification hull = victimBody.hullClassification;
                if (hull == HullClassification.Golem || hull == HullClassification.BeetleQueen)
                {
                    bodyIndex = BodyCatalog.FindBodyIndexCaseInsensitive("GreaterWispBody");
                }
                else
                {
                    bodyIndex = BodyCatalog.FindBodyIndexCaseInsensitive("WispBody");
                }
            }
            gravestoneController.bodyIndex = bodyIndex;
            gravestoneList.Add(gravestoneController);
        }
        public void FixedUpdate()
        {
            UpdateTethers();
            if (body == null || body.healthComponent == null || body.healthComponent.alive == false)
            {
                for (int i = boundGhosts.Count - 1; i >= 0; i--)
                {
                    CharacterBody ghostBody = boundGhosts[i];
                    if (ghostBody != null && ghostBody.healthComponent != null)
                    {
                        ghostBody.healthComponent.Suicide();
                    }
                }
                return;
            }
            if (gravestoneMode == GravestoneMode.Floating)
            {
                for (int i = 0; i < gravestoneList.Count; i++)
                {
                    gravestoneList[i].UpdatePositionFloating();
                }
            }
            if (gravestoneMode == GravestoneMode.Firing)
            {
                prepStopwatch += Time.fixedDeltaTime;
                for (int i = 0; i < gravestoneList.Count; i++)
                {
                    gravestoneList[i].UpdatePositionPreparingOrFiring(i, gravestoneList.Count, prepStopwatch);
                }
            }
        }
        public void UpdateTethers()
        {
            List<Transform> transformList = new List<Transform>();
            for (int i = boundGhosts.Count - 1; i >= 0; i--)
            {
                CharacterBody ghostBody = boundGhosts[i];
                if (ghostBody == null || ghostBody.healthComponent == null || ghostBody.healthComponent.alive == false)
                {
                    boundGhosts.RemoveAt(i);
                    continue;
                }
                if (Vector3.Distance(body.transform.position, ghostBody.transform.position) < 150f)
                {
                    transformList.Add(ghostBody.transform);
                }
            }
            tetherVfxOrigin.SetTetheredTransforms(transformList);
            body.SetBuffCount(MassResurrectModule.GravekeeperArmorBuff.buffIndex, transformList.Count);
        }
    }
    public class FloatingGravestoneController : MonoBehaviour
    {
        public BodyIndex bodyIndex;
        public GravekeeperResurrectController boundResurrectController;
        public float stopwatch;
        private float initialRotation;
        private float radius;
        private float height;
        private float rotationSpeed;
        public void Awake()
        {
            initialRotation = UnityEngine.Random.Range(0f, Mathf.PI * 2);
            radius = UnityEngine.Random.Range(8f, 16f);
            height = UnityEngine.Random.Range(8f, 16f);
            rotationSpeed = UnityEngine.Random.Range(Mathf.PI / 6, Mathf.PI / 3);
            rotationSpeed *= (2 * UnityEngine.Random.RandomRangeInt(0, 2)) - 1;

        }
        public void OnDestroy()
        {
            if (boundResurrectController != null)
            {
                boundResurrectController.gravestoneList.Remove(this);
            }
        }
        public void FixedUpdate()
        {
            stopwatch += Time.fixedDeltaTime;
            if (boundResurrectController == null || boundResurrectController.body == null)
            {
                Destroy(this.gameObject);
                return;
            }
            else if (boundResurrectController.body.healthComponent == null || boundResurrectController.body.healthComponent.alive == false)
            {
                Destroy(this.gameObject);
                return;
            }


        }
        public void UpdatePositionFloating()
        {
            Vector3 grovetenderPosition = boundResurrectController.body.transform.position;
            float xOffset = Mathf.Cos(initialRotation + stopwatch * rotationSpeed) * radius;
            float zOffset = Mathf.Sin(initialRotation + stopwatch * rotationSpeed) * radius;
            float yOffset = height;

            Vector3 targetPosition = grovetenderPosition + new Vector3(xOffset, yOffset, zOffset);
            Vector3 currentPosition = gameObject.transform.position;

            Vector3 movePosition = Vector3.Lerp(currentPosition, targetPosition, Mathf.Min(stopwatch * 0.05f * boundResurrectController.body.attackSpeed, 0.5f));
            gameObject.transform.position = movePosition;
        }
        public void UpdatePositionPreparingOrFiring(int index, int total, float prepStopwatch)
        {
            CharacterBody grovetenderBody = boundResurrectController.body;
            Vector3 right = Vector3.Cross(Vector3.up, grovetenderBody.inputBank.aimDirection).normalized;
            Vector3 center = grovetenderBody.transform.position + new Vector3(0f, 6f, 0f);
            float archRadius = 12f;

            float segmentSize = Mathf.PI / (total + 1);

            float startRadians = -Mathf.PI / 2;

            float radians = startRadians + (segmentSize * (index + 1));

            Vector3 rightOffset = right * Mathf.Sin(radians);
            Vector3 upOffset = Vector3.up * Mathf.Cos(radians);

            Vector3 currentPosition = gameObject.transform.position;
            Vector3 desiredPosition = center + (rightOffset * archRadius) + (upOffset * archRadius);

            gameObject.transform.position = Vector3.Lerp(currentPosition, desiredPosition, Mathf.Min(prepStopwatch * 0.25f * grovetenderBody.attackSpeed, 0.5f));

            if (boundResurrectController.ai != null && boundResurrectController.ai.currentEnemy != null && boundResurrectController.ai.currentEnemy.characterBody != null)
            {
                CharacterBody targetBody = boundResurrectController.ai.currentEnemy.characterBody;
                Vector3 targetPos = targetBody.transform.position;
                Vector3 direction = (targetPos - gameObject.transform.position).normalized;
                gameObject.transform.up = direction;
            }
        }
    }
    public class ProjectileGravestone : MonoBehaviour, IProjectileImpactBehavior
    {
        public BodyIndex bodyIndex;
        public GameObject owner;
        public TeamIndex teamIndex;
        public GravekeeperResurrectController resurrectController;
        private ProjectileStickOnImpact stick;
        private ProjectileController projController;
        private bool hasMadeImpact = false;
        public static GameObject impactEffect = Addressables.LoadAssetAsync<GameObject>(RoR2_Base_Common_VFX.OmniImpactVFXLarge_prefab).WaitForCompletion();
        public static GameObject indicator = Addressables.LoadAssetAsync<GameObject>(RoR2_Base_Common.TeamAreaIndicator__GroundOnly_prefab).WaitForCompletion();
        private GameObject indicatorInstance;
        private Rigidbody rigid;
        private Vector3 impactNormal = Vector3.up;
        public void Start()
        {
            projController = GetComponent<ProjectileController>();
            owner = projController.owner;
            teamIndex = owner.GetComponent<TeamComponent>().teamIndex;
            GravekeeperResurrectController gravestoneController = owner.GetComponent<GravekeeperResurrectController>();
            resurrectController = gravestoneController;
            bodyIndex = gravestoneController.queuedBodyIndices[0];
            gravestoneController.queuedBodyIndices.RemoveAt(0);
            stick = GetComponent<ProjectileStickOnImpact>();
            rigid = GetComponent<Rigidbody>();
        }
        public void FixedUpdate()
        {
            if (rigid != null && !hasMadeImpact)
            {
                projController.transform.rotation = Util.QuaternionSafeLookRotation(rigid.velocity.normalized);
            }
        }
        public void OnProjectileImpact(ProjectileImpactInfo info)
        {
            if (!base.enabled)
            {
                return;
            }
            HurtBox hurtBox = info.collider.GetComponent<HurtBox>();
            if (hurtBox != null)
            {
                return;
            }
            impactNormal = info.estimatedImpactNormal;
            if (hasMadeImpact == false)
            {
                hasMadeImpact = true;
                EffectManager.SpawnEffect(impactEffect, new EffectData { origin = this.gameObject.transform.position, scale = 8f, rotation = Quaternion.identity }, true);
                Util.PlaySound("Play_bellBody_attackLand", gameObject);
                indicatorInstance = Instantiate(indicator, gameObject.transform.position, Util.QuaternionSafeLookRotation(impactNormal));
                indicatorInstance.transform.localScale = Vector3.one * grovetenderResurrectGravestoneRadius.Value;
                TeamAreaIndicator teamAreaIndicator = indicatorInstance.GetComponent<TeamAreaIndicator>();
                if (teamAreaIndicator != null)
                {
                    teamAreaIndicator.teamFilter = gameObject.GetComponent<TeamFilter>();
                }
            }
        }
        public void OnDestroy()
        {
            if (indicatorInstance != null)
            {
                Destroy(indicatorInstance);
                indicatorInstance = null;
            }
            if (!NetworkServer.active)
            {
                return;
            }
            if (bodyIndex == BodyIndex.None)
            {
                return;
            }
            if (resurrectController.boundGhosts.Count >= (int)grovetenderResurrectMaxGhosts.Value)
            {
                return;
            }
            string bodyName = BodyCatalog.GetBodyName(bodyIndex);
            GameObject bodyPrefab = BodyCatalog.FindBodyPrefab(bodyName);
            if (bodyPrefab == null)
            {
                return;
            }
            CharacterMaster master = MasterCatalog.allAiMasters.FirstOrDefault((CharacterMaster master) => master.bodyPrefab == bodyPrefab);
            if (master == null)
            {
                return;
            }
            MasterSummon summon = new MasterSummon
            {
                masterPrefab = master.gameObject,
                ignoreTeamMemberLimit = true,
                position = gameObject.transform.position + new Vector3(0f, 1f, 0f),
            };
            summon.summonerBodyObject = (owner ? owner : null);
            summon.teamIndexOverride = teamIndex == TeamIndex.None ? null : teamIndex;
            summon.ignoreTeamMemberLimit = true;
            summon.useAmbientLevel = true;
            summon.preSpawnSetupCallback = (Action<CharacterMaster>)Delegate.Combine(summon.preSpawnSetupCallback, new Action<CharacterMaster>(PreSpawnSetup));
            CharacterMaster master2 = summon.Perform();
            if (master2 == null)
            {
                return;
            }
            CharacterBody body = master2.GetBody();
            if (body != null)
            {
                EntityStateMachine[] components = body.GetComponents<EntityStateMachine>();
                foreach (EntityStateMachine component in components)
                {
                    component.initialStateType = component.mainStateType;
                }
                CharacterMotor motor = body.GetComponent<CharacterMotor>();
                resurrectController.boundGhosts.Add(body);
                if (motor != null)
                {
                    motor.velocity = impactNormal * 20f;
                }
                else
                {
                    Rigidbody rigidbody = body.GetComponent<Rigidbody>();
                    if (rigidbody != null)
                    {
                        rigidbody.velocity = impactNormal * 20f;
                    }
                }
            }
            void PreSpawnSetup(CharacterMaster newMaster)
            {
                newMaster.inventory.GiveItemPermanent(RoR2Content.Items.HealthDecay, (int)grovetenderResurrectGhostLifetime.Value);
                newMaster.inventory.GiveItemPermanent(MassResurrectModule.GravekeeperGhostItem, 1);
            }
        }
    }
}
