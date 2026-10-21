using System;
using System.IO;
using System.Linq;
using DazPose.Performer;
using DazPose.UnityValidation;
using DazPose.Validation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class Day5PrepareSetup
{
    [Serializable] private sealed class Report { public bool passed; public string error; public string[] dependencies; }
    public static void Run()
    {
        var report = new Report();
        try
        {
            if (!Application.dataPath.Replace('\\', '/').Contains("/.dazposewizard/production-consolidation/")) throw new Exception("Disposable project required.");
            var scene = EditorSceneManager.OpenScene("Assets/Production/Scenes/Day2ActorValidation.unity");
            var performers = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<SuccubusPerformer>(true)).OrderBy(p => p.name).ToArray();
            var performer = performers[0];
            foreach (var other in performers.Skip(1)) UnityEngine.Object.DestroyImmediate(other.gameObject);
            performer.transform.SetPositionAndRotation(new Vector3(0, 0, -.8f), Quaternion.identity);
            var wardrobe = performer.GetComponent<PerformerWardrobe>();
            var body = wardrobe.Body;
            var catalog = AssetDatabase.LoadAssetAtPath<WardrobeCatalog>(ClothingSetupBuilder.CatalogPath);
            var config = AssetDatabase.LoadAssetAtPath<WardrobeConfiguration>(ClothingSetupBuilder.ConfigurationPath);
            if (!WardrobeCandidateWorkflow.TryCompile(catalog, config, body, out string error)) throw new Exception(error);
            if (!WardrobeCandidateWorkflow.IsCompiled(catalog, config)) throw new Exception("Saved baseline candidate did not retain compilation parity.");
            wardrobe.ConfigureBinding(catalog, config, body, performer, true);
            EditorUtility.SetDirty(wardrobe);
            PrefabUtility.RecordPrefabInstancePropertyModifications(wardrobe);
            var draft = performer.GetComponent<ClothingSetupDraftBinding>() ?? performer.gameObject.AddComponent<ClothingSetupDraftBinding>();
            draft.Configure(catalog, config);
            performer.gameObject.AddComponent<ClothingSetupPreviewRunner>().Configure(performer);
            var camera = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Camera>(true)).Single(c => c.CompareTag("MainCamera"));
            camera.fieldOfView = 45f; camera.transform.position = new Vector3(2.5f, 1.5f, 3.2f);
            camera.transform.rotation = Quaternion.LookRotation(new Vector3(0, .95f, -.8f) - camera.transform.position, Vector3.up);
            if (!EditorSceneManager.SaveScene(scene, ClothingSetupBuilder.ScenePath)) throw new Exception("Could not persist setup draft binding.");
            AssetDatabase.SaveAssets();
            report.dependencies = AssetDatabase.GetDependencies(ClothingSetupBuilder.ScenePath, true); report.passed = true;
        }
        catch (Exception exception) { report.error = exception.ToString(); }
        Directory.CreateDirectory("TestOutput/day5"); File.WriteAllText("TestOutput/day5/setup-preparation.json", JsonUtility.ToJson(report, true));
        EditorApplication.Exit(report.passed ? 0 : 1);
    }
}
