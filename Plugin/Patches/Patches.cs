using BepInEx;
using HarmonyLib;

using System;
using System.Reflection;
using Unity.Mathematics;
using UnityEngine;

namespace LordAshes
{
    public partial class ProjectilePlugin : BaseUnityPlugin
    {
        [HarmonyPatch(typeof(MovableBoardAsset), "Pickup")]
        public static class Patches
        {
            public static bool Prefix()
            {
                Debug.Log("Projectile Plugin: Pickup Detected");
                if(self.fastSelectionActive!=null)
                {
                    Debug.Log("Projectile Plugin: Pickup Detected During Fast Selection (With "+ (self.selectTarget.Count-1)+" of "+ self.fastSelectionActive.targets+" Entries)");
                    self.target(LocalClient.SelectedCreatureId, self.fastSelectionActive);
                }
                return true;
            }
        }
    }
}
