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
        // Valheim 0.22.65's generic preview creates an enabled renderer and
        // assigns sharedMesh before bones. Unity validates it immediately and
        // emits "Bones do not match bindpose" during that transient state.
        // For Degu only, preflight the entire binding and keep each new
        // renderer disabled until its skeleton and mesh are both complete.
        private static bool CopyRenderers(object __instance,GameObject source,GameObject living)
        {
            if (!source || (source.name != "Degu" && source.name != "Degu(Clone)")) return true;
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
                    Mesh mesh=skin.sharedMesh;
                    Transform[] original=skin.bones;
                    int count=mesh.bindposes.Length;
                    // Reject invalid source skin BEFORE adding any Unity renderer.
                    if(original==null || count==0 || original.Length!=count)
                    {
                        DeguPlugin.Log?.LogError("Degu preview rejected invalid source skin "+
                            from.name+": bones="+(original==null ? -1 : original.Length)+
                            ", bindposes="+count);
                        continue;
                    }
                    var bones=new Transform[count];
                    bool complete=true;
                    for(int i=0;i<count;i++)
                    {
                        Transform target;
                        if(!original[i] || !map.TryGetValue(original[i],out target) || !target)
                        { complete=false; break; }
                        bones[i]=target;
                    }
                    Transform root;
                    if(!complete || !skin.rootBone ||
                        !map.TryGetValue(skin.rootBone,out root) || !root)
                    {
                        DeguPlugin.Log?.LogError("Degu preview rejected unmapped skeleton for "+from.name);
                        continue;
                    }
                    // Unity must not see mesh data while this renderer has zero bones.
                    SkinnedMeshRenderer result=into.gameObject.AddComponent<SkinnedMeshRenderer>();
                    result.enabled=false;
                    try
                    {
                        result.bones=bones;
                        result.rootBone=root;
                        result.sharedMesh=mesh;
                        if(result.bones.Length!=result.sharedMesh.bindposes.Length)
                            throw new InvalidOperationException("Degu preview renderer lost bone bindings");
                        var live=Matching(from,source.transform,living?living.transform:null);
                        result.sharedMaterials=live && live.sharedMaterials.Length==skin.sharedMaterials.Length
                            ?live.sharedMaterials:skin.sharedMaterials;
                        result.localBounds=skin.localBounds;
                        result.updateWhenOffscreen=true;
                        result.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
                        result.enabled=true;
                    }
                    catch
                    {
                        UnityEngine.Object.Destroy(result);
                        throw;
                    }
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
