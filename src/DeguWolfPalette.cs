using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using BepInEx.Bootstrap;
using HarmonyLib;
using UnityEngine;

namespace SirrohDegu
{
    // Clean-room visual pipeline. This is the ONLY material provider for Degu.
    // No hard-coded Overhaul version, no patch of vanilla Wolf, no global
    // Material mutation, no per-animal allocated textures or materials.
    internal static class DeguWolfPalette
    {
        internal const string SkinKey="furRandomizerSet";
        internal const string ActiveKey="furRandomizerSetActive";
        internal const string EyeKey="sirroh.wolf.eye.v1";
        internal const string LevelKey="sirroh.pet.level.v1";
        internal const string ElementKey="sirroh.pet.element.v1";
        internal const string RunesKey="sirroh.wolf.showrunes.v1";
        internal const string VisualName="SirrohDegu_StableVisual";
        private static readonly string[] Names=
            {"Default","Albino","Black","DarkBrown","Grey","Blood","Midnight"};
        private static readonly Material[] Original=new Material[7];
        private static readonly Material[] DeguCopy=new Material[7];
        private static readonly HashSet<string> Reported=new HashSet<string>();
        private static MethodInfo eyeMethod,loadMethod,prepareMethod;
        private static float nextAttempt;
        private static bool everReady;
        private static bool firstApplied;

        private static void Note(string text,bool warning=false)
        {
            if(!Reported.Add(text))return;
            if(warning)DeguPlugin.Log?.LogWarning("DEGU_CORE: "+text);
            else DeguPlugin.Log?.LogInfo("DEGU_CORE: "+text);
        }

        internal static SkinnedMeshRenderer Renderer(GameObject obj)
        {
            if(!obj)return null;
            foreach(var r in obj.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if(r && r.name==VisualName)return r;
            return null;
        }

        private static Assembly OverhaulAssembly()
        {
            if(Chainloader.PluginInfos.TryGetValue("sirro.valheim.pracafizyczna",out var p))
                return p.Instance?.GetType().Assembly;
            return AccessTools.TypeByName("Sirro.PracaFizyczna.WolfVisuals")?.Assembly;
        }

        private static int ImportNativeCatalog(Assembly assembly)
        {
            if(assembly==null)return 0;
            Type catalog=assembly.GetType("Sirro.PracaFizyczna.MaterialCatalog",false);
            if(catalog==null)return 0;
            var load=AccessTools.Method(catalog,"Load");
            if(load!=null)loadMethod=load;
            var f=AccessTools.Field(catalog,"Variants");
            IDictionary dic=f?.GetValue(null) as IDictionary;
            Array arr=dic!=null && dic.Contains("Wolf")?dic["Wolf"] as Array:null;
            if(arr==null || arr.Length!=7)return 0;
            int found=0;
            for(int i=0;i<7;i++)
            {
                object variant=arr.GetValue(i);
                if(variant==null)continue;
                Material mat=AccessTools.Field(variant.GetType(),"Main")?.GetValue(variant) as Material;
                if(!mat)continue;
                Original[i]=mat;found++;
            }
            return found;
        }

        private static int ImportLoadedAssetBundles()
        {
            // Native MaterialCatalog.Load also borrows AssetBundles. This
            // independent read-only fallback works if its own dictionary was
            // intentionally left empty because standalone Fur Randomizer loaded
            // the actual Wolf material bundle.
            int found=0;
            foreach(AssetBundle bundle in AssetBundle.GetAllLoadedAssetBundles())
            {
                if(!bundle)continue;
                string[] paths;
                try{paths=bundle.GetAllAssetNames();}
                catch(Exception){continue;}
                foreach(string path in paths)
                {
                    if(!path.EndsWith(".mat",StringComparison.OrdinalIgnoreCase))continue;
                    string[] bits=path.Replace('\\','/').Split('/');
                    if(bits.Length<3 || !bits[bits.Length-3].Equals("Wolf",StringComparison.OrdinalIgnoreCase))continue;
                    for(int i=0;i<7;i++)
                    {
                        if(Original[i] || !bits[bits.Length-2].Equals(Names[i],StringComparison.OrdinalIgnoreCase))continue;
                        try
                        {
                            Material material=bundle.LoadAsset<Material>(path);
                            if(material){Original[i]=material;found++;}
                        }catch(Exception ex){Note("Wolf bundle item "+Names[i]+": "+ex.GetBaseException().Message,true);}
                        break;
                    }
                }
            }
            return found;
        }

        private static bool Ready(bool allowReload=true)
        {
            int have=0;
            foreach(Material m in Original)if(m)have++;
            if(have==7 && eyeMethod!=null)return true;
            if(Time.unscaledTime<nextAttempt)return have>0 && eyeMethod!=null;
            nextAttempt=Time.unscaledTime+5f;
            Assembly asm=OverhaulAssembly();
            if(asm==null){Note("Overhaul assembly is not loaded. Wolf eyes and runes are unavailable.",true);return false;}
            Type visuals=asm.GetType("Sirro.PracaFizyczna.WolfVisuals",false);
            if(visuals!=null)
            {
                eyeMethod=AccessTools.Method(visuals,"ApplyProperties");
                prepareMethod=AccessTools.Method(visuals,"Prepare");
            }
            int native=ImportNativeCatalog(asm);
            if(have==0 || native<7)
            {
                // Safe read of already loaded bundles, regardless of the
                // registered standalone FurRandomizer GUID or version.
                ImportLoadedAssetBundles();
            }
            have=0;
            foreach(Material m in Original)if(m)have++;
            if(have<7 && allowReload && loadMethod!=null)
            {
                try
                {
                    loadMethod.Invoke(null,null);
                    ImportNativeCatalog(asm);
                    ImportLoadedAssetBundles();
                }
                catch(Exception ex){Note("native Wolf catalogue Load: "+ex.GetBaseException().Message,true);}
            }
            have=0;
            foreach(Material m in Original)if(m)have++;
            if(have>0 && !everReady)
            {
                everReady=true;
                Note("Wolf palette connected; materials="+have+"/7 from native catalog or loaded AssetBundle; eye bridge="+(eyeMethod!=null));
            }
            if(have<7)
                Note("Only "+have+"/7 Wolf coat materials available; missing indices are not replaced.",true);
            if(eyeMethod==null)
                Note("WolfVisuals.ApplyProperties not found; eyes/runes cannot be shown.",true);
            return have>0 && eyeMethod!=null;
        }

        internal static bool Apply(GameObject target,int coat,int eye,int element)
        {
            if(coat<0 || coat>=7 || eye<1 || eye>6)return false;
            if(!Ready())return false;
            var skin=Renderer(target);
            if(!skin || !skin.sharedMesh)return false;
            Material source=Original[coat];
            if(!source)return false;
            var originalSlots=skin.sharedMaterials;
            if(originalSlots==null || originalSlots.Length<2)return false;
            if(!DeguCopy[coat])
            {
                // Exactly seven cached Degu-specific material instances. Use
                // actual Wolf albedo/material parameters; change only the copy
                // to match the known-good original Valheim Wolf shader.
                var copy=new Material(source);
                copy.name="Degu Wolf coat "+Names[coat];
                Shader native=originalSlots[0]?originalSlots[0].shader:null;
                if(native && copy.shader!=native)copy.shader=native;
                if(prepareMethod!=null)
                {
                    try{prepareMethod.Invoke(null,new object[]{copy});}
                    catch(Exception ex){Note("Wolf shader Prepare: "+ex.GetBaseException().Message,true);}
                }
                DeguCopy[coat]=copy;
            }
            Material material=DeguCopy[coat];
            if(originalSlots[0]!=material || originalSlots[1]!=material)
            {
                originalSlots[0]=material;
                originalSlots[1]=material;
                skin.sharedMaterials=originalSlots;
            }
            try
            {
                eyeMethod.Invoke(null,new object[]{skin,material,eye,Mathf.Clamp(element,0,5)});
                var pb=new MaterialPropertyBlock();
                skin.GetPropertyBlock(pb,0);
                skin.SetPropertyBlock(pb,1);
                if(!firstApplied)
                {
                    firstApplied=true;
                    Note("FIRST WOLF VISUAL APPLIED: coat="+coat+" eye="+eye+" element="+element+
                        " material="+material.name+" atlas="+(material.mainTexture?material.mainTexture.name:"none"));
                }
                return true;
            }
            catch(Exception ex)
            {
                Note("WolfVisuals.ApplyProperties failed: "+ex.GetBaseException().Message,true);
                return false;
            }
        }

        internal static bool Matches(GameObject obj,int coat)
        {
            var r=Renderer(obj);
            return r && coat>=0 && coat<7 && DeguCopy[coat] &&
                r.sharedMaterials.Length>=2 && r.sharedMaterials[0]==DeguCopy[coat];
        }

        internal static void Register(GameObject prefab)
        {
            if(!prefab)return;
            // Detach inherited SkinAssignment scripts only on the DEGU prefab,
            // not on the source Wolf or in the global fur catalog.
            foreach(Behaviour b in prefab.GetComponentsInChildren<Behaviour>(true))
                if(b && b.GetType().Name=="SkinAssignment")b.enabled=false;
            if(!prefab.GetComponent<DeguAppearanceRuntime>())
                prefab.AddComponent<DeguAppearanceRuntime>();
            Note("Degu appearance component attached to network prefab.");
        }

        internal static void Shutdown()
        {
            foreach(Material m in DeguCopy)if(m)UnityEngine.Object.Destroy(m);
            Array.Clear(DeguCopy,0,7);
            Array.Clear(Original,0,7);
            eyeMethod=loadMethod=prepareMethod=null;
            nextAttempt=0f;everReady=false;firstApplied=false;Reported.Clear();
        }
    }

    // Network owner selects appearance once; every client renders the saved ZDO.
    // UI never changes local material directly. It requests a server ZDO write.
    public sealed class DeguAppearanceRuntime:MonoBehaviour
    {
        private IEnumerator Start()
        {
            yield return null; // all clone Awake callbacks finished; fixes premature skinned render
            if(!gameObject)yield break;
            var skin=DeguWolfPalette.Renderer(gameObject);
            if(skin && skin.sharedMesh && skin.rootBone &&
               skin.bones.Length==skin.sharedMesh.bindposes.Length)skin.enabled=true;
            else DeguPlugin.Log?.LogError("DEGU_CORE: clone renderer has invalid bones/bindposes/root.");
            var view=GetComponent<ZNetView>();
            if(!view)yield break;
            ZDO zdo=null;
            for(int n=0;n<100;n++)
            {
                if(!view)yield break;
                if(view.IsValid() && (zdo=view.GetZDO())!=null)break;
                yield return new WaitForSeconds(.2f);
            }
            if(zdo==null){DeguPlugin.Log?.LogError("DEGU_CORE: ZDO initialization failed.");yield break;}
            if(view.IsOwner())
            {
                if(!zdo.GetBool(DeguWolfPalette.ActiveKey,false))
                {
                    if(zdo.GetInt(DeguWolfPalette.SkinKey,-1)<0)
                        zdo.Set(DeguWolfPalette.SkinKey,UnityEngine.Random.Range(0,7));
                    zdo.Set(DeguWolfPalette.ActiveKey,true);
                }
                if(zdo.GetInt(DeguWolfPalette.EyeKey,0)<1)
                    zdo.Set(DeguWolfPalette.EyeKey,UnityEngine.Random.Range(1,7));
            }
            int lastCoat=-1,lastEye=-1,lastElement=-1;
            bool applied=false;
            float nextMaterialCheck=0;
            while(view && view.IsValid() && gameObject)
            {
                zdo=view.GetZDO();
                if(zdo==null)yield break;
                int coat=zdo.GetInt(DeguWolfPalette.SkinKey,-1);
                int eye=zdo.GetInt(DeguWolfPalette.EyeKey,0);
                int level=zdo.GetInt(DeguWolfPalette.LevelKey,1);
                int element=zdo.GetBool(DeguWolfPalette.RunesKey,true) && level>=25
                    ?Mathf.Clamp(zdo.GetInt(DeguWolfPalette.ElementKey,0),0,5):0;
                if(coat>=0 && coat<7 && eye>=1 && eye<=6 &&
                    (!applied || coat!=lastCoat || eye!=lastEye || element!=lastElement ||
                     (Time.unscaledTime>=nextMaterialCheck && !DeguWolfPalette.Matches(gameObject,coat))))
                {
                    if(DeguWolfPalette.Apply(gameObject,coat,eye,element))
                    {applied=true;lastCoat=coat;lastEye=eye;lastElement=element;}
                }
                if(Time.unscaledTime>=nextMaterialCheck)nextMaterialCheck=Time.unscaledTime+5f;
                yield return new WaitForSeconds(.7f);
            }
        }
    }
}
