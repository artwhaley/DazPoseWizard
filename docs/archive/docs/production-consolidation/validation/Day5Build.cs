using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class Day5Build
{
    [Serializable] private sealed class Dependencies { public string[] paths; }
    [Serializable] private sealed class Report { public string result, exception, output, backend; public int errors, warnings; }
    public static void Run()
    {
        var report = new Report();
        try
        {
            if (!Application.dataPath.Replace('\\', '/').Contains("/.dazposewizard/production-consolidation/")) throw new Exception("Disposable project required.");
            report.output = Path.GetFullPath("TestOutput/day5/player/Day5Validation.exe"); Directory.CreateDirectory(Path.GetDirectoryName(report.output));
            File.WriteAllText("TestOutput/day5/dependencies.json", JsonUtility.ToJson(new Dependencies { paths = AssetDatabase.GetDependencies(Day5SceneBuilder.ScenePath, true) }, true));
            var build = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = new[] { Day5SceneBuilder.ScenePath }, target = BuildTarget.StandaloneWindows64,
                locationPathName = report.output, options = BuildOptions.Development });
            report.result = build.summary.result.ToString(); report.errors = (int)build.summary.totalErrors; report.warnings = (int)build.summary.totalWarnings;
            report.backend = PlayerSettings.GetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Standalone).ToString();
        }
        catch (Exception exception) { report.exception = exception.ToString(); }
        Directory.CreateDirectory("TestOutput/day5"); File.WriteAllText("TestOutput/day5/build.json", JsonUtility.ToJson(report, true));
        if (report.result == "Succeeded" && report.exception == null) BuildSetup();
        else EditorApplication.Exit(1);
    }
    private static void BuildSetup()
    {
        var report = new Report();
        try
        {
            report.output = Path.GetFullPath("TestOutput/day5/setup-player/ClothingSetupValidation.exe"); Directory.CreateDirectory(Path.GetDirectoryName(report.output));
            var build = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = new[] { "Assets/Scenes/ClothingSetup.unity" }, target = BuildTarget.StandaloneWindows64,
                locationPathName = report.output, options = BuildOptions.Development });
            report.result = build.summary.result.ToString(); report.errors = (int)build.summary.totalErrors; report.warnings = (int)build.summary.totalWarnings;
            report.backend = PlayerSettings.GetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Standalone).ToString();
        }
        catch (Exception exception) { report.exception = exception.ToString(); }
        File.WriteAllText("TestOutput/day5/setup-build.json", JsonUtility.ToJson(report, true));
        EditorApplication.Exit(report.result == "Succeeded" && report.exception == null ? 0 : 1);
    }
}
