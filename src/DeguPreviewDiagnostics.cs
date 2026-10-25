using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace SirrohDegu
{
    // Only installed by version-gated Overhaul adapter.
    internal static class DeguPreviewDiagnostics
    {
        private static string lastState;
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
            if(!OverhaulCompatibility.Enabled)return;
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
            if(!OverhaulCompatibility.Enabled)return;
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
                    if(!skin || !skin.enabled || !skin.sharedMesh)continue;
                    var result=into.gameObject.AddComponent<SkinnedMeshRenderer>();
                    Transform[] original=skin.bones;
                    var bones=new Transform[original.Length];
                    for(int i=0;i<bones.Length;i++)
                        bones[i]=original[i] && map.TryGetValue(original[i],out Transform found)?found:into;
                    if(skin.sharedMesh.bindposes.Length!=bones.Length)
                    {
                        DeguPlugin.Log?.LogWarning("Degu preview skipped inconsistent mesh: bindposes="+
                            skin.sharedMesh.bindposes.Length+", bones="+bones.Length);
                        UnityEngine.Object.Destroy(result);
                        continue;
                    }
                    result.bones=bones;
                    result.rootBone=skin.rootBone && map.TryGetValue(skin.rootBone,out Transform root)?root:into;
                    result.sharedMesh=skin.sharedMesh;
                    var live=Matching(from,source.transform,living?living.transform:null);
                    result.sharedMaterials=live && live.sharedMaterials.Length==skin.sharedMaterials.Length
                        ?live.sharedMaterials:skin.sharedMaterials;
                    result.localBounds=skin.localBounds;
                    result.updateWhenOffscreen=true;
                    result.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
                }
            }
            catch(Exception e)
            {
                DeguPlugin.Log?.LogError("Degu-only preview failed: "+e.GetBaseException().Message);
            }
            return false;
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
