using System;
using System.Collections.Generic;
using System.Text;
using EntityStates;
using RoR2.CharacterAI;
using JetBrains.Annotations;
using R2API;
using RoR2;
using RoR2.Skills;
using RoR2BepInExPack.GameAssetPaths.Version_1_39_0;
using UnityEngine;
using UnityEngine.AddressableAssets;
using System.Linq;

namespace EnemyAbilities.Abilities.Vermin
{
    [EnemyAbilities.ModuleInfo("Warren Network", "Gives Blind Vermin a new Utility", "Blind Vermin", true)]
    public class WarrenNetworkModule : BaseModule
    {

        private static GameObject bodyPrefab = Addressables.LoadAssetAsync<GameObject>(RoR2_DLC1_Vermin.VerminBody_prefab).WaitForCompletion();
        private static GameObject masterPrefab = Addressables.LoadAssetAsync<GameObject>(RoR2_DLC1_Vermin.VerminMaster_prefab).WaitForCompletion();
        private static GameObject warrenPrefab;
        private static GameObject scorchlingBodyPrefab = Addressables.LoadAssetAsync<GameObject>(RoR2_DLC2_Scorchling.ScorchlingBody_prefab).WaitForCompletion();

        public override void Awake()
        {
            base.Awake();
            CreateSkill();
            bodyPrefab.AddComponent<VerminStateController>();
            Stage.onStageStartGlobal += AttachVerminDriverController;
            GlobalEventManager.onCharacterDeathGlobal += RemoveVermin;
            CreateWarrenPrefab();
        }
        private void CreateWarrenPrefab()
        {
            Transform[] transforms = scorchlingBodyPrefab.GetComponentsInChildren<Transform>();
            GameObject mdlObject = transforms.Where(transform => transform.gameObject.name == "mdlScorchlingBreachPile").FirstOrDefault().gameObject;
            GameObject meshObject = transforms.Where(transform => transform.gameObject.name == "meshScorchlingBreachPile").FirstOrDefault().gameObject;
            warrenPrefab = mdlObject.InstantiateClone("warrenPrefab");
            MeshRenderer renderer = warrenPrefab.GetComponentInChildren<MeshRenderer>();
            if (renderer != null)
            {
                Log.Debug($"renderer found");
                Material material = Addressables.LoadAssetAsync<Material>(RoR2_Base_wispgraveyard.matWPTerrainRocky_mat).WaitForCompletion();
                renderer.material = material;
                renderer.materials = [material];
            }
            
        }
        private void RemoveVermin(DamageReport damageReport)
        {
            if (damageReport != null && damageReport.victimBody != null)
            {
                VerminStateController driverController = damageReport.victimBody.gameObject.GetComponent<VerminStateController>();
                if (driverController != null)
                {
                    WarrenNetwork.instance.RemoveVermin(driverController);
                }
            }
        }

        private void AttachVerminDriverController(Stage stage)
        {
            stage.gameObject.AddComponent<WarrenNetwork>();
        }

        public void CreateSkill()
        {
            WarrenSkillDef spawnWarrenEntrance = ScriptableObject.CreateInstance<WarrenSkillDef>();
            (spawnWarrenEntrance as ScriptableObject).name = "VerminBodySpawnWarrenEntrance";
            spawnWarrenEntrance.skillName = "VerminSpawnWarrenEntrance";
            spawnWarrenEntrance.activationStateMachineName = "Body";
            spawnWarrenEntrance.activationState = ContentAddition.AddEntityState<SpawnWarrenEntrance>(out _);
            spawnWarrenEntrance.baseRechargeInterval = 60f;
            spawnWarrenEntrance.isCombatSkill = true;
            ContentAddition.AddSkillDef(spawnWarrenEntrance);

            SkillFamily skillFamily = ScriptableObject.CreateInstance<SkillFamily>();
            (skillFamily as ScriptableObject).name = "VerminUtilityFamily";
            skillFamily.variants = [new SkillFamily.Variant() { skillDef = spawnWarrenEntrance }];

            GenericSkill skill = bodyPrefab.AddComponent<GenericSkill>();
            skill.skillName = "VerminSpawnWarrenEntrance";
            skill._skillFamily = skillFamily;

            SkillLocator locator = bodyPrefab.GetComponent<SkillLocator>();
            locator.utility = skill;

            ContentAddition.AddSkillFamily(skillFamily);

            AISkillDriver waitInBurrow = masterPrefab.AddComponent<AISkillDriver>();
            waitInBurrow.customName = "waitInBurrow";
            waitInBurrow.skillSlot = SkillSlot.None;
            waitInBurrow.minDistance = 0f;
            waitInBurrow.maxDistance = Mathf.Infinity;
            waitInBurrow.moveTargetType = AISkillDriver.TargetType.CurrentEnemy;
            waitInBurrow.movementType = AISkillDriver.MovementType.Stop;
            waitInBurrow.aimType = AISkillDriver.AimType.AtMoveTarget;
            waitInBurrow.enabled = false;
            masterPrefab.ReorderSkillDrivers(waitInBurrow, 0);

            AISkillDriver useUtility = masterPrefab.AddComponent<AISkillDriver>();
            useUtility.customName = "useUtility";
            useUtility.skillSlot = SkillSlot.Utility;
            useUtility.requireSkillReady = true;
            useUtility.minDistance = 50f;
            useUtility.maxDistance = Mathf.Infinity;
            useUtility.moveTargetType = AISkillDriver.TargetType.CurrentEnemy;
            useUtility.movementType = AISkillDriver.MovementType.Stop;
            useUtility.aimType = AISkillDriver.AimType.AtMoveTarget;
            useUtility.driverUpdateTimerOverride = 1f;
            useUtility.enabled = false;
            masterPrefab.ReorderSkillDrivers(useUtility, 1);
        }
        public class WarrenSkillDef : SkillDef
        {
            private class InstanceData : BaseSkillInstanceData
            {
                public VerminStateController controller;
            }
            public override BaseSkillInstanceData OnAssigned([NotNull] GenericSkill skillSlot)
            {
                return new InstanceData
                {
                    controller = skillSlot.characterBody.gameObject.GetComponent<VerminStateController>()
                };
            }
            public override bool IsReady([NotNull] GenericSkill skillSlot)
            {
                InstanceData data = skillSlot.skillInstanceData as InstanceData;
                if (data?.controller == null)
                {
                    return false;
                }
                return !data.controller.hasCreatedWarren && base.IsReady(skillSlot);
            }
        }
        public class SpawnWarrenEntrance : BaseSkillState
        {
            private static float baseDuration = 1f;
            private float duration;
            private VerminStateController stateController;
            public override void OnEnter()
            {
                base.OnEnter();
                Log.Debug($"Used SpawnWarrenEntrance!");
                stateController = characterBody.gameObject.GetComponent<VerminStateController>();
                if (stateController.hasCreatedWarren)
                {
                    outer.SetNextStateToMain();
                    return;
                }
                duration = baseDuration / attackSpeedStat;
                Instantiate(warrenPrefab, characterBody.footPosition, Quaternion.identity);
            }
            public override void FixedUpdate()
            {
                base.FixedUpdate();
                if (base.fixedAge > duration)
                {
                    outer.SetNextStateToMain();
                }
            }
            public override void OnExit()
            {
                base.OnExit();
            }
            public override InterruptPriority GetMinimumInterruptPriority()
            {
                return InterruptPriority.PrioritySkill;
            }

        }
        public class WarrenNetwork : MonoBehaviour
        {
            public static WarrenNetwork instance;
            private static List<VerminStateController> vermin = new List<VerminStateController>();
            private static List<Warren> warrens = new List<Warren>();
            public void Awake()
            {
                instance = this;
            }
            public void AddVermin(VerminStateController driverController)
            {
                vermin.Add(driverController);
            }
            public void RemoveVermin(VerminStateController driverController)
            {
                vermin.Remove(driverController);
            }
            public void AddWarren(Warren warren)
            {
                warrens.Add(warren);
            }
            public void RemoveWarren(Warren warren)
            {
                warrens.Remove(warren);
            }
        }
        public class VerminStateController : MonoBehaviour
        {
            private CharacterBody body;
            private AISkillDriver waitDriver;
            private AISkillDriver utilityDriver;
            private float digStopwatch;
            private float timeBeforeAllowedToDig;
            public bool hasCreatedWarren;
            public void Awake()
            {
                body = GetComponent<CharacterBody>();
                timeBeforeAllowedToDig = UnityEngine.Random.Range(2f, 16f);
            }
            public void Start()
            {
                if (body != null && body.master != null)
                {
                    AISkillDriver[] drivers = body.master.GetComponents<AISkillDriver>();
                    if (drivers.Length > 0)
                    {
                        waitDriver = drivers.Where(driver => driver.customName == "waitInBurrow").FirstOrDefault();
                        utilityDriver = drivers.Where(driver => driver.customName == "useUtility").FirstOrDefault();
                        waitDriver.enabled = false;
                        utilityDriver.enabled = false;
                        WarrenNetwork.instance.AddVermin(this);
                    }
                }
            }
            public void FixedUpdate()
            {
                digStopwatch += Time.fixedDeltaTime;
                if (digStopwatch > timeBeforeAllowedToDig && utilityDriver != null && utilityDriver.enabled == false)
                {
                    utilityDriver.enabled = true;
                }
            }
        }
        public class Warren : MonoBehaviour
        {
            private static float checkInterval = 0.25f;
            private float checkTimer;
            public void Start()
            {
                WarrenNetwork.instance.AddWarren(this);
                checkTimer = checkInterval;
            }
            public void OnDestroy()
            {
                WarrenNetwork.instance.RemoveWarren(this);
            }
            public void FixedUpdate()
            {
                checkTimer -= Time.fixedDeltaTime;
                if (checkTimer < 0)
                {
                    checkTimer += checkInterval;
                    //sphere search for nearby players
                }
            }
        }
    }
}
