using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DazPose.Performer;
using UnityEngine;

namespace DazPose.Validation
{
    /// <summary>Saved setup-scene startup; batch validation uses the same artist preview actor.</summary>
    public sealed class ClothingSetupPreviewRunner : MonoBehaviour
    {
        [SerializeField] private SuccubusPerformer actor;
        [SerializeField] private string initialOutfitId = "first-outfit";
        public string Status { get; private set; } = "Waiting for preview";
        [Serializable] private sealed class Assertion { public string name, detail; public bool passed; }
        [Serializable] private sealed class Report { public string result, exception; public List<Assertion> assertions = new List<Assertion>(); }
        private readonly Report _report = new Report();
#if UNITY_EDITOR
        public void Configure(SuccubusPerformer performer) => actor = performer;
#endif
        private async void Start()
        {
            try
            {
                for (int frame = 0; frame < 180 && actor != null && !actor.IsRuntimeReady; frame++) await Awaitable.NextFrameAsync();
                Check("setupActorReady", actor != null && actor.IsRuntimeReady, "Saved production actor initializes without injected setup.");
                var wardrobe = actor.GetComponent<PerformerWardrobe>();
                var draft = actor.GetComponent<ClothingSetupDraftBinding>();
                Check("savedDraftMatchesRuntimeBinding", draft != null && draft.Catalog == wardrobe.Catalog && draft.Configuration == wardrobe.Configuration,
                    "Prefab wardrobe overrides and saved authoring draft references agree.");
                var preset = wardrobe.Catalog.Presets.Single(p => p.PresetId == initialOutfitId);
                Applied(await actor.OutfitAsync(preset));
                if (actor.IsHidden) await actor.DissolveInAsync(.25f);
                Check("setupInitiallyDressedAndVisible", actor.VisibilityState == PerformerVisibilityState.Visible && (actor.CurrentWardrobe.VisibleMask == actor.CurrentWardrobe.PopulatedMask && !actor.CurrentWardrobe.IsNaked),
                    "Play opens a visible, dressed artist preview.");
                Status = "Ready";
                if (!Application.isBatchMode) return;
                int populated = actor.CurrentWardrobe.PopulatedMask;
                string hair = actor.CurrentWardrobe.EffectiveHairId;
                int expected = populated;
                for (int layer = 2; layer >= 0; layer--)
                {
                    if ((expected & (1 << layer)) == 0) continue;
                    expected &= ~(1 << layer);
                    Applied(await actor.TryRemoveLayerAsync());
                    Check("setupRemoveLayer" + layer, actor.CurrentWardrobe.VisibleMask == expected && actor.CurrentWardrobe.EffectiveHairId == hair, "Expected mask " + expected);
                }
                Check("setupNakedRetainsSelection", actor.CurrentWardrobe.IsNaked && actor.CurrentWardrobe.CanAddLayer && actor.CurrentWardrobe.OutfitId == initialOutfitId,
                    "Temporary nakedness retains the selected outfit.");
                for (int layer = 0; layer < 3; layer++)
                {
                    if ((populated & (1 << layer)) == 0) continue;
                    expected |= 1 << layer;
                    Applied(await actor.TryAddLayerAsync());
                    Check("setupAddLayer" + layer, actor.CurrentWardrobe.VisibleMask == expected && actor.CurrentWardrobe.EffectiveHairId == hair, "Expected mask " + expected);
                }
                Check("setupRestored", (actor.CurrentWardrobe.VisibleMask == actor.CurrentWardrobe.PopulatedMask && !actor.CurrentWardrobe.IsNaked) && actor.VisibilityState == PerformerVisibilityState.Visible, "Artist preview remains usable after removal/addition.");
                Finish(null);
            }
            catch (Exception exception) { Status = "Error: " + exception.Message; Debug.LogException(exception); Finish(exception.ToString()); }
        }
        private void Applied(WardrobeChangeResult result)
        {
            if (result.Status != WardrobeChangeStatus.Applied && result.Status != WardrobeChangeStatus.AlreadyEquipped)
                throw new InvalidOperationException(result.Status + ": " + result.Message);
        }
        private void Check(string name, bool passed, string detail)
        {
            _report.assertions.Add(new Assertion { name = name, passed = passed, detail = detail });
            if (!passed) throw new InvalidOperationException(name + ": " + detail);
        }
        private void Finish(string exception)
        {
            _report.result = exception == null ? "passed" : "failed"; _report.exception = exception;
            string output = Application.isEditor ? Path.GetFullPath("TestOutput/day5/setup-runtime.json")
                : Path.Combine(Path.GetDirectoryName(Application.dataPath), "day5-setup-runtime.json");
            Directory.CreateDirectory(Path.GetDirectoryName(output)); File.WriteAllText(output, JsonUtility.ToJson(_report, true));
            if (!Application.isBatchMode) return;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit(exception == null ? 0 : 1);
#endif
        }
    }
}
