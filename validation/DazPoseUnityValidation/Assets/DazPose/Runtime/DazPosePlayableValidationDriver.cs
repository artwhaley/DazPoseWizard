using System;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace DazPose.UnityValidation
{
    [Serializable]
    public sealed class DazPoseRuntimeExpectedTransform
    {
        public string path;
        public bool hasPosition;
        public bool hasRotation;
        public bool hasScale;
        public Vector3 localPosition;
        public Quaternion localRotation;
        public Vector3 localScale;
    }

    [DisallowMultipleComponent]
    [AddComponentMenu("DAZ Pose/Validation Only/Playable Clip Smoke Test")]
    [RequireComponent(typeof(Animator))]
    public sealed class DazPosePlayableValidationDriver : MonoBehaviour
    {
        private const float PositionToleranceMeters = 1e-5f;
        private const float RotationToleranceDegrees = 0.001f;
        private const float ScaleTolerance = 1e-5f;

        public AnimationClip clip;
        public DazPoseRuntimeExpectedTransform[] expectedTransforms = Array.Empty<DazPoseRuntimeExpectedTransform>();
        public bool validationAddedAnimator;
        public Animator validationOwnedAnimator;
        public string validationSmokeTestOwner;
        [NonSerialized] public bool validationComplete;
        [NonSerialized] public bool validationPassed;

        private Animator _animator;
        private PlayableGraph _graph;
        private bool _graphReady;

        private void OnEnable()
        {
            if (clip == null)
            {
                Debug.LogError("DAZ Pose validation-only Playables driver has no AnimationClip assigned.", this);
                validationComplete = true;
                return;
            }

            _animator = GetComponent<Animator>();
            if (_animator == null)
            {
                Debug.LogError("DAZ Pose validation-only Playables driver requires an Animator on the Genesis8Female animation root.", this);
                validationComplete = true;
                return;
            }

            try
            {
                _graph = PlayableGraph.Create("DAZ Pose validation-only Playables smoke test");
                _graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                var playable = AnimationClipPlayable.Create(_graph, clip);
                playable.SetApplyFootIK(false);
                playable.SetTime(0.5);
                playable.SetSpeed(0);
                var output = AnimationPlayableOutput.Create(_graph, "DAZ Pose validation", _animator);
                output.SetSourcePlayable(playable);
                _graph.Play();
                _graphReady = true;
                Debug.Log("DAZ Pose runtime smoke test is sampling '" + clip.name + "' at 0.5 sec on " + transform.name + ". The pose is held while Play Mode remains active.", this);
            }
            catch (Exception exception)
            {
                Debug.LogError("DAZ Pose runtime Playables setup failed: " + exception, this);
                validationComplete = true;
            }
        }

        private void LateUpdate()
        {
            if (!_graphReady || validationComplete) return;
            _graph.Evaluate(0f);
            ValidateSample();
        }

        private void ValidateSample()
        {
            var errors = new System.Collections.Generic.List<string>();
            var maxPosition = 0f;
            var maxRotation = 0f;
            var maxScale = 0f;
            foreach (var expected in expectedTransforms)
            {
                if (expected == null) continue;
                var actual = string.IsNullOrEmpty(expected.path) ? transform : transform.Find(expected.path);
                if (actual == null)
                {
                    errors.Add("Missing transform path '" + expected.path + "'.");
                    continue;
                }
                if (expected.hasPosition)
                {
                    var error = Vector3.Distance(expected.localPosition, actual.localPosition);
                    maxPosition = Mathf.Max(maxPosition, error);
                    if (error > PositionToleranceMeters) errors.Add(expected.path + " position error " + error.ToString("G6") + " m.");
                }
                if (expected.hasRotation)
                {
                    var error = Quaternion.Angle(expected.localRotation, actual.localRotation);
                    maxRotation = Mathf.Max(maxRotation, error);
                    if (error > RotationToleranceDegrees) errors.Add(expected.path + " rotation error " + error.ToString("G6") + " deg.");
                }
                if (expected.hasScale)
                {
                    var error = Vector3.Distance(expected.localScale, actual.localScale);
                    maxScale = Mathf.Max(maxScale, error);
                    if (error > ScaleTolerance) errors.Add(expected.path + " scale error " + error.ToString("G6") + ".");
                }
            }

            validationComplete = true;
            validationPassed = expectedTransforms.Length > 0 && errors.Count == 0;
            if (validationPassed)
                Debug.Log("PASS DAZ Pose runtime smoke test: Animator/Playables drove " + expectedTransforms.Length
                    + " pose transforms from '" + clip.name + "' at 0.5 sec; max errors rotation=" + maxRotation.ToString("G6")
                    + " deg, position=" + maxPosition.ToString("G6") + " m, scale=" + maxScale.ToString("G6") + ". The static clip remains active in Play Mode.", this);
            else
                Debug.LogError("FAIL DAZ Pose runtime smoke test for '" + clip.name + "'. "
                    + (expectedTransforms.Length == 0 ? "No animated transforms were inspected." : string.Join(" ", errors)), this);
        }

        private void OnDisable()
        {
            if (_graph.IsValid()) _graph.Destroy();
            _graphReady = false;
        }
    }
}
