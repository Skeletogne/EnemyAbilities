using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace EnemyAbilities.Abilities
{
    //will probably do something with this at some point
    public class BaseModule : MonoBehaviour
    {
        public virtual void Awake()
        {
            Log.Debug($"Loading module {this.GetType().Name}");
        }
    }
}