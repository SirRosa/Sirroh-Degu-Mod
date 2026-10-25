using System;
using System.Collections;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace SirrohDegu
{
    // SpawnSystem is the vanilla _ZoneCtrl spawner. No custom spawn loop, no
    // spawning on clients and no per-frame scans. Apply once to each zone list.
    internal static class DeguWorld
    {
        internal const string NameKey = "creature_sirroh_degu";

        internal static void Configure(GameObject prefab)
        {
            Character character = prefab.GetComponent<Character>();
            if (character == null) throw new InvalidOperationException("Degu Character missing.");
            character.m_name = "$" + NameKey;
            FieldInfo faction = AccessTools.Field(typeof(Character), "m_faction");
            if (faction == null) throw new MissingFieldException("Character.m_faction");
            // This is Valheim's native faction of Fuling/Deathsquito/etc.
            faction.SetValue(character, Enum.Parse(faction.FieldType, "PlainsMonsters"));

            MonsterAI ai = prefab.GetComponent<MonsterAI>();
            if (ai == null) throw new InvalidOperationException("Degu MonsterAI missing.");
            SetIfPresent(ai, "m_huntPlayer", false);
            SetIfPresent(ai, "m_attackPlayerObjects", false);
            // Preserve vanilla movement, feeding, taming and animation.
            DeguPlugin.Log?.LogInfo("Degu: PlainsMonsters faction, passive target selection and localized name.");
        }

        private static void SetIfPresent(object obj, string field, object value)
        {
            FieldInfo f = AccessTools.Field(obj.GetType(), field);
            if (f != null) f.SetValue(obj, value);
        }

        // Native Localization has moved between managed assemblies across
        // Valheim builds. Resolve by name at runtime, so this DLL does not
        // require a compile-time reference to a particular localization DLL.
        private static Type LocateLocalization()
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type=assembly.GetType("Localization",false);
                if (type != null) return type;
            }
            return null;
        }
        // Localization.AddWord invokes Localization.SetupLanguage in Valheim 1.0.
        // Our SetupLanguage postfix therefore re-enters Localize synchronously.
        // The gate MUST be raised before AddWord and reset even on exceptions,
        // otherwise one Degu word reloads all game localization files forever.
        private static bool _localizationRegistrationActive;
        internal static void Localize()
        {
            if (_localizationRegistrationActive) return;
            _localizationRegistrationActive=true;
            try { RegisterLocalizationWord(); }
            finally { _localizationRegistrationActive=false; }
        }

        private static void RegisterLocalizationWord()
        {
            Type type=LocateLocalization();
            if (type == null) return;
            object loc=AccessTools.Property(type,"instance")?.GetValue(null,null)
                    ?? AccessTools.Field(type,"instance")?.GetValue(null);
            if (loc == null) return;
            string language="";
            MethodInfo selected=AccessTools.Method(type,"GetSelectedLanguage");
            if (selected != null)
                try { language=Convert.ToString(selected.Invoke(loc,null)); }
                catch (Exception ex) { DeguPlugin.Log?.LogWarning("Degu language lookup: "+ex.Message); }
            bool polish=string.Equals(language,"Polish",StringComparison.OrdinalIgnoreCase)
                     || string.Equals(language,"Polski",StringComparison.OrdinalIgnoreCase);
            MethodInfo add=AccessTools.Method(type,"AddWord",new[]{typeof(string),typeof(string)});
            if (add == null)
            {
                DeguPlugin.Log?.LogWarning("Degu localization API AddWord unavailable.");
                return;
            }
            add.Invoke(loc,new object[]{NameKey,polish?"Koszatniczka":"Degu"});
        }

        internal static void InstallLocalizationHook(Harmony harmony)
        {
            Type type=LocateLocalization();
            if (type == null) return;
            MethodInfo setup=AccessTools.Method(type,"SetupLanguage");
            MethodInfo after=AccessTools.Method(typeof(DeguWorld),nameof(AfterLanguage));
            if (setup!=null && after!=null)
                harmony.Patch(setup,postfix:new HarmonyMethod(after));
            else
                DeguPlugin.Log?.LogWarning("Localization.SetupLanguage unavailable; using registration localization.");
        }
        private static void AfterLanguage() { Localize(); }

        private static object Field(object obj, string name)
        {
            return obj == null ? null : AccessTools.Field(obj.GetType(), name)?.GetValue(obj);
        }
        private static void Put(object obj, string name, object value)
        {
            FieldInfo field = obj.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field == null) return;
            if (field.FieldType.IsEnum && value is string text)
                value = Enum.Parse(field.FieldType, text);
            field.SetValue(obj, value);
        }

        // Instance-local initialization on vanilla spawner's periodic pass.
        // The Degu prefab is registered AFTER ZNetScene.Awake, so this hook
        // safely retries only until it is available.
        [HarmonyPatch(typeof(SpawnSystem), "UpdateSpawning")]
        private static class SpawnSystemPatches
        {
            [HarmonyPrefix]
            private static void Before(SpawnSystem __instance)
            {
                if (__instance == null || ZNetScene.instance == null) return;
                try { EnsureSpawn(__instance); }
                catch (Exception e) { WarnOnce("Degu Plains spawn rule: " + e); }
            }
        }

        private static bool _warned;
        private static void WarnOnce(string message)
        {
            if (_warned) return;
            _warned = true;
            DeguPlugin.Log?.LogWarning(message);
        }

        internal static void EnsureSpawn(SpawnSystem system)
        {
            GameObject degu = ZNetScene.instance.GetPrefab("Degu");
            if (degu == null) return;
            IEnumerable lists = Field(system, "m_spawnLists") as IEnumerable;
            if (lists == null)
            {
                WarnOnce("SpawnSystem.m_spawnLists not available; Plains spawning disabled.");
                return;
            }
            foreach (object spawnList in lists)
            {
                IList spawners = Field(spawnList, "m_spawners") as IList;
                if (spawners == null) continue;
                object baseline = null;
                foreach (object entry in spawners)
                {
                    if (entry == null) continue;
                    GameObject source = Field(entry, "m_prefab") as GameObject;
                    if (source != null && source.name == "Degu") return; // idempotent
                    object biome = Field(entry, "m_biome");
                    if (biome == null || !string.Equals(biome.ToString(),"Plains",StringComparison.OrdinalIgnoreCase))
                        continue;
                    if (source != null && source.name == "Lox") baseline = entry;
                    else if (baseline == null) baseline = entry;
                }
                if (baseline == null) continue;
                MethodInfo cloneMethod = typeof(object).GetMethod("MemberwiseClone",
                    BindingFlags.Instance|BindingFlags.NonPublic);
                object spawn = cloneMethod.Invoke(baseline, null);
                Put(spawn,"m_name","SirrohDegu_Plains_3to7");
                Put(spawn,"m_prefab",degu);
                Put(spawn,"m_biome","Plains");
                Put(spawn,"m_groupSizeMin",3);
                Put(spawn,"m_groupSizeMax",7);
                Put(spawn,"m_groupRadius",8f);
                Put(spawn,"m_maxSpawned",21);
                Put(spawn,"m_spawnInterval",250f);
                Put(spawn,"m_spawnChance",35f);
                Put(spawn,"m_spawnAtDay",true);
                Put(spawn,"m_spawnAtNight",true);
                Put(spawn,"m_huntPlayer",false);
                Put(spawn,"m_requiredGlobalKey","");
                spawners.Add(spawn);
                DeguPlugin.Log?.LogInfo("Degu Plains natural spawn registered: group 3-7, max 21, 250s, 35%.");
                return;
            }
            WarnOnce("Could not find a native Plains spawn template on ZoneCtrl.");
        }

        // If the wolf AI attempts to scan for enemies, a wild Degu never
        // acquires a hostile target. Tamed ones retain normal companion AI.
        [HarmonyPatch(typeof(BaseAI), "FindEnemy")]
        private static class DeguNeverHunt
        {
            [HarmonyPostfix]
            private static void After(BaseAI __instance, ref Character __result)
            {
                if (__instance == null || __instance.gameObject == null) return;
                if (!__instance.gameObject.name.StartsWith("Degu",StringComparison.Ordinal)) return;
                Character c = __instance.GetComponent<Character>();
                if (c != null && !c.IsTamed()) __result = null;
            }
        }
    }
}
