using System;
using System.Linq;
using System.Reflection;
using HG;
using RoR2;
using RoR2.Audio;
using RoR2.CharacterAI;
using RoR2.Projectile;
using UnityEngine;
using UnityEngine.Networking;

namespace EnemyAbilities
{

    // !!! This is not my code !!! The following was taken from a version of EnemiesPlus.Utils, FULL credit to .score
    public static class EnemiesPlusUtils
    {
        public static void ReorderSkillDrivers(this GameObject master, AISkillDriver targetSkill, int targetIdx)
        {
            var c = master.GetComponents<AISkillDriver>();
            master.ReorderSkillDrivers(c, Array.IndexOf(c, targetSkill), targetIdx);
        }
        public static void ReorderSkillDrivers(this GameObject master, AISkillDriver[] skills, int currentIdx, int targetIdx)
        {
            if (currentIdx < 0 || currentIdx >= skills.Length)
            {
                Log.Error($"{currentIdx} index not found or out of range. Must be less than {skills.Length}");
                return;
            }
            var targetName = skills[currentIdx].customName;

            if (targetIdx < 0 || targetIdx >= skills.Length)
            {
                Log.Error($"Unable to reorder skilldriver {targetName} into position {targetIdx}. target must be less than {skills.Length}");
                return;
            }

            if (targetIdx == currentIdx)
            {
                Log.Warning($"Skilldriver {targetName} already has the target index of {targetIdx}");
                return;
            }

            // reference to original might get nulled so they need to be re-added later
            var overrides = skills.Where(s => s.nextHighPriorityOverride != null)
                .ToDictionary(
                s => s.customName,
                s => s.nextHighPriorityOverride.customName);

            // move down. this modifies the order.
            if (targetIdx > currentIdx)
            {
                master.AddComponentCopy(skills[currentIdx]);
                UnityEngine.Object.DestroyImmediate(skills[currentIdx]);
            }

            // anything before the target idx can be ignored.
            // move all elements after the target target skilldriver without modifying order
            for (var i = targetIdx; i < skills.Length; i++)
            {
                if (i != currentIdx)
                {
                    // start with skill that currently occupies target idx
                    master.AddComponentCopy(skills[i]);
                    UnityEngine.Object.DestroyImmediate(skills[i]);
                }
            }

            // sanity check
            skills = master.GetComponents<AISkillDriver>();
            var newTarget = skills.FirstOrDefault(s => s.customName == targetName);
            if (newTarget != null && Array.IndexOf(skills, newTarget) == targetIdx)
                Log.Debug($"Successfully set {targetName} to {targetIdx}");
            else
                Log.Error($"Done fucked it up on {targetName} with {targetIdx}");

            // restore overrides
            if (overrides.Any())
            {
                for (var i = 0; i < skills.Length; i++)
                {
                    var skill = skills[i];
                    if (skill && overrides.TryGetValue(skill.customName, out var target))
                    {
                        var skillComponent = skills.FirstOrDefault(s => s.customName == target);
                        if (skillComponent == null)
                        {
                            Log.Error($"Unable to reset skill override for {skill.customName} targeting {target}");
                        }
                        else
                        {
                            skill.nextHighPriorityOverride = skillComponent;
                            Log.Debug($"successfully reset override for {skill.customName} targeting {target}");
                        }
                    }
                }
            }
        }
        public static T GetCopyOf<T>(this Component comp, T other) where T : Component
        {
            var type = comp.GetType();
            if (type != other.GetType())
                return null; // type mis-match

            var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Default | BindingFlags.DeclaredOnly;
            var pinfos = type.GetProperties(flags);
            foreach (var pinfo in pinfos)
            {
                if (pinfo.CanWrite)
                {
                    try
                    {
                        pinfo.SetValue(comp, pinfo.GetValue(other, null), null);
                    }
                    catch { } // In case of NotImplementedException being thrown. For some reason specifying that exception didn't seem to catch it, so I didn't catch anything specific.
                }
            }

            var finfos = type.GetFields(flags);
            foreach (var finfo in finfos)
            {
                finfo.SetValue(comp, finfo.GetValue(other));
            }

            return comp as T;
        }

        public static T AddComponentCopy<T>(this GameObject go, T toAdd) where T : Component
        {
            return go.AddComponent<T>().GetCopyOf(toAdd);
        }

        //ctrl+c, ctrl+v of ProjectileImpactExplosion with a couple extra bits and pieces. lazy, but it works
        public class ProjectileDetonateOnImpact : ProjectileExplosion, IProjectileImpactBehavior
        {
            public enum TransformSpace
            {
                World,
                Local,
                Normal
            }

            private Vector3 impactNormal = Vector3.up;

            public GameObject impactEffect;

            public NetworkSoundEventDef lifetimeExpiredSound;

            public float offsetForLifetimeExpiredSound;

            public bool destroyOnEnemy = true;

            public bool detonateOnEnemy;

            public bool detonateOnWorld;

            public bool destroyOnWorld;

            public bool destroyOnDistance;

            public float maxDistance;

            private float maxDistanceSqr;

            public bool impactOnWorld = true;

            public bool timerAfterImpact;

            public float lifetime;

            public float lifetimeAfterImpact;

            public float lifetimeRandomOffset;

            private float stopwatch;

            public uint minimumPhysicsStepsToKeepAliveAfterImpact;

            private float stopwatchAfterImpact;

            private bool hasImpact;

            private bool hasPlayedLifetimeExpiredSound;

            public TransformSpace transformSpace;

            private Vector3 startPos;

            public bool explodeOnLifeTimeExpiration;
            public bool nullifyExplosions = false;

            public override void Awake()
            {
                base.Awake();
                lifetime += UnityEngine.Random.Range(0f, lifetimeRandomOffset);
            }

            protected void Start()
            {
                startPos = base.transform.position;
                maxDistanceSqr = maxDistance * maxDistance;
            }

            protected void FixedUpdate()
            {
                stopwatch += Time.fixedDeltaTime;
                if (!NetworkServer.active && !projectileController.isPrediction)
                {
                    return;
                }
                if (explodeOnLifeTimeExpiration && alive && stopwatch >= lifetime)
                {
                    explosionEffect = impactEffect ?? explosionEffect;
                    if (!nullifyExplosions)
                    {
                        Detonate();
                    }
                }
                if (timerAfterImpact && hasImpact)
                {
                    stopwatchAfterImpact += Time.fixedDeltaTime;
                }
                bool num = stopwatch >= lifetime;
                bool flag = timerAfterImpact && stopwatchAfterImpact > lifetimeAfterImpact;
                bool flag2 = (bool)projectileHealthComponent && !projectileHealthComponent.alive;
                bool flag3 = false;
                if (destroyOnDistance && (base.transform.position - startPos).sqrMagnitude >= maxDistanceSqr)
                {
                    flag3 = true;
                }
                if (num || flag || flag2 || flag3)
                {
                    alive = false;
                }
                if (timerAfterImpact && hasImpact && minimumPhysicsStepsToKeepAliveAfterImpact != 0)
                {
                    minimumPhysicsStepsToKeepAliveAfterImpact--;
                    alive = true;
                }
                if (alive && !hasPlayedLifetimeExpiredSound)
                {
                    bool flag4 = stopwatch > lifetime - offsetForLifetimeExpiredSound;
                    if (timerAfterImpact)
                    {
                        flag4 |= stopwatchAfterImpact > lifetimeAfterImpact - offsetForLifetimeExpiredSound;
                    }
                    if (flag4)
                    {
                        hasPlayedLifetimeExpiredSound = true;
                        if (NetworkServer.active && (bool)lifetimeExpiredSound)
                        {
                            PointSoundManager.EmitSoundServer(lifetimeExpiredSound.index, base.transform.position);
                        }
                    }
                }
                if (!alive)
                {
                    explosionEffect = impactEffect ?? explosionEffect;
                    if (!nullifyExplosions)
                    {
                        Detonate();
                    }
                }
            }

            public override Quaternion GetRandomDirectionForChild()
            {
                Quaternion randomChildRollPitch = GetRandomChildRollPitch();
                return transformSpace switch
                {
                    TransformSpace.Local => base.transform.rotation * randomChildRollPitch,
                    TransformSpace.Normal => Quaternion.FromToRotation(Vector3.forward, impactNormal) * randomChildRollPitch,
                    _ => randomChildRollPitch,
                };
            }

            public void OnProjectileImpact(ProjectileImpactInfo impactInfo)
            {
                if (nullifyExplosions)
                {
                    return;
                }
                if (!alive)
                {
                    return;
                }
                Collider collider = impactInfo.collider;
                impactNormal = impactInfo.estimatedImpactNormal;
                if (!collider)
                {
                    return;
                }
                DamageInfo damageInfo = new DamageInfo();
                if ((bool)projectileDamage)
                {
                    damageInfo.damage = projectileDamage.damage;
                    damageInfo.crit = projectileDamage.crit;
                    damageInfo.attacker = (projectileController.owner ? projectileController.owner.gameObject : null);
                    damageInfo.inflictor = base.gameObject;
                    damageInfo.damageType = projectileDamage.damageType;
                    damageInfo.inflictor = base.gameObject;
                    damageInfo.position = impactInfo.estimatedPointOfImpact;
                    damageInfo.force = projectileDamage.force * base.transform.forward;
                    damageInfo.procChainMask = projectileController.procChainMask;
                    damageInfo.procCoefficient = projectileController.procCoefficient;
                    damageInfo.damageType = projectileDamage.damageType;
                }
                HurtBox component = collider.GetComponent<HurtBox>();
                if ((bool)component)
                {
                    if (destroyOnEnemy)
                    {
                        HealthComponent healthComponent = component.healthComponent;
                        if ((bool)healthComponent)
                        {
                            if (healthComponent.gameObject == projectileController.owner || ((bool)projectileHealthComponent && healthComponent == projectileHealthComponent))
                            {
                                return;
                            }
                            alive = false;
                        }
                    }
                    else if (detonateOnEnemy)
                    {
                        HealthComponent healthComponent2 = component.healthComponent;
                        if ((bool)healthComponent2 && healthComponent2.gameObject != projectileController.owner && healthComponent2 != projectileHealthComponent)
                        {
                            DetonateNoDestroy();
                        }
                    }
                }
                else if (detonateOnWorld)
                {
                    DetonateNoDestroy();
                }
                else if (destroyOnWorld)
                {
                    alive = false;
                }
                hasImpact = (bool)component || impactOnWorld;
                if (NetworkServer.active && hasImpact)
                {
                    GlobalEventManager.instance.OnHitAll(damageInfo, collider.gameObject);
                }
            }
        }
    }
    public static class Utils
    {

    }
}
