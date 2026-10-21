using System;
using System.Linq;
using UnityEngine;
using DazPose.Performer.HandGrip;

namespace DazPose.UnityValidation.Partner
{
    /// <summary>
    /// Runtime owner for the prepared partner's anatomy state and authored rigid contact
    /// centerline. Run order is before SuccubusPerformer.Update so contact consumers see
    /// the current body state before grip planning/evaluation.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    [DisallowMultipleComponent]
    public sealed class PartnerBody : MonoBehaviour
    {
        private static readonly float[] ComplianceDistribution = { .15f, .30f, .40f, .15f };
        private const float ComplianceLimitDegrees = 5f;

        [SerializeField] private SkinnedMeshRenderer structuralBody;
        [SerializeField] private SkinnedMeshRenderer shellRenderer;
        [SerializeField] private Transform[] shaftBones = Array.Empty<Transform>();
        [SerializeField] private Transform mainPelvis;
        [SerializeField] private Transform donorPelvis;
        [SerializeField] private Transform[] distributedBones = Array.Empty<Transform>();
        [SerializeField] private Vector3[] flaccidCenterlineRoot = Array.Empty<Vector3>();
        [SerializeField] private Vector3[] erectCenterlineRoot = Array.Empty<Vector3>();
        private Vector3[] flaccidCenterlinePelvis = Array.Empty<Vector3>();
        private Vector3[] erectCenterlinePelvis = Array.Empty<Vector3>();
        [SerializeField] private float flaccidRadius = .020f;
        [SerializeField] private float erectRadius = .023f;
        [SerializeField, Range(0f, 1f)] private float erection01 = 1f;
        [SerializeField] private bool shellVisible = true;
        [SerializeField, Range(0f, 1f)] private float position01 = .5f;

        private Quaternion[] erectBaseRotations = Array.Empty<Quaternion>();
        private Vector3 donorMainLocalPosition;
        private Quaternion donorMainLocalRotation = Quaternion.identity;
        private float shaft1X;
        private float shaft4X;
        private float distributedBend;
        private Vector3 contactTargetRoot;
        private string validationError = "PartnerBody has not been configured.";
        private Vector3 previousNormalWorld;
        private bool hasPreviousNormal;
        private RendererCalibrationSnapshot structuralCalibration;
        private RendererCalibrationSnapshot shellCalibration;
        private Transform[] capturedShaftBones = Array.Empty<Transform>();

        private struct RendererCalibrationSnapshot
        {
            public bool captured;
            public Transform rendererTransform;
            public Transform parent;
            public Vector3 localPosition;
            public Quaternion localRotation;
            public Vector3 localScale;
            public Mesh mesh;
            public Transform[] bones;
        }

        public bool IsReady => string.IsNullOrEmpty(validationError);
        public string ValidationError => validationError;
        public SkinnedMeshRenderer StructuralBody => structuralBody;
        public SkinnedMeshRenderer ShellRenderer => shellRenderer;
        public Transform MainPelvis => mainPelvis;
        public Transform DonorPelvis => donorPelvis;
        public System.Collections.Generic.IReadOnlyList<Transform> ShaftBones => shaftBones;
        public Vector3 ContactTargetRoot => contactTargetRoot;
        public Vector3[] FlaccidCenterlineRoot => (Vector3[])flaccidCenterlineRoot.Clone();
        public Vector3[] ErectCenterlineRoot => (Vector3[])erectCenterlineRoot.Clone();
        public float FlaccidRadius => flaccidRadius;
        public float ErectRadius => erectRadius;
        public float Position01
        {
            get => position01;
            set { RequireFinite(value, nameof(Position01)); position01 = Mathf.Clamp01(value); UpdateContactTarget(); }
        }

        public float Erection01
        {
            get => erection01;
            set
            {
                RequireFinite(value, nameof(Erection01));
                erection01 = Mathf.Clamp01(value);
                ApplyErectionBlendShapes();
                UpdateContactTarget();
            }
        }

        public bool ShellVisible
        {
            get => shellVisible;
            set
            {
                shellVisible = value;
                if (shellRenderer != null) shellRenderer.enabled = value;
            }
        }

        public float ContactRadius => Mathf.Lerp(flaccidRadius, erectRadius, Erection01);
        public bool ShellFollowsBones => shellRenderer != null && shellRenderer.sharedMesh != null
            && shellRenderer.bones != null && shellRenderer.bones.Length > 0
            && shellRenderer.sharedMesh.bindposes.Length == shellRenderer.bones.Length;

        /// <summary>State eligibility only; the contact target must also validate its measured geometry profile.</summary>
        public bool IsContactSupported => IsReady && HasValidLiveCalibrationBindings(out _)
            && Mathf.Approximately(Erection01, 1f)
            && Mathf.Approximately(shaft1X, 0f) && Mathf.Approximately(shaft4X, 0f)
            && Mathf.Approximately(distributedBend, 0f) && HasSupportedScale();
        public bool IsRigidZeroCompliance => Mathf.Approximately(shaft1X, 0f)
            && Mathf.Approximately(shaft4X, 0f) && Mathf.Approximately(distributedBend, 0f);

        /// <summary>
        /// Side-effect-free serialized readiness for a contact declaration. This deliberately
        /// does not depend on Awake-captured calibration; runtime callers add live-calibration
        /// checks in the contact adapter.
        /// </summary>
        public bool TryValidateContactConfiguration(out string reason)
        {
            reason = null;
            if (structuralBody == null || structuralBody.sharedMesh == null)
                reason = "PartnerBody requires its prepared structural body renderer and mesh.";
            else if (!structuralBody.transform.IsChildOf(transform))
                reason = "PartnerBody structural renderer must belong to its runtime hierarchy.";
            else if (!HasRequiredBlendShape(structuralBody, "ErectionToFlaccid"))
                reason = "The structural body mesh must retain the authored ErectionToFlaccid blend shape.";
            else if (shellRenderer != null && shellRenderer.sharedMesh != null &&
                !HasRequiredBlendShape(shellRenderer, "ShellErectionToFlaccid"))
                reason = "The shell mesh must retain the authored ShellErectionToFlaccid blend shape.";
            else if (shellRenderer != null && !shellRenderer.transform.IsChildOf(transform))
                reason = "PartnerBody shell renderer must belong to its runtime hierarchy.";
            else if (shaftBones == null || shaftBones.Length < 2)
                reason = "PartnerBody requires the authored shaft bone chain.";
            else if (Array.Exists(shaftBones, bone => bone == null))
                reason = "PartnerBody's shaft bone chain contains an unbound bone.";
            else if (Array.Exists(shaftBones, bone => !bone.IsChildOf(transform)))
                reason = "PartnerBody shaft bones must belong to its runtime hierarchy.";
            else if (flaccidCenterlineRoot == null || erectCenterlineRoot == null ||
                flaccidCenterlineRoot.Length < 2 || flaccidCenterlineRoot.Length != erectCenterlineRoot.Length)
                reason = "PartnerBody requires matching flaccid and erect centerlines with at least two samples.";
            else if (!FinitePoints(flaccidCenterlineRoot) || !FinitePoints(erectCenterlineRoot))
                reason = "PartnerBody centerline samples must contain only finite root-space coordinates.";
            else if (!GripFrame.IsFinite(flaccidRadius) || !GripFrame.IsFinite(erectRadius) ||
                flaccidRadius <= 0f || erectRadius <= 0f)
                reason = "PartnerBody contact radii must be finite and positive meters.";
            else if ((mainPelvis == null) != (donorPelvis == null))
                reason = "PartnerBody donor following requires both pelvis transforms or neither.";
            else if (mainPelvis == donorPelvis)
                reason = "PartnerBody main and donor pelvis references must be distinct.";
            else if (mainPelvis != null && (!mainPelvis.IsChildOf(transform) || !donorPelvis.IsChildOf(transform)))
                reason = "PartnerBody pelvis bindings must belong to its runtime hierarchy.";
            else if (distributedBones != null && Array.Exists(distributedBones, bone => bone == null))
                reason = "PartnerBody distributed compliance bone list contains an unbound transform.";
            else if (distributedBones != null && distributedBones.Any(bone => Array.IndexOf(shaftBones, bone) < 0))
                reason = "Every distributed compliance bone must belong to the configured shaft chain.";
            else if (!HasSupportedScale())
                reason = "Partner contact requires positive unit scale on the actor root and all bound contact transforms.";
            else if (!Mathf.Approximately(Erection01, 1f))
                reason = "Partner contact is certified only at the authored erect endpoint (Erection01 = 1).";
            else if (!IsRigidZeroCompliance)
                reason = "Partner contact is certified only with procedural compliance set to zero.";
            else if (structuralBody != null && (!structuralBody.enabled || !structuralBody.gameObject.activeInHierarchy))
                reason = "Partner contact requires an enabled active structural body renderer.";
            else if (shellRenderer != null && (!shellRenderer.enabled || !shellRenderer.gameObject.activeInHierarchy))
                reason = "Partner contact requires an enabled active shell renderer when a shell is declared.";
            return reason == null;
        }

        private void Awake()
        {
            ValidateConfiguration();
            if (!IsReady) return;
            CaptureCalibrationBindings();
            CaptureBaseState();
            CapturePelvisCenterlines();
            ApplyErectionBlendShapes();
            if (shellRenderer != null) shellRenderer.enabled = shellVisible;
            UpdateContactTarget();
        }

        private void Update()
        {
            if (!IsReady) return;
            ApplyComplianceIfNeeded();
            FollowDonorPelvis();
        }

        private void OnValidate()
        {
            if (!Application.isPlaying) return;
            ValidateConfiguration();
            if (!IsReady) return;
            ApplyErectionBlendShapes();
            if (shellRenderer != null) shellRenderer.enabled = shellVisible;
        }

        private static void RequireFinite(float value, string parameter)
        {
            if (!GripFrame.IsFinite(value)) throw new ArgumentOutOfRangeException(parameter);
        }

        /// <summary>
        /// Migrates the old acceptance controller's authored references and state. This
        /// captures bind transforms only; it does not move the actor, camera, or pelvis.
        /// </summary>
        public bool Configure(SkinnedMeshRenderer body, SkinnedMeshRenderer shell,
            Transform[] bodyBones, Transform primaryPelvis, Transform attachedPelvis,
            Transform[] complianceBones, Vector3[] flaccidLineRoot, Vector3[] erectLineRoot,
            float flaccidContactRadius, float erectContactRadius, float initialErection01,
            bool initialShellVisible, out string error, float initialPosition01 = .5f)
        {
            RequireFinite(initialErection01, nameof(initialErection01));
            RequireFinite(initialPosition01, nameof(initialPosition01));
            structuralBody = body;
            shellRenderer = shell;
            shaftBones = bodyBones != null ? (Transform[])bodyBones.Clone() : Array.Empty<Transform>();
            mainPelvis = primaryPelvis;
            donorPelvis = attachedPelvis;
            distributedBones = complianceBones != null ? (Transform[])complianceBones.Clone() : Array.Empty<Transform>();
            flaccidCenterlineRoot = flaccidLineRoot != null ? (Vector3[])flaccidLineRoot.Clone() : Array.Empty<Vector3>();
            erectCenterlineRoot = erectLineRoot != null ? (Vector3[])erectLineRoot.Clone() : Array.Empty<Vector3>();
            flaccidRadius = flaccidContactRadius;
            erectRadius = erectContactRadius;
            erection01 = Mathf.Clamp01(initialErection01);
            shellVisible = initialShellVisible;
            position01 = Mathf.Clamp01(initialPosition01);
            shaft1X = shaft4X = distributedBend = 0f;
            CaptureBaseState();
            CapturePelvisCenterlines();
            ValidateConfiguration();
            error = validationError;
            if (IsReady)
            {
                CaptureCalibrationBindings();
                ApplyErectionBlendShapes();
                if (shellRenderer != null) shellRenderer.enabled = shellVisible;
                UpdateContactTarget();
            }
            return IsReady;
        }

        public void SetCompliance(float firstBoneXDegrees, float fourthBoneXDegrees,
            float distributedXDegrees)
        {
            RequireFinite(firstBoneXDegrees, nameof(firstBoneXDegrees));
            RequireFinite(fourthBoneXDegrees, nameof(fourthBoneXDegrees));
            RequireFinite(distributedXDegrees, nameof(distributedXDegrees));
            shaft1X = Mathf.Clamp(firstBoneXDegrees, -ComplianceLimitDegrees, ComplianceLimitDegrees);
            shaft4X = Mathf.Clamp(fourthBoneXDegrees, -ComplianceLimitDegrees, ComplianceLimitDegrees);
            distributedBend = Mathf.Clamp(distributedXDegrees, -ComplianceLimitDegrees, ComplianceLimitDegrees);
            ApplyComplianceIfNeeded();
        }

        public void RestoreCanonicalState()
        {
            SetCompliance(0f, 0f, 0f);
            Erection01 = 1f;
            ShellVisible = true;
        }

        /// <summary>Returns the authored center, tangent and radius in partner-root units.</summary>
        public bool TryGetContactFrameRoot(float position01, out Vector3 centerRoot,
            out Vector3 tangentRoot, out float radius)
        {
            centerRoot = default;
            tangentRoot = Vector3.forward;
            radius = ContactRadius;
            if (!IsReady || flaccidCenterlineRoot.Length < 2
                || erectCenterlineRoot.Length != flaccidCenterlineRoot.Length) return false;
            SampleCenterline(position01, out centerRoot, out tangentRoot);
            return GripFrame.IsFinite(centerRoot) && GripFrame.IsFinite(tangentRoot)
                && tangentRoot.sqrMagnitude > 1e-8f && GripFrame.IsFinite(radius) && radius > 0f;
        }

        /// <summary>World-meter contact frame. Unsupported anatomy or actor scale is rejected.</summary>
        public bool TryGetContactFrame(float position01, out GripFrame frame, out string reason)
        {
            frame = default;
            if (!IsReady)
            {
                reason = validationError;
                return false;
            }
            if (!HasValidLiveCalibrationBindings(out reason)) return false;
            if (!HasSupportedScale())
            {
                reason = "Partner contact requires positive unit scale on the actor root and pelvis so authored radii remain world meters.";
                return false;
            }
            if (!Mathf.Approximately(Erection01, 1f))
            {
                reason = "Partner contact is certified only at the authored erect endpoint (Erection01 = 1).";
                return false;
            }
            if (!Mathf.Approximately(shaft1X, 0f) || !Mathf.Approximately(shaft4X, 0f)
                || !Mathf.Approximately(distributedBend, 0f))
            {
                reason = "Partner contact is certified only with procedural compliance set to zero.";
                return false;
            }
            if (!TryGetContactFrameRoot(position01, out Vector3 centerRoot, out Vector3 tangentRoot,
                    out float radius))
            {
                reason = "The partner's authored contact centerline is invalid.";
                return false;
            }

            Vector3 centerWorld = transform.TransformPoint(centerRoot);
            Vector3 tangentWorld = transform.TransformDirection(tangentRoot).normalized;
            Vector3 referenceNormal = transform.up;
            if (hasPreviousNormal)
            {
                Vector3 candidate = previousNormalWorld - tangentWorld * Vector3.Dot(previousNormalWorld, tangentWorld);
                if (candidate.sqrMagnitude > 1e-8f) referenceNormal = candidate.normalized;
            }
            if (!GripFrame.TryCreate(centerWorld, tangentWorld, referenceNormal, radius, out frame))
            {
                reason = "A stable orthogonal world contact frame could not be formed from the authored centerline.";
                return false;
            }
            if (hasPreviousNormal && Vector3.Dot(frame.Normal, previousNormalWorld) < 0f
                && !GripFrame.TryCreate(centerWorld, tangentWorld, -referenceNormal, radius, out frame))
            {
                reason = "A stable orthogonal world contact frame could not be formed from the authored centerline.";
                return false;
            }
            previousNormalWorld = frame.Normal;
            hasPreviousNormal = true;
            reason = null;
            return true;
        }

        /// <summary>Explicit refresh hook for a future contact adapter before evaluating a frame.</summary>
        public bool RefreshBeforeContact(out string reason)
        {
            if (!IsReady) { reason = validationError; return false; }
            if (!HasValidLiveCalibrationBindings(out reason)) return false;
            ApplyComplianceIfNeeded();
            FollowDonorPelvis();
            reason = null;
            return true;
        }

        /// <summary>
        /// Verifies that the renderers and skinning bindings still match the geometry
        /// calibration captured during Awake or Configure. Actor-root and pelvis motion
        /// remain supported; renderer-local edits and rebinding invalidate contact.
        /// </summary>
        public bool HasValidLiveCalibrationBindings(out string reason)
        {
            reason = null;
            if (!structuralCalibration.captured || !shellCalibration.captured)
            {
                reason = "Partner contact calibration bindings have not been captured.";
                return false;
            }
            if (!MatchesCalibration(structuralBody, structuralCalibration, out reason)) return false;
            if (shellRenderer != null && !MatchesCalibration(shellRenderer, shellCalibration, out reason)) return false;
            if (shellRenderer == null && shellCalibration.rendererTransform != null)
            {
                reason = "The calibrated shell renderer binding was removed.";
                return false;
            }
            if (!SameTransforms(shaftBones, capturedShaftBones)
                || !AllBoundBonesInHierarchy(shaftBones))
            {
                reason = "PartnerBody shaft bone bindings changed or left the runtime hierarchy after calibration.";
                return false;
            }
            return true;
        }

        private void CaptureCalibrationBindings()
        {
            structuralCalibration = CaptureRenderer(structuralBody);
            shellCalibration = CaptureRenderer(shellRenderer);
            capturedShaftBones = shaftBones != null ? (Transform[])shaftBones.Clone() : Array.Empty<Transform>();
        }

        private RendererCalibrationSnapshot CaptureRenderer(SkinnedMeshRenderer renderer)
        {
            if (renderer == null) return new RendererCalibrationSnapshot { captured = true };
            Transform rendererTransform = renderer.transform;
            Transform[] bones = renderer.bones;
            return new RendererCalibrationSnapshot
            {
                captured = true,
                rendererTransform = rendererTransform,
                parent = rendererTransform.parent,
                localPosition = rendererTransform.localPosition,
                localRotation = rendererTransform.localRotation,
                localScale = rendererTransform.localScale,
                mesh = renderer.sharedMesh,
                bones = bones != null ? (Transform[])bones.Clone() : Array.Empty<Transform>()
            };
        }

        private bool MatchesCalibration(SkinnedMeshRenderer renderer,
            RendererCalibrationSnapshot captured, out string reason)
        {
            reason = null;
            if (renderer == null || !captured.captured || renderer.transform != captured.rendererTransform
                || renderer.sharedMesh != captured.mesh)
            {
                reason = "A calibrated partner renderer or mesh binding changed.";
                return false;
            }
            Transform rendererTransform = renderer.transform;
            if (rendererTransform.parent != captured.parent
                || !Approximately(rendererTransform.localPosition, captured.localPosition)
                || !Approximately(rendererTransform.localRotation, captured.localRotation)
                || !Approximately(rendererTransform.localScale, captured.localScale))
            {
                reason = "A calibrated partner renderer's local transform changed.";
                return false;
            }
            Transform[] liveBones = renderer.bones;
            if (!SameTransforms(liveBones, captured.bones) || !AllBoundBonesInHierarchy(liveBones))
            {
                reason = "A calibrated partner renderer's skinning bone bindings changed or left the runtime hierarchy.";
                return false;
            }
            return true;
        }

        private bool AllBoundBonesInHierarchy(Transform[] bones)
        {
            if (bones == null) return false;
            for (int i = 0; i < bones.Length; i++)
                if (bones[i] == null || !bones[i].IsChildOf(transform)) return false;
            return true;
        }

        private static bool SameTransforms(Transform[] current, Transform[] captured)
        {
            if (current == null || captured == null || current.Length != captured.Length) return false;
            for (int i = 0; i < current.Length; i++)
                if (current[i] != captured[i]) return false;
            return true;
        }

        private static bool Approximately(Vector3 a, Vector3 b) => (a - b).sqrMagnitude <= 1e-10f;
        private static bool Approximately(Quaternion a, Quaternion b) => Mathf.Abs(Quaternion.Dot(a, b)) >= .999999f;

        private void CaptureBaseState()
        {
            erectBaseRotations = new Quaternion[shaftBones.Length];
            for (int i = 0; i < shaftBones.Length; i++)
                erectBaseRotations[i] = shaftBones[i] != null ? shaftBones[i].localRotation : Quaternion.identity;
            if (mainPelvis != null && donorPelvis != null)
            {
                donorMainLocalPosition = mainPelvis.InverseTransformPoint(donorPelvis.position);
                donorMainLocalRotation = Quaternion.Inverse(mainPelvis.rotation) * donorPelvis.rotation;
            }
        }

        private void CapturePelvisCenterlines()
        {
            if (mainPelvis == null)
            {
                flaccidCenterlinePelvis = Array.Empty<Vector3>();
                erectCenterlinePelvis = Array.Empty<Vector3>();
                return;
            }
            flaccidCenterlinePelvis = new Vector3[flaccidCenterlineRoot.Length];
            erectCenterlinePelvis = new Vector3[erectCenterlineRoot.Length];
            for (int i = 0; i < flaccidCenterlineRoot.Length; i++)
            {
                flaccidCenterlinePelvis[i] = mainPelvis.InverseTransformPoint(transform.TransformPoint(flaccidCenterlineRoot[i]));
                erectCenterlinePelvis[i] = mainPelvis.InverseTransformPoint(transform.TransformPoint(erectCenterlineRoot[i]));
            }
        }

        private void ValidateConfiguration()
        {
            TryValidateContactConfiguration(out validationError);
        }

        private void ApplyErectionBlendShapes()
        {
            if (structuralBody != null && structuralBody.sharedMesh != null)
                structuralBody.SetBlendShapeWeight(structuralBody.sharedMesh.GetBlendShapeIndex("ErectionToFlaccid"),
                    BlendShapeWeightForErection(Erection01));
            if (shellRenderer != null && shellRenderer.sharedMesh != null)
                shellRenderer.SetBlendShapeWeight(shellRenderer.sharedMesh.GetBlendShapeIndex("ShellErectionToFlaccid"),
                    BlendShapeWeightForErection(Erection01));
        }

        private void ApplyComplianceIfNeeded()
        {
            if (erectBaseRotations == null || erectBaseRotations.Length != shaftBones.Length) return;
            bool enabled = Mathf.Approximately(Erection01, 1f)
                && (!ShellVisible || ShellFollowsBones);
            if (!enabled && shaft1X == 0f && shaft4X == 0f && distributedBend == 0f) return;
            for (int i = 0; i < shaftBones.Length; i++)
            {
                Transform bone = shaftBones[i];
                if (bone == null) continue;
                float x = enabled ? (i == 0 ? shaft1X : i == 3 ? shaft4X : 0f) : 0f;
                int distributedIndex = Array.IndexOf(distributedBones, bone);
                if (enabled && distributedIndex >= 0 && distributedIndex < ComplianceDistribution.Length)
                    x += distributedBend * ComplianceDistribution[distributedIndex];
                bone.localRotation = ApplyComplianceOffset(erectBaseRotations[i], new Vector3(x, 0f, 0f));
            }
        }

        private void FollowDonorPelvis()
        {
            if (mainPelvis == null || donorPelvis == null) return;
            donorPelvis.SetPositionAndRotation(mainPelvis.TransformPoint(donorMainLocalPosition),
                mainPelvis.rotation * donorMainLocalRotation);
        }

        private void UpdateContactTarget()
        {
            if (flaccidCenterlineRoot.Length == 0 || erectCenterlineRoot.Length != flaccidCenterlineRoot.Length) return;
            if (TryGetContactFrameRoot(Position01, out Vector3 center, out _, out _)) contactTargetRoot = center;
        }

        private void SampleCenterline(float position01, out Vector3 center, out Vector3 tangent)
        {
            int count = flaccidCenterlineRoot.Length;
            float scaled = Mathf.Clamp01(position01) * (count - 1);
            int index = Mathf.Min(count - 2, Mathf.FloorToInt(scaled));
            float t = scaled - index;
            Vector3[] flaccid = mainPelvis != null ? flaccidCenterlinePelvis : flaccidCenterlineRoot;
            Vector3[] erect = mainPelvis != null ? erectCenterlinePelvis : erectCenterlineRoot;
            Vector3 a = Vector3.Lerp(flaccid[index], erect[index], Erection01);
            Vector3 b = Vector3.Lerp(flaccid[index + 1], erect[index + 1], Erection01);
            Vector3 localCenter = Vector3.Lerp(a, b, t);
            Vector3 localTangent = (b - a).normalized;
            if (mainPelvis != null)
            {
                center = transform.InverseTransformPoint(mainPelvis.TransformPoint(localCenter));
                tangent = transform.InverseTransformDirection(mainPelvis.TransformDirection(localTangent)).normalized;
            }
            else { center = localCenter; tangent = localTangent; }
        }

        private bool HasSupportedScale()
        {
            Vector3 scale = transform.lossyScale;
            if (!IsPositiveUnitScale(scale) || !IsPositiveUnitScale(transform.localScale)
                || mainPelvis != null && !IsPositiveUnitScale(mainPelvis.lossyScale)
                || donorPelvis != null && !IsPositiveUnitScale(donorPelvis.lossyScale)
                || structuralBody != null && !IsPositiveUnitScale(structuralBody.transform.lossyScale)
                || shellRenderer != null && !IsPositiveUnitScale(shellRenderer.transform.lossyScale)) return false;
            foreach (Transform bone in shaftBones)
                if (bone == null || !IsPositiveUnitScale(bone.lossyScale)) return false;
            return true;
        }

        private static bool IsPositiveUnitScale(Vector3 scale) => scale.x > 0f && scale.y > 0f && scale.z > 0f
            && Mathf.Abs(scale.x - 1f) <= .001f && Mathf.Abs(scale.y - 1f) <= .001f
            && Mathf.Abs(scale.z - 1f) <= .001f;

        private static bool HasRequiredBlendShape(SkinnedMeshRenderer renderer, string name) =>
            renderer.sharedMesh.GetBlendShapeIndex(name) >= 0;

        private static bool FinitePoints(Vector3[] points)
        {
            for (int i = 0; i < points.Length; i++)
                if (!GripFrame.IsFinite(points[i])) return false;
            return true;
        }

        public static float BlendShapeWeightForErection(float value) => 100f * (1f - Mathf.Clamp01(value));
        public static Quaternion ApplyComplianceOffset(Quaternion erectBase, Vector3 degrees) =>
            erectBase * Quaternion.Euler(degrees);
    }
}
