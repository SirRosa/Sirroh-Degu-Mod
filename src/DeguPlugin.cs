using System;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace SirrohDegu
{
    [BepInPlugin("sirroh.valheim.degu", "Sirroh Degu", "0.4.3")]
    [BepInDependency("sirro.valheim.pracafizyczna", BepInDependency.DependencyFlags.SoftDependency)]
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

            DeguAudioPitch.Multiplier=Config.Bind("Audio","VoicePitchMultiplier",1.45f,
                new ConfigDescription("Pitch multiplier for Degu's cloned Wolf sounds only. "+
                    "1.0 = original Wolf voice; 1.45 = higher, squeaky voice; restart required.",
                    new AcceptableValueRange<float>(1.0f,2.0f))).Value;
            _patches = new Harmony("sirroh.valheim.degu");
            _patches.PatchAll(typeof(DeguPlugin).Assembly);
            DeguWorld.InstallLocalizationHook(_patches);
            DeguWorld.Localize();
            // Rebuilt palette initializes lazily from native Wolf materials.
            // No global ZNetView patches or per-spawn bindpose diagnostic spam.
            DeguCompassIcon.Register(_patches);
            DeguFamiliarBridge.Install();
            DeguPreviewBridge.Install();
            Logger.LogInfo("Sirroh Degu v0.4.3 loaded. Waiting for ZNetScene prefab registry.");
        }

        private void OnDestroy()
        {
            // Appearance service owns no Harmony patches.
            DeguCompassIcon.Shutdown();
            DeguFamiliarBridge.Shutdown();
            DeguPreviewBridge.Shutdown();
            DeguWolfPalette.Shutdown();
            DeguAudioPitch.Shutdown();
            DeguCorpse.Shutdown();
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
                try
                {
                    // Retry optional integrations only after Overhaul and the
                    // game's prefab registries have completed initialization.
                    // Wolf palette resolves independently as game assets load.
                    DeguFamiliarBridge.Install();
                    DeguPreviewBridge.Install();
                    Register(__instance);
                }
                catch (Exception ex) { Log?.LogError("Degu prefab registration FAILED: " + ex); }
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

        private static object GetNamedPrefabs(ZNetScene scene)
        {
            // In Valheim 1.0, m_namedPrefabs is a private field; its backing
            // implementation can differ from the older Dictionary API.
            FieldInfo field = AccessTools.Field(typeof(ZNetScene), "m_namedPrefabs");
            if (field == null) throw new MissingFieldException("ZNetScene.m_namedPrefabs");
            object lookup = field.GetValue(scene);
            if (lookup == null) throw new InvalidOperationException("ZNetScene.m_namedPrefabs is null.");
            return lookup;
        }

        private static bool TryGetNamedPrefab(object lookup, int hash, out GameObject value)
        {
            MethodInfo method = lookup.GetType().GetMethod("TryGetValue",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null, new[] { typeof(int), typeof(GameObject).MakeByRefType() }, null);
            if (method == null) throw new MissingMethodException(lookup.GetType().FullName, "TryGetValue");
            object[] args = { hash, null };
            bool found = (bool)method.Invoke(lookup, args);
            value = found ? (GameObject)args[1] : null;
            return found;
        }

        private static void AddNamedPrefab(object lookup, int hash, GameObject prefab)
        {
            MethodInfo method = lookup.GetType().GetMethod("Add",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null, new[] { typeof(int), typeof(GameObject) }, null);
            if (method == null) throw new MissingMethodException(lookup.GetType().FullName, "Add");
            method.Invoke(lookup, new object[] { hash, prefab });
        }

        private static void RemoveNamedPrefab(object lookup, int hash)
        {
            MethodInfo method = lookup.GetType().GetMethod("Remove",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null, new[] { typeof(int) }, null);
            if (method == null) throw new MissingMethodException(lookup.GetType().FullName, "Remove");
            method.Invoke(lookup, new object[] { hash });
        }
        // Used only for Degu-owned cloned Wolf sound prefabs. This cannot
        // overwrite original sfx_wolf* names or their material/audio settings.
        internal static void RegisterSupplementalPrefab(ZNetScene scene,GameObject prefab)
        {
            if(!scene || !prefab || scene.m_prefabs==null)return;
            int hash=StringExtensionMethods.GetStableHashCode(prefab.name);
            object lookup=GetNamedPrefabs(scene);
            if(TryGetNamedPrefab(lookup,hash,out GameObject existing))
            {
                if(existing!=prefab)
                    throw new InvalidOperationException("Degu sound prefab hash collision: "+prefab.name);
                return;
            }
            if(!scene.m_prefabs.Contains(prefab))scene.m_prefabs.Add(prefab);
            AddNamedPrefab(lookup,hash,prefab);
        }

        private static void Register(ZNetScene scene)
        {
            if (scene == null || scene.m_prefabs == null)
            {
                Log?.LogWarning("ZNetScene prefab collections unavailable; Degu not registered.");
                return;
            }

            int hash = StringExtensionMethods.GetStableHashCode("Degu");
            object namedPrefabs = GetNamedPrefabs(scene);
            GameObject existing;
            if (TryGetNamedPrefab(namedPrefabs, hash, out existing))
            {
                if (existing != null && existing.name == "Degu")
                {
                    // Wolf palette resolves independently as game assets load.
                    DeguWolfPalette.Register(existing);
                    DeguAudioPitch.Configure(scene,existing,GetHolder().transform);
                    DeguCorpse.Register(scene,existing,GetHolder().transform);
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
                    // The Degu renderer is now a NEW stable child of the actor,
                    // not the original Wolf skinned renderer. Keep ONLY the
                    // new stable visual, leaving original bones/Animator live.
                    SkinnedMeshRenderer deguVisual = DeguMesh.ReplaceMesh(target, clone);
                    foreach (var other in clone.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                        if (other != deguVisual) other.enabled = false;

                    var character = clone.GetComponent<Character>();
                    if (character == null) throw new InvalidOperationException("Wolf has no Character component.");
                    if (clone.GetComponent<ZNetView>() == null)
                        throw new InvalidOperationException("Wolf has no ZNetView component.");
                    DeguWorld.Configure(clone);
                    DeguWorld.Localize();
                    // Original Wolf motion/feeding/taming remain; wild targeting
                    // and spawn behavior are intentionally configured separately.
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

            AddNamedPrefab(namedPrefabs, hash, _deguPrefab);
            GameObject resolved = scene.GetPrefab("Degu");
            if (resolved != _deguPrefab)
            {
                RemoveNamedPrefab(namedPrefabs, hash);
                scene.m_prefabs.Remove(_deguPrefab);
                throw new InvalidOperationException("ZNetScene.GetPrefab('Degu') failed after registration.");
            }

            // Wolf palette resolves independently as game assets load.
            DeguWolfPalette.Register(_deguPrefab);
            DeguAudioPitch.Configure(scene,_deguPrefab,GetHolder().transform);
            DeguCorpse.Register(scene,_deguPrefab,GetHolder().transform);
            // Final Degu renderer is staged disabled until clone Start.
            Log?.LogInfo("Degu REGISTERED + VERIFIED: ZNetScene.GetPrefab('Degu') resolved; " +
                         "prefab activeSelf=" + _deguPrefab.activeSelf +
                         ", activeInHierarchy=" + _deguPrefab.activeInHierarchy +
                         ". Console: devcommands; spawn Degu");
        }
    }
}
