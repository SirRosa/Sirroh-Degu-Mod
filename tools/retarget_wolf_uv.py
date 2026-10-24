#!/usr/bin/env python3
"""Retarget the ORIGINAL Valheim Wolf atlas onto Degu's Face Set anatomy.

Valheim's Custom/Creature shader reads UV0 into _MainTex; _BumpMap and
_EmissionMap use the same UV. The user-authored Degu did NOT contain a
compatible Wolf UV unwrap: 359 of 1477 vertices used UV=(0,0), while the
dense eye meshes spanned nearly the entire atlas. Copying wolfMat alone
cannot repair this.

This rebuild changes ONLY the UV coordinates (two float16 values per
vertex) in SDG3. Geometry, bind weights, index winding and material
references are untouched. Runtime continues to use the original in-game
wolfMat and game textures; the original ripped atlas is used only to
audit / visualize the mapping offline.
"""
import argparse
import base64
import hashlib
import json
import math
import struct
import zlib
from pathlib import Path

import numpy as np

ROOT = Path(__file__).resolve().parents[1]
EXPECTED_GIT_BLOB_SHA = "b3bdc036c5a654ee216519284c0a1606e0b97b53"

# Pixel rectangles defined in the 256x256 original Wolf Pixel1 albedo.
# Eye centers are independently confirmed by the existing WolfVisuals.cs
# recoloring code (39,67) / (184,97), top-left pixel coordinates.
REGIONS = {
    "eye_l": (31.0, 59.0, 47.0, 75.0),
    "eye_r": (176.0, 89.0, 192.0, 105.0),
    "ear_l": (106.0, 89.0, 117.0, 108.0),
    "ear_r": (143.0, 89.0, 154.0, 108.0),
    "muzzle": (17.0, 137.0, 62.0, 164.0),
    "tail": (156.0, 98.0, 197.0, 168.0),
    "fur": (18.0, 116.0, 213.0, 223.0),
}

def clamp(v, a, b):
    return float(max(a,min(b,v)))

def original_atlas(path):
    from PIL import Image
    data = path.read_bytes()
    sha=hashlib.sha1(b"blob "+str(len(data)).encode("ascii")+b"\0"+data).hexdigest()
    if sha!=EXPECTED_GIT_BLOB_SHA:
        raise ValueError("Wrong source Wolf texture (Git blob SHA does not match): "+sha)
    img=Image.open(path).convert("RGBA")
    if img.size != (256,256):
        raise ValueError("Original Wolf atlas expected to be 256x256: "+str(img.size))
    return img

def to_uv(px,py):
    # Half-texel inset avoids sampling neighbouring atlas tiles.
    return (clamp(px + 0.5, 1, 254)/256.,
            1. - clamp(py + 0.5, 1, 254)/256.)

def region_uv(rect, a,b):
    x0,y0,x1,y1=rect
    return to_uv(x0+(x1-x0)*clamp(a,0.04,.96),
                 y0+(y1-y0)*clamp(b,0.04,.96))

def vertex_uv(label, position, original):
    x,y,z=position
    # Original eye spheres each contain 500+ vertices with fully stretched UVs.
    # Use a small eye-specific portion of the actual Wolf atlas, for BOTH eyes.
    if label == 21:  # Eye L, original game blue iris at x39,y67
        return region_uv(REGIONS["eye_l"], original[0], original[1])
    if label == 22:  # Eye R, original game blue iris at x184,y97
        return region_uv(REGIONS["eye_r"], original[0], original[1])
    # Separate ear islands: mirrored anatomy maps to the two ear color patches.
    if label == 15:
        source = "ear_l" if x < 0 else "ear_r"
        return region_uv(REGIONS[source],
                         .50+(z-.219)*4., .50-(y-.396)*5.)
    # Jaw and muzzle: a dark/white band in the actual wolf face atlas.
    if label == 16:
        return region_uv(REGIONS["muzzle"],
                         .5-(z-.34)*3., .5-(y-.29)*3.)
    if label == 23:
        return region_uv(REGIONS["muzzle"],
                         .18+(0.395-z)*1.7, .35+(0.37-y)*2.)
    # Tail uses the dark curled tail art at the top-rear of the Wolf UV atlas.
    if label in (6,9,10):
        return region_uv(REGIONS["tail"],
                         .40 + (-z-.49)*1.3,
                         .48 - (y-.035)*4. + (-z-.49)*.30)
    # Wolf albedo has recognizable body/legs in side silhouette layout.
    # Front of Degu (+Z) maps to the left head/forepaws of the wolf drawing,
    # hindquarters (-Z) to the right. This is planar projection, with both
    # sides mirrored on the SAME opaque color regions (no entire-map eye).
    px = clamp(114.0 - 191.0*z, 18.0, 210.0)
    py = clamp(225.0 - 181.0*y, 117.0, 219.0)
    if label in (2,5,8,17,18,19):
        py = clamp(py, 181.0, 219.0) # paws and legs in lower wolf body area
    if label == 14:
        px=clamp(px, 30, 92)
        py=clamp(py, 120, 187)
    if label == 13: # Head
        px=clamp(px, 25, 83)
        py=clamp(py, 121, 178)
    # Tiny mirrored offset (not a second texture) adds left/right side shading
    # and helps avoid sampling the exact same texel on adjacent UV island seams.
    px += 1.25 if x>0 else -1.25
    return to_uv(px,py)

def main():
    p=argparse.ArgumentParser()
    p.add_argument("--atlas",type=Path,required=True)
    args=p.parse_args()
    atlas=original_atlas(args.atlas)
    asset=ROOT/"Assets"/"Degu.meshbin"
    raw=bytearray(asset.read_bytes())
    if raw[:4]!=b"SDG3":raise ValueError("Not a valid Degu SDG3 payload")
    n,ni,bones=struct.unpack_from("<HHB",raw,4)
    if (n,ni,bones)!=(1477,7740,34):raise ValueError("Degu topology mismatch")
    labels=np.frombuffer(zlib.decompress(base64.b64decode(
        (ROOT/"Assets"/"DeguFaceSets.zlib.b64").read_text().strip())),dtype=np.uint8)
    if len(labels)!=n:raise ValueError("Face Sets and UV vertices mismatch")
    points=np.array([struct.unpack_from("<3h",raw,9+17*i) for i in range(n)],dtype=float)/16384.
    old_uv=np.array([struct.unpack_from("<2e",raw,9+17*i+6) for i in range(n)])
    new_uv=[]
    for i in range(n):
        uv=vertex_uv(int(labels[i]),points[i],old_uv[i])
        struct.pack_into("<ee",raw,9+17*i+6,*uv)
        new_uv.append(uv)
    uv=np.asarray(new_uv)
    if np.any((uv<=0)|(uv>=1)):raise ValueError("UV out of atlas bounds")
    # Make sure all other bytes, including ALL skin weights, remain untouched.
    original=asset.read_bytes()
    for i in range(n):
        b=9+17*i
        if raw[b:b+6]!=original[b:b+6] or raw[b+10:b+17]!=original[b+10:b+17]:
            raise ValueError("UV update altered geometry or skin weights!")
    if raw[9+17*n:]!=original[9+17*n:]:
        raise ValueError("UV update altered triangle winding!")
    asset.write_bytes(raw)

    # Alpha and shading diagnostics on the exact original Wolf texture;
    # original material references are untouched in the compiled DLL.
    pixels=np.asarray(atlas,dtype=np.uint8)
    sampled=[]
    for u,v in uv:
        xi=int(clamp(u*256.,0,255))
        yi=int(clamp((1.-v)*256.,0,255))
        sampled.append(pixels[yi,xi,:])
    sampled=np.asarray(sampled)
    qa={"status":"PASS","source":"Original Valheim wolfMat _MainTex reference",
        "reference_git_blob_sha":EXPECTED_GIT_BLOB_SHA,
        "atlas_size":[256,256],"vertices":n,
        "original_uv_zero_count":int(np.sum(np.all(np.abs(old_uv)<1e-5,axis=1))),
        "new_uv_zero_count":int(np.sum(np.all(np.abs(uv)<1e-5,axis=1))),
        "face_set_count":int(len(set(labels.tolist()))),
        "eye_l_count":int(np.sum(labels==21)),"eye_r_count":int(np.sum(labels==22)),
        "eye_l_uv_bounds":uv[labels==21].min(0).tolist()+uv[labels==21].max(0).tolist(),
        "eye_r_uv_bounds":uv[labels==22].min(0).tolist()+uv[labels==22].max(0).tolist(),
        "sampled_alpha_min":int(sampled[:,3].min()),
        "sampled_alpha_below_128":int(np.sum(sampled[:,3]<128)),
        "sampled_alpha_below_32":int(np.sum(sampled[:,3]<32)),
        "source_material":"Existing runtime wolfMat (not replaced)",
        "warning":"UV sampling validated offline; game rendering must still be verified in Valheim."}
    if qa["sampled_alpha_below_32"]>0:
        print("Warning: original atlas contains low-alpha samples; Custom/Creature uses albedo RGB")
    out=ROOT/"release"
    out.mkdir(parents=True,exist_ok=True)
    (out/"Degu_Wolf_UV_QA.json").write_text(json.dumps(qa,indent=2)+"\n")
    # Export a visual UV overlay that can be examined independently of Blender.
    from PIL import ImageDraw
    vis=atlas.resize((1024,1024))
    pen=ImageDraw.Draw(vis,"RGBA")
    for i in range(n):
        u,v=uv[i]
        xx=int(u*1024);yy=int((1-v)*1024)
        color=(40,230,220,110) if int(labels[i]) in (21,22) else (236,51,45,125)
        pen.ellipse((xx-2,yy-2,xx+2,yy+2),fill=color)
    vis.save(out/"Degu_Wolf_UV_Overlay.png")
    print("DEGU_WOLF_UV_QA_PASS "+json.dumps(qa))
if __name__=="__main__":main()
