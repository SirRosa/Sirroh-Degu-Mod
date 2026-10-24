using System;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace SirrohDegu
{
    [BepInPlugin("sirroh.valheim.degu", "Sirroh Degu", "0.1.1")]
    public sealed class DeguPlugin : BaseUnityPlugin
    {
        internal static DeguPlugin Instance;
        internal static ManualLogSource Log;

        // Keep the network prefab under an inactive parent, just like a prefab asset.
        // A clone of this prefab will be active, while the template never runs Awake.
        private static GameObject _prefabHolder;
        private static GameObject _deguPrefab;
        private Harmony _patches;

        private void Awake()
        {
            Instance = this;
            Log = Logger;
            _patches = new Harmony("sirroh.valheim.degu");
            _patches.PatchAll(typeof(DeguPlugin).Assembly);
            Logger.LogInfo("Sirroh Degu v0.1.1 loaded. Waiting for ZNetScene prefab registry.");
        }

        private void OnDestroy()
        {
            if (_patches != null) _patches.UnpatchSelf();
            if (_prefabHolder != null) UnityEngine.Object.Destroy(_prefabHolder);
            _prefabHolder = null;
            _deguPrefab = null;
            if (Instance == this) { Instance = null; Log = null; }
        }

        [HarmonyPatch(typeof(ZNetScene), "Awake")]
        private static class ZNetSceneAwakePatch
        {
            // The original ZNetScene.Awake builds m_namedPrefabs.
            // A prefix can only change m_prefabs, and an active Instantiate from
            // that prefix calls ZNetView.Awake before ZNetScene initialization.
            [HarmonyPostfix]
            private static void Postfix(ZNetScene __instance)
            {
                try { Register(__instance); }
                catch (Exception ex) { Log?.LogError("Degu prefab registration FAILED: " + ex); }
            }
        }

        [HarmonyPatch(typeof(ZNetView), "Awake")]
        private static class DeguSpawnDiagnostic
        {
            // Diagnostic only: executed for all views, logging only Degu spawns.
            [HarmonyPostfix]
            private static void Postfix(ZNetView __instance)
            {
                if (__instance != null && __instance.gameObject != null &&
                    __instance.gameObject.name == "Degu(Clone)")
                    Log?.LogInfo("Degu(Clone): ZNetView.Awake executed (runtime spawn initialized).");
            }
        }

        private static GameObject GetHolder()
        {
            if (_prefabHolder != null) return _prefabHolder;
            _prefabHolder = new GameObject("SirrohDegu_InactivePrefabs");
            _prefabHolder.SetActive(false);
            UnityEngine.Object.DontDestroyOnLoad(_prefabHolder);
            return _prefabHolder;
        }

        private static void Register(ZNetScene scene)
        {
            if (scene == null || scene.m_prefabs == null || scene.m_namedPrefabs == null)
            {
                Log?.LogWarning("ZNetScene prefab collections unavailable; Degu not registered.");
                return;
            }

            int hash = "Degu".GetStableHashCode();
            GameObject existing;
            if (scene.m_namedPrefabs.TryGetValue(hash, out existing))
            {
                if (existing != null && existing.name == "Degu")
                {
                    Log?.LogInfo("Degu already exists in ZNetScene.GetPrefab; no duplicate.");
                    return;
                }
                throw new InvalidOperationException(
                    "Degu prefab name hash collides with another registered prefab.");
            }

            if (_deguPrefab == null)
            {
                GameObject wolf = scene.GetPrefab("Wolf");
                if (wolf == null)
                {
                    foreach (GameObject prefab in scene.m_prefabs)
                        if (prefab != null && prefab.name == "Wolf") { wolf = prefab; break; }
                }
                if (wolf == null)
                {
                    Log?.LogWarning("Wolf prefab not found in ZNetScene; cannot create Degu.");
                    return;
                }

                // Never Instantiate an active network prefab into an active scene during
                // ZNetScene setup: it invokes ZNetView.Awake with an incomplete network state.
                // The holder is inactiveInHierarchy; preserve the game's temporary
                // disable-initialization flag to protect nested ZNetViews and other mods.
                GameObject holder = GetHolder();
                if (holder.activeInHierarchy)
                    throw new InvalidOperationException("Degu prefab holder must be inactive.");

                bool oldDisable = ZNetView.m_forceDisableInit;
                GameObject clone;
                try
                {
                    ZNetView.m_forceDisableInit = true;
                    clone = UnityEngine.Object.Instantiate(wolf, holder.transform);
                }
                finally
                {
                    ZNetView.m_forceDisableInit = oldDisable;
                }
                if (clone == null) throw new InvalidOperationException("Unable to clone Wolf prefab.");

                clone.name = "Degu";
                // Inactive parent suppresses Awake, but activeSelf=true is essential:
                // it allows normal ZNetScene spawning to activate this network creature.
                clone.SetActive(true);
                if (clone.activeInHierarchy)
                {
                    UnityEngine.Object.Destroy(clone);
                    throw new InvalidOperationException("Degu clone was activated while staging.");
                }

                try
                {
                    SkinnedMeshRenderer target = null;
                    foreach (var candidate in clone.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    {
                        if (candidate == null || candidate.sharedMesh == null) continue;
                        if (target == null || candidate.bones.Length > target.bones.Length)
                            target = candidate;
                    }
                    if (target == null) throw new InvalidOperationException("Wolf has no skinned mesh renderer.");
                    DeguMesh.ReplaceMesh(target);
                    foreach (var other in clone.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                        if (other != target) other.enabled = false;

                    var character = clone.GetComponent<Character>();
                    if (character == null) throw new InvalidOperationException("Wolf has no Character component.");
                    if (clone.GetComponent<ZNetView>() == null)
                        throw new InvalidOperationException("Wolf has no ZNetView component.");
                    character.m_name = "Degu";
                    // The cloned Wolf retains AI, taming, combat, sounds, animations,
                    // drops and physics. No new SpawnSystem entries are created.
                    _deguPrefab = clone;
                }
                catch
                {
                    UnityEngine.Object.Destroy(clone);
                    throw;
                }
            }

            if (!scene.m_prefabs.Contains(_deguPrefab))
                scene.m_prefabs.Add(_deguPrefab);

            scene.m_namedPrefabs.Add(hash, _deguPrefab);
            GameObject resolved = scene.GetPrefab("Degu");
            if (resolved != _deguPrefab)
            {
                scene.m_namedPrefabs.Remove(hash);
                scene.m_prefabs.Remove(_deguPrefab);
                throw new InvalidOperationException("ZNetScene.GetPrefab('Degu') failed after registration.");
            }

            Log?.LogInfo("Degu REGISTERED + VERIFIED: ZNetScene.GetPrefab('Degu') resolved; " +
                         "prefab activeSelf=" + _deguPrefab.activeSelf +
                         ", activeInHierarchy=" + _deguPrefab.activeInHierarchy +
                         ". Console: devcommands; spawn Degu");
        }
    }
}
