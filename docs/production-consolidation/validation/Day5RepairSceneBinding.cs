using System;
using System.IO;
using DazPose.Performer;
using DazPose.UnityValidation;
using DazPose.Validation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class Day5RepairSceneBinding
{
    public static void Run()
    {
        try
        {
            if (!Application.dataPath.Replace('\\', '/').Contains("/.dazposewizard/production-consolidation/")) throw new Exception("Disposable project required.");
            var scene = EditorSceneManager.OpenScene(Day5SceneBuilder.ScenePath);
            var runner = UnityEngine.Object.FindAnyObjectByType<Day5WardrobeRunner>();
            var actor = (SuccubusPerformer)new SerializedObject(runner).FindProperty("actor").objectReferenceValue;
            var draft = actor.GetComponent<ClothingSetupDraftBinding>();
            if (!WardrobeCandidateWorkflow.IsCompiled(draft.Catalog, draft.Configuration)) throw new Exception("Saved draft is not compiled.");
            var wardrobe = actor.GetComponent<PerformerWardrobe>();
            wardrobe.ConfigureBinding(draft.Catalog, draft.Configuration, wardrobe.Body, actor, true);
            EditorUtility.SetDirty(wardrobe); PrefabUtility.RecordPrefabInstancePropertyModifications(wardrobe);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new Exception("Scene save failed.");
            File.WriteAllText("TestOutput/day5/binding-repair.txt", "Recorded candidate wardrobe prefab overrides in the exact saved scene.");
            EditorApplication.Exit(0);
        }
        catch (Exception exception) { Debug.LogException(exception); EditorApplication.Exit(1); }
    }
}
