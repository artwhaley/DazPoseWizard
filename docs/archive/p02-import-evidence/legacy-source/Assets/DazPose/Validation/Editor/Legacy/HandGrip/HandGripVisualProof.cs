using System;
using System.IO;
using DazPose.Performer;
using DazPose.Performer.HandGrip;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DazPose.Editor.HandGrip
{
    public static class HandGripVisualProof
    {
        public static void Run()
        {
            if (!Application.isBatchMode)
                throw new InvalidOperationException("Visual proof requires Unity batch mode.");
            try
            {
                EditorSceneManager.OpenScene(HandGripAcceptanceSetup.AcceptanceScenePath);
                var performer = UnityEngine.Object.FindAnyObjectByType<SuccubusPerformer>();
                var animator = performer.GetComponent<Animator>();
                var profile = AssetDatabase.LoadAssetAtPath<HandGripRigProfile>(HandGripAcceptanceSetup.ProfilePath);
                Transform hand = animator.transform.Find(profile.HandPath);
                var host = new GameObject("Hand Visual Proof Camera");
                Camera camera = host.AddComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.12f, 0.12f, 0.12f);
                camera.nearClipPlane = 0.005f; camera.farClipPlane = 5f; camera.fieldOfView = 35f;
                Vector3 center = hand.TransformPoint(profile.PalmAnchorLocalPosition);
                Quaternion anchor = hand.rotation * profile.PalmAnchorLocalRotation;
                Vector3 outward = anchor * Vector3.up;
                camera.transform.position = center + outward * 0.36f + anchor * Vector3.forward * 0.1f;
                camera.transform.rotation = Quaternion.LookRotation(center - camera.transform.position, anchor * Vector3.forward);
                var target = new RenderTexture(1024, 1024, 24);
                camera.targetTexture = target;
                Directory.CreateDirectory("TestOutput/TargetDrivenGrip/visual");
                var skins = animator.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                var proxies = new GameObject[skins.Length];
                var meshes = new Mesh[skins.Length];
                for (int index = 0; index < skins.Length; index++)
                {
                    proxies[index] = new GameObject("Baked visual proof");
                    proxies[index].transform.SetPositionAndRotation(skins[index].transform.position, skins[index].transform.rotation);
                    proxies[index].transform.localScale = skins[index].transform.lossyScale;
                    meshes[index] = new Mesh();
                    proxies[index].AddComponent<MeshFilter>().sharedMesh = meshes[index];
                    proxies[index].AddComponent<MeshRenderer>().sharedMaterials = skins[index].sharedMaterials;
                }
                for (int pose = 0; pose < 7; pose++)
                {
                    foreach (var digit in profile.Digits)
                    for (int joint = 0; joint < digit.JointPaths.Length; joint++)
                    {
                        bool close = pose == 1 || pose >= 2 && (int)digit.Digit == pose - 2;
                        animator.transform.Find(digit.JointPaths[joint]).localRotation = close
                            ? digit.ClosedLocalRotations[joint] : digit.OpenLocalRotations[joint];
                    }
                    for (int index = 0; index < skins.Length; index++)
                    {
                        skins[index].BakeMesh(meshes[index]);
                        skins[index].enabled = false;
                    }
                    camera.Render();
                    RenderTexture.active = target;
                    var image = new Texture2D(1024, 1024, TextureFormat.RGB24, false);
                    image.ReadPixels(new Rect(0, 0, 1024, 1024), 0, 0); image.Apply();
                    File.WriteAllBytes("TestOutput/TargetDrivenGrip/visual/" + pose + ".png", image.EncodeToPNG());
                    UnityEngine.Object.DestroyImmediate(image);
                }
                for (int index = 0; index < skins.Length; index++)
                { UnityEngine.Object.DestroyImmediate(proxies[index]); UnityEngine.Object.DestroyImmediate(meshes[index]); }
                RenderTexture.active = null; camera.targetTexture = null;
                UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(host);
                EditorApplication.Exit(0);
            }
            catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
        }
    }
}
