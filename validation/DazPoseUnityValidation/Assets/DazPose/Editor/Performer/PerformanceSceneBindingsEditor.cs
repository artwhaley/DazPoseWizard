using DazPose.Performer;
using UnityEditor;
using UnityEngine;

namespace DazPose.Editor.Performer
{
    [CustomEditor(typeof(PerformanceSceneBindings))]
    public sealed class PerformanceSceneBindingsEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var binding = (PerformanceSceneBindings)target;
            EditorGUILayout.HelpBox("Declare only the capabilities this sequence uses. Bindings never start playback or discover another actor.", MessageType.Info);
            using (new EditorGUI.DisabledScope(Application.isPlaying || binding.Performer == null))
                if (GUILayout.Button("Wire declared playback and device references"))
                {
                    Undo.RecordObject(binding.Performer, "Wire scene playback");
                    binding.Performer.ConfigureMotionSource(binding.Playback);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(binding.Performer);
                    EditorUtility.SetDirty(binding.Performer);
                    if (binding.Player != null)
                    { Undo.RecordObject(binding.Player, "Wire player devices"); binding.Player.ConfigureToyService(binding.Toys); EditorUtility.SetDirty(binding.Player); PrefabUtility.RecordPrefabInstancePropertyModifications(binding.Player); }
                    if (binding.Toys != null)
                    { Undo.RecordObject(binding.Toys, "Wire toy playback"); binding.Toys.ConfigurePlayback(binding.Playback); EditorUtility.SetDirty(binding.Toys); PrefabUtility.RecordPrefabInstancePropertyModifications(binding.Toys); }
                }
            var issues = binding.Validate(Application.isPlaying);
            if (issues.Length == 0) EditorGUILayout.HelpBox("Declared capabilities and references are ready.", MessageType.Info);
            foreach (var issue in issues)
            {
                EditorGUILayout.HelpBox(issue.ToString(), MessageType.Error);
                if (issue.Owner != null && GUILayout.Button("Inspect " + issue.Owner.name + "/" + issue.PropertyPath))
                { Selection.activeObject = issue.Owner; EditorGUIUtility.PingObject(issue.Owner); }
            }
        }
    }
}
