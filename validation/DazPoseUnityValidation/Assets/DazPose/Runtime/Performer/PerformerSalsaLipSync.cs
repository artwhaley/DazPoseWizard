using System;
using System.Linq;
using CrazyMinnow.SALSA;
using UnityEngine;

namespace DazPose.Performer
{
    /// <summary>
    /// Narrow runtime validator and lifecycle guard for the editor-authored SALSA setup.
    /// Playback remains owned exclusively by SuccubusPerformer / PerformerSpeech.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Performer/SALSA Lip Sync Integration")]
    public sealed class PerformerSalsaLipSync : MonoBehaviour
    {
        [SerializeField] private SuccubusPerformer performer;
        [SerializeField] private Salsa salsa;
        [SerializeField] private QueueProcessor queueProcessor;

        private PerformerLipSyncMorphBinding[] _bindings = Array.Empty<PerformerLipSyncMorphBinding>();
        private bool _configurationValid;
        private bool _failureReported;
        private bool _salsaWasEnabled;
        private bool _salsaDisabledCleanupApplied;
        private bool _restoreSalsaOnEnable;
        private bool _savedConfigReady;
        private string _status = "Waiting for runtime validation.";

        public bool IsReady => _configurationValid && salsa != null && salsa.isActiveAndEnabled
            && queueProcessor != null && queueProcessor.isActiveAndEnabled;
        public bool UsesSpeechAudioSource => performer != null && salsa != null
            && performer.SpeechAudioSource != null && salsa.audioSrc == performer.SpeechAudioSource;
        public string Status => IsReady ? "Ready" : _status;
        public bool IsSALSAing => IsReady && salsa.IsSALSAing;
        public float AnalysisValue => salsa == null ? 0f : salsa.analysisValue;
        public string WeightSummary
        {
            get
            {
                if (_bindings == null || _bindings.Length == 0) return "unresolved";
                var peak = 0f;
                var peakViseme = "none";
                var limit = 0f;
                foreach (var binding in _bindings)
                {
                    var mesh = binding.Renderer == null ? null : binding.Renderer.sharedMesh;
                    if (mesh == null || binding.Index < 0 || binding.Index >= mesh.blendShapeCount
                        || !string.Equals(mesh.GetBlendShapeName(binding.Index), binding.Definition.BlendShapeName,
                            StringComparison.Ordinal)) continue;
                    var weight = Mathf.Abs(binding.Renderer.GetBlendShapeWeight(binding.Index)
                        - binding.Definition.RestUnityWeight);
                    if (weight > peak)
                    {
                        peak = weight;
                        peakViseme = binding.Definition.Viseme;
                    }
                    limit = Mathf.Max(limit, binding.Definition.MaximumUnityWeight);
                }
                return "peak " + peak.ToString("F1") + "/" + limit.ToString("F1") + " Unity weight (" + peakViseme + ")";
            }
        }
        public float AudioUpdateDelay => salsa == null ? 0.0875f : salsa.audioUpdateDelay;
        public int TriggeredIndex => salsa == null ? -1 : salsa.TriggeredIndex;
        public int VisemeCount => salsa == null || salsa.visemes == null ? 0 : salsa.visemes.Count;
        public string CurrentViseme
        {
            get
            {
                var index = TriggeredIndex;
                if (salsa == null || salsa.visemes == null || index < 0 || index >= salsa.visemes.Count
                    || salsa.visemes[index] == null || salsa.visemes[index].expData == null) return "none";
                return salsa.visemes[index].expData.name;
            }
        }
        public string BindingSummary
        {
            get
            {
                if (_bindings == null || _bindings.Length == 0) return "unresolved";
                return string.Join(", ", _bindings.Select(binding => binding.Definition.Viseme + "="
                    + binding.Index + ":" + (binding.Renderer == null || binding.Renderer.sharedMesh == null
                        || binding.Index < 0 || binding.Index >= binding.Renderer.sharedMesh.blendShapeCount
                        ? "<missing>" : binding.Renderer.sharedMesh.GetBlendShapeName(binding.Index))));
            }
        }

        private void Reset()
        {
            if (performer == null) performer = GetComponent<SuccubusPerformer>();
            if (salsa == null) salsa = GetComponent<Salsa>();
            if (queueProcessor == null) queueProcessor = GetComponent<QueueProcessor>();
        }

        private void OnEnable()
        {
            if (!Application.isPlaying) return;
            if (_restoreSalsaOnEnable && salsa != null)
            {
                salsa.configReady = _savedConfigReady;
                salsa.enabled = true;
                _restoreSalsaOnEnable = false;
            }
            ValidateRuntimeSetup();
        }

        private void Start()
        {
            if (Application.isPlaying && !_configurationValid) ValidateRuntimeSetup();
        }

        private void LateUpdate()
        {
            if (!Application.isPlaying) return;
            if (salsa == null || !salsa.isActiveAndEnabled)
            {
                if (!_salsaDisabledCleanupApplied)
                {
                    SetOwnedWeightsToRest();
                    _salsaDisabledCleanupApplied = true;
                }
                if (_configurationValid)
                    _status = salsa == null ? "SALSA reference is missing." : "SALSA is disabled; speech playback remains available.";
                _salsaWasEnabled = false;
                return;
            }

            _salsaDisabledCleanupApplied = false;
            if (!_salsaWasEnabled)
            {
                _salsaWasEnabled = true;
                ValidateRuntimeSetup();
            }

            if (!_configurationValid) return;
            if (performer == null || salsa.audioSrc != performer.SpeechAudioSource
                || salsa.queueProcessor != queueProcessor || !salsa.configReady
                || salsa.useExternalAnalysis || salsa.usePersistence
                || performer.SpeechAudioSource == null || !performer.SpeechAudioSource.isActiveAndEnabled)
            {
                FailRuntimeSetup("SALSA must keep using the performer's active SpeechAudioSource and root QueueProcessor with ready, internal, non-persistent analysis.");
                return;
            }
            if (queueProcessor == null || !queueProcessor.isActiveAndEnabled)
            {
                FailRuntimeSetup("The SALSA QueueProcessor must remain enabled while the character is active.");
                return;
            }

            // Unity meshes are immutable during ordinary playback, but guard against hot-swapping
            // the renderer or mesh so a stale resolved index is never silently animated.
            for (var index = 0; index < _bindings.Length; index++)
            {
                var binding = _bindings[index];
                var mesh = binding.Renderer == null ? null : binding.Renderer.sharedMesh;
                if (mesh == null || binding.Index < 0 || binding.Index >= mesh.blendShapeCount
                    || !string.Equals(mesh.GetBlendShapeName(binding.Index), binding.Definition.BlendShapeName,
                        StringComparison.Ordinal))
                {
                    FailRuntimeSetup("Resolved speech binding changed at '" + PerformerLipSyncMorphCatalog.RendererPath
                        + "' for '" + binding.Definition.BlendShapeName + "'. Run the editor setup again after refreshing the Lara FBX.");
                    return;
                }

                var currentWeight = Mathf.Abs(binding.Renderer.GetBlendShapeWeight(binding.Index)
                    - binding.Definition.RestUnityWeight);
                if (currentWeight > binding.Definition.MaximumUnityWeight + 1f)
                {
                    FailRuntimeSetup("Speech viseme " + binding.Definition.Viseme + " reached Unity weight "
                        + currentWeight.ToString("F2") + ", above its configured ceiling of "
                        + binding.Definition.MaximumUnityWeight.ToString("F2") + ". SALSA was stopped and the owned shape was reset.");
                    return;
                }
            }
        }

        private void OnDisable()
        {
            if (Application.isPlaying && gameObject.activeInHierarchy && _configurationValid
                && salsa != null && salsa.enabled)
            {
                // Turning this narrow bridge off also stops its only SALSA integration. Clearing
                // configReady first makes SALSA.OnDisable skip its module-wide viseme teardown.
                _savedConfigReady = salsa.configReady;
                salsa.configReady = false;
                salsa.enabled = false;
                _restoreSalsaOnEnable = true;
            }
            // SALSA's own OnDisable handles normal release when the complete character is
            // deactivated. This exact-channel cleanup also covers callback ordering and never
            // touches Expression, breath, blink, gaze, or authored face state.
            SetOwnedWeightsToRest();
            _salsaDisabledCleanupApplied = true;
        }

        public bool ValidateConfiguration(out string failure)
        {
            failure = null;
            if (performer == null || performer.gameObject != gameObject)
                return FailValidation("PerformerSalsaLipSync must sit beside SuccubusPerformer on the performer root.", out failure);

            var animator = performer.GetComponent<Animator>();
            var source = performer.SpeechAudioSource;
            if (animator == null || source == null)
                return FailValidation("The performer Animator or serialized SpeechAudioSource is missing.", out failure);
            var headBones = animator.GetComponentsInChildren<Transform>(true)
                .Where(item => string.Equals(item.name, "head", StringComparison.Ordinal)).ToArray();
            if (headBones.Length != 1 || source.transform.parent != headBones[0] || !source.isActiveAndEnabled)
                return FailValidation("SpeechAudioSource must remain active and enabled directly beneath the performer's unique Animator head bone.", out failure);

            if (!PerformerLipSyncMorphCatalog.TryResolveBindings(animator, out var bindings, out failure))
                return false;
            // Retain exact resolved handles even when a later validation fails so the
            // fail-closed path can return those owned shapes to their known rest weights.
            _bindings = bindings;

            var allSalsa = performer.GetComponentsInChildren<Salsa>(true);
            var allQueues = performer.GetComponentsInChildren<QueueProcessor>(true);
            if (allSalsa.Length != 1 || allSalsa[0] != salsa || salsa == null || salsa.gameObject != gameObject)
                return FailValidation("Expected exactly one Salsa on the performer root and the bridge must reference it.", out failure);
            if (allQueues.Length != 1 || allQueues[0] != queueProcessor || queueProcessor == null
                || queueProcessor.gameObject != gameObject)
                return FailValidation("Expected exactly one QueueProcessor on the performer root and the bridge must reference it.", out failure);
            if (performer.GetComponentsInChildren<Emoter>(true).Length != 0 || salsa.emoter != null)
                return FailValidation("P0.9B does not use EmoteR; remove it from the configured performer.", out failure);
            if (performer.GetComponentsInChildren<Eyes>(true).Length != 0)
                return FailValidation("P0.9B does not use SALSA Eyes; remove it from the configured performer.", out failure);
            if (!salsa.configReady || salsa.useExternalAnalysis || salsa.usePersistence || salsa.audioSrc != source
                || salsa.queueProcessor != queueProcessor)
                return FailValidation("SALSA must be ready, use internal non-persistent analysis, observe the exact SpeechAudioSource, and reference the root QueueProcessor.", out failure);
            if (salsa.visemes == null || salsa.visemes.Count != PerformerLipSyncMorphCatalog.Definitions.Count)
                return FailValidation("SALSA must contain exactly the eight ordered, catalog-owned speech visemes.", out failure);

            for (var index = 0; index < bindings.Length; index++)
            {
                var binding = bindings[index];
                var definition = binding.Definition;
                var viseme = salsa.visemes[index];
                if (viseme == null || viseme.expData == null
                    || !string.Equals(viseme.expData.name, definition.ControllerName, StringComparison.Ordinal))
                    return FailValidation("SALSA viseme order/name mismatch at catalog entry " + definition.Viseme + ".", out failure);
                if (viseme.expData.components == null || viseme.expData.controllerVars == null
                    || viseme.expData.components.Count != 1 || viseme.expData.controllerVars.Count != 1)
                    return FailValidation("Viseme " + definition.Viseme + " must contain one complete Shape controller.", out failure);

                var component = viseme.expData.components[0];
                var helper = viseme.expData.controllerVars[0];
                if (component == null || component.controlType != ExpressionComponent.ControlType.Shape
                    || helper == null || helper.smr != binding.Renderer || helper.blendIndex != binding.Index
                    || !Approximately(helper.minShape, definition.RestSalsaAmount)
                    || !Approximately(helper.maxShape, definition.MaximumSalsaAmount)
                    || !Approximately(component.durationOn, definition.DurationOn)
                    || !Approximately(component.durationHold, definition.DurationHold)
                    || !Approximately(component.durationOff, definition.DurationOff))
                    return FailValidation("Viseme " + definition.Viseme + " has an incomplete or mismatched Shape controller.", out failure);
            }

            failure = null;
            return true;
        }

#if UNITY_EDITOR
        public void SetEditorReferences(SuccubusPerformer nextPerformer, Salsa nextSalsa, QueueProcessor nextQueueProcessor)
        {
            performer = nextPerformer;
            salsa = nextSalsa;
            queueProcessor = nextQueueProcessor;
        }
#endif

        private void ValidateRuntimeSetup()
        {
            if (ValidateConfiguration(out var failure))
            {
                _configurationValid = true;
                _failureReported = false;
                _salsaWasEnabled = salsa != null && salsa.isActiveAndEnabled;
                _status = "Ready";
                return;
            }
            FailRuntimeSetup(failure);
        }

        private void FailRuntimeSetup(string failure)
        {
            _configurationValid = false;
            _status = string.IsNullOrEmpty(failure) ? "Lip-sync configuration is invalid." : failure;
            SetOwnedWeightsToRest();
            _salsaDisabledCleanupApplied = true;
            if (salsa != null && salsa.enabled)
            {
                // SALSA.OnDisable checks configReady before doing module-wide cleanup. Clearing
                // that flag first prevents a configuration error from touching unrelated SALSA data.
                salsa.configReady = false;
                salsa.enabled = false;
            }
            if (_failureReported) return;
            _failureReported = true;
            Debug.LogError("P0.9B realtime lip sync is unavailable: " + _status
                + " PerformerSpeech playback is unaffected. Run Tools > DAZ Pose > Development > Setup or Refresh Performer SALSA Lip Sync after correcting the setup.", this);
        }

        private void SetOwnedWeightsToRest()
        {
            if (_bindings == null) return;
            foreach (var binding in _bindings)
            {
                var mesh = binding.Renderer == null ? null : binding.Renderer.sharedMesh;
                if (mesh == null || binding.Index < 0 || binding.Index >= mesh.blendShapeCount
                    || !string.Equals(mesh.GetBlendShapeName(binding.Index), binding.Definition.BlendShapeName,
                        StringComparison.Ordinal)) continue;
                binding.Renderer.SetBlendShapeWeight(binding.Index, binding.Definition.RestUnityWeight);
            }
        }

        private static bool FailValidation(string message, out string failure)
        {
            failure = message;
            return false;
        }

        private static bool Approximately(float left, float right) => Mathf.Abs(left - right) <= 0.001f;
    }
}
