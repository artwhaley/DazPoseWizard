using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DazPose.Player;
using DazPose.Story;
using DazPose.Story.Language;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DazPose.Editor.Studio
{
    // T02's read-only probe. This entry point never saves or plays the scene and runs only in the identified batch-mode sandbox.
    public static class T02MountsProbe
    {
        [Serializable] sealed class Identity { public string sourceProject; public string sandboxProject; }
        [Serializable] sealed class MountReport
        {
            public string height, error;
            public Vector3 localPositionInBody, worldOffsetFromHeadAtPlayerStart;
            public float metersAboveHeadAtPlayerStart, lookDownDegreesAtPlayerStart;
        }
        [Serializable] sealed class Report
        {
            public string scene;
            public bool compiled, sceneDirtyAfterProbe, programStillBindsCameraStart, setupCallsCameraStartAt;
            public string[] contentDiagnostics, locations;
            public MountReport[] mounts;
        }

        public static void Run()
        {
            var root = Path.GetFullPath(Path.Combine(Application.dataPath, "..")).TrimEnd('\\', '/');
            var marker = Path.Combine(root, ".dazposewizard-sandbox.json");
            if (!Application.isBatchMode || !File.Exists(marker))
                throw new InvalidOperationException("T02 probe requires the identified batch-mode sandbox.");
            var identity = JsonUtility.FromJson<Identity>(File.ReadAllText(marker));
            if (!string.Equals(root, identity.sandboxProject, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(root, identity.sourceProject, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("T02 sandbox identity mismatch.");

            var scene = EditorSceneManager.OpenScene("Assets/Scenes/FirstContact.unity", OpenSceneMode.Single);
            var hosts = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<StorySceneBindings>(true)).ToArray();
            if (hosts.Length != 1) throw new InvalidOperationException("Expected one FirstContact story binding host.");
            var host = hosts[0];
            var story = AssetDatabase.LoadAssetAtPath<CompiledStoryAsset>("Assets/Production/Story/Programs/FirstContact.dazstory");
            bool compiled = story != null && story.IsCompiled;
            var content = story == null || story.content == null || story.program == null
                ? new[] { "FirstContact compiled content unavailable." }
                : story.content.Diagnostics(story.program, host);
            var locations = host.locations.Select(row => row.id + (row.placement != null ? " (resolves)" : " (unassigned)")).ToArray();
            bool bindsCameraStart = compiled && story.program.bindings.Any(use => use.symbol == "camera_start");
            bool setupCalls = compiled && story.program.instructions.Any(i => i.code == StoryCode.Call && i.name == "Camera.StartAt");

            var mounts = new List<MountReport>();
            var body = host.playerBody;
            var bodyMounts = body != null ? body.GetComponent<PlayerCameraMounts>() : null;
            var head = body != null ? body.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "head") : null;
            var start = host.locations.FirstOrDefault(row => row.id == "player_start")?.placement;
            foreach (PlayerCameraHeight height in Enum.GetValues(typeof(PlayerCameraHeight)))
            {
                var row = new MountReport { height = height.ToString() };
                try
                {
                    if (bodyMounts == null) throw new InvalidOperationException("The player body has no PlayerCameraMounts component.");
                    if (head == null || start == null) throw new InvalidOperationException("Missing head bone or player_start mark.");
                    var mount = bodyMounts.Mount(height);
                    // What PlayerBody.StartHiddenAt(player_start) will do: put the body root on the mark, mounts follow.
                    var bodyToWorldAtStart = Matrix4x4.TRS(start.position, start.rotation, body.transform.lossyScale);
                    Vector3 Place(Vector3 world) => bodyToWorldAtStart.MultiplyPoint3x4(body.transform.InverseTransformPoint(world));
                    Vector3 headAtStart = Place(head.position);
                    Vector3 mountAtStart = Place(mount.position);
                    Vector3 forwardAtStart = start.rotation * body.transform.InverseTransformDirection(mount.forward);
                    row.localPositionInBody = mount.localPosition;
                    row.worldOffsetFromHeadAtPlayerStart = mountAtStart - headAtStart;
                    row.metersAboveHeadAtPlayerStart = row.worldOffsetFromHeadAtPlayerStart.y;
                    row.lookDownDegreesAtPlayerStart = -Mathf.Asin(Mathf.Clamp(forwardAtStart.normalized.y, -1f, 1f)) * Mathf.Rad2Deg;
                }
                catch (Exception error) { row.error = error.Message; }
                mounts.Add(row);
            }

            var report = new Report { scene = scene.path, compiled = compiled, contentDiagnostics = content, locations = locations,
                mounts = mounts.ToArray(), programStillBindsCameraStart = bindsCameraStart, setupCallsCameraStartAt = setupCalls,
                sceneDirtyAfterProbe = scene.isDirty };
            var json = JsonUtility.ToJson(report, true);
            Directory.CreateDirectory(Path.Combine(root, "TestOutput", "T02"));
            File.WriteAllText(Path.Combine(root, "TestOutput", "T02", "mounts-probe.json"), json);
            Debug.Log("T02_MOUNTS_PROBE " + json);
        }
    }
}
