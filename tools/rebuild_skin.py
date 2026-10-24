#!/usr/bin/env python3
"""Rebuild Valheim Degu skin from the user's original FaceSets, not old weights."""
import base64, collections, json, math, struct, zlib
from pathlib import Path
import numpy as np
ROOT=Path(__file__).resolve().parents[1]
NAMES=['Pelvis','Spine','Spine1','Spine2','Neck','Head','Jaw','Tongue',
'L Ear','R Ear','L Cheek','R Cheek','Eye R','Eye L',
'L Clavicle','L UpperArm','L Forearm','L Hand',
'R Clavicle','R UpperArm','R Forearm','R Hand',
'L Thigh','L Calf','L HorseLink','L Foot',
'R Thigh','R Calf','R HorseLink','R Foot','Tail','Tail1','Tail2','Tail3']
SYMM={2:(25,29),5:(23,27),8:(24,28),15:(8,9),17:(15,19),18:(16,20),19:(17,21),20:(14,18)}
REGION={4:1,9:33,10:32,11:0,12:2,13:5,14:4,16:6,21:13,22:12,23:5}
JOINTS={0:(0,.19,-.295),1:(0,.245,-.125),2:(0,.24,-.01),3:(0,.28,.085),
4:(0,.31,.19),5:(0,.35,.30),6:(0,.275,.33),7:(0,.278,.38),
8:(-.107,.396,.219),9:(.107,.396,.219),10:(-.078,.315,.328),
11:(.078,.315,.328),12:(.062,.354,.312),13:(-.062,.354,.312),
14:(-.092,.135,.16),15:(-.086,.08,.16),16:(-.07,.033,.173),
17:(-.086,.012,.239),18:(.092,.135,.16),19:(.086,.08,.16),
20:(.07,.033,.173),21:(.086,.012,.239),22:(-.104,.145,-.295),
23:(-.098,.07,-.29),24:(-.095,.028,-.27),25:(-.093,.01,-.172),
26:(.104,.145,-.295),27:(.098,.07,-.29),28:(.095,.028,-.27),
29:(.093,.01,-.172),30:(0,.027,-.404),31:(0,.025,-.47),
32:(0,.018,-.55),33:(0,.01,-.63)}
def ramp(a,b,x):
    t=np.clip((x-a)/(b-a),0,1)
    return t*t*(3-2*t)
def main():
    asset=ROOT/'Assets'/'Degu.meshbin'
    raw=bytearray(asset.read_bytes())
    assert raw[:4]==b'SDG3'
    n,icount,bones=struct.unpack_from('<HHB',raw,4)
    assert (n,icount,bones)==(1477,7740,34)
    faceid=np.frombuffer(zlib.decompress(base64.b64decode((ROOT/'Assets'/'DeguFaceSets.zlib.b64').read_text().strip())),dtype=np.uint8)
    assert len(faceid)==n
    pts=np.array([struct.unpack_from('<hhh',raw,9+i*17) for i in range(n)],dtype=np.float64)/16384
    tris=np.frombuffer(raw,dtype='<u2',count=icount,offset=9+n*17).reshape(-1,3)
    x,y,z=pts.T
    W=np.zeros((n,34),dtype=float)
    for label,bone in REGION.items():W[faceid==label,bone]=1
    for label,(left,right) in SYMM.items():
        mask=faceid==label
        W[mask&(x<0),left]=1;W[mask&(x>=0),right]=1
    mask=faceid==6
    W[mask&(z>-.435),30]=1;W[mask&(z<=-.435),31]=1
    ocular=np.isin(faceid,[21,22])
    # Restore left/right thighs, missing in original FaceSet partition.
    hip=.90*ramp(.032,.078,abs(x))*ramp(-.44,-.355,z)*(1-ramp(-.215,-.115,z))*ramp(.025,.07,y)*(1-ramp(.235,.31,y))
    for bone,side in ((22,x<0),(26,x>=0)):
        t=hip*side*(~ocular)
        W*=(1-t[:,None]);W[:,bone]+=t
    flank=.35*ramp(.042,.087,abs(x))*ramp(-.38,-.28,z)*(1-ramp(-.16,-.09,z))*ramp(.14,.22,y)*(1-ramp(.30,.36,y))
    W[:,22]+=flank*(x<0);W[:,26]+=flank*(x>=0)
    shoulder=.67*ramp(.045,.082,abs(x))*ramp(.045,.115,z)*(1-ramp(.245,.29,z))*ramp(.02,.065,y)*(1-ramp(.18,.25,y))
    W[:,15]+=shoulder*(x<0)*(~ocular);W[:,19]+=shoulder*(x>=0)*(~ocular)
    W[:,3]+=.5*(1-ramp(.16,.22,abs(x)))*ramp(.025,.09,z)*(1-ramp(.15,.23,z))*ramp(.14,.20,y)*(~ocular)
    cheek=.25*ramp(.038,.065,abs(x))*ramp(.255,.30,z)*(1-ramp(.36,.39,z))*ramp(.24,.3,y)*(1-ramp(.38,.43,y))*(~ocular)
    W[:,10]+=cheek*(x<0);W[:,11]+=cheek*(x>=0)
    W[W.sum(axis=1)==0,5]=1
    W/=W.sum(axis=1,keepdims=True)
    adj=[set() for _ in range(n)]
    for aa,bb,cc in tris:
        a,b,c=map(int,(aa,bb,cc))
        adj[a].update((b,c));adj[b].update((a,c));adj[c].update((a,b))
    for _ in range(6):
        nw=W.copy()
        for i,nei in enumerate(adj):
            if not nei or ocular[i]:continue
            near=[k for k in nei if not ocular[k] and (x[i]*x[k]>=0 or abs(x[i])<.015 or abs(x[k])<.015)]
            if near:nw[i]=.72*W[i]+.28*np.mean(W[near],axis=0)
        W=nw
    seams=collections.defaultdict(list)
    for i,point in enumerate(pts):seams[tuple(np.rint(point*16384).astype(int))].append(i)
    for indices in seams.values():
        if len(indices)>1:W[indices]=np.mean(W[indices],axis=0)
    W[faceid==21,:]=0;W[faceid==21,13]=1
    W[faceid==22,:]=0;W[faceid==22,12]=1
    for i in range(n):
        strongest=np.argsort(-W[i],kind='stable')
        W[i,strongest[4:]]=0;W[i,W[i]<.0001]=0
        W[i]/=W[i].sum()
    assert np.max(abs(W.sum(axis=1)-1))<1e-10
    assert np.max(np.count_nonzero(W,axis=1))<=4
    counts={name:int(np.sum(W[:,i]>.005)) for i,name in enumerate(NAMES)}
    for bone in ('L Thigh','R Thigh','L UpperArm','R UpperArm','L Forearm','R Forearm','L Foot','R Foot'):
        assert counts[bone]>=8,(bone,counts[bone])
    for i in range(n):
        ids=np.argsort(-W[i],kind='stable')[:4]
        q=np.floor(W[i,ids]*255).astype(int)
        assert 0 <= 255-sum(q[:3]) <= 255
        start=9+i*17+10
        raw[start:start+4]=bytes(map(int,ids))
        raw[start+4:start+7]=bytes(map(int,q[:3]))
    asset.write_bytes(raw)
    deformation={}
    for name in ('L Thigh','R Thigh','L UpperArm','R UpperArm','L Forearm','R Forearm','L Foot','R Foot','Head','Tail2'):
        bone=NAMES.index(name);anchor=np.array(JOINTS[bone]);delta=pts-anchor;angle=math.radians(22)
        posed=np.stack([np.cos(angle)*delta[:,0]-np.sin(angle)*delta[:,2],delta[:,1],np.sin(angle)*delta[:,0]+np.cos(angle)*delta[:,2]],axis=-1)+anchor
        movement=np.linalg.norm(posed-pts,axis=1)*W[:,bone]
        deformation[name]=int(sum(movement>.0005))
        assert deformation[name]>4,(name,deformation[name])
    # Verify original mesh winding is outward (flipping causes invisible front surfaces).
    a,b,c=pts[tris[:,0]],pts[tris[:,1]],pts[tris[:,2]]
    volume=float(np.einsum('ij,ij->',a,np.cross(b,c))/6)
    assert volume>.0
    qa={'status':'PASS','source':'Degu original FaceSets','vertices':n,'triangles':icount//3,
        'original_mesh_vertices':1325,'outward_signed_volume':volume,
        'maximum_influences':int(np.max(np.count_nonzero(W,axis=1))),
        'normalization_error':float(np.max(abs(W.sum(axis=1)-1))),
        'group_vertex_counts':counts,'analytic_deformation_vertices':deformation,
        'warning':'Numerical pose check; animation must still be tested in Valheim.'}
    output=ROOT/'release'/'Degu_Skinning_QA.json'
    output.parent.mkdir(parents=True,exist_ok=True)
    output.write_text(json.dumps(qa,indent=2,ensure_ascii=False)+'\n')
    print('DEGU_SKIN_QA_PASS',json.dumps(qa,ensure_ascii=False))
if __name__=='__main__':main()
