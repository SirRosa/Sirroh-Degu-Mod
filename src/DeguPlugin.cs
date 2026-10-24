using System;
using System.Collections.Generic;
using BepInEx;
using HarmonyLib;
using UnityEngine;

namespace SirrohDegu
{
    [BepInPlugin("sirroh.valheim.degu", "Sirroh Degu", "0.1.0")]
    public sealed class DeguPlugin : BaseUnityPlugin
    {
        internal static DeguPlugin Instance;
        private Harmony _patches;

        private void Awake()
        {
            Instance = this;
            _patches = new Harmony("sirroh.valheim.degu");
            _patches.PatchAll(typeof(DeguPlugin).Assembly);
            Logger.LogInfo("Sirroh Degu loaded; prefab will register when ZNetScene starts.");
        }

        private void OnDestroy()
        {
            if (_patches != null) _patches.UnpatchSelf();
            if (Instance == this) Instance = null;
        }

        [HarmonyPatch(typeof(ZNetScene), "Awake")]
        private static class ZNetSceneAwakePatch
        {
            [HarmonyPrefix]
            private static void Prefix(ZNetScene __instance)
            {
                try { Register(__instance); }
                catch (Exception ex) { Instance?.Logger.LogError("Degu prefab registration failed: " + ex); }
            }
        }

        private static void Register(ZNetScene scene)
        {
            if (scene == null || scene.m_prefabs == null) return;
            var prefabs = scene.m_prefabs;
            GameObject wolf = null;
            foreach (GameObject go in prefabs)
            {
                if (go == null) continue;
                if (go.name == "Degu")
                {
                    Instance?.Logger.LogInfo("Degu prefab already registered; skipping duplicate.");
                    return;
                }
                if (go.name == "Wolf") wolf = go;
            }
            if (wolf == null)
            {
                Instance?.Logger.LogWarning("Wolf template not found in ZNetScene; Degu not registered.");
                return;
            }

            GameObject degu = UnityEngine.Object.Instantiate(wolf);
            degu.name = "Degu";
            degu.SetActive(false);
            try
            {
                SkinnedMeshRenderer target = null;
                foreach (var candidate in degu.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    if (candidate == null || candidate.sharedMesh == null) continue;
                    if (target == null || candidate.bones.Length > target.bones.Length) target = candidate;
                }
                if (target == null) throw new InvalidOperationException("Wolf has no skinned mesh renderer.");
                DeguMesh.ReplaceMesh(target);
                foreach (var other in degu.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    if (other != target) other.enabled = false;

                Character creature = degu.GetComponent<Character>();
                if (creature == null) throw new InvalidOperationException("Wolf template has no Character component.");
                creature.m_name = "Degu";
                // Intentionally unchanged: MonsterAI, Tameable, Character stats,
                // Animator/animations, sounds, CharacterDrop, collision and ZNetView.
                prefabs.Add(degu);
                Instance?.Logger.LogInfo("Degu registered. Spawn with devcommands: spawn Degu");
            }
            catch
            {
                UnityEngine.Object.Destroy(degu);
                throw;
            }
        }
    }
}