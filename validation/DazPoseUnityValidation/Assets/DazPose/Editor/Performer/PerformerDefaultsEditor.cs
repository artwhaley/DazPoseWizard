using System;
using System.Collections.Generic;
using System.Linq;
using DazPose.Performer;
using DazPose.Performer.HandGrip;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DazPose.Editor.Performer
{
    /// <summary>Explicit, instance-only tuning transfer across Play Mode. Never writes shared assets or rig bindings.</summary>
    [InitializeOnLoad]
    public static class PerformerDefaultsEditor
    {
        const string Key = "DazPose.Performer.PendingDefaults.v1";
        const string SavedKey = "DazPose.Performer.SavedTargets.v1";
        [Serializable] public sealed class Value
        {
            public string path; public int type, integer; public float number; public bool boolean;
            public AnimationCurve curve;
        }
        [Serializable] public sealed class Pending { public string objectId; public Value[] values; }
        [Serializable] sealed class Queue { public List<Pending> entries = new List<Pending>(); }
        [Serializable] sealed class SavedTargets { public List<string> ids = new List<string>(); public List<string> instances = new List<string>(); }

        static PerformerDefaultsEditor() { EditorApplication.playModeStateChanged += StateChanged; }
        public static bool IsSupported(string root)
        {
            if (root == "breathingBones") return false;
            return root == "defaultTransition" || root == "defaultExpressionBlendTime" || root == "initialExpressionIntensity"
                || root == "breathsPerMinute"
                || root == "motionOwnershipBlendSeconds" || root.StartsWith("breathing", StringComparison.Ordinal)
                || root.StartsWith("morphBreathing", StringComparison.Ordinal) || root.StartsWith("breathe", StringComparison.Ordinal)
                || root.StartsWith("boneBreathing", StringComparison.Ordinal) || root.StartsWith("gaze", StringComparison.Ordinal)
                || root.StartsWith("headGaze", StringComparison.Ordinal) || root.StartsWith("eyeGaze", StringComparison.Ordinal)
                || root.StartsWith("attentionLife", StringComparison.Ordinal) || root.StartsWith("eyeFixation", StringComparison.Ordinal)
                || root.StartsWith("headAttention", StringComparison.Ordinal) || root.StartsWith("blink", StringComparison.Ordinal);
        }

        public static bool QueueApply(SuccubusPerformer performer, out string error)
        {
            if (!Application.isPlaying || performer == null) { error = "Select a saved performer instance in Play Mode."; return false; }
            if (string.IsNullOrEmpty(performer.gameObject.scene.path)) { error = "Save the scene before entering Play Mode to apply defaults."; return false; }
            var saved = JsonUtility.FromJson<SavedTargets>(SessionState.GetString(SavedKey, "{}"));
            int actorIndex = saved?.instances.IndexOf(performer.GetEntityId().ToString()) ?? -1;
            if (actorIndex < 0)
            { error = "This performer did not exist in the saved scene before Play Mode. Apply only to a saved instance."; return false; }
            var queue = ReadQueue();
            var grip = performer.GetComponent<PerformerHandGripController>();
            int gripIndex = grip != null ? saved.instances.IndexOf(grip.GetEntityId().ToString()) : -1;
            if (grip != null && gripIndex < 0)
            { error = "The grip controller was added during Play Mode. Save its setup before applying tuning."; return false; }
            try
            {
                Capture(performer, saved.ids[actorIndex], queue, IsSupported);
                if (grip != null) Capture(grip, saved.ids[gripIndex], queue, root => root == "gripStrength01" || root == "wristTwistDegrees");
            }
            catch (InvalidOperationException exception) { error = exception.Message; return false; }
            SessionState.SetString(Key, JsonUtility.ToJson(queue)); error = null; return true;
        }

        static Queue ReadQueue()
        {
            string json = SessionState.GetString(Key, "");
            return string.IsNullOrEmpty(json) ? new Queue() : JsonUtility.FromJson<Queue>(json);
        }
        static void Capture(Component component, string id, Queue queue, Func<string, bool> supported)
        {
            var values = new List<Value>();
            using (var serialized = new SerializedObject(component))
            {
                var property = serialized.GetIterator();
                bool enter = true;
                while (property.NextVisible(enter))
                {
                    var root = property.propertyPath.Split('.')[0];
                    enter = supported(root) && property.propertyType == SerializedPropertyType.Generic;
                    if (!supported(root) || property.propertyType == SerializedPropertyType.Generic) continue;
                    var value = new Value { path = property.propertyPath, type = (int)property.propertyType };
                    switch (property.propertyType)
                    {
                        case SerializedPropertyType.Boolean: value.boolean = property.boolValue; break;
                        case SerializedPropertyType.Integer: case SerializedPropertyType.Enum: value.integer = property.intValue; break;
                        case SerializedPropertyType.Float: value.number = property.floatValue; break;
                        case SerializedPropertyType.AnimationCurve: value.curve = property.animationCurveValue; break;
                        default: throw new InvalidOperationException("Unsupported tuning type at " + property.propertyPath);
                    }
                    values.Add(value);
                }
            }
            queue.entries.RemoveAll(item => item.objectId == id);
            queue.entries.Add(new Pending { objectId = id, values = values.ToArray() });
        }

        static void StateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode)
            {
                var saved = new SavedTargets();
                for (int i = 0; i < EditorSceneManager.sceneCount; i++)
                {
                    var scene = EditorSceneManager.GetSceneAt(i);
                    if (!scene.isLoaded || string.IsNullOrEmpty(scene.path) || EditorSceneManager.IsPreviewScene(scene)) continue;
                    foreach (var root in scene.GetRootGameObjects())
                    foreach (var actor in root.GetComponentsInChildren<SuccubusPerformer>(true))
                    {
                        saved.ids.Add(GlobalObjectId.GetGlobalObjectIdSlow(actor).ToString());
                        saved.instances.Add(actor.GetEntityId().ToString());
                        var grip = actor.GetComponent<PerformerHandGripController>();
                        if (grip != null)
                        { saved.ids.Add(GlobalObjectId.GetGlobalObjectIdSlow(grip).ToString()); saved.instances.Add(grip.GetEntityId().ToString()); }
                    }
                }
                SessionState.SetString(SavedKey, JsonUtility.ToJson(saved));
            }
            if (state == PlayModeStateChange.EnteredEditMode) EditorApplication.delayCall += ApplyQueued;
        }
        internal static void ApplyQueued()
        {
            if (Application.isPlaying) return;
            var queue = ReadQueue(); SessionState.EraseString(Key);
            if (queue.entries.Count == 0) return;
            Undo.IncrementCurrentGroup(); int group = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("Apply performer tuning to instance defaults");
            foreach (var pending in queue.entries)
            {
                if (!GlobalObjectId.TryParse(pending.objectId, out var id)) continue;
                var component = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(id) as Component;
                if (component == null)
                { Debug.LogWarning("Performer defaults were not applied: the saved instance no longer exists. " + pending.objectId); continue; }
                using (var serialized = new SerializedObject(component))
                {
                    if (pending.values.Any(value => serialized.FindProperty(value.path) == null || (int)serialized.FindProperty(value.path).propertyType != value.type))
                    { Debug.LogWarning("Performer defaults were not applied: serialized fields changed.", component); continue; }
                    Undo.RecordObject(component, "Apply performer tuning to instance defaults");
                    foreach (var value in pending.values)
                    {
                        var property = serialized.FindProperty(value.path);
                        switch ((SerializedPropertyType)value.type)
                        {
                            case SerializedPropertyType.Boolean: property.boolValue = value.boolean; break;
                            case SerializedPropertyType.Integer: case SerializedPropertyType.Enum: property.intValue = value.integer; break;
                            case SerializedPropertyType.Float: property.floatValue = value.number; break;
                            case SerializedPropertyType.AnimationCurve: property.animationCurveValue = value.curve; break;
                        }
                    }
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    PrefabUtility.RecordPrefabInstancePropertyModifications(component);
                    EditorUtility.SetDirty(component); EditorSceneManager.MarkSceneDirty(component.gameObject.scene);
                }
            }
            Undo.FlushUndoRecordObjects(); Undo.CollapseUndoOperations(group);
        }
    }
}

