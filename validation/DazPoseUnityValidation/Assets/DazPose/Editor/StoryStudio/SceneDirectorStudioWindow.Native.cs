using System;
using System.Linq;
using DazPose.Story;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DazPose.Editor.Studio
{
    public sealed partial class SceneDirectorStudioWindow
    {
        [SerializeField] StoryProgramSequence nativeSequence;
        NativeStageContext nativeContext;
        Vector2 nativeScroll;

        void ResolveNativeDefaults()
        {
            if (host == null) SelectHost(NativeStageContext.FindHost());
            if (nativeSequence == null)
            {
                var matches = host.gameObject.scene.GetRootGameObjects()
                    .SelectMany(g => g.GetComponentsInChildren<StoryProgramSequence>(true)).Where(s => s.Bindings == host).ToArray();
                if (matches.Length == 1) nativeSequence = matches[0];
            }
            if (source == null && nativeSequence != null && nativeSequence.CompiledSource != null)
                SelectSource(nativeSequence.CompiledSource);
        }

        void OnGUI()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                EditorGUILayout.LabelField("Native directing — N01 foundation", EditorStyles.boldLabel);
                if (GUILayout.Button("Find scene/source", EditorStyles.toolbarButton)) Try(ResolveNativeDefaults);
                if (GUILayout.Button("G1 Camera Proof", EditorStyles.toolbarButton)) NativeDirectingG1Window.Open();
            }
            nativeScroll = EditorGUILayout.BeginScrollView(nativeScroll);
            EditorGUILayout.LabelField("Active scene", SceneManager.GetActiveScene().path);
            var nextHost = (StorySceneBindings)EditorGUILayout.ObjectField("Scene host", host, typeof(StorySceneBindings), true);
            if (nextHost != host) { nativeSequence = null; SelectHost(nextHost); }
            var nextSequence = (StoryProgramSequence)EditorGUILayout.ObjectField("Saved story sequence", nativeSequence, typeof(StoryProgramSequence), true);
            if (nextSequence != nativeSequence)
            {
                Release(); nativeSequence = nextSequence;
                if (nativeSequence != null) { SelectHost(nativeSequence.Bindings); SelectSource(nativeSequence.CompiledSource); }
            }
            var nextSource = (CompiledStoryAsset)EditorGUILayout.ObjectField("Story source", source, typeof(CompiledStoryAsset), false);
            if (nextSource != source) SelectSource(nextSource);
            var entries = manifest?.Analysis.Program?.entries.Where(e => e.kind == "sequence").Select(e => e.name).ToArray() ?? Array.Empty<string>();
            if (entries.Length > 0)
            {
                int index = Mathf.Max(0, Array.IndexOf(entries, sequence));
                sequence = entries[EditorGUILayout.Popup("Sequence entry", index, entries)];
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Start directing")) Try(() =>
                {
                    Release();
                    if (source == null || !source.IsCompiled || string.IsNullOrEmpty(sequence))
                        throw new InvalidOperationException("Choose a valid imported story source and sequence entry. Resolve Console/import errors first.");
                    if (nativeSequence != null && (host == null || nativeSequence.Bindings != host || nativeSequence.gameObject.scene != host.gameObject.scene || nativeSequence.CompiledSource != source))
                        throw new InvalidOperationException("The saved sequence, source and host disagree. Select the matching saved sequence or clear it to inspect this source independently.");
                    nativeContext = NativeStageContext.Resolve(host);
                });
                if (GUILayout.Button("Stop directing")) { Release(); Repaint(); }
            }
            if (nativeContext != null)
            {
                EditorGUILayout.HelpBox("EDIT MODE — NATIVE CONTEXT CONNECTED\nG1 is awaiting Art's camera proof. Cue staging/transport is held until the mandatory gates are implemented and accepted.", MessageType.Info);
                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUILayout.ObjectField("Actual Lara", nativeContext.Lara, typeof(UnityEngine.Object), true);
                    EditorGUILayout.ObjectField("Prepared body", nativeContext.Body, typeof(UnityEngine.Object), true);
                    EditorGUILayout.ObjectField("Player root", nativeContext.PlayerRoot, typeof(Transform), true);
                    EditorGUILayout.ObjectField("ViewRig", nativeContext.ViewRig, typeof(Transform), true);
                    EditorGUILayout.ObjectField("HeadPose", nativeContext.HeadPose, typeof(Transform), true);
                    EditorGUILayout.ObjectField("Real Game camera", nativeContext.Camera, typeof(Camera), true);
                }
                if (GUILayout.Button("Select real player camera")) Selection.activeObject = nativeContext.Camera;
            }
            else EditorGUILayout.HelpBox("Open the saved fixture scene, click Find scene/source, then Start directing to inspect its real references. Use the standard Scene and Game tabs for the camera proof.", MessageType.Info);
            if (error != null) EditorGUILayout.HelpBox(error, MessageType.Error);
            EditorGUILayout.EndScrollView();
        }
    }
}
