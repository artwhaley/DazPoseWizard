using System;
using System.Linq;
using System.Text;
using DazPose.Performer;
using UnityEditor;
using UnityEngine;

namespace DazPose.UnityValidation
{
    /// <summary>Reusable visual-asset generation for any released catalog. No scenes or test actors are created.</summary>
    public sealed class WardrobeAppearanceDeliveryWindow : EditorWindow
    {
        [SerializeField] private WardrobeCatalog catalog;
        [SerializeField] private PerformerWardrobe canonicalOwner;
        [SerializeField] private string report = "Select a released wardrobe catalog to inspect or generate its reusable clothing and hair assets.";
        [SerializeField] private Vector2 scroll;

        [MenuItem("DazPose/Wardrobe/Appearance Delivery")]
        public static void Open()
        {
            var window = GetWindow<WardrobeAppearanceDeliveryWindow>();
            window.titleContent = new GUIContent("Wardrobe Assets");
            window.minSize = new Vector2(480, 320); window.Show();
        }
        private void OnEnable()
        {
            var lara = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Production/Performers/Lara.prefab");
            canonicalOwner = lara != null ? lara.GetComponentInChildren<PerformerWardrobe>(true) : null;
            if (catalog == null && canonicalOwner != null) catalog = canonicalOwner.Catalog;
        }
        private void OnGUI()
        {
            EditorGUILayout.LabelField("Reusable wardrobe visual assets", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Uses the existing released catalog and wardrobe pipeline. Generates clothing/hair prefabs and their manifest; no diagnostic scene, actor or first-outfit correction is created.", MessageType.Info);
            catalog = (WardrobeCatalog)EditorGUILayout.ObjectField("Released catalog", catalog, typeof(WardrobeCatalog), false);
            EditorGUILayout.LabelField("Lara body", canonicalOwner != null && canonicalOwner.Body != null ? canonicalOwner.Body.name : "Missing configured PerformerWardrobe.Body");
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Inspect catalog")) Run(Inspect);
                using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode || catalog == null || !catalog.IsReleased || canonicalOwner == null))
                    if (GUILayout.Button("Generate / reuse visual assets")) Run(Generate);
            }
            EditorGUILayout.Space();EditorGUILayout.LabelField("Current acceptance: Gate B",EditorStyles.boldLabel);
            using(new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))if(GUILayout.Button("Create / open Gate B scene"))PrepareGateB();
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.TextArea(report, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();
        }
        private void PrepareGateB()
        {
            try
            {
                report = CardReturnGateBuilder.CreateOrOpen();
                EditorUtility.DisplayDialog("Gate B preparation", report, "OK");
            }
            catch (Exception error)
            {
                report = "GATE B IS NOT READY. Do not enter Play.\n" + error;
                Debug.LogException(error);
                EditorUtility.DisplayDialog("Gate B preparation failed", report, "OK");
            }
            Repaint();
        }
        private void Run(Action action)
        { try { action(); } catch (Exception error) { report = error.Message; } Repaint(); }
        private void Inspect()
        {
            if (catalog == null || !catalog.Validate(out _)) throw new InvalidOperationException("Select a valid released catalog.");
            var lines = new StringBuilder("Catalog: " + AssetDatabase.GetAssetPath(catalog) + "\nGeneration: " + catalog.ReleasedGenerationId);
            foreach (var preset in catalog.Presets)
            {
                lines.AppendLine().AppendLine(preset.DisplayName + " [" + preset.PresetId + "]");
                foreach (var part in preset.Parts) lines.AppendLine("  Layer " + part.layer + ": " + part.sourcePieceId);
                foreach (var hair in preset.Package.pieces.Where(piece => string.Equals(piece.role, "hair", StringComparison.OrdinalIgnoreCase)))
                    lines.AppendLine("  Hair: " + preset.PresetId + "/" + hair.id);
            }
            report = lines.ToString();
        }
        private void Generate()
        {
            var result = WardrobeRigAssetBuilder.Generate(catalog, canonicalOwner, canonicalOwner.Body);
            if (!result.succeeded) throw new InvalidOperationException(string.Join("\n", result.issues));
            report = "Created " + result.createdPrefabCount + "; reused " + result.reusedPrefabCount + "\nManifest: " + result.manifestPath;
        }
    }
}
