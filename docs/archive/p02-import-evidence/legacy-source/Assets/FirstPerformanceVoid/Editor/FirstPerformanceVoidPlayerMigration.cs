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
    /// <summary>One-time, pose-preserving Player setup for the currently loaded lounge scene.</summary>
    public static class FirstPerformanceVoidPlayerMigration
    {
        [MenuItem("Tools/DAZ Pose/First Performance Void/Migrate to Directable Player View")]
        public static void MigrateLoadedScene()
        {
            Scene scene = SceneManager.GetSceneByPath(FirstPerformanceVoidBuilder.ScenePath);
            if (!scene.IsValid() || !scene.isLoaded)
                throw new InvalidOperationException("Open FirstPerformanceVoid before running the Player View migration.");

            GameObject roomObject = scene.GetRootGameObjects().SingleOrDefault(go => go.name == "FirstPerformanceVoid");
            if (roomObject == null) throw new InvalidOperationException("The loaded lounge has no FirstPerformanceVoid root.");
            Transform room = roomObject.transform;
            FirstPerformanceVoidControls controls = room.GetComponent<FirstPerformanceVoidControls>();
            SuccubusPerformer performer = room.GetComponentInChildren<SuccubusPerformer>(true);
            Camera[] cameras = room.GetComponentsInChildren<Camera>(true)
                .Where(camera => camera != null && camera.CompareTag("MainCamera")).ToArray();
            if (controls == null) throw new InvalidOperationException("The loaded lounge has no FirstPerformanceVoidControls component.");
            if (performer == null) throw new InvalidOperationException("The loaded lounge has no SuccubusPerformer.");
            if (cameras.Length != 1) throw new InvalidOperationException("Expected exactly one MainCamera in the loaded lounge; found " + cameras.Length + ".");
            Camera camera = cameras[0];

            Transform laraHead = FindLaraHead(performer);
            if (laraHead == null) throw new InvalidOperationException("Could not find Lara's animated head bone for FaceViewTarget.");
            PlayerController player = camera.GetComponentInParent<PlayerController>();
            Transform viewRig;
            Transform headPose;

            if (player == null)
            {
                if (Vector3.Distance(camera.transform.localScale, Vector3.one) > 0.0001f)
                    throw new InvalidOperationException("MainCamera has a non-identity local scale. The identity camera hierarchy cannot preserve that scale; no migration was applied.");
                CreatePlayerHierarchy(room, camera, out player, out viewRig, out headPose);
            }
            else
            {
                headPose = camera.transform.parent;
                viewRig = headPose != null ? headPose.parent : null;
                if (headPose == null || headPose.name != "HeadPose" || viewRig == null || viewRig.name != "ViewRig"
                    || player.transform.name != "Player" || viewRig.parent != player.transform)
                    throw new InvalidOperationException("MainCamera is already under a PlayerController but not in Player/ViewRig/HeadPose; migration stopped without rearranging it.");
                player.Configure(viewRig, headPose, camera);
            }

            Transform faceViewTarget = EnsureFaceViewTarget(laraHead, useUndo: true);
            PerformerSeat seat = room.GetComponentInChildren<PerformerSeat>(true);
            EnsureViewMarkers(room, camera, faceViewTarget, seat,
                out Transform wide, out Transform lara, out Transform lounge, useUndo: true);
            RebindGazeTargets(performer, player.HeadTransform);
            controls.ConfigurePlayerView(player, wide, lara, lounge, faceViewTarget);

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException("Unity could not save the migrated FirstPerformanceVoid scene.");

            Debug.Log("P0D_PLAYER_MIGRATION_COMPLETE: reparented the existing MainCamera under Player/ViewRig/HeadPose while preserving its world pose and camera components; saved only the currently loaded FirstPerformanceVoid scene.", roomObject);
        }

        internal static Transform FindLaraHead(SuccubusPerformer performer)
        {
            Animator animator = performer.GetComponent<Animator>();
            if (animator == null) animator = performer.GetComponentInChildren<Animator>(true);
            if (animator == null) return null;
            if (animator.isHuman)
            {
                Transform humanoidHead = animator.GetBoneTransform(HumanBodyBones.Head);
                if (humanoidHead != null) return humanoidHead;
            }
            return animator.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(transform => string.Equals(transform.name, "head", StringComparison.OrdinalIgnoreCase));
        }

        internal static Transform EnsureFaceViewTarget(Transform laraHead, bool useUndo = false)
        {
            Transform target = laraHead.Find("FaceViewTarget");
            if (target != null) return target;

            Transform elsewhere = laraHead.GetComponentInParent<SuccubusPerformer>()
                ?.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(transform => transform.name == "FaceViewTarget");
            if (elsewhere != null)
            {
                if (useUndo) Undo.SetTransformParent(elsewhere, laraHead, "Attach FaceViewTarget to Lara head");
                else elsewhere.SetParent(laraHead, true);
                return elsewhere;
            }

            var targetObject = new GameObject("FaceViewTarget");
            if (useUndo) Undo.RegisterCreatedObjectUndo(targetObject, "Create Lara FaceViewTarget");
            targetObject.transform.SetParent(laraHead, false);
            targetObject.transform.localScale = Vector3.one;
            targetObject.transform.localRotation = Quaternion.identity;

            Transform leftEye = FindNamedDescendant(laraHead, "lEye");
            Transform rightEye = FindNamedDescendant(laraHead, "rEye");
            Vector3 faceCenter = leftEye != null && rightEye != null
                ? (leftEye.position + rightEye.position) * 0.5f
                : laraHead.position + laraHead.up * 0.04f + laraHead.forward * 0.07f;
            targetObject.transform.position = faceCenter;
            return targetObject.transform;
        }

        internal static void EnsureViewMarkers(Transform room, Camera camera, Transform faceViewTarget,
            PerformerSeat seat, out Transform wide, out Transform lara, out Transform lounge, bool useUndo = false)
        {
            Transform markerRoot = room.Find("PerformanceMarkers");
            if (markerRoot == null) markerRoot = CreateChild(room, "PerformanceMarkers", useUndo);
            Transform playerViewRoot = markerRoot.Find("PlayerView");
            if (playerViewRoot == null) playerViewRoot = CreateChild(markerRoot, "PlayerView", useUndo);

            wide = GetOrCreateMarker(playerViewRoot, "ViewMark_Wide", useUndo, out bool wideCreated);
            lara = GetOrCreateMarker(playerViewRoot, "ViewMark_Lara", useUndo, out bool laraCreated);
            lounge = GetOrCreateMarker(playerViewRoot, "ViewMark_Lounge", useUndo, out bool loungeCreated);

            if (wideCreated) wide.position = room.TransformPoint(new Vector3(0f, 1.65f, -6f));
            Vector3 towardCamera = camera.transform.position - faceViewTarget.position;
            if (towardCamera.sqrMagnitude < 0.01f) towardCamera = Vector3.forward;
            towardCamera.Normalize();
            if (laraCreated) lara.position = faceViewTarget.position + towardCamera * 2.6f;

            Transform seatTarget = seat != null && seat.SeatAnchor != null ? seat.SeatAnchor : seat != null ? seat.transform : null;
            Vector3 loungeCenter = seatTarget != null ? seatTarget.position : faceViewTarget.position;
            Vector3 loungeDirection = camera.transform.position - loungeCenter;
            if (loungeDirection.sqrMagnitude < 0.01f) loungeDirection = towardCamera;
            loungeDirection.Normalize();
            if (loungeCreated) lounge.position = loungeCenter + loungeDirection * 3.0f + Vector3.up * 0.18f;
        }

        internal static void RebindGazeTargets(SuccubusPerformer performer, Transform playerHead)
        {
            foreach (MonoBehaviour component in performer.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (component == null) continue;
                var serialized = new SerializedObject(component);
                SerializedProperty gazeTarget = serialized.FindProperty("gazeTarget");
                if (gazeTarget == null || gazeTarget.propertyType != SerializedPropertyType.ObjectReference
                    || gazeTarget.objectReferenceValue == playerHead) continue;
                Undo.RecordObject(component, "Point Lara gaze controls at Player HeadPose");
                gazeTarget.objectReferenceValue = playerHead;
                serialized.ApplyModifiedProperties();
            }
        }

        private static void CreatePlayerHierarchy(Transform room, Camera camera, out PlayerController player,
            out Transform viewRig, out Transform headPose)
        {
            Vector3 oldPosition = camera.transform.position;
            Quaternion oldRotation = camera.transform.rotation;
            Vector3 oldScale = camera.transform.lossyScale;
            Transform oldParent = camera.transform.parent;
            Vector3 oldLocalPosition = camera.transform.localPosition;
            Quaternion oldLocalRotation = camera.transform.localRotation;
            Vector3 oldLocalScale = camera.transform.localScale;

            var playerObject = new GameObject("Player");
            Undo.RegisterCreatedObjectUndo(playerObject, "Create directable Player");
            playerObject.transform.SetParent(room, false);
            playerObject.transform.position = oldPosition;
            playerObject.transform.rotation = Quaternion.identity;
            playerObject.transform.localScale = Vector3.one;

            GameObject rigObject = CreateChild(playerObject.transform, "ViewRig").gameObject;
            viewRig = rigObject.transform;
            viewRig.position = oldPosition;
            viewRig.rotation = oldRotation;
            viewRig.localScale = Vector3.one;
            headPose = CreateChild(viewRig, "HeadPose");
            headPose.localPosition = Vector3.zero;
            headPose.localRotation = Quaternion.identity;
            headPose.localScale = Vector3.one;

            Undo.RecordObject(camera.transform, "Reparent the existing MainCamera under Player");
            Undo.SetTransformParent(camera.transform, headPose, "Reparent the existing MainCamera under Player");
            camera.transform.localPosition = Vector3.zero;
            camera.transform.localRotation = Quaternion.identity;
            camera.transform.localScale = Vector3.one;

            bool preserved = Vector3.Distance(camera.transform.position, oldPosition) <= 0.0001f
                && Quaternion.Angle(camera.transform.rotation, oldRotation) <= 0.01f
                && Vector3.Distance(camera.transform.lossyScale, oldScale) <= 0.0001f;
            if (!preserved)
            {
                Undo.SetTransformParent(camera.transform, oldParent, "Restore MainCamera parent after failed migration");
                camera.transform.localPosition = oldLocalPosition;
                camera.transform.localRotation = oldLocalRotation;
                camera.transform.localScale = oldLocalScale;
                Undo.DestroyObjectImmediate(playerObject);
                throw new InvalidOperationException("Reparenting could not preserve MainCamera's world pose and scale; its original parent and local transform were restored.");
            }

            PlayerView view = Undo.AddComponent<PlayerView>(playerObject);
            player = Undo.AddComponent<PlayerController>(playerObject);
            view.Configure(viewRig, headPose, camera);
            player.Configure(viewRig, headPose, camera);
        }

        private static Transform FindNamedDescendant(Transform parent, string name) =>
            parent.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(transform => string.Equals(transform.name, name, StringComparison.OrdinalIgnoreCase));

        private static Transform GetOrCreateMarker(Transform parent, string name, bool useUndo, out bool created)
        {
            Transform existing = parent.Find(name);
            if (existing != null)
            {
                created = false;
                return existing;
            }
            created = true;
            return CreateChild(parent, name, useUndo);
        }

        private static Transform CreateChild(Transform parent, string name, bool useUndo = false)
        {
            var child = new GameObject(name);
            if (useUndo) Undo.RegisterCreatedObjectUndo(child, "Create " + name);
            child.transform.SetParent(parent, false);
            return child.transform;
        }
    }
}
