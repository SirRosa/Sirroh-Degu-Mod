using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using BepInEx.Bootstrap;
using UnityEngine;

namespace SirrohDegu
{
    // Only installed by version-gated Overhaul adapter.
    internal static class DeguPreviewDiagnostics
    {
        private static string lastState;

        private static Harmony patches;
        internal static void Install()
        {
            if(!Chainloader.PluginInfos.TryGetValue("sirro.valheim.pracafizyczna",out var p) ||
                p.Metadata.Version.ToString()!="0.22.65")
            {
                DeguPlugin.Log?.LogWarning("DEGU_PREVIEW: exact Overhaul 0.22.65 not found; preview hooks disabled.");
                return;
            }
            try
            {
                var assembly=p.Instance.GetType().Assembly;
                var type=assembly.GetType("Sirro.PracaFizyczna.FamiliarModelPreview",true);
                var panel=assembly.GetType("Sirro.PracaFizyczna.FamiliarMenuPanel",true);
                var copy=AccessTools.Method(type,"CopyRenderers");
                var show=AccessTools.Method(type,"Show");
                var detail=AccessTools.Method(panel,"UpdateDetail");
                if(copy==null || show==null || detail==null)
                    throw new MissingMethodException("Overhaul preview/UpdateDetail");
                patches=new Harmony("sirroh.degu.preview.v031");
                patches.Patch(copy,prefix:new HarmonyMethod(AccessTools.Method(
                    typeof(DeguPreviewDiagnostics),nameof(CopyRenderers))));
                patches.Patch(show,postfix:new HarmonyMethod(AccessTools.Method(
                    typeof(DeguPreviewDiagnostics),nameof(AfterPreviewShow))));
                patches.Patch(detail,prefix:new HarmonyMethod(AccessTools.Method(
                    typeof(DeguPreviewDiagnostics),nameof(BeforeDetail))),
                    postfix:new HarmonyMethod(AccessTools.Method(
                    typeof(DeguPreviewDiagnostics),nameof(AfterDetail))));
                DeguPlugin.Log?.LogInfo("DEGU_PREVIEW: Degu-only renderer hooks active.");
            }
            catch(Exception e)
            {
                patches?.UnpatchSelf();
                patches=null;
                DeguPlugin.Log?.LogError("DEGU_PREVIEW: patch failed (wild variants unaffected): "+e);
            }
        }
        internal static void Shutdown()
        {
            patches?.UnpatchSelf();
            patches=null;
            lastState=null;
        }

        private static readonly BindingFlags StaticPrivate = BindingFlags.Static | BindingFlags.NonPublic;
        private static readonly BindingFlags InstancePrivate = BindingFlags.Instance | BindingFlags.NonPublic;
        private static object Selected()
        {
            Type type=AccessTools.TypeByName("Sirro.PracaFizyczna.FamiliarMenuPanel");
            return type?.GetMethod("Selected",StaticPrivate)?.Invoke(null,null);
        }
        private static object Field(object obj,string name) =>
            obj?.GetType().GetField(name,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic)?.GetValue(obj);
        private static object PanelField(string name)
        {
            Type type=AccessTools.TypeByName("Sirro.PracaFizyczna.FamiliarMenuPanel");
            return type?.GetField(name,StaticPrivate)?.GetValue(null);
        }
        private static bool IsDegu(object info) => string.Equals(Field(info,"Prefab") as string,"Degu",StringComparison.Ordinal);
        private static bool Available(string name)
        {
            object control=PanelField(name);
            if(control==null)return false;
            PropertyInfo p=control.GetType().GetProperty("interactable");
            return p!=null && Convert.ToBoolean(p.GetValue(control,null));
        }
        private static void BeforeDetail()
        {
            object info=Selected();
            if(!IsDegu(info) || !(Field(info,"Alive") is bool alive) || alive)return;
            Player player=Player.m_localPlayer;
            if(!player || !ZNetScene.instance)return;
            object idObject=Field(info,"Id");
            if(!(idObject is ZDOID id))return;
            GameObject obj=ZNetScene.instance.FindInstance(id);
            Character animal=obj?obj.GetComponent<Character>():null;
            ZNetView view=obj?obj.GetComponent<ZNetView>():null;
            if(!animal || !animal.IsTamed() || !view || !view.IsValid())return;
            ZDO zdo=view.GetZDO();
            if(zdo==null || zdo.m_uid!=id ||
               zdo.GetLong("sirroh.tresura.owner.v1",0L)!=player.GetPlayerID())return;
            var state=info.GetType().GetField("Alive",BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public);
            state?.SetValue(info,true);
            DeguPlugin.Log?.LogInfo("Degu Tresura: corrected stale Alive for owned, living familiar.");
        }
        private static void AfterDetail()
        {
            object info=Selected();
            if(!IsDegu(info))return;
            string value="alive="+Field(info,"Alive")+
                " | skin="+Available("_left")+"/"+Available("_right")+
                " eyes="+Available("_eyeLeft")+"/"+Available("_eyeRight")+
                " name="+Available("_fontLeft")+"/"+Available("_fontRight")+
                " rune="+Available("_runesToggle");
            if(lastState==value)return;
            lastState=value;
            DeguPlugin.Log?.LogInfo("Degu Tresura controls: "+value);
        }
        // Overhaul's renderer copies mesh before bones. On Degu, the preview
        // can report "Bones do not match bindpose" in that interim state.
        // Copy ONLY Degu's visible renderer with bones assigned first.
        private static bool CopyRenderers(object __instance,GameObject source,GameObject living)
        {
            if(!source || source.name!="Degu")return true;
            try
            {
                var map=__instance.GetType().GetField("_transforms",InstancePrivate)
                    ?.GetValue(__instance) as Dictionary<Transform,Transform>;
                if(map==null)throw new InvalidOperationException("Missing preview transform map");
                foreach(var pair in map)
                {
                    Transform from=pair.Key,into=pair.Value;
                    if(!from || !into || (from!=source.transform && !from.gameObject.activeSelf))
                        continue;
                    var mf=from.GetComponent<MeshFilter>();
                    var mr=from.GetComponent<MeshRenderer>();
                    if(mf && mr && mr.enabled && mf.sharedMesh)
                    {
                        var filter=into.gameObject.AddComponent<MeshFilter>();
                        filter.sharedMesh=mf.sharedMesh;
                        var renderer=into.gameObject.AddComponent<MeshRenderer>();
                        renderer.sharedMaterials=mr.sharedMaterials;
                        renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
                        renderer.receiveShadows=false;
                    }
                    var skin=from.GetComponent<SkinnedMeshRenderer>();
                    // Staged network prefab keeps its Degu visual disabled
                    // until clone initialization. Detached UI preview is safe
                    // to construct from that renderer without a Character.
                    if(!skin || !skin.sharedMesh ||
                       (!skin.enabled && skin.name!="SirrohDegu_StableVisual"))continue;
                    Transform[] original=skin.bones;
                    if(original==null || original.Length==0 ||
                       skin.sharedMesh.bindposes.Length!=original.Length)
                    {
                        DeguPlugin.Log?.LogWarning("DEGU_PREVIEW: invalid source binding "+skin.name);
                        continue;
                    }
                    var bones=new Transform[original.Length];
                    for(int i=0;i<bones.Length;i++)
                        bones[i]=original[i] && map.TryGetValue(original[i],out Transform found)?found:into;
                    bool mapped=true;
                    for(int i=0;i<original.Length;i++)
                        if(!original[i] || !map.TryGetValue(original[i],out Transform found) || !found)
                        {mapped=false;break;}
                    if(!mapped || !skin.rootBone || !map.TryGetValue(skin.rootBone,out Transform boundRoot) || !boundRoot)
                    {
                        DeguPlugin.Log?.LogWarning("DEGU_PREVIEW: unmapped Degu bone/root; skipping renderer "+skin.name);
                        continue;
                    }
                    var result=into.gameObject.AddComponent<SkinnedMeshRenderer>();
                    result.enabled=false;
                    result.bones=bones;
                    result.rootBone=boundRoot;
                    result.sharedMesh=skin.sharedMesh;
                    var live=Matching(from,source.transform,living?living.transform:null);
                    result.sharedMaterials=live && live.sharedMaterials.Length==skin.sharedMaterials.Length
                        ?live.sharedMaterials:skin.sharedMaterials;
                    result.localBounds=skin.localBounds;
                    result.updateWhenOffscreen=true;
                    result.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
                    result.enabled=true;
                }
            }
            catch(Exception e)
            {
                DeguPlugin.Log?.LogError("Degu-only preview failed: "+e.GetBaseException().Message);
            }
            return false;
        }

        // Reapply native materials to the detached renderer-only scene after
        // Overhaul's Show creates it; never instantiate an active Character.
        private static void AfterPreviewShow(object __instance,string prefab,int skin,
            ZDOID petId,int eyeColor,bool showRunes)
        {
            if(prefab!="Degu" || __instance==null)return;
            try
            {
                GameObject visual=__instance.GetType().GetField("_visual",InstancePrivate)
                    ?.GetValue(__instance) as GameObject;
                if(!visual)return;
                ZDO zdo=ZDOMan.instance?.GetZDO(petId);
                int level=zdo==null?1:zdo.GetInt(DeguAppearanceService.LevelKey,1);
                int element=zdo==null?0:zdo.GetInt(DeguAppearanceService.ElementKey,0);
                if(zdo==null)
                {
                    object selected=Selected();
                    object id=Field(selected,"Id");
                    if(id is ZDOID matching && matching==petId)
                    {
                        if(Field(selected,"PetLevel") is int l)level=l;
                        if(Field(selected,"Element") is int e)element=e;
                    }
                }
                if(eyeColor<1 || eyeColor>6)
                    eyeColor=zdo==null?0:zdo.GetInt(DeguAppearanceService.EyeKey,0);
                int appliedElement=showRunes && level>=25?Mathf.Clamp(element,0,5):0;
                bool done=DeguAppearanceService.ApplyVisual(visual,skin,eyeColor,appliedElement);
                if(!done)
                    DeguPlugin.Log?.LogWarning("DEGU_PREVIEW: no material for requested Degu preview");
                __instance.GetType().GetMethod("Render",InstancePrivate)?.Invoke(__instance,null);
            }
            catch(Exception ex)
            {
                DeguPlugin.Log?.LogError("DEGU_PREVIEW: apply failed: "+ex.GetBaseException());
            }
        }
        private static SkinnedMeshRenderer Matching(Transform from,Transform root,Transform living)
        {
            if(!living)return null;
            var path=new Stack<string>();
            for(Transform cursor=from;cursor && cursor!=root;cursor=cursor.parent)
                path.Push(cursor.name);
            while(path.Count>0)
            {
                living=living.Find(path.Pop());
                if(!living)return null;
            }
            return living.GetComponent<SkinnedMeshRenderer>();
        }
    }
}
