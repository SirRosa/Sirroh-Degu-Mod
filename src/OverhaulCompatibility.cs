using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using BepInEx.Bootstrap;
using HarmonyLib;
using UnityEngine;

namespace SirrohDegu
{
    // Compatibility for the EXACT, unchanged Sirroh Overhaul 0.22.65 assembly.
    // Never compile against, overwrite, or replace any Overhaul binary.
    internal static class OverhaulCompatibility
    {
        private const string PluginId = "sirro.valheim.pracafizyczna";
        private const string SupportedVersion = "0.22.65";
        private const string DeguRenderer = "SirrohDegu_StableVisual";
        internal const string SkinKey = "furRandomizerSet";
        internal const string ActiveKey = "furRandomizerSetActive";
        internal const string EyeKey = "sirroh.wolf.eye.v1";
        private const string LevelKey = "sirroh.pet.level.v1";
        private const string ElementKey = "sirroh.pet.element.v1";
        private const string RuneKey = "sirroh.wolf.showrunes.v1";

        private static Harmony patches;
        private static FieldInfo variantsField, mainField;
        private static MethodInfo assign, wolfApply, applyProperties, ensureEye, tryApply;
        private static bool warned;
        internal static bool Enabled { get; private set; }

        private static Type Required(string name, Assembly assembly)
        {
            Type type = assembly.GetType("Sirro.PracaFizyczna." + name, false);
            if (type == null) throw new MissingMemberException("Missing Overhaul type " + name);
            return type;
        }

        private static MethodInfo RequiredMethod(Type type, string name)
        {
            MethodInfo method = AccessTools.Method(type, name);
            if (method == null) throw new MissingMethodException(type.FullName, name);
            return method;
        }

        internal static void Install()
        {
            if (!Chainloader.PluginInfos.TryGetValue(PluginId, out var plugin)) return;
            if (plugin.Metadata.Version.ToString() != SupportedVersion)
            {
                DeguPlugin.Log?.LogWarning("Overhaul compatibility disabled: only exact v" +
                    SupportedVersion + " is supported; installed " + plugin.Metadata.Version);
                return;
            }
            try
            {
                Assembly assembly = plugin.Instance.GetType().Assembly;
                Type catalog = Required("MaterialCatalog", assembly);
                Type visuals = Required("WolfVisuals", assembly);
                Type taming = Required("TamingSkill", assembly);
                Type panel = Required("FamiliarMenuPanel", assembly);
                Type preview = Required("FamiliarModelPreview", assembly);
                Type skin = Required("SkinAssignment", assembly);
                variantsField = AccessTools.Field(catalog, "Variants");
                if (variantsField == null) throw new MissingFieldException(catalog.FullName, "Variants");
                Type variant = Required("Variant", assembly);
                mainField = AccessTools.Field(variant, "Main");
                if (mainField == null) throw new MissingFieldException(variant.FullName, "Main");
                assign = RequiredMethod(catalog, "Assign");
                wolfApply = RequiredMethod(visuals, "Apply");
                applyProperties = RequiredMethod(visuals, "ApplyProperties");
                ensureEye = RequiredMethod(visuals, "EnsureEye");
                tryApply = RequiredMethod(catalog, "TryApply");

                // Preflight EVERY method before installing any Harmony patch.
                var wolfChecks = new[] {
                    RequiredMethod(taming,"ChooseEyeColor"),
                    RequiredMethod(taming,"ToggleElementRunes"),
                    RequiredMethod(taming,"Capture"),
                    RequiredMethod(taming,"SyncFamiliar"),
                    RequiredMethod(taming,"HandleLifecycleRequest"),
                    RequiredMethod(panel,"UpdateDetail"),
                    RequiredMethod(preview,"Show")
                };
                var count = RequiredMethod(catalog,"Count");
                var display = RequiredMethod(catalog,"DisplayName");
                var texture = RequiredMethod(catalog,"PreviewTexture");
                var random = RequiredMethod(catalog,"RandomIndex");
                var supported = RequiredMethod(catalog,"IsSupported");
                var previewVisuals = RequiredMethod(visuals,"ApplyPreview");

                patches = new Harmony("sirroh.degu.overhaul.v02265");
                foreach (MethodInfo target in wolfChecks)
                    patches.Patch(target, transpiler: new HarmonyMethod(
                        AccessTools.Method(typeof(OverhaulCompatibility), nameof(AllowDeguChecks))));
                foreach (MethodInfo target in new[]{count,display,texture})
                    patches.Patch(target, prefix: new HarmonyMethod(
                        AccessTools.Method(typeof(OverhaulCompatibility),nameof(AliasSpecies))));
                patches.Patch(random, prefix: new HarmonyMethod(
                    AccessTools.Method(typeof(OverhaulCompatibility),nameof(AliasAnimal))));
                patches.Patch(supported, postfix: new HarmonyMethod(
                    AccessTools.Method(typeof(OverhaulCompatibility),nameof(SupportedDegu))));
                patches.Patch(tryApply, prefix: new HarmonyMethod(
                    AccessTools.Method(typeof(OverhaulCompatibility),nameof(ApplyDeguPrefix))));
                patches.Patch(previewVisuals, prefix: new HarmonyMethod(
                    AccessTools.Method(typeof(OverhaulCompatibility),nameof(DeguPreviewPrefix))));
                Enabled = true;
                DeguPlugin.Log?.LogInfo("Overhaul v0.22.65 adapter active: 7 coats, 6 eyes, runes and Tresura.");
            }
            catch (Exception exception)
            {
                Enabled = false;
                if (patches != null) patches.UnpatchSelf();
                patches = null;
                DeguPlugin.Log?.LogError("Overhaul compatibility disabled; failed preflight/patch: " + exception);
            }
        }

        internal static void Shutdown()
        {
            Enabled = false;
            if (patches != null) patches.UnpatchSelf();
            patches = null;
        }

        internal static void AfterDeguRegistered(GameObject prefab)
        {
            if (!Enabled || !prefab || prefab.GetComponent<DeguVariantAssignment>()) return;
            prefab.AddComponent<DeguVariantAssignment>();
            DeguPlugin.Log?.LogInfo("Degu variant assignment registered on actual network prefab.");
        }

        private static bool Degu(string animal) =>
            string.Equals(animal, "Degu", StringComparison.OrdinalIgnoreCase);

        // Only extend comparisons against literal "Wolf" in the seven exact
        // Overhaul 0.22.65 methods. No global alias or Wolf prefab replacement.
        private static bool WolfOrDeguEquals(string a, string b) =>
            string.Equals(a,b,StringComparison.Ordinal) ||
            (a == "Degu" && b == "Wolf") || (a == "Wolf" && b == "Degu");

        private static bool WolfOrDeguNotEquals(string a, string b) =>
            !WolfOrDeguEquals(a,b);

        private static IEnumerable<CodeInstruction> AllowDeguChecks(
            IEnumerable<CodeInstruction> instructions, MethodBase original)
        {
            var codes = instructions.ToList();
            MethodInfo equal = AccessTools.Method(typeof(string),"op_Equality");
            MethodInfo notEqual = AccessTools.Method(typeof(string),"op_Inequality");
            MethodInfo oursEqual = AccessTools.Method(typeof(OverhaulCompatibility),nameof(WolfOrDeguEquals));
            MethodInfo oursNotEqual = AccessTools.Method(typeof(OverhaulCompatibility),nameof(WolfOrDeguNotEquals));
            int replaced=0;
            for (int i=0;i+1<codes.Count;i++)
            {
                if (codes[i].opcode != OpCodes.Ldstr || !Equals(codes[i].operand,"Wolf")) continue;
                // C# emits ldstr "Wolf"; call string.op_Equality/Inequality.
                for (int j=i+1;j<=Math.Min(i+2,codes.Count-1);j++)
                {
                    if (codes[j].operand is MethodInfo method && method == equal)
                    { codes[j].operand=oursEqual; replaced++; break; }
                    if (codes[j].operand is MethodInfo other && other == notEqual)
                    { codes[j].operand=oursNotEqual; replaced++; break; }
                }
            }
            if (replaced==0) throw new InvalidOperationException(
                "Wolf comparison contract changed in " + original.DeclaringType.FullName + "." + original.Name);
            return codes;
        }

        private static void AliasSpecies(ref string species)
        {
            if (Degu(species)) species="Wolf";
        }
        private static void AliasAnimal(ref string animal)
        {
            if (Degu(animal)) animal="Wolf";
        }
        private static void SupportedDegu(string name, ref bool __result)
        {
            if (Degu(name)) __result=true;
        }

        private static SkinnedMeshRenderer Visible(GameObject creature)
        {
            if (!creature) return null;
            foreach (var renderer in creature.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (renderer && renderer.name == DeguRenderer) return renderer;
            return null;
        }

        // MaterialCatalog's default renderer selection would recolor Degu's
        // hidden Wolf renderer. Replace ONLY Degu's branch and leave other
        // creatures entirely on the original Overhaul implementation.
        private static bool ApplyDeguPrefix(GameObject animalObject, string animal,
            int index, ref bool __result)
        {
            if (!Degu(animal)) return true;
            __result=false;
            try
            {
                var renderer=Visible(animalObject);
                var variants=variantsField.GetValue(null) as IDictionary;
                var skins=variants==null?null:variants["Wolf"] as Array;
                if (!renderer || skins==null || skins.Length==0) return false;
                if (index<0 || index>=skins.Length) index=0;
                object chosen=skins.GetValue(index) ?? skins.GetValue(0);
                Material material=chosen==null?null:mainField.GetValue(chosen) as Material;
                if (!material) return false;
                assign.Invoke(null,new object[]{renderer,0,material,true});
                assign.Invoke(null,new object[]{renderer,1,material,true});
                wolfApply.Invoke(null,new object[]{animalObject,renderer,material});
                CopyEyeSubmesh(renderer);
                __result=true;
            }
            catch (Exception e) { WarnOnce("Degu coat apply: "+e.GetBaseException().Message); }
            return false;
        }

        private static void CopyEyeSubmesh(SkinnedMeshRenderer renderer)
        {
            if (!renderer || renderer.sharedMaterials==null || renderer.sharedMaterials.Length<2) return;
            var block=new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block,0);
            block.SetColor("_EmissionColor",Color.white*.18f);
            renderer.SetPropertyBlock(block,1);
        }

        // The unchanged Overhaul preview selects skins[0], which is the
        // disabled Wolf renderer. Use Degu's active skinned submesh instead.
        private static bool DeguPreviewPrefix(GameObject preview, ZDO zdo,
            int chosenEye, bool showRunes)
        {
            if (!preview || !preview.name.StartsWith("Visual Degu",StringComparison.Ordinal))
                return true;
            if (zdo==null) return false;
            try
            {
                var renderer=Visible(preview);
                if (!renderer || !renderer.sharedMaterial) return false;
                int eye=chosenEye>=1 && chosenEye<=6?chosenEye:
                    Mathf.Clamp(zdo.GetInt(EyeKey,0),0,6);
                int element=showRunes && zdo.GetInt(LevelKey,1)>=25?
                    Mathf.Clamp(zdo.GetInt(ElementKey,0),0,5):0;
                applyProperties.Invoke(null,new object[]{renderer,renderer.sharedMaterial,eye,element});
                CopyEyeSubmesh(renderer);
            }
            catch (Exception e) { WarnOnce("Degu preview apply: "+e.GetBaseException().Message); }
            return false;
        }

        internal static int ChooseCoat() => UnityEngine.Random.Range(0,7);
        internal static int EnsureRandomEye(ZDO zdo, bool owner)
        {
            if (!Enabled || zdo==null) return 0;
            try { return (int)ensureEye.Invoke(null,new object[]{zdo,owner}); }
            catch (Exception e) { WarnOnce("Degu eye selection: "+e.GetBaseException().Message); return 0; }
        }
        internal static void Apply(GameObject creature,int index)
        {
            if (!Enabled || !creature || SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
                return;
            try { tryApply.Invoke(null,new object[]{creature,"Degu",index}); }
            catch (Exception e) { WarnOnce("Degu appearance: "+e.GetBaseException().Message); }
        }
        private static void WarnOnce(string error)
        {
            if (warned) return;
            warned=true;
            DeguPlugin.Log?.LogWarning(error);
        }
    }

    // One short-lived coroutine per network-spawned Degu, no Update loop.
    // The ZDO owner alone creates random coat and eye values.
    public sealed class DeguVariantAssignment : MonoBehaviour
    {
        private static readonly WaitForSeconds Delay=new WaitForSeconds(.2f);
        private IEnumerator Start()
        {
            if (!OverhaulCompatibility.Enabled) yield break;
            ZNetView view=GetComponent<ZNetView>();
            if (!view) yield break;
            for (int retry=0;retry<300;retry++)
            {
                if (!view || !gameObject) yield break;
                if (!view.IsValid()) { yield return Delay; continue; }
                ZDO zdo=view.GetZDO();
                if (zdo==null) { yield return Delay; continue; }
                if (view.IsOwner() && !zdo.GetBool(OverhaulCompatibility.ActiveKey,false))
                {
                    zdo.Set(OverhaulCompatibility.SkinKey,OverhaulCompatibility.ChooseCoat());
                    zdo.Set(OverhaulCompatibility.ActiveKey,true);
                }
                if (!zdo.GetBool(OverhaulCompatibility.ActiveKey,false))
                { yield return Delay; continue; }
                int eye=OverhaulCompatibility.EnsureRandomEye(zdo,view.IsOwner());
                if (eye==0) { yield return Delay; continue; }
                OverhaulCompatibility.Apply(gameObject,zdo.GetInt(OverhaulCompatibility.SkinKey,0));
                yield break;
            }
        }
    }
}
