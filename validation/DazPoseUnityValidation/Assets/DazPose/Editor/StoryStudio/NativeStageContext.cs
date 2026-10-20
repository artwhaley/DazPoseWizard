using System;
using System.Linq;
using DazPose.Performer;
using DazPose.Player;
using DazPose.Story;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DazPose.Editor.Studio
{
    /// <summary>Read-only binding context for the actual saved scene. Acquiring it never stages or starts gameplay.</summary>
    public sealed class NativeStageContext
    {
        public StorySceneBindings Host { get; }
        public Scene Scene => Host.gameObject.scene;
        public PerformanceSceneBindings Performance { get; }
        public PlayerController Player { get; }
        public PlayerView View { get; }
        public Transform PlayerRoot => View.transform;
        public Transform ViewRig {get;}
        public Transform HeadPose {get;}
        public Camera Camera {get;}
        public SuccubusPerformer Lara {get;}
        public PlayerBodyPresence Body {get;}
        public bool ValidateAllLocations {get;}

        NativeStageContext(StorySceneBindings host, PerformanceSceneBindings performance,bool validateAllLocations)
        {
            Host = host;
            Performance = performance;
            Player = performance.Player;
            View = Player.ConfiguredView;Lara=host.lara;Body=host.playerBody;ViewRig=View.ConfiguredViewRig;HeadPose=View.HeadTransform;Camera=View.MainCamera;ValidateAllLocations=validateAllLocations;
        }

        public static StorySceneBindings FindHost()
        {
            RequireEditMode();
            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded || string.IsNullOrEmpty(scene.path))
                throw new InvalidOperationException("Open a saved performance scene, then select its StorySceneBindings host.");
            var hosts = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<StorySceneBindings>(true)).ToArray();
            if (hosts.Length != 1)
                throw new InvalidOperationException($"The active scene has {hosts.Length} StorySceneBindings hosts. Select the intended host explicitly.");
            return hosts[0];
        }

        public static NativeStageContext Resolve(StorySceneBindings host,bool validateAllLocations=true)
        {
            RequireEditMode();
            if (host == null) throw new InvalidOperationException("Assign the saved scene's StorySceneBindings host.");
            var scene = host.gameObject.scene;
            if (!scene.IsValid() || !scene.isLoaded || string.IsNullOrEmpty(scene.path) || EditorSceneManager.IsPreviewScene(scene)
                || scene != SceneManager.GetActiveScene() || PrefabStageUtility.GetCurrentPrefabStage() != null)
                throw new InvalidOperationException("Choose a host in the active, open, saved scene; Prefab Mode and preview scenes are unsupported.");
            void Same(Component component, string field)
            {
                if (component == null) throw new InvalidOperationException("Assign " + field + ".");
                if (component.gameObject.scene != scene || EditorUtility.IsPersistent(component))
                    throw new InvalidOperationException(field + " must reference a saved object in the host's own scene.");
            }
            var performance = host.GetComponent<PerformanceSceneBindings>();
            Same(performance, "StorySceneBindings/PerformanceSceneBindings");
            Same(host.lara, "StorySceneBindings.lara");
            if(validateAllLocations||host.laraFocus!=null)Same(host.laraFocus, "StorySceneBindings.laraFocus");
            Same(host.playerBody, "StorySceneBindings.playerBody");
            Same(performance.Player, "PerformanceSceneBindings.Player");
            if (performance.Performer != host.lara)
                throw new InvalidOperationException("PerformanceSceneBindings.Performer and StorySceneBindings.lara must reference the same saved Lara.");
            if (performance.Player.BodyPresence != host.playerBody)
                throw new InvalidOperationException("PlayerController.BodyPresence and StorySceneBindings.playerBody must reference the same prepared body.");
            var view = performance.Player.ConfiguredView;
            Same(view, "PlayerController.view (configured PlayerView)");
            if (view.transform != performance.Player.transform)
                throw new InvalidOperationException("The configured PlayerView and PlayerController must share the saved player root.");
            Same(view.ConfiguredViewRig, "PlayerView.viewRig");
            Same(view.HeadTransform, "PlayerView.headPose");
            Same(view.MainCamera, "PlayerView.mainCamera");
            if (view.ConfiguredViewRig == view.transform || !view.ConfiguredViewRig.IsChildOf(view.transform)
                || view.HeadTransform == view.ConfiguredViewRig || !view.HeadTransform.IsChildOf(view.ConfiguredViewRig)
                || !view.MainCamera.transform.IsChildOf(view.HeadTransform))
                throw new InvalidOperationException("Expected the saved Player -> ViewRig -> HeadPose -> MainCamera hierarchy. Repair the assigned references explicitly.");
            Same(host.lara.ConfiguredAnimator, "Lara.ConfiguredAnimator");
            if (host.lara.ConfiguredAnimator.transform != host.lara.transform)
                throw new InvalidOperationException("Lara's configured Generic Animator must share her performer root.");
            var locations = host.locations ?? Array.Empty<StoryLocationBinding>();
            if (locations.Any(l => l == null || string.IsNullOrWhiteSpace(l.id)) || locations.GroupBy(l => l.id, StringComparer.Ordinal).Any(g => g.Count() != 1))
                throw new InvalidOperationException("Scene locations require unique, nonempty IDs and non-null rows.");
            if(validateAllLocations)foreach (var location in locations)
            {
                Same(location.placement, "Location '" + location.id + "'.placement");
                if (location.focus != null) Same(location.focus, "Location '" + location.id + "'.focus");
                if (location.seat == null) continue;
                Same(location.seat, "Location '" + location.id + "'.seat");
                Same(location.seat.ApproachAnchor, "Location '" + location.id + "'.seat.ApproachAnchor");
                Same(location.seat.SeatAnchor, "Location '" + location.id + "'.seat.SeatAnchor");
            }
            return new NativeStageContext(host, performance,validateAllLocations);
        }

        public static void RequireEditMode()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || Application.isPlaying)
                throw new InvalidOperationException("Native directing diagnostics require Edit Mode. Stop Play manually first.");
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
                throw new InvalidOperationException("Wait for Unity's current compile/import to finish, then retry.");
        }

        public static string ObjectPath(Transform value)
        {
            if (value == null) return "<missing>";
            string path = value.name;
            for (var parent = value.parent; parent != null; parent = parent.parent) path = parent.name + "/" + path;
            return value.gameObject.scene.path + ":" + path;
        }

        public static void RefreshViews()
        {
            EditorApplication.QueuePlayerLoopUpdate();
            SceneView.RepaintAll();
            foreach (var window in Resources.FindObjectsOfTypeAll<EditorWindow>().Where(w=>w.GetType().Name=="GameView")) window.Repaint();
        }
    }
}
