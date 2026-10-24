using System;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;

namespace SirrohDegu
{
    internal static class DeguMesh
    {
        // GLB skin joint order, not alphabetically sorted.
        private static readonly string[] Names = {
            "Pelvis","Spine","Spine1","Spine2","Neck","Head","Jaw","Tongue",
            "L Ear","R Ear","L Cheek","R Cheek","Eye R","Eye L",
            "L Clavicle","L UpperArm","L Forearm","L Hand",
            "R Clavicle","R UpperArm","R Forearm","R Hand",
            "L Thigh","L Calf","L HorseLink","L Foot",
            "R Thigh","R Calf","R HorseLink","R Foot",
            "Tail","Tail1","Tail2","Tail3"
        };

        // Original Wolf hierarchy index order, recovered from Wolf.prefab. Every
        // parent index precedes its children and exactly matches Names above.
        private static readonly int[] Parent = {
            -1,0,1,2,3,4,5,6,5,5,5,5,5,5,4,14,15,16,4,18,19,20,1,22,23,24,1,26,27,28,1,30,31,32
        };

        // Anatomical attachment points measured on the SOURCE Degu mesh, NOT
        // original Wolf bone offsets. Degu source: X=side, Y=up, Z=forward.
        private static readonly Vector3[] DeguJoints = {
            new Vector3(0f,.19f,-.295f),      // Pelvis
            new Vector3(0f,.245f,-.125f),     // Spine
            new Vector3(0f,.24f,-.01f),       // Spine1
            new Vector3(0f,.28f,.085f),       // Spine2
            new Vector3(0f,.31f,.19f),        // Neck
            new Vector3(0f,.35f,.30f),        // Head
            new Vector3(0f,.275f,.33f),       // Jaw
            new Vector3(0f,.278f,.38f),       // Tongue (no skin weights yet)
            new Vector3(-.107f,.396f,.219f),  // L Ear
            new Vector3(.107f,.396f,.219f),   // R Ear
            new Vector3(-.078f,.315f,.328f),  // L Cheek
            new Vector3(.078f,.315f,.328f),   // R Cheek
            new Vector3(.062f,.354f,.312f),   // Eye R
            new Vector3(-.062f,.354f,.312f),  // Eye L
            new Vector3(-.092f,.135f,.16f),   // L Clavicle
            new Vector3(-.086f,.08f,.16f),    // L UpperArm
            new Vector3(-.07f,.033f,.173f),   // L Forearm
            new Vector3(-.086f,.012f,.239f),  // L Hand
            new Vector3(.092f,.135f,.16f),    // R Clavicle
            new Vector3(.086f,.08f,.16f),     // R UpperArm
            new Vector3(.07f,.033f,.173f),    // R Forearm
            new Vector3(.086f,.012f,.239f),   // R Hand
            new Vector3(-.104f,.145f,-.295f),// L Thigh
            new Vector3(-.098f,.07f,-.29f),   // L Calf
            new Vector3(-.095f,.028f,-.27f),  // L HorseLink
            new Vector3(-.093f,.01f,-.172f),  // L Foot
            new Vector3(.104f,.145f,-.295f), // R Thigh
            new Vector3(.098f,.07f,-.29f),    // R Calf
            new Vector3(.095f,.028f,-.27f),   // R HorseLink
            new Vector3(.093f,.01f,-.172f),  // R Foot
            new Vector3(0f,.027f,-.404f),    // Tail
            new Vector3(0f,.025f,-.47f),     // Tail1
            new Vector3(0f,.018f,-.55f),     // Tail2
            new Vector3(0f,.01f,-.63f)       // Tail3
        };

        private const float VisualScale = 1.30f;
        private static readonly Vector3 DeguPelvis = new Vector3(0f, .19f, -.295f);

        internal static void ReplaceMesh(SkinnedMeshRenderer skin, GameObject cloneRoot)
        {
            if (cloneRoot == null || skin == null || skin.sharedMesh == null)
                throw new InvalidOperationException("Wolf has no valid skinned renderer.");
            if (Names.Length != 34 || Parent.Length != Names.Length || DeguJoints.Length != Names.Length)
                throw new InvalidOperationException("Degu skeleton reference arrays inconsistent.");

            var lookup = new Dictionary<string, Transform>(StringComparer.Ordinal);
            foreach (Transform t in cloneRoot.GetComponentsInChildren<Transform>(true))
                if (t != null && !lookup.ContainsKey(t.name))
                    lookup.Add(t.name, t);

            Transform[] wolfBones = new Transform[Names.Length];
            for (int i = 0; i < Names.Length; ++i)
            {
                if (!lookup.TryGetValue(Names[i], out wolfBones[i]) || wolfBones[i] == null)
                    throw new InvalidOperationException("Original Wolf bone missing: " + Names[i]);
                if (Parent[i] >= 0 && wolfBones[i].parent != wolfBones[Parent[i]])
                    throw new InvalidOperationException("Wolf hierarchy mismatch at " + Names[i]);
            }

            Mesh model = Load();
            try
            {
                // Get the UP vector from Wolf's actual feet-to-thigh axis.
                // The previous Cross(left, forward) could point DOWN, turning
                // the body over and placing the feet above the ground.
                Vector3 originalForward = wolfBones[5].position - wolfBones[0].position;
                Vector3 originalUp =
                    (wolfBones[22].position - wolfBones[25].position) +
                    (wolfBones[26].position - wolfBones[29].position);
                Vector3 side = wolfBones[22].position - wolfBones[26].position;
                if (originalForward.sqrMagnitude < .00001f ||
                    originalUp.sqrMagnitude < .00001f || side.sqrMagnitude < .00001f)
                    throw new InvalidOperationException("Wolf anatomical basis is degenerate.");
                // Y is Valheim's global gravity-up. Limb vectors are tilted
                // during rest (wolf hocks are behind the thighs), therefore
                // their direction must validate uprightness, not determine it.
                Vector3 up = Vector3.up;
                Vector3 forward = Vector3.ProjectOnPlane(originalForward, up).normalized;
                if (forward.sqrMagnitude < .5f)
                    throw new InvalidOperationException("Wolf head vector parallel to up.");
                Vector3 right = Vector3.Cross(up, forward).normalized;
                up = Vector3.Cross(forward, right).normalized;
                if (Vector3.Dot(up, originalUp.normalized) < .6f ||
                    Vector3.Dot(forward, originalForward.normalized) < .25f ||
                    Mathf.Abs(Vector3.Dot(right, side.normalized)) < .6f)
                    throw new InvalidOperationException("Wolf axis orientation did not validate.");

                // Do not treat animated Wolf mesh bounds as the collision
                // floor: those bounds include tail and animation safety margins.
                Vector3 pelvisWorld = wolfBones[0].position;
                float targetFloorWorldY = cloneRoot.transform.position.y + 0.025f;

                // Source Degu is already level (+Y up, same resting height
                // of front/rear feet). Disable old artificial body pitch.
                Quaternion levelRotation = Quaternion.identity;
                Vector3 MapDegu(Vector3 point)
                {
                    Vector3 p = point - DeguPelvis;
                    Vector3 basis = right*p.x + up*p.y + forward*p.z;
                    return pelvisWorld + VisualScale * (levelRotation * basis);
                }
                // Anchor visual ground to FOOT geometry, never tail/belly.
                float footMinimum = float.PositiveInfinity;
                int[] footBones = {17,21,25,29};
                BoneWeight[] weights = model.boneWeights;
                Vector3[] rawVertices = model.vertices;
                for (int v=0;v<rawVertices.Length;v++)
                {
                    BoneWeight w=weights[v];
                    float influence=0f;
                    foreach (int bone in footBones)
                    {
                        if (w.boneIndex0==bone) influence+=w.weight0;
                        if (w.boneIndex1==bone) influence+=w.weight1;
                        if (w.boneIndex2==bone) influence+=w.weight2;
                        if (w.boneIndex3==bone) influence+=w.weight3;
                    }
                    if (influence>=.25f)
                        footMinimum=Mathf.Min(footMinimum,MapDegu(rawVertices[v]).y);
                }
                if (float.IsNaN(footMinimum) || float.IsInfinity(footMinimum))
                    throw new InvalidOperationException("Degu weighted paw geometry missing.");
                float groundCorrection=targetFloorWorldY-footMinimum;
                if (float.IsNaN(groundCorrection) || Mathf.Abs(groundCorrection) > 10f)
                    throw new InvalidOperationException("Degu ground calibration invalid.");

                Vector3 Project(Vector3 point)
                {
                    return MapDegu(point) + groundCorrection * up;
                }
                // Under the right-handed transform above, the source mesh's
                // negative X side can map to the original Wolf's positive
                // anatomical side. In that case swap L/R ANIMATION DRIVERS,
                // never vertex groups or mesh topology.
                bool swapSides = Vector3.Dot(right, side.normalized) > 0f;
                InstanceLog("Degu axis QA: up-to-leg=" +
                    Vector3.Dot(up, originalUp.normalized).ToString("F2") +
                    ", forwards=" + Vector3.Dot(forward, originalForward.normalized).ToString("F2") +
                    ", swapSides=" + swapSides +
                    ", groundCorrection=" + groundCorrection.ToString("F3"));

                // Fit vertices and bones through exactly the same transformation.
                Vector3[] vertices = model.vertices;
                for (int i=0; i<vertices.Length; i++)
                    vertices[i] = skin.transform.InverseTransformPoint(Project(vertices[i]));
                model.vertices = vertices;

                Transform[] drivenWolf = wolfBones;
                Transform[] fitted = new Transform[Names.Length];
                GameObject rigRoot = new GameObject("SirrohDegu_FittedSkeleton");
                rigRoot.transform.SetParent(skin.transform, false);
                for (int i=0; i<Names.Length; i++)
                {
                    var joint = new GameObject("Sirroh_" + Names[i]);
                    Transform parent = Parent[i] < 0 ? rigRoot.transform : fitted[Parent[i]];
                    joint.transform.SetParent(parent, false);
                    fitted[i] = joint.transform;
                    if (Parent[i] < 0)
                        fitted[i].rotation = drivenWolf[i].rotation;
                    else
                        fitted[i].localRotation = drivenWolf[i].localRotation;
                    fitted[i].position = Project(DeguJoints[i]);
                }

                Matrix4x4[] inverseBinds = new Matrix4x4[fitted.Length];
                for (int i=0; i<fitted.Length; i++)
                    inverseBinds[i] = fitted[i].worldToLocalMatrix * skin.transform.localToWorldMatrix;
                model.bindposes = inverseBinds;
                model.RecalculateBounds();
                // Split eye triangles into a separately controllable submesh
                // using the SAME original wolfMat in both slots. Opposite
                // winding copies ensure mirrored eye spheres remain visible
                // from both sides despite Unity backface culling.
                int[] indices = model.triangles;
                BoneWeight[] bw = model.boneWeights;
                var coatIndices = new List<int>(indices.Length);
                var eyeIndices = new List<int>(indices.Length / 2);
                for (int t=0; t<indices.Length; t+=3)
                {
                    int a=indices[t], b=indices[t+1], c=indices[t+2];
                    bool eyeA = (bw[a].boneIndex0 == 12 || bw[a].boneIndex0 == 13) && bw[a].weight0 > .8f;
                    bool eyeB = (bw[b].boneIndex0 == 12 || bw[b].boneIndex0 == 13) && bw[b].weight0 > .8f;
                    bool eyeC = (bw[c].boneIndex0 == 12 || bw[c].boneIndex0 == 13) && bw[c].weight0 > .8f;
                    if (eyeA && eyeB && eyeC)
                    {
                        eyeIndices.Add(a); eyeIndices.Add(b); eyeIndices.Add(c);
                        eyeIndices.Add(a); eyeIndices.Add(c); eyeIndices.Add(b);
                    }
                    else
                    {
                        coatIndices.Add(a); coatIndices.Add(b); coatIndices.Add(c);
                    }
                }
                if (eyeIndices.Count == 0 || coatIndices.Count == 0)
                    throw new InvalidOperationException("Degu eye submeshes could not be classified.");
                Material originalWolfMaterial = skin.sharedMaterial;
                if (originalWolfMaterial == null)
                    throw new InvalidOperationException("Wolf material missing; Degu requires original wolfMat.");
                model.subMeshCount = 2;
                model.SetTriangles(coatIndices.ToArray(), 0);
                model.SetTriangles(eyeIndices.ToArray(), 1);
                skin.sharedMaterials = new[] { originalWolfMaterial, originalWolfMaterial };
                // Keep ORIGINAL Valheim Wolf material & its original atlas:
                // eye emissive is isolated by submesh MaterialPropertyBlock.
                skin.bones = fitted;
                skin.rootBone = fitted[0];
                skin.sharedMesh = model;
                skin.localBounds = model.bounds;
                skin.updateWhenOffscreen = true;

                // v0.1.8 raised animated PAW BONES to the prefab origin,
                // retracting the legs into the torso. Never override limbs.
                var driver = cloneRoot.AddComponent<DeguRigDriver>();
                driver.Configure(skin.transform, drivenWolf, fitted);
                var eyeGlow = cloneRoot.AddComponent<DeguEyeEmission>();
                eyeGlow.Configure(skin);
                InstanceLog("Degu shader QA: original " + originalWolfMaterial.name +
                    ", eye triangles doubled=" + eyeIndices.Count/3 +
                    ", pitch correction disabled.");
                InstanceLog("Degu fitted rig v0.1.8: vertices=" + model.vertexCount +
                    ", 34 joints, grounded using paw vertices without leg repositioning, original Wolf materials.");
            }
            catch
            {
                UnityEngine.Object.Destroy(model);
                throw;
            }
        }

        private static void InstanceLog(string text) { if (DeguPlugin.Instance != null) DeguPlugin.Log.LogInfo(text); }

        private static Mesh Load()
        {
            using (Stream stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("SirrohDegu.Model"))
            {
                if (stream == null) throw new FileNotFoundException("Embedded Degu.meshbin missing.");
                using (var br = new BinaryReader(stream))
                {
                    if (br.ReadByte()!='S' || br.ReadByte()!='D' || br.ReadByte()!='G' || br.ReadByte()!='3')
                        throw new InvalidDataException("Wrong model asset magic.");
                    int vcount=br.ReadUInt16(), icount=br.ReadUInt16(), bones=br.ReadByte();
                    if (vcount < 100 || vcount > 60000 || icount < 300 || icount > 65000 || icount%3!=0 || bones != Names.Length)
                        throw new InvalidDataException("Invalid model sizes.");
                    var vertices=new Vector3[vcount];
                    var uv=new Vector2[vcount];
                    var weights=new BoneWeight[vcount];
                    for (int i=0;i<vcount;i++)
                    {
                        vertices[i]=new Vector3(br.ReadInt16()/16384f,br.ReadInt16()/16384f,br.ReadInt16()/16384f);
                        uv[i]=new Vector2(ReadHalf(br.ReadUInt16()),ReadHalf(br.ReadUInt16()));
                        int a=br.ReadByte(),b=br.ReadByte(),c=br.ReadByte(),d=br.ReadByte();
                        int wa=br.ReadByte(),wb=br.ReadByte(),wc=br.ReadByte(),wd=255-wa-wb-wc;
                        if (wd<0 || wd>255) throw new InvalidDataException("Corrupt normalized weights.");
                        if (a>=bones || b>=bones || c>=bones || d>=bones)
                            throw new InvalidDataException("Out-of-range bone index at vertex " + i);
                        int total=wa+wb+wc+wd;
                        if (total == 0) throw new InvalidDataException("Vertex has no weights: " + i);
                        weights[i]=new BoneWeight{
                            boneIndex0=a,boneIndex1=b,boneIndex2=c,boneIndex3=d,
                            weight0=(float)wa/total,weight1=(float)wb/total,
                            weight2=(float)wc/total,weight3=(float)wd/total
                        };
                    }
                    var triangles=new int[icount];
                    for(int i=0;i<icount;i+=3)
                    {
                        // Source SDG3 triangles already have OUTWARD winding.
                        // The previous b/c swap rendered the inside of the body.
                        int a=br.ReadUInt16(),b=br.ReadUInt16(),c=br.ReadUInt16();
                        if (a>=vcount || b>=vcount || c>=vcount) throw new InvalidDataException("Invalid triangle index.");
                        triangles[i]=a;triangles[i+1]=b;triangles[i+2]=c;
                    }
                    if (stream.Position != stream.Length) throw new InvalidDataException("Model has trailing data.");
                    var mesh=new Mesh { name="Degu_Weighted_WolfRig" };
                    mesh.vertices=vertices;
                    mesh.uv=uv;
                    mesh.triangles=triangles;
                    mesh.RecalculateNormals();
                    mesh.RecalculateTangents();
                    // Original FaceSet-based weights were rebuilt before DLL compilation.
                    mesh.boneWeights=weights;
                    mesh.RecalculateBounds();
                    return mesh;
                }
            }
        }

        private static float ReadHalf(ushort value)
        {
            int sign=(value >> 15)&1, exponent=(value >> 10)&31, mantissa=value & 1023;
            float v;
            if(exponent==0) v=(float)(mantissa * Math.Pow(2,-24));
            else if(exponent==31) v=mantissa==0 ? float.PositiveInfinity : float.NaN;
            else v=(float)((1.0+mantissa/1024.0)*Math.Pow(2,exponent-15));
            return sign==0 ? v : -v;
        }
    }
}