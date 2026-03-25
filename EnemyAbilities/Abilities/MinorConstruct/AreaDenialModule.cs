using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BepInEx.Configuration;
using R2API;
using RoR2;
using RoR2BepInExPack.GameAssetPaths.Version_1_39_0;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace EnemyAbilities.Abilities.MinorConstruct
{
    [EnemyAbilities.ModuleInfo("Alpha Tripwire", "Gives Alpha Constructs a new passive ability:\n-Alpha Tripwire: Causes Alpha Constructs to passively create tripwires between themselves and other nearby Alpha Constructs. Walking into the tripwires deals a burst of damage and inflicts a brief root. Watch your step!", "Alpha Construct", true)]
    public class AreaDenialModule : BaseModule
    {
        private static GameObject bodyPrefab = Addressables.LoadAssetAsync<GameObject>(RoR2_DLC1_MajorAndMinorConstruct.MinorConstructBody_prefab).WaitForCompletion();
        private static Material laserMaterial = Addressables.LoadAssetAsync<Material>(RoR2_DLC1_MajorAndMinorConstruct.matMajorConstructBeam_mat).WaitForCompletion();
        public static GameObject tetherPrefab = Addressables.LoadAssetAsync<GameObject>(RoR2_Base_moon2.BloodSiphonTetherVFX_prefab).WaitForCompletion().InstantiateClone("areaDenialTetherVFX");
        private static GameObject laserPrefab = Addressables.LoadAssetAsync<GameObject>(RoR2_Base_Golem.LaserGolem_prefab).WaitForCompletion().InstantiateClone("areaDenialWarningLaser");

        private static ConfigEntry<float> tripwireMaxLength;
        private static ConfigEntry<float> maxTripwires;
        private static ConfigEntry<float> tripwireActivationDelay;
        private static ConfigEntry<float> tripwireRechargeTime;
        private static ConfigEntry<float> tripwireRadius;
        private static ConfigEntry<float> tripwireDamageCoefficient;
        private static ConfigEntry<float> tripwireRootDuration;
        private static ConfigEntry<float> tripwireSlowDuration;

        public override void RegisterConfig()
        {
            base.RegisterConfig();
            tripwireMaxLength = BindFloat("Max Tripwire Length", 50f, "The maximum distance two alpha constructs can be apart and still form a tripwire.", 25f, 100f, 1f, PluginConfig.FormatType.Distance);
            maxTripwires = BindFloat("Max Tripwires", 2f, "The maximum number of tripwires that can be connected to one Alpha Construct.", 1f, 4f, 1f, PluginConfig.FormatType.None);
            tripwireActivationDelay = BindFloat("Tripwire Activation Delay", 2f, "The warning duration before tripwires become live.", 0.5f, 4f, 0.1f, PluginConfig.FormatType.Time);
            tripwireRechargeTime = BindFloat("Tripwire Recharge Time", 4f, "The duration of time after a tripwire is triggered that the tripwire will reappear.", 1f, 10f, 0.1f, PluginConfig.FormatType.Time);
            tripwireRadius = BindFloat("Tripwire Radius", 2f, "How close a target has to be to the nearest point on the tripwire in order to trigger it.", 1f, 3f, 0.1f, PluginConfig.FormatType.Distance);
            tripwireDamageCoefficient = BindFloat("Tripwire Damage Coefficient", 250f, "The damage coefficient of the tripwire.", 100f, 500f, 5f, PluginConfig.FormatType.Percentage);
            tripwireRootDuration = BindFloat("Tripwire Root Duration", 1f, "The duration of the root upon triggering a tripwire.", 0f, 5f, 0.1f, PluginConfig.FormatType.Time);
            tripwireSlowDuration = BindFloat("Tripwire Slow Duration", 2f, "The duration of the slow upon triggering a tripwire.", 0f, 5f, 0.1f, PluginConfig.FormatType.Time);
        }
        public override void Initialise()
        {
            base.Initialise();
            SetupTetherPrefab();
            ModifyLaserPrefab();
            bodyPrefab.AddComponent<AreaDenialComponent>();
            Stage.onStageStartGlobal += ResetIndexCounter;
        }
        private void ResetIndexCounter(Stage obj)
        {
            if (obj != null)
            {
                AreaDenialComponent.indexCounter = 0;
            }
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
        public void ModifyLaserPrefab()
        {
            LineRenderer lineRenderer = laserPrefab.GetComponent<LineRenderer>();
            lineRenderer.startWidth = 0.4f;
            lineRenderer.endWidth = 0.4f;
        }
        public class AreaDenialConnection
        {
            public AreaDenialComponent transmitterComponent;
            public AreaDenialComponent receiverComponent;
            public float lifetime;
            public bool damageActive;
            public GameObject laserInstance;
            public LineRenderer laserLineComponent;
            public ConnectionState state;
            public static float connectionRadius = tripwireRadius.Value;
            public static float damageCoefficient = tripwireDamageCoefficient.Value / 100f;
            public static float postTriggerDuration = tripwireRechargeTime.Value;
            public float postTriggerTimer;
            public enum ConnectionState
            {
                Warning,
                Active,
                PostTrigger
            };
        }
        public class AreaDenialComponent : MonoBehaviour
        {
            public CharacterBody body;
            private static int maxConnections = (int)maxTripwires.Value;
            private TetherVfxOrigin tetherVfxOrigin;
            public static int indexCounter = 0;
            private int index;
            private float checkConnectionTimer;
            public static float checkConnectionInterval = 0.1f;
            private static float maxDistance = tripwireMaxLength.Value;
            private static float laserActivateDelay = tripwireActivationDelay.Value;
            private float flashInterval = 0.1f;
            private List<AreaDenialConnection> outgoingConnections = new List<AreaDenialConnection>();
            private List<AreaDenialConnection> incomingConnections = new List<AreaDenialConnection>();
            private static GameObject explosionEffect = Addressables.LoadAssetAsync<GameObject>(RoR2_DLC1_MajorAndMinorConstruct.OmniExplosionVFXMajorConstruct_prefab).WaitForCompletion();
            public int Connections => outgoingConnections.Count + incomingConnections.Count;
            public void Start()
            {
                checkConnectionTimer = checkConnectionInterval;
                index = indexCounter;
                indexCounter++;
                body = GetComponent<CharacterBody>();
                if (body == null)
                {
                    return;
                }
                HealthComponent healthComponent = body.healthComponent;
                if (healthComponent == null)
                {
                    return;
                }
                tetherVfxOrigin = healthComponent.gameObject.AddComponent<TetherVfxOrigin>();
                tetherVfxOrigin.tetherPrefab = tetherPrefab;
                tetherVfxOrigin.transform = body.aimOriginTransform;
            }
            public void OnEnable()
            {
                InstanceTracker.Add(this);
            }
            public void OnDisable()
            {
                if (outgoingConnections.Count > 0)
                {
                    for (int i = outgoingConnections.Count - 1; i >= 0; i--)
                    {
                        AreaDenialConnection connection = outgoingConnections[i];
                        BreakConnection(connection);
                    }
                }
                InstanceTracker.Remove(this);
            }
            public void FixedUpdate()
            {
                checkConnectionTimer -= Time.fixedDeltaTime;
                for (int i = outgoingConnections.Count - 1; i >= 0; i--)
                {
                    AreaDenialConnection connection = outgoingConnections[i];
                    connection.lifetime += Time.fixedDeltaTime;

                    if (connection.state == AreaDenialConnection.ConnectionState.Warning)
                    {
                        if (connection.lifetime >= laserActivateDelay)
                        {
                            Util.PlaySound("Play_voidman_m1_shoot", body.gameObject);
                            connection.state = AreaDenialConnection.ConnectionState.Active;
                            if (connection.laserInstance != null)
                            {
                                connection.laserInstance.SetActive(false);
                            }
                        }
                        else if (connection.laserLineComponent != null && connection.receiverComponent != null && connection.receiverComponent.body != null)
                        {
                            connection.laserLineComponent.SetPosition(0, body.aimOriginTransform.position);
                            connection.laserLineComponent.SetPosition(1, connection.receiverComponent.body.aimOriginTransform.position);
                            if (connection.lifetime > laserActivateDelay / 2f)
                            {
                                bool flash = connection.lifetime % flashInterval > flashInterval / 2f;
                                float lineWidth = flash ? 0.1f : 0.4f;
                                connection.laserLineComponent.startWidth = lineWidth;
                                connection.laserLineComponent.endWidth = lineWidth;
                            }
                        }
                    }
                    if (connection.state == AreaDenialConnection.ConnectionState.Active)
                    {
                        CheckConnectionForCollision(connection);
                    }
                    if (connection.state == AreaDenialConnection.ConnectionState.PostTrigger)
                    {
                        connection.postTriggerTimer += Time.fixedDeltaTime;
                        if (connection.postTriggerTimer >= AreaDenialConnection.postTriggerDuration)
                        {
                            connection.state = AreaDenialConnection.ConnectionState.Warning;
                            connection.lifetime = 0f;
                            connection.postTriggerTimer = 0f;
                            if (connection.laserLineComponent != null)
                            {
                                connection.laserInstance.SetActive(true);
                                connection.laserLineComponent.startWidth = 0.4f;
                                connection.laserLineComponent.endWidth = 0.4f;
                            }
                        }
                    }
                }
                //checks connections
                if (checkConnectionTimer < 0)
                {
                    List<Transform> tetheredTransforms = new List<Transform>();
                    checkConnectionTimer += checkConnectionInterval;
                    //should check existing connections. are they still valid or have they been broken?
                    if (Connections > 0)
                    {
                        for (int i = outgoingConnections.Count - 1; i >= 0; i--)
                        {
                            AreaDenialConnection connection = outgoingConnections[i];
                            AreaDenialComponent receiverComponent = connection.receiverComponent;
                            if (IsConnectionValid(receiverComponent, false))
                            {
                                if (connection.state == AreaDenialConnection.ConnectionState.Active)
                                {
                                    tetheredTransforms.Add(receiverComponent.body.aimOriginTransform);
                                }
                            }
                            else
                            {
                                BreakConnection(connection);
                            }
                        }
                        tetherVfxOrigin.SetTetheredTransforms(tetheredTransforms);
                    }
                    List<AreaDenialComponent> allComponents = InstanceTracker.GetInstancesList<AreaDenialComponent>();
                    foreach (AreaDenialComponent component in allComponents)
                    {
                        if (!IsConnectionValid(component, true))
                        {
                            continue;
                        }
                        AreaDenialConnection connection = new AreaDenialConnection();
                        connection.lifetime = 0f;
                        connection.state = AreaDenialConnection.ConnectionState.Warning;
                        connection.transmitterComponent = this;
                        connection.receiverComponent = component;
                        outgoingConnections.Add(connection);
                        component.incomingConnections.Add(connection);
                        if (connection.laserInstance == null)
                        {
                            connection.laserInstance = Instantiate(laserPrefab, body.aimOriginTransform.position, Quaternion.identity);
                        }
                        if (connection.laserInstance != null)
                        {
                            if (!connection.laserInstance.activeInHierarchy)
                            {
                                connection.laserInstance.SetActive(true);
                            }
                            if (!connection.laserInstance.transform.parent !=  body.aimOriginTransform)
                            {
                                connection.laserInstance.transform.parent = body.aimOriginTransform;
                            }
                            connection.laserLineComponent = connection.laserInstance.GetComponent<LineRenderer>();
                        }
                    }
                }
            }
            private void CheckConnectionForCollision(AreaDenialConnection connection)
            {
                if (body == null || connection == null || connection.receiverComponent == null || connection.receiverComponent.body == null)
                {
                    return;
                }
                Vector3 connectionStart = body.aimOriginTransform.position;
                Vector3 connectionEnd = connection.receiverComponent.body.aimOriginTransform.position;
                TeamMask teamMask = TeamMask.GetEnemyTeams(body.teamComponent.teamIndex);
                foreach (TeamComponent.Team team in TeamComponent.teamsList)
                {
                    TeamIndex teamIndex = team.teamIndex;
                    if (!teamMask.HasTeam(teamIndex))
                    {
                        continue;
                    }
                    foreach (TeamComponent teamComponent in TeamComponent.GetTeamMembers(teamIndex))
                    {
                        CharacterBody targetBody = teamComponent.body;
                        if (targetBody == null || targetBody.healthComponent == null || !targetBody.healthComponent.alive)
                        {
                            continue;
                        }
                        float dist = DistanceToLineSegment(targetBody.corePosition, connectionStart, connectionEnd);
                        if (dist < AreaDenialConnection.connectionRadius)
                        {
                            TriggerConnectionDamage(connection, targetBody);
                            return;
                        }
                    }
                }
            }
            private float DistanceToLineSegment(Vector3 targetPosition, Vector3 connectionStart, Vector3 connectionEnd)
            {
                Vector3 line = connectionEnd - connectionStart;
                float lineLength = line.magnitude;
                float t = Mathf.Clamp01(Vector3.Dot(targetPosition - connectionStart, line) / (lineLength * lineLength));
                Vector3 closestPoint = connectionStart + t * line;
                return Vector3.Distance(targetPosition, closestPoint);
            }
            private void TriggerConnectionDamage(AreaDenialConnection connection, CharacterBody target)
            {
                if (body == null)
                {
                    return;
                }
                DamageInfo damageInfo = new DamageInfo
                {
                    damage = body.damage * AreaDenialConnection.damageCoefficient,
                    attacker = body.gameObject,
                    inflictor = body.gameObject,
                    position = target.corePosition,
                    crit = body.RollCrit(),
                    force = Vector3.zero,
                    damageType = DamageType.Generic,
                    procCoefficient = 1f,
                    damageColorIndex = DamageColorIndex.Default
                };
                EffectManager.SpawnEffect(explosionEffect, new EffectData { origin = target.corePosition, rotation = target.transform.rotation, scale = 1f }, true);
                target.healthComponent.TakeDamage(damageInfo);
                float rootDuration = tripwireRootDuration.Value;
                float slowDuration = tripwireSlowDuration.Value;
                if (slowDuration > 0f)
                {
                    target.AddTimedBuff(RoR2Content.Buffs.Slow60, slowDuration);
                }
                if (rootDuration > 0f)
                {
                    target.AddTimedBuff(RoR2Content.Buffs.LunarSecondaryRoot, rootDuration);
                    if (target.rigidbody != null)
                    {
                        target.rigidbody.velocity = Vector3.zero;
                    }
                }
                if (target.characterMotor != null)
                {
                    target.characterMotor.velocity = Vector3.zero;
                }
                connection.state = AreaDenialConnection.ConnectionState.PostTrigger;
                connection.postTriggerTimer = 0f;
                if (connection.laserInstance != null)
                {
                    connection.laserInstance.SetActive(false);
                }
            }
            private void BreakConnection(AreaDenialConnection connection)
            {
                if (connection != null)
                {
                    outgoingConnections.Remove(connection);
                    connection.receiverComponent.incomingConnections.Remove(connection);
                }
                if (connection.laserInstance != null)
                {
                    Destroy(connection.laserInstance);
                }
            }
            private bool IsConnectionValid(AreaDenialComponent receiverComponent, bool newConnection)
            {
                AreaDenialComponent transmitterComponent = this;
                if (transmitterComponent == receiverComponent)
                {
                    return false;
                }
                if (transmitterComponent == null || receiverComponent == null)
                {
                    return false;
                }
                if (transmitterComponent.index >= receiverComponent.index)
                {
                    return false;
                }
                if (newConnection)
                {
                    if (Connections >= maxConnections || receiverComponent.Connections >= maxConnections)
                    {
                        return false;
                    }
                    foreach (AreaDenialConnection connection in outgoingConnections)
                    {
                        if (connection.receiverComponent == receiverComponent)
                        {
                            return false;
                        }
                    }
                }
                CharacterBody receiverBody = receiverComponent.body;
                if (body == null || receiverBody == null)
                {
                    return false;
                }
                if (body.healthComponent == null || body.healthComponent.alive == false || receiverBody.healthComponent == null || receiverBody.healthComponent.alive == false)
                {
                    return false;
                }
                Vector3 transmitterPos = body.aimOriginTransform.position;
                Vector3 receiverPos = receiverBody.aimOriginTransform.position;
                float dist = Vector3.Distance(transmitterPos, receiverPos);
                if (dist > maxDistance)
                {
                    return false;
                }
                Vector3 direction = (receiverPos - transmitterPos).normalized;
                if (Physics.Raycast(transmitterPos, direction, dist, LayerIndex.world.mask, QueryTriggerInteraction.Ignore))
                {
                    return false;
                }
                return true;
            }
        }
    }
}
