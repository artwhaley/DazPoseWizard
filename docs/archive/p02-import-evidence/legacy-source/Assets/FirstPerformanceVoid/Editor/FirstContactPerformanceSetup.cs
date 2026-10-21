using System;
using System.Linq;
using DazPose.Performer;
using DazPose.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DazPose.FirstPerformanceVoid.Editor
{
    public static class FirstContactPerformanceSetup
    {
        private const string ScenePath = "Assets/Scenes/FirstPerformanceVoid.unity";

        [MenuItem("Tools/DAZ Pose/First Performance Void/Install First Contact Performance")]
        public static void Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Install First Contact in Edit Mode.");
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            if (!scene.IsValid() || !scene.isLoaded)
                throw new InvalidOperationException("Open the current FirstPerformanceVoid scene before installing First Contact.");

            FirstPerformanceVoidControls controls = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<FirstPerformanceVoidControls>(true)).SingleOrDefault();
            if (controls == null) throw new InvalidOperationException("The lounge has no room controls.");
            var controlData = new SerializedObject(controls);
            SuccubusPerformer performer = Read<SuccubusPerformer>(controlData, "performer");
            PlayerController player = Read<PlayerController>(controlData, "playerController");
            Transform face = Read<Transform>(controlData, "laraFaceViewTarget");
            Transform laraView = Read<Transform>(controlData, "viewMarkLara");
            Transform loungeView = Read<Transform>(controlData, "viewMarkLounge");
            if (performer == null || player == null || face == null || laraView == null || loungeView == null)
                throw new InvalidOperationException("The current lounge must already have its migrated Player View and wired view markers.");
            foreach (Component component in new Component[] { performer, player, face, laraView, loungeView })
                if (component.gameObject.scene != scene)
                    throw new InvalidOperationException(component.name + " must belong to the loaded lounge scene.");
            Transform camera = player.View != null && player.View.MainCamera != null ? player.View.MainCamera.transform : null;
            if (camera == null || camera.parent == null || camera.parent.name != "HeadPose"
                || camera.parent.parent == null || camera.parent.parent.name != "ViewRig")
                throw new InvalidOperationException("The existing Player/ViewRig/HeadPose/MainCamera hierarchy is incomplete.");

            PerformerPoseSmokeHarness smoke = performer.GetComponent<PerformerPoseSmokeHarness>();
            if (smoke == null) throw new InvalidOperationException("Lara has no configured speech smoke harness to copy A and B from.");
            var smokeData = new SerializedObject(smoke);
            AudioClip clipA = Read<AudioClip>(smokeData, "speechClipA");
            AudioClip clipB = Read<AudioClip>(smokeData, "speechClipB");
            PerformerSeat seat = Read<PerformerSeat>(smokeData, "seatingTestSeat");
            if (clipA == null || clipB == null) throw new InvalidOperationException("Assign the existing Speech A and B clips on Lara's smoke harness first.");
            if (seat == null || seat.gameObject.scene != scene || seat.SeatAnchor == null || seat.ApproachAnchor == null
                || seat.SeatingProfile == null)
                throw new InvalidOperationException("Lara's existing lounge seat is not configured in this scene.");
            if (!seat.SeatingProfile.IsReady(out string reason)) throw new InvalidOperationException(reason);

            Transform markers = controls.transform.Find("PerformanceMarkers");
            if (markers == null) throw new InvalidOperationException("The lounge has no PerformanceMarkers group.");
            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Install First Contact Performance");
            try
            {
                Transform firstContactMarks = Child(markers, "FirstContact", out _);
                Transform close = Child(firstContactMarks, "LaraCloseMark", out bool closeCreated);
                Transform final = Child(firstContactMarks, "ViewMark_Final", out bool finalCreated);
                if (closeCreated)
                {
                    Vector3 towardStart = Vector3.ProjectOnPlane(performer.transform.position - laraView.position, Vector3.up);
                    if (towardStart.sqrMagnitude < 0.0001f) towardStart = -performer.transform.forward;
                    Vector3 closePosition = laraView.position + towardStart.normalized * 2f;
                    closePosition.y = performer.transform.position.y;
                    close.position = closePosition;
                    Vector3 facing = Vector3.ProjectOnPlane(laraView.position - closePosition, Vector3.up);
                    close.rotation = Quaternion.LookRotation(facing.normalized, Vector3.up);
                }
                if (finalCreated)
                {
                    Vector3 push = Vector3.ProjectOnPlane(seat.SeatAnchor.position - loungeView.position, Vector3.up);
                    if (push.sqrMagnitude < 0.0001f)
                        throw new InvalidOperationException("ViewMark_Lounge is directly over the seat; tune that composition before creating the final push.");
                    final.position = loungeView.position + push.normalized * 0.55f;
                    final.rotation = loungeView.rotation;
                }

                FirstContactPerformance performance = controls.GetComponent<FirstContactPerformance>();
                if (performance == null) performance = Undo.AddComponent<FirstContactPerformance>(controls.gameObject);
                Undo.RecordObject(performance, "Configure First Contact references");
                performance.Configure(performer, player, seat, face, close, laraView, loungeView, final, clipA, clipB);
                Undo.RecordObject(controls, "Wire First Contact panel");
                controls.ConfigureFirstContact(performance);
                EditorUtility.SetDirty(performance);
                EditorUtility.SetDirty(controls);
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene))
                    throw new InvalidOperationException("Could not save the loaded lounge scene.");
                Undo.CollapseUndoOperations(undoGroup);
                Debug.Log("FIRST_CONTACT_INSTALLED: saved only the loaded lounge scene."
                    + "\nLaraCloseMark: " + close.position.ToString("F3") + ", facing " + close.forward.ToString("F3")
                    + "\nViewMark_Final: " + final.position.ToString("F3")
                    + "\nSpeech A: " + AssetDatabase.GetAssetPath(clipA) + " (" + clipA.length.ToString("F3") + "s)"
                    + "\nSpeech B: " + AssetDatabase.GetAssetPath(clipB) + " (" + clipB.length.ToString("F3") + "s)"
                    + "\nExisting camera, Player hierarchy, view markers, seat, lights, smoke and materials preserved.", performance);
            }
            catch
            {
                Undo.RevertAllDownToGroup(undoGroup);
                throw;
            }
        }

        private static T Read<T>(SerializedObject source, string propertyName) where T : UnityEngine.Object =>
            source.FindProperty(propertyName)?.objectReferenceValue as T;

        private static Transform Child(Transform parent, string name, out bool created)
        {
            Transform child = parent.Find(name);
            created = child == null;
            if (!created) return child;
            var item = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(item, "Create " + name);
            item.transform.SetParent(parent, false);
            return item.transform;
        }
    }
}
