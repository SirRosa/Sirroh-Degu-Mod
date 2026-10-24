using UnityEngine;

namespace SirrohDegu
{
    // The existing wolfMat shader and ORIGINAL in-game textures are retained.
    // PropertyBlock overrides only the eye submesh, not the coat and not
    // any shared Wolf material. OnEnable executes on each live clone.
    public sealed class DeguEyeEmission : MonoBehaviour
    {
        [SerializeField] private SkinnedMeshRenderer eyeRenderer;
        private bool warned;

        internal void Configure(SkinnedMeshRenderer renderer)
        {
            eyeRenderer = renderer;
        }

        private void OnEnable()
        {
            if (eyeRenderer == null) return;
            Material[] materials = eyeRenderer.sharedMaterials;
            if (materials == null || materials.Length < 2 || materials[1] == null)
                return;
            Material material = materials[1];
            // Use the original Wolf's emission atlas if it exists, keeping
            // its texture and shader untouched for every other creature.
            MaterialPropertyBlock props = new MaterialPropertyBlock();
            eyeRenderer.GetPropertyBlock(props, 1);
            if (material.HasProperty("_EmissionMap"))
            {
                Texture emission = material.GetTexture("_EmissionMap");
                if (emission != null) props.SetTexture("_EmissionMap", emission);
            }
            if (material.HasProperty("_EmissionColor"))
            {
                props.SetColor("_EmissionColor", new Color(.12f, .16f, .21f, 1f));
            }
            else if (!warned)
            {
                warned = true;
                DeguPlugin.Log?.LogWarning("wolfMat lacks _EmissionColor; eye glow requires shader support.");
            }
            // Custom/Creature may use either parameter in different variants.
            if (material.HasProperty("_EmissionIntensity"))
                props.SetFloat("_EmissionIntensity", .20f);
            eyeRenderer.SetPropertyBlock(props, 1);
        }
    }
}
