using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DazPose.Performer;
using UnityEngine;

namespace DazPose.Validation
{
    /// <summary>Saved scene runner comparing an edited candidate with a fresh actor using its release.</summary>
    public sealed class Day5WardrobeRunner : MonoBehaviour
    {
        [SerializeField] private SuccubusPerformer actor;
        [SerializeField] private GameObject actorPrefab;
        [SerializeField] private WardrobeCatalog releasedCatalog;
        [SerializeField] private WardrobePreset candidateOutfit;
        [SerializeField] private string outfitId = "third-outfit";
        [SerializeField] private bool autoRun = true;
        [Serializable] private sealed class Assertion { public string name, detail; public bool passed; }
        [Serializable] private sealed class Report
        { public string result, exception, startedUtc, completedUtc; public List<Assertion> assertions = new List<Assertion>(); }
        private Report _report = new Report();
        private SuccubusPerformer _fresh;
        private SuccubusPerformer _previous;
        private bool _running, _finished;
        private string _status = "Ready";
        private float _started;
#if UNITY_EDITOR
        public void Configure(SuccubusPerformer preview, GameObject prefab, WardrobeCatalog release, WardrobePreset candidate)
        { actor = preview; actorPrefab = prefab; releasedCatalog = release; candidateOutfit = candidate; outfitId = candidate.PresetId; }
#endif
        private async void Start()
        {
            if (autoRun) Run();
            while (!_finished && Time.realtimeSinceStartup - _started < 120f) await Awaitable.NextFrameAsync();
            if (!_finished && _running) Finish(false, "Wardrobe validation exceeded 120 seconds.");
        }

        private async void Run()
        {
            if (_running) return;
            _running = true; _finished = false; _started = Time.realtimeSinceStartup;
            _report = new Report { startedUtc = DateTime.UtcNow.ToString("O") };
            try
            {
                var previousHolder = new GameObject("Previous release usability fixture"); previousHolder.SetActive(false);
                _previous = Instantiate(actorPrefab, previousHolder.transform).GetComponent<SuccubusPerformer>();
                previousHolder.transform.position = new Vector3(-100, 0, 100); previousHolder.SetActive(true);
                for (int i = 0; i < 180 && !_previous.IsRuntimeReady; i++) await Awaitable.NextFrameAsync();
                Check("previousReleaseReadyAfterFailedCandidates", _previous.IsRuntimeReady, "Original prefab release remains usable after transactional failure tests.");
                Applied(await _previous.OutfitAsync(outfitId), "previous release dress");
                int previousMask = _previous.CurrentWardrobe.PopulatedMask;
                Applied(await _previous.TryRemoveLayerAsync(), "previous release remove");
                Applied(await _previous.TryAddLayerAsync(), "previous release add");
                Check("previousReleaseStillDressesRemovesAndRestores", _previous.CurrentWardrobe.VisibleMask == previousMask,
                    "Previous release visible mask restored to " + previousMask);
                Destroy(previousHolder); _previous = null;
                var holder = new GameObject("Fresh released actor fixture"); holder.SetActive(false);
                var instance = Instantiate(actorPrefab, holder.transform);
                _fresh = instance.GetComponent<SuccubusPerformer>();
                var freshWardrobe = _fresh.GetComponent<PerformerWardrobe>();
                var freshBody = instance.GetComponentsInChildren<SkinnedMeshRenderer>(true).First(r => r.sharedMesh != null && r.sharedMesh.GetBlendShapeIndex("CapturedOpening") >= 0);
                freshWardrobe.ConfigureBinding(releasedCatalog, null, freshBody, _fresh);
                holder.transform.position = new Vector3(100, 0, 100); holder.SetActive(true);
                for (int i = 0; i < 180 && (!actor.IsRuntimeReady || !_fresh.IsRuntimeReady); i++) await Awaitable.NextFrameAsync();
                Check("savedActorAndFreshActorReady", actor.IsRuntimeReady && _fresh.IsRuntimeReady, "Both production actors initialized; fresh release has no draft configuration overlay.");
                Check("savedCandidateBindingSurvivedReopen", !actor.GetComponent<PerformerWardrobe>().Catalog.IsReleased && actor.GetComponent<PerformerWardrobe>().Catalog.Presets.Contains(candidateOutfit),
                    "Scene actor binds the saved candidate, rather than inheriting its prefab's release.");
                string hairKey = releasedCatalog.Presets.Where(p => p.HairAction == WardrobeHairAction.Set).Select(p => p.PresetId + "/" + p.HairId).First();
                Applied(await actor.SetHairAsync(hairKey), "candidate hair");
                Applied(await _fresh.SetHairAsync(hairKey), "release hair");
                Check("explicitHairPresentBeforeUndressing", actor.CurrentWardrobe.EffectiveHairId != "none", hairKey);
                if (actor.IsHidden) await actor.DissolveInAsync(.25f);
                await BothOutfits(); Compare(7, "fully dressed");
                for (int cycle = 0; cycle < 2; cycle++)
                {
                    foreach (int mask in new[] { 3, 1, 0 })
                    {
                        Applied(await actor.TryRemoveLayerAsync(), "candidate remove");
                        Applied(await _fresh.TryRemoveLayerAsync(), "release remove");
                        Compare(mask, "remove cycle " + cycle);
                        if (!Application.isBatchMode) await Awaitable.WaitForSecondsAsync(1.5f);
                    }
                    var naked = actor.CurrentWardrobe;
                    Check("nakedKeepsSelection_" + cycle, naked.OutfitId == outfitId && naked.CanAddLayer && naked.IsNaked, "Remove-all retains the selected outfit.");
                    string hair = naked.EffectiveHairId;
                    var noRemove = await actor.TryRemoveLayerAsync();
                    Check("nakedRemovalBoundary_" + cycle, noRemove.Status == WardrobeChangeStatus.NoChange, noRemove.Status.ToString());
                    foreach (int mask in new[] { 1, 3, 7 })
                    {
                        Applied(await actor.TryAddLayerAsync(), "candidate add");
                        Applied(await _fresh.TryAddLayerAsync(), "release add");
                        Compare(mask, "add cycle " + cycle);
                        Check("independentHair_" + cycle + "_" + mask, actor.CurrentWardrobe.EffectiveHairId == hair, hair);
                        if (!Application.isBatchMode) await Awaitable.WaitForSecondsAsync(1.5f);
                    }
                }
                Applied(await actor.OutfitAsync("unclothed"), "explicit unclothed");
                Check("explicitUnclothedClearsRememberedSelection", string.IsNullOrEmpty(actor.CurrentWardrobe.OutfitId) && !actor.CurrentWardrobe.CanAddLayer, "Unclothed is distinct from naked preview.");
                await BothOutfits(); Compare(7, "restore after explicit unclothed");
                Check("releaseReportsSnapshotRevision", _fresh.CurrentWardrobe.ConfigurationRevision == releasedCatalog.Presets.Single(p => p.PresetId == outfitId).ConfigurationRevision,
                    _fresh.CurrentWardrobe.ConfigurationRevision.ToString());
                Destroy(holder); _fresh = null;
                Finish(true, null);
            }
            catch (Exception exception) { Finish(false, exception.ToString()); }
        }

        private async Awaitable BothOutfits()
        {
            Applied(await actor.OutfitAsync(candidateOutfit), "candidate dress");
            Applied(await _fresh.OutfitAsync(outfitId), "fresh release dress");
        }
        private void Compare(int expectedMask, string phase)
        {
            _status = phase;
            var a = actor.CurrentWardrobe; var b = _fresh.CurrentWardrobe;
            bool sameParts = Enumerable.Range(0, 3).All(i => a.LayerPieceIds[i].OrderBy(x => x).SequenceEqual(b.LayerPieceIds[i].OrderBy(x => x)));
            Check(phase, a.VisibleMask == expectedMask && b.VisibleMask == expectedMask && a.PopulatedMask == 7 && b.PopulatedMask == 7 &&
                a.OutfitId == b.OutfitId && sameParts && a.CurrentFootwearPoseActive == b.CurrentFootwearPoseActive && a.EffectiveFootwearId == b.EffectiveFootwearId,
                "candidate=" + a.VisibleMask + ";release=" + b.VisibleMask + ";partsEqual=" + sameParts + ";shoe=" + a.EffectiveFootwearId + ";bentFeet=" + a.CurrentFootwearPoseActive);
            if (expectedMask == 3 || expectedMask == 1)
                Check("hosieryKeepsBentFeet_" + phase, a.CurrentFootwearPoseActive && b.CurrentFootwearPoseActive &&
                    !actor.GetComponent<FootwearPoseDriver>().IsFootwearActive && !_fresh.GetComponent<FootwearPoseDriver>().IsFootwearActive,
                    "Shoes removed; hosiery retains bent feet with actual shoe support off.");
        }
        private void Applied(WardrobeChangeResult result, string phase)
        { if (result.Status != WardrobeChangeStatus.Applied && result.Status != WardrobeChangeStatus.AlreadyEquipped) throw new InvalidOperationException(phase + ": " + result.Status + " / " + result.FailureCode + ": " + result.Message); }
        private void Check(string name, bool passed, string detail)
        {
            _report.assertions.Add(new Assertion { name = name, passed = passed, detail = detail });
            if (!passed) throw new InvalidOperationException(name + ": " + detail);
        }
        private void Finish(bool passed, string exception)
        {
            if (_finished) return;
            _finished = true; _running = false;
            _report.result = passed ? "passed" : "failed"; _report.exception = exception; _report.completedUtc = DateTime.UtcNow.ToString("O");
            _status = "Day 5 " + _report.result;
            if (_fresh != null) { Destroy(_fresh.transform.parent.gameObject); _fresh = null; }
            if (_previous != null) { Destroy(_previous.transform.parent.gameObject); _previous = null; }
            string output = Application.isEditor ? Path.GetFullPath("TestOutput/day5/runtime.json") : Path.Combine(Path.GetDirectoryName(Application.dataPath), "day5-runtime.json");
            Directory.CreateDirectory(Path.GetDirectoryName(output)); File.WriteAllText(output, JsonUtility.ToJson(_report, true));
            Debug.Log(_status + ": " + output);
            if (Application.isBatchMode)
            {
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit(passed ? 0 : 1);
#endif
            }
        }
        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(15, 15, 410, 175), GUI.skin.box);
            GUILayout.Label("Day 5 wardrobe validation — " + _status);
            if (actor != null && actor.CurrentWardrobe != null)
                GUILayout.Label("Maid layers visible: " + actor.CurrentWardrobe.VisibleMask + " / 7");
            if (!_running && GUILayout.Button("Run saved wardrobe test")) Run();
            if (!_running && actor != null)
            {
                if (GUILayout.Button("Dress / redress")) actor.Outfit(candidateOutfit);
                if (GUILayout.Button("Remove next layer")) actor.TryRemoveLayer();
                if (GUILayout.Button("Add next layer")) actor.TryAddLayer();
            }
            GUILayout.EndArea();
        }
    }
}
