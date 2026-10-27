using System;
using System.Collections;
using System.Reflection;
using BepInEx.Bootstrap;
using HarmonyLib;
using UnityEngine;

namespace SirrohDegu
{
    // Single authoritative appearance pipeline for both wild and tamed Degu.
    // ZDO choices do not depend on whether Overhaul's UI/Harmony patches install.
    internal static class DeguAppearanceService
    {
        internal const string SkinKey="furRandomizerSet";
        internal const string ActiveKey="furRandomizerSetActive";
        internal const string EyeKey="sirroh.wolf.eye.v1";
        internal const string LevelKey="sirroh.pet.level.v1";
        internal const string ElementKey="sirroh.pet.element.v1";
        internal const string RunesKey="sirroh.wolf.showrunes.v1";
        private const string OverhaulId="sirro.valheim.pracafizyczna";
        private const string RequiredVersion="0.22.65";
        private const string VisualName="SirrohDegu_StableVisual";
        private static Type inheritedAssignment;
        private static FieldInfo variants,materialField;
        private static MethodInfo assign,applyProperties;
        private static bool warned;
        internal static bool CanRender {get;private set;}

        internal static void Install()
        {
            CanRender=false;
            try
            {
                if(!Chainloader.PluginInfos.TryGetValue(OverhaulId,out var plugin))
                {
                    DeguPlugin.Log?.LogWarning("DEGU_APPEARANCE: Overhaul missing. ZDO randomizer remains active; cosmetics need Overhaul 0.22.65.");
                    return;
                }
                string version=plugin.Metadata.Version.ToString();
                if(version!=RequiredVersion)
                {
                    DeguPlugin.Log?.LogError("DEGU_APPEARANCE: incompatible Overhaul "+version+
                        " (expected "+RequiredVersion+"). ZDO assignment remains active.");
                    return;
                }
                Assembly assembly=plugin.Instance.GetType().Assembly;
                Type catalog=assembly.GetType("Sirro.PracaFizyczna.MaterialCatalog",true);
                Type variant=assembly.GetType("Sirro.PracaFizyczna.Variant",true);
                Type visual=assembly.GetType("Sirro.PracaFizyczna.WolfVisuals",true);
                inheritedAssignment=assembly.GetType("Sirro.PracaFizyczna.SkinAssignment",false);
                variants=AccessTools.Field(catalog,"Variants");
                materialField=AccessTools.Field(variant,"Main");
                assign=AccessTools.Method(catalog,"Assign");
                applyProperties=AccessTools.Method(visual,"ApplyProperties");
                if(variants==null || materialField==null || assign==null || applyProperties==null)
                    throw new MissingMemberException("Required Overhaul 0.22.65 appearance API missing.");
                CanRender=true;
                DeguPlugin.Log?.LogInfo("DEGU_APPEARANCE: independent ZDO randomizer + Wolf material adapter ready (Overhaul "+version+").");
            }
            catch(Exception e)
            {
                CanRender=false;
                DeguPlugin.Log?.LogError("DEGU_APPEARANCE: material adapter FAILED (ZDO randomizer still works): "+e);
            }
        }

        internal static void RegisterPrefab(GameObject prefab)
        {
            if(!prefab)return;
            // Wolf's inherited SkinAssignment is not Degu's authority.
            // Never allow two independent writers to fight over the same ZDO.
            if(inheritedAssignment!=null)
            {
                Behaviour duplicate=prefab.GetComponent(inheritedAssignment) as Behaviour;
                if(duplicate)duplicate.enabled=false;
            }
            if(!prefab.GetComponent<DeguAppearanceState>())
                prefab.AddComponent<DeguAppearanceState>();
            DeguPlugin.Log?.LogInfo("DEGU_APPEARANCE: prefab="+prefab.name+
                " sync="+(prefab.GetComponent<DeguAppearanceState>()!=null)+
                " materials="+CanRender+" (wild variant writer independent of Tresura).");
        }

        internal static SkinnedMeshRenderer Visible(GameObject obj)
        {
            if(!obj)return null;
            foreach(SkinnedMeshRenderer skin in obj.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if(skin && skin.name==VisualName)return skin;
            return null;
        }

        // Returns false if the material bundle has not finished loading;
        // callers then retry without changing persisted cosmetic choices.
        internal static bool ApplyVisual(GameObject obj,int coat,int eye,int element)
        {
            if(!CanRender || SystemInfo.graphicsDeviceType==UnityEngine.Rendering.GraphicsDeviceType.Null)
                return false;
            SkinnedMeshRenderer skin=Visible(obj);
            if(!skin || skin.sharedMesh==null)return false;
            try
            {
                var all=variants.GetValue(null) as IDictionary;
                var coats=all==null?null:all["Wolf"] as Array;
                if(coats==null || coats.Length<7)return false;
                coat=Mathf.Clamp(coat,0,6);
                object selected=coats.GetValue(coat);
                Material material=selected==null?null:materialField.GetValue(selected) as Material;
                if(!material)return false;
                // Use exact native wolf materials, shaders, texture palettes and
                // emission maps. Do not generate new materials per spawn.
                assign.Invoke(null,new object[]{skin,0,material,true});
                assign.Invoke(null,new object[]{skin,1,material,true});
                applyProperties.Invoke(null,new object[]{skin,material,
                    Mathf.Clamp(eye,0,6),Mathf.Clamp(element,0,5)});
                // Degu's eye polygons are submesh 1; the Overhaul routines
                // write to submesh 0. Copy the complete property block.
                var block=new MaterialPropertyBlock();
                skin.GetPropertyBlock(block,0);
                skin.SetPropertyBlock(block,1);
                return true;
            }
            catch(Exception e)
            {
                if(!warned)
                {
                    warned=true;
                    DeguPlugin.Log?.LogError("DEGU_APPEARANCE: material apply exception: "+e.GetBaseException());
                }
                return false;
            }
        }

        internal static int Element(ZDO zdo)
        {
            if(zdo==null || !zdo.GetBool(RunesKey,true) || zdo.GetInt(LevelKey,1)<25)
                return 0;
            return Mathf.Clamp(zdo.GetInt(ElementKey,0),0,5);
        }
    }

    // Applied to EVERY Degu prefab regardless of Overhaul UI initialization.
    // The ZDO owner writes once, all peers render from the same keys.
    public sealed class DeguAppearanceState : MonoBehaviour
    {
        private static readonly WaitForSeconds StartupDelay=new WaitForSeconds(.2f);
        private static readonly WaitForSeconds PollDelay=new WaitForSeconds(1f);
        private IEnumerator Start()
        {
            ZNetView view=GetComponent<ZNetView>();
            if(!view)
            {
                DeguPlugin.Log?.LogError("DEGU_APPEARANCE: Degu ZNetView missing.");
                yield break;
            }
            ZDO zdo=null;
            for(int i=0;i<300;i++)
            {
                if(!view || !gameObject)yield break;
                if(view.IsValid() && (zdo=view.GetZDO())!=null)break;
                yield return StartupDelay;
            }
            if(zdo==null)
            {
                DeguPlugin.Log?.LogError("DEGU_APPEARANCE: ZDO was not initialized within 60 seconds.");
                yield break;
            }
            bool owner=view.IsOwner();
            if(owner && !zdo.GetBool(DeguAppearanceService.ActiveKey,false))
            {
                // Preserve existing worlds where a coat was stored without flag.
                if(zdo.GetInt(DeguAppearanceService.SkinKey,-1)<0)
                    zdo.Set(DeguAppearanceService.SkinKey,UnityEngine.Random.Range(0,7));
                zdo.Set(DeguAppearanceService.ActiveKey,true);
            }
            if(owner && zdo.GetInt(DeguAppearanceService.EyeKey,0)<=0)
                zdo.Set(DeguAppearanceService.EyeKey,UnityEngine.Random.Range(1,7));
            int appliedSkin=-1,appliedEye=-1,appliedElement=-1;
            bool applied=false,notified=false;
            while(view && view.IsValid() && gameObject)
            {
                zdo=view.GetZDO();
                if(zdo==null)yield break;
                int coat=zdo.GetInt(DeguAppearanceService.SkinKey,-1);
                int eye=zdo.GetInt(DeguAppearanceService.EyeKey,0);
                int element=DeguAppearanceService.Element(zdo);
                bool ready=zdo.GetBool(DeguAppearanceService.ActiveKey,false) &&
                    coat>=0 && coat<7 && eye>=1 && eye<=6;
                if(ready && (!applied || coat!=appliedSkin ||
                    eye!=appliedEye || element!=appliedElement))
                {
                    if(DeguAppearanceService.ApplyVisual(gameObject,coat,eye,element))
                    {
                        applied=true;
                        appliedSkin=coat;
                        appliedEye=eye;
                        appliedElement=element;
                        if(!notified)
                            DeguPlugin.Log?.LogInfo("DEGU_APPEARANCE: first applied id="+zdo.m_uid+
                                " owner="+owner+" coat="+coat+" eye="+eye+" element="+element);
                    }
                    else if(!notified)
                    {
                        notified=true;
                        DeguPlugin.Log?.LogWarning("DEGU_APPEARANCE: waiting for visible renderer/Overhaul materials; "+
                            "id="+zdo.m_uid+" owner="+owner+" coat="+coat+" eye="+eye+
                            " material_adapter="+DeguAppearanceService.CanRender);
                    }
                }
                yield return PollDelay;
            }
        }
    }
}
