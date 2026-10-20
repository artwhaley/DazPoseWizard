using DazPose.Performer;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DazPose.UnityValidation
{
    public sealed class WardrobeAppearanceDeliveryWindow : EditorWindow
    {
        [SerializeField] private WardrobeCatalog catalog;
        [SerializeField] private WardrobeCatalog productionCatalog;
        [SerializeField] private WardrobeConfiguration configuration;
        [SerializeField] private WardrobeConfiguration productionConfiguration;
        [SerializeField] private SkinnedMeshRenderer canonicalBody;
        [SerializeField] private WardrobeRigManifest manifest;
        [SerializeField] private string gateScenePath;
        [SerializeField] private string report = "Production Lara inputs are loaded automatically. Inspect them before preparing the diagnostic candidate.";
        [SerializeField] private Vector2 scroll;

        [MenuItem("DazPose/Wardrobe/Appearance Delivery")]
        public static void Open()
        {
            var window = GetWindow<WardrobeAppearanceDeliveryWindow>();
            window.titleContent = new GUIContent("Appearance Delivery");
            window.minSize = new Vector2(440, 360);
            window.Show();
        }

        private void OnEnable()
        {
            var lara = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Production/Performers/Lara.prefab");
            var wardrobe = lara != null ? lara.GetComponentInChildren<PerformerWardrobe>(true) : null;
            if (productionCatalog == null && wardrobe != null) productionCatalog = wardrobe.Catalog;
            if (productionConfiguration == null && wardrobe != null) productionConfiguration = wardrobe.Configuration;
            if (catalog == null) catalog = productionCatalog;
            if (configuration == null) configuration = productionConfiguration;
            if (canonicalBody == null && lara != null)
            {
                canonicalBody = lara.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                    .FirstOrDefault(x => x.sharedMesh != null && x.sharedMesh.GetBlendShapeIndex("CapturedOpening") >= 0);
            }
            if (manifest == null && catalog != null && catalog.IsReleased)
                manifest = AssetDatabase.LoadAssetAtPath<WardrobeRigManifest>("Assets/Production/Wardrobe/Rigs/" + catalog.ReleasedGenerationId + "/WardrobeRigManifest.asset");
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Wardrobe Appearance Delivery", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Production Lara's released catalog and matching configuration load by default. Diagnostic candidate/release outputs are isolated; this window never advances Lara's production references or changes First Contact automatically.", MessageType.Info);
            if (productionCatalog == null || productionConfiguration == null || canonicalBody == null)
                EditorGUILayout.HelpBox("Production input missing: " + (productionCatalog == null ? "Assets/Production/Performers/Lara.prefab → PerformerWardrobe.catalog; " : string.Empty) +
                    (productionConfiguration == null ? "Assets/Production/Performers/Lara.prefab → PerformerWardrobe.configuration; " : string.Empty) +
                    (canonicalBody == null ? "Lara canonical body SkinnedMeshRenderer." : string.Empty), MessageType.Error);
            EditorGUILayout.LabelField("Production Lara Catalog", AssetDatabase.GetAssetPath(productionCatalog));
            EditorGUILayout.LabelField("Production Lara Configuration", AssetDatabase.GetAssetPath(productionConfiguration));
            catalog = (WardrobeCatalog)EditorGUILayout.ObjectField("Staged Catalog", catalog, typeof(WardrobeCatalog), false);
            configuration = (WardrobeConfiguration)EditorGUILayout.ObjectField("Staged Configuration", configuration, typeof(WardrobeConfiguration), false);
            canonicalBody = (SkinnedMeshRenderer)EditorGUILayout.ObjectField("Canonical Lara Body", canonicalBody, typeof(SkinnedMeshRenderer), true);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Inspect inputs", GUILayout.Height(30))) InspectInputs();
                using (new EditorGUI.DisabledScope(AssetDatabase.LoadAssetAtPath<SceneAsset>(WardrobeProtectedCardsFixtureBuilder.GateAScene) == null))
                    if (GUILayout.Button("Open Gate A scene", GUILayout.Height(30)))
                        EditorSceneManager.OpenScene(WardrobeProtectedCardsFixtureBuilder.GateAScene, OpenSceneMode.Single);
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(productionCatalog == null || productionConfiguration == null || canonicalBody == null))
                    if (GUILayout.Button("Prepare corrected candidate", GUILayout.Height(28))) PrepareCandidate();
                using (new EditorGUI.DisabledScope(catalog == null || catalog.IsReleased || configuration == null || canonicalBody == null))
                    if (GUILayout.Button("Compile and publish candidate", GUILayout.Height(28))) PublishCandidate();
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(!IsStagedRelease))
                    if (GUILayout.Button("Generate reusable prefabs", GUILayout.Height(28))) GeneratePrefabs();
                using (new EditorGUI.DisabledScope(!IsStagedRelease || manifest == null))
                    if (GUILayout.Button("Create Gate A scene", GUILayout.Height(28))) CreateGateAScene();
            }
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Delivery report", EditorStyles.boldLabel);
            scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.ExpandHeight(true));
            EditorGUILayout.TextArea(report, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();
        }

        private void InspectInputs()
        {
            if (catalog == null) { report = "No catalog selected."; return; }
            if (!catalog.Validate(out string error)) { report = "Catalog validation failed: " + error; Repaint(); return; }
            if (configuration == null) { report = "Matching configuration is missing for " + AssetDatabase.GetAssetPath(catalog); Repaint(); return; }
            if (catalog.IsReleased && configuration.GenerationHash != catalog.ReleasedGenerationId)
            { report = "Catalog/configuration generation mismatch: " + catalog.ReleasedGenerationId + " vs " + configuration.GenerationHash; Repaint(); return; }
            var lines = new System.Text.StringBuilder();
            lines.AppendLine("Catalog: " + AssetDatabase.GetAssetPath(catalog));
            lines.AppendLine("Release: " + (catalog.IsReleased ? catalog.ReleasedGenerationId : "candidate / unpublished"));
            lines.AppendLine("Configuration: " + AssetDatabase.GetAssetPath(configuration));
            lines.AppendLine("Canonical body: " + (canonicalBody != null ? canonicalBody.name + " / " + canonicalBody.sharedMesh?.name : "missing"));
            lines.AppendLine("Candidate output: " + WardrobeCorrectedCandidateBuilder.CandidateRoot);
            lines.AppendLine("Rig output: " + (catalog.IsReleased ? "Assets/Production/Wardrobe/Rigs/" + catalog.ReleasedGenerationId : "available after publication"));
            foreach (var preset in catalog.Presets)
            {
                lines.AppendLine(); lines.AppendLine(preset.PresetId + " / signature " + preset.CharacterSignature);
                foreach (var part in preset.Parts) lines.AppendLine("  layer " + part.layer + ": " + part.sourcePieceId);
                foreach (var fit in preset.FitStates) lines.AppendLine("  fit mask " + fit.VisibleMask + ": " + fit.Status);
                lines.AppendLine("  hair policy: " + preset.HairAction + " " + preset.HairId);
            }
            lines.AppendLine(); lines.AppendLine("No assets were modified by Inspect inputs.");
            report = lines.ToString(); Repaint();
        }

        private void GeneratePrefabs()
        {
            var result = WardrobeRigAssetBuilder.Generate(catalog, canonicalBody);
            var lines = new System.Text.StringBuilder();
            lines.AppendLine(result.succeeded ? "Reusable wardrobe prefabs are ready for native inspection." : "Prefab generation stopped.");
            lines.AppendLine("Root: " + result.root); lines.AppendLine("Manifest: " + result.manifestPath);
            lines.AppendLine("Created: " + result.createdPrefabCount + " / reused: " + result.reusedPrefabCount);
            if (result.succeeded) { manifest = result.manifest; lines.AppendLine("Manifest GUID: " + AssetDatabase.AssetPathToGUID(result.manifestPath)); }
            foreach (var issue in result.issues) lines.AppendLine("- " + issue);
            report = lines.ToString(); Repaint();
        }
        private void PrepareCandidate()
        {
            try
            {
                var result = WardrobeCorrectedCandidateBuilder.Prepare(productionCatalog, productionConfiguration);
                catalog = result.catalog; configuration = result.configuration; manifest = null;
                report = result.message + "\nCatalog: " + AssetDatabase.GetAssetPath(catalog) + "\nConfiguration: " + AssetDatabase.GetAssetPath(configuration) +
                    "\nCorrected mapping: dress layer 2; panties layer 1. Production Lara references remain unchanged.";
            }
            catch (System.Exception error) { report = "Candidate preparation stopped: " + error; }
            Repaint();
        }
        private void PublishCandidate()
        {
            var result = WardrobeGenerationPublisher.PublishCandidate(catalog, configuration, canonicalBody);
            report = "Publication " + (result.published ? "SUCCEEDED" : "FAILED") + "\nOld production generation: " + productionCatalog.ReleasedGenerationId +
                "\nNew diagnostic generation: " + result.generationId + "\nCatalog: " + result.catalogPath + "\nPresets: " + result.presetCount + "; fit states: " + result.fitStateCount +
                "\nTemporary compile root: " + result.temporaryArtifactRoot + "\n" + string.Join("\n", result.issues);
            if (result.published) manifest = null;
            Repaint();
        }
        private void CreateGateAScene()
        {
            try
            {
                gateScenePath = WardrobeProtectedCardsFixtureBuilder.CreateGateAScene(catalog, manifest);
                report = "Gate A scene saved: " + gateScenePath + "\nClick Open Gate A scene, then enter Play yourself. The Appearance Gate panel is saved in the scene.";
            }
            catch (System.Exception error) { report = "Gate A scene creation stopped: " + error; }
            Repaint();
        }
        private bool IsStagedRelease => catalog != null && catalog.IsReleased && productionCatalog != null &&
            catalog.ReleasedGenerationId != productionCatalog.ReleasedGenerationId;
    }
}
