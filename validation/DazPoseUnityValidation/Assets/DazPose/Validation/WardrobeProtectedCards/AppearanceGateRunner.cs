using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DazPose.Performer;
using UnityEngine;

namespace DazPose.Validation.WardrobeProtectedCards
{
    /// <summary>Saved, user-operated Gate A controls. It never starts a scene or emits hardware output.</summary>
    public sealed class AppearanceGateRunner : MonoBehaviour
    {
        [SerializeField] private SuccubusPerformer actor;
        [SerializeField] private WardrobeCatalog catalog;
        [SerializeField] private WardrobeRigManifest manifest;
        [SerializeField] private string catalogAssetPath, manifestAssetPath, manifestGuid, rigAssetList, previousGeneration;
        [SerializeField] private string outfitId = "first-outfit";
        [SerializeField] private string emikoHairKey = "first-outfit/EmikoHair_59875";
        [SerializeField] private bool visible = true;
        [Serializable] private sealed class Assertion { public string name, detail; public bool passed; }
        [Serializable] private sealed class Report
        {
            public string gate = "A", result, previousGeneration, generation, catalogPath, manifestPath, manifestGuid, rigAssets, error, startedUtc, completedUtc;
            public string actor, outfit, hair, visibleGarments, bodyMesh, visibleAttachmentMeshes, footwear;
            public int visibleAttachmentCount;
            public List<Assertion> assertions = new List<Assertion>();
        }
        private Report report = new Report();
        private string status = "Ready — use the controls below.";
        private bool busy;
        private Vector2 scroll;
        public void Configure(SuccubusPerformer performer, WardrobeCatalog source, WardrobeRigManifest sourceManifest,
            string sourceCatalogPath, string sourceManifestPath, string sourceManifestGuid, string sourceRigAssetList, string oldGeneration,
            string preset, string hairKey)
        {
            actor = performer; catalog = source; manifest = sourceManifest; catalogAssetPath = sourceCatalogPath;
            manifestAssetPath = sourceManifestPath; manifestGuid = sourceManifestGuid; rigAssetList = sourceRigAssetList;
            previousGeneration = oldGeneration;
            outfitId = preset; emikoHairKey = hairKey;
        }

        private void OnGUI()
        {
            if (!visible) return;
            GUILayout.BeginArea(new Rect(14, 14, 430, 570), GUI.skin.window);
            GUILayout.Label("Appearance Gate — Gate A", GUI.skin.box);
            GUILayout.Label(status, GUI.skin.label);
            GUILayout.Label("Actor: " + (actor != null ? actor.name : "MISSING") + " | Release: " + (catalog != null ? catalog.ReleasedGenerationId : "MISSING"));
            if (actor != null)
            {
                var state = actor.GetComponent<PerformerWardrobe>()?.CurrentWardrobe;
                if (state != null)
                {
                    GUILayout.Label("Outfit: " + state.OutfitId + " | Layers: " + state.VisibleMask + "/" + state.PopulatedMask + " | Hair: " + state.EffectiveHairId);
                    GUILayout.Label("Visible garments: " + string.Join(", ", state.LayerPieceIds.SelectMany(x => x)));
                }
            }
            using (new GUILayout.VerticalScope())
            {
                using (new GUILayout.HorizontalScope()) { Button("Set Emiko hair", () => SetHair(emikoHairKey)); Button("Dress outfit 1", Dress); }
                using (new GUILayout.HorizontalScope()) { Button("Remove one layer", Remove); Button("Add one layer", Add); }
                using (new GUILayout.HorizontalScope()) { Button("Set next released hair", NextHair); Button("Clear hair", () => SetHair("none")); }
                Button("Exercise second actor", ExerciseSecondActor);
                Button("Write report", WriteReport);
            }
            scroll = GUILayout.BeginScrollView(scroll, GUILayout.Height(190)); GUILayout.Label(status, GUI.skin.box); GUILayout.EndScrollView();
            GUILayout.EndArea();
        }
        private void Button(string label, Action action)
        { using (new GUILayout.HorizontalScope()) { using (new GUILayout.VerticalScope()) if (GUILayout.Button(label, GUILayout.Height(28)) && !busy) action(); } }
        private PerformerWardrobe Wardrobe => actor != null ? actor.GetComponent<PerformerWardrobe>() : null;
        private async void SetHair(string key)
        {
            if (!Ready()) return; busy = true;
            try
            {
                var before = Wardrobe.CurrentWardrobe;
                var result = await Wardrobe.SetHairAsync(key); var after = Wardrobe.CurrentWardrobe;
                bool passed = Applied(result.Status) && after.EffectiveHairId == (key == "none" ? "none" : key) &&
                    before.OutfitId == after.OutfitId && before.VisibleMask == after.VisibleMask;
                Record("setHair:" + key, passed, result.Status + ": hair=" + after.EffectiveHairId + "; outfit/layers preserved=" + (before.OutfitId == after.OutfitId && before.VisibleMask == after.VisibleMask));
            }
            catch (Exception error) { Record("setHair:" + key, false, error.ToString()); }
            finally { busy = false; }
        }
        private async void Dress()
        {
            if (!Ready()) return; busy = true;
            try
            {
                if (!catalog.TryResolve(outfitId, out var preset, out var error)) throw new InvalidOperationException("Cannot resolve outfit: " + error);
                var result = await Wardrobe.SetOutfitAsync(preset); var state = Wardrobe.CurrentWardrobe;
                Record("dress:" + outfitId, Applied(result.Status) && state.OutfitId == outfitId && state.VisibleMask == state.PopulatedMask,
                    result.Status + ": outfit=" + state.OutfitId + "; visible=" + state.VisibleMask + "/" + state.PopulatedMask + "; garments=" + string.Join(",", state.LayerPieceIds.SelectMany(x => x)));
            }
            catch (Exception error) { Record("dress:" + outfitId, false, error.ToString()); }
            finally { busy = false; }
        }
        private async void Remove()
        {
            if (!Ready()) return; busy = true;
            try
            {
                var before = Wardrobe.CurrentWardrobe; var result = await Wardrobe.TryRemoveLayerAsync(); var after = Wardrobe.CurrentWardrobe;
                bool expected = result.Status == WardrobeChangeStatus.Applied
                    ? before.HighestVisibleLayer.HasValue && after.VisibleMask == (before.VisibleMask & ~(1 << before.HighestVisibleLayer.Value))
                    : result.Status == WardrobeChangeStatus.NoChange && before.IsNaked && result.NoChangeReason == WardrobeNoChangeReason.AlreadyNaked;
                Record("removeLayer", expected, result.Status + ": visible=" + before.VisibleMask + "→" + after.VisibleMask + "; remaining=" + string.Join(",", after.LayerPieceIds.SelectMany(x => x)));
            }
            catch (Exception error) { Record("removeLayer", false, error.ToString()); }
            finally { busy = false; }
        }
        private async void Add()
        {
            if (!Ready()) return; busy = true;
            try
            {
                var before = Wardrobe.CurrentWardrobe; var result = await Wardrobe.TryAddLayerAsync(); var after = Wardrobe.CurrentWardrobe;
                int nextLayer = -1;
                for (int layer = 0; layer <= before.LayerCeiling; ++layer)
                    if ((before.PopulatedMask & (1 << layer)) != 0 && (before.VisibleMask & (1 << layer)) == 0) { nextLayer = layer; break; }
                bool expected = result.Status == WardrobeChangeStatus.Applied
                    ? nextLayer >= 0 && after.VisibleMask == (before.VisibleMask | (1 << nextLayer))
                    : result.Status == WardrobeChangeStatus.NoChange && !before.CanAddLayer && result.NoChangeReason == WardrobeNoChangeReason.FullyDressed;
                Record("addLayer", expected, result.Status + ": visible=" + before.VisibleMask + "→" + after.VisibleMask + "; garments=" + string.Join(",", after.LayerPieceIds.SelectMany(x => x)));
            }
            catch (Exception error) { Record("addLayer", false, error.ToString()); }
            finally { busy = false; }
        }
        private void NextHair()
        {
            if (catalog == null) { status = "Catalog is missing."; return; }
            string current = Wardrobe != null ? Wardrobe.CurrentWardrobe.EffectiveHairId : "none";
            string next = catalog.Presets.Where(x => x.HairAction == WardrobeHairAction.Set)
                .Select(x => x.PresetId + "/" + x.HairId).FirstOrDefault(x => x != current && x != emikoHairKey);
            if (string.IsNullOrEmpty(next)) { status = "No second released hair key is available."; return; }
            SetHair(next);
        }
        private async void ExerciseSecondActor()
        {
            if (!Ready()) return; busy = true; SuccubusPerformer second = null;
            try
            {
                var clone = Instantiate(actor.gameObject, actor.transform.position + actor.transform.right * 2.5f, actor.transform.rotation);
                clone.name = "Gate A independent wardrobe owner"; second = clone.GetComponent<SuccubusPerformer>();
                if (second == null) throw new InvalidOperationException("Cloned actor has no SuccubusPerformer.");
                var w = second.GetComponent<PerformerWardrobe>();
                var body = clone.GetComponentsInChildren<SkinnedMeshRenderer>(true).FirstOrDefault(x => x.sharedMesh != null && x.sharedMesh.GetBlendShapeIndex("CapturedOpening") >= 0);
                if (w == null || body == null) throw new InvalidOperationException("Second actor is missing its wardrobe or canonical body.");
                var binding = clone.GetComponent<WardrobeRigBinding>();
                var source = actor.GetComponent<WardrobeRigBinding>();
                if (binding == null || source == null) throw new InvalidOperationException("Saved Gate A scene is missing actor rig bindings.");
                if (binding.Manifest != source.Manifest || !binding.ClothingRoot.IsChildOf(clone.transform) || !binding.HairRoot.IsChildOf(clone.transform))
                    throw new InvalidOperationException("Cloned actor did not retain independent attachment roots and the shared immutable rig manifest.");
                for (int i = 0; i < 120 && !second.IsRuntimeReady; i++) await Awaitable.NextFrameAsync();
                if (!second.IsRuntimeReady) throw new InvalidOperationException("Second actor wardrobe failed to initialize: " + w.BindingDiagnostic);
                if (!catalog.TryResolve(outfitId, out var preset, out var resolveError)) throw new InvalidOperationException("Cannot resolve outfit: " + resolveError);
                var dress = await w.SetOutfitAsync(preset);
                if (!Applied(dress.Status)) throw new InvalidOperationException("Second actor outfit failed: " + dress.Status + " / " + dress.Message);
                string firstBefore = Wardrobe.CurrentWardrobe.EffectiveHairId;
                string alternateHair = catalog.Presets.Where(x => x.HairAction == WardrobeHairAction.Set)
                    .Select(x => x.PresetId + "/" + x.HairId).FirstOrDefault(x => x != emikoHairKey);
                if (string.IsNullOrEmpty(alternateHair)) throw new InvalidOperationException("A second released hair key is required for the two-owner isolation case.");
                var hairResult = await w.SetHairAsync(alternateHair);
                if (!Applied(hairResult.Status)) throw new InvalidOperationException("Second actor alternate hair failed: " + hairResult.Status + " / " + hairResult.Message);
                var layerResult = await w.TryRemoveLayerAsync();
                if (!Applied(layerResult.Status)) throw new InvalidOperationException("Second actor layer removal failed: " + layerResult.Status + " / " + layerResult.Message);
                var firstBones = VisibleBones(actor.GetComponent<LaraWardrobe>());
                var secondBones = VisibleBones(second.GetComponent<LaraWardrobe>());
                bool sharedSkeletonInstances = firstBones.Overlaps(secondBones);
                Record("independentOwners", Wardrobe.CurrentWardrobe.EffectiveHairId == firstBefore && Wardrobe != w && !sharedSkeletonInstances &&
                    Wardrobe.CurrentWardrobe.VisibleMask != w.CurrentWardrobe.VisibleMask && w.CurrentWardrobe.EffectiveHairId == alternateHair,
                    "firstOwner=" + actor.name + "; secondOwner=" + second.name + "; distinctWardrobeComponents=" + (Wardrobe != w) +
                    "; sharedBoneTransforms=" + sharedSkeletonInstances + "; firstHair=" + Wardrobe.CurrentWardrobe.EffectiveHairId +
                    "; secondHair=" + w.CurrentWardrobe.EffectiveHairId + "; firstMask=" + Wardrobe.CurrentWardrobe.VisibleMask + "; secondMask=" + w.CurrentWardrobe.VisibleMask);
            }
            catch (Exception error) { Record("independentOwners", false, error.ToString()); }
            finally { if (second != null) Destroy(second.gameObject); busy = false; }
        }
        private bool Ready()
        {
            if (actor == null || Wardrobe == null || catalog == null) { status = "Gate scene is missing actor/catalog references."; return false; }
            if (!actor.IsRuntimeReady || !Wardrobe.IsRuntimeInitialized)
            { status = "Wardrobe is not ready: " + (Wardrobe.BindingDiagnostic ?? "waiting for performer and appearance initialization"); return false; }
            return true;
        }
        private static bool Applied(WardrobeChangeStatus value) => value == WardrobeChangeStatus.Applied || value == WardrobeChangeStatus.AlreadyEquipped;
        private static HashSet<Transform> VisibleBones(LaraWardrobe binder)
        {
            var result = new HashSet<Transform>();
            if (binder == null) return result;
            foreach (var piece in binder.Pieces.Where(x => x != null && x.visible && x.renderer != null && x.renderer.enabled))
                foreach (var bone in piece.renderer.bones) if (bone != null) result.Add(bone);
            return result;
        }
        private void Record(string name, bool passed, string detail)
        { report.assertions.Add(new Assertion { name = name, passed = passed, detail = detail }); report.result = report.assertions.All(x => x.passed) ? "in_progress" : "failed"; status = (passed ? "PASS " : "FAIL ") + name + " — " + detail; }
        private void WriteReport()
        {
            if (report.startedUtc == null) report.startedUtc = DateTime.UtcNow.ToString("O");
            report.completedUtc = DateTime.UtcNow.ToString("O"); report.generation = catalog != null ? catalog.ReleasedGenerationId : null;
            report.catalogPath = catalogAssetPath; report.manifestPath = manifestAssetPath; report.manifestGuid = manifestGuid;
            report.rigAssets = rigAssetList; report.previousGeneration = previousGeneration; report.actor = actor != null ? actor.name : null;
            if (Wardrobe != null) { var state = Wardrobe.CurrentWardrobe; report.outfit = state.OutfitId; report.hair = state.EffectiveHairId; report.visibleGarments = string.Join(",", state.LayerPieceIds.SelectMany(x => x)); }
            var body = actor != null ? actor.GetComponentsInChildren<SkinnedMeshRenderer>(true).FirstOrDefault(x => x.sharedMesh != null && x.sharedMesh.GetBlendShapeIndex("CapturedOpening") >= 0) : null;
            report.bodyMesh = body != null && body.sharedMesh != null ? body.sharedMesh.name : null;
            report.footwear = Wardrobe != null ? Wardrobe.CurrentWardrobe.EffectiveFootwearId : null;
            var binder = actor != null ? actor.GetComponent<LaraWardrobe>() : null;
            var visiblePieces = binder != null ? binder.Pieces.Where(x => x != null && x.visible && x.renderer != null && x.renderer.enabled).ToArray() : Array.Empty<LaraWardrobe.Piece>();
            report.visibleAttachmentCount = visiblePieces.Length;
            report.visibleAttachmentMeshes = string.Join(",", visiblePieces.Select(x => x.stableKey + "=" + (x.renderer.sharedMesh != null ? x.renderer.sharedMesh.name : "missing")));
            report.result = report.assertions.All(x => x.passed) && report.assertions.Count > 0 ? "passed" : "failed_or_incomplete";
            var repoRoot = Directory.GetParent(Application.dataPath).Parent.Parent;
            string path = Path.Combine(repoRoot.FullName, "docs/wardrobe-directing/evidence/gate-a-native.json");
            Directory.CreateDirectory(Path.GetDirectoryName(path)); File.WriteAllText(path, JsonUtility.ToJson(report, true)); status = "Report written: " + path;
        }
    }
}
