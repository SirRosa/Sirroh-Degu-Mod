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

        private static Harmony patches, uiPatches;
        private static FieldInfo variantsField, mainField;
        private static Type inheritedSkinAssignment;
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

        // Split the authoritative wild appearance adapter from optional menu
        // Harmony integrations. A failed menu transpiler must NEVER turn off
        // wild Degu fur/eyes or prevent DeguVariantAssignment registration.
        internal static void Install()
        {
            if (!Chainloader.PluginInfos.TryGetValue(PluginId, out var plugin))
            {
                DeguPlugin.Log?.LogWarning("Degu appearance: Overhaul plugin not loaded.");
                return;
            }
            if (plugin.Metadata.Version.ToString()!=SupportedVersion)
            {
                DeguPlugin.Log?.LogWarning("Degu appearance disabled: exact Overhaul "+
                    SupportedVersion+" required, found "+plugin.Metadata.Version);
                return;
            }
            Assembly assembly=plugin.Instance.GetType().Assembly;
            try
            {
                Type catalog=Required("MaterialCatalog",assembly);
                Type visuals=Required("WolfVisuals",assembly);
                inheritedSkinAssignment=Required("SkinAssignment",assembly);
                Type variant=Required("Variant",assembly);
                variantsField=AccessTools.Field(catalog,"Variants");
                mainField=AccessTools.Field(variant,"Main");
                if(variantsField==null || mainField==null)
                    throw new MissingMemberException("MaterialCatalog.Variants / Variant.Main");
                assign=RequiredMethod(catalog,"Assign");
                wolfApply=RequiredMethod(visuals,"Apply");
                applyProperties=RequiredMethod(visuals,"ApplyProperties");
                ensureEye=RequiredMethod(visuals,"EnsureEye");
                tryApply=RequiredMethod(catalog,"TryApply");
                var count=RequiredMethod(catalog,"Count");
                var display=RequiredMethod(catalog,"DisplayName");
                var texture=RequiredMethod(catalog,"PreviewTexture");
                var random=RequiredMethod(catalog,"RandomIndex");
                var supported=RequiredMethod(catalog,"IsSupported");
                var valid=RequiredMethod(catalog,"IsValid");

                // Core lifecycle: no dependence on fragile UI string-compare IL.
                patches=new Harmony("sirroh.degu.appearance.v030");
                foreach(MethodInfo target in new[]{count,display,texture,valid})
                    patches.Patch(target,prefix:new HarmonyMethod(
                        AccessTools.Method(typeof(OverhaulCompatibility),nameof(AliasSpecies))));
                patches.Patch(random,prefix:new HarmonyMethod(
                    AccessTools.Method(typeof(OverhaulCompatibility),nameof(AliasAnimal))));
                patches.Patch(supported,postfix:new HarmonyMethod(
                    AccessTools.Method(typeof(OverhaulCompatibility),nameof(SupportedDegu))));
                patches.Patch(tryApply,prefix:new HarmonyMethod(
                    AccessTools.Method(typeof(OverhaulCompatibility),nameof(ApplyDeguPrefix))));
                Enabled=true;
                DeguPlugin.Log?.LogInfo("Degu appearance core ACTIVE: 7 wild coats / 6 eyes / ZDO; UI errors cannot disable it.");
            }
            catch(Exception e)
            {
                Enabled=false;
                patches?.UnpatchSelf();
                patches=null;
                DeguPlugin.Log?.LogError("Degu appearance core FAILED: "+e);
                return;
            }
            try
            {
                Type visuals=Required("WolfVisuals",assembly);
                Type taming=Required("TamingSkill",assembly);
                Type panel=Required("FamiliarMenuPanel",assembly);
                Type preview=Required("FamiliarModelPreview",assembly);
                var checks=new[]{
                    RequiredMethod(taming,"ChooseEyeColor"),
                    RequiredMethod(taming,"ToggleElementRunes"),
                    RequiredMethod(taming,"Capture"),
                    RequiredMethod(taming,"SyncFamiliar"),
                    RequiredMethod(taming,"HandleLifecycleRequest"),
                    RequiredMethod(panel,"UpdateDetail"),
                    RequiredMethod(preview,"Show")
                };
                var applyPreview=RequiredMethod(visuals,"ApplyPreview");
                var copy=RequiredMethod(preview,"CopyRenderers");
                uiPatches=new Harmony("sirroh.degu.menu.preview.v030");
                foreach(MethodInfo target in checks)
                    uiPatches.Patch(target,transpiler:new HarmonyMethod(
                        AccessTools.Method(typeof(OverhaulCompatibility),nameof(AllowDeguChecks))));
                uiPatches.Patch(applyPreview,prefix:new HarmonyMethod(
                    AccessTools.Method(typeof(OverhaulCompatibility),nameof(DeguPreviewPrefix))));
                uiPatches.Patch(copy,prefix:new HarmonyMethod(
                    AccessTools.Method(typeof(DeguPreviewDiagnostics),"CopyRenderers")));
                uiPatches.Patch(RequiredMethod(preview,"Show"),
                    postfix:new HarmonyMethod(AccessTools.Method(typeof(OverhaulCompatibility),nameof(AfterDeguPreviewShow))));
                uiPatches.Patch(RequiredMethod(panel,"UpdateDetail"),
                    prefix:new HarmonyMethod(AccessTools.Method(typeof(DeguPreviewDiagnostics),"BeforeDetail")),
                    postfix:new HarmonyMethod(AccessTools.Method(typeof(DeguPreviewDiagnostics),"AfterDetail")));
                DeguPlugin.Log?.LogInfo("Degu Tresura + 3D preview hooks ACTIVE.");
            }
            catch(Exception e)
            {
                uiPatches?.UnpatchSelf();
                uiPatches=null;
                DeguPlugin.Log?.LogError(
                    "Degu optional Tresura/preview hooks FAILED (wild cosmetics still active): "+e);
            }
        }

        internal static void Shutdown()
        {
            Enabled=false;
            uiPatches?.UnpatchSelf();
            uiPatches=null;
            patches?.UnpatchSelf();
            patches=null;
        }

        internal static void AfterDeguRegistered(GameObject prefab)
        {
            if (!Enabled || !prefab || prefab.GetComponent<DeguVariantAssignment>()) return;
            // Wolf was patched by Overhaul before Degu cloned it. Disable ONLY
            // the inherited coroutine on Degu; otherwise two randomizers race.
            // Its original Wolf prefab/component is never touched.
            if (inheritedSkinAssignment != null)
            {
                Behaviour duplicate = prefab.GetComponent(inheritedSkinAssignment) as Behaviour;
                if (duplicate != null) duplicate.enabled = false;
            }
            prefab.AddComponent<DeguVariantAssignment>();
            DeguPlugin.Log?.LogInfo("Degu owns one ZDO-authoritative coat/eye assignment.");
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
            MethodInfo oursEqual = AccessTools.Method(typeof(OverhaulCompatibility),nameof(WolfOrDeguEquals));
            MethodInfo oursNotEqual = AccessTools.Method(typeof(OverhaulCompatibility),nameof(WolfOrDeguNotEquals));
            int replaced=0;
            for (int i=0;i+1<codes.Count;i++)
            {
                if (codes[i].opcode != OpCodes.Ldstr || !Equals(codes[i].operand,"Wolf")) continue;
                // C# emits ldstr "Wolf"; call string.op_Equality/Inequality.
                for (int j=i+1;j<=Math.Min(i+2,codes.Count-1);j++)
                {
                    if (codes[j].operand is MethodInfo method && method.DeclaringType == typeof(string) && method.Name == "op_Equality")
                    { codes[j].operand=oursEqual; replaced++; break; }
                    if (codes[j].operand is MethodInfo other && other.DeclaringType == typeof(string) && other.Name == "op_Inequality")
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
            // WolfVisuals writes eye-specific albedo, colored emission and
            // elemental mask to submesh 0. Degu eyes use submesh 1: copy the
            // complete property block instead of overriding just brightness.
            var block=new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block,0);
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

        // Read-only, Degu-only preview completion for both loaded and unloaded
        // familiars. Never changes world creatures, ZDO or shared materials.
        private static void AfterDeguPreviewShow(object __instance,
            string prefab, ZDOID petId, int eyeColor, bool showRunes)
        {
            if (!Enabled || !Degu(prefab) || __instance==null) return;
            try
            {
                Type type=__instance.GetType();
                var visual=type.GetField("_visual",BindingFlags.Instance|BindingFlags.NonPublic)
                    ?.GetValue(__instance) as GameObject;
                var renderer=Visible(visual);
                if (!renderer || !renderer.sharedMaterial) return;
                ZDO zdo=ZDOMan.instance?.GetZDO(petId);
                int chosenEye=eyeColor>=1 && eyeColor<=6?eyeColor:
                    zdo==null?0:Mathf.Clamp(zdo.GetInt(EyeKey,0),0,6);
                int level=zdo==null?1:zdo.GetInt(LevelKey,1);
                int element=zdo!=null && showRunes && level>=25?
                    Mathf.Clamp(zdo.GetInt(ElementKey,0),0,5):0;
                applyProperties.Invoke(null,new object[]{
                    renderer,renderer.sharedMaterial,chosenEye,element
                });
                CopyEyeSubmesh(renderer);
                // Show() already rendered the RT; update it with the current
                // eye/emission overrides without instantiating a Character.
                type.GetMethod("Render",BindingFlags.Instance|BindingFlags.NonPublic)
                    ?.Invoke(__instance,null);
            }
            catch(Exception e)
            {
                WarnOnce("Degu preview final appearance: "+e.GetBaseException().Message);
            }
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
                // Wild Degu get one random appearance, while owned pets must
                // also react to skin/eye/rune ZDO changes after menu selection.
                // Check once per second, apply only if appearance changed.
                int lastSkin=zdo.GetInt(OverhaulCompatibility.SkinKey,0);
                int lastEye=zdo.GetInt(OverhaulCompatibility.EyeKey,0);
                int lastElement=zdo.GetInt("sirroh.pet.element.v1",0);
                int lastLevel=zdo.GetInt("sirroh.pet.level.v1",1);
                bool lastRunes=zdo.GetBool("sirroh.wolf.showrunes.v1",true);
                var slow=new WaitForSeconds(1f);
                while (view && view.IsValid() && gameObject)
                {
                    yield return slow;
                    ZDO current=view.GetZDO();
                    if(current==null)yield break;
                    int skin=current.GetInt(OverhaulCompatibility.SkinKey,0);
                    int nextEye=current.GetInt(OverhaulCompatibility.EyeKey,0);
                    int element=current.GetInt("sirroh.pet.element.v1",0);
                    int level=current.GetInt("sirroh.pet.level.v1",1);
                    bool runes=current.GetBool("sirroh.wolf.showrunes.v1",true);
                    if(skin!=lastSkin || nextEye!=lastEye || element!=lastElement ||
                       level!=lastLevel || runes!=lastRunes)
                    {
                        OverhaulCompatibility.Apply(gameObject,skin);
                        lastSkin=skin;
                        lastEye=nextEye;
                        lastElement=element;
                        lastLevel=level;
                        lastRunes=runes;
                    }
                }
                yield break;
            }
        }
    }
}
