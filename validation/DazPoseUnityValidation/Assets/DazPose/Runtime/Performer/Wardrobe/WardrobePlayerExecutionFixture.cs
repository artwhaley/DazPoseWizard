using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace DazPose.Performer
{
    /// <summary>Player-side smoke fixture proving wardrobe assets and authoring calls work without the editor.</summary>
    public sealed class WardrobePlayerExecutionFixture : MonoBehaviour
    {
        [Serializable] private sealed class Assertion
        {
            public string name, expected, actual;
            public bool passed;
        }

        [Serializable] private sealed class Report
        {
            public int schemaVersion = 1;
            public bool passed;
            public string startedUtc, completedUtc;
            public Assertion[] assertions;
        }

        [SerializeField] private SuccubusPerformer performer;
        [SerializeField] private PerformerWardrobe wardrobe;
        [SerializeField] private SceneWardrobeBinding sceneBinding;
        private readonly List<Assertion> _assertions = new List<Assertion>();

        private async void Start()
        {
            string started = DateTime.UtcNow.ToString("O");
            try
            {
                Check("serializedRuntimeReferences", "performer, wardrobe and scene binding are present",
                    "performer=" + (performer != null) + ";wardrobe=" + (wardrobe != null) + ";binding=" + (sceneBinding != null),
                    performer != null && wardrobe != null && sceneBinding != null && wardrobe.Catalog != null);
                if (performer == null || wardrobe == null || sceneBinding == null || wardrobe.Catalog == null)
                    throw new InvalidOperationException("The player fixture scene is missing a serialized runtime reference.");

                var first = await performer.OutfitAsync("first-outfit");
                await WaitForWardrobeQueue();
                WardrobeState firstState = wardrobe.CurrentWardrobe;
                Check("stableIdOutfitCommand", "Applied first-outfit", first.Status + "/" + firstState.OutfitId,
                    first.Status == WardrobeChangeStatus.Applied && firstState.OutfitId == "first-outfit");

                sceneBinding.SetPerformerBindingId("player-lara");
                sceneBinding.CaptureCurrent();
                SceneWardrobeSnapshot captured = sceneBinding.Snapshot;
                bool capturedResolvedState = captured != null && captured.presetId == firstState.OutfitId &&
                    captured.layerCeiling == firstState.LayerCeiling && captured.hairAction != WardrobeHairAction.Keep &&
                    (captured.hairAction != WardrobeHairAction.Set || !string.IsNullOrWhiteSpace(captured.resolvedHairId));

                var second = await performer.OutfitAsync("second-outfit");
                await WaitForWardrobeQueue();
                var recall = await sceneBinding.RecallAsync(WardrobeTransition.Cut);
                await WaitForWardrobeQueue();
                WardrobeState restored = wardrobe.CurrentWardrobe;
                bool recalledState = recall.Status == WardrobeChangeStatus.Applied && restored.OutfitId == captured.presetId &&
                    restored.LayerCeiling == captured.layerCeiling &&
                    NormalizeHair(restored.EffectiveHairId) == NormalizeHair(captured.resolvedHairId);
                Check("serializedSceneWardrobeRecall", "capture, switch outfit, then recall exact outfit/layers/resolved hair",
                    "capture=" + capturedResolvedState + ";switch=" + second.Status + ";recall=" + recall.Status +
                    ";state=" + restored.OutfitId + "/" + restored.LayerCeiling + "/" + restored.EffectiveHairId,
                    capturedResolvedState && second.Status == WardrobeChangeStatus.Applied && recalledState);

                int originalMask = restored.VisibleMask;
                var remove = await performer.TryRemoveLayerAsync();
                await WaitForWardrobeQueue();
                int removedMask = wardrobe.CurrentWardrobe.VisibleMask;
                var add = await performer.TryAddLayerAsync();
                await WaitForWardrobeQueue();
                WardrobeState finalState = wardrobe.CurrentWardrobe;
                bool relativeCommands = remove.Status == WardrobeChangeStatus.Applied && add.Status == WardrobeChangeStatus.Applied &&
                    removedMask != originalMask && finalState.VisibleMask == originalMask && finalState.OutfitId == restored.OutfitId;
                Check("relativeLayerAuthoringCommands", "TryRemoveLayer and TryAddLayer round-trip independently",
                    "remove=" + remove.Status + "/" + removedMask + ";add=" + add.Status + "/" + finalState.VisibleMask +
                    ";outfit=" + finalState.OutfitId,
                    relativeCommands);
            }
            catch (Exception exception)
            {
                Check("playerFixtureException", "no exception", exception.ToString(), false);
            }

            var report = new Report
            {
                startedUtc = started,
                completedUtc = DateTime.UtcNow.ToString("O"),
                passed = _assertions.Count > 0 && _assertions.TrueForAll(assertion => assertion.passed),
                assertions = _assertions.ToArray()
            };
            string reportPath = ReadArgument("-wardrobePlayerReport");
            if (string.IsNullOrWhiteSpace(reportPath))
                reportPath = Path.Combine(Application.persistentDataPath, "wardrobe-player-report.json");
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath)));
                File.WriteAllText(reportPath, JsonUtility.ToJson(report, true));
                Debug.Log("WARDROBE_PLAYER_GATE_" + (report.passed ? "PASSED" : "FAILED") + ": " + reportPath);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
                Application.Quit(2);
                return;
            }
            Application.Quit(report.passed ? 0 : 1);
        }

        private async Awaitable WaitForWardrobeQueue()
        {
            for (int frame = 0; frame < 16; ++frame)
            {
                if (wardrobe != null && !wardrobe.CurrentWardrobe.IsChanging) return;
                await Awaitable.NextFrameAsync();
            }
            throw new TimeoutException("Player wardrobe queue did not settle within 16 frames.");
        }

        private void Check(string name, string expected, string actual, bool passed) =>
            _assertions.Add(new Assertion { name = name, expected = expected, actual = actual, passed = passed });

        private static string NormalizeHair(string hairId) =>
            string.IsNullOrWhiteSpace(hairId) || string.Equals(hairId, "none", StringComparison.OrdinalIgnoreCase)
                ? string.Empty : hairId;

        private static string ReadArgument(string key)
        {
            string[] arguments = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(arguments, key);
            return index >= 0 && index + 1 < arguments.Length ? arguments[index + 1].Trim('"') : string.Empty;
        }
    }
}
