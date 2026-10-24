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
        private const float GroundOffset = -.19f;
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
                // Compute an orthonormal, handedness-preserving basis from the
                // ACTUAL Wolf pose in this prefab (not a guessed Unity axis).
                // Degu +Z = forward, +Y = up, +X = anatomical right.
                Vector3 forward = wolfBones[5].position - wolfBones[0].position;
                Vector3 left = wolfBones[22].position - wolfBones[26].position;
                if (forward.sqrMagnitude < .00001f || left.sqrMagnitude < .00001f)
                    throw new InvalidOperationException("Cannot infer Wolf body axes.");
                forward.Normalize();
                left = Vector3.ProjectOnPlane(left, forward).normalized;
                Vector3 up = Vector3.Cross(left, forward).normalized;
                forward = Vector3.Cross(up, left).normalized;
                Vector3 right = -left;
                if (up.sqrMagnitude < .9f || forward.sqrMagnitude < .9f || left.sqrMagnitude < .9f)
                    throw new InvalidOperationException("Wolf anatomy basis invalid.");

                Vector3 pelvisWorld = wolfBones[0].position;
                Vector3 Project(Vector3 point)
                {
                    Vector3 p = point - DeguPelvis;
                    return pelvisWorld + VisualScale *
                        (right*p.x + up*p.y + forward*p.z) + GroundOffset*up;
                }

                // Convert mesh vertices AND the anatomical bones using one
                // common basis. Previous code only scaled vertices and bound
                // them to Wolf's unrelated joint locations: that caused the
                // extreme stretching and floating limbs.
                Vector3[] vertices = model.vertices;
                for (int i=0; i<vertices.Length; i++)
                    vertices[i] = skin.transform.InverseTransformPoint(Project(vertices[i]));
                model.vertices = vertices;

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
                        fitted[i].rotation = wolfBones[i].rotation;
                    else
                        fitted[i].localRotation = wolfBones[i].localRotation;
                    fitted[i].position = Project(DeguJoints[i]);
                }

                Matrix4x4[] inverseBinds = new Matrix4x4[fitted.Length];
                for (int i=0; i<fitted.Length; i++)
                    inverseBinds[i] = fitted[i].worldToLocalMatrix * skin.transform.localToWorldMatrix;
                model.bindposes = inverseBinds;
                model.RecalculateBounds();
                skin.bones = fitted;
                skin.rootBone = fitted[0];
                skin.sharedMesh = model;
                skin.localBounds = model.bounds;
                skin.updateWhenOffscreen = true;

                var driver = cloneRoot.AddComponent<DeguRigDriver>();
                driver.Configure(skin.transform, wolfBones, fitted);
                InstanceLog("Degu fitted rig v0.1.2: vertices=" + model.vertexCount +
                    ", animated joints=34, axes validated, source wolf Animator unchanged.");
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
                        // GLB triangles are CCW; Unity's front face is CW.
                        int a=br.ReadUInt16(),b=br.ReadUInt16(),c=br.ReadUInt16();
                        if (a>=vcount || b>=vcount || c>=vcount) throw new InvalidDataException("Invalid triangle index.");
                        triangles[i]=a;triangles[i+1]=c;triangles[i+2]=b;
                    }
                    if (stream.Position != stream.Length) throw new InvalidDataException("Model has trailing data.");
                    var mesh=new Mesh { name="Degu_Weighted_WolfRig" };
                    mesh.vertices=vertices;
                    mesh.uv=uv;
                    mesh.triangles=triangles;
                    mesh.RecalculateNormals();
                    mesh.RecalculateTangents();
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