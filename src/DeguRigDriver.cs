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

        internal void Configure(Transform renderer, Transform[] original, Transform[] fitted)
        {
            if (renderer == null || original == null || fitted == null ||
                original.Length != 34 || fitted.Length != 34)
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

            // The original pelvis may have slight bobbing / animated offset.
            // Express that delta in renderer space and add it to the fitted
            // rest offset rather than copying its incompatible absolute position.
            fittedDegu[0].localPosition = fittedRootAtBind +
                (rendererRoot.InverseTransformPoint(originalWolf[0].position) -
                 originalRootAtBind);
            fittedDegu[0].rotation = originalWolf[0].rotation;

            for (int i=1; i<34; ++i)
                if (originalWolf[i] != null && fittedDegu[i] != null)
                    fittedDegu[i].localRotation = originalWolf[i].localRotation;
        }
    }
}
