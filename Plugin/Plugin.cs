using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;


namespace LordAshes
{
    [BepInPlugin(Guid, Name, Version)]
    [BepInDependency(LordAshes.FileAccessPlugin.Guid)]
    [BepInDependency(LordAshes.AssetDataPlugin.Guid)]
    [BepInDependency(RadialUI.RadialUIPlugin.Guid)]
    public partial class ProjectilePlugin : BaseUnityPlugin
    {
        // Plugin info
        public const string Name = "Ping Plug-In";              
        public const string Guid = "org.lordashes.plugins.projectile";
        public const string Version = "2.0.0.0";

        public class P3
        {
            public float x { get; set; }
            public float y { get; set; }
            public float z { get; set; }

            public P3() { ;  }

            public P3(Vector3 source)
            {
                this.x = source.x;
                this.y = source.y;
                this.z = source.z;
            }

            public Vector3 ToVector3()
            {
                return new Vector3(this.x, this.y, this.z);
            }
        }

        // Configuration
        private int animationLength = 30;

        private List<Projectile> projectiles = new List<Projectile>();

        private Dictionary<GameObject, List<Vector3>> animationStack = new Dictionary<GameObject, List<Vector3>>();

        private Dictionary<string, Type> pathBuilders = new Dictionary<string, Type>();

        private List<P3> selectTarget = new List<P3>();
        private string attackSelected = "";
        private CreatureGuid attackerCid = CreatureGuid.Empty;
        private Projectile fastSelectionActive = null;

        private static ProjectilePlugin self = null;

        void Awake()
        {
            UnityEngine.Debug.Log("Projectile Plugin: "+this.GetType().AssemblyQualifiedName+" is Active.");

            self = this;

            animationLength = Config.Bind("Settings", "Projectile Frame Length", 30).Value;

            foreach(string projectileFile in FileAccessPlugin.File.Catalog().Where(file => file.ToUpper().EndsWith(".PROJECTILE")))
            {
                UnityEngine.Debug.Log("Projectile Plugin: Found " + projectileFile);
                string json = FileAccessPlugin.File.ReadAllText(projectileFile);
                UnityEngine.Debug.Log("Projectile Plugin: " + json);
                projectiles.Add(JsonConvert.DeserializeObject<Projectile>(json));
            }

            RadialUI.RadialSubmenu.EnsureMainMenuItem(ProjectilePlugin.Guid, RadialUI.RadialSubmenu.MenuType.character, "Projectiles", FileAccessPlugin.Image.LoadSprite("Projectile.png"));

            foreach(Projectile projectile in projectiles)
            {
                for (int t = 1; t <= projectile.targets; t++)
                {
                    int tgt = t;
                    if (tgt == 1)
                    {
                        RadialUI.RadialSubmenu.CreateSubMenuItem(ProjectilePlugin.Guid, projectile.name + " (Target " + tgt + " of " + projectile.targets + ")", FileAccessPlugin.Image.LoadSprite(projectile.iconName), (c, n, i) => { attackerCid = LocalClient.SelectedCreatureId; target(c, projectile); }, true, () => MenuDecide(projectile, tgt));
                    }
                    else
                    {
                        RadialUI.RadialSubmenu.CreateSubMenuItem(ProjectilePlugin.Guid, projectile.name + " (Target " + tgt + " of "+ projectile.targets + ")", FileAccessPlugin.Image.LoadSprite(projectile.iconName), (c,n,i)=> { target(c, projectile); }, true, () => MenuDecide(projectile, tgt));
                    }
                }
            }

            foreach (string pathBuilderFile in FileAccessPlugin.File.Catalog(true).Where(file => file.ToUpper().Contains("_PATHBUILDER.DLL")))
            {
                string part1 = pathBuilderFile.Substring(pathBuilderFile.LastIndexOf("(") + 1).Trim();
                part1 = part1.Substring(0, part1.Length - 1);
                string part2 = pathBuilderFile.Substring(0,pathBuilderFile.LastIndexOf("(")).Trim();
                string fullPath = part1 + part2;
                Assembly aby = System.Reflection.Assembly.LoadFrom(fullPath);
                string builderName = System.IO.Path.GetFileNameWithoutExtension(fullPath);
                builderName = builderName.Substring(0, builderName.LastIndexOf("_"));
                foreach (Type type in aby.GetTypes())
                {
                    foreach (MethodInfo method in type.GetMethods())
                    {
                        if (method.Name == "MakePath") { pathBuilders.Add(builderName.ToUpper(), type); }
                    }
                }
            }
            foreach (KeyValuePair<string, Type> builder in pathBuilders)
            {
                UnityEngine.Debug.Log("Projectile Plugin: PathBuilder " + builder.Key + " => " + Convert.ToString(builder.Value));
            }

            UnityEngine.Debug.Log("Projectile Plugin: Subscribing to projectile request.");
            AssetDataPlugin.Subscribe(ProjectilePlugin.Guid, HandleRequest);

            var harmony = new Harmony(Guid);
            harmony.PatchAll();

            Utility.PostOnMainPage(this.GetType());
        }

        private bool MenuDecide(Projectile p, int t)
        {
            if (selectTarget.Count == 0)
            {
                CreatureBoardAsset asset = null;
                CreaturePresenter.TryGetAsset(LocalClient.SelectedCreatureId, out asset);
                if (asset != null)
                {
                    UnityEngine.Debug.Log("Projectile Plugin: Adding Attacker To Targets List");
                    selectTarget.Add(new P3(asset.GetHook(CreatureBoardAsset.HookTransform.SPELL).position));
                }
            }
            if (attackSelected != p.name && attackSelected != "") { UnityEngine.Debug.Log("Projectile Plugin: Menu Check: " + p.name+" (" + t + ") => Rejected For Attack Name"); return false; }
            if (t!=selectTarget.Count) { UnityEngine.Debug.Log("Projectile Plugin: Menu Check: " + p.name +" (" + t + ") => Rejected For Target Count"); return false; }
            UnityEngine.Debug.Log("Projectile Plugin: Menu Check: " + p.name + " ("+t+") => OK");
            return true;
        }

        private void target(CreatureGuid cid, Projectile p)
        {
            if (attackSelected == "")
            {
                UnityEngine.Debug.Log("Projectile Plugin: Projectile Attack '"+p.name+"' Selected");
                attackSelected = p.name;
            }
            UnityEngine.Debug.Log("Projectile Plugin: Target Selected");
            if (cid == attackerCid)
            {
                UnityEngine.Debug.Log("Projectile Plugin: Fast Selection Activated");
                fastSelectionActive = p;
            }
            else
            {
                UnityEngine.Debug.Log("Projectile Plugin: Adding Target");
                CreatureBoardAsset asset = null;
                CreaturePresenter.TryGetAsset(cid, out asset);
                if (asset != null)
                {
                    switch (p.targetArea.ToUpper())
                    {
                        case "HEAD":
                            selectTarget.Add(new P3(asset.HookHead.position));
                            break;
                        case "CAST":
                        case "SPELL":
                            selectTarget.Add(new P3(asset.GetHook(CreatureBoardAsset.HookTransform.SPELL).position));
                            break;
                        case "TORCH":
                            selectTarget.Add(new P3(asset.GetHook(CreatureBoardAsset.HookTransform.TORCH).position));
                            break;
                        default:
                            selectTarget.Add(new P3(asset.GetHook(CreatureBoardAsset.HookTransform.HIT).position));
                            break;
                    }
                }
            }
            if(selectTarget.Count>p.targets)
            {
                fire(p);
            }
            else if(fastSelectionActive!=null)
            {
                SystemMessage.DisplayInfoText(p.name+"\r\nSelect Target "+selectTarget.Count+" of "+p.targets);
            }
        }

        private void fire(Projectile p)
        {
            UnityEngine.Debug.Log("Projectile Plugin: Targetting Complete");
            p.sources = selectTarget;
            AssetDataPlugin.SendInfo(ProjectilePlugin.Guid, JsonConvert.SerializeObject(p));
            attackSelected = "";
            attackerCid = CreatureGuid.Empty;
            selectTarget.Clear();
            fastSelectionActive = null;
        }

        void Update()
        {
            if (Utility.isBoardLoaded())
            {
                if (animationStack.Count > 0)
                {
                    for(int k=0; k<animationStack.Count; k++)
                    {
                        GameObject projectile = animationStack.ElementAt(k).Key;
                        Vector3 pt1 = animationStack.ElementAt(k).Value.ElementAt(0);
                        projectile.transform.localPosition = pt1;
                        Vector3 pt2 = (animationStack.ElementAt(k).Value.Count() > 1) ? animationStack.ElementAt(k).Value.ElementAt(1) : Vector3.zero;
                        if ((pt2 != Vector3.zero) && (pt2.ToString()!=pt1.ToString()))
                        {
                            Vector3 delta = new Vector3(pt2.x - pt1.x, 0, pt2.z - pt1.z);
                            float angle = GetAngle(delta);
                            Debug.Log("Projectile Plugin: Delta = " + pt2 + " - " + pt1 + " = " + delta + " => Angle = " + angle);
                            projectile.transform.localRotation = Quaternion.Euler(new Vector3(0, angle, 0));
                        }
                        Debug.Log("Projectile Plugin: Moving "+projectile.name+" To "+ pt1 +" ("+ animationStack.ElementAt(k).Value.Count()+" Steps Remaining)");
                        animationStack.ElementAt(k).Value.RemoveAt(0);
                        if (animationStack.ElementAt(k).Value.Count == 0) 
                        {
                            Debug.Log("Projectile Plugin: Removing " + projectile.name);
                            GameObject.Destroy(projectile);
                            animationStack.Remove(animationStack.ElementAt(k).Key); 
                            k--; 
                        }
                    }
                }
            }
            else
            {
                if (animationStack.Count > 0) { animationStack.Clear(); }
            }
        }

        private void HandleRequest(AssetDataPlugin.DatumChange change)
        {
            try
            {
                Projectile p = JsonConvert.DeserializeObject<Projectile>(change.value.ToString());
                Debug.Log("Projectile Plugin: Getting Projectile Name " + p.name);

                List<P3> targets = p.sources;

                Debug.Log("Projectile Plugin: Getting Projectile Prefab " + p.assetBundleName);
                AssetBundle ab = FileAccessPlugin.AssetBundle.Load(p.assetBundleName+"/"+ p.assetBundleName);
                for (int t = 1; t < targets.Count; t++)
                {
                    System.Guid guid = System.Guid.NewGuid();
                    Debug.Log("Projectile Plugin: Creating " + p.name+" Target "+t);
                    GameObject go = new GameObject();
                    go.name = guid.ToString();
                    GameObject projectileItem = GameObject.Instantiate(ab.LoadAsset<GameObject>(p.assetBundleName));
                    projectileItem.transform.parent = go.transform;
                    Debug.Log("Projectile Plugin: Adding Animation For " +guid.ToString() + " From " + Convert.ToString(targets[0].ToVector3()) + " To " + Convert.ToString(targets[t].ToVector3()) + " In " + animationLength + " Frames");
                    string builderName = p.pathType.ToUpper();
                    if (!pathBuilders.ContainsKey(builderName))
                    {
                        Debug.Log("Projectile Plugin: Did not file Path Builder '"+builderName+"'. Using Path Builder '" + pathBuilders.ElementAt(0).Key+"' Instead.");
                        builderName = pathBuilders.ElementAt(0).Key; 
                    }
                    else
                    {
                        Debug.Log("Projectile Plugin: Using Path Builder " + builderName);
                    }
                    if (pathBuilders[builderName] != null)
                    {
                        List<Vector3> path = (List<Vector3>)pathBuilders[builderName].GetMethod("MakePath").Invoke(null, new object[] { targets[0].ToVector3(), targets[t].ToVector3(), p.pathParameterString, animationLength });
                        Vector3 delayPos = targets[0].ToVector3();
                        for(int d=0; d<(animationLength*(t-1)/10); d++)
                        {
                            path.Insert(0, delayPos);
                        }
                        go.transform.position = new Vector3(path.ElementAt(0).x, path.ElementAt(0).y, path.ElementAt(0).z+t);
                        animationStack.Add(go, path);
                    }
                    else
                    {
                        Debug.Log("Projectile Plugin: Builder Is Null");
                    }
                }
                ab.Unload(false);
            }
            catch(Exception x)
            {
                Debug.Log("Projectile Plugin: Exception Processing Projectile");
                Debug.LogException(x);
            }
        }

        private float GetAngle(Vector3 delta)
        {
            double angle = 0f;
            string quadrent = ((delta.x >= 0) ? "+" : "-") + ((delta.z >= 0) ? "+" : "-");
            switch(quadrent)
            {
                case "--":
                    angle = (Math.Atan(delta.x / delta.z) * 180.0 / Math.PI);
                    angle = (angle + 180);
                    break;
                case "-+":
                    angle = (Math.Atan(delta.x / delta.z) * 180.0 / Math.PI);
                    break;
                case "+-":
                    angle = (Math.Atan(delta.x / delta.z) * 180.0 / Math.PI);
                    angle = (angle + 180);
                    break;
                case "++":
                    angle = (Math.Atan(delta.x / delta.z) * 180.0 / Math.PI);
                    break;
            }
            return (float)angle;
        }
    }
}
