using System.Collections.Generic;
using System.Linq;
using DazPose.Performer;
using DazPose.Performer.HandGrip;
using UnityEditor;
using UnityEngine;

namespace DazPose.Editor.Performer
{
    [CustomEditor(typeof(SuccubusPerformer)), CanEditMultipleObjects]
    public sealed class SuccubusPerformerEditor : UnityEditor.Editor
    {
        private static readonly HashSet<string> GroupedPropertyNames = new HashSet<string>
        {
            "animator",
            "initialPose", "initialExpression", "initialExpressionIntensity", "startHidden",
            "defaultTransition", "defaultExpressionBlendTime",
            "breathingEnabled", "breathsPerMinute", "breathingCurve",
            "morphBreathingEnabled", "morphBreathingStrength", "breatheStrength", "breatheBellyStrength",
            "boneBreathingEnabled", "boneBreathingStrength", "breathingBones",
            "gazeEnabled", "gazeAcquireToleranceDegrees", "headGazeEnabled", "headGazeWeight",
            "headGazeResponse", "headGazeMaxYaw", "headGazeMaxPitch", "eyeGazeEnabled",
            "eyeGazeWeight", "eyeGazeResponse", "eyeGazeMaxYaw", "eyeGazeMaxPitch", "gazeReleaseResponse",
            "attentionLifeEnabled", "attentionLifeSeed", "eyeFixationLifeEnabled",
            "eyeFixationMaxHorizontalDegrees", "eyeFixationMaxVerticalDegrees",
            "eyeFixationMinimumHoldSeconds", "eyeFixationMaximumHoldSeconds", "eyeFixationCenterBias",
            "headAttentionLifeEnabled", "headAttentionMaxTiltDegrees", "headAttentionMaxChinDegrees",
            "headAttentionMinimumHoldSeconds", "headAttentionMaximumHoldSeconds", "headAttentionTransitionResponse",
            "blinkEnabled", "blinkStrength", "blinkMinimumIntervalSeconds", "blinkMaximumIntervalSeconds",
            "blinkCloseDurationSeconds", "blinkClosedDurationSeconds", "blinkOpenDurationSeconds",
            "speechAudioSource", "locomotionProfile", "teleportProfile", "dissolveProfile", "dissolveRig",
            "motionDriver", "motionSet", "rightArmMotionMask", "motionOwnershipBlendSeconds",
            "gestureUpperBodyMask"
        };

        private bool setupFoldout = true;
        private bool initialStateFoldout = true;
        private bool lifeFoldout = true;
        private bool speechFoldout = true;
        private bool movementFoldout = true;
        private bool effectsFoldout = true;
        private bool contactFoldout = true;
        private bool playbackFoldout = true;
        private bool advancedFoldout = false;
        private string applyMessage;
        private MessageType applyMessageType = MessageType.Info;

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawGroup("Setup / readiness", ref setupFoldout, "animator");
            DrawGroup("Initial state", ref initialStateFoldout,
                "initialPose", "initialExpression", "initialExpressionIntensity", "startHidden",
                "defaultTransition", "defaultExpressionBlendTime");
            DrawGroup("Life / gaze", ref lifeFoldout,
                "breathingEnabled", "breathsPerMinute", "breathingCurve",
                "morphBreathingEnabled", "morphBreathingStrength", "breatheStrength", "breatheBellyStrength",
                "boneBreathingEnabled", "boneBreathingStrength",
                "gazeEnabled", "gazeAcquireToleranceDegrees", "headGazeEnabled", "headGazeWeight",
                "headGazeResponse", "headGazeMaxYaw", "headGazeMaxPitch", "eyeGazeEnabled",
                "eyeGazeWeight", "eyeGazeResponse", "eyeGazeMaxYaw", "eyeGazeMaxPitch", "gazeReleaseResponse",
                "attentionLifeEnabled", "attentionLifeSeed", "eyeFixationLifeEnabled",
                "eyeFixationMaxHorizontalDegrees", "eyeFixationMaxVerticalDegrees",
                "eyeFixationMinimumHoldSeconds", "eyeFixationMaximumHoldSeconds", "eyeFixationCenterBias",
                "headAttentionLifeEnabled", "headAttentionMaxTiltDegrees", "headAttentionMaxChinDegrees",
                "headAttentionMinimumHoldSeconds", "headAttentionMaximumHoldSeconds", "headAttentionTransitionResponse",
                "blinkEnabled", "blinkStrength", "blinkMinimumIntervalSeconds", "blinkMaximumIntervalSeconds",
                "blinkCloseDurationSeconds", "blinkClosedDurationSeconds", "blinkOpenDurationSeconds");
            DrawGroup("Speech", ref speechFoldout, "speechAudioSource");
            DrawGroup("Movement", ref movementFoldout, "locomotionProfile");
            DrawGroup("Effects", ref effectsFoldout, "teleportProfile", "dissolveProfile", "dissolveRig");
            DrawContactGroup();
            DrawGroup("Playback / gesture", ref playbackFoldout,
                "motionDriver", "motionSet", "rightArmMotionMask", "motionOwnershipBlendSeconds", "gestureUpperBodyMask");
            DrawAdvancedGroup();
            DrawUnclassifiedProperties();

            serializedObject.ApplyModifiedProperties();
            DrawApplyToDefaults();
        }

        private void DrawGroup(string title, ref bool expanded, params string[] propertyNames)
        {
            expanded = EditorGUILayout.BeginFoldoutHeaderGroup(expanded, title);
            if (expanded)
            {
                EditorGUI.indentLevel++;
                foreach (string propertyName in propertyNames)
                    DrawProperty(propertyName);
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private void DrawProperty(string propertyName)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property != null)
                EditorGUILayout.PropertyField(property, true);
        }

        private void DrawContactGroup()
        {
            contactFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(contactFoldout, "Contact");
            if (contactFoldout)
            {
                EditorGUI.indentLevel++;
                var controllers = targets
                    .OfType<SuccubusPerformer>()
                    .Select(performer => performer != null
                        ? performer.GetComponent<PerformerHandGripController>()
                        : null)
                    .ToArray();

                if (controllers.Length == 0 || controllers.All(controller => controller == null))
                {
                    EditorGUILayout.HelpBox("No PerformerHandGripController is attached to this object.", MessageType.Info);
                }
                else
                {
                    if (controllers.Any(controller => controller == null))
                        EditorGUILayout.HelpBox("Some selected performers have no same-object hand grip controller. Fields below edit the controllers that are present.", MessageType.Warning);

                    PerformerHandGripController[] presentControllers = controllers
                        .Where(controller => controller != null)
                        .ToArray();
                    EditorGUILayout.BeginHorizontal();
                    using (new EditorGUI.DisabledScope(true))
                        EditorGUILayout.ObjectField("Hand grip controller", presentControllers[0],
                            typeof(PerformerHandGripController), true);
                    if (GUILayout.Button("Select", GUILayout.Width(64f)))
                    {
                        Selection.objects = presentControllers.Cast<Object>().ToArray();
                        EditorGUIUtility.PingObject(presentControllers[0]);
                    }
                    EditorGUILayout.EndHorizontal();

                    var controllerObject = new SerializedObject(presentControllers.Cast<Object>().ToArray());
                    controllerObject.Update();
                    DrawControllerProperty(controllerObject, "profile");
                    DrawControllerProperty(controllerObject, "gripStrength01");
                    DrawControllerProperty(controllerObject, "wristTwistDegrees");
                    controllerObject.ApplyModifiedProperties();
                }

                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private static void DrawControllerProperty(SerializedObject controllerObject, string propertyName)
        {
            SerializedProperty property = controllerObject.FindProperty(propertyName);
            if (property != null)
                EditorGUILayout.PropertyField(property, true);
        }

        private void DrawAdvancedGroup()
        {
            advancedFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(advancedFoldout, "Advanced");
            if (advancedFoldout)
            {
                EditorGUI.indentLevel++;
                DrawProperty("breathingBones");
                DrawRuntimeTelemetry();
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private void DrawRuntimeTelemetry()
        {
            SuccubusPerformer performer = target as SuccubusPerformer;
            if (performer == null)
                return;

            EditorGUILayout.LabelField("Runtime telemetry", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.Toggle("Runtime ready", performer.IsRuntimeReady);
                EditorGUILayout.Toggle("Transitioning", performer.IsTransitioning);
                EditorGUILayout.Toggle("Hidden", performer.IsHidden);
                EditorGUILayout.Toggle("Locomoting", performer.IsLocomoting);
                EditorGUILayout.Toggle("Teleporting", performer.IsTeleporting);
                EditorGUILayout.Toggle("Dissolving", performer.IsDissolving);
            }
            if (targets.Length > 1)
                EditorGUILayout.HelpBox("Runtime telemetry shows the first selected performer.", MessageType.None);
        }

        private void DrawUnclassifiedProperties()
        {
            SerializedProperty iterator = serializedObject.GetIterator();
            bool enterChildren = true;
            while (iterator.NextVisible(enterChildren))
            {
                enterChildren = false;
                if (iterator.depth != 0 || iterator.name == "m_Script" || GroupedPropertyNames.Contains(iterator.name))
                    continue;

                EditorGUILayout.PropertyField(iterator, true);
            }
        }

        private void DrawApplyToDefaults()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Play tuning is temporary", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Apply schedules supported tuning onto the saved scene instance after exiting Play Mode. Setup, calibration, and bindings are excluded.",
                MessageType.Info);

            using (new EditorGUI.DisabledScope(!Application.isPlaying || targets.Length != 1))
            {
                if (GUILayout.Button("Apply supported tuning to saved defaults"))
                {
                    bool queued = PerformerDefaultsEditor.QueueApply((SuccubusPerformer)target, out string error);
                    applyMessageType = queued ? MessageType.Info : MessageType.Error;
                    applyMessage = queued
                        ? "Supported tuning will be applied to the saved scene instance after Play Mode exits."
                        : error;
                }
            }

            if (!Application.isPlaying)
                EditorGUILayout.HelpBox("Enter Play Mode to tune values and schedule them for the saved scene instance.", MessageType.None);
            else if (targets.Length != 1)
                EditorGUILayout.HelpBox("Select one performer to apply tuning to its saved scene instance.", MessageType.None);

            if (!string.IsNullOrEmpty(applyMessage))
                EditorGUILayout.HelpBox(applyMessage, applyMessageType);
        }
    }
}
