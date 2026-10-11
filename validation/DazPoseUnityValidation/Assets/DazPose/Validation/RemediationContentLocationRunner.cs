using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DazPose.Performer;
using UnityEngine;

namespace DazPose.Validation
{
    /// <summary>R09 saved-scene execution witness for folder-independent prepared content.</summary>
    public sealed class RemediationContentLocationRunner : MonoBehaviour
    {
        [Serializable] private sealed class Assertion
        { public string name, expected, actual; public bool passed; }
        [Serializable] private sealed class Report
        {
            public string ticket, startedUtc, completedUtc, sourceHash, exception;
            public string[] inputAssetHashes;
            public bool complete, passed;
            public List<Assertion> assertions = new List<Assertion>();
        }

        [SerializeField] private SuccubusPerformer actor;
        [SerializeField] private PerformanceSceneBindings bindings;
        [SerializeField] private PerformerGesture gesture;
        [SerializeField] private PerformerAction action;
        [SerializeField] private string reportPath, sourceHash, inputHash, initialReportJson;
        private bool finished;

#if UNITY_EDITOR
        public void Configure(SuccubusPerformer owner, PerformanceSceneBindings sceneBindings,
            PerformerGesture preparedGesture, PerformerAction preparedAction, string output,
            string source, string input, string priorReportJson)
        {
            actor = owner; bindings = sceneBindings; gesture = preparedGesture; action = preparedAction;
            reportPath = output; sourceHash = source; inputHash = input; initialReportJson = priorReportJson;
        }
#endif

        private async void Start()
        {
            Report report = null;
            try
            {
                report = JsonUtility.FromJson<Report>(initialReportJson) ?? new Report();
                report.ticket = "R09";
                report.startedUtc = string.IsNullOrEmpty(report.startedUtc) ? DateTime.UtcNow.ToString("O") : report.startedUtc;
                report.sourceHash = sourceHash ?? string.Empty;
                report.inputAssetHashes = new[] { inputHash ?? string.Empty };
                for (int frame = 0; frame < 300 && (actor == null || !actor.IsRuntimeReady); frame++)
                    await Awaitable.NextFrameAsync();
                Record(report, "runtimeActorReady", actor != null && actor.IsRuntimeReady,
                    actor != null ? "ready=" + actor.IsRuntimeReady : "actor missing");
                Record(report, "savedBindingsAndContent", bindings != null && gesture != null && action != null,
                    bindings != null && gesture != null && action != null ? "saved actor, bindings, Gesture and Action" : "missing serialized reference");
                if (actor == null || bindings == null || gesture == null || action == null)
                    throw new InvalidOperationException("R09 saved scene is missing a required runtime reference.");

                using (PerformanceRun run = PerformanceRun.Begin(bindings))
                {
                    GestureCompletion gestureResult = await run.Performer.GestureAsync(gesture);
                    Record(report, "ordinaryFolderGestureExecutes", gestureResult == GestureCompletion.Completed, gestureResult.ToString());
                    ActionCompletion actionResult = await run.Performer.PerformAsync(action);
                    Record(report, "ordinaryFolderActionExecutes", actionResult == ActionCompletion.Completed, actionResult.ToString());
                }
                Finish(report, null);
            }
            catch (Exception exception)
            {
                if (report == null) report = new Report { ticket = "R09", sourceHash = sourceHash ?? string.Empty, inputAssetHashes = new[] { inputHash ?? string.Empty } };
                Finish(report, exception.ToString());
            }
        }

        private void Finish(Report report, string exception)
        {
            if (finished) return;
            finished = true;
            report.exception = exception;
            report.passed = string.IsNullOrEmpty(exception) && report.assertions != null && report.assertions.All(item => item.passed);
            report.complete = true;
            report.completedUtc = DateTime.UtcNow.ToString("O");
            string output = string.IsNullOrWhiteSpace(reportPath) ? Path.Combine("TestOutput", "remediation", "R09.json") : reportPath;
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output)));
            File.WriteAllText(output, JsonUtility.ToJson(report, true));
#if UNITY_EDITOR
            if (Application.isBatchMode) UnityEditor.EditorApplication.Exit(report.passed ? 0 : 1);
#else
            if (Application.isBatchMode) Application.Quit(report.passed ? 0 : 1);
#endif
        }

        private static void Record(Report report, string name, bool passed, string actual)
        {
            report.assertions.Add(new Assertion { name = name, expected = "pass", actual = actual ?? string.Empty, passed = passed });
            if (!passed) throw new InvalidOperationException(name + " failed: " + actual);
        }
    }
}
