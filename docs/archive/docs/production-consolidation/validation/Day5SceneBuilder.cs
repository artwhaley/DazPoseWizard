using System;
using System.IO;
using System.Linq;
using DazPose.Performer;
using DazPose.UnityValidation;
using DazPose.Validation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class Day5SceneBuilder
{
    public const string ScenePath = "Assets/Production/Scenes/Day5WardrobeValidation.unity";
    public static void Create()
    {
        try
        {
            if (!Application.dataPath.Replace('\\', '/').Contains("/.dazposewizard/production-consolidation/")) throw new Exception("Disposable project required.");
            var scene = EditorSceneManager.OpenScene("Assets/Production/Scenes/Day2ActorValidation.unity");
            var actors = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<SuccubusPerformer>(true)).OrderBy(a => a.name).ToArray();
            var actor = actors[0];
            foreach (var other in actors.Skip(1)) UnityEngine.Object.DestroyImmediate(other.gameObject);
            actor.transform.SetPositionAndRotation(new Vector3(0, 0, -.8f), Quaternion.identity);
            var wardrobe = actor.GetComponent<PerformerWardrobe>();
            var body = wardrobe.Body;
            var sourceCatalog = AssetDatabase.LoadAssetAtPath<WardrobeCatalog>("Assets/Wardrobe/WardrobeCatalog.asset");
            var sourceConfig = AssetDatabase.LoadAssetAtPath<WardrobeConfiguration>("Assets/Wardrobe/WardrobeConfiguration.asset");
            string root = "Assets/Production/Wardrobe/Day5-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
            Folder(root + "/Presets");
            var parts = sourceCatalog.Presets.Select(p =>
            {
                var copy = UnityEngine.Object.Instantiate(p); AssetDatabase.CreateAsset(copy, root + "/Presets/" + p.PresetId + ".asset"); return copy;
            }).ToArray();
            var catalog = ScriptableObject.CreateInstance<WardrobeCatalog>(); catalog.ConfigureCandidate(parts);
            AssetDatabase.CreateAsset(catalog, root + "/Catalog.asset");
            var config = UnityEngine.Object.Instantiate(sourceConfig); AssetDatabase.CreateAsset(config, root + "/Configuration.asset");
            var session = new WardrobeEditSession(config, catalog);
            var maid = parts.Single(p => p.PresetId == "third-outfit");
            foreach (var part in maid.Parts)
            {
                var piece = maid.Package.pieces.Single(p => p.id == part.sourcePieceId);
                int layer = piece.role == "footwear" ? 2 : piece.role == "hosiery" ? 0 : 1;
                if (!session.TrySetLayer(maid.PresetId, part.sourcePieceId, layer, out string layerError)) throw new Exception(layerError);
            }
            if (!WardrobeCandidateWorkflow.TrySaveDraft(catalog, config, session, body, out catalog, out config, out string error)) throw new Exception(error);
            if (!WardrobeCandidateWorkflow.TryCompile(catalog, config, body, out error)) throw new Exception(error);
            // Preserve the candidate for comparison before the publisher advances the distinct catalog.
            var previewPresets = catalog.Presets.Select(p =>
            {
                var copy = UnityEngine.Object.Instantiate(p); AssetDatabase.CreateAsset(copy, root + "/Preview-" + p.PresetId + ".asset"); return copy;
            }).ToArray();
            var previewConfig = UnityEngine.Object.Instantiate(config); AssetDatabase.CreateAsset(previewConfig, root + "/PreviewConfiguration.asset");
            var previewCatalog = UnityEngine.Object.Instantiate(catalog); previewCatalog.ConfigureCandidate(previewPresets);
            previewCatalog.SetCompiledConfigurationHash(catalog.CompiledConfigurationHash); previewCatalog.SetAuthoringConfiguration(previewConfig);
            AssetDatabase.CreateAsset(previewCatalog, root + "/PreviewCatalog.asset"); AssetDatabase.SaveAssets();
            var published = WardrobeGenerationPublisher.PublishCandidate(catalog, config, body);
            if (!published.published) throw new Exception(string.Join("; ", published.issues));
            wardrobe.ConfigureBinding(previewCatalog, previewConfig, body, actor, true);
            EditorUtility.SetDirty(wardrobe);
            PrefabUtility.RecordPrefabInstancePropertyModifications(wardrobe);
            var draft = actor.GetComponent<ClothingSetupDraftBinding>() ?? actor.gameObject.AddComponent<ClothingSetupDraftBinding>();
            draft.Configure(previewCatalog, previewConfig);
            var camera = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Camera>(true)).Single(c => c.CompareTag("MainCamera"));
            camera.fieldOfView = 45f; camera.transform.position = new Vector3(2.5f, 1.5f, 3.2f);
            camera.transform.rotation = Quaternion.LookRotation(new Vector3(0, .95f, -.8f) - camera.transform.position, Vector3.up);
            var runner = new GameObject("Day 5 Saved Wardrobe Runner").AddComponent<Day5WardrobeRunner>();
            runner.Configure(actor, AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Production/Performers/Lara.prefab"), catalog,
                previewPresets.Single(p => p.PresetId == "third-outfit"));
            if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new Exception("Could not save the delivered Day 5 scene.");
            AssetDatabase.SaveAssets();
            Directory.CreateDirectory("TestOutput/day5");
            File.WriteAllText("TestOutput/day5/prepared-release.json", JsonUtility.ToJson(published, true));
            Day5Validation.Run();
        }
        catch (Exception exception) { Debug.LogException(exception); EditorApplication.Exit(1); }
    }
    private static void Folder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        int slash = path.LastIndexOf('/'); Folder(path.Substring(0, slash)); AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
    }
}
