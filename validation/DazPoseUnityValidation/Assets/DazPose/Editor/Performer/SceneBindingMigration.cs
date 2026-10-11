using System;
using System.Linq;
using DazPose.Motion;
using DazPose.Player;
using DazPose.Toys;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DazPose.Editor.Performer
{
    public static class SceneBindingMigration
    {
        #if DAZPOSE_DEVELOPMENT_TOOLS
[MenuItem("DazPose/Development/Legacy/Production/Bind player and toy references in selected scope")]
#endif
        static void BindSelection()
        {
            try { Bind(Selection.activeGameObject); }
            catch (InvalidOperationException error) { Debug.LogError(error.Message, Selection.activeGameObject); }
        }
        /// <summary>Optional legacy migration. Never searches outside the explicitly selected hierarchy.</summary>
        public static void Bind(GameObject scope)
        {
            if (Application.isPlaying || scope == null || EditorUtility.IsPersistent(scope))
                throw new InvalidOperationException("Select a scene hierarchy in Edit Mode.");
            var players = scope.GetComponentsInChildren<PlayerController>(true);
            var toys = scope.GetComponentsInChildren<ToyControlService>(true);
            var clocks = scope.GetComponentsInChildren<MotionDriver>(true);
            if (players.Length == 0 || toys.Length != 1 || clocks.Length != 1)
                throw new InvalidOperationException("Selected scope requires a player, exactly one toy service and exactly one playback clock. Assign explicit references in the inspectors when the scope is ambiguous.");
            if (players.Any(player => player.BoundToyService != null && player.BoundToyService != toys[0]) ||
                (toys[0].PlaybackDriver != null && toys[0].PlaybackDriver != clocks[0]))
                throw new InvalidOperationException("Selected scope contains conflicting explicit references. Resolve them in the owning inspectors; migration does not overwrite them.");
            Undo.IncrementCurrentGroup(); int group = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("Bind scoped player and toy references");
            foreach (var player in players)
            {
                Undo.RecordObject(player, "Bind player device service"); player.ConfigureToyService(toys[0]);
                PrefabUtility.RecordPrefabInstancePropertyModifications(player); EditorUtility.SetDirty(player);
            }
            Undo.RecordObject(toys[0], "Bind toy playback clock"); toys[0].ConfigurePlayback(clocks[0]);
            PrefabUtility.RecordPrefabInstancePropertyModifications(toys[0]); EditorUtility.SetDirty(toys[0]);
            EditorSceneManager.MarkSceneDirty(scope.scene); Undo.CollapseUndoOperations(group);
        }
    }
}
