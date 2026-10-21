using System;
using System.IO;
using System.Linq;
using System.Text;
using DazPose.Player;
using UnityEditor;
using UnityEngine;

namespace DazPose.Editor.Studio
{
    // Read-only sandbox probe: which skeleton does the visible body use, and does breathing move its skin?
    public static class T02BreathingProbe
    {
        static string Path(Transform t, Transform root) { var s = t.name; for (var p = t.parent; p != null && p != root.parent; p = p.parent) s = p.name + "/" + s; return s; }

        public static void Run()
        {
            var root = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "..")).TrimEnd('\\', '/');
            if (!Application.isBatchMode || !File.Exists(System.IO.Path.Combine(root, ".dazposewizard-sandbox.json")))
                throw new InvalidOperationException("T02 probe requires the identified batch-mode sandbox.");
            var report = new StringBuilder();
            var body = PrefabUtility.LoadPrefabContents("Assets/Production/Player/PlayerBody.prefab");
            try
            {
                var all = body.GetComponentsInChildren<Transform>(true);
                var renderers = body.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                report.AppendLine("Renderers:");
                foreach (var r in renderers)
                    report.AppendLine("  " + Path(r.transform, body.transform) + " active=" + r.gameObject.activeInHierarchy + " enabled=" + r.enabled + " bones=" + (r.bones?.Length ?? 0) + " rootBone=" + (r.rootBone != null ? Path(r.rootBone, body.transform) : "none"));
                foreach (var name in new[] { "hip", "pelvis", "abdomenLower", "abdomenUpper", "chestLower", "chestUpper" })
                {
                    report.AppendLine(name + ":");
                    foreach (var t in all.Where(x => x.name == name))
                    {
                        var users = renderers.Where(r => r.bones != null && r.bones.Contains(t)).Select(r => r.name).ToArray();
                        report.AppendLine("  " + Path(t, body.transform) + " skinnedBy=[" + string.Join(", ", users) + "]");
                    }
                }
                var breathing = body.GetComponent<PlayerBreathing>();
                report.AppendLine("PlayerBreathing present=" + (breathing != null) + (breathing != null ? " bpm=" + breathing.BreathsPerMinute + " strength=" + breathing.Strength : ""));
                var presence = body.GetComponent<PlayerBodyPresence>();
                report.AppendLine("Presence startsHidden=" + presence.StartsHidden + " stablyHidden=" + presence.IsStablyHidden);
                if (breathing != null)
                {
                    breathing.Strength = 10f;
                    breathing.Prepare();
                    foreach (var r in renderers.Where(r => r.sharedMesh != null))
                    {
                        var rest = new Mesh(); r.BakeMesh(rest, true); var a = rest.vertices;
                        breathing.ApplyAtCycles(0.5f);
                        var heaved = new Mesh(); r.BakeMesh(heaved, true); var b = heaved.vertices;
                        breathing.RestoreRest();
                        float max = 0; for (int i = 0; i < Math.Min(a.Length, b.Length); i++) max = Mathf.Max(max, Vector3.Distance(a[i], b[i]));
                        report.AppendLine("  skin response at strength 10, full inhale: " + r.name + " max vertex move " + max.ToString("F5") + " m over " + a.Length + " verts");
                    }
                }
            }
            finally { PrefabUtility.UnloadPrefabContents(body); }
            Directory.CreateDirectory(System.IO.Path.Combine(root, "TestOutput", "T02"));
            File.WriteAllText(System.IO.Path.Combine(root, "TestOutput", "T02", "breathing-probe.txt"), report.ToString());
            Debug.Log("T02_BREATHING_PROBE\n" + report);
        }
    }
}
