using System;
using System.Collections.Generic;
using UnityEngine;

namespace SirrohDegu
{
    internal static class DeguAppearance
    {
        private static Material _fur;
        private static Material _eyes;

        private static Material CreateMaterial(string name, Color tint, bool eye)
        {
            // Never reuse the Wolf shader/material. Its material settings and
            // UV atlas do not match Degu and caused see-through blue fur and
            // the Wolf's glossy eye texture covering the Degu eyeballs.
            Shader shader = Shader.Find("Standard");
            if (shader == null) shader = Shader.Find("Legacy Shaders/Diffuse");
            if (shader == null)
                throw new InvalidOperationException("Unity opaque Standard/Diffuse shader not found");

            Material result = new Material(shader);
            result.name = name;
            result.SetOverrideTag("RenderType", "Opaque");
            result.renderQueue = 2000;
            result.DisableKeyword("_ALPHATEST_ON");
            result.DisableKeyword("_ALPHABLEND_ON");
            result.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            if (result.HasProperty("_Mode")) result.SetFloat("_Mode", 0f);
            if (result.HasProperty("_SrcBlend")) result.SetInt("_SrcBlend", 1);
            if (result.HasProperty("_DstBlend")) result.SetInt("_DstBlend", 0);
            if (result.HasProperty("_ZWrite")) result.SetInt("_ZWrite", 1);
            if (result.HasProperty("_Cull")) result.SetInt("_Cull", 2);
            if (result.HasProperty("_Metallic")) result.SetFloat("_Metallic", 0f);
            if (result.HasProperty("_Glossiness")) result.SetFloat("_Glossiness", eye ? .18f : .04f);
            if (result.HasProperty("_Color")) result.SetColor("_Color", tint);
            if (result.HasProperty("_BaseColor")) result.SetColor("_BaseColor", tint);
            return result;
        }

        private static Texture2D CreateFurTexture()
        {
            const int size = 64;
            Texture2D texture = new Texture2D(size,size,TextureFormat.RGBA32,true);
            texture.name = "Degu_WarmBrown_Procedural64";
            texture.wrapMode = TextureWrapMode.Repeat;
            texture.filterMode = FilterMode.Bilinear;
            Color32[] pixels = new Color32[size*size];
            for(int y=0;y<size;y++)
            for(int x=0;x<size;x++)
            {
                // Stable fine microvariation: no high-frequency white or black
                // speckles that would become visual noise at small screen sizes.
                uint n=(uint)(x*374761393 + y*668265263 + 0x1ae731);
                n=(n^(n>>13))*1274126177u;
                int d=(int)(n%13)-6;
                pixels[y*size+x] = new Color32((byte)(155+d),
                    (byte)(120+d),(byte)(87+d),255);
            }
            texture.SetPixels32(pixels);
            texture.Apply(true, true);
            return texture;
        }

        internal static void Apply(SkinnedMeshRenderer renderer, Mesh mesh)
        {
            if (_fur == null)
            {
                _fur = CreateMaterial("Sirroh_Degu_Fur_Opaque", Color.white, false);
                Texture2D tex=CreateFurTexture();
                if (_fur.HasProperty("_MainTex")) _fur.SetTexture("_MainTex", tex);
                if (_fur.HasProperty("_BaseMap")) _fur.SetTexture("_BaseMap", tex);
                _eyes = CreateMaterial("Sirroh_Degu_Eyes_Opaque",
                    new Color(.09f,.065f,.045f,1f), true);
            }

            BoneWeight[] bw=mesh.boneWeights;
            int[] original=mesh.triangles;
            var body=new List<int>(original.Length);
            var eyes=new List<int>(original.Length/2);
            for (int t=0;t<original.Length;t+=3)
            {
                int a=original[t],b=original[t+1],c=original[t+2];
                bool eye = IsEye(bw[a]) && IsEye(bw[b]) && IsEye(bw[c]);
                List<int> output = eye ? eyes : body;
                output.Add(a);output.Add(b);output.Add(c);
            }
            if (body.Count == 0 || eyes.Count == 0)
                throw new InvalidOperationException("Degu eye/body submesh classification failed");
            mesh.subMeshCount=2;
            mesh.SetTriangles(body.ToArray(),0);
            mesh.SetTriangles(eyes.ToArray(),1);
            renderer.sharedMaterials=new[] {_fur,_eyes};
            DeguPlugin.Log?.LogInfo("Degu material QA: opaque fur triangles=" +
                body.Count/3 + ", dark eyes triangles=" + eyes.Count/3);
        }

        private static bool IsEye(BoneWeight bw)
        {
            return (bw.boneIndex0 == 12 || bw.boneIndex0 == 13) && bw.weight0 > .9f;
        }
    }
}
