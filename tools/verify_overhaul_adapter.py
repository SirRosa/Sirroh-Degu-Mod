"""Regression contracts for Degu 0.3.1 independent appearance pipeline.

Static checks cannot replace Valheim runtime, but catch accidental coupling
between wild randomization, Overhaul menu patches and prefab construction.
"""
from pathlib import Path
import hashlib
import re
import subprocess

def sha(path):
    return subprocess.check_output(["git", "hash-object", path], text=True).strip()

# Keep the user-verified 0.2.5 gameplay / model code byte-identical.
frozen = {
    "src/DeguWorld.cs": "c8c5294b65bec39d88ad5cabd2068f1d3cd886e9",
    # DeguMesh has one controlled lifecycle change, checked below; no geometry change.
    "src/DeguRigDriver.cs": "72cb05dfde8d02b2ff4e3e523cdc7b82ba0232c5",
    "src/DeguEyeEmission.cs": "f0bf96db0577dcb01d2b9246fcabf5330b791fee",
    "Assets/Degu.part1.b64": "3643a4de782a1b9c5fc1ac95dc586d5caf7c3aef",
    "Assets/Degu.part2.b64": "dddd2ba9a064c5ef8f57a66fa87a956f8477d869",
    "Assets/Degu.part3.b64": "b0f39cdcf191621d6df4211fa4ae004945ba599b",
}
for path, expected in frozen.items():
    assert sha(path) == expected, "Regression in protected v0.2.5 file: "+path
mesh=Path("src/DeguMesh.cs").read_text()
assert sha("src/DeguMesh.cs")=="ae30815d9666c561faa67fd159e7a25cbe9662a3"
assert 'skin.enabled = false;' in mesh
assert 'skin.bones = fitted;' in mesh
assert 'model.bindposes = inverseBinds;' in mesh
assert 'skin.sharedMesh = model;' in mesh
print("PASS original geometry, weights, rig, UV inputs; only deferred renderer activation changed")

world=Path("src/DeguWorld.cs").read_text()
for token in ('PlainsMonsters','SetIfPresent(ai, "m_huntPlayer", false)',
              'SetIfPresent(ai, "m_attackPlayerObjects", false)',
              'Put(spawn,"m_groupSizeMin",3)',
              'Put(spawn,"m_groupSizeMax",7)',
              'Put(spawn,"m_spawnInterval",250f)',
              'Put(spawn,"m_spawnChance",35f)'):
    assert token in world,token
print("PASS neutral AI and Plains group-spawn parameters")

plugin=Path("src/DeguPlugin.cs").read_text()
service=Path("src/DeguAppearanceService.cs").read_text()
menu=Path("src/DeguFamiliarRepair.cs").read_text()
preview=Path("src/DeguPreviewDiagnostics.cs").read_text()
assert 'DeguAppearanceService.Install();' in plugin
assert 'DeguAppearanceService.RegisterPrefab(existing);' in plugin
assert 'DeguAppearanceService.RegisterPrefab(_deguPrefab);' in plugin
assert 'OverhaulCompatibility.Install();' not in plugin
assert 'OverhaulCompatibility.AfterDeguRegistered' not in plugin
assert 'DeguPreviewDiagnostics.Install();' in plugin
assert 'DeguPreviewDiagnostics.Shutdown();' in plugin
assert 'DeguFamiliarRepair.Install();' in plugin
assert 'DeguFamiliarRepair.Shutdown();' in plugin
assert 'class DeguAppearanceState : MonoBehaviour' in service
assert 'prefab.AddComponent<DeguAppearanceState>()' in service
assert 'if(owner && !zdo.GetBool(DeguAppearanceService.ActiveKey,false))' in service
assert 'UnityEngine.Random.Range(0,7)' in service
assert 'UnityEngine.Random.Range(1,7)' in service
assert 'if(owner && zdo.GetInt(DeguAppearanceService.EyeKey,0)<=0)' in service
assert 'if(zdo.GetInt(DeguAppearanceService.SkinKey,-1)<0)' in service
assert 'bool ready=zdo.GetBool(DeguAppearanceService.ActiveKey,false)' in service
assert 'coat>=0 && coat<7 && eye>=1 && eye<=6' in service
assert 'if(DeguAppearanceService.ApplyVisual(gameObject,coat,eye,element))' in service
assert 'return Mathf.Clamp(zdo.GetInt(ElementKey,0),0,5)' in service
assert 'zdo.GetInt(LevelKey,1)<25' in service
assert '!zdo.GetBool(RunesKey,true)' in service
assert 'skin.name==VisualName' in service
for dangerous in ['new Material(', 'gameObject.AddComponent<Character>', 'new ZNetView(']:
    assert dangerous not in service,dangerous
print("PASS ZDO-authoritative wild appearance independent from menu installation")

for name in ('ChooseSkin','ChooseEyes','ChooseName','ToggleRunes','ServerRequest',
             'AfterDetail','AfterCapture'):
    assert 'private static ' in menu and name+'(' in menu,name
for command in ('if(!SendRequest(info,10,next))return false;',
                'if(!SendRequest(info,18,next))return false;',
                'if(!SendRequest(familiar,11,next))return false;',
                'if(!SendRequest(info,17,previous?0:1))return false;'):
    assert command in menu,command
for token in ('pet.Set(SkinKey,requested)', 'pet.Set(EyeKey,requested)',
              'pet.Set(RunesKey,requested==1)', 'pet.Set(NameColorKey,requested)',
              'pet.GetLong(OwnerKey,0L)!=userId','roster.Contains(target)',
              'pet.GetInt(PetLevelKey,1)<25'):
    assert token in menu,token
assert 'nameof(CopyRenderers)' in preview
assert 'nameof(AfterPreviewShow)' in preview
assert preview.index('result.bones=bones;') < preview.index('result.sharedMesh=skin.sharedMesh;')
assert 'DeguAppearanceService.ApplyVisual(visual,skin,eyeColor,appliedElement)' in preview
print("PASS separate native Tresura RPC and detached-preview adapters")

import struct
icon=Path("Assets/Icons/Degu.png").read_bytes()
assert icon[:8]==bytes.fromhex("89504e470d0a1a0a")
assert struct.unpack_from(">II",icon,16)==(96,96)
assert hashlib.sha256(icon).hexdigest()=="925240b90df35a969b1adebf666a7d7875aa4d502f2ce027cd27095d65613a03"
assert '<EmbeddedResource Include="Assets/Icons/Degu.png" LogicalName="SirrohDegu.Creatures.Degu.png" />' in Path("SirrohDegu.csproj").read_text()
print("PASS original Degu compass icon unchanged")

manifest=Path("manifest.json").read_text()
assert '"version_number": "0.3.1"' in manifest
assert '"0.3.1"' in plugin
print("PASS 0.3.1 source, manifest and reproducibility contracts")

trace=Path("src/DeguBindposeTrace.cs").read_text()
for token in ("OnUnityLog(", "BeforeSpawn(", "AfterSpawn(", "AfterPrefab(",
              "DEGU_BINDPOSE_EVENT", "STATUS=COUNT_OK_MATRIX_UNVERIFIED"):
    assert token in trace, token
for forbidden in ("zdo.Set(", "AddComponent<", "renderer.sharedMesh=", "sharedMesh ="):
    assert forbidden not in trace, "Bindpose trace must never modify the model: "+forbidden
for token in ("DeguBindposeTrace.Install();","DeguBindposeTrace.Shutdown();",
              "DeguBindposeTrace.BeforeSpawn(", "DeguBindposeTrace.AfterSpawn(",
              "DeguBindposeTrace.AfterPrefab("):
    assert token in plugin,token
print("PASS read-only Unity bindpose incident correlation without hiding errors")

assert 'visual.enabled=true;' in service
assert 'yield return null;' in service
assert 'visual.bones.Length!=visual.sharedMesh.bindposes.Length' in service
assert 'skin.name!="SirrohDegu_StableVisual"' in preview
print("PASS Degu renderer staged disabled and activated after all Unity Awake callbacks")
