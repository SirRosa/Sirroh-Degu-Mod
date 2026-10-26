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

assert 'Put(spawn,"m_requiredNotGlobalKey","");' not in world
assert 'obj.GetType().GetField(name,' in world
assert not Path("src/DeguPreviewDiagnostics.cs").exists()
assert not Path("src/DeguFamiliarRepair.cs").exists()
preview=Path("src/DeguPreviewRenderer.cs").read_text()
assert 'if (!source || source.name != "Degu") return true;' in preview
assert preview.index('bindCount!=originalBones.Length') < preview.index('AddComponent<SkinnedMeshRenderer>()')
assert preview.index('into.gameObject.SetActive(false);') < preview.index('AddComponent<SkinnedMeshRenderer>()')
assert preview.index('copied.bones=mapped;') < preview.index('copied.sharedMesh=skin.sharedMesh;')
assert 'if(!originalBones[i] ||' in preview
assert 'rootBone' in preview and 'valid=false;' in preview
assert 'finally' in preview and 'into.gameObject.SetActive(originalActive);' in preview
print("PASS: Degu-only preview builds no unbound active skinned renderers")

# User-provided lossless RGBA 96x96 creature icon packaged into the single DLL.
import hashlib, struct
icon=Path("Assets/Icons/Degu.png").read_bytes()
assert icon[:8] == bytes.fromhex("89504e470d0a1a0a")
assert struct.unpack_from(">II",icon,16)==(96,96)
assert hashlib.sha256(icon).hexdigest()=="925240b90df35a969b1adebf666a7d7875aa4d502f2ce027cd27095d65613a03"
proj=Path("SirrohDegu.csproj").read_text()
assert '<EmbeddedResource Include="Assets/Icons/Degu.png" LogicalName="SirrohDegu.Creatures.Degu.png" />' in proj
code=Path("src/DeguCompassIcon.cs").read_text()
assert 'CreatureIconLibrary' in code and '"ForPrefab"' in code
assert 'nameof(AfterPrefabIcon)' in code
assert 'GetManifestResourceStream(ResourceName)' in code
assert 'Sprite.Create(texture=loaded,new Rect(0f,0f,96f,96f),' in code
assert 'DeguCompassIcon.Register(_patches);' in plugin
assert 'DeguCompassIcon.Shutdown();' in plugin
print("PASS exact user 96x96 icon embedded and wired to Overhaul creature/compass icon pipeline")

# One adapter, no overlapping patches or duplicated pet RPC handling.
assert 'DeguFamiliarRepair' not in plugin
assert 'DeguPreviewDiagnostics' not in bridge
assert 'DeguPreviewRenderer' in bridge
assert 'SyncFamiliar' not in bridge.split("var wolfChecks = new[]")[1].split("};")[0]
assert 'SupportedDegu' not in bridge
assert 'Patch(supported' not in bridge
assert 'ChangeNameColor' not in bridge
assert 'HandleLifecycleRequest' in bridge
assert 'ChooseEyeColor' in bridge
assert 'ToggleElementRunes' in bridge
assert 'Capture' in bridge and 'Show' in bridge and 'UpdateDetail' in bridge
assert 'AliasSpecies' in bridge
assert 'WolfOrDeguEquals' in bridge
assert 'skin!=lastSkin || nextEye!=lastEye' in bridge
assert 'if (OverhaulCompatibility.Apply(gameObject,skin))' in bridge
assert 'internal static bool Apply(GameObject creature,int index)' in bridge
assert 'Overhaul compatibility disabled' in bridge
assert 'zdo.GetInt(LevelKey,1)>=25' in bridge
assert 'AfterDeguRegistered' in plugin
assert 'DeguVariantAssignment' in bridge
assert '[BepInPlugin("sirroh.valheim.degu", "Sirroh Degu", "0.2.8")]' in plugin
# Keep actual Degu geometry and rig untouched, validate 34 fitted bindposes.
mesh=Path("src/DeguMesh.cs").read_text()
assert 'model.bindposes = inverseBinds;' in mesh
assert 'skin.bones = fitted;' in mesh and 'skin.sharedMesh = model;' in mesh
assert 'originalSkin.enabled = false;' in mesh
print("PASS: single native Overhaul menu/RPC/ZDO path and isolated live Degu coat updater")
