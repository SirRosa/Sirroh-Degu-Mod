using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace SirrohDegu
{
    /// <summary>
    /// A separate NETWORKED Degu corpse prefab built from Wolf_Ragdoll.
    /// Retains Valheim's Ragdoll, joints, colliders, drops and decay timer;
    /// replaces ONLY the inherited Wolf visuals and the Degu death-effect
    /// reference. Never modifies Wolf or Wolf_Ragdoll.
    /// </summary>
    internal static class DeguCorpse
    {
        internal const string PrefabName="DeguRagdoll";
        private const string WrittenKey="sirroh.degu.corpse.appearance.v1";
        private static GameObject prefab;
        private static readonly MethodInfo CloneFields=typeof(object).GetMethod(
            "MemberwiseClone",BindingFlags.Instance|BindingFlags.NonPublic);

        private sealed class DeathSnapshot
        {
            internal Vector3 Position;
            internal float At;
            internal int Coat;
            internal int Eye;
            internal int Element;
            internal int Level;
            internal bool Runes;
        }

        // Only the local ZDO owner captures the death of an actual Degu.
        // The resulting corpse's ZDO receives the exact values once. Other
        // clients read that corpse's ZDO instead of rolling random variants.
        private static readonly List<DeathSnapshot> Pending=new List<DeathSnapshot>();

        [HarmonyPatch(typeof(Character),"OnDeath")]
        private static class RememberDeguDeath
        {
            [HarmonyPrefix]
            private static void Before(Character __instance)
            {
                if(!__instance || !__instance.gameObject ||
                   !__instance.gameObject.name.StartsWith("Degu(",StringComparison.Ordinal))
                    return;
                ZNetView view=__instance.GetComponent<ZNetView>();
                if(!view || !view.IsValid() || !view.IsOwner())return;
                ZDO zdo=view.GetZDO();
                if(zdo==null)return;
                float now=Time.time;
                for(int i=Pending.Count-1;i>=0;i--)
                    if(now-Pending[i].At>8f)Pending.RemoveAt(i);
                if(Pending.Count>=64)Pending.RemoveAt(0);
                Pending.Add(new DeathSnapshot{
                    Position=__instance.transform.position,
                    At=now,
                    Coat=Mathf.Clamp(zdo.GetInt(DeguWolfPalette.SkinKey,0),0,6),
                    Eye=Mathf.Clamp(zdo.GetInt(DeguWolfPalette.EyeKey,1),1,6),
                    Element=Mathf.Clamp(zdo.GetInt(DeguWolfPalette.ElementKey,0),0,5),
                    Level=Mathf.Clamp(zdo.GetInt(DeguWolfPalette.LevelKey,1),1,100),
                    Runes=zdo.GetBool(DeguWolfPalette.RunesKey,true)
                });
            }
        }

        private static bool TryConsume(Vector3 position,out int coat,out int eye,
            out int element,out int level,out bool runes)
        {
            coat=0;eye=1;element=0;level=1;runes=false;
            float now=Time.time;
            int best=-1;
            float bestSq=3.0f*3.0f;
            for(int i=Pending.Count-1;i>=0;i--)
            {
                var p=Pending[i];
                if(now-p.At>8f){Pending.RemoveAt(i);continue;}
                float dist=(p.Position-position).sqrMagnitude;
                if(dist<=bestSq){best=i;bestSq=dist;}
            }
            if(best<0)return false;
            var s=Pending[best];
            Pending.RemoveAt(best);
            coat=s.Coat;eye=s.Eye;element=s.Element;level=s.Level;runes=s.Runes;
            return true;
        }

        internal static void Register(ZNetScene scene,GameObject degu,Transform holder)
        {
            if(!scene || !degu || !holder)return;
            if(holder.gameObject.activeInHierarchy)
                throw new InvalidOperationException("DeguRagdoll prefab holder must be inactive.");

            if(!prefab)
            {
                GameObject original=scene.GetPrefab("Wolf_Ragdoll");
                if(!original)
                {
                    DeguPlugin.Log?.LogWarning("DEGU_CORPSE: native Wolf_Ragdoll not found; keeping original death effects.");
                    return;
                }

                GameObject candidate=null;
                bool previous=ZNetView.m_forceDisableInit;
                try
                {
                    ZNetView.m_forceDisableInit=true;
                    candidate=UnityEngine.Object.Instantiate(original,holder);
                }
                finally {ZNetView.m_forceDisableInit=previous;}
                if(!candidate)throw new InvalidOperationException("Could not clone native Wolf_Ragdoll.");
                candidate.name=PrefabName;
                try
                {
                    if(candidate.activeInHierarchy)
                        throw new InvalidOperationException("DeguRagdoll unexpectedly activated during staging.");
                    if(!candidate.GetComponent<Ragdoll>() || !candidate.GetComponent<ZNetView>())
                        throw new InvalidOperationException("Native Wolf_Ragdoll is missing Ragdoll or ZNetView.");

                    SkinnedMeshRenderer source=null;
                    foreach(SkinnedMeshRenderer skin in candidate.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                        if(skin && skin.sharedMesh && skin.bones!=null &&
                           skin.bones.Length==34 && skin.sharedMesh.bindposes.Length==34)
                        {source=skin;break;}
                    if(!source)
                        throw new InvalidOperationException(
                            "Wolf_Ragdoll has no complete 34-bone Wolf renderer; refusing to replace death effects.");

                    // Same validated mesh, FaceSets, UVs and 34-bone fit as the
                    // living Degu. Source ragdoll bones remain driven by PHYSICS.
                    SkinnedMeshRenderer visual=DeguMesh.ReplaceMesh(source,candidate);
                    foreach(var skin in candidate.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                        if(skin && skin!=visual)skin.enabled=false;
                    foreach(LODGroup lod in candidate.GetComponentsInChildren<LODGroup>(true))
                        if(lod)lod.enabled=false;

                    // The inherited wolf-sized physics hull does not match
                    // the much smaller visible Degu. Add a compact ground
                    // contact to an EXISTING native ragdoll Rigidbody.
                    // The native joints, collisions, decay and loot remain.
                    try { DeguCorpseGroundSupport.Configure(candidate); }
                    catch(Exception ex)
                    {
                        // A physics configuration difference between Valheim
                        // builds must NEVER revert Degu's corpse to Wolf or
                        // prevent native ragdoll/loot creation.
                        DeguPlugin.Log?.LogWarning("DEGU_CORPSE: torso contact unavailable; "+
                            "keeping native DeguRagdoll instead of losing corpse: "+
                            ex.GetBaseException().Message);
                    }

                    if(!candidate.GetComponent<DeguCorpseVisual>())
                        candidate.AddComponent<DeguCorpseVisual>();
                    prefab=candidate;
                }
                catch
                {
                    UnityEngine.Object.Destroy(candidate);
                    throw;
                }
            }

            // Separate network prefab; never changes the Wolf_Ragdoll record.
            DeguPlugin.RegisterSupplementalPrefab(scene,prefab);
            RewireDeathEffect(degu,prefab);
        }

        private static void RewireDeathEffect(GameObject degu,GameObject replacement)
        {
            Character actor=degu.GetComponent<Character>();
            if(!actor)throw new InvalidOperationException("Degu Character missing while linking corpse.");
            EffectList original=actor.m_deathEffects;
            if(original==null || original.m_effectPrefabs==null)
                throw new InvalidOperationException("Degu death EffectList missing.");
            var oldEntries=original.m_effectPrefabs;
            var entries=(EffectList.EffectData[])oldEntries.Clone();
            int changed=0;
            for(int i=0;i<oldEntries.Length;i++)
            {
                var entry=oldEntries[i];
                if(entry==null || !entry.m_prefab)continue;
                if(entry.m_prefab==replacement)continue;
                if(entry.m_prefab.name!="Wolf_Ragdoll")continue;
                object copy=CloneFields.Invoke(entry,null);
                var isolated=copy as EffectList.EffectData;
                if(isolated==null)throw new InvalidOperationException("Cannot copy ragdoll EffectData.");
                isolated.m_prefab=replacement;
                entries[i]=isolated;
                changed++;
            }
            if(changed==0)
            {
                foreach(var entry in entries)
                    if(entry!=null && entry.m_prefab==replacement)return; // repeated scene setup
                throw new InvalidOperationException(
                    "Original Degu death effects have no Wolf_Ragdoll entry; cannot safely rewire.");
            }
            // Deep-copy the container and only the changed entry; no mutation
            // of the Wolf's potentially shared EffectList or EffectData.
            var isolatedList=CloneFields.Invoke(original,null) as EffectList;
            if(isolatedList==null)throw new InvalidOperationException("Cannot copy EffectList.");
            isolatedList.m_effectPrefabs=entries;
            actor.m_deathEffects=isolatedList;
            DeguPlugin.Log?.LogInfo("DEGU_CORPSE: native death effects now use DeguRagdoll; Wolf loot and physics retained.");
        }

        internal static void Shutdown()
        {
            Pending.Clear();
            // _prefabHolder lifecycle owns the staged ragdoll. Do not destroy
            // separate ZNetScene instance objects or their network ZDOs.
            prefab=null;
        }

        // Called exclusively by the corpse's own MonoBehaviour, not by a
        // global character polling loop.
        internal static void InitializeAppearance(ZDO zdo,Vector3 position)
        {
            if(zdo.GetBool(WrittenKey,false))return;
            TryConsume(position,out int coat,out int eye,out int element,out int level,out bool runes);
            zdo.Set(DeguWolfPalette.SkinKey,coat);
            zdo.Set(DeguWolfPalette.ActiveKey,true);
            zdo.Set(DeguWolfPalette.EyeKey,eye);
            zdo.Set(DeguWolfPalette.ElementKey,element);
            zdo.Set(DeguWolfPalette.LevelKey,level);
            zdo.Set(DeguWolfPalette.RunesKey,runes);
            zdo.Set(WrittenKey,true);
        }

        internal static bool HasAppearance(ZDO zdo)=>zdo!=null && zdo.GetBool(WrittenKey,false);
    }

    /// <summary>Network client renderer, no new loot or physics behaviour.</summary>
    public sealed class DeguCorpseVisual : MonoBehaviour
    {
        private IEnumerator Start()
        {
            yield return null; // native Ragdoll / ZNetView Awake completed
            SkinnedMeshRenderer visual=DeguWolfPalette.Renderer(gameObject);
            if(visual && visual.sharedMesh && visual.rootBone &&
               visual.bones.Length==visual.sharedMesh.bindposes.Length)
                visual.enabled=true;
            else
            {
                DeguPlugin.Log?.LogError("DEGU_CORPSE: cloned renderer bone map invalid; not enabling.");
                yield break;
            }
            ZNetView view=GetComponent<ZNetView>();
            if(!view)yield break;
            int appliedCoat=-1,appliedEye=-1,appliedElement=-1;
            bool applied=false;
            for(int i=0;i<150 && view && view.IsValid();i++)
            {
                ZDO zdo=view.GetZDO();
                if(zdo!=null)
                {
                    if(view.IsOwner() && !DeguCorpse.HasAppearance(zdo))
                        DeguCorpse.InitializeAppearance(zdo,transform.position);
                    if(DeguCorpse.HasAppearance(zdo))
                    {
                        int coat=Mathf.Clamp(zdo.GetInt(DeguWolfPalette.SkinKey,0),0,6);
                        int eye=Mathf.Clamp(zdo.GetInt(DeguWolfPalette.EyeKey,1),1,6);
                        int level=zdo.GetInt(DeguWolfPalette.LevelKey,1);
                        int element=zdo.GetBool(DeguWolfPalette.RunesKey,false) && level>=25?
                            Mathf.Clamp(zdo.GetInt(DeguWolfPalette.ElementKey,0),0,5):0;
                        if(!applied || coat!=appliedCoat || eye!=appliedEye ||
                           element!=appliedElement)
                        {
                            if(DeguWolfPalette.Apply(gameObject,coat,eye,element))
                            {
                                applied=true;
                                appliedCoat=coat;
                                appliedEye=eye;
                                appliedElement=element;
                            }
                        }
                        if(applied)yield break; // ragdoll appearance is immutable
                    }
                }
                yield return new WaitForSeconds(.2f);
            }
        }
    }
}
