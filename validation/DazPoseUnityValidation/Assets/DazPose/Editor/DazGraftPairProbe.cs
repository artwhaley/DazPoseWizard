using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DazPose.UnityValidation
{
    // Development proof for a captured endpoint pair; does not modify either FBX or the performer.
    public static class DazGraftPairProbe
    {
        public const string DirectoryPath = "Assets/TestData/GraftPairProbe";
        public const string ScenePath = DirectoryPath + "/GraftPairPreview.unity";
        private const float PositionTolerance = 2e-6f;
        private const float CellSize = 2e-6f;

        [Serializable] private class Manifest
        {
            public string closedAsset, openAsset, closedSHA256, openSHA256;
            public Point[] points;
        }
        [Serializable] private class Point
        {
            public int index, materialMask;
            public Vector3 position, delta, normalDelta;
            public bool graftOnly;
        }
        [Serializable] private class Validation
        {
            public string closedSHA256, openSHA256;
            public int importedClosedVertices, importedOpenVertices, matchedVertices;
            public int openingMovedVertices, correctedGraftBlinkVertices;
            public float maximumPositionMatchError, maximumOpening, maximumEndpointError;
            public bool hostOpeningUnchanged, correctedGraftBlinkZero;
        }

        [MenuItem("Tools/DAZ Pose/Development/Open Graft Pair Preview")]
        public static void OpenPreview()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Build();
            var body = FindBody(UnityEngine.Object.FindObjectsByType<SkinnedMeshRenderer>(FindObjectsSortMode.None));
            Selection.activeGameObject = body.gameObject;
            DazGraftProbeWindow.ShowFor(body);
            var view = SceneView.lastActiveSceneView;
            if (view == null) view = EditorWindow.GetWindow<SceneView>();
            view.sceneLighting = false;
            view.LookAtDirect(new Vector3(0, .95f, .035f), Quaternion.Euler(10, 180, 0), .28f);
            view.Repaint();
        }

        // Batch entry: generated assets and scene stay in the ignored TestData folder.
        public static void Build()
        {
            var manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(DirectoryPath + "/endpoint-manifest.json"));
            if (Hash(manifest.closedAsset) != manifest.closedSHA256 || Hash(manifest.openAsset) != manifest.openSHA256)
                throw new IOException("Graft endpoint assets changed; regenerate the endpoint manifest.");
            var closed = AssetDatabase.LoadAssetAtPath<GameObject>(manifest.closedAsset);
            var opened = AssetDatabase.LoadAssetAtPath<GameObject>(manifest.openAsset);
            var closedBody = FindBody(closed.GetComponentsInChildren<SkinnedMeshRenderer>(true));
            var openBody = FindBody(opened.GetComponentsInChildren<SkinnedMeshRenderer>(true));
            Mesh source = closedBody.sharedMesh;
            Vector3[] vertices = source.vertices;
            var materialMasks = new int[vertices.Length];
            for (int s = 0; s < source.subMeshCount; s++)
                foreach (int index in source.GetIndices(s)) materialMasks[index] |= 1 << s;
            var grid = new Dictionary<Vector3Int, List<Point>>();
            foreach (var point in manifest.points)
            {
                var cell = Cell(point.position);
                if (!grid.TryGetValue(cell, out var list)) grid[cell] = list = new List<Point>();
                list.Add(point);
            }
            var deltas = new Vector3[vertices.Length];
            var normalDeltas = new Vector3[vertices.Length];
            var graftOnly = new bool[vertices.Length];
            var validation = new Validation
            {
                closedSHA256 = manifest.closedSHA256, openSHA256 = manifest.openSHA256,
                importedClosedVertices = vertices.Length, importedOpenVertices = openBody.sharedMesh.vertexCount,
                hostOpeningUnchanged = true
            };
            for (int i = 0; i < vertices.Length; i++)
            {
                var candidates = new List<Point>();
                var center = Cell(vertices[i]);
                for (int x = -1; x <= 1; x++) for (int y = -1; y <= 1; y++) for (int z = -1; z <= 1; z++)
                    if (grid.TryGetValue(center + new Vector3Int(x, y, z), out var list))
                        candidates.AddRange(list.Where(p => (p.position - vertices[i]).sqrMagnitude <= PositionTolerance * PositionTolerance
                            && (p.materialMask & materialMasks[i]) == materialMasks[i]));
                if (candidates.Count == 0) throw new IOException("No raw point match for imported vertex " + i);
                Point match = candidates.OrderBy(p => (p.position - vertices[i]).sqrMagnitude).First();
                if (candidates.Any(p => (p.delta - match.delta).sqrMagnitude > 1e-12f
                    || (p.normalDelta - match.normalDelta).sqrMagnitude > 1e-8f || p.graftOnly != match.graftOnly))
                    throw new IOException("Ambiguous endpoint transfer at imported vertex " + i);
                deltas[i] = match.delta;
                normalDeltas[i] = match.normalDelta;
                graftOnly[i] = match.graftOnly;
                validation.matchedVertices++;
                validation.maximumPositionMatchError = Mathf.Max(validation.maximumPositionMatchError, (match.position - vertices[i]).magnitude);
                if (deltas[i].magnitude > 1e-7f)
                {
                    validation.openingMovedVertices++;
                    validation.maximumOpening = Mathf.Max(validation.maximumOpening, deltas[i].magnitude);
                    if (!graftOnly[i]) validation.hostOpeningUnchanged = false;
                }
            }
            var targetGrid = new Dictionary<Vector3Int, List<Vector3>>();
            foreach (var vertex in openBody.sharedMesh.vertices)
            {
                var key = Cell(vertex);
                if (!targetGrid.TryGetValue(key, out var list)) targetGrid[key] = list = new List<Vector3>();
                list.Add(vertex);
            }
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 target = vertices[i] + deltas[i];
                Vector3Int cell = Cell(target);
                float error = float.PositiveInfinity;
                for (int x = -1; x <= 1; x++) for (int y = -1; y <= 1; y++) for (int z = -1; z <= 1; z++)
                    if (targetGrid.TryGetValue(cell + new Vector3Int(x, y, z), out var list))
                        foreach (var v in list) error = Mathf.Min(error, (target - v).magnitude);
                if (error > PositionTolerance) throw new IOException("Recovered endpoint differs from imported open mesh at vertex " + i);
                validation.maximumEndpointError = Mathf.Max(validation.maximumEndpointError, error);
            }
            var blink = new Vector3[vertices.Length];
            var blinkNormals = new Vector3[vertices.Length];
            var blinkTangents = new Vector3[vertices.Length];
            int blinkIndex = source.GetBlendShapeIndex("Genesis8Female__eCTRLEyesClosedL");
            if (blinkIndex < 0) throw new IOException("Expected left blink probe is absent.");
            source.GetBlendShapeFrameVertices(blinkIndex, 0, blink, blinkNormals, blinkTangents);
            Mesh recovered = UnityEngine.Object.Instantiate(source);
            recovered.name = "Captured graft opening proof";
            recovered.ClearBlendShapes();
            recovered.AddBlendShapeFrame("CapturedOpening", 100, deltas, normalDeltas, null);
            recovered.AddBlendShapeFrame("ExportedBlink", 100, blink, blinkNormals, blinkTangents);
            for (int i = 0; i < blink.Length; i++)
                if (graftOnly[i])
                {
                    if (blink[i].magnitude > 1e-7f) validation.correctedGraftBlinkVertices++;
                    blink[i] = blinkNormals[i] = blinkTangents[i] = Vector3.zero;
                }
            recovered.AddBlendShapeFrame("CorrectedBlink", 100, blink, blinkNormals, blinkTangents);
            var storedOpening = new Vector3[vertices.Length];
            var storedBlink = new Vector3[vertices.Length];
            recovered.GetBlendShapeFrameVertices(0, 0, storedOpening, null, null);
            recovered.GetBlendShapeFrameVertices(2, 0, storedBlink, null, null);
            validation.correctedGraftBlinkZero = true;
            for (int i = 0; i < vertices.Length; i++)
            {
                if (storedOpening[i] != deltas[i]) throw new IOException("Stored opening frame changed during mesh creation.");
                if (graftOnly[i] && storedBlink[i] != Vector3.zero) validation.correctedGraftBlinkZero = false;
                if (!graftOnly[i] && storedBlink[i] != blink[i]) throw new IOException("Corrected blink changed host-face movement.");
            }
            if (!validation.correctedGraftBlinkZero) throw new IOException("Corrected blink still changes the graft.");
            if (!validation.hostOpeningUnchanged) throw new IOException("Opening unexpectedly changes host geometry.");
            string meshPath = DirectoryPath + "/RecoveredBody.asset";
            if (AssetDatabase.LoadAssetAtPath<Mesh>(meshPath) is Mesh existing)
            {
                EditorUtility.CopySerialized(recovered, existing);
                UnityEngine.Object.DestroyImmediate(recovered);
                recovered = existing;
                EditorUtility.SetDirty(existing);
            }
            else AssetDatabase.CreateAsset(recovered, meshPath);
            AssetDatabase.SaveAssets();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var preview = UnityEngine.Object.Instantiate(closed);
            preview.name = "Graft Pair Preview";
            var previewBody = FindBody(preview.GetComponentsInChildren<SkinnedMeshRenderer>(true));
            foreach (var renderer in preview.GetComponentsInChildren<Renderer>(true)) renderer.enabled = renderer == previewBody;
            previewBody.sharedMesh = recovered;
            for (int i = 0; i < recovered.blendShapeCount; i++) previewBody.SetBlendShapeWeight(i, 0);
            EditorSceneManager.SaveScene(scene, ScenePath);
            System.IO.Directory.CreateDirectory("TestOutput/appearance-evidence");
            File.WriteAllText("TestOutput/appearance-evidence/graft-recovery.validation.json", JsonUtility.ToJson(validation, true));
            Debug.Log("GRAFT_RECOVERY_VALIDATED: " + JsonUtility.ToJson(validation));
        }

        private static SkinnedMeshRenderer FindBody(IEnumerable<SkinnedMeshRenderer> renderers) =>
            renderers.Single(r => r.sharedMesh != null && (r.sharedMesh.name.StartsWith("Genesis8Female", StringComparison.Ordinal)
                || r.sharedMesh.name == "Captured graft opening proof"));
        private static Vector3Int Cell(Vector3 p) => new Vector3Int(Mathf.FloorToInt(p.x / CellSize), Mathf.FloorToInt(p.y / CellSize), Mathf.FloorToInt(p.z / CellSize));
        private static string Hash(string path)
        {
            using var algorithm = SHA256.Create();
            using var stream = File.OpenRead(path);
            return BitConverter.ToString(algorithm.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }
    }

    public class DazGraftProbeWindow : EditorWindow
    {
        private SkinnedMeshRenderer body;
        private float opening, blink;
        private bool originalBlink;
        public static void ShowFor(SkinnedMeshRenderer renderer)
        {
            var window = GetWindow<DazGraftProbeWindow>("DAZ Graft Probe");
            window.body = renderer;
            window.opening = window.blink = 0;
            window.originalBlink = false;
            window.Show();
        }
        private void OnGUI()
        {
            EditorGUILayout.LabelField("Captured opening: closed → open", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("This is a development preview of the captured endpoints. Intermediate values interpolate between them.", MessageType.Info);
            if (body == null) { EditorGUILayout.LabelField("Open the graft pair preview to connect the controls."); return; }
            EditorGUI.BeginChangeCheck();
            opening = EditorGUILayout.Slider("Opening", opening, 0, 100);
            blink = EditorGUILayout.Slider("Left blink", blink, 0, 100);
            originalBlink = EditorGUILayout.Toggle("Use exported blink", originalBlink);
            if (EditorGUI.EndChangeCheck())
            {
                body.SetBlendShapeWeight(0, opening);
                body.SetBlendShapeWeight(1, originalBlink ? blink : 0);
                body.SetBlendShapeWeight(2, originalBlink ? 0 : blink);
                SceneView.RepaintAll();
            }
        }
    }
}
