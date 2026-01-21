using System.Collections.Generic;
using System.Linq;
using EntityStates;
using R2API;
using RoR2;
using RoR2.CharacterAI;
using RoR2.Scripts.GameBehaviors;
using RoR2.Skills;
using RoR2BepInExPack.GameAssetPaths.Version_1_35_0;
using UnityEngine;
using UnityEngine.AddressableAssets;

//have mercy on your soul, for my code will not

namespace EnemyAbilities.Abilities.IronHauler
{

    [EnemyAbilities.ModuleInfo("Tractor Beam & Fling", "Gives Solus Transporters a pair of new utility abilities:\n- Tractor Beam: The Transporter picks up and manoeuvres a unit that is valid for cargo.\n- Fling: Toss it's cargo at it's target. Deals impact damage based on the cargo's weight.", "Solus Transporter", true)]
    public class TractorBeamModule : BaseModule
    {
        private static GameObject bodyPrefab = Addressables.LoadAssetAsync<GameObject>(RoR2_DLC3_IronHauler.IronHaulerBody_prefab).WaitForCompletion();
        private static GameObject masterPrefab = Addressables.LoadAssetAsync<GameObject>(RoR2_DLC3_IronHauler.IronHaulerMaster_prefab).WaitForCompletion();
        public static SkillDef tractorBeamSkillDef;
        public static SkillDef flingSkillDef;
        public static BuffDef cargoBuffDef;
        public static float maxCargoChaseDistance = 125f;
        public static GameObject tetherPrefab = Addressables.LoadAssetAsync<GameObject>(RoR2_Base_moon2.BloodSiphonTetherVFX_prefab).WaitForCompletion().InstantiateClone("tractorBeamTetherVFX");

        public static Sprite buffIcon = Addressables.LoadAssetAsync<Sprite>(RoR2_DLC2.texBuffDisableAllSkillsIcon_png).WaitForCompletion();
        public static GameObject tractorBeamVFX = Addressables.LoadAssetAsync<GameObject>(RoR2_DLC3_IronHauler.IronHaulerAirLaunchProjectileGhost_prefab).WaitForCompletion();
        public static GameObject gravityWellProjectile = Addressables.LoadAssetAsync<GameObject>(RoR2_DLC3_IronHauler.IronHaulerGravityWellProjectile_prefab).WaitForCompletion();
        private static Material laserMaterial = Addressables.LoadAssetAsync<Material>(RoR2_DLC1_MajorAndMinorConstruct.matMajorConstructBeam_mat).WaitForCompletion();
        public static GameObject sphereVFX;

        public override void Awake()
        {
            base.Awake();
            bodyPrefab.AddComponent<IronHaulerDriverController>();
            CreateSkills();
            CreateBuffDef();
            BodyCatalog.availability.CallWhenAvailable(ModifyTransporter);
            On.RoR2.EntityStateMachine.SetState += DropCargoOnStun;
            RecalculateStatsAPI.GetStatCoefficients += AddArmor;
            SetupTetherPrefab();

            Transform[] transforms = gravityWellProjectile.GetComponentsInChildren<Transform>();
            if (transforms.Length > 0)
            {
                Transform transform = transforms.Where(t => t.name == "Sphere").FirstOrDefault();
                if (transform != null)
                {
                    if (transform.gameObject != null)
                    {
                        GameObject newGameObject = transform.gameObject.InstantiateClone("Clone");
                        sphereVFX = newGameObject;
                    }
                }
            }
            if (sphereVFX != null)
            {
                MeshRenderer renderer = sphereVFX.GetComponent<MeshRenderer>();
                MeshFilter filter = sphereVFX.GetComponent<MeshFilter>();
            }
        }
        private void AddArmor(CharacterBody sender, RecalculateStatsAPI.StatHookEventArgs args)
        {
            if (sender == null)
            {
                return;
            }
            if (sender.HasBuff(cargoBuffDef))
            {
                args.armorAdd += 30f;
            }
        }
        private void DropCargoOnStun(On.RoR2.EntityStateMachine.orig_SetState orig, EntityStateMachine entityStateMachine, EntityState entityState)
        {
            orig(entityStateMachine, entityState);
            if (entityState == null)
            {
                return;
            }
            if (entityState is FrozenState || entityState is StunState)
            {
                CharacterBody body = entityStateMachine.commonComponents.characterBody;
                if (body != null)
                {
                    IronHaulerDriverController controller = body.gameObject.GetComponent<IronHaulerDriverController>();
                    if (controller != null && controller.hasCargo == true)
                    {
                        controller.DetachCargo();
                    }
                }
            }
        }
        private void ModifyTransporter()
        {
            BodyIndex bodyIndex = BodyCatalog.FindBodyIndexCaseInsensitive("IronHaulerBody");
            CharacterBody body = BodyCatalog.GetBodyPrefabBodyComponent(bodyIndex);
            body.baseMoveSpeed *= 1.5f;
            body.baseAcceleration = body.baseMoveSpeed * 6f;
        }
        public void CreateSkills()
        {
            tractorBeamSkillDef = ScriptableObject.CreateInstance<SkillDef>();
            (tractorBeamSkillDef as ScriptableObject).name = "IronHaulerBodyTractorBeam";
            tractorBeamSkillDef.skillName = "IronHaulerTractorBeam";
            tractorBeamSkillDef.activationStateMachineName = "Weapon";
            tractorBeamSkillDef.activationState = ContentAddition.AddEntityState<TractorBeam>(out _);
            tractorBeamSkillDef.baseRechargeInterval = 10f;
            tractorBeamSkillDef.cancelSprintingOnActivation = true;
            tractorBeamSkillDef.isCombatSkill = true;
            tractorBeamSkillDef.beginSkillCooldownOnSkillEnd = false;
            ContentAddition.AddSkillDef(tractorBeamSkillDef);

            flingSkillDef = ScriptableObject.CreateInstance<SkillDef>();
            (flingSkillDef as ScriptableObject).name = "IronHaulerBodyFling";
            flingSkillDef.skillName = "IronHaulerFling";
            flingSkillDef.activationStateMachineName = "Weapon";
            flingSkillDef.activationState = ContentAddition.AddEntityState<Fling>(out _);
            flingSkillDef.baseRechargeInterval = 1f;
            flingSkillDef.cancelSprintingOnActivation = true;
            flingSkillDef.isCombatSkill = true;
            ContentAddition.AddSkillDef(flingSkillDef);

            SkillFamily skillFamily = ScriptableObject.CreateInstance<SkillFamily>();
            (skillFamily as ScriptableObject).name = "IronHaulerUtilityFamily";
            skillFamily.variants = [new SkillFamily.Variant() { skillDef = tractorBeamSkillDef }];

            GenericSkill skill = bodyPrefab.AddComponent<GenericSkill>();
            skill.skillName = "IronHaulerTractorBeam";
            skill._skillFamily = skillFamily;

            SkillLocator locator = bodyPrefab.GetComponent<SkillLocator>();
            locator.utility = skill;

            ContentAddition.AddSkillFamily(skillFamily);

            AISkillDriver[] skillDrivers = masterPrefab.GetComponents<AISkillDriver>();
            foreach (AISkillDriver driver in skillDrivers)
            {
                driver.moveInputScale = 1f;
            }
            AISkillDriver fleeWithCargo = masterPrefab.AddComponent<AISkillDriver>();
            fleeWithCargo.customName = "fleeWithCargo";
            fleeWithCargo.skillSlot = SkillSlot.None;
            fleeWithCargo.minDistance = 0f;
            fleeWithCargo.maxDistance = 40f;
            fleeWithCargo.moveTargetType = AISkillDriver.TargetType.Custom;
            fleeWithCargo.selectionRequiresTargetLoS = false;
            fleeWithCargo.movementType = AISkillDriver.MovementType.FleeMoveTarget;
            fleeWithCargo.moveInputScale = 1f;
            masterPrefab.ReorderSkillDrivers(fleeWithCargo, 0);

            AISkillDriver strafeWithCargo = masterPrefab.AddComponent<AISkillDriver>();
            strafeWithCargo.customName = "strafeWithCargo";
            strafeWithCargo.skillSlot = SkillSlot.Utility;
            strafeWithCargo.requiredSkill = flingSkillDef;
            strafeWithCargo.minDistance = 0f;
            strafeWithCargo.maxDistance = 60f;
            strafeWithCargo.moveTargetType = AISkillDriver.TargetType.Custom;
            strafeWithCargo.activationRequiresAimTargetLoS = true;
            strafeWithCargo.selectionRequiresTargetLoS = false;
            strafeWithCargo.movementType = AISkillDriver.MovementType.StrafeMovetarget;
            strafeWithCargo.moveInputScale = 0.75f;
            masterPrefab.ReorderSkillDrivers(strafeWithCargo, 1);

            AISkillDriver chaseWithCargo = masterPrefab.AddComponent<AISkillDriver>();
            chaseWithCargo.customName = "chaseWithCargo";
            chaseWithCargo.skillSlot = SkillSlot.Utility;
            chaseWithCargo.requiredSkill = flingSkillDef;
            chaseWithCargo.minDistance = 0f;
            chaseWithCargo.maxDistance = 150f;
            chaseWithCargo.moveTargetType = AISkillDriver.TargetType.Custom;
            chaseWithCargo.activationRequiresAimTargetLoS = true;
            chaseWithCargo.selectionRequiresTargetLoS = false;
            chaseWithCargo.movementType = AISkillDriver.MovementType.ChaseMoveTarget;
            chaseWithCargo.moveInputScale = 0.75f;
            masterPrefab.ReorderSkillDrivers(chaseWithCargo, 2);

            AISkillDriver chaseWithCargoFar = masterPrefab.AddComponent<AISkillDriver>();
            chaseWithCargoFar.customName = "chaseWithCargoFar";
            chaseWithCargoFar.skillSlot = SkillSlot.None;
            chaseWithCargoFar.minDistance = 0f;
            chaseWithCargoFar.maxDistance = Mathf.Infinity;
            chaseWithCargoFar.moveTargetType = AISkillDriver.TargetType.Custom;
            chaseWithCargoFar.selectionRequiresTargetLoS = false;
            chaseWithCargoFar.movementType = AISkillDriver.MovementType.ChaseMoveTarget;
            chaseWithCargoFar.moveInputScale = 0.75f;
            masterPrefab.ReorderSkillDrivers(chaseWithCargoFar, 3);

            AISkillDriver useUtilityOnCargoTarget = masterPrefab.AddComponent<AISkillDriver>();
            useUtilityOnCargoTarget.customName = "useUtilityOnCargoTarget";
            useUtilityOnCargoTarget.skillSlot = SkillSlot.Utility;
            useUtilityOnCargoTarget.requiredSkill = tractorBeamSkillDef;
            useUtilityOnCargoTarget.requireSkillReady = true;
            useUtilityOnCargoTarget.minDistance = 0f;
            useUtilityOnCargoTarget.maxDistance = 40f;
            useUtilityOnCargoTarget.moveTargetType = AISkillDriver.TargetType.Custom;
            useUtilityOnCargoTarget.selectionRequiresTargetLoS = false;
            useUtilityOnCargoTarget.activationRequiresAimTargetLoS = true;
            useUtilityOnCargoTarget.movementType = AISkillDriver.MovementType.StrafeMovetarget;
            useUtilityOnCargoTarget.moveInputScale = 1f;
            masterPrefab.ReorderSkillDrivers(useUtilityOnCargoTarget, 4);

            AISkillDriver chaseCargoTarget = masterPrefab.AddComponent<AISkillDriver>();
            chaseCargoTarget.customName = "chaseCargoTarget";
            chaseCargoTarget.skillSlot = SkillSlot.None;
            chaseCargoTarget.minDistance = 0f;
            chaseCargoTarget.maxDistance = maxCargoChaseDistance;
            chaseCargoTarget.moveTargetType = AISkillDriver.TargetType.Custom;
            chaseCargoTarget.activationRequiresTargetLoS = false;
            chaseCargoTarget.movementType = AISkillDriver.MovementType.ChaseMoveTarget;
            chaseCargoTarget.moveInputScale = 1f;
            masterPrefab.ReorderSkillDrivers(chaseCargoTarget, 5);
        }
        public void CreateBuffDef()
        {
            cargoBuffDef = ScriptableObject.CreateInstance<BuffDef>();
            cargoBuffDef.name = "CargoBuff";
            cargoBuffDef.isDebuff = true;
            cargoBuffDef.canStack = false;
            cargoBuffDef.iconSprite = buffIcon;
            cargoBuffDef.isHidden = false;
            ContentAddition.AddBuffDef(cargoBuffDef);
        }
        public void SetupTetherPrefab()
        {
            LineRenderer lineRenderer = tetherPrefab.GetComponent<LineRenderer>();
            if (lineRenderer == null)
            {
                Log.Error($"TractorBeamModule.SetupTetherPrefab failed - tetherPrefab has no LineRenderer!");
                return;
            }
            Material laserMaterialClone = new Material(laserMaterial);
            laserMaterialClone.mainTextureScale = Vector2.one;
            lineRenderer.material = laserMaterialClone;
        }
        public static bool TargetIsValidForCargoSelection(CharacterBody targetBody, CharacterBody haulerBody)
        {
            if (targetBody == null || haulerBody == null)
            {
                return false;
            }
            //filters out dead targets
            if (targetBody.healthComponent == null || !targetBody.healthComponent.alive)
            {
                return false;
            }
            if (haulerBody.healthComponent == null || !haulerBody.healthComponent.alive)
            {
                return false;
            }
            Vector3 targetPos = targetBody.corePosition;
            Vector3 pos = haulerBody.corePosition;
            if (Vector3.Distance(pos, targetPos) > maxCargoChaseDistance)
            {
                return false;
            }
            //filters out bosses
            if (targetBody.isChampion)
            {
                return false;
            }
            //filters out other haulers
            if (targetBody.bodyIndex == BodyCatalog.FindBodyIndexCaseInsensitive("IronHaulerBody"))
            {
                return false;
            }
            //filters out players
            if (targetBody.isPlayerControlled)
            {
                return false;
            }
            //filters out masterless bodies like projectiles
            if (targetBody.master == null)
            {
                return false;
            }
            //filters out enemies currently hauled by other haulers
            if (targetBody.gameObject.GetComponent<IronHaulerCargoController>() != null)
            {
                return false;
            }
            //filters out alpha constructs, distributors and void barnacles
            if (targetBody.rigidbody == null && targetBody.characterMotor == null)
            {
                return false;
            }
            //filters out enemies without setStateOnHurt. covers lunar wisps. fuck those guys.
            if (targetBody.GetComponent<SetStateOnHurt>() == null)
            {
                return false;
            }
            return true;
        }
    }
    public class TractorBeam : BaseSkillState
    {
        private IronHaulerDriverController driverController;
        private BaseAI ai;
        private float duration;
        public static float baseDuration = 0.5f;
        public override void OnEnter()
        {
            base.OnEnter();
            duration = baseDuration / attackSpeedStat;

            if (characterBody == null || characterBody.gameObject == null || characterBody.master == null)
            {
                return;
            }
            driverController = characterBody.gameObject.GetComponent<IronHaulerDriverController>();
            if (driverController == null)
            {
                return;
            }
            ai = characterBody.master.GetComponent<BaseAI>();
            if (ai == null)
            {
                return;
            }
            if (ai.customTarget == null && ai.customTarget.characterBody == null)
            {
                return;
            }
            CharacterBody targetBody = ai.customTarget.characterBody;
            if (!TractorBeamModule.TargetIsValidForCargoSelection(targetBody, characterBody))
            {
                //BAD NO, DROP IT NOW!
                RefundSkill();
                return;
            }
            IronHaulerCargoController cargoController = targetBody.gameObject.AddComponent<IronHaulerCargoController>();
            cargoController.haulerBody = characterBody;
            cargoController.haulerDriverController = driverController;
            driverController.cargoBody = targetBody;
            driverController.cargoController = cargoController;
            driverController.TryFindCustomTarget();
            activatorSkillSlot.SetSkillOverride(characterBody.gameObject, TractorBeamModule.flingSkillDef, GenericSkill.SkillOverridePriority.Replacement);
            activatorSkillSlot.rechargeStopwatch = 0f;
            activatorSkillSlot.RemoveAllStocks();
        }
        public void ExitToMain()
        {
            outer.SetNextStateToMain();
        }
        public void RefundSkill()
        {
            activatorSkillSlot.AddOneStock();
            activatorSkillSlot.baseRechargeStopwatch = activatorSkillSlot._cooldownOverride;
        }
        public override void FixedUpdate()
        {
            base.FixedUpdate();
            if (isAuthority && base.fixedAge > duration)
            {
                outer.SetNextStateToMain();
            }
        }
        public override InterruptPriority GetMinimumInterruptPriority()
        {
            return InterruptPriority.PrioritySkill;
        }
    }
    public class Fling : BaseSkillState
    {
        private static float baseDuration = 6f;
        private float duration;
        private IronHaulerDriverController driverController;
        private IronHaulerCargoController cargoController;
        private CharacterBody cargoBody;

        private static float baseDamageCoefficient = 2f;
        private static float baseForce = 1500f;
        private static Vector3 bonusForce = new Vector3(0f, 1500f, 0f);
        private static float procCoefficient = 1f;
        private static float baseRadius = 12f;

        private static float lockOnDuration = 1f;
        private float lockOnTimer = 0f;

        private static float windupMaxDuration = 5f;
        private float windUpTimer = 0f;

        private static float spinDownDuration = 2f;
        private float spinDownTimer = 0f;

        private CharacterMaster master;
        private BaseAI ai;
        private CharacterBody targetBody;
        private QuaternionPID angularVelocityPID;
        private VectorPID torquePID;
        private VectorPID forcePID;
        private Vector3 savedTorquePID;
        private Vector3 savedAngularVelocityPID;
        private float savedAngularDrag;
        private float savedMaxAngularVelocity;
        private static float maxSpinAngularVelocity = 80f;
        private Vector3 flingVelocity;
        private bool cargoDetached;
        private Vector3 randomSpinDownDirection;
        private bool collidersDisabled;
        private enum FlingState
        {
            None,
            LockOn,
            Windup,
            PostFling
        }
        private FlingState flingState;

        public override void OnEnter()
        {
            base.OnEnter();
            driverController = characterBody.gameObject.GetComponent<IronHaulerDriverController>();
            cargoController = driverController.cargoController;
            cargoController.tractorBeamBonusDistance = 4f;
            cargoBody = cargoController.body;
            duration = baseDuration;
            lockOnTimer = 0f;
            master = characterBody.master;
            driverController.TryFindCustomTarget();
            if (master != null)
            {
                ai = master.gameObject.GetComponent<BaseAI>();
                if (ai != null && ai.customTarget != null && ai.customTarget.characterBody != null)
                {
                    targetBody = ai.customTarget.characterBody;
                }
                ai.aimVectorMaxSpeed = 180f;
            }
            angularVelocityPID = characterBody.gameObject.GetComponent<QuaternionPID>();
            torquePID = characterBody.gameObject.GetComponents<VectorPID>().Where(pid => pid.customName == "torquePID").FirstOrDefault();
            forcePID = characterBody.gameObject.GetComponents<VectorPID>().Where(pid => pid.customName == "Force PID").FirstOrDefault(); 
            if (torquePID != null)
            {
                savedTorquePID = torquePID.PID;
                torquePID.PID = new Vector3(8f, 0f, 0f);
                torquePID.gain = 1;
                torquePID.ResetPID();
            }
            if (angularVelocityPID != null)
            {
                savedAngularVelocityPID = angularVelocityPID.PID;
                angularVelocityPID.PID = new Vector3(80f, 0f, 4f);
                angularVelocityPID.ResetPID();
            }
            if (rigidbody != null)
            {
                savedAngularDrag = rigidbody.angularDrag;
                savedMaxAngularVelocity = rigidbody.maxAngularVelocity;
            }
            flingState = FlingState.LockOn;
            SetColliders(false);
        }
        private void SetColliders(bool enable)
        {
            foreach (Collider collider in characterBody.gameObject.GetComponents<Collider>())
            {
                if (collider != null)
                {
                    collider.enabled = enable;
                    if (collidersDisabled == !enable)
                    {
                        collidersDisabled = enable;
                    }
                }
            }
        }
        public override void OnExit()
        {
            base.OnExit();
            if (collidersDisabled == true)
            {
                SetColliders(true);
            }
            if (cargoController != null)
            {
                cargoController.inWindupState = false;
                cargoController.tractorBeamBonusDistance = 11f;
            }
            if (torquePID != null)
            {
                torquePID.PID = savedTorquePID;
                torquePID.gain = 3;
                torquePID.enabled = true;
                torquePID.ResetPID();
            }
            if (angularVelocityPID != null)
            {
                angularVelocityPID.PID = savedAngularVelocityPID;
                angularVelocityPID.ResetPID();
            }
            if (rigidbody != null)
            {
                rigidbody.maxAngularVelocity = savedMaxAngularVelocity;
                rigidbody.angularDrag = savedAngularDrag;
            }
            ai.aimVectorMaxSpeed = 60f;
        }
        public override void FixedUpdate()
        {
            base.FixedUpdate();
            if (targetBody == null || targetBody.healthComponent == null || !targetBody.healthComponent.alive || ai.customTarget == null) //just to be on the safe side
            {
                outer.SetNextStateToMain();
                return;
            }
            if (flingState == FlingState.LockOn)
            {
                if (rigidbody != null)
                {
                    rigidbody.velocity = Vector3.zero;
                }
                lockOnTimer += Time.fixedDeltaTime;
                if (lockOnTimer >= lockOnDuration)
                {
                    torquePID.enabled = false;
                    rigidbody.angularVelocity = Vector3.zero;
                    rigidbody.angularDrag = 0f;
                    rigidbody.maxAngularVelocity = maxSpinAngularVelocity / cargoController.hullRadius + 4f;
                    ai.aimVectorMaxSpeed = 180f;
                    flingState = FlingState.Windup;
                    cargoController.inWindupState = true;
                }
            }
            if (flingState == FlingState.Windup)
            {
                if (windUpTimer > windupMaxDuration)
                {
                    outer.SetNextStateToMain();
                }
                if (rigidbody != null)
                {
                    //needs some kind of effect to mask how jarring it is when it stops
                    rigidbody.velocity = Vector3.zero;
                }
                windUpTimer += Time.fixedDeltaTime;
                Vector3 spinAxis = characterBody.transform.right;
                float spinSpeedPerFixedUpdate = 0.2f;
                Vector3 vectorRadius = cargoBody.transform.position - characterBody.transform.position;
                Vector3 tangentialVelocity = Vector3.Cross(rigidbody.angularVelocity, vectorRadius);
                Vector3 requiredVelocity = FindRequiredLaunchVelocity(tangentialVelocity);
 
                float alignment = Vector3.Dot(tangentialVelocity.normalized, requiredVelocity.normalized);

                //doesn't have to be super accurate tbh
                if (alignment > 0.95f)
                {
                    flingVelocity = requiredVelocity;
                    flingState = FlingState.PostFling;
                    cargoController.PrepareForFling(flingVelocity);
                    randomSpinDownDirection = UnityEngine.Random.insideUnitSphere;
                }
                if (requiredVelocity.magnitude > tangentialVelocity.magnitude && flingState == FlingState.Windup)
                {
                    rigidbody.angularVelocity += spinAxis * spinSpeedPerFixedUpdate;
                }
            }
            if (flingState == FlingState.PostFling && !cargoDetached)
            {
                torquePID.PID = new Vector3(0.25f, 0f, 0f);
                torquePID.enabled = true;
                torquePID.ResetPID();
                cargoDetached = true;

                float cargoMass = 0f;
                if (cargoBody.rigidbody != null)
                {
                    cargoMass = cargoBody.rigidbody.mass;
                }
                if (cargoBody.characterMotor != null)
                {
                    cargoMass = cargoBody.characterMotor.mass;
                }
                BlastAttack blastAttack = new BlastAttack();
                blastAttack.attacker = characterBody.gameObject;
                blastAttack.attackerFiltering = AttackerFiltering.NeverHitSelf;
                blastAttack.baseDamage = damageStat * (baseDamageCoefficient + cargoMass / 200f);
                blastAttack.baseForce = baseForce + cargoMass * 3f;
                blastAttack.bonusForce = bonusForce + new Vector3(0f, cargoMass * 1.5f, 0f);
                blastAttack.crit = RollCrit();
                blastAttack.damageColorIndex = DamageColorIndex.Default;
                blastAttack.damageType = DamageTypeCombo.Generic;
                blastAttack.falloffModel = BlastAttack.FalloffModel.SweetSpot;
                blastAttack.inflictor = characterBody.gameObject;
                blastAttack.procCoefficient = procCoefficient;
                blastAttack.radius = baseRadius + cargoMass / 50;
                blastAttack.teamIndex = characterBody.teamComponent.teamIndex;
                cargoController.blastAttack = blastAttack;

                skillLocator.primary.rechargeStopwatch = skillLocator.primary.cooldownOverride - 3f;

                driverController.DetachCargo();

                if (collidersDisabled == true)
                {
                    SetColliders(true);
                }
            }
            if (flingState == FlingState.PostFling)
            {
                spinDownTimer += Time.fixedDeltaTime;
                rigidbody.angularVelocity += randomSpinDownDirection * 0.2f;
                if (spinDownTimer > spinDownDuration)
                {
                    outer.SetNextStateToMain();
                }
            }
        }
        private Vector3 FindRequiredLaunchVelocity(Vector3 currentVelocity)
        {
            Vector3 gravity = Physics.gravity; 
            Vector3 initialPosition = cargoBody.transform.position;
            Vector3 finalPosition = ai.customTarget.characterBody.footPosition;
            float time = 3f;

            Vector3 vector1 = (finalPosition - initialPosition) / time;
            Vector3 vector2 = 0.5f * gravity * time;
            Vector3 requiredVelocity = vector1 - vector2;

            return requiredVelocity;
        }
        public override InterruptPriority GetMinimumInterruptPriority()
        {
            return InterruptPriority.Stun;
        }
    }
    public class IronHaulerCargoController : MonoBehaviour
    {
        public CharacterBody haulerBody;
        public IronHaulerDriverController haulerDriverController;
        public CharacterBody body;
        private int fixedUpdatesWithoutHauler;
        private int maxFixedUpdatesWithoutHauler = 1;
        public static float baseTractorBeamBonusDistance = 11f;
        public float tractorBeamBonusDistance;
        private float pullStrength;
        private float stopwatch;
        public OriginalBehaviours originalBehaviours;
        private Rigidbody rigid;
        private RigidbodyMotor rigidMotor;
        private CharacterMotor motor;
        private SetStateOnHurt stateOnHurt;
        private PseudoCharacterMotor pseudoMotor;
        private bool origBehavioursRecorded;
        private float stunRefreshTimer;
        private static float stunRefreshDuration = 0.25f;
        private bool tempCanBeStunned;
        public float hullRadius;
        private ChildMonsterController childMonsterController;
        public bool inWindupState = false;
        public bool readyToFling = false;
        private Vector3 flingVelocity;
        public bool flung = false;
        private VectorPID forcePID;
        public BlastAttack blastAttack;
        private float drag;
        private static GameObject explosionPrefab = Addressables.LoadAssetAsync<GameObject>(RoR2_Base_Loader.LoaderGroundSlam_prefab).WaitForCompletion();
        private int _origLayer;
        private GameObject sphereVFXInstance;
        private static float delayBeforeRestoring = 0.2f;
        private float delayTimer = 0f;
        public bool restore = false;
        private bool firedBlast = false;
        public class OriginalBehaviours
        {
            public bool HasCharMotor;
            public bool HasRigidBody;
            public bool HasRigidBodyMotor;
            public bool CharMotorGravity;
            public float CharMotorGravityScale;
            public bool RigidBodyGravity;
            public bool IgnoreFallDamage;
            public bool RespectImpactDamage;
            public bool HasPseudoMotor;
            public int GravityCount;
            public int FlightCount;

            public OriginalBehaviours(
                bool hasCharMotor = false,
                bool hasRigidBody = false,
                bool hasRigidBodyMotor = false,
                bool characterMotorGravity = false,
                float characterMotorGravityScale = 0f,
                bool rigidBodyGravity = false,
                bool ignoreFallDamage = false,
                bool respectImpactDamage = false,
                bool hasPseudoMotor = false,
                int gravityCount = 0,
                int flightCount = 0)
            {
                HasCharMotor = hasCharMotor;
                HasRigidBody = hasRigidBody;
                HasRigidBodyMotor = hasRigidBodyMotor;
                CharMotorGravity = characterMotorGravity;
                CharMotorGravityScale = characterMotorGravityScale;
                RigidBodyGravity = rigidBodyGravity;
                IgnoreFallDamage = ignoreFallDamage;
                RespectImpactDamage = respectImpactDamage;
                HasPseudoMotor = hasPseudoMotor;
                GravityCount = gravityCount;
                FlightCount = flightCount;
            }
        }
        public void Awake()
        {
            body = GetComponent<CharacterBody>();
            body.SetBuffCount(TractorBeamModule.cargoBuffDef.buffIndex, 1);
            motor = GetComponent<CharacterMotor>();
            rigid = GetComponent<Rigidbody>();
            rigidMotor = GetComponent<RigidbodyMotor>();
            stateOnHurt = GetComponent<SetStateOnHurt>();
            pseudoMotor = GetComponent<PseudoCharacterMotor>();
            childMonsterController = GetComponent<ChildMonsterController>();
            VectorPID[] pids = GetComponents<VectorPID>();
            if (motor != null)
            {
            }

            if (pids != null && pids.Length > 0)
            {
                forcePID = pids.Where(pid => pid.customName == "Force PID").FirstOrDefault();
            }
            if (childMonsterController != null)
            {
                childMonsterController.enabled = false;
            }
            if (stateOnHurt != null)
            {
                if (stateOnHurt.canBeStunned == false)
                {
                    stateOnHurt.canBeStunned = true;
                    tempCanBeStunned = true;
                }
                stateOnHurt.SetStun(1f);
            }
            sphereVFXInstance = Object.Instantiate(TractorBeamModule.sphereVFX, body.gameObject.transform);
            switch (body.hullClassification)
            {
                case HullClassification.Human:
                    hullRadius = 4f;
                    break;
                case HullClassification.Golem:
                    hullRadius = 8f;
                    break;
                //accursed gups
                case HullClassification.BeetleQueen:
                    hullRadius = 12f;
                    break;
                default:
                    hullRadius = 10f;
                    break;
            }
            float scale = Mathf.Max(1f, 1 * ((hullRadius) + 5f));
            sphereVFXInstance.transform.localScale = new Vector3(scale, scale, scale);
            tractorBeamBonusDistance = baseTractorBeamBonusDistance;
        }
        public void RecordAndSuppressBehaviours()
        {
            originalBehaviours = new OriginalBehaviours
            {
                HasCharMotor = motor != null,
                HasRigidBody = rigid != null,
                HasRigidBodyMotor = rigidMotor != null,
                CharMotorGravity = motor != null ? motor.useGravity : false,
                CharMotorGravityScale = motor != null ? motor.gravityScale : 0f,
                RigidBodyGravity = rigid != null ? rigid.useGravity : false,
                IgnoreFallDamage = (body.bodyFlags & CharacterBody.BodyFlags.IgnoreFallDamage) != 0,
                RespectImpactDamage = rigidMotor != null ? rigidMotor.canTakeImpactDamage : false,
                HasPseudoMotor = pseudoMotor != null,
                GravityCount = motor != null ? motor.gravityParameters.channeledAntiGravityGranterCount : 0,
                FlightCount = motor != null ? motor.flightParameters.channeledFlightGranterCount : 0,
            };
            body.bodyFlags |= CharacterBody.BodyFlags.IgnoreFallDamage;
            if (motor != null)
            {
                motor.useGravity = false;
                motor.gravityScale = 0f;
                CharacterGravityParameters gravityParameters = body.characterMotor.gravityParameters;
                CharacterFlightParameters flightParameters = body.characterMotor.flightParameters;
                flightParameters.channeledFlightGranterCount = 0;
                gravityParameters.channeledAntiGravityGranterCount = 0;
                motor.gravityParameters = gravityParameters;
                motor.flightParameters = flightParameters;
            }
            if (rigid != null)
            {
                rigid.useGravity = false;
            }
            if (rigidMotor != null)
            {
                rigidMotor.canTakeImpactDamage = false;
            }
            DisableCharacterMotorCollision();
        }
        public void FixedUpdate()
        {
            if (origBehavioursRecorded == false)
            {
                //done here rather than Awake() because of Vultures
                RecordAndSuppressBehaviours();
                origBehavioursRecorded = true;
            }
            if (restore == true)
            {
                delayTimer += Time.fixedDeltaTime;
            }
            if (restore == true && delayTimer > delayBeforeRestoring)
            {
                RestoreOriginalBehaviours();
            }
            stopwatch += Time.fixedDeltaTime;

            if (fixedUpdatesWithoutHauler > maxFixedUpdatesWithoutHauler && !flung && !readyToFling)
            {
                //fallback
                Log.Error($"IronHaulerCargoController has existed for {fixedUpdatesWithoutHauler} fixed updates without a hauler! Destroying...");
                restore = true;
                return;
            }
            if ((haulerBody == null || haulerDriverController == null) && !flung && !readyToFling)
            {
                fixedUpdatesWithoutHauler++;
                return;
            }
            else
            {
                fixedUpdatesWithoutHauler = 0;
            }
            //refreshes stun duration to prevent repeated stun-enter animation

            stunRefreshTimer -= Time.fixedDeltaTime;
            if (stunRefreshTimer < 0f)
            {
                stunRefreshTimer += stunRefreshDuration;
                RefreshStun();
            }
            if (readyToFling == true && flung == false)
            {
                flung = true;
                EnterFlungState();

            }
            if (body != null && body.healthComponent != null && body.healthComponent.alive && haulerBody != null && haulerDriverController != null && readyToFling == false)
            {
                RepositionCargo();
            }
        }
        public void EnterFlungState()
        {
            if (motor != null)
            {
                motor.velocity = flingVelocity;
                motor.disableAirControlUntilCollision = true;
                motor.useGravity = true;
                motor.gravityScale = 1f;
                motor.onMovementHit += OnMovementHitCharacterMotor;
            }
            if (rigid != null)
            {
                rigid.velocity = flingVelocity;
                rigid.useGravity = true;
                drag = rigid.drag;
                rigid.drag = 0f;
                if (forcePID != null)
                {
                    forcePID.enabled = false;
                }
            }
            if (rigidMotor != null)
            {
                rigidMotor.onMovementHit += OnMovementHitRigidbodyMotor;
            }
        }
        private void OnMovementHitRigidbodyMotor(ref CharacterMotor.MovementHitInfo movementHitInfo)
        {
            Detonate();
        }
        private void OnMovementHitCharacterMotor(ref CharacterMotor.MovementHitInfo movementHitInfo)
        {
            Detonate();
        }
        private void Detonate()
        {
            if (firedBlast == true)
            {
                return;
            }
            if (body.healthComponent == null && body.healthComponent.alive == false)
            {
                return;
            }


            if (blastAttack != null)
            {
                firedBlast = true;
                EffectManager.SpawnEffect(explosionPrefab, new EffectData { origin = body.corePosition, scale = blastAttack.radius }, true);
                blastAttack.position = body.corePosition;
                blastAttack.Fire();
                DamageInfo damageInfo = new DamageInfo
                {
                    attacker = body.gameObject,
                    inflictor = body.gameObject,
                    crit = false,
                    damage = body.healthComponent.fullHealth / 20f, //have to adjust for friendly-fire scaling (which i think is 2 by default)?
                    damageColorIndex = DamageColorIndex.Default,
                    damageType = DamageType.Generic,
                    procCoefficient = 0f,
                    procChainMask = default(ProcChainMask),
                    force = Vector3.zero,
                    position = body.transform.position
                };
                body.healthComponent.TakeDamage(damageInfo);
            }
            restore = true;
        }
        public void RepositionCargo()
        {
            InputBankTest haulerInputBankTest = haulerBody.inputBank;
            if (haulerInputBankTest == null)
            {
                Log.Error($"Hauler has no Input Bank!");
            }
            Ray aimRay = haulerInputBankTest.GetAimRay();
            Vector3 targetPos = body.corePosition;
            float tractorBeamDistance = tractorBeamBonusDistance + hullRadius;
            Vector3 startPosition = aimRay.origin;
            Vector3 startDirection;
            if (inWindupState)
            {
                startDirection = haulerBody.modelLocator.modelBaseTransform.forward;
            }
            else
            {
                startDirection = aimRay.direction;
            }
            bool success = Physics.Raycast(startPosition, startDirection, out RaycastHit hitInfo, tractorBeamDistance, LayerIndex.world.mask, QueryTriggerInteraction.Ignore);
            if (success == true)
            {
                targetPos = hitInfo.point - startDirection * hullRadius;
            }
            else
            {
                targetPos = startPosition + startDirection * (tractorBeamDistance);
            }
            Vector3 coreToTransformOffset = body.corePosition - body.transform.position;
            Vector3 desiredCore = targetPos;
            Vector3 desiredPos = desiredCore - coreToTransformOffset;
            float tractorCharge = Mathf.Clamp01(stopwatch / 6f);
            pullStrength = tractorCharge * 0.3f;
            Vector3 newPos = Vector3.Lerp(body.footPosition, desiredPos, pullStrength);
            TeleportHelper.TeleportBody(body, newPos, true);
        }
        public void RestoreOriginalBehaviours()
        {
            body.SetBuffCount(TractorBeamModule.cargoBuffDef.buffIndex, 0);
            if (originalBehaviours.HasCharMotor && motor != null)
            {
                motor.gravityScale = originalBehaviours.CharMotorGravityScale;
                motor.useGravity = originalBehaviours.CharMotorGravity;
                CharacterGravityParameters gravParams = motor.gravityParameters;
                CharacterFlightParameters flightParams = motor.flightParameters;
                gravParams.channeledAntiGravityGranterCount = originalBehaviours.GravityCount;
                flightParams.channeledFlightGranterCount = originalBehaviours.FlightCount;
                motor.gravityParameters = gravParams;
                motor.flightParameters = flightParams;
                if (flung == true)
                {
                    motor.onMovementHit -= OnMovementHitCharacterMotor;
                }
            }
            if (originalBehaviours.HasRigidBodyMotor && rigidMotor)
            {
                rigidMotor.canTakeImpactDamage = originalBehaviours.RespectImpactDamage;
                if (flung)
                {
                    rigidMotor.onMovementHit -= OnMovementHitRigidbodyMotor;
                }

            }
            if (originalBehaviours.HasRigidBody && rigid != null)
            {
                rigid.useGravity = originalBehaviours.RigidBodyGravity;
                rigid.drag = drag;
            }
            if (forcePID != null)
            {
                forcePID.enabled = true;
            }
            if (!originalBehaviours.IgnoreFallDamage)
            {
                body.bodyFlags &= ~CharacterBody.BodyFlags.IgnoreFallDamage;
            }
            if (tempCanBeStunned)
            {
                stateOnHurt.canBeStunned = false;
            }
            if (childMonsterController != null)
            {
                childMonsterController.enabled = true;
            }
            EnableCharacterMotorCollision();
            UnityEngine.Object.Destroy(sphereVFXInstance);
            UnityEngine.Object.Destroy(this);
        }
        public void RefreshStun()
        {
            if (stateOnHurt != null)
            {
                if (stateOnHurt.targetStateMachine.state is StunState stunState)
                {
                    if (stunState.duration - stunState.fixedAge <= 1f)
                    {
                        stunState.duration += stunRefreshDuration;
                    }
                }
                else
                {
                    if (stateOnHurt.canBeStunned)
                    {
                        stateOnHurt.SetStun(1f);
                    }
                }
            }
        }
        public void PrepareForFling(Vector3 velocity)
        {
            readyToFling = true;
            flingVelocity = velocity;
        }
        private void DisableCharacterMotorCollision()
        {
            if (motor != null)
            {
                _origLayer = body.gameObject.layer;
                body.gameObject.layer = LayerIndex.GetAppropriateFakeLayerForTeam(body.teamComponent.teamIndex).intVal;
                motor.Motor.RebuildCollidableLayers();
            }
        }
        private void EnableCharacterMotorCollision()
        {
            if (motor != null)
            {
                body.gameObject.layer = _origLayer;
                motor.Motor.RebuildCollidableLayers();
            }
        }
    }
    public class IronHaulerDriverController : MonoBehaviour
    {
        public bool hasCargo;
        public bool tractorBeamReady;
        public bool hasValidCargoTarget;

        public CharacterBody cargoBody;
        public IronHaulerCargoController cargoController;
        private TetherVfxOrigin tetherVfxOrigin;    

        public AISkillDriver fleeWithCargoDriver;
        public AISkillDriver strafeWithCargoDriver;
        public AISkillDriver chaseWithCargoDriver;
        public AISkillDriver chaseWithCargoFarDriver;
        public AISkillDriver chaseCargoTargetDriver;
        public AISkillDriver useTractorBeamDriver;

        private CharacterBody body;
        private SkillLocator skillLocator;
        private ChildLocator childLocator;
        private Transform muzzleTransform;
        public BaseAI ai;

        public static float targetCheckInterval = 0.25f;
        private float targetCheckTimer;

        public void Awake()
        {
            body = GetComponent<CharacterBody>();
            IronHaulerController origController = GetComponent<IronHaulerController>();
            if (origController != null)
            {
                origController.enabled = false;
            }

            hasCargo = false;
            targetCheckTimer = targetCheckInterval;

            if (body != null && body.modelLocator != null && body.modelLocator.modelTransform != null)
            {
                childLocator = body.modelLocator.modelTransform.GetComponent<ChildLocator>();
            }
            muzzleTransform = childLocator.FindChild("Muzzle");
            tetherVfxOrigin = body.healthComponent.gameObject.AddComponent<TetherVfxOrigin>();
            tetherVfxOrigin.tetherPrefab = TractorBeamModule.tetherPrefab;
            tetherVfxOrigin.transform = muzzleTransform;

        }
        public void Start()
        {
            if (body != null && body.master != null)
            {
                ai = body.master.GetComponent<BaseAI>();
            }
            skillLocator = body.skillLocator;
            AISkillDriver[] skillDrivers = ai.skillDrivers;
            fleeWithCargoDriver = skillDrivers.Where(driver => driver.customName == "fleeWithCargo").FirstOrDefault();
            strafeWithCargoDriver = skillDrivers.Where(driver => driver.customName == "strafeWithCargo").FirstOrDefault();
            chaseWithCargoDriver = skillDrivers.Where(driver => driver.customName == "chaseWithCargo").FirstOrDefault();
            chaseWithCargoFarDriver = skillDrivers.Where(driver => driver.customName == "chaseWithCargoFar").FirstOrDefault();
            chaseCargoTargetDriver = skillDrivers.Where(driver => driver.customName == "chaseCargoTarget").FirstOrDefault();
            useTractorBeamDriver = skillDrivers.Where(driver => driver.customName == "useUtilityOnCargoTarget").FirstOrDefault();
        }
        public void FixedUpdate()
        {
            if (ai == null)
            {
                return;
            }
            targetCheckTimer -= Time.fixedDeltaTime;
            if (ai.customTarget.gameObject == null || !ai.customTarget.healthComponent.alive)
            {
                targetCheckTimer = targetCheckInterval;
                TryFindCustomTarget();
            }
            if (targetCheckTimer < 0)
            {
                targetCheckTimer += targetCheckInterval;
                TryFindCustomTarget();
            }
            tractorBeamReady = skillLocator.utility.skillDef == TractorBeamModule.tractorBeamSkillDef && skillLocator.utility.IsReady();

            fleeWithCargoDriver.enabled = hasCargo;
            strafeWithCargoDriver.enabled = hasCargo;
            chaseWithCargoDriver.enabled = hasCargo;
            chaseWithCargoFarDriver.enabled = hasCargo;

            bool canLookForCargo = tractorBeamReady && hasValidCargoTarget && !hasCargo;
            chaseCargoTargetDriver.enabled = canLookForCargo;
            useTractorBeamDriver.enabled = canLookForCargo;

            if (cargoBody != null && cargoController != null)
            {
                if (cargoBody.healthComponent == null || !cargoBody.healthComponent.alive)
                {
                    DetachCargo();

                }
                else
                {
                    hasCargo = true;
                }
            }
            else
            {
                hasCargo = false;
            }
        }
        public void Update()
        {
            if (cargoBody != null && hasCargo)
            {
                tetherVfxOrigin.SetTetheredTransforms(cargoBody.coreTransform != null ? [cargoBody.coreTransform] : [cargoBody.transform]);
            }
            else
            {
                tetherVfxOrigin.SetTetheredTransforms([]);
            }
        }
        public void DetachCargo()
        {
            if (!cargoController.readyToFling)
            {
                cargoController.restore = true;
            }
            //cargoController.haulerBody = null;
            //cargoController.haulerDriverController = null;
            cargoBody = null;
            cargoController = null;
            hasCargo = false;
            skillLocator.utility.UnsetSkillOverride(body.gameObject, TractorBeamModule.flingSkillDef, GenericSkill.SkillOverridePriority.Replacement);
            skillLocator.utility.RemoveAllStocks();
            skillLocator.utility.rechargeStopwatch = 0f;
        }
        public void TryFindCustomTarget()
        {
            if (ai == null || ai.bodyInputBank == null || ai.body == null)
            {
                return;
            }
            Ray aimRay = ai.bodyInputBank.GetAimRay();
            BullseyeSearch search = new BullseyeSearch();
            search.viewer = ai.body;
            search.filterByDistinctEntity = true;
            search.filterByLoS = false;
            search.maxDistanceFilter = Mathf.Infinity;
            search.minDistanceFilter = 0f;
            search.maxAngleFilter = 360f;
            search.searchDirection = aimRay.direction;
            search.searchOrigin = aimRay.origin;
            search.sortMode = BullseyeSearch.SortMode.Distance;
            search.queryTriggerInteraction = QueryTriggerInteraction.UseGlobal;
            TeamMask teamMask = TeamMask.allButNeutral;
            search.teamMaskFilter = teamMask;
            search.RefreshCandidates();
            search.FilterOutGameObject(ai.body.gameObject);

            if (ai.body != null && skillLocator != null && skillLocator.utility != null && skillLocator.utility.IsReady() && skillLocator.utility.skillDef == TractorBeamModule.tractorBeamSkillDef && !hasCargo)
            {
                IEnumerable<HurtBox> source = search.GetResults().Where(hurtBox => hurtBox.healthComponent != null && hurtBox.healthComponent.body != null && TractorBeamModule.TargetIsValidForCargoSelection(hurtBox.healthComponent.body, body));
                if (source.Any())
                {
                    ai.customTarget.gameObject = source.FirstOrDefault()?.healthComponent.body.gameObject;
                    hasValidCargoTarget = true;
                    return;
                }
            }
            hasValidCargoTarget = false;
            TryFindDefaultTarget(search);
        }
        private void TryFindDefaultTarget(BullseyeSearch search)
        {
            IEnumerable<HurtBox> source = search.GetResults().Where(TargetValidForNormalSelection);
            ai.customTarget.gameObject = source.FirstOrDefault()?.healthComponent.body.gameObject;
        }
        private bool TargetValidForNormalSelection(HurtBox arg)
        {
            HealthComponent targetHealthComponent = arg.healthComponent;
            if (targetHealthComponent == null)
            {
                return false;
            }
            if (!targetHealthComponent.alive)
            {
                return false;
            }
            CharacterBody targetBody = targetHealthComponent.body;
            if (body.teamComponent.teamIndex == targetBody.teamComponent.teamIndex)
            {
                return false;
            }
            bool isDrone = !((targetBody.bodyFlags & CharacterBody.BodyFlags.Drone) == 0);
            if (isDrone)
            {
                return false;
            }
            if (targetBody.gameObject.GetComponent<IronHaulerCargoController>() != null)
            {
                return false;
            }
            return true;
        }
    }
}
