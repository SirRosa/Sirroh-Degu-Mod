using System;
using UnityEngine;

namespace SirrohDegu
{
    // Copy local Wolf Animator rotations after the vanilla Animator updates.
    // The independent Degu armature keeps anatomically fitted BONE POSITIONS,
    // so rotations do not stretch the mesh toward Wolf's incompatible joints.
    [DefaultExecutionOrder(10000)]
    public sealed class DeguRigDriver : MonoBehaviour
    {
        [SerializeField] private Transform rendererRoot;
        [SerializeField] private Transform[] originalWolf;
        [SerializeField] private Transform[] fittedDegu;
        [SerializeField] private Vector3 originalRootAtBind;
        [SerializeField] private Vector3 fittedRootAtBind;
        private bool warnedExtremeRootMotion;

        internal void Configure(Transform renderer, Transform[] original,
            Transform[] fitted, Transform sourceRenderer, Transform creatureRoot)
        {
            if (renderer == null || original == null || fitted == null ||
                original.Length != 34 || fitted.Length != 34 ||
                sourceRenderer == null || creatureRoot == null)
                throw new ArgumentException("Degu needs 34 original and fitted bones.");
            rendererRoot = renderer;
            originalWolf = original;
            fittedDegu = fitted;
            originalRootAtBind = renderer.InverseTransformPoint(original[0].position);
            fittedRootAtBind = fitted[0].localPosition;
        }

        private void LateUpdate()
        {
            if (rendererRoot == null || originalWolf == null || fittedDegu == null ||
                originalWolf.Length != 34 || fittedDegu.Length != 34)
                return;
            if (originalWolf[0] == null || fittedDegu[0] == null) return;

            // A sprint animation may contain a Wolf-model root translation
            // or scale curve that is incompatible with the Degu proportions.
            // Retain natural pelvis bobbing but NEVER copy a very large
            // displacement that can pull the entire Degu beneath the floor.
            Vector3 pelvisDelta = rendererRoot.InverseTransformPoint(
                originalWolf[0].position) - originalRootAtBind;
            if (pelvisDelta.sqrMagnitude > .24f * .24f)
            {
                if (!warnedExtremeRootMotion)
                {
                    warnedExtremeRootMotion = true;
                    DeguPlugin.Log?.LogWarning(
                        "Degu sprint diagnostic: source Wolf pelvis offset " +
                        pelvisDelta.magnitude.ToString("F3") +
                        "m exceeds safe Degu root translation; limiting to 0.24m.");
                }
                pelvisDelta = Vector3.ClampMagnitude(pelvisDelta, .24f);
            }
            fittedDegu[0].localPosition = fittedRootAtBind + pelvisDelta;

            fittedDegu[0].rotation = originalWolf[0].rotation;

            for (int i=1; i<34; ++i)
                if (originalWolf[i] != null && fittedDegu[i] != null)
                    fittedDegu[i].localRotation = originalWolf[i].localRotation;


        }
    }
}
