using System;
using System.Collections;
using System.IO;
using System.Security.Cryptography;
using BepInEx;
using BepInEx.Bootstrap;
using UnityEngine;

namespace SirrohDegu
{
    // Read-only A/B diagnostics. No ZDO writes, changes to AI, renderer materials,
    // mesh, Harmony patches or the Degu variant coroutine.
    internal static class DeguRuntimeAudit
    {
        private const int MaxSpawnReports = 24;
        private const int MaxBindposeReports = 8;
        private static int spawned;
        private static int bindposeErrors;

        internal static void Install()
        {
            spawned = 0;
            bindposeErrors = 0;
            Application.logMessageReceived += OnUnityLog;
            try
            {
                string ownPath = typeof(DeguPlugin).Assembly.Location;
                DeguPlugin.Log?.LogInfo("DEGU_AUDIT plugin=" + Fingerprint(ownPath));
                if (Chainloader.PluginInfos.TryGetValue("sirro.valheim.pracafizyczna", out var overhaul))
                {
                    string path = overhaul.Instance.GetType().Assembly.Location;
                    DeguPlugin.Log?.LogInfo("DEGU_AUDIT overhaul_version=" +
                        overhaul.Metadata.Version + " assembly=" + Fingerprint(path));
                }
                else DeguPlugin.Log?.LogWarning("DEGU_AUDIT Overhaul plugin not loaded");
                string[] duplicates = Directory.GetFiles(Paths.PluginPath,
                    "SirrohDegu.dll", SearchOption.AllDirectories);
                DeguPlugin.Log?.LogInfo("DEGU_AUDIT installed_Degu_dll_count=" + duplicates.Length);
                foreach (string path in duplicates)
                    DeguPlugin.Log?.LogInfo("DEGU_AUDIT installation=" + Fingerprint(path));
            }
            catch (Exception ex)
            {
                DeguPlugin.Log?.LogWarning("DEGU_AUDIT plugin inventory unavailable: " + ex.Message);
            }
        }

        internal static void Shutdown()
        {
            Application.logMessageReceived -= OnUnityLog;
        }

        private static string Fingerprint(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return "path_missing=" + path;
            using (var stream = File.OpenRead(path))
            using (var hash = SHA256.Create())
                return path + " sha256=" + BitConverter.ToString(hash.ComputeHash(stream))
                    .Replace("-", "").ToLowerInvariant();
        }

        internal static void ReportPrefab(GameObject prefab)
        {
            if (!prefab) return;
            var variant = prefab.GetComponent<DeguVariantAssignment>();
            DeguPlugin.Log?.LogInfo("DEGU_AUDIT prefab=" + prefab.name +
                " compat=" + OverhaulCompatibility.Enabled +
                " randomizer=" + (variant != null && variant.enabled) +
                " visual=" + RendererState(prefab));
        }

        internal static void ReportSpawn(ZNetView view)
        {
            if (!view || view.gameObject.name != "Degu(Clone)" ||
                DeguPlugin.Instance == null || ++spawned > MaxSpawnReports)
                return;
            DeguPlugin.Instance.StartCoroutine(ReportSpawnAfterSync(view, spawned));
        }

        private static IEnumerator ReportSpawnAfterSync(ZNetView view, int ordinal)
        {
            // Wait for the original DeguVariantAssignment to write/sync the ZDO.
            // This audit never initializes cosmetic keys itself.
            yield return new WaitForSeconds(4f);
            if (!view || !view.gameObject) yield break;
            try
            {
                if (!view.IsValid())
                {
                    DeguPlugin.Log?.LogWarning("DEGU_AUDIT spawn=" + ordinal + " view_invalid");
                    yield break;
                }
                ZDO zdo = view.GetZDO();
                if (zdo == null) yield break;
                DeguPlugin.Log?.LogInfo("DEGU_AUDIT spawn=" + ordinal +
                    " id=" + zdo.m_uid +
                    " network_owner=" + zdo.GetOwner() +
                    " local_is_owner=" + view.IsOwner() +
                    " active=" + zdo.GetBool("furRandomizerSetActive", false) +
                    " coat=" + zdo.GetInt("furRandomizerSet", -1) +
                    " eye=" + zdo.GetInt("sirroh.wolf.eye.v1", -1) +
                    " randomizer=" + (view.GetComponent<DeguVariantAssignment>() != null) +
                    " visible=" + RendererState(view.gameObject));
            }
            catch (Exception ex)
            {
                DeguPlugin.Log?.LogWarning("DEGU_AUDIT spawn=" + ordinal +
                    " inspection_failed=" + ex.Message);
            }
        }

        private static string RendererState(GameObject source)
        {
            foreach (SkinnedMeshRenderer skin in
                source.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (!skin || skin.name != "SirrohDegu_StableVisual") continue;
                var mesh = skin.sharedMesh;
                var block = new MaterialPropertyBlock();
                skin.GetPropertyBlock(block, 1);
                return "enabled=" + skin.enabled +
                    ",mesh=" + (mesh ? mesh.name : "none") +
                    ",bindposes=" + (mesh ? mesh.bindposes.Length : -1) +
                    ",bones=" + skin.bones.Length +
                    ",material=" + (skin.sharedMaterial ? skin.sharedMaterial.name : "none") +
                    ",eye_texture=" + (block.GetTexture("_MainTex")
                        ? block.GetTexture("_MainTex").name : "none");
            }
            return "renderer_missing";
        }

        private static void OnUnityLog(string message, string stackTrace, LogType type)
        {
            if (message == null || !message.Contains("Bones do not match bindpose") ||
                ++bindposeErrors > MaxBindposeReports) return;
            DeguPlugin.Log?.LogWarning("DEGU_AUDIT bindpose_event=" + bindposeErrors +
                " unity_type=" + type +
                " stack=" + (string.IsNullOrEmpty(stackTrace) ? "(empty)" :
                    stackTrace.Substring(0, Math.Min(stackTrace.Length, 1600)).Replace("\n", " | ")));
            // Do not claim an invalid renderer from a scan performed AFTER Unity's event.
        }
    }
}
