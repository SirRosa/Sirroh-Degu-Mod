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
        [SerializeField] private Transform actorRoot;
        [SerializeField] private Vector3[] pawContacts;
        private float maxPawCorrection;

        internal void Configure(Transform renderer, Transform[] original,
            Transform[] fitted, Transform creature, Vector3[] contacts)
        {
            if (renderer == null || original == null || fitted == null ||
                original.Length != 34 || fitted.Length != 34 ||
                creature == null || contacts == null || contacts.Length != 4)
                throw new ArgumentException("Degu needs 34 original and fitted bones.");
            rendererRoot = renderer;
            originalWolf = original;
            fittedDegu = fitted;
            originalRootAtBind = renderer.InverseTransformPoint(original[0].position);
            fittedRootAtBind = fitted[0].localPosition;
            actorRoot = creature;
            pawContacts = (Vector3[])contacts.Clone();
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

            // Clamp four animated toes to the actor's collision-ground reference.
            // Correct only the terminal paw bone, leaving vanilla animation
            // chains untouched. No raycasts and no mesh CPU skinning in Update.
            if (actorRoot != null && pawContacts != null && pawContacts.Length == 4)
            {
                const float clearance = .018f;
                float floorY = actorRoot.position.y + clearance;
                int[] pawIndices = { 17, 21, 25, 29 };
                for (int p=0;p<4;p++)
                {
                    Transform toe = fittedDegu[pawIndices[p]];
                    if (toe == null) continue;
                    float lowestY = toe.TransformPoint(pawContacts[p]).y;
                    float raise = Mathf.Clamp(floorY - lowestY, 0f, .22f);
                    if (raise > .0005f)
                    {
                        toe.position += Vector3.up * raise;
                        maxPawCorrection = Mathf.Max(maxPawCorrection, raise);
                    }
                }
            }

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
                        maxLimbAngle.ToString("F2")+" deg; maximum paw correction="+
                        maxPawCorrection.ToString("F3")+"m. Test while Degu walks.");
                }
            }
        }
    }
}
