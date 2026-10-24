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
        private Vector3[] originalLocalPositions;
        private Vector3[] fittedLocalPositions;
        private Quaternion[] initialOriginalRotations;
        private Quaternion[] initialFittedRotations;
        private float maxLegMotion;
        private float debugStarted;
        private bool loggedGait;

        // Keep each limb on the correct side after rotating the source Degu axes
        // into the right-handed Wolf basis.
        internal static Transform[] MapSourceBones(Transform[] wolf, bool swap)
        {
            if (!swap) return wolf;
            Transform[] result = (Transform[])wolf.Clone();
            int[] left = { 8, 10, 13, 14, 15, 16, 17, 22, 23, 24, 25 };
            int[] right = { 9, 11, 12, 18, 19, 20, 21, 26, 27, 28, 29 };
            for (int i = 0; i < left.Length; ++i)
            {
                result[left[i]] = wolf[right[i]];
                result[right[i]] = wolf[left[i]];
            }
            return result;
        }

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
            originalLocalPositions = new Vector3[original.Length];
            fittedLocalPositions = new Vector3[fitted.Length];
            initialOriginalRotations = new Quaternion[original.Length];
            initialFittedRotations = new Quaternion[fitted.Length];
            for (int i=0;i<fitted.Length;i++)
            {
                originalLocalPositions[i] = original[i].localPosition;
                fittedLocalPositions[i] = fitted[i].localPosition;
                initialOriginalRotations[i] = original[i].localRotation;
                initialFittedRotations[i] = fitted[i].localRotation;
            }
            debugStarted = Time.time;
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
            {
                if (originalWolf[i] == null || fittedDegu[i] == null) continue;
                // Delta-from-bind retargeting keeps Degu's fitted rest orientation.
                Quaternion delta = originalWolf[i].localRotation *
                                   Quaternion.Inverse(initialOriginalRotations[i]);
                fittedDegu[i].localRotation = delta * initialFittedRotations[i];

                // Some animations translate individual bones. Previously these
                // animated offsets were entirely ignored, freezing limbs driven
                // by translation rather than rotation.
                Vector3 move = originalWolf[i].localPosition - originalLocalPositions[i];
                fittedDegu[i].localPosition = fittedLocalPositions[i] + 0.40f * move;
            }

            if (!loggedGait)
            {
                float lFront = Quaternion.Angle(initialOriginalRotations[15], originalWolf[15].localRotation);
                float rFront = Quaternion.Angle(initialOriginalRotations[19], originalWolf[19].localRotation);
                float lRear = Quaternion.Angle(initialOriginalRotations[22], originalWolf[22].localRotation);
                float rRear = Quaternion.Angle(initialOriginalRotations[26], originalWolf[26].localRotation);
                maxLegMotion = Mathf.Max(maxLegMotion, lFront, rFront, lRear, rRear);
                if (Time.time - debugStarted > 10f)
                {
                    loggedGait = true;
                    DeguPlugin.Log?.LogInfo("Degu gait QA (first 10 seconds): max Wolf leg bone angle from bind=" +
                        maxLegMotion.ToString("F1") + "deg (zero means original Animator does not animate these joints).");
                }
            }
        }
    }
}
