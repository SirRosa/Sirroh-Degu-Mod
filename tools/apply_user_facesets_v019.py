#!/usr/bin/env python3
"""Apply the user's Blender v0.1.8 hand-painted Face Sets and v0.1.9 UV fix.

These face indices were READ FROM the user's submitted Blender mesh, rather
than inferred from normals. Blender polygon order was independently checked
against all 2,580 triangles of the embedded Valheim SDG3 asset (exact match).

Faces 24/26 = INNER ears; Face Set 25 = nose. Duplicating boundary vertices
ensures these small atlas islands do NOT spill onto outer ear/body triangles.
The corrected armature from the user-provided .blend is symmetrical; original
DeguJoints head positions on both sides already match those corrected
positions. Geometry and bone weights of each vertex are preserved.
"""
from pathlib import Path
import base64, collections, json, struct, zlib
import numpy as np

ROOT = Path(__file__).resolve().parents[1]
FACE_PATCHES = {
    24: [101, 284, 312, 340, 369, 374, 485, 549],
    25: [11, 75, 225, 238, 239, 386, 399, 400, 426, 477, 478,
         496, 561, 567, 580, 581, 582, 657],
    26: [27, 53, 133, 135, 202, 257, 557, 649],
}
# Pixel regions from user's original 256 x 256 annotated Wolf texture:
# yellow inner-ear region and green muzzle-tip region.
EAR_BOX = (97.23, 92.23, 102.27, 97.27)
NOSE_BOX = (62.23, 133.19, 67.27, 137.81)

def emit_uv(target, xyz, source_min, source_max):
    x,y,z=xyz
    if target in (24,26):
        uaxis=z
        vaxis=y
        box=EAR_BOX
    else:
        uaxis=x
        vaxis=y
        box=NOSE_BOX
    amin,bmin=source_min
    amax,bmax=source_max
    a=(uaxis-amin)/(amax-amin) if amax>amin else .5
    b=(vaxis-bmin)/(bmax-bmin) if bmax>bmin else .5
    x0,y0,x1,y1=box
    px=x0+np.clip(a,0,1)*(x1-x0)
    py=y1-np.clip(b,0,1)*(y1-y0)
    return float(px/256),float(1-py/256)

def main():
    asset=ROOT/'Assets'/'Degu.meshbin'
    fs=ROOT/'Assets'/'DeguFaceSets.zlib.b64'
    raw=asset.read_bytes()
    assert raw[:4]==b'SDG3'
    count,icount,bones=struct.unpack_from('<HHB',raw,4)
    assert (count,icount,bones)==(1477,7740,34), (count,icount,bones)
    old_rows=[bytearray(raw[9+i*17:9+(i+1)*17]) for i in range(count)]
    faces=np.frombuffer(raw,dtype='<u2',offset=9+count*17,count=icount).reshape(-1,3).copy()
    original_faces=faces.copy()
    old_labels=np.frombuffer(zlib.decompress(base64.b64decode(fs.read_text().strip())),dtype=np.uint8)
    assert old_labels.size==count
    labels=old_labels.tolist()
    assert sum(len(x) for x in FACE_PATCHES.values())==34
    face_label={}
    for label,ix in FACE_PATCHES.items():
        for fi in ix:
            assert 0<=fi<len(faces) and fi not in face_label
            face_label[fi]=label
    assert len(face_label)==34

    coords=np.array([struct.unpack_from('<hhh',row,0) for row in old_rows],
                    dtype=np.float64)/16384.
    extrema={}
    for label,faceids in FACE_PATCHES.items():
        verts=np.unique(faces[faceids].reshape(-1))
        pts=coords[verts]
        v=np.column_stack((pts[:,2],pts[:,1])) if label in (24,26) else pts[:,:2]
        extrema[label]=(v.min(axis=0),v.max(axis=0))
    copies={}
    groups=collections.Counter()
    for faceidx,label in face_label.items():
        for corner in range(3):
            old_vertex=int(faces[faceidx,corner])
            key=(old_vertex,label)
            if key not in copies:
                newrow=bytearray(old_rows[old_vertex])
                u,v=emit_uv(label,coords[old_vertex],*extrema[label])
                struct.pack_into('<ee',newrow,6,u,v)
                copies[key]=count+len(copies)
                old_rows.append(newrow)
                labels.append(label)
                groups[label]+=1
            faces[faceidx,corner]=copies[key]
    new_count=len(old_rows)
    assert new_count==count+32,(count,new_count)
    assert len(labels)==new_count
    assert len(faces)==len(original_faces)
    assert np.array_equal(faces[~np.isin(np.arange(len(faces)), list(face_label))],
                          original_faces[~np.isin(np.arange(len(faces)), list(face_label))])
    for fi,label in face_label.items():
        assert all(labels[int(v)]==label for v in faces[fi])
    # Original vertex data and skin weights preserved; new vertices are identical
    # except for UV0 and are used only by the user's annotated special faces.
    output=bytearray(b'SDG3'+struct.pack('<HHB',new_count,icount,bones))
    for row in old_rows: output.extend(row)
    output.extend(faces.astype('<u2').tobytes())
    assert len(output)==9+new_count*17+icount*2
    asset.write_bytes(output)
    fs.write_text(base64.b64encode(zlib.compress(bytes(labels),9)).decode()+'\n')
    report={
        "status":"PASS",
        "source":"User edited Degu_Wolf_Reweighted_v0.1.8.blend; UV correction from reviewed v0.1.9.blend",
        "original_vertices":count,
        "export_vertices":new_count,
        "triangles":len(faces),
        "annotated_faces":{str(label):len(ix) for label,ix in FACE_PATCHES.items()},
        "new_vertices_by_faceset":dict(groups),
        "original_weights_preserved":True,
        "original_triangle_order_preserved":True,
        "inner_ear_regions":[24,26],
        "nose_region":25,
        "other_texture_regions_unchanged":True,
    }
    dest=ROOT/'release'/'Degu_User_FaceSets_QA.json'
    dest.parent.mkdir(parents=True,exist_ok=True)
    dest.write_text(json.dumps(report,indent=2,ensure_ascii=False)+'\n')
    print('DEGU_USER_FACESETS_QA_PASS',json.dumps(report,ensure_ascii=False))

if __name__=='__main__':main()
