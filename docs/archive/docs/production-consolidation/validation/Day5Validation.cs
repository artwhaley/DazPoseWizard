using System;
using System.IO;
using DazPose.Validation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class Day5Validation
{
    static Day5Validation()
    {
        EditorApplication.playModeStateChanged += state =>
        { if (state == PlayModeStateChange.EnteredEditMode && SessionState.GetBool("Day5.Active", false)) EditorApplication.delayCall += Finish; };
    }
    public static void Run()
    {
        try
        {
            if (!Application.dataPath.Replace('\\', '/').Contains("/.dazposewizard/production-consolidation/")) throw new Exception("Disposable project required.");
            EditorSceneManager.OpenScene(Day5SceneBuilder.ScenePath);
            if (UnityEngine.Object.FindObjectsByType<Day5WardrobeRunner>(FindObjectsSortMode.None).Length != 1) throw new Exception("Exact delivered scene must have its saved runner.");
            File.Delete("TestOutput/day5/runtime.json"); SessionState.SetString("Day5.Phase", "wardrobe"); SessionState.SetBool("Day5.Active", true); EditorApplication.EnterPlaymode();
        }
        catch (Exception exception) { Debug.LogException(exception); EditorApplication.Exit(1); }
    }
    private static void Finish()
    {
        SessionState.SetBool("Day5.Active", false);
        try
        {
            if (SessionState.GetString("Day5.Phase", "wardrobe") == "wardrobe")
            {
                if (!File.ReadAllText("TestOutput/day5/runtime.json").Contains("\"result\": \"passed\"")) throw new Exception("Day 5 runtime assertions failed.");
                RunSetup(); return;
            }
            if (!File.ReadAllText("TestOutput/day5/setup-runtime.json").Contains("\"result\": \"passed\"")) throw new Exception("Saved Clothing Setup assertions failed.");
            Day5Build.Run();
        }
        catch (Exception exception) { Debug.LogException(exception); EditorApplication.Exit(1); }
    }
    public static void RunSetup()
    {
        try
        {
            if (!Application.dataPath.Replace('\\', '/').Contains("/.dazposewizard/production-consolidation/")) throw new Exception("Disposable project required.");
            EditorSceneManager.OpenScene("Assets/Scenes/ClothingSetup.unity");
            if (UnityEngine.Object.FindObjectsByType<ClothingSetupPreviewRunner>(FindObjectsSortMode.None).Length != 1) throw new Exception("Clothing Setup must contain its saved preview runner.");
            File.Delete("TestOutput/day5/setup-runtime.json"); SessionState.SetString("Day5.Phase", "setup"); SessionState.SetBool("Day5.Active", true); EditorApplication.EnterPlaymode();
        }
        catch (Exception exception) { Debug.LogException(exception); EditorApplication.Exit(1); }
    }
}
