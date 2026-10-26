using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;

namespace SirrohDegu
{
    // The only Degu-specific preview renderer implementation. Overhaul owns
    // all UI, familiar records, requests and ZDO state.
    internal static class DeguPreviewRenderer
    {
        private const BindingFlags PrivateInstance =
            BindingFlags.Instance | BindingFlags.NonPublic;

        // Prefix to FamiliarModelPreview.CopyRenderers, ONLY for prefab Degu.
        internal static bool CopyRenderers(object __instance,
            GameObject source, GameObject living)
        {
            if (!source || source.name != "Degu") return true;
            try
            {
                var map = __instance.GetType().GetField("_transforms",PrivateInstance)
                    ?.GetValue(__instance) as Dictionary<Transform,Transform>;
                if (map==null || !map.ContainsKey(source.transform))
                    throw new InvalidOperationException("Degu preview transform hierarchy absent.");

                foreach(var pair in map)
                {
                    Transform from=pair.Key;
                    Transform into=pair.Value;
                    if(!from || !into ||
                       (from!=source.transform && !from.gameObject.activeSelf)) continue;

                    MeshFilter sourceFilter=from.GetComponent<MeshFilter>();
                    MeshRenderer sourceMesh=from.GetComponent<MeshRenderer>();
                    if(sourceFilter && sourceMesh && sourceMesh.enabled &&
                       sourceFilter.sharedMesh)
                    {
                        // Non-skinned geometry follows the normal native copy path.
                        MeshFilter filter=into.gameObject.AddComponent<MeshFilter>();
                        filter.sharedMesh=sourceFilter.sharedMesh;
                        MeshRenderer copied=into.gameObject.AddComponent<MeshRenderer>();
                        copied.sharedMaterials=sourceMesh.sharedMaterials;
                        copied.shadowCastingMode=ShadowCastingMode.Off;
                        copied.receiveShadows=false;
                    }

                    SkinnedMeshRenderer skin=from.GetComponent<SkinnedMeshRenderer>();
                    if(!skin || !skin.enabled || !skin.sharedMesh) continue;

                    // Check all bindings BEFORE allocating an active renderer.
                    // No incomplete sharedMesh/bones states and no arbitrary
                    // transform substitution for missing bone references.
                    Transform[] originalBones=skin.bones;
                    int bindCount=skin.sharedMesh.bindposes.Length;
                    if(originalBones==null || bindCount==0 ||
                       bindCount!=originalBones.Length)
                    {
                        DeguPlugin.Log?.LogError("Degu preview: invalid source binding on "+
                            from.name+": mesh bindposes="+bindCount+
                            ", bones="+(originalBones==null?-1:originalBones.Length));
                        continue;
                    }
                    var mapped=new Transform[originalBones.Length];
                    bool valid=true;
                    for(int i=0;i<originalBones.Length;i++)
                    {
                        Transform resolved;
                        if(!originalBones[i] ||
                           !map.TryGetValue(originalBones[i],out resolved) || !resolved)
                        { valid=false; break; }
                        mapped[i]=resolved;
                    }
                    Transform rootBone=null;
                    if(skin.rootBone &&
                       (!map.TryGetValue(skin.rootBone,out rootBone) || !rootBone))
                        valid=false;
                    if(!valid)
                    {
                        DeguPlugin.Log?.LogError(
                            "Degu preview: unmapped source bone/root in "+from.name);
                        continue;
                    }

                    // This child is completely inactive until the renderer has
                    // a mesh, exactly matching its already-mapped bone array.
                    // Prevent native Unity skin evaluation during construction.
                    bool originalActive=into.gameObject.activeSelf;
                    into.gameObject.SetActive(false);
                    try
                    {
                        var copied=into.gameObject.AddComponent<SkinnedMeshRenderer>();
                        copied.enabled=false;
                        copied.bones=mapped;
                        copied.rootBone=rootBone;
                        copied.sharedMesh=skin.sharedMesh;
                        SkinnedMeshRenderer live=Matching(
                            from,source.transform,living?living.transform:null);
                        copied.sharedMaterials=live &&
                            live.sharedMaterials.Length==skin.sharedMaterials.Length
                            ?live.sharedMaterials:skin.sharedMaterials;
                        copied.localBounds=skin.localBounds;
                        copied.updateWhenOffscreen=true;
                        copied.shadowCastingMode=ShadowCastingMode.Off;
                        copied.receiveShadows=false;
                        copied.enabled=true;
                    }
                    finally
                    {
                        into.gameObject.SetActive(originalActive);
                    }
                }
            }
            catch(Exception ex)
            {
                DeguPlugin.Log?.LogError(
                    "Degu preview binding failed: "+ex.GetBaseException());
            }
            return false;
        }

        private static SkinnedMeshRenderer Matching(
            Transform source,Transform root,Transform living)
        {
            if(!living)return null;
            var path=new Stack<string>();
            for(Transform node=source;node && node!=root;node=node.parent)
                path.Push(node.name);
            while(path.Count>0)
            {
                living=living.Find(path.Pop());
                if(!living)return null;
            }
            return living.GetComponent<SkinnedMeshRenderer>();
        }
    }
}
