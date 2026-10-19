using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DazPose.Editor.Studio
{
    /// <summary>Art-operated camera-only feasibility check. No product preview lease, sampling, session, or custom rendering.</summary>
    public sealed class NativeDirectingG1Window : EditorWindow
    {
        const string DiagnosticVersion = "N01-G1-v1";
        const string FixtureMarker = ".native-directing-fixture.json";
        [Serializable] sealed class CodeFile { public string path = "", sha256 = ""; }
        [Serializable] sealed class Fixture
        {
            public int schemaVersion = 0;
            public string fixtureId = "", sourceProject = "", fixtureProject = "", scenePath = "", sourceRevision = "", codeHash = "", sceneBaselineSha256 = "";
            public CodeFile[] codeFiles = Array.Empty<CodeFile>();
        }
        [Serializable] internal sealed class ObservedObject { public string id, path, type, json; }
        [Serializable] sealed class OverrideRow { public string target, propertyPath, value, objectReference; }
        [Serializable] sealed class Overrides { public OverrideRow[] properties; }
        [Serializable] internal sealed class Snapshot
        {
            public bool sceneDirty;
            public int undoGroup;
            public string sceneSha256;
            public Vector3 cameraPosition, cameraForward;
            public ObservedObject[] objects;
        }
        [Serializable] internal sealed class Difference { public string id, path, before, after; }
        [Serializable] sealed class Report
        {
            public string diagnosticVersion, timestampUtc, gate, nativeResult, projectPath, scenePath, fixtureId, sourceRevision, codeHash, unityVersion;
            public string operatorRole, propertyFamily, exception, savePolicy, shiftedScreenshot, restoredScreenshot;
            public bool codeVerified, shifted, shiftedGameObservedByArt, restoredGameObservedByArt, restorationMatches, sceneDirtyPreserved, savedSceneBytesPreserved, saveAttemptIntercepted;
            public string playerRoot, viewRig, headPose, mainCamera, lara, playerBody;
            public Snapshot baseline, shiftedState, current;
            public Difference[] restoredDifferences;
        }

        [SerializeField] bool shifted;
        [SerializeField] Transform heldRoot, heldRig;
        [SerializeField] Vector3 originalRootLocalPosition;
        [SerializeField] Quaternion originalRigLocalRotation;
        NativeStageContext context;
        Fixture fixture;
        Snapshot baseline, shiftedState;
        bool shiftObserved, restoreObserved, saveIntercepted;
        string failure, status = "Validate the isolated fixture, then Shift -> observe Game -> Restore -> observe Game -> Export.", shiftedScreenshot = "", restoredScreenshot = "", reportDirectory;
        Vector2 scroll;

        [MenuItem("DazPose/Story/Native Directing Diagnostics/G1 Camera Proof")]
        public static void Open()
        {
            var window = GetWindow<NativeDirectingG1Window>("G1 Camera Proof");
            window.minSize = new Vector2(520, 440);
            window.Show();
        }

        [MenuItem("DazPose/Story/Native Directing Diagnostics/Read-only Status and Export Report")]
        public static void ReadOnlyStatus()
        {
            var window = GetWindow<NativeDirectingG1Window>("G1 Camera Proof");
            window.Show();
            window.Guard(() => { window.ValidateContext(); window.Export(); });
        }

        void OnEnable()
        {
            AssemblyReloadEvents.beforeAssemblyReload += BeforeReload;
            EditorSceneManager.sceneClosing += SceneClosing;
            EditorSceneManager.sceneSaving += SceneSaving;
            EditorSceneManager.activeSceneChangedInEditMode += SceneChanged;
            EditorApplication.playModeStateChanged += PlayChanged;
            Undo.undoRedoPerformed += UndoRedo;
            if (shifted) Restore("Recovered G1 shift after window reload");
        }

        void OnDisable()
        {
            AssemblyReloadEvents.beforeAssemblyReload -= BeforeReload;
            EditorSceneManager.sceneClosing -= SceneClosing;
            EditorSceneManager.sceneSaving -= SceneSaving;
            EditorSceneManager.activeSceneChangedInEditMode -= SceneChanged;
            EditorApplication.playModeStateChanged -= PlayChanged;
            Undo.undoRedoPerformed -= UndoRedo;
            Restore("G1 panel closed");
        }
        void BeforeReload() => Restore("Assembly reload");
        void UndoRedo() => Restore("Undo/Redo interrupted G1; repeat the proof after restoring");
        void SceneClosing(Scene scene, bool removingScene) { if (heldRoot != null && heldRoot.gameObject.scene == scene) Restore("Scene closing"); }
        void SceneChanged(Scene oldScene, Scene newScene) { Restore("Active scene changed"); context = null; }
        void PlayChanged(PlayModeStateChange state) { if (state == PlayModeStateChange.ExitingEditMode) Restore("User requested Play; G1 restored before transition"); }
        void SceneSaving(Scene scene, string path)
        {
            if (!shifted || heldRoot == null || heldRoot.gameObject.scene != scene) return;
            saveIntercepted = true;
            failure = "Save was requested during G1. The camera was restored before scene serialization. Repeat G1 without saving while shifted.";
            Restore("Save request: restored before serialization; G1 is not the N02 save-safe preview");
            Debug.LogWarning(failure);
        }
        internal static string[] FilterSave(string[] paths)
        {
            var refused = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var window in Resources.FindObjectsOfTypeAll<NativeDirectingG1Window>())
            {
                if (!window.shifted || window.heldRoot == null) continue;
                string scenePath = window.heldRoot.gameObject.scene.path;
                if (!paths.Any(p => p == scenePath || p == scenePath + ".meta")) continue;
                refused.Add(scenePath); refused.Add(scenePath + ".meta");
                window.saveIntercepted = true;
                window.failure = "Scene save refused while G1 was shifted. Camera restored. Repeat the proof without saving.";
                window.Restore("Blocked asset-save path during G1");
                Debug.LogWarning(window.failure);
            }
            return paths.Where(p => !refused.Contains(p)).ToArray();
        }

        static string ProjectPath => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        static string Normalize(string path) => Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        static string HashFile(string path)
        {
            using (var stream = File.OpenRead(path))
            using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }
        void ValidateContext()
        {
            NativeStageContext.RequireEditMode();
            string marker = Path.Combine(ProjectPath, FixtureMarker);
            if (!File.Exists(marker)) throw new InvalidOperationException("This is not the isolated native-directing project. Open the prepared G1Project; diagnostic refuses the production project.");
            var value = JsonUtility.FromJson<Fixture>(File.ReadAllText(marker));
            if (value == null || value.schemaVersion != 1 || string.IsNullOrEmpty(value.fixtureId)
                || !string.Equals(Normalize(value.fixtureProject), Normalize(ProjectPath), StringComparison.OrdinalIgnoreCase)
                || string.Equals(Normalize(value.sourceProject), Normalize(ProjectPath), StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Invalid independent fixture identity. Recreate/repair the fixture through its source-only preparation tool.");
            if (value.scenePath != "Assets/__NativeDirectingFixtures/G1_FirstContact.unity"
                || SceneManager.GetActiveScene().path != value.scenePath)
                throw new InvalidOperationException("Open only the disposable scene Assets/__NativeDirectingFixtures/G1_FirstContact.unity for G1. Production scenes are refused.");
            if (value.codeFiles == null || value.codeFiles.Length == 0) throw new InvalidOperationException("Fixture code sync manifest is missing.");
            var codeText = new StringBuilder();
            foreach (var file in value.codeFiles.OrderBy(f => f.path, StringComparer.Ordinal))
            {
                string full = Path.GetFullPath(Path.Combine(ProjectPath, file.path));
                if (!file.path.StartsWith("Assets/", StringComparison.Ordinal)
                    || !full.StartsWith(Normalize(Application.dataPath) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                    || !File.Exists(full) || HashFile(full) != file.sha256)
                    throw new InvalidOperationException("Fixture code/meta differs from synchronized implementation: " + file.path + ". Ask the executor to sync current source, then let Unity finish compiling.");
                codeText.Append(file.path).Append('|').Append(file.sha256).Append('\n');
            }
            using (var sha = SHA256.Create())
            {
                string aggregate = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(codeText.ToString()))).Replace("-", "").ToLowerInvariant();
                if (aggregate != value.codeHash) throw new InvalidOperationException("Fixture code manifest hash is inconsistent.");
            }
            var resolved = NativeStageContext.Resolve(NativeStageContext.FindHost());
            if (shifted && context != null && resolved.Host != context.Host) throw new InvalidOperationException("Host changed during G1; Restore before selecting a new host.");
            context = resolved; fixture = value;
            status = "Isolated fixture and current source hashes verified. Camera: " + NativeStageContext.ObjectPath(context.Camera.transform);
        }

        internal static bool HasShift => Resources.FindObjectsOfTypeAll<NativeDirectingG1Window>().Any(w=>w.shifted);

        void Shift()
        {
            if (shifted) throw new InvalidOperationException("Restore the current G1 shift first.");
            if (Resources.FindObjectsOfTypeAll<NativeDirectingG1Window>().Any(w => w != this && w.shifted))
                throw new InvalidOperationException("Another G1 panel owns a camera shift. Restore it before acquiring this one.");
            ValidateContext();
            if (AnimationMode.InAnimationMode()) throw new InvalidOperationException("Stop the Animation Window/Timeline preview manually before G1; no AnimationMode owner is taken.");
            if (SceneManager.sceneCount != 1) throw new InvalidOperationException("For G1, close other scenes manually; exactly one disposable scene must be loaded.");
            var camera = context.Camera;
            if (!camera.enabled || !camera.gameObject.activeInHierarchy || camera.targetTexture != null)
                throw new InvalidOperationException("The saved MainCamera must already be enabled, active, and render to the Game display (targetTexture null). G1 does not repair or activate objects.");
            var cameras = context.Scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Camera>(true))
                .Where(c => c.enabled && c.gameObject.activeInHierarchy && c.targetTexture == null && c.targetDisplay == camera.targetDisplay).ToArray();
            if (cameras.Length != 1 || cameras[0] != camera)
                throw new InvalidOperationException("G1 requires exactly the bound MainCamera rendering to its target display. Inspect competing saved cameras; no camera is silently disabled.");
            if (!EditorUtility.DisplayDialog("G1 camera shift — isolated fixture only",
                "Project: " + ProjectPath + "\nScene: " + context.Scene.path + "\n\nThis holds a temporary 0.75 m sideways shift and 18 degree yaw on the real rig. Observe Game, then Restore. Do not save, edit the rig, or press Play while shifted. No pose/material staging or save-safety acceptance is performed.", "Shift Camera Rig", "Cancel")) return;
            baseline = Capture(context);
            originalRootLocalPosition = context.PlayerRoot.localPosition;
            originalRigLocalRotation = context.ViewRig.localRotation;
            heldRoot = context.PlayerRoot; heldRig = context.ViewRig;
            shiftObserved = restoreObserved = saveIntercepted = false; failure = null;
            shifted = true; // Install recovery ownership before the first temporary write.
            try
            {
                heldRoot.position += context.ViewRig.right * .75f;
                heldRig.rotation = Quaternion.AngleAxis(18f, Vector3.up) * heldRig.rotation;
                shiftedState = Capture(context);
                status = "SHIFTED — observe the real Game frame, then click Restore Camera Rig. Do not save.";
                NativeStageContext.RefreshViews();
            }
            catch (Exception e) { failure = e.ToString(); Restore("Shift failed; restoring owned transforms"); throw; }
        }

        void Restore(string reason)
        {
            if (!shifted) return;
            try
            {
                if (heldRoot == null || heldRig == null) throw new InvalidOperationException("G1 camera objects were destroyed. Recover the disposable scene from the isolated baseline; production remains untouched.");
                heldRoot.localPosition = originalRootLocalPosition;
                heldRig.localRotation = originalRigLocalRotation;
                shifted = false;
                status = reason + ". Observe the restored Game frame, then Export Report.";
                NativeStageContext.RefreshViews();
            }
            catch (Exception e) { failure = e.ToString(); Debug.LogException(e); }
            Repaint();
        }

        [Serializable] sealed class ObjectReferenceJson { public UnityEngine.Object value; }
        static string StableJson(UnityEngine.Object obj)
        {
            // Obtain reference tokens through serialization itself: no obsolete int/EntityId conversions.
            const string pattern="\\\"instanceID\\\"\\s*:\\s*(-?\\d+)";
            var identities=new Dictionary<string,string>();
            using(var serialized=new SerializedObject(obj))
            {
                var property=serialized.GetIterator();
                while(property.Next(true))
                {
                    if(property.propertyType!=SerializedPropertyType.ObjectReference)continue;
                    var reference=property.objectReferenceValue;
                    if(reference==null)continue;
                    var token=Regex.Match(JsonUtility.ToJson(new ObjectReferenceJson{value=reference}),pattern);
                    if(!token.Success)continue;
                    var global=GlobalObjectId.GetGlobalObjectIdSlow(reference);
                    identities[token.Groups[1].Value]=global.identifierType==0?"transient:"+reference.GetEntityId():global.ToString();
                }
            }
            return Regex.Replace(EditorJsonUtility.ToJson(obj),pattern,match=>
            {
                string id=match.Groups[1].Value;
                if(id=="0")return "\"stableObjectID\":\"null\"";
                return identities.TryGetValue(id,out var identity)?"\"stableObjectID\":\""+identity+"\"":match.Value;
            });
        }

        internal static Snapshot Capture(NativeStageContext current)
        {
            var objects = new HashSet<UnityEngine.Object> { current.Host, current.Performance, current.Player, current.View, current.Camera, current.Lara, current.Body };
            foreach (var root in new[] { current.PlayerRoot, current.Lara.transform, current.Body.transform })
            {
                foreach (var t in root.GetComponentsInChildren<Transform>(true)) { objects.Add(t); objects.Add(t.gameObject); }
                foreach (var renderer in root.GetComponentsInChildren<Renderer>(true)) objects.Add(renderer);
                foreach (var behaviour in root.GetComponentsInChildren<MonoBehaviour>(true)) if (behaviour != null) objects.Add(behaviour);
            }
            foreach (var component in current.Camera.GetComponents<Component>()) if (component != null) objects.Add(component);
            var rows = new List<ObservedObject>();
            foreach (var obj in objects)
            {
                var component = obj as Component;
                var go = obj as GameObject;
                rows.Add(new ObservedObject { id = GlobalObjectId.GetGlobalObjectIdSlow(obj).ToString(), type = obj.GetType().FullName,
                    path = NativeStageContext.ObjectPath(component != null ? component.transform : go.transform), json = StableJson(obj) });
            }
            var prefabs = new HashSet<GameObject>();
            foreach (var t in current.Scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)))
            {
                var root = PrefabUtility.GetOutermostPrefabInstanceRoot(t.gameObject);
                if (root != null) prefabs.Add(root);
            }
            foreach (var root in prefabs)
            {
                var modifications = PrefabUtility.GetPropertyModifications(root) ?? Array.Empty<PropertyModification>();
                var properties = modifications.Select(m => new OverrideRow { target = m.target == null ? "null" : GlobalObjectId.GetGlobalObjectIdSlow(m.target).ToString(),
                    propertyPath = m.propertyPath, value = m.value, objectReference = m.objectReference == null ? "null" : GlobalObjectId.GetGlobalObjectIdSlow(m.objectReference).ToString() })
                    .OrderBy(m => m.target, StringComparer.Ordinal).ThenBy(m => m.propertyPath, StringComparer.Ordinal).ToArray();
                rows.Add(new ObservedObject { id = "prefab:" + GlobalObjectId.GetGlobalObjectIdSlow(root), path = NativeStageContext.ObjectPath(root.transform),
                    type = "PrefabOverrides", json = JsonUtility.ToJson(new Overrides { properties = properties }) });
            }
            return new Snapshot { sceneDirty = current.Scene.isDirty, undoGroup = Undo.GetCurrentGroup(), sceneSha256 = HashFile(Path.Combine(ProjectPath, current.Scene.path)),
                cameraPosition = current.Camera.transform.position, cameraForward = current.Camera.transform.forward,
                objects = rows.OrderBy(r => r.id, StringComparer.Ordinal).ToArray() };
        }
        internal static Difference[] Compare(Snapshot before, Snapshot after)
        {
            if (before == null) return Array.Empty<Difference>();
            var a = before.objects.ToDictionary(o => o.id); var b = after.objects.ToDictionary(o => o.id);
            return a.Keys.Union(b.Keys).Where(k => !a.ContainsKey(k) || !b.ContainsKey(k) || a[k].json != b[k].json)
                .Select(k => new Difference { id = k, path = b.ContainsKey(k) ? b[k].path : a[k].path, before = a.ContainsKey(k) ? a[k].json : "<absent>", after = b.ContainsKey(k) ? b[k].json : "<absent>" }).ToArray();
        }
        void Export()
        {
            ValidateContext();
            var now = Capture(context); var differences = Compare(baseline, now);
            bool matches = baseline != null && !shifted && differences.Length == 0;
            bool dirtyMatches = baseline != null && baseline.sceneDirty == now.sceneDirty;
            bool bytesMatch = baseline != null && baseline.sceneSha256 == now.sceneSha256;
            string result = baseline == null || shifted || !shiftObserved || !restoreObserved ? "NOT RUN" :
                matches && dirtyMatches && bytesMatch && failure == null && !saveIntercepted ? "PASS" : "FAIL";
            var report = new Report { diagnosticVersion = DiagnosticVersion, timestampUtc = DateTime.UtcNow.ToString("O"), gate = "G1", nativeResult = result,
                projectPath = ProjectPath, scenePath = context.Scene.path, fixtureId = fixture.fixtureId, sourceRevision = fixture.sourceRevision, codeHash = fixture.codeHash,
                unityVersion = Application.unityVersion, operatorRole = "Art manually clicks and confirms Game observations; executor has not operated Unity",
                propertyFamily = "Camera root position and ViewRig rotation only. No N02 lease, pose/material sampling, or general save-safety claim.",
                savePolicy = "Do not save while G1 is shifted. Asset save is filtered; direct sceneSaving restores before serialization and invalidates this proof.",
                codeVerified = true, shifted = shifted, shiftedGameObservedByArt = shiftObserved, restoredGameObservedByArt = restoreObserved,
                restorationMatches = matches, sceneDirtyPreserved = dirtyMatches, savedSceneBytesPreserved = bytesMatch, saveAttemptIntercepted = saveIntercepted,
                playerRoot = NativeStageContext.ObjectPath(context.PlayerRoot), viewRig = NativeStageContext.ObjectPath(context.ViewRig), headPose = NativeStageContext.ObjectPath(context.HeadPose),
                mainCamera = NativeStageContext.ObjectPath(context.Camera.transform), lara = NativeStageContext.ObjectPath(context.Lara.transform), playerBody = NativeStageContext.ObjectPath(context.Body.transform),
                exception = failure, shiftedScreenshot = shiftedScreenshot, restoredScreenshot = restoredScreenshot, baseline = baseline, shiftedState = shiftedState, current = now, restoredDifferences = differences };
            reportDirectory = Path.Combine(ProjectPath, "Temp/NativeDirectingDiagnostics", "G1_" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff"));
            Directory.CreateDirectory(reportDirectory);
            File.WriteAllText(Path.Combine(reportDirectory, "report.json"), JsonUtility.ToJson(report, true));
            string text = $"G1 {result}\nDiagnostic: {DiagnosticVersion}\nUnity: {Application.unityVersion}\nProject: {ProjectPath}\nScene: {context.Scene.path}\nSource revision: {fixture.sourceRevision}\nCode SHA256: {fixture.codeHash}\nCode/meta verified: true\nArt observed shifted/restored Game frames: {shiftObserved}/{restoreObserved}\nRestoration matches: {matches}\nScene dirty preserved: {dirtyMatches}\nScene bytes preserved: {bytesMatch}\nRestored object differences: {differences.Length}\nSave intercepted: {saveIntercepted}\nException: {failure ?? "none"}\nShifted screenshot: {shiftedScreenshot}\nRestored screenshot: {restoredScreenshot}\n\nExact baseline/shifted/current object, renderer, bone and prefab JSON and differences are in report.json. No executor Unity operation. No N02+ acceptance implied.\n";
            File.WriteAllText(Path.Combine(reportDirectory, "report.txt"), text);
            status = "Exported G1 " + result + " — " + reportDirectory;
            Debug.Log(text + "\nReport: " + reportDirectory);
        }
        void Guard(Action action)
        {
            try { action(); }
            catch (Exception e) { failure = e.ToString(); status = e.Message; Debug.LogException(e); }
            Repaint();
        }
        void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.LabelField("G1 — real saved Game camera", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Project", ProjectPath);
            EditorGUILayout.HelpBox("ART-OPERATED EDIT MODE CHECK. Use the disposable scene in the independent project. No Save, rig editing or Play while shifted. Capture both real Game frames manually. G1 proves camera feasibility only.", shifted ? MessageType.Warning : MessageType.Info);
            if (GUILayout.Button("Validate fixture and bindings (read only)")) Guard(ValidateContext);
            using (new EditorGUI.DisabledScope(shifted)) if (GUILayout.Button("Shift Camera Rig")) Guard(Shift);
            using (new EditorGUI.DisabledScope(!shifted))
            {
                shiftObserved = EditorGUILayout.ToggleLeft("Art: shifted Game frame is visibly different", shiftObserved);
                if (GUILayout.Button("Restore Camera Rig")) Guard(() => Restore("Art clicked Restore Camera Rig"));
            }
            using (new EditorGUI.DisabledScope(shifted || baseline == null))
                restoreObserved = EditorGUILayout.ToggleLeft("Art: restored Game frame matches the baseline", restoreObserved);
            shiftedScreenshot = EditorGUILayout.TextField("Shifted screenshot / notes", shiftedScreenshot);
            restoredScreenshot = EditorGUILayout.TextField("Restored screenshot / notes", restoredScreenshot);
            if (GUILayout.Button("Export Report")) Guard(Export);
            if (!string.IsNullOrEmpty(reportDirectory))
            {
                EditorGUILayout.SelectableLabel(reportDirectory, EditorStyles.textField, GUILayout.Height(36));
                if (GUILayout.Button("Show report folder")) EditorUtility.RevealInFinder(reportDirectory);
            }
            EditorGUILayout.HelpBox(status, failure == null ? MessageType.Info : MessageType.Error);
            EditorGUILayout.EndScrollView();
        }
    }

    // Inactive unless an Art-opened G1 panel owns a shift. Does not run a diagnostic on import.
    public sealed class NativeDirectingG1SaveGuard : AssetModificationProcessor
    {
        static string[] OnWillSaveAssets(string[] paths) => NativeDirectingG1Window.FilterSave(paths);
    }
}
