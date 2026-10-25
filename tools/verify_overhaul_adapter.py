from pathlib import Path
import re
bridge=Path("src/OverhaulCompatibility.cs").read_text()
plugin=Path("src/DeguPlugin.cs").read_text()
world=Path("src/DeguWorld.cs").read_text()
assert 'SupportedVersion = "0.22.65"' in bridge
assert 'Chainloader.PluginInfos.TryGetValue' in bridge
assert 'patches.UnpatchSelf()' in bridge
for entry in ("ChooseEyeColor","ToggleElementRunes","Capture","SyncFamiliar","HandleLifecycleRequest","UpdateDetail","Show","ApplyPreview"):
    assert '"'+entry+'"' in bridge,entry
for entry in ("IsSupported","Count","DisplayName","PreviewTexture","RandomIndex","TryApply"):
    assert '"'+entry+'"' in bridge,entry
assert 'DeguRenderer = "SirrohDegu_StableVisual"' in bridge
assert 'renderer,0,material,true' in bridge and 'renderer,1,material,true' in bridge
assert 'new object[]{animalObject,renderer,material}' in bridge
assert 'zdo.GetInt(LevelKey,1)>=25' in bridge
assert 'view.IsOwner()' in bridge and 'SkinKey' in bridge and 'EyeKey' in bridge
assert 'OverhaulCompatibility.AfterDeguRegistered' in plugin
assert 'OverhaulCompatibility.Install()' in plugin
assert 'PlainsMonsters' in world
assert 'SetIfPresent(ai, "m_huntPlayer", false)' in world
assert 'SetIfPresent(ai, "m_attackPlayerObjects", false)' in world
assert 'if (c != null && !c.IsTamed()) __result = null;' in world
for property,value in (("m_biome",'"Plains"'),("m_groupSizeMin","3"),("m_groupSizeMax","7"),("m_spawnAtDay","true"),("m_spawnAtNight","true")):
    assert re.search(r'Put\(spawn,"'+property+r'",'+value+r'\)',world),property
assert 'source != null && source.name == "Degu"' in world
print("PASS Degu compatibility source contract and Plains faction/spawn configuration")

# Regression for Valheim 1.0: Localization.AddWord calls SetupLanguage;
# the SetupLanguage postfix must NOT call AddWord recursively.
localize=world[world.index("internal static void Localize()"):world.index("private static object Field(")]
assert 'if (_localizationRegistrationActive) return;' in localize
assert '_localizationRegistrationActive=true;' in localize
assert 'try { RegisterLocalizationWord(); }' in localize
assert 'finally { _localizationRegistrationActive=false; }' in localize
assert 'add.Invoke(loc,new object[]{NameKey' in localize
assert 'harmony.Patch(setup,postfix:new HarmonyMethod(after));' in localize
assert localize.index('if (_localizationRegistrationActive) return;') < localize.index('RegisterLocalizationWord();')
print('PASS Localization.AddWord -> SetupLanguage recursion guarded')
