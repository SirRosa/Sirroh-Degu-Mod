using System;
using System.Collections.Generic;
using UnityEngine;

namespace SirrohDegu
{
    // Restore a useful animated influence to anatomical limb regions.
    // Source Degu Face Sets omitted the thigh: v0.1.2 assigned only one
    // source vertex per thigh and barely any upper-arm vertices.
    // This pass only changes weights, never source vertex positions/topology.
    internal static class DeguWeightRepair
    {
        private static float Ramp(float min, float max, float x)
        {
            float t = Mathf.Clamp01((x - min) / (max - min));
            return t * t * (3f - 2f * t);
        }

        private static void AddBlend(Dictionary<int, float> influences, int bone, float factor)
        {
            factor = Mathf.Clamp01(factor);
            if (factor <= 0.001f) return;
            var keys = new List<int>(influences.Keys);
            foreach (int k in keys) influences[k] *= 1f - factor;
            if (influences.ContainsKey(bone)) influences[bone] += factor;
            else influences.Add(bone, factor);
        }

        internal static void Apply(Vector3[] vertices, BoneWeight[] weights)
        {
            if (vertices == null || weights == null || vertices.Length != weights.Length)
                throw new ArgumentException("Degu weight data length mismatch");
            int[] count = new int[34];
            int repaired = 0;
            for (int i = 0; i < vertices.Length; i++)
            {
                BoneWeight original = weights[i];
                var dict = new Dictionary<int, float>(5);
                Accumulate(dict, original.boneIndex0, original.weight0);
                Accumulate(dict, original.boneIndex1, original.weight1);
                Accumulate(dict, original.boneIndex2, original.weight2);
                Accumulate(dict, original.boneIndex3, original.weight3);
                // Separate eye spheres: retain their rigid eye weights.
                if (!((dict.ContainsKey(12) && dict[12] > .75f) ||
                      (dict.ContainsKey(13) && dict[13] > .75f)))
                {
                    Vector3 v = vertices[i];
                    float x = Mathf.Abs(v.x);
                    int side = v.x < 0f ? 0 : 1;
                    float thigh = Ramp(.035f, .075f, x) *
                        Ramp(-.43f, -.35f, v.z) *
                        (1f - Ramp(-.16f, -.08f, v.z)) *
                        Ramp(.025f, .095f, v.y) *
                        (1f - Ramp(.22f, .29f, v.y));
                    // The thighs are near the outer side of the haunch.
                    AddBlend(dict, side == 0 ? 22 : 26, thigh * .82f);

                    float calf = Ramp(.035f, .063f, x) *
                        Ramp(-.37f, -.31f, v.z) *
                        (1f - Ramp(-.16f, -.10f, v.z)) *
                        Ramp(.005f, .025f, v.y) *
                        (1f - Ramp(.09f, .15f, v.y));
                    AddBlend(dict, side == 0 ? 23 : 27, calf * .35f);

                    float shoulder = Ramp(.028f, .065f, x) *
                        Ramp(.05f, .10f, v.z) *
                        (1f - Ramp(.21f, .27f, v.z)) *
                        Ramp(.03f, .065f, v.y) *
                        (1f - Ramp(.15f, .22f, v.y));
                    AddBlend(dict, side == 0 ? 15 : 19, shoulder * .55f);

                    float forearm = Ramp(.025f, .055f, x) *
                        Ramp(.08f, .15f, v.z) *
                        (1f - Ramp(.22f, .29f, v.z)) *
                        Ramp(0f, .012f, v.y) *
                        (1f - Ramp(.085f, .15f, v.y));
                    AddBlend(dict, side == 0 ? 16 : 20, forearm * .25f);
                }

                // Keep at most 4 influences, in descending weight order.
                var sorted = new List<KeyValuePair<int, float>>(dict);
                sorted.Sort((a, b) => b.Value.CompareTo(a.Value));
                float sum = 0f;
                for (int k = 0; k < sorted.Count && k < 4; k++)
                    sum += sorted[k].Value;
                if (sum < .0001f) throw new InvalidOperationException("Degu vertex without skin weights");

                BoneWeight updated = new BoneWeight();
                for (int k = 0; k < 4 && k < sorted.Count; k++)
                {
                    var entry = sorted[k];
                    float val = entry.Value / sum;
                    if (val < .0001f) continue;
                    count[entry.Key]++;
                    switch (k)
                    {
                        case 0: updated.boneIndex0 = entry.Key; updated.weight0 = val; break;
                        case 1: updated.boneIndex1 = entry.Key; updated.weight1 = val; break;
                        case 2: updated.boneIndex2 = entry.Key; updated.weight2 = val; break;
                        case 3: updated.boneIndex3 = entry.Key; updated.weight3 = val; break;
                    }
                }
                if (Mathf.Abs(updated.weight0 + updated.weight1 +
                    updated.weight2 + updated.weight3 - 1f) > .001f)
                    throw new InvalidOperationException("Degu invalid weight normalization");
                if (updated.boneIndex0 != original.boneIndex0 ||
                    Mathf.Abs(updated.weight0 - original.weight0) > .01f) repaired++;
                weights[i] = updated;
            }
            if (count[22] < 3 || count[26] < 3 || count[15] < 5 || count[19] < 5)
                throw new InvalidOperationException("Degu limb groups still have insufficient coverage");
            DeguPlugin.Log?.LogInfo("Degu skin QA: corrected vertices=" + repaired +
                ", thigh L/R=" + count[22] + "/" + count[26] +
                ", upper arm L/R=" + count[15] + "/" + count[19]);
        }

        private static void Accumulate(Dictionary<int, float> table, int id, float weight)
        {
            if (id < 0 || id >= 34 || weight <= 0f) return;
            if (table.ContainsKey(id)) table[id] += weight;
            else table.Add(id, weight);
        }
    }
}
