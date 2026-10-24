#!/usr/bin/env python3
"""Build a native, reviewable Blender .blend from the exact new in-game skin.

Uses Valheim's 34 Wolf bone names/hierarchy, fitted Degu joints,
reweighted glTF UV-split geometry, original face labels and armature modifier.
Tests rest pose and four independent deformation pose samples IN BLENDER.
"""
import base64, collections, json, math, struct, sys, zlib
from pathlib import Path
import bpy
import numpy as np
from mathutils import Quaternion, Vector

ROOT=Path(__file__).resolve().parents[1]
sys.path.insert(0,str(ROOT/'tools'))
from rebuild_skin import JOINTS,NAMES
PARENTS=[-1,0,1,2,3,4,5,6,5,5,5,5,5,5,4,14,15,16,4,18,19,20,1,22,23,24,1,26,27,28,1,30,31,32]
assert len(PARENTS)==len(NAMES)==34
TIP={
    'Head':(0,.32,.39),'Jaw':(0,.267,.385),'Tongue':(0,.278,.40),
    'Eye L':(-.063,.356,.332),'Eye R':(.063,.356,.332),
    'L Ear':(-.125,.457,.225),'R Ear':(.125,.457,.225),
    'L Cheek':(-.086,.298,.37),'R Cheek':(.086,.298,.37),
    'L Hand':(-.09,.005,.31),'R Hand':(.09,.005,.31),
    'L Foot':(-.095,.005,-.10),'R Foot':(.095,.005,-.10),
    'Tail3':(0,.008,-.69)
}
def evaluated(mesh):
    dg=bpy.context.evaluated_depsgraph_get()
    obj=mesh.evaluated_get(dg)
    temporary=obj.to_mesh()
    try:return np.array([tuple(v.co) for v in temporary.vertices])
    finally:obj.to_mesh_clear()

def main():
    raw=(ROOT/'Assets'/'Degu.meshbin').read_bytes()
    n,ni,nb=struct.unpack_from('<HHB',raw,4)
    assert raw[:4]==b'SDG3' and (n,ni,nb)==(1477,7740,34)
    labels=np.frombuffer(zlib.decompress(base64.b64decode((ROOT/'Assets'/'DeguFaceSets.zlib.b64').read_text().strip())),dtype=np.uint8)
    verts=[];uvs=[];weights=[]
    for i in range(n):
        off=9+17*i
        vert=struct.unpack_from('<hhh',raw,off)
        uv=struct.unpack_from('<ee',raw,off+6)
        ids=struct.unpack_from('<BBBB',raw,off+10)
        wa,wb,wc=struct.unpack_from('<BBB',raw,off+14)
        ww=(wa,wb,wc,255-wa-wb-wc)
        assert min(ww)>=0 and sum(ww)==255
        verts.append(tuple(x/16384 for x in vert))
        uvs.append(uv)
        weights.append([(j,w/255) for j,w in zip(ids,ww) if w>0])
    inds=np.frombuffer(raw,dtype='<u2',offset=9+n*17,count=ni).reshape(-1,3)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    mesh=bpy.data.meshes.new('Degu_OriginalGeometry_UVSplits_1477')
    mesh.from_pydata(verts,[],inds.tolist())
    mesh.update()
    assert len(mesh.vertices)==1477 and len(mesh.polygons)==2580
    uv_layer=mesh.uv_layers.new(name='Wolf_Valheim_Original_Albedo_UV')
    for poly in mesh.polygons:
        for loop_index in poly.loop_indices:
            vertex=mesh.loops[loop_index].vertex_index
            uv_layer.data[loop_index].uv=uvs[vertex]
    # Pack the exact original in-game Wolf atlas only for Blender UV review.
    # No extra material is introduced into the runtime mod.
    atlas_path=ROOT/'Assets'/'Reference'/'Wolf_Original_Albedo.png'
    if atlas_path.exists():
        img=bpy.data.images.load(str(atlas_path), check_existing=True)
        img.name='Valheim Original Wolf Pixel1 - UV Reference'
        img.pack()
        ob_atlas=img.name
    else:
        ob_atlas='not available'
    fs=mesh.attributes.new('.sculpt_face_set','INT','FACE')
    for face in mesh.polygons:
        fs.data[face.index].value=int(collections.Counter(int(labels[v]) for v in face.vertices).most_common(1)[0][0])

    ob=bpy.data.objects.new('Degu_OriginalGeometry_NEW_WEIGHTS',mesh)
    bpy.context.collection.objects.link(ob)
    ob.rotation_euler[0]=math.pi/2
    # REVIEW ONLY: Blender's Principled preview uses the exact packed
    # Valheim Wolf atlas. This DOES NOT ship a new Unity/game material.
    if atlas_path.exists():
        preview=bpy.data.materials.new('PREVIEW_ONLY__Valheim_original_wolfMat')
        preview.use_nodes=True
        nodes=preview.node_tree.nodes
        shader=nodes.get('Principled BSDF')
        texnode=nodes.new('ShaderNodeTexImage')
        texnode.name='Wolf original albedo - user outlined UV'
        texnode.image=img
        preview.node_tree.links.new(texnode.outputs['Color'],shader.inputs['Base Color'])
        shader.inputs['Alpha'].default_value=1.0
        ob.data.materials.append(preview)

    arm=bpy.data.armatures.new('Wolf_34_Bones_DeguFitted')
    rig=bpy.data.objects.new('Valheim_Wolf_Hierarchy_DeguRest',arm)
    bpy.context.collection.objects.link(rig)
    rig.rotation_euler[0]=math.pi/2
    bpy.context.view_layer.objects.active=rig
    rig.select_set(True)
    bpy.ops.object.mode_set(mode='EDIT')
    mapping={}
    for i,name in enumerate(NAMES):
        eb=arm.edit_bones.new(name)
        eb.head=Vector(JOINTS[i])
        children=[j for j,parent in enumerate(PARENTS) if parent==i]
        forced={'Spine':'Spine1','Neck':'Head','Head':'Jaw'}
        if name in forced: tail=JOINTS[NAMES.index(forced[name])]
        elif children:tail=JOINTS[children[0]]
        else:tail=TIP.get(name)
        if tail is None:tail=(eb.head.x,eb.head.y+.04,eb.head.z)
        if (Vector(tail)-eb.head).length<.004:tail=eb.head+Vector((0,.04,0))
        eb.tail=Vector(tail)
        mapping[name]=eb
    for i,parent in enumerate(PARENTS):
        if parent!=-1:
            eb=mapping[NAMES[i]]
            eb.parent=mapping[NAMES[parent]]
            eb.use_connect=False
    bpy.ops.object.mode_set(mode='OBJECT')
    bpy.context.view_layer.objects.active=ob
    bpy.ops.object.select_all(action='DESELECT')
    ob.select_set(True)
    for name in NAMES:ob.vertex_groups.new(name=name)
    for i,row in enumerate(weights):
        for bone,weight in row:ob.vertex_groups[NAMES[bone]].add([i],weight,'REPLACE')
    mod=ob.modifiers.new('Wolf_Degu_ValidatedSkin','ARMATURE')
    mod.object=rig
    mod.use_deform_preserve_volume=False # Unity uses linear blend skinning by default
    bpy.context.view_layer.update()
    rest=evaluated(ob)
    orig=np.asarray(verts)
    rest_error=float(np.max(np.linalg.norm(rest-orig,axis=1)))
    assert rest_error<.0001,('REST_POSE_DRIFT',rest_error)
    moved={}
    for name in ('L Thigh','R Thigh','L UpperArm','R UpperArm','L Forearm','R Forearm','Head','Tail2'):
        pb=rig.pose.bones[name]
        pb.rotation_mode='QUATERNION'
        pb.rotation_quaternion=Quaternion(Vector((0,0,1)),math.radians(22))
        bpy.context.view_layer.update()
        change=np.linalg.norm(evaluated(ob)-rest,axis=1)
        moved[name]=int(np.sum(change>.0005))
        pb.rotation_quaternion=Quaternion((1,0,0,0))
        bpy.context.view_layer.update()
        assert moved[name]>5, (name,moved[name])
    for name in NAMES:assert rig.data.bones.get(name) is not None
    assert len(ob.vertex_groups)==34
    counts={name:0 for name in NAMES}
    for row in weights:
        for idx,val in row:
            if val>.005:counts[NAMES[idx]]+=1
    for name in ('L Thigh','R Thigh','L UpperArm','R UpperArm'):
        assert counts[name]>8,(name,counts[name])
    ob['geometry_source']='Degu.blend: 1325 vertices / UV split to 1477 for runtime'
    ob['albedo_atlas_reference']=ob_atlas
    ob['FaceSets']='Original per-vertex labels transferred to Blender faces'
    ob['skin']='REBUILT FROM FACE SETS, max 4 weights, normalized'
    rig['animation_source']='Valheim vanilla Wolf rig; game controls an independent fitted Degu skeleton'
    rig.show_in_front=True
    arm.display_type='STICK'
    output=ROOT/'release'/'Degu_Wolf_Reweighted_v0.1.8.blend'
    output.parent.mkdir(parents=True,exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(output),check_existing=False)
    report={'status':'PASS','blender':'NATIVE Blender save + evaluated modifier pose tests',
            'file':output.name,'bones':len(arm.bones),'vertex_groups':len(ob.vertex_groups),
            'vertices':len(mesh.vertices),'faces':len(mesh.polygons),
            'rest_pose_error':rest_error,'deformed_vertices':moved,'group_counts':counts}
    (ROOT/'release'/'Degu_Blender_QA.json').write_text(json.dumps(report,indent=2)+'\n')
    print('DEGU_BLENDER_QA_PASS '+json.dumps(report))
if __name__=='__main__':main()
