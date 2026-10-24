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

        internal static void ReplaceMesh(SkinnedMeshRenderer skin)
        {
            Transform[] wolfBones = skin.bones;
            if (wolfBones == null || wolfBones.Length == 0) throw new InvalidOperationException("Wolf renderer has no bones.");
            var lookup = new Dictionary<string, Transform>(StringComparer.Ordinal);
            foreach (Transform t in wolfBones)
                if (t != null && !lookup.ContainsKey(t.name)) lookup.Add(t.name, t);
            // Some cosmetic bones can exist outside renderer.bones; locate all from the
            // original Wolf armature without changing its animator or hierarchy.
            foreach (Transform t in skin.transform.root.GetComponentsInChildren<Transform>(true))
                if (t != null && !lookup.ContainsKey(t.name)) lookup.Add(t.name, t);
            var bones = new Transform[Names.Length];
            for (int i = 0; i < Names.Length; ++i)
            {
                if (!lookup.TryGetValue(Names[i], out bones[i]) || bones[i] == null)
                    throw new InvalidOperationException("Wolf bone missing: " + Names[i]);
            }
            Mesh sourceWolf = skin.sharedMesh;
            if (sourceWolf == null) throw new InvalidOperationException("Wolf sharedMesh missing.");
            Mesh model = Load();
            try
            {
                // Uniformly fit Degu to original Wolf mesh's renderer-space
                // bounds. Keep Wolf transform, Animator, collider and skeleton.
                Bounds sourceBounds = model.bounds;
                Bounds targetBounds = sourceWolf.bounds;
                float ratio = targetBounds.size.magnitude / Math.Max(sourceBounds.size.magnitude, 0.001f);
                if (!!float.IsNaN(ratio) && !float.IsInfinity(ratio) || ratio <= 0f || ratio > 100f)
                    throw new InvalidDataException("Degu/Wolf size ratio invalid.");
                Vector3[] vertices = model.vertices;
                Vector3 offset = targetBounds.center - sourceBounds.center * ratio;
                for (int i=0; i<vertices.Length; i++) vertices[i] = vertices[i]*ratio + offset;
                model.vertices = vertices;

                // Bind to the actual unchanged Wolf rig. At bind pose each
                // bone/world -> renderer/local composition is identity.
                Matrix4x4[] bindposes = new Matrix4x4[bones.Length];
                for (int i=0; i<bones.Length; ++i)
                    bindposes[i] = bones[i].worldToLocalMatrix * skin.transform.localToWorldMatrix;
                model.bindposes = bindposes;
                model.RecalculateBounds();
                skin.bones = bones;
                skin.rootBone = bones[0];
                skin.sharedMesh = model;
                skin.localBounds = model.bounds;
                skin.updateWhenOffscreen = true;
                InstanceLog("Degu mesh replaced: " + model.vertexCount + " vertices, 34 Wolf bones.");
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
                    if (br.ReadByte()!='S' || br.ReadByte()!='D' || br.ReadByte()!='G' || br.ReadByte()!='2')
                        throw new InvalidDataException("Wrong model asset magic.");
                    int vcount=br.ReadUInt16(), icount=br.ReadUInt16(), bones=br.ReadByte();
                    if (vcount < 100 || vcount > 60000 || icount < 300 || icount > 65000 || icount%3!=0 || bones != Names.Length)
                        throw new InvalidDataException("Invalid model sizes.");
                    var vertices=new Vector3[vcount];
                    var normals=new Vector3[vcount];
                    var uv=new Vector2[vcount];
                    var weights=new BoneWeight[vcount];
                    for (int i=0;i<vcount;i++)
                    {
                        vertices[i]=new Vector3(br.ReadInt16()/16384f,br.ReadInt16()/16384f,br.ReadInt16()/16384f);
                        normals[i]=new Vector3(br.ReadSByte()/127f,br.ReadSByte()/127f,br.ReadSByte()/127f).normalized;
                        uv[i]=new Vector2(ReadHalf(br.ReadUInt16()),ReadHalf(br.ReadUInt16()));
                        int a=br.ReadByte(),b=br.ReadByte(),c=br.ReadByte(),d=br.ReadByte();
                        int wa=br.ReadByte(),wb=br.ReadByte(),wc=br.ReadByte(),wd=br.ReadByte();
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
                    mesh.normals=normals;
                    mesh.uv=uv;
                    mesh.triangles=triangles;
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