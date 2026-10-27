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
preview=Path("src/DeguPreviewDiagnostics.cs").read_text()
assert preview.index('result.bones=bones;') < preview.index('result.sharedMesh=skin.sharedMesh;')
assert 'BeforeDetail' in preview and 'AfterDetail' in preview
assert 'zdo.GetLong("sirroh.tresura.owner.v1",0L)' in preview
print("PASS optional spawn field, Degu-only preview bindpose order and UI diagnostics")

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

# Full companion integration regression: server-owned ZDO, menu buttons, 4 opcodes.
repair=Path("src/DeguFamiliarRepair.cs").read_text()
for name in ("ChooseSkin","ChooseEyes","ChooseName","ToggleRunes","ServerRequest","AfterDetail","AfterCapture"):
    assert "private static " in repair and name+"(" in repair,name
for cmd in ("SendRequest(info,10,next)","SendRequest(info,18,next)","SendRequest(familiar,11,next)","SendRequest(info,17,previous?0:1)"):
    assert cmd in repair,cmd
for key in ("pet.Set(SkinKey,requested)","pet.Set(NameColorKey,requested)","pet.Set(EyeKey,requested)","pet.Set(RunesKey,requested==1)"):
    assert key in repair,key
assert "pet.GetLong(OwnerKey,0L)!=userId" in repair
assert "roster.Contains(target)" in repair
assert "pet.GetInt(PetLevelKey,1)<25" in repair
assert "ZDOMan.instance.ForceSendZDO(target)" in repair
assert "DeguFamiliarRepair.Install();" in plugin
assert "DeguFamiliarRepair.Shutdown();" in plugin
assert "new WaitForSeconds(1f)" in bridge
assert "skin!=lastSkin || nextEye!=lastEye" in bridge
print("PASS Degu controls/RPC validation, persistence and reactive live appearance")

# Runtime A/B audit must be observational: preserve baseline randomizer and world code.
audit=Path("src/DeguRuntimeAudit.cs").read_text()
for method in ("ReportPrefab(", "ReportSpawn(", "OnUnityLog(", "Fingerprint("):
    assert method in audit,method
assert "furRandomizerSetActive" in audit and "sirroh.wolf.eye.v1" in audit
assert "DeguRuntimeAudit.Install();" in plugin
assert "DeguRuntimeAudit.Shutdown();" in plugin
assert "DeguRuntimeAudit.ReportPrefab(" in plugin
assert "DeguRuntimeAudit.ReportSpawn(" in plugin
for forbidden in ("zdo.Set(", "pet.Set(", "renderer.sharedMesh =", "skin.sharedMesh =", "AddComponent<", "Random.Range(", ".Patch("):
    assert forbidden not in audit,"Audit must not write world state: "+forbidden
print("PASS read-only runtime A/B diagnostics; no RNG, ZDO writes, renderer changes, Harmony patches")
