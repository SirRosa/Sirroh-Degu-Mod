using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx.Bootstrap;
using HarmonyLib;
using UnityEngine;

namespace SirrohDegu
{
    // Strictly renderer-only detached UI preview; no Character, ZNetView,
    // live AI or prefab Awake. No cloning partially bound skinned meshes.
    internal static class DeguPreviewBridge
    {
        static Harmony harmony;
        static readonly HashSet<string> Warned=new HashSet<string>();
        static readonly BindingFlags PrivateInstance=BindingFlags.Instance|BindingFlags.NonPublic;
        static Type type,panel;

        static void Warn(string s)
        {
            if(Warned.Add(s))DeguPlugin.Log?.LogWarning("DEGU_PREVIEW: "+s);
        }
        internal static void Install()
        {
            if(harmony!=null)return;
            Assembly asm=null;
            if(Chainloader.PluginInfos.TryGetValue("sirro.valheim.pracafizyczna",out var p))
                asm=p.Instance?.GetType().Assembly;
            if(asm==null)asm=AccessTools.TypeByName("Sirro.PracaFizyczna.FamiliarModelPreview")?.Assembly;
            type=asm?.GetType("Sirro.PracaFizyczna.FamiliarModelPreview",false);
            panel=asm?.GetType("Sirro.PracaFizyczna.FamiliarMenuPanel",false);
            if(type==null){Warn("native preview class not available.");return;}
            MethodInfo copy=AccessTools.Method(type,"CopyRenderers");
            MethodInfo show=AccessTools.Method(type,"Show");
            if(copy==null || show==null){Warn("native preview API incomplete.");return;}
            harmony=new Harmony("sirroh.degu.rebuild.preview.v040");
            try
            {
                harmony.Patch(copy,prefix:new HarmonyMethod(
                    AccessTools.Method(typeof(DeguPreviewBridge),nameof(Copy))));
                harmony.Patch(show,postfix:new HarmonyMethod(
                    AccessTools.Method(typeof(DeguPreviewBridge),nameof(AfterShow))));
            }
            catch(Exception ex)
            {
                Warn("native preview patch failed: "+ex.GetBaseException().Message);
                harmony.UnpatchSelf();harmony=null;
            }
        }
        internal static void Shutdown(){harmony?.UnpatchSelf();harmony=null;Warned.Clear();}

        static SkinnedMeshRenderer LiveRenderer(Transform source,Transform root,Transform living)
        {
            if(!living)return null;
            Stack<string> segments=new Stack<string>();
            for(Transform t=source;t && t!=root;t=t.parent)segments.Push(t.name);
            while(segments.Count>0)
            {
                living=living.Find(segments.Pop());
                if(!living)return null;
            }
            return living.GetComponent<SkinnedMeshRenderer>();
        }

        static bool Copy(object __instance,GameObject source,GameObject living)
        {
            if(!source || source.name!="Degu")return true;
            try
            {
                var map=type.GetField("_transforms",PrivateInstance)?.GetValue(__instance)
                    as Dictionary<Transform,Transform>;
                if(map==null){Warn("preview bone transform map missing");return false;}
                int copied=0;
                foreach(var pair in map)
                {
                    Transform from=pair.Key,to=pair.Value;
                    if(!from || !to)continue;
                    SkinnedMeshRenderer sourceSkin=from.GetComponent<SkinnedMeshRenderer>();
                    if(!sourceSkin || sourceSkin.name!=DeguWolfPalette.VisualName || !sourceSkin.sharedMesh)
                        continue;
                    Transform[] original=sourceSkin.bones;
                    if(original==null || original.Length==0 ||
                       sourceSkin.sharedMesh.bindposes.Length!=original.Length ||
                       !sourceSkin.rootBone)
                    {
                        Warn("Degu source has invalid preview bind count/root");
                        continue;
                    }
                    var bones=new Transform[original.Length];
                    bool mapped=true;
                    for(int i=0;i<original.Length;i++)
                    {
                        if(!original[i] || !map.TryGetValue(original[i],out Transform bone) || !bone)
                        {mapped=false;break;}
                        bones[i]=bone;
                    }
                    if(!mapped || !map.TryGetValue(sourceSkin.rootBone,out Transform root) || !root)
                    {
                        Warn("Degu preview cannot map complete 34 bone armature");
                        continue;
                    }
                    var skin=to.gameObject.AddComponent<SkinnedMeshRenderer>();
                    skin.enabled=false;
                    skin.bones=bones;skin.rootBone=root;
                    skin.sharedMesh=sourceSkin.sharedMesh;
                    SkinnedMeshRenderer live=LiveRenderer(from,source.transform,
                        living?living.transform:null);
                    skin.sharedMaterials=live && live.sharedMaterials.Length==sourceSkin.sharedMaterials.Length
                        ?live.sharedMaterials:sourceSkin.sharedMaterials;
                    skin.localBounds=sourceSkin.localBounds;
                    skin.updateWhenOffscreen=true;
                    skin.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
                    skin.enabled=true;
                    copied++;
                }
                if(copied!=1)Warn("Degu preview renderer count="+copied+" (expected one)");
            }
            catch(Exception ex){Warn("Degu copy preview: "+ex.GetBaseException().Message);}
            return false; // do not invoke native Wolf renderer cloning for Degu
        }

        static void AfterShow(object __instance,string prefab,int skin,
            ZDOID petId,int eyeColor,bool showRunes)
        {
            if(prefab!="Degu" || __instance==null)return;
            try
            {
                GameObject visual=type.GetField("_visual",PrivateInstance)?.GetValue(__instance) as GameObject;
                if(!visual)return;
                ZDO zdo=ZDOMan.instance?.GetZDO(petId);
                int level=zdo==null?1:zdo.GetInt(DeguWolfPalette.LevelKey,1);
                int element=zdo==null?0:zdo.GetInt(DeguWolfPalette.ElementKey,0);
                if(zdo==null && panel!=null)
                {
                    object info=AccessTools.Method(panel,"Selected")?.Invoke(null,null);
                    if(info!=null)
                    {
                        var flags=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
                        object id=info.GetType().GetField("Id",flags)?.GetValue(info);
                        if(id is ZDOID recordId && recordId==petId)
                        {
                            if(info.GetType().GetField("PetLevel",flags)?.GetValue(info) is int l)level=l;
                            if(info.GetType().GetField("Element",flags)?.GetValue(info) is int e)element=e;
                        }
                    }
                }
                if(eyeColor<1 || eyeColor>6)
                    eyeColor=zdo==null?1:Mathf.Clamp(zdo.GetInt(DeguWolfPalette.EyeKey,1),1,6);
                int applied=showRunes && level>=25?Mathf.Clamp(element,0,5):0;
                if(!DeguWolfPalette.Apply(visual,Mathf.Clamp(skin,0,6),eyeColor,applied))
                    Warn("Degu 3D preview waiting for Wolf materials");
                type.GetMethod("Render",PrivateInstance)?.Invoke(__instance,null);
            }
            catch(Exception ex){Warn("Degu preview materials: "+ex.GetBaseException().Message);}
        }
    }
}
