using System.Collections.Generic;
using DazPose.Player;
using UnityEditor;

namespace DazPose.Editor.Player
{
    [CustomEditor(typeof(PlayerView)), CanEditMultipleObjects]
    public sealed class PlayerViewEditor : UnityEditor.Editor
    {
        private static readonly HashSet<string> GroupedPropertyNames = new HashSet<string>
        {
            "viewRig",
            "headPose",
            "mainCamera",
            "defaultMoveDuration",
            "defaultLookDuration",
            "defaultTrackingSettings"
        };

        private bool rigBindingExpanded = true;
        private bool viewDefaultsExpanded = true;

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            SerializedProperty scriptProperty = serializedObject.FindProperty("m_Script");
            if (scriptProperty != null)
            {
                using (new EditorGUI.DisabledScope(true))
                    EditorGUILayout.PropertyField(scriptProperty);
            }

            DrawGroup("Rig binding", ref rigBindingExpanded, "viewRig", "headPose", "mainCamera");
            DrawGroup("View defaults", ref viewDefaultsExpanded,
                "defaultMoveDuration", "defaultLookDuration", "defaultTrackingSettings");
            DrawUnclassifiedProperties();

            EditorGUILayout.HelpBox(
                "Play Mode tuning is temporary. Performer Apply-to-defaults does not capture PlayerView timing, tracking, view-rig, head-pose, or camera values.",
                MessageType.Info);

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawGroup(string title, ref bool expanded, params string[] propertyNames)
        {
            expanded = EditorGUILayout.BeginFoldoutHeaderGroup(expanded, title);
            if (expanded)
            {
                EditorGUI.indentLevel++;
                foreach (string propertyName in propertyNames)
                {
                    SerializedProperty property = serializedObject.FindProperty(propertyName);
                    if (property != null)
                        EditorGUILayout.PropertyField(property, true);
                }
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
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
    }
}
