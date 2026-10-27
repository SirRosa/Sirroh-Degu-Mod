"""Contracts for completely rebuilt Degu 0.4.0 modules.
This verifies source isolation and prevents static regressions only.
It DOES NOT test the client Valheim game runtime.
"""
from pathlib import Path
import subprocess, hashlib, json, re

def blob(path):
    return subprocess.check_output(["git","hash-object",path],text=True).strip()

frozen={
 "src/DeguWorld.cs":"c8c5294b65bec39d88ad5cabd2068f1d3cd886e9",
 "src/DeguRigDriver.cs":"72cb05dfde8d02b2ff4e3e523cdc7b82ba0232c5",
 "src/DeguEyeEmission.cs":"f0bf96db0577dcb01d2b9246fcabf5330b791fee",
 "Assets/Degu.part1.b64":"3643a4de782a1b9c5fc1ac95dc586d5caf7c3aef",
 "Assets/Degu.part2.b64":"dddd2ba9a064c5ef8f57a66fa87a956f8477d869",
 "Assets/Degu.part3.b64":"b0f39cdcf191621d6df4211fa4ae004945ba599b"
}
for path,expected in frozen.items():
    assert blob(path)==expected,"Changed gameplay/asset file: "+path
assert blob("src/DeguMesh.cs")=="ae30815d9666c561faa67fd159e7a25cbe9662a3"
print("PASS original model, weights, rig, user textures, AI and spawn sources unchanged")

csproj=Path("SirrohDegu.csproj").read_text()
legacy=[
 "DeguAppearanceService.cs","DeguFamiliarRepair.cs",
 "DeguPreviewDiagnostics.cs","DeguBindposeTrace.cs","OverhaulCompatibility.cs"
]
for old in legacy:
    assert '<Compile Remove="src/'+old+'" />' in csproj,old
plugin=Path("src/DeguPlugin.cs").read_text()
for old in ("DeguAppearanceService","DeguFamiliarRepair","DeguPreviewDiagnostics",
            "DeguBindposeTrace","OverhaulCompatibility","DeguSpawnDiagnostic"):
    assert old not in plugin,old
for new in ("DeguWolfPalette.Register(existing);","DeguWolfPalette.Register(_deguPrefab);",
            "DeguFamiliarBridge.Install();","DeguPreviewBridge.Install();",
            "DeguFamiliarBridge.Shutdown();","DeguPreviewBridge.Shutdown();",
            "DeguWolfPalette.Shutdown();"):
    assert new in plugin,new
print("PASS old adapters impossible to compile or run; independent module bootstrapping")

wolf=Path("src/DeguWolfPalette.cs").read_text()
ui=Path("src/DeguFamiliarBridge.cs").read_text()
preview=Path("src/DeguPreviewBridge.cs").read_text()
for name in ['"Default","Albino","Black","DarkBrown","Grey","Blood","Midnight"',
             'ImportNativeCatalog(Assembly assembly)',
             'ImportLoadedAssetBundles()',
             'AssetBundle.GetAllLoadedAssetBundles()',
             'new Material(source)',
             'ApplyProperties',
             'material.mainTexture',
             'UnityEngine.Random.Range(0,7)',
             'UnityEngine.Random.Range(1,7)',
             'zdo.Set(DeguWolfPalette.SkinKey',
             'zdo.Set(DeguWolfPalette.EyeKey',
             'zdo.GetBool(DeguWolfPalette.RunesKey,true) && level>=25',
             'skin.bones.Length==skin.sharedMesh.bindposes.Length']:
    assert name in wolf,name
assert "version.Major!=" not in wolf # no hard-coded BepInEx version gate
assert "RequiredVersion" not in wolf
assert "material.shader=" not in wolf
assert 'Assembly.LoadFrom' not in wolf
assert wolf.count('DeguPlugin.Log?.LogWarning')<=1
print("PASS version-independent native Wolf fur/eye/rune bridge, exact 7/6, ZDO, bounded logging")

for p in ('SkinCount','SkinName','ChooseSkin','ChooseEyes','ChangeName','ToggleRunes',
          'ServerOperation','CaptureDone','BeforeLifecycleTick','AfterAdoption',
          'BeforeRefresh','AfterDetail'):
    assert 'nameof('+p+')' in ui,p
for op,key in (('10','SkinKey'),('18','EyeKey'),('17','RunesKey')):
    assert 'Request(info,'+op+',' in ui or 'Request(familiar,'+op+',' in ui
    assert 'zdo.Set(DeguWolfPalette.'+key in ui
assert 'roster.Contains(target)' in ui
assert 'zdo.GetLong(Owner,0L)!=userId' in ui
assert 'pendingField?.GetValue(null)' in ui
assert 'list.Count>=limit' in ui
assert 'list.Add(zdo.m_uid)' in ui
assert 'save.Invoke(null,new object[]{p})' in ui
assert 'Patch(taming,"OnAnswer",nameof(AfterAdoption),false)' in ui
assert 'Patch(taming,"OnStatus"' not in ui
print("PASS independent Tresura action/UI/ownership/roster recovery")

for p in ('nameof(Copy)','nameof(AfterShow)','source.name!="Degu"',
          'skin.bones=bones;skin.rootBone=root;',
          'skin.sharedMesh=sourceSkin.sharedMesh',
          'DeguWolfPalette.Apply(visual,'):
    assert p in preview,p
assert preview.index('skin.bones=bones;skin.rootBone=root;') < preview.index('skin.sharedMesh=sourceSkin.sharedMesh')
assert 'AddComponent<Character>' not in preview
assert 'AddComponent<ZNetView>' not in preview
print("PASS renderer-only complete-bone 3D preview")

world=Path("src/DeguWorld.cs").read_text()
for token in ('PlainsMonsters','SetIfPresent(ai, "m_huntPlayer", false)',
              'SetIfPresent(ai, "m_attackPlayerObjects", false)',
              'Put(spawn,"m_groupSizeMin",3)',
              'Put(spawn,"m_groupSizeMax",7)',
              'Put(spawn,"m_spawnInterval",250f)',
              'Put(spawn,"m_spawnChance",35f)'):
    assert token in world,token
assert json.loads(Path("manifest.json").read_text())["version_number"]=="0.4.0"
assert '"0.4.0"' in plugin
assert "0.4.0" in Path("update.md").read_text()
icon=Path("Assets/Icons/Degu.png").read_bytes()
assert hashlib.sha256(icon).hexdigest()=="925240b90df35a969b1adebf666a7d7875aa4d502f2ce027cd27095d65613a03"
print("PASS neutral AI, Plains 3–7 spawn, icon integrity, version ledger")
