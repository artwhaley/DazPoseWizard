using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CrazyMinnow.SALSA;
using DazPose.Performer;
using DazPose.UnityValidation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DazPose.Editor.Importing
{
    internal static class PerformerSalsaConfigurator
    {
        private const string SetupMenu = "Tools/DAZ Pose/Development/Setup or Refresh Performer SALSA Lip Sync";
        private const string AuditMenu = "Tools/DAZ Pose/Development/Audit Performer SALSA Lip Sync";

        private sealed class SetupPlan
        {
            public SuccubusPerformer Performer;
            public Animator Animator;
            public AudioSource SpeechAudioSource;
            public Salsa Salsa;
            public QueueProcessor QueueProcessor;
            public PerformerSalsaLipSync Bridge;
            public PerformerLipSyncMorphBinding[] Bindings;
            public List<LipsyncExpression> Visemes;
            public string SpeechAudioPath;
        }

        [MenuItem(SetupMenu)]
        private static void SetupOrRefreshFromMenu()
        {
            if (!TryGetTargetPerformer(out var performer, out var targetFailure))
            {
                EditorUtility.DisplayDialog("SALSA lip-sync setup", targetFailure, "Close");
                return;
            }

            if (!TryBuildPlan(performer, out var plan, out var failure))
            {
                EditorUtility.DisplayDialog("SALSA lip-sync setup", failure, "Close");
                Debug.LogError("P0.9B SALSA setup was not applied: " + failure, performer);
                return;
            }

            try
            {
                ApplyPlan(plan);
                Debug.Log("P0.9B SALSA lip sync configured on " + GetScenePath(performer.transform)
                    + ". SpeechAudioSource: " + plan.SpeechAudioPath + ". Visemes: "
                    + string.Join(", ", PerformerLipSyncMorphCatalog.Definitions.Select(item => item.Viseme))
                    + ". Maximum SALSA amount: " + plan.Bindings[0].Definition.MaximumSalsaAmount
                    + " (Unity weight " + plan.Bindings[0].Definition.MaximumUnityWeight + ")"
                    + ". The validation scene is marked dirty; save it to persist the setup.", performer);
                EditorUtility.DisplayDialog("SALSA lip-sync setup",
                    "Setup completed without adding an AudioSource, changing its playback settings, or altering the speech queue.\n\n"
                    + "The scene is marked dirty. Save it, reload it, then use the smoke harness in Play Mode.\n\n"
                    + "Configured " + plan.Bindings.Length + " exact Shape-only visemes on "
                    + PerformerLipSyncMorphCatalog.RendererPath + ". Each SALSA amount is normalized; max "
                    + plan.Bindings[0].Definition.MaximumSalsaAmount + " maps to Unity weight "
                    + plan.Bindings[0].Definition.MaximumUnityWeight + ".", "Close");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, performer);
                EditorUtility.DisplayDialog("SALSA lip-sync setup failed",
                    "The scene configuration was rolled back where Unity Undo supports it.\n\n" + exception.Message, "Close");
            }
        }

        [MenuItem(AuditMenu)]
        private static void AuditFromMenu()
        {
            if (!TryGetTargetPerformer(out var performer, out var failure))
            {
                EditorUtility.DisplayDialog("SALSA lip-sync audit", failure, "Close");
                return;
            }
            PerformerLipSyncAudit.WriteAudit(performer);
        }

        private static bool TryBuildPlan(SuccubusPerformer performer, out SetupPlan plan, out string failure)
        {
            plan = null;
            failure = null;
            if (performer == null || !performer.gameObject.scene.IsValid() || !performer.gameObject.scene.isLoaded)
                return Fail("Select a live scene SuccubusPerformer or open the validation scene with one active performer.", out failure);
            if (PrefabStageUtility.GetCurrentPrefabStage() == null && !EditorSceneManager.IsPreviewScene(performer.gameObject.scene)
                && !SceneManager.GetActiveScene().IsValid())
                return Fail("The performer is not in a loaded editable scene.", out failure);

            var animator = performer.GetComponent<Animator>();
            if (animator == null || animator.transform != performer.transform)
                return Fail("SuccubusPerformer requires its Animator on the same root GameObject.", out failure);

            var source = performer.SpeechAudioSource;
            if (source == null)
                return Fail("The serialized SpeechAudioSource is missing. Assign the existing head-mounted SpeechAudio AudioSource first; setup will not create one.", out failure);
            var headBones = animator.GetComponentsInChildren<Transform>(true)
                .Where(item => string.Equals(item.name, "head", StringComparison.Ordinal)).ToArray();
            if (headBones.Length != 1 || source.transform.parent != headBones[0] || !source.isActiveAndEnabled)
                return Fail("SpeechAudioSource must be active and enabled directly beneath the performer's head bone. Found '"
                    + GetScenePath(source.transform) + "'.", out failure);

            if (!PerformerLipSyncMorphCatalog.TryResolveBindings(animator, out var bindings, out failure)) return false;
            if (bindings.Any(binding => binding.Definition.MaximumSalsaAmount <= binding.Definition.RestSalsaAmount
                || binding.Definition.MaximumSalsaAmount > 1f))
                return Fail("The speech catalog contains a SALSA amount outside its normalized 0–1 range.", out failure);

            var allSalsa = performer.GetComponentsInChildren<Salsa>(true);
            var allQueues = performer.GetComponentsInChildren<QueueProcessor>(true);
            var salsa = performer.GetComponent<Salsa>();
            var queueProcessor = performer.GetComponent<QueueProcessor>();
            if (allSalsa.Length > 1 || allQueues.Length > 1
                || allSalsa.Any(item => item != salsa) || allQueues.Any(item => item != queueProcessor))
                return Fail("A SALSA or QueueProcessor component exists below the performer root or is duplicated. Setup stopped before changing the scene.", out failure);
            if (salsa != null && salsa.audioSrc != null && salsa.audioSrc != source)
                return Fail("The existing Salsa already observes a different AudioSource. Setup will not redirect an unrelated audio pipeline.", out failure);
            if (salsa != null && salsa.queueProcessor != null && salsa.queueProcessor != queueProcessor)
                return Fail("The existing Salsa references a different QueueProcessor. Setup will not replace that reference automatically.", out failure);
            if (salsa != null && salsa.emoter != null)
                return Fail("The existing Salsa is linked to EmoteR. P0.9B does not configure or remove EmoteR.", out failure);
            if (performer.GetComponentsInChildren<Emoter>(true).Length != 0)
                return Fail("EmoteR exists under this performer. Remove or resolve that separate setup before configuring the P0.9B performer.", out failure);
            if (performer.GetComponentsInChildren<Eyes>(true).Length != 0)
                return Fail("SALSA Eyes exists under this performer. P0.9B leaves gaze and eyes to the existing performer systems.", out failure);

            if (salsa != null)
            {
                foreach (var viseme in salsa.visemes ?? new List<LipsyncExpression>())
                {
                    if (viseme == null || viseme.expData == null)
                        return Fail("The existing Salsa contains a null viseme/controller entry. Setup stopped before mutation.", out failure);
                    if (!viseme.expData.name.StartsWith(PerformerLipSyncMorphCatalog.ControllerNamePrefix, StringComparison.Ordinal))
                        return Fail("Salsa contains an unowned viseme '" + viseme.expData.name
                            + "'. Setup will preserve unrelated authored configuration and therefore stopped before mutation.", out failure);
                }
                if (salsa.configReady && (salsa.visemes == null || salsa.visemes.Count == 0))
                    return Fail("The existing Salsa is marked ready but has no visemes. Inspect its setup before refreshing it.", out failure);
            }

            var allBridges = performer.GetComponentsInChildren<PerformerSalsaLipSync>(true);
            var bridge = performer.GetComponent<PerformerSalsaLipSync>();
            if (allBridges.Length > 1 || allBridges.Any(item => item != bridge))
                return Fail("PerformerSalsaLipSync exists below the performer root or is duplicated.", out failure);

            try
            {
                var projectRoot = Directory.GetParent(Application.dataPath).FullName;
                DazPoseRequiredMorphManifestStore.ValidateSpeechControls(projectRoot,
                    bindings.Select(binding => binding.Definition.SourceControlName));
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException
                || exception is InvalidDataException || exception is ArgumentException)
            {
                return Fail("Speech morph export preflight failed: " + exception.Message, out failure);
            }

            plan = new SetupPlan
            {
                Performer = performer,
                Animator = animator,
                SpeechAudioSource = source,
                Salsa = salsa,
                QueueProcessor = queueProcessor,
                Bridge = bridge,
                Bindings = bindings,
                Visemes = BuildVisemes(bindings),
                SpeechAudioPath = GetScenePath(source.transform)
            };
            return true;
        }

        private static void ApplyPlan(SetupPlan plan)
        {
            var undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Setup Performer SALSA Lip Sync");
            try
            {
                if (plan.QueueProcessor == null)
                    plan.QueueProcessor = Undo.AddComponent<QueueProcessor>(plan.Performer.gameObject);
                if (plan.Salsa == null)
                    plan.Salsa = Undo.AddComponent<Salsa>(plan.Performer.gameObject);
                if (plan.Bridge == null)
                    plan.Bridge = Undo.AddComponent<PerformerSalsaLipSync>(plan.Performer.gameObject);

                Undo.RecordObject(plan.QueueProcessor, "Configure SALSA QueueProcessor");
                Undo.RecordObject(plan.Salsa, "Configure SALSA visemes");
                Undo.RecordObject(plan.Bridge, "Bind SALSA lip-sync bridge");

                plan.QueueProcessor.enabled = true;

                plan.Salsa.configReady = false;
                plan.Salsa.audioSrc = plan.SpeechAudioSource;
                plan.Salsa.useExternalAnalysis = false;
                plan.Salsa.usePersistence = false;
                plan.Salsa.queueProcessor = plan.QueueProcessor;
                plan.Salsa.autoAdjustAnalysis = true;
                plan.Salsa.audioUpdateDelay = 0.0875f;
                plan.Salsa.loCutoff = 0.015f;
                plan.Salsa.hiCutoff = 0.75f;
                plan.Salsa.useAdvDyn = true;
                plan.Salsa.advDynPrimaryBias = 0.5f;
                plan.Salsa.useAdvDynJitter = true;
                plan.Salsa.advDynJitterAmount = 0.1f;
                plan.Salsa.advDynJitterProb = 0.2f;
                plan.Salsa.useAdvDynSecondaryMix = false;
                plan.Salsa.advDynSecondaryMix = 0f;
                if (plan.Salsa.visemes == null) plan.Salsa.visemes = new List<LipsyncExpression>();
                plan.Salsa.visemes.Clear();
                plan.Salsa.visemes.AddRange(plan.Visemes);
                plan.Salsa.DistributeTriggers(LerpEasings.EasingType.SquaredIn);
                plan.Salsa.configReady = true;
                plan.Salsa.Initialize();
                plan.Salsa.enabled = true;

                plan.Bridge.SetEditorReferences(plan.Performer, plan.Salsa, plan.QueueProcessor);
                plan.Bridge.enabled = true;

                var validation = plan.Bridge.ValidateConfiguration(out var failure);
                if (!validation) throw new InvalidOperationException("Post-configuration validation failed: " + failure);
                DazPoseRequiredMorphManifestStore.MarkSpeechControlsConfirmed(
                    Directory.GetParent(Application.dataPath).FullName,
                    plan.Bindings.Select(binding => binding.Definition.SourceControlName));

                EditorUtility.SetDirty(plan.Salsa);
                EditorUtility.SetDirty(plan.QueueProcessor);
                EditorUtility.SetDirty(plan.Bridge);
                PrefabUtility.RecordPrefabInstancePropertyModifications(plan.Salsa);
                PrefabUtility.RecordPrefabInstancePropertyModifications(plan.QueueProcessor);
                PrefabUtility.RecordPrefabInstancePropertyModifications(plan.Bridge);
                PrefabUtility.RecordPrefabInstancePropertyModifications(plan.Performer);
                EditorSceneManager.MarkSceneDirty(plan.Performer.gameObject.scene);
                Undo.CollapseUndoOperations(undoGroup);
            }
            catch
            {
                Undo.RevertAllDownToGroup(undoGroup);
                throw;
            }
        }

        private static List<LipsyncExpression> BuildVisemes(PerformerLipSyncMorphBinding[] bindings)
        {
            var result = new List<LipsyncExpression>(bindings.Length);
            foreach (var binding in bindings)
            {
                var definition = binding.Definition;
                var viseme = new LipsyncExpression(definition.ControllerName,
                    new InspectorControllerHelperData(), 0f);
                var expression = viseme.expData;
                expression.name = definition.ControllerName;
                expression.inspFoldout = false;
                expression.components.Clear();
                expression.controllerVars.Clear();

                var helper = new InspectorControllerHelperData
                {
                    smr = binding.Renderer,
                    blendIndex = binding.Index,
                    minShape = definition.RestSalsaAmount,
                    maxShape = definition.MaximumSalsaAmount
                };
                var component = new ExpressionComponent
                {
                    name = "Shape " + definition.Viseme,
                    controlType = ExpressionComponent.ControlType.Shape,
                    durationOn = definition.DurationOn,
                    durationHold = definition.DurationHold,
                    durationOff = definition.DurationOff,
                    easing = LerpEasings.EasingType.CubicOut,
                    isPersistent = false,
                    enabled = true,
                    inspFoldout = false
                };
                expression.controllerVars.Add(helper);
                expression.components.Add(component);
                result.Add(viseme);
            }
            return result;
        }

        private static bool TryGetTargetPerformer(out SuccubusPerformer performer, out string failure)
        {
            performer = null;
            failure = null;
            var selected = Selection.activeGameObject == null ? null
                : Selection.activeGameObject.GetComponentInParent<SuccubusPerformer>();
            if (selected != null && selected.gameObject.scene.IsValid() && selected.gameObject.scene.isLoaded)
            {
                performer = selected;
                return true;
            }

            var activeScene = SceneManager.GetActiveScene();
            var candidates = Resources.FindObjectsOfTypeAll<SuccubusPerformer>()
                .Where(item => item != null && item.gameObject.scene.IsValid() && item.gameObject.scene.isLoaded
                    && (item.gameObject.scene == activeScene || PrefabStageUtility.GetCurrentPrefabStage() != null))
                .ToArray();
            if (candidates.Length == 1)
            {
                performer = candidates[0];
                return true;
            }
            failure = candidates.Length == 0
                ? "No SuccubusPerformer is selected or present in the active scene. Open PoseValidation.unity and select its performer root."
                : "More than one SuccubusPerformer is in scope. Select the exact performer root to configure.";
            return false;
        }

        private static bool Fail(string message, out string failure)
        {
            failure = message;
            return false;
        }

        private static string GetScenePath(Transform target)
        {
            if (target == null) return "<missing>";
            var segments = new Stack<string>();
            for (var current = target; current != null; current = current.parent) segments.Push(current.name);
            return string.Join("/", segments);
        }
    }
}
