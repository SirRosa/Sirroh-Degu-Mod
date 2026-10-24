#!/usr/bin/env python3
"""Project Degu symmetrically onto the real one-sided Valheim Wolf atlas.

Ground truth: user's 256x256 outline (nose at ~65,136; eye dots ~39,67
and ~184,97; torso in one side-profile outline). Wolf's opposite side
mirrors the same outline, so no second body island is introduced.

Only UV0 in the SDG3 file changes. No skin weights, mesh geometry or
triangle winding changes. The runtime renderer retains the unmodified
Wolf materials/textures from Valheim. The Atlas is solely a build-time QA
reference, never shipped as a texture with the mod.
"""
import argparse
import base64
import hashlib
import json
import struct
import zlib
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

ROOT=Path(__file__).resolve().parents[1]
EXPECTED_GIT_SHA="b3bdc036c5a654ee216519284c0a1606e0b97b53"

# Traced from the USER'S 256x256 red outline, not an invented rectangle.
# One profile with head at the left, paws at bottom and tail at upper-right.
SIDE_OUTLINE=[
    (65,136),(95,144),(94,151),(108,169),(123,172),(146,172),
    (160,154),(165,133),(169,133),(169,155),(180,177),
    (194,188),(193,203),(183,220),(191,231),(187,237),
    (182,238),(179,232),(165,221),(162,213),(165,192),
    (159,214),(147,217),(123,218),(107,212),(89,212),
    (70,224),(62,239),(52,235),(64,218),(66,207),
    (62,197),(67,182),(67,162),(57,156),(56,146),
]
# The user corrected the eye spots from BLUE to ORANGE (24 Oct).
# The YELLOW patch is the inner ear; CYAN is the underside of paws.
EYE_CENTERS={21:(39.5,67.5,-.062),22:(183.5,96.5,.062)}
INNER_EAR=(99.2,97.3)
PAW_PAD=(157.9,101.)

NOSE_PIXEL=(65.,136.)

def load_atlas(path):
    data=path.read_bytes()
    sha=hashlib.sha1(b"blob "+str(len(data)).encode()+b"\x00"+data).hexdigest()
    assert sha==EXPECTED_GIT_SHA, "Original Wolf albedo revision mismatch: "+sha
    im=Image.open(path).convert("RGBA")
    assert im.size==(256,256)
    return im

def mask_and_points():
    im=Image.new("L",(256,256),0)
    ImageDraw.Draw(im).polygon(SIDE_OUTLINE,fill=255)
    # Keep a 2-pixel texture sampling margin within the actual silhouette.
    im=im.filter(ImageFilter.MinFilter(5))
    ys,xs=np.where(np.asarray(im)>0)
    return im,np.column_stack((xs,ys)).astype(float)

def clamp(x,a,b):
    return min(b,max(a,x))

def project(label,p,normal,mask,inside):
    x,y,z=p
    # True anatomical surface orientation, not Face Set alone:
    # the same ear set contains INNER and OUTER ear surfaces, and the
    # same paw Face Set contains upper fur and underside pads.
    nx,ny,nz=normal
    if label==15 and y>.365 and nz>.17:
        cx,cy=INNER_EAR
        return ((cx+clamp((abs(x)-.10)*70.,-2.5,2.5)+.5)/256.,
                1.-(cy+clamp((y-.40)*75.,-3.,3.)+.5)/256.)
    if label in (2,19) and y<.075 and ny<-.10:
        cx,cy=PAW_PAD
        return ((cx+clamp(abs(x)*18.,-3.2,3.2)+.5)/256.,
                1.-(cy+clamp((z-.035)*16.,-4.,4.)+.5)/256.)
    # Degu X is side; BOTH SIDES deliberately use the SAME Wolf profile
    # (no 1.25-pixel left/right offsets, no second copy of the atlas).
    px=123.-150.*z
    py=236.-220.*y

    if label in (17,18,19,20): # forelegs/forepaws
        px-=15.
    elif label in (2,5,8):     # rear legs/feet
        px+=25.
    elif label==13:            # face peak near user's nose marker
        py-=17.*clamp((z-.315)/.08,0,1)
    elif label in (6,9,10):    # raised tail UV island
        t=clamp((-z-.395)/(.693-.395),0,1)
        px=181.-16.*t
        py=184.-49.*t+(y-.02)*140.

    if label==23:              # explicitly annotated green nose point
        px,py=NOSE_PIXEL
    elif label in EYE_CENTERS:
        # Map sphere front using anatomical XY, NOT the original distorted
        # eye UVs which were stretched across the complete texture.
        cx,cy,eye_x=EYE_CENTERS[label]
        px=cx+(x-eye_x)*200.
        py=cy-(y-.354)*200.
    else:
        ix=int(clamp(round(px),0,255))
        iy=int(clamp(round(py),0,255))
        if not mask[iy,ix]:
            q=np.asarray((px,py))
            dist=np.sum((inside-q)**2,axis=1)
            px,py=inside[np.argmin(dist)]
    # Top-left atlas coordinate -> Unity/Blender bottom-left UV
    return ((px+.5)/256.,1.-(py+.5)/256.)

def main():
    parser=argparse.ArgumentParser()
    parser.add_argument("--atlas",required=True,type=Path)
    args=parser.parse_args()
    atlas=load_atlas(args.atlas)
    mask,inside=mask_and_points()
    asset=ROOT/"Assets"/"Degu.meshbin"
    original=asset.read_bytes()
    raw=bytearray(original)
    assert raw[:4]==b"SDG3"
    n,nindices,nbones=struct.unpack_from("<HHB",raw,4)
    assert (n,nindices,nbones)==(1477,7740,34)
    ids=np.frombuffer(zlib.decompress(base64.b64decode(
        (ROOT/"Assets"/"DeguFaceSets.zlib.b64").read_text().strip())),dtype=np.uint8)
    assert len(ids)==n
    points=np.asarray([struct.unpack_from("<hhh",raw,9+i*17) for i in range(n)],dtype=float)/16384.
    # Calculate per-vertex normals to distinguish ear interiors and toe pads.
    tris=np.frombuffer(raw,dtype="<u2",count=nindices,offset=9+n*17).reshape(-1,3)
    normals=np.zeros_like(points)
    for tri in tris:
        a,b,c=map(int,tri)
        norm=np.cross(points[b]-points[a],points[c]-points[a])
        normals[a]+=norm
        normals[b]+=norm
        normals[c]+=norm
    normals/=np.maximum(np.linalg.norm(normals,axis=1)[:,None],1e-9)
    ear_inner_count=int(np.sum((ids==15)&(points[:,1]>.365)&(normals[:,2]>.17)))
    sole_count=int(np.sum(np.isin(ids,[2,19])&(points[:,1]<.075)&(normals[:,1]<-.10)))
    old_uv=np.asarray([struct.unpack_from("<ee",raw,15+i*17) for i in range(n)])
    new_uv=np.zeros((n,2),dtype=float)
    for i in range(n):
        uv=project(int(ids[i]),points[i],normals[i],np.asarray(mask),inside)
        struct.pack_into("<ee",raw,15+i*17,*uv)
        new_uv[i]=uv
    for i in range(n):
        base=9+i*17
        assert raw[base:base+6]==original[base:base+6], "Position mutated"
        assert raw[base+10:base+17]==original[base+10:base+17], "Skinning mutated"
    assert raw[9+17*n:]==original[9+17*n:],"Triangle indices mutated"
    assert np.all((new_uv>0)&(new_uv<1))

    # Prove source-model mirrored vertices map to identical texture pixels.
    points_by_pos={tuple(np.rint(q*16384).astype(int)):i for i,q in enumerate(points)}
    mirror_errors=[]
    for i,(x,y,z) in enumerate(points):
        if int(ids[i]) in (21,22):continue
        key=tuple(np.rint(np.array((-x,y,z))*16384).astype(int))
        j=points_by_pos.get(key)
        if j is not None and int(ids[i])==int(ids[j]):
            mirror_errors.append(float(np.linalg.norm((new_uv[i]-new_uv[j])*256)))
    if mirror_errors:
        assert max(mirror_errors)<.10,("Mismatch between mirrored wolf coat UVs",max(mirror_errors))
    asset.write_bytes(raw)

    # In the original screenshot all coat UVs must remain in the red silhouette.
    xy=np.stack((new_uv[:,0]*256-.5, (1-new_uv[:,1])*256-.5),axis=1)
    # Ear interiors and sole pads deliberately occupy their own islands
    # OUTSIDE the red coat silhouette (yellow and cyan user annotations).
    # Only the ordinary furry coat must remain inside the red outline.
    special=np.isin(ids,[21,22,23])
    special |= (ids==15)&(points[:,1]>.365)&(normals[:,2]>.17)
    special |= np.isin(ids,[2,19])&(points[:,1]<.075)&(normals[:,1]<-.10)
    away=[i for i in range(n) if not special[i] and
          not np.asarray(mask)[int(clamp(round(xy[i,1]),0,255)),
                               int(clamp(round(xy[i,0]),0,255))]]
    assert not away, ("Coat UV outside user-defined Wolf silhouette",len(away))
    ar=np.asarray(atlas)
    sample=ar[np.clip(xy[:,1].astype(int),0,255),np.clip(xy[:,0].astype(int),0,255)]
    qa={
      "status":"PASS","source":"User provided original 256x256 one-sided wolf outline",
      "wolf_texture_sha":EXPECTED_GIT_SHA,
      "vertices":n,"facesets":len(set(ids.tolist())),"eyes_left":int(sum(ids==21)),
      "eyes_right":int(sum(ids==22)),
      "nose_px":list(NOSE_PIXEL),"eye_dots_px":[[39.5,67.5],[183.5,96.5]],
      "mirrored_pairs_checked":len(mirror_errors),
      "mirrored_uv_error_max_px":max(mirror_errors) if mirror_errors else None,
      "coat_points_outside_user_outline":len(away),
      "old_zero_uv_count":int(np.sum(np.all(np.abs(old_uv)<1e-5,axis=1))),
      "new_zero_uv_count":int(np.sum(np.all(np.abs(new_uv)<1e-5,axis=1))),
      "source_atlas_sampled_alpha_min":int(sample[:,3].min()),
      "material":"Original Valheim wolfMat, not modified",
      "skin_positions_indices_preserved":True,
      "notes":"UV approximation follows user outline, NOT extracted original Wolf mesh UV triangles; game verification remains necessary."
    }
    out=ROOT/"release"
    out.mkdir(exist_ok=True)
    (out/"Degu_Wolf_UV_QA.json").write_text(json.dumps(qa,indent=2,ensure_ascii=False)+"\n")
    # Build a human-inspectable preview directly on original Valheim albedo.
    preview=atlas.resize((1024,1024))
    draw=ImageDraw.Draw(preview,"RGBA")
    lines=[(int(px*4),int(py*4)) for px,py in SIDE_OUTLINE]
    draw.line(lines+[lines[0]],fill=(255,53,52,210),width=3)
    for i,(u,v) in enumerate(new_uv):
        px=int(u*1024);py=int((1.-v)*1024)
        if ids[i] in (21,22):color=(231,134,35,175)
        elif ids[i]==15 and points[i,1]>.365 and normals[i,2]>.17:color=(255,243,35,170)
        elif ids[i] in (2,19) and points[i,1]<.075 and normals[i,1]<-.10:color=(68,211,239,175)
        elif ids[i] in (2,5,8,17,18,19,20):color=(255,190,22,125)
        else:color=(55,240,105,100)
        draw.ellipse((px-2,py-2,px+2,py+2),fill=color)
    preview.save(out/"Degu_Wolf_UV_Overlay.png")
    print("DEGU_WOLF_UV_QA_PASS "+json.dumps(qa))
if __name__=="__main__":
    main()
