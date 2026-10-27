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
        private static MethodInfo assign,applyProperties;
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
            if(CanRender)return true;
            if(incompatible)return false;
            try
            {
                Assembly assembly=null;
                if(Chainloader.PluginInfos.TryGetValue(OverhaulId,out var plugin))
                {
                    Version version=plugin.Metadata.Version;
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
                if(catalogVariants==null || eyeShader==null)
                {
                    WarnBridge("Overhaul 0.22.65 has no Variants or ApplyProperties.");
                    return false;
                }
                variants=catalogVariants;
                applyProperties=eyeShader;
                assign=AccessTools.Method(catalog,"Assign");
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
            if(SystemInfo.graphicsDeviceType==UnityEngine.Rendering.GraphicsDeviceType.Null)
                return false;
            if(!TryResolveMaterials())return false;
            SkinnedMeshRenderer skin=Visible(obj);
            if(!skin || !skin.sharedMesh)return false;
            try
            {
                var all=variants.GetValue(null) as IDictionary;
                var coats=all==null?null:all["Wolf"] as Array;
                if(coats==null || coats.Length<7)
                {
                    WarnBridge("Wolf[7] variant array not ready; waiting for native Overhaul FurRandomizer.Load.");
                    return false;
                }
                coat=Mathf.Clamp(coat,0,6);
                object selected=coats.GetValue(coat);
                if(selected==null)
                {
                    WarnBridge("Wolf variant "+coat+" is absent from the bundled materials.");
                    return false;
                }
                // Inspect the actual Variant instance instead of depending on
                // a second private type lookup at plugin Awake.
                var main=selected.GetType().GetField("Main",
                    BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public);
                Material material=main?.GetValue(selected) as Material;
                if(!material)
                {
                    WarnBridge("Wolf material for coat "+coat+" has not loaded.");
                    return false;
                }
                Material[] slots=skin.sharedMaterials;
                if(slots==null || slots.Length<2)
                {
                    WarnBridge("Degu stable renderer lacks two coat/eye submeshes.");
                    return false;
                }
                // Reuse the exact materials that native Wolf Fur Randomizer
                // already loaded; do not invent replacement tints or textures.
                // The native Assign method also performs the Wolf shader and
                // emission preparation where available.
                if(assign!=null)
                {
                    assign.Invoke(null,new object[]{skin,0,material,true});
                    assign.Invoke(null,new object[]{skin,1,material,true});
                }
                else
                {
                    slots[0]=material;
                    slots[1]=material;
                    skin.sharedMaterials=slots;
                }
                applyProperties.Invoke(null,new object[]{skin,material,
                    Mathf.Clamp(eye,0,6),Mathf.Clamp(element,0,5)});
                // Wolf's shader writes overrides to submesh 0. Degu eyes are
                // submesh 1, so mirror complete eye/emission properties.
                var block=new MaterialPropertyBlock();
                skin.GetPropertyBlock(block,0);
                skin.SetPropertyBlock(block,1);
                return true;
            }
            catch(Exception ex)
            {
                WarnBridge("applying Wolf coat/eyes: "+ex.GetBaseException().Message);
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
