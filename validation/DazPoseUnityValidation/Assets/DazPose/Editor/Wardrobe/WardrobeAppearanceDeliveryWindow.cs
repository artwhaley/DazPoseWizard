using System;
using System.Linq;
using System.Text;
using DazPose.Performer;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DazPose.UnityValidation
{
    public sealed class WardrobeAppearanceDeliveryWindow : EditorWindow
    {
        [SerializeField] private WardrobeCatalog catalog, productionCatalog;
        [SerializeField] private WardrobeConfiguration configuration, productionConfiguration;
        [SerializeField] private PerformerWardrobe canonicalOwner;
        [SerializeField] private SkinnedMeshRenderer canonicalBody;
        [SerializeField] private WardrobeRigManifest manifest;
        [SerializeField] private string gateScenePath;
        [SerializeField] private string report = "Click Prepare / resume Gate A. Completed work is recovered automatically.";
        [SerializeField] private Vector2 scroll;
        [SerializeField] private bool individualSteps;

        [MenuItem("DazPose/Wardrobe/Appearance Delivery")]
        public static void Open()
        {
            var window = GetWindow<WardrobeAppearanceDeliveryWindow>();
            window.titleContent = new GUIContent("Appearance Delivery");
            window.minSize = new Vector2(480, 420);
            window.Show();
        }

        private void OnEnable()
        {
            LoadProductionInputs();
            try
            {
                if (WardrobeCorrectedCandidateBuilder.TryRecover(productionCatalog, productionConfiguration, out var recovered))
                { catalog = recovered.catalog; configuration = recovered.configuration; report = recovered.message; }
                else { catalog = productionCatalog; configuration = productionConfiguration; }
                RecoverManifest();
            }
            catch (Exception error) { report = "Saved delivery input inspection stopped: " + error.Message; }
            if (string.IsNullOrEmpty(gateScenePath)) gateScenePath = WardrobeProtectedCardsFixtureBuilder.GateAScene;
        }

        private void LoadProductionInputs()
        {
            var lara = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Production/Performers/Lara.prefab");
            canonicalOwner = lara != null ? lara.GetComponentInChildren<PerformerWardrobe>(true) : null;
            productionCatalog = canonicalOwner != null ? canonicalOwner.Catalog : null;
            productionConfiguration = canonicalOwner != null ? canonicalOwner.Configuration : null;
            // The configured body is authoritative, including for inactive prefab asset hierarchies.
            canonicalBody = canonicalOwner != null ? canonicalOwner.Body : null;
        }

        private void RecoverManifest()
        {
            manifest = IsStagedRelease ? AssetDatabase.LoadAssetAtPath<WardrobeRigManifest>(
                "Assets/Production/Wardrobe/Rigs/" + catalog.ReleasedGenerationId + "/WardrobeRigManifest.asset") : null;
            if (manifest != null && !manifest.Validate(catalog, out _)) manifest = null;
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Wardrobe Appearance Delivery — Gate A", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Prepare / resume recovers the corrected release, generates its reusable prefabs, and saves the diagnostic scene. Press Play yourself after setup completes.", MessageType.Info);
            EditorGUILayout.LabelField("Production catalog", AssetDatabase.GetAssetPath(productionCatalog));
            EditorGUILayout.LabelField("Staged catalog", AssetDatabase.GetAssetPath(catalog));
            EditorGUILayout.LabelField("Stage", IsStagedRelease ? "Published corrected release — ready to resume" : "Candidate preparation / publication");
            bool missing = productionCatalog == null || productionConfiguration == null || canonicalOwner == null || canonicalBody == null;
            if (missing) EditorGUILayout.HelpBox("Lara.prefab must have PerformerWardrobe with configured catalog, configuration and Body references.", MessageType.Error);
            using (new EditorGUI.DisabledScope(missing || EditorApplication.isPlayingOrWillChangePlaymode))
                if (GUILayout.Button("Prepare / resume Gate A", GUILayout.Height(36))) ResumeGateA();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Inspect inputs")) RunOperation(InspectInputs);
                using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode || AssetDatabase.LoadAssetAtPath<SceneAsset>(gateScenePath) == null))
                    if (GUILayout.Button("Open Gate A scene")) OpenGateScene();
            }
            individualSteps = EditorGUILayout.Foldout(individualSteps, "Individual steps (diagnostics)");
            if (individualSteps)
            {
                using (new EditorGUI.DisabledScope(missing || EditorApplication.isPlayingOrWillChangePlaymode))
                    if (GUILayout.Button("Prepare corrected candidate / recover published release")) RunOperation(PrepareCandidate);
                using (new EditorGUI.DisabledScope(missing || catalog == null || catalog.IsReleased || EditorApplication.isPlayingOrWillChangePlaymode))
                    if (GUILayout.Button("Compile and publish candidate")) RunOperation(PublishCandidate);
                using (new EditorGUI.DisabledScope(missing || !IsStagedRelease || EditorApplication.isPlayingOrWillChangePlaymode))
                    if (GUILayout.Button("Generate reusable prefabs")) RunOperation(GeneratePrefabs);
                using (new EditorGUI.DisabledScope(!IsStagedRelease || manifest == null || EditorApplication.isPlayingOrWillChangePlaymode))
                    if (GUILayout.Button("Create Gate A scene")) RunOperation(CreateGateAScene);
            }
            scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.ExpandHeight(true));
            EditorGUILayout.TextArea(report, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();
        }

        private void ResumeGateA()
        {
            var log = new StringBuilder();
            int step = 0;
            Action<Action> record = operation => { operation(); log.AppendLine(report).AppendLine(); };
            try
            {
                LoadProductionInputs();
                WardrobeAppearanceDeliveryWorkflow.Resume(() => record(PrepareCandidate), () => IsStagedRelease,
                    () => record(PublishCandidate), () => record(GeneratePrefabs), () => record(CreateGateAScene),
                    stage => { log.AppendLine(stage); EditorUtility.DisplayProgressBar("Prepare / resume Gate A", stage, step++ / 4f); });
                log.AppendLine("Gate A setup complete. Click Open Gate A scene, then enter Play yourself. The saved Appearance Gate panel contains the checks.");
            }
            catch (Exception error) { log.AppendLine(error.ToString()).AppendLine("Completed assets are retained. Prepare / resume Gate A retries from those assets."); }
            finally { EditorUtility.ClearProgressBar(); report = log.ToString(); Repaint(); }
        }

        private void RunOperation(Action action)
        { try { action(); } catch (Exception error) { report = error.ToString(); } Repaint(); }

        private void InspectInputs()
        {
            if (catalog == null || configuration == null || canonicalOwner == null || canonicalBody == null)
                throw new InvalidOperationException("Production/staged wardrobe input is missing.");
            if (!catalog.Validate(out string error)) throw new InvalidOperationException(error);
            if (catalog.IsReleased && configuration.GenerationHash != catalog.ReleasedGenerationId)
                throw new InvalidOperationException("Staged catalog/configuration generations differ.");
            var lines = new StringBuilder();
            lines.AppendLine("Catalog: " + AssetDatabase.GetAssetPath(catalog));
            lines.AppendLine("Release: " + (catalog.IsReleased ? catalog.ReleasedGenerationId : "unpublished"));
            lines.AppendLine("Configuration: " + AssetDatabase.GetAssetPath(configuration));
            lines.AppendLine("Canonical owner/body: " + canonicalOwner.name + " / " + canonicalBody.name);
            foreach (var preset in catalog.Presets)
            {
                lines.AppendLine(preset.PresetId + " / signature " + preset.CharacterSignature);
                var resolved = WardrobeCandidateSnapshot.Resolve(preset, catalog.IsReleased ? null : configuration);
                foreach (var part in resolved.Parts) lines.AppendLine("  layer " + part.layer + ": " + part.sourcePieceId);
                foreach (var fit in preset.FitStates) lines.AppendLine("  fit mask " + fit.VisibleMask + ": " + fit.Status);
            }
            report = lines.ToString();
        }

        private void PrepareCandidate()
        {
            var result = WardrobeCorrectedCandidateBuilder.Prepare(productionCatalog, productionConfiguration);
            catalog = result.catalog; configuration = result.configuration; RecoverManifest();
            report = result.message + "\nCatalog: " + AssetDatabase.GetAssetPath(catalog) + "\nConfiguration: " + AssetDatabase.GetAssetPath(configuration);
        }
        private void PublishCandidate()
        {
            if (IsStagedRelease) { report = "Reused already published corrected release " + catalog.ReleasedGenerationId; return; }
            var result = WardrobeGenerationPublisher.PublishCandidate(catalog, configuration, canonicalBody);
            if (!result.published) throw new InvalidOperationException("Publication FAILED: " + string.Join("\n", result.issues) + "\nCompile artifacts: " + result.temporaryArtifactRoot);
            RecoverManifest();
            report = "Publication SUCCEEDED. Old production: " + productionCatalog.ReleasedGenerationId + "\nDiagnostic: " + result.generationId + "\nCatalog: " + result.catalogPath;
        }
        private void GeneratePrefabs()
        {
            var result = WardrobeRigAssetBuilder.Generate(catalog, canonicalOwner, canonicalBody);
            if (!result.succeeded) throw new InvalidOperationException("Prefab generation stopped: " + string.Join("\n", result.issues));
            manifest = result.manifest;
            report = "Reusable prefabs ready. Created: " + result.createdPrefabCount + "; reused: " + result.reusedPrefabCount + "\nManifest: " + result.manifestPath;
        }
        private void CreateGateAScene()
        {
            gateScenePath = WardrobeProtectedCardsFixtureBuilder.CreateGateAScene(catalog, manifest, productionCatalog.ReleasedGenerationId);
            report = "Gate A scene ready: " + gateScenePath;
        }
        private void OpenGateScene()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) EditorSceneManager.OpenScene(gateScenePath, OpenSceneMode.Single);
        }
        private bool IsStagedRelease => catalog != null && catalog.IsReleased && productionCatalog != null &&
            catalog.ReleasedGenerationId != productionCatalog.ReleasedGenerationId;
    }
}
