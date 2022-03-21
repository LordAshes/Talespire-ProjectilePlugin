using BepInEx;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace LordAshes
{
    public partial class ProjectilePlugin : BaseUnityPlugin
    {
        public class Projectile
        {
            public string name { get; set; }
            public string iconName { get; set; }
            public string assetBundleName { get; set; }
            public int targets { get; set; }
            public string pathType { get; set; } = "Arc";
            public string pathParameterString { get; set; } = "";
            public string targetArea { get; set; } = "SPELL";
            public List<P3> sources { get; set; }
        }
    }
}
