using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using DazPose.Performer;
using UnityEngine;

namespace DazPose.Validation
{
    /// <summary>R05 side-effect witness for the complete declared authoring asset surface.</summary>
    public sealed class RemediationContentPreflightRunner : MonoBehaviour
    {
        [Serializable] private sealed class Assertion
        { public string name, expected, actual; public bool passed; }

        [Serializable] private sealed class Report
        {
            public string ticket = "R05", startedUtc, completedUtc, sourceHash, exception;
            public string[] inputAssetHashes;
            public bool complete, passed;
            public List<Assertion> assertions = new List<Assertion>();
        }

        [SerializeField] private SuccubusPerformer actor;
        [SerializeField] private PerformanceSceneBindings bindings;
        [SerializeField] private PerformerGesture validGesture;
        [SerializeField] private PerformerAction validAction;
        [SerializeField] private PerformerSpell validSpell;
        [SerializeField] private PerformerAura validAura;
        [SerializeField] private PerformerMagicStyle validStyle;
        [SerializeField] private Transform validTarget;
        [SerializeField] private string reportPath, sourceHash, inputHash;

        private bool finished;
        private Report report;

#if UNITY_EDITOR
        public void Configure(SuccubusPerformer owner, PerformanceSceneBindings sceneBindings,
            PerformerGesture gesture, PerformerAction action, PerformerSpell spell, PerformerAura aura,
            PerformerMagicStyle style, Transform target, string output, string source, string input)
        {
            actor = owner;
            bindings = sceneBindings;
            validGesture = gesture;
            validAction = action;
            validSpell = spell;
            validAura = aura;
            validStyle = style;
            validTarget = target;
            reportPath = output;
            sourceHash = source;
            inputHash = input;
        }
#endif

        private async void Start()
        {
            report = new Report
            {
                startedUtc = DateTime.UtcNow.ToString("O"),
                sourceHash = sourceHash ?? string.Empty,
                inputAssetHashes = new[] { inputHash ?? string.Empty }
            };
            try
            {
                for (int frame = 0; frame < 240 && (actor == null || !actor.IsRuntimeReady); ++frame)
                    await Awaitable.NextFrameAsync();
                RunPreflight();
                Finish(report.assertions.All(item => item.passed), null);
            }
            catch (Exception exception) { Finish(false, exception.ToString()); }
        }

        private void RunPreflight()
        {
            Record("fixtureReferences", actor != null && bindings != null && validGesture != null
                && validAction != null && validSpell != null && validAura != null && validStyle != null && validTarget != null,
                "Actor, binding, prepared Gesture/Action/Magic assets and target are required.");
            if (actor == null || bindings == null || validGesture == null || validAction == null
                || validSpell == null || validAura == null || validStyle == null || validTarget == null) return;

            PerformanceSceneBindings probe = CreateProbe();
            probe.Configure(actor, null, null, null, PerformerCapability.Gesture | PerformerCapability.Action | PerformerCapability.Magic,
                new[] { new PerformanceSceneBindings.Target { id = "magicTarget", transform = validTarget } });
            probe.DeclareContent(new[] { validGesture }, new[] { validAction }, new[] { validSpell }, new[] { validAura });
            BindingIssue[] valid = probe.Validate(false);
            Record("validPreparedAssets", valid.Length == 0, Describe(valid));

            object originalMask = GetField(actor, "gestureUpperBodyMask");
            SetField(actor, "gestureUpperBodyMask", null);
            ExpectFailure("invalidGestureMask", "gestureUpperBodyMask", probe.Validate(PerformerCapability.Gesture, false), "gesture mask");
            SetField(actor, "gestureUpperBodyMask", originalMask);

            var missingGesture = ScriptableObject.CreateInstance<PerformerGesture>();
            probe.DeclareContent(new[] { missingGesture }, Array.Empty<PerformerAction>(), Array.Empty<PerformerSpell>(), Array.Empty<PerformerAura>());
            ExpectFailure("invalidGestureClip", "gestures.Array.data[0]", probe.Validate(PerformerCapability.Gesture, false), "requires a baked Generic AnimationClip");
            DestroyFixtureObject(missingGesture);

            PerformerAction invalidAction = Instantiate(validAction);
            invalidAction.name = "R05 invalid action trajectory";
            SetField(invalidAction, "rootY", new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(1f, 1f)));
            probe.DeclareContent(Array.Empty<PerformerGesture>(), new[] { invalidAction }, Array.Empty<PerformerSpell>(), Array.Empty<PerformerAura>());
            ExpectFailure("invalidActionTrajectory", "actions.Array.data[0]", probe.Validate(PerformerCapability.Action, false), "ends");
            DestroyFixtureObject(invalidAction);

            PerformerSpell missingStyleSpell = ScriptableObject.CreateInstance<PerformerSpell>();
            probe.DeclareContent(Array.Empty<PerformerGesture>(), Array.Empty<PerformerAction>(), new[] { missingStyleSpell }, Array.Empty<PerformerAura>());
            ExpectFailure("missingMagicStyle", "spells.Array.data[0]", probe.Validate(PerformerCapability.Magic, false), "shared PerformerMagicStyle");
            DestroyFixtureObject(missingStyleSpell);

            PerformerMagicStyle missingPrefabStyle = Instantiate(validStyle);
            missingPrefabStyle.name = "R05 missing magic prefab style";
            SetField(missingPrefabStyle, "visualEffectAsset", null);
            PerformerSpell missingPrefabSpell = Instantiate(validSpell);
            missingPrefabSpell.name = "R05 missing magic prefab spell";
            SetField(missingPrefabSpell, "style", missingPrefabStyle);
            probe.DeclareContent(Array.Empty<PerformerGesture>(), Array.Empty<PerformerAction>(), new[] { missingPrefabSpell }, Array.Empty<PerformerAura>());
            ExpectFailure("missingMagicPrefab", "spells.Array.data[0]", probe.Validate(PerformerCapability.Magic, false), "VFX Graph");
            DestroyFixtureObject(missingPrefabSpell);
            DestroyFixtureObject(missingPrefabStyle);

            probe.Configure(actor, null, null, null, PerformerCapability.Magic,
                new[] { new PerformanceSceneBindings.Target { id = "magicTarget", transform = null } });
            probe.DeclareContent(Array.Empty<PerformerGesture>(), Array.Empty<PerformerAction>(), new[] { validSpell }, new[] { validAura });
            ExpectFailure("missingMagicTarget", "targets.Array.data[0].transform", probe.Validate(PerformerCapability.Magic, false), "Assign this named target");

            string[] failureMessages = new[]
            {
                CheckMissingActorField(probe, "speechAudioSource", PerformerCapability.Speech, "actor/speechAudioSource"),
                CheckMissingActorField(probe, "teleportProfile", PerformerCapability.Teleport, "actor/teleportProfile"),
                CheckMissingActorField(probe, "dissolveProfile", PerformerCapability.Visibility, "actor/dissolveProfile"),
                CheckMissingActorField(probe, "dissolveRig", PerformerCapability.Visibility, "actor/dissolveRig")
            };
            Record("productionErrorsContainNoAcceptanceSetup", failureMessages.All(message =>
                message.IndexOf("acceptance", StringComparison.OrdinalIgnoreCase) < 0
                && message.IndexOf("PoseValidation.unity", StringComparison.OrdinalIgnoreCase) < 0),
                string.Join(" | ", failureMessages));

            probe.Configure(actor, null, null, null, PerformerCapability.Speech);
            probe.DeclareContent(Array.Empty<PerformerGesture>(), Array.Empty<PerformerAction>(), Array.Empty<PerformerSpell>(), Array.Empty<PerformerAura>());
            BindingIssue[] speechOnly = probe.Validate(PerformerCapability.Speech, false);
            Record("speechOnlyLeavesActionAndMagicOptional", speechOnly.Length == 0, Describe(speechOnly));
            DestroyFixtureObject(probe.gameObject);
        }

        private string CheckMissingActorField(PerformanceSceneBindings probe, string fieldName,
            PerformerCapability capability, string expectedPath)
        {
            object original = GetField(actor, fieldName);
            SetField(actor, fieldName, null);
            BindingIssue[] issues = probe.Validate(capability, false);
            BindingIssue match = issues.FirstOrDefault(issue => issue.PropertyPath == expectedPath);
            string detail = match != null ? match.Message : Describe(issues);
            Record("missing_" + fieldName, match != null, detail);
            SetField(actor, fieldName, original);
            return detail ?? string.Empty;
        }

        private PerformanceSceneBindings CreateProbe()
        {
            var holder = new GameObject("R05 Content Preflight Bindings");
            holder.transform.SetParent(transform, false);
            return holder.AddComponent<PerformanceSceneBindings>();
        }

        private void ExpectFailure(string name, string path, BindingIssue[] issues, string reason)
        {
            string detail = Describe(issues);
            Record(name, issues.Any(issue => issue.PropertyPath == path
                && issue.Message.IndexOf(reason, StringComparison.OrdinalIgnoreCase) >= 0), detail);
        }

        private void Record(string name, bool passed, string actual)
        { report.assertions.Add(new Assertion { name = name, expected = "pass", actual = actual ?? string.Empty, passed = passed }); }

        private void Finish(bool passed, string exception)
        {
            if (finished) return;
            finished = true;
            report.exception = exception;
            report.passed = passed;
            report.complete = true;
            report.completedUtc = DateTime.UtcNow.ToString("O");
            string output = string.IsNullOrWhiteSpace(reportPath) ? Path.Combine("TestOutput", "remediation", "R05.json") : reportPath;
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(output)));
            System.IO.File.WriteAllText(output, JsonUtility.ToJson(report, true));
#if UNITY_EDITOR
            if (Application.isBatchMode) UnityEditor.EditorApplication.Exit(passed ? 0 : 1);
#else
            if (Application.isBatchMode) Application.Quit(passed ? 0 : 1);
#endif
        }

        private static string Describe(BindingIssue[] issues) => issues == null || issues.Length == 0
            ? "no issues" : string.Join(" | ", issues.Select(issue => issue.ToString()));

        private static object GetField(object owner, string name) => owner.GetType().GetField(name,
            BindingFlags.Instance | BindingFlags.NonPublic).GetValue(owner);

        private static void SetField(object owner, string name, object value) => owner.GetType().GetField(name,
            BindingFlags.Instance | BindingFlags.NonPublic).SetValue(owner, value);

        private static void DestroyFixtureObject(UnityEngine.Object value)
        {
            if (value == null) return;
#if UNITY_EDITOR
            if (!Application.isPlaying) UnityEngine.Object.DestroyImmediate(value);
            else UnityEngine.Object.Destroy(value);
#else
            UnityEngine.Object.Destroy(value);
#endif
        }
    }
}
