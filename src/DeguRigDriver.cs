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
        [SerializeField] private Transform originalVisualRoot;
        [SerializeField] private Transform actorRoot;
        [SerializeField] private Vector3 sourceVisualScaleAtBind;
        [SerializeField] private Vector3 stableVisualScaleAtBind;
        private bool warnedOriginalScale;
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
            originalVisualRoot = sourceRenderer;
            actorRoot = creatureRoot;
            sourceVisualScaleAtBind = sourceRenderer.localScale;
            stableVisualScaleAtBind = renderer.localScale;
            initialLimbRotations = new[] {
                original[15].localRotation, original[19].localRotation,
                original[22].localRotation, original[26].localRotation
            };
            debugStart = Time.time;
        }

        // Diagnostic only: prove whether the original Wolf Animator updates limb joints.
        [SerializeField] private Quaternion[] initialLimbRotations;
        private float debugStart;
        private float maxLimbAngle;
        private bool diagnosticComplete;
        private bool missingBoneWarning;

        // Configure() is called on the inactive prefab. Its NON-SERIALIZED
        // fields do not survive Unity Instantiate() into the live animal.
        // Initialize the runtime clone independently, instead of dereferencing
        // a null diagnostics array on every frame.
        private void Awake()
        {
            InitializeDiagnosticState();
        }

        private void InitializeDiagnosticState()
        {
            if (initialLimbRotations != null && initialLimbRotations.Length == 4)
                return;
            if (originalWolf == null || originalWolf.Length != 34) return;
            int[] indices = { 15, 19, 22, 26 };
            foreach (int index in indices)
                if (originalWolf[index] == null) return;
            initialLimbRotations = new[] {
                originalWolf[15].localRotation, originalWolf[19].localRotation,
                originalWolf[22].localRotation, originalWolf[26].localRotation
            };
            debugStart = Time.time;
            maxLimbAngle = 0f;
            diagnosticComplete = false;
        }

        private void LateUpdate()
        {
            if (rendererRoot == null || originalWolf == null || fittedDegu == null ||
                originalWolf.Length != 34 || fittedDegu.Length != 34)
                return;
            if (originalWolf[0] == null || fittedDegu[0] == null) return;

            // A live creature can outlive a transient Wolf visual transform.
            // Do not throw each frame if a reference disappears.
            if (initialLimbRotations == null || initialLimbRotations.Length != 4)
                InitializeDiagnosticState();

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

            // Report an actual sprint-driven source scale anomaly once.
            // No per-frame logging, bone overrides or extra physics work.
            if (!warnedOriginalScale && originalVisualRoot != null)
            {
                Vector3 now = originalVisualRoot.localScale;
                Vector3 expected = sourceVisualScaleAtBind;
                if ((now - expected).sqrMagnitude > .04f)
                {
                    warnedOriginalScale = true;
                    DeguPlugin.Log?.LogWarning(
                        "Degu sprint diagnostic: vanilla Wolf renderer localScale " +
                        now + " (at bind " + expected +
                        "); isolated Degu visual retains its own scale " +
                        stableVisualScaleAtBind + ".");
                }
            }
            fittedDegu[0].rotation = originalWolf[0].rotation;

            for (int i=1; i<34; ++i)
                if (originalWolf[i] != null && fittedDegu[i] != null)
                    fittedDegu[i].localRotation = originalWolf[i].localRotation;

            if (!diagnosticComplete && initialLimbRotations != null &&
                initialLimbRotations.Length == 4)
            {
                int[] limbs={15,19,22,26};
                for (int i=0; i<4; ++i)
                    if (originalWolf[limbs[i]] == null)
                    {
                        diagnosticComplete = true;
                        if (!missingBoneWarning)
                        {
                            missingBoneWarning = true;
                            DeguPlugin.Log?.LogWarning(
                                "Degu gait QA skipped: a Wolf animation bone is missing.");
                        }
                        return;
                    }
                for (int i=0; i<4; ++i)
                    maxLimbAngle = Mathf.Max(maxLimbAngle,
                        Quaternion.Angle(initialLimbRotations[i],originalWolf[limbs[i]].localRotation));
                if (Time.time-debugStart>=10f)
                {
                    diagnosticComplete=true;
                    DeguPlugin.Log?.LogInfo("Degu ANIM QA: maximum original Wolf limb motion in 10 seconds = "+
                        maxLimbAngle.ToString("F2")+" deg. No artificial toe lift applied.");
                }
            }
        }
    }
}
