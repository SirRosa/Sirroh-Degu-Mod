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
        private static FieldInfo variants;
        private static MethodInfo assign,applyProperties,loadCatalog;
        private static float nextCatalogRetry;
        private static Material[] selectedWolfMaterials;
        private static MethodInfo selectedAssign;
        private static string selectedSource;
        private static float nextMaterialAudit;
        internal static bool CanRender {get;private set;}

        private static string lastFailure;
        private static bool incompatible;

        // Some BepInEx load configurations populate PluginInfos later than
        // the dependent plugin's Awake. Never make a one-time failure permanent.
        // Resolve the exact loaded Overhaul types again when a Degu needs a
        // material. No patch, alias, scene scan or material clone is needed.
        internal static void Install()
        {
            TryResolveMaterials();
        }

        internal static bool TryResolveMaterials()
        {
            if(CanRender && applyProperties!=null)return true;
            if(incompatible)return false;
            try
            {
                Assembly assembly=null;
                if(Chainloader.PluginInfos.TryGetValue(OverhaulId,out var plugin))
                {
                    System.Version version=plugin.Metadata.Version;
                    if(version.Major!=0 || version.Minor!=22 || version.Build!=65)
                    {
                        incompatible=true;
                        WarnBridge("Overhaul version "+version+" incompatible; exact 0.22.65 required.");
                        return false;
                    }
                    assembly=plugin.Instance.GetType().Assembly;
                }
                // Can execute during the loading phase, before PluginInfos has
                // registered every plugin. Resolve from the loaded CLR types.
                Type catalog=assembly?.GetType("Sirro.PracaFizyczna.MaterialCatalog",false)
                    ?? AccessTools.TypeByName("Sirro.PracaFizyczna.MaterialCatalog");
                Type visual=assembly?.GetType("Sirro.PracaFizyczna.WolfVisuals",false)
                    ?? AccessTools.TypeByName("Sirro.PracaFizyczna.WolfVisuals");
                if(catalog==null || visual==null)
                {
                    WarnBridge("MaterialCatalog / WolfVisuals type not loaded yet.");
                    return false;
                }
                if(catalog.Assembly!=visual.Assembly)
                {
                    WarnBridge("MaterialCatalog and WolfVisuals are from different assemblies.");
                    return false;
                }
                var catalogVariants=AccessTools.Field(catalog,"Variants");
                var eyeShader=AccessTools.Method(visual,"ApplyProperties");
                if(catalogVariants==null)
                {
                    WarnBridge("Overhaul MaterialCatalog.Variants field missing.");
                    return false;
                }
                // Coat must not fail just because an optional eye-emission
                // shader method was renamed. Continue applying Wolf fur.
                if(eyeShader==null)
                    WarnBridge("WolfVisuals.ApplyProperties unavailable; coat remains usable but eyes/emission need compatible Overhaul.");
                variants=catalogVariants;
                applyProperties=eyeShader;
                assign=AccessTools.Method(catalog,"Assign");
                loadCatalog=AccessTools.Method(catalog,"Load");
                inheritedAssignment=AccessTools.TypeByName("Sirro.PracaFizyczna.SkinAssignment");
                CanRender=true;
                lastFailure=null;
                DeguPlugin.Log?.LogInfo(
                    "DEGU_APPEARANCE: Wolf material bridge connected; coat=Wolf[0..6], eye=1..6, emissive=WolfVisuals.");
                return true;
            }
            catch(Exception ex)
            {
                WarnBridge("resolving Overhaul bridge: "+ex.GetBaseException().Message);
                return false;
            }
        }

        private static void WarnBridge(string reason)
        {
            if(lastFailure==reason)return;
            lastFailure=reason;
            DeguPlugin.Log?.LogWarning("DEGU_APPEARANCE bridge: "+reason);
        }

        internal static void RegisterPrefab(GameObject prefab)
        {
            if(!prefab)return;
            // Wolf's inherited SkinAssignment is not Degu's authority.
            // Never allow two independent writers to fight over the same ZDO.
            if(inheritedAssignment==null)
                inheritedAssignment=AccessTools.TypeByName("Sirro.PracaFizyczna.SkinAssignment");
            // Both integrated and standalone Fur Randomizer can have their own
            // SkinAssignment class. A cloned Degu is authored by ONE ZDO owner
            // component only; disable only inherited writers on the DEGU prefab.
            int disabled=0;
            foreach(Behaviour component in prefab.GetComponentsInChildren<Behaviour>(true))
            {
                if(!component || !component.enabled)continue;
                Type type=component.GetType();
                if(type.Name=="SkinAssignment")
                {
                    component.enabled=false;
                    disabled++;
                }
            }
            if(!prefab.GetComponent<DeguAppearanceState>())
                prefab.AddComponent<DeguAppearanceState>();
            if(disabled>0)
                DeguPlugin.Log?.LogInfo("DEGU_APPEARANCE: disabled "+disabled+
                    " inherited Wolf SkinAssignment components on Degu ONLY.");
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

        // No color/material clones: inspect ONLY the two trusted plugin
        // assemblies (Overhaul and its optional standalone Fur Randomizer).
        // In external-bundle mode Overhaul's own Variants dictionary may be
        // empty while the standalone module owns the real seven materials.
        private static Type FindCatalog(System.Reflection.Assembly assembly)
        {
            if(assembly==null)return null;
            Type exact=assembly.GetType("Sirro.PracaFizyczna.MaterialCatalog",false);
            if(exact!=null)return exact;
            try
            {
                foreach(Type type in assembly.GetTypes())
                    if(type!=null && type.Name=="MaterialCatalog" &&
                       AccessTools.Field(type,"Variants")!=null)
                        return type;
            }
            catch(ReflectionTypeLoadException ex)
            {
                foreach(Type type in ex.Types)
                    if(type!=null && type.Name=="MaterialCatalog" &&
                       AccessTools.Field(type,"Variants")!=null)
                        return type;
            }
            return null;
        }

        private static bool LoadedWolfCoats(Type catalog,out Material[] materials)
        {
            materials=null;
            if(catalog==null)return false;
            FieldInfo f=AccessTools.Field(catalog,"Variants");
            IDictionary dict=f?.GetValue(null) as IDictionary;
            if(dict==null || !dict.Contains("Wolf"))return false;
            Array skins=dict["Wolf"] as Array;
            if(skins==null || skins.Length!=7)return false;
            Material[] result=new Material[7];
            for(int i=0;i<7;i++)
            {
                object skin=skins.GetValue(i);
                if(skin==null)return false;
                FieldInfo main=AccessTools.Field(skin.GetType(),"Main");
                if(main==null)return false;
                result[i]=main.GetValue(skin) as Material;
                if(!result[i])return false;
            }
            materials=result;
            return true;
        }

        private static bool ResolveWolfMaterials()
        {
            if(selectedWolfMaterials!=null &&
               selectedWolfMaterials.Length==7 && selectedWolfMaterials[0] &&
               selectedWolfMaterials[6])
            {
                // Keep retrying the independent WolfVisuals eye/emission
                // method if only the coat material source became ready.
                if(applyProperties==null)TryResolveMaterials();
                return true;
            }
            bool wolfVisualBridge=TryResolveMaterials();
            // A standalone Fur Randomizer can still provide real Wolf coats
            // if Overhaul's optional reflection bridge is temporarily absent.
            if(!wolfVisualBridge &&
               !Chainloader.PluginInfos.ContainsKey("sirroh.furrandomizer"))
                return false;
            selectedWolfMaterials=null;
            selectedCatalog=null;
            selectedAssign=null;

            var candidates=new System.Collections.Generic.List<Type>(2);
            Type integrated=variants?.DeclaringType;
            if(integrated!=null)candidates.Add(integrated);

            if(Chainloader.PluginInfos.TryGetValue("sirroh.furrandomizer",out var standalone) &&
               standalone?.Instance!=null)
            {
                Type own=FindCatalog(standalone.Instance.GetType().Assembly);
                if(own!=null && !candidates.Contains(own))candidates.Add(own);
            }

            // Try Overhaul first, then the separate fur plugin. A standalone
            // bundle never needs to be duplicated in Degu's own assembly.
            foreach(Type candidate in candidates)
            {
                if(!LoadedWolfCoats(candidate,out Material[] mats))continue;
                selectedWolfMaterials=mats;
                selectedAssign=AccessTools.Method(candidate,"Assign");
                selectedSource=candidate.Assembly.GetName().Name;
                DeguPlugin.Log?.LogInfo("DEGU_APPEARANCE: native Wolf coats READY; source="+selectedSource+
                    " variants=7, render bridge=true.");
                return true;
            }

            // If assets are not ready, ask ONLY the integrated Overhaul
            // MaterialCatalog to load/borrow its own asset bundle. Never
            // create or upload another copy of the user's Wolf textures.
            if(loadCatalog!=null && Time.unscaledTime>=nextCatalogRetry)
            {
                nextCatalogRetry=Time.unscaledTime+5f;
                try
                {
                    loadCatalog.Invoke(null,null);
                    if(integrated!=null && LoadedWolfCoats(integrated,out Material[] mats))
                    {
                        selectedWolfMaterials=mats;
                        selectedAssign=AccessTools.Method(integrated,"Assign");
                        selectedSource=integrated.Assembly.GetName().Name;
                        DeguPlugin.Log?.LogInfo("DEGU_APPEARANCE: native Wolf coats READY after Load; source="+
                            selectedSource+" variants=7.");
                        return true;
                    }
                }
                catch(Exception ex)
                {
                    WarnBridge("native Overhaul MaterialCatalog.Load failed: "+
                        ex.GetBaseException().Message);
                }
            }
            if(Time.unscaledTime>=nextMaterialAudit)
            {
                nextMaterialAudit=Time.unscaledTime+15f;
                DeguPlugin.Log?.LogWarning("DEGU_APPEARANCE: Wolf[7] MATERIALS UNAVAILABLE; OverhaulCatalog="+
                    (integrated!=null)+" standaloneFur="+
                    Chainloader.PluginInfos.ContainsKey("sirroh.furrandomizer")+
                    ". Visual appearance cannot update until one catalog contains seven real Wolf materials.");
            }
            return false;
        }

        internal static bool ApplyVisual(GameObject obj,int coat,int eye,int element)
        {
            if(SystemInfo.graphicsDeviceType==UnityEngine.Rendering.GraphicsDeviceType.Null)
                return false;
            if(!ResolveWolfMaterials())return false;
            SkinnedMeshRenderer skin=Visible(obj);
            if(!skin || !skin.sharedMesh)
            {
                WarnBridge("Degu has no visible SirrohDegu_StableVisual renderer.");
                return false;
            }
            try
            {
                coat=Mathf.Clamp(coat,0,6);
                Material material=selectedWolfMaterials[coat];
                if(!material)return false;
                Material[] slots=skin.sharedMaterials;
                if(slots==null || slots.Length<2)
                {
                    WarnBridge("Degu visible renderer has no second eye submesh.");
                    return false;
                }
                if(selectedAssign!=null &&
                   selectedAssign.GetParameters().Length==4)
                {
                    selectedAssign.Invoke(null,new object[]{skin,0,material,true});
                    selectedAssign.Invoke(null,new object[]{skin,1,material,true});
                }
                else
                {
                    // Fallback for standalone Fur Randomizer variants. The
                    // material comes from that plugin's actual Wolf palette;
                    // preserve the vanilla live renderer shader if needed.
                    Shader shader=slots[0] ? slots[0].shader:null;
                    if(shader && material.shader!=shader)material.shader=shader;
                    slots[0]=material;
                    slots[1]=material;
                    skin.sharedMaterials=slots;
                }
                if(applyProperties==null)
                {
                    WarnBridge("WolfVisuals.ApplyProperties unavailable: coat applied, waiting for eye/emission binding.");
                    return false; // retry eyes on next polling tick
                }
                applyProperties.Invoke(null,new object[]{skin,material,
                    Mathf.Clamp(eye,0,6),Mathf.Clamp(element,0,5)});
                var block=new MaterialPropertyBlock();
                skin.GetPropertyBlock(block,0);
                skin.SetPropertyBlock(block,1);
                return true;
            }
            catch(Exception ex)
            {
                WarnBridge("applying native Wolf materials to Degu: "+
                    ex.GetBaseException().Message);
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
            // Wait until Unity completed ALL component Awake callbacks on the
            // network clone before activating its staged skinned renderer.
            // Activation during Instantiate / ZNetView.Awake may observe
            // incompletely remapped skeleton references.
            yield return null;
            if(!gameObject)yield break;
            var visual=DeguAppearanceService.Visible(gameObject);
            if(!visual || !visual.sharedMesh || !visual.rootBone ||
               visual.bones.Length!=visual.sharedMesh.bindposes.Length)
            {
                DeguPlugin.Log?.LogError("DEGU_VISUAL: cannot enable staged clone, invalid bones/bindposes/root.");
            }
            else
            {
                visual.enabled=true;
                DeguPlugin.Log?.LogInfo("DEGU_VISUAL: activated after all Awake callbacks; bones="+
                    visual.bones.Length+" bindposes="+visual.sharedMesh.bindposes.Length);
            }
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
                        {
                            var r=DeguAppearanceService.Visible(gameObject);
                            Material m=r?r.sharedMaterials[0]:null;
                            var state=new MaterialPropertyBlock();
                            if(r)r.GetPropertyBlock(state,0);
                            Texture eyeTex=state.GetTexture("_MainTex");
                            DeguPlugin.Log?.LogInfo("DEGU_APPEARANCE: APPLIED id="+zdo.m_uid+
                                " owner="+owner+" coat="+coat+" eye="+eye+" element="+element+
                                " material="+(m?m.name:"missing")+
                                " atlas="+(m && m.mainTexture?m.mainTexture.name:"missing")+
                                " eyeAtlas="+(eyeTex?eyeTex.name:"missing")+
                                " shader="+(m && m.shader?m.shader.name:"missing"));
                        }
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
