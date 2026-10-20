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
        [SerializeField] private SuccubusPerformer secondActorTemplate;
        [SerializeField] private int fixtureRevision;
        public const int CurrentFixtureRevision = 3;
        [SerializeField] private WardrobeCatalog catalog;
        [SerializeField] private WardrobeRigManifest manifest;
        [SerializeField] private string catalogAssetPath, manifestAssetPath, manifestGuid, rigAssetList, previousGeneration;
        [SerializeField] private string outfitId = "first-outfit";
        [SerializeField] private string emikoHairKey = "first-outfit/EmikoHair_59875";
        [SerializeField] private bool visible = true;
        [Serializable] private sealed class Assertion { public string name, detail; public bool passed; }
        [Serializable] private sealed class Report
        {
            public string gate = "A", result, previousGeneration, generation, catalogPath, manifestPath, manifestGuid, rigAssets, error, startedUtc, completedUtc, hairCoverage;
            public string actor, outfit, hair, visibleGarments, bodyMesh, visibleAttachmentMeshes, footwear;
            public int visibleAttachmentCount;
            public int secondActorsCreated, secondActorsDisposed, secondActorAttachmentsDisposed;
            public List<Assertion> assertions = new List<Assertion>();
            public List<Observation> observations = new List<Observation>();
        }
        [Serializable] private sealed class Observation
        {
            public string operation, outfit, hair, visibleGarments, fit, bodyMesh, attachments, boneOwners;
            public int mask, attachmentInstances;
        }
        private Report report = new Report();
        private string status = "Ready — use the controls below.";
        private bool busy;
        private SuccubusPerformer liveSecondActor;
        private Vector2 scroll;
        public void Configure(SuccubusPerformer performer, WardrobeCatalog source, WardrobeRigManifest sourceManifest,
            string sourceCatalogPath, string sourceManifestPath, string sourceManifestGuid, string sourceRigAssetList, string oldGeneration,
            string preset, string hairKey, SuccubusPerformer template)
        {
            actor = performer; catalog = source; manifest = sourceManifest; catalogAssetPath = sourceCatalogPath;
            manifestAssetPath = sourceManifestPath; manifestGuid = sourceManifestGuid; rigAssetList = sourceRigAssetList;
            previousGeneration = oldGeneration;
            outfitId = preset; emikoHairKey = hairKey;
            secondActorTemplate = template; fixtureRevision = CurrentFixtureRevision;
        }
        public bool MatchesSavedSetup(WardrobeCatalog source, WardrobeRigManifest sourceManifest)
            => fixtureRevision == CurrentFixtureRevision && catalog == source && manifest == sourceManifest && actor != null &&
                !actor.StartsHidden && actor.gameObject.activeSelf && MatchesActor(actor, source, sourceManifest) && secondActorTemplate != null &&
                !secondActorTemplate.gameObject.activeSelf && MatchesActor(secondActorTemplate, source, sourceManifest) &&
                actor.GetComponent<PerformerWardrobe>().Body != secondActorTemplate.GetComponent<PerformerWardrobe>().Body;

        private static bool MatchesActor(SuccubusPerformer performer, WardrobeCatalog source, WardrobeRigManifest sourceManifest)
        {
            var wardrobe = performer.GetComponent<PerformerWardrobe>(); var binding = performer.GetComponent<WardrobeRigBinding>();
            return wardrobe != null && wardrobe.Catalog == source && wardrobe.Body != null && wardrobe.Body.transform.IsChildOf(performer.transform) &&
                binding != null && wardrobe.RigBinding == binding && binding.Manifest == sourceManifest && binding.ClothingRoot != null && binding.HairRoot != null &&
                binding.ClothingRoot.IsChildOf(wardrobe.Body.transform) && binding.HairRoot.IsChildOf(wardrobe.Body.transform);
        }

        private void Awake() { report.startedUtc = DateTime.UtcNow.ToString("O"); }

        private void OnGUI()
        {
            if (!visible) return;
            GUILayout.BeginArea(new Rect(14, 14, Mathf.Max(240, Mathf.Min(520, Screen.width - 28)), Mathf.Max(180, Screen.height - 28)), GUI.skin.window);
            scroll = GUILayout.BeginScrollView(scroll);
            GUILayout.Label("Appearance Gate — Gate A", GUI.skin.box);
            GUILayout.Label(status, GUI.skin.label);
            GUILayout.Label("Actor: " + (actor != null ? actor.name : "MISSING") + " | Release: " + (catalog != null ? catalog.ReleasedGenerationId : "MISSING"));
            if (actor != null)
            {
                var state = actor.GetComponent<PerformerWardrobe>()?.CurrentWardrobe;
                if (state != null)
                {
                    GUILayout.Label("Outfit: " + state.OutfitId + " | Layers: " + state.VisibleMask + "/" + state.PopulatedMask + " | Hair: " + state.EffectiveHairId);
                    GUILayout.Label("Visible garments: " + string.Join(", ", VisibleGarments(state)));
                }
            }
            using (new GUILayout.VerticalScope())
            {
                using (new GUILayout.HorizontalScope()) { Button("Set Emiko hair", () => SetHair(emikoHairKey)); Button("Dress outfit 1", Dress); }
                using (new GUILayout.HorizontalScope()) { Button("Remove one layer", Remove); Button("Add one layer", Add); }
                using (new GUILayout.HorizontalScope()) { Button(manifest != null && manifest.Hair.Length > 1 ? "Set next released hair" : "Toggle Emiko / no hair", NextHair); Button("Clear hair", () => SetHair("none")); }
                Button("Exercise second actor", ExerciseSecondActor);
                Button("Remove second actor", RemoveSecondActor);
                Button("Write report", WriteReport);
            }
            GUILayout.EndScrollView();
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
                string beforeHair = Wardrobe.CurrentWardrobe.EffectiveHairId;
                var result = await Wardrobe.SetOutfitAsync(preset); var state = Wardrobe.CurrentWardrobe;
                Record("dress:" + outfitId, Applied(result.Status) && state.OutfitId == outfitId && state.VisibleMask == state.PopulatedMask && beforeHair == state.EffectiveHairId,
                    result.Status + ": outfit=" + state.OutfitId + "; visible=" + state.VisibleMask + "/" + state.PopulatedMask + "; garments=" + string.Join(",", VisibleGarments(state)) + "; hair preserved=" + (beforeHair == state.EffectiveHairId));
                Record("hairPreserved:" + beforeHair, beforeHair == state.EffectiveHairId, "Dress preserves the selected hair: " + beforeHair);
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
                Record("remove:" + before.VisibleMask + "->" + after.VisibleMask, expected, result.Status + ": " + result.NoChangeReason + "; remaining=" + string.Join(",", VisibleGarments(after)));
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
                int nextLayer = WardrobeLayerState.LowestHidden(before.PopulatedMask, before.LayerCeiling);
                bool expected = result.Status == WardrobeChangeStatus.Applied
                    ? nextLayer >= 0 && after.VisibleMask == (before.VisibleMask | (1 << nextLayer))
                    : result.Status == WardrobeChangeStatus.NoChange && !before.CanAddLayer && result.NoChangeReason == WardrobeNoChangeReason.FullyDressed;
                Record("add:" + before.VisibleMask + "->" + after.VisibleMask, expected, result.Status + ": " + result.NoChangeReason + "; garments=" + string.Join(",", VisibleGarments(after)));
            }
            catch (Exception error) { Record("addLayer", false, error.ToString()); }
            finally { busy = false; }
        }
        private void NextHair()
        {
            if (catalog == null) { status = "Catalog is missing."; return; }
            string current = Wardrobe != null ? Wardrobe.CurrentWardrobe.EffectiveHairId : "none";
            string next = manifest.Hair.Select(x => x.stableHairKey).FirstOrDefault(x => x != current);
            if (string.IsNullOrEmpty(next)) next = "none";
            SetHair(next);
        }
        private async void ExerciseSecondActor()
        {
            if (!Ready()) return; busy = true; SuccubusPerformer second = null; GameObject clone = null;
            try
            {
                if (liveSecondActor != null) await ReleaseSecondActor();
                if (secondActorTemplate == null || secondActorTemplate.gameObject.activeSelf)
                    throw new InvalidOperationException("Gate scene needs its saved inactive second-actor template. Prepare / resume Gate A creates it.");
                clone = Instantiate(secondActorTemplate.gameObject, actor.transform.position + actor.transform.right * 2.5f, actor.transform.rotation);
                clone.name = "Gate A independent wardrobe owner"; second = clone.GetComponent<SuccubusPerformer>();
                report.secondActorsCreated++;
                if (second == null) throw new InvalidOperationException("Cloned actor has no SuccubusPerformer.");
                var w = second.GetComponent<PerformerWardrobe>();
                var body = w != null ? w.Body : null;
                if (w == null || body == null) throw new InvalidOperationException("Second actor is missing its wardrobe or canonical body.");
                var binding = clone.GetComponent<WardrobeRigBinding>();
                var source = actor.GetComponent<WardrobeRigBinding>();
                if (binding == null || source == null) throw new InvalidOperationException("Saved Gate A scene is missing actor rig bindings.");
                if (binding.Manifest != source.Manifest || !binding.ClothingRoot.IsChildOf(clone.transform) || !binding.HairRoot.IsChildOf(clone.transform))
                    throw new InvalidOperationException("Cloned actor did not retain independent attachment roots and the shared immutable rig manifest.");
                clone.SetActive(true);
                for (int i = 0; i < 120 && (!second.IsRuntimeReady || !w.IsRuntimeInitialized); i++) await Awaitable.NextFrameAsync();
                if (!second.IsRuntimeReady || !w.IsRuntimeInitialized) throw new InvalidOperationException("Second actor wardrobe failed to initialize: " + w.BindingDiagnostic);
                if (!catalog.TryResolve(outfitId, out var preset, out var resolveError)) throw new InvalidOperationException("Cannot resolve outfit: " + resolveError);
                var dress = await w.SetOutfitAsync(preset);
                if (!Applied(dress.Status)) throw new InvalidOperationException("Second actor outfit failed: " + dress.Status + " / " + dress.Message);
                var firstBefore = Wardrobe.CurrentWardrobe;
                string alternateHair = manifest.Hair.Select(x => x.stableHairKey).FirstOrDefault(x => x != firstBefore.EffectiveHairId) ?? "none";
                var hairResult = await w.SetHairAsync(alternateHair);
                if (!Applied(hairResult.Status)) throw new InvalidOperationException("Second actor alternate hair failed: " + hairResult.Status + " / " + hairResult.Message);
                if (w.CurrentWardrobe.VisibleMask == firstBefore.VisibleMask)
                {
                    var layerResult = await w.TryRemoveLayerAsync();
                    if (!Applied(layerResult.Status)) throw new InvalidOperationException("Second actor layer removal failed: " + layerResult.Status + " / " + layerResult.Message);
                }
                var firstBones = VisibleBones(actor.GetComponent<LaraWardrobe>());
                var secondBones = VisibleBones(second.GetComponent<LaraWardrobe>());
                bool sharedSkeletonInstances = firstBones.Overlaps(secondBones);
                bool secondProjection = CheckProjection(second, w, out string secondDetail);
                Record("independentOwners", Wardrobe.CurrentWardrobe.EffectiveHairId == firstBefore.EffectiveHairId &&
                    Wardrobe.CurrentWardrobe.VisibleMask == firstBefore.VisibleMask && Wardrobe.CurrentWardrobe.OutfitId == firstBefore.OutfitId &&
                    Wardrobe != w && firstBones.Count > 0 && secondBones.Count > 0 && !sharedSkeletonInstances && secondProjection &&
                    Wardrobe.CurrentWardrobe.VisibleMask != w.CurrentWardrobe.VisibleMask && w.CurrentWardrobe.EffectiveHairId == alternateHair,
                    "firstOwner=" + actor.name + "; secondOwner=" + second.name + "; distinctWardrobeComponents=" + (Wardrobe != w) +
                    "; sharedBoneTransforms=" + sharedSkeletonInstances + "; firstHair=" + Wardrobe.CurrentWardrobe.EffectiveHairId +
                    "; secondHair=" + w.CurrentWardrobe.EffectiveHairId + "; firstMask=" + Wardrobe.CurrentWardrobe.VisibleMask + "; secondMask=" + w.CurrentWardrobe.VisibleMask + "; " + secondDetail);
                liveSecondActor = second; clone = null;
            }
            catch (Exception error) { Record("independentOwners", false, error.ToString()); }
            finally { if (clone != null) Destroy(clone); busy = false; }
        }
        private async void RemoveSecondActor()
        {
            if (!Ready()) return; busy = true;
            try { await ReleaseSecondActor(); }
            catch (Exception error) { Record("secondActorDisposed", false, error.ToString()); }
            finally { busy = false; }
        }
        private async Awaitable ReleaseSecondActor()
        {
            if (liveSecondActor == null) { status = "No second actor is active. Exercise second actor first."; return; }
            var previous = liveSecondActor; liveSecondActor = null;
            var descriptors = previous.GetComponentsInChildren<WardrobeAttachmentDescriptor>(true);
            var bones = descriptors.SelectMany(descriptor => descriptor.Parts).SelectMany(part => part.renderer.bones).Distinct().ToArray();
            Destroy(previous.gameObject);
            await Awaitable.NextFrameAsync();
            bool released = previous == null && descriptors.All(descriptor => descriptor == null) && bones.All(bone => bone == null);
            if (released) { report.secondActorsDisposed++; report.secondActorAttachmentsDisposed += descriptors.Length; }
            Record("secondActorDisposed", released, "actor/attachment/skeleton objects released=" + released + "; attachment instances=" + descriptors.Length);
        }
        private bool Ready()
        {
            if (actor == null || Wardrobe == null || catalog == null) { status = "Gate scene is missing actor/catalog references."; return false; }
            if (!actor.IsRuntimeReady || !Wardrobe.IsRuntimeInitialized)
            { status = "Wardrobe is not ready: " + (Wardrobe.BindingDiagnostic ?? "waiting for performer and appearance initialization"); return false; }
            return true;
        }
        private static bool Applied(WardrobeChangeStatus value) => value == WardrobeChangeStatus.Applied || value == WardrobeChangeStatus.AlreadyEquipped;
        private static string[] VisibleGarments(WardrobeState state) => state.LayerPieceIds
            .Where((pieces, layer) => (state.VisibleMask & (1 << layer)) != 0).SelectMany(pieces => pieces).OrderBy(id => id, StringComparer.Ordinal).ToArray();

        private bool CheckProjection(SuccubusPerformer owner, PerformerWardrobe wardrobe, out string detail)
        {
            var state = wardrobe.CurrentWardrobe;
            var descriptors = owner.GetComponentsInChildren<WardrobeAttachmentDescriptor>(true);
            var parts = descriptors.SelectMany(descriptor => descriptor.Parts)
                .Where(part => part.renderer != null && part.renderer.enabled && part.renderer.gameObject.activeInHierarchy).ToArray();
            var garments = parts.Where(part => !string.Equals(part.role, "hair", StringComparison.OrdinalIgnoreCase)).Select(part => part.stablePieceId).OrderBy(id => id, StringComparer.Ordinal).ToArray();
            string[] expected = VisibleGarments(state);
            if (state.OutfitId == "first-outfit") expected = new[] {
                (state.VisibleMask & 2) != 0 ? "CharlenesCloset_Panty_1981" : null,
                (state.VisibleMask & 4) != 0 ? "PeekaBoo_16572" : null }.Where(id => id != null).OrderBy(id => id, StringComparer.Ordinal).ToArray();
            var hair = parts.Where(part => string.Equals(part.role, "hair", StringComparison.OrdinalIgnoreCase)).ToArray();
            bool hairMatches = state.EffectiveHairId == "none" ? hair.Length == 0 : hair.Length == 1 && hair[0].stableKey == state.EffectiveHairId;
            bool bonesOwned = parts.All(part => part.renderer.bones.Length == part.boneNames.Length &&
                part.renderer.bones.All(bone => bone != null && bone.IsChildOf(owner.transform)));
            var fit = wardrobe.CurrentFit;
            bool fitMatches = fit != null && fit.Status == WardrobeFitStatus.Validated && fit.VisibleMask == state.VisibleMask && wardrobe.Body.sharedMesh == fit.BodyMesh;
            detail = "rendered=" + string.Join(",", garments) + "; hairRenderers=" + hair.Length + "; fitMask=" + (fit != null ? fit.VisibleMask.ToString() : "missing") + "; bonesOwned=" + bonesOwned;
            report.observations.Add(new Observation {
                operation = owner.name, outfit = state.OutfitId, hair = state.EffectiveHairId, mask = state.VisibleMask,
                visibleGarments = string.Join(",", garments), fit = fit != null ? fit.name : "missing",
                bodyMesh = wardrobe.Body.sharedMesh != null ? wardrobe.Body.sharedMesh.name : "missing",
                attachments = string.Join(";", parts.Select(part => part.stableKey + "/" + part.stablePieceId + "=" + part.renderer.sharedMesh?.name)),
                boneOwners = string.Join(";", parts.Select(part => part.stablePieceId + "=" + string.Join(",", part.renderer.bones.Select(bone => bone != null ? bone.GetEntityId().ToString() : "missing")))),
                attachmentInstances = descriptors.Length });
            return garments.SequenceEqual(expected) && hairMatches && bonesOwned && fitMatches && wardrobe.Body.enabled && !wardrobe.Body.forceRenderingOff;
        }
        private static HashSet<Transform> VisibleBones(LaraWardrobe binder)
        {
            var result = new HashSet<Transform>();
            if (binder == null) return result;
            foreach (var piece in binder.Pieces.Where(x => x != null && x.visible && x.renderer != null && x.renderer.enabled))
                foreach (var bone in piece.renderer.bones) if (bone != null) result.Add(bone);
            return result;
        }
        private void Record(string name, bool passed, string detail)
        {
            if (passed && Wardrobe != null && Wardrobe.IsRuntimeInitialized)
            { passed = CheckProjection(actor, Wardrobe, out string projection); detail += "; " + projection; }
            report.assertions.Add(new Assertion { name = name, passed = passed, detail = detail });
            report.result = report.assertions.All(x => x.passed) ? "in_progress" : "failed";
            status = (passed ? "PASS " : "FAIL ") + name + " — " + detail;
        }
        private void WriteReport()
        {
            if (report.startedUtc == null) report.startedUtc = DateTime.UtcNow.ToString("O");
            report.completedUtc = DateTime.UtcNow.ToString("O"); report.generation = catalog != null ? catalog.ReleasedGenerationId : null;
            report.catalogPath = catalogAssetPath; report.manifestPath = manifestAssetPath; report.manifestGuid = manifestGuid;
            report.rigAssets = rigAssetList; report.previousGeneration = previousGeneration; report.actor = actor != null ? actor.name : null;
            report.hairCoverage = manifest != null && manifest.Hair.Length > 1 ? "Multiple released hair assets available." :
                "One released hair asset. Native independence covers Emiko/none, clear/set, clothing retention and two owners. Switching between two distinct hair assets is NOT_RUN: no second released hairstyle exists.";
            if (Wardrobe != null) { var state = Wardrobe.CurrentWardrobe; report.outfit = state.OutfitId; report.hair = state.EffectiveHairId; report.visibleGarments = string.Join(",", VisibleGarments(state)); }
            var body = Wardrobe != null ? Wardrobe.Body : null;
            report.bodyMesh = body != null && body.sharedMesh != null ? body.sharedMesh.name : null;
            report.footwear = Wardrobe != null ? Wardrobe.CurrentWardrobe.EffectiveFootwearId : null;
            var binder = actor != null ? actor.GetComponent<LaraWardrobe>() : null;
            var visiblePieces = binder != null ? binder.Pieces.Where(x => x != null && x.visible && x.renderer != null && x.renderer.enabled).ToArray() : Array.Empty<LaraWardrobe.Piece>();
            report.visibleAttachmentCount = visiblePieces.Length;
            report.visibleAttachmentMeshes = string.Join(",", visiblePieces.Select(x => x.stableKey + "=" + (x.renderer.sharedMesh != null ? x.renderer.sharedMesh.name : "missing")));
            string[] required = { "setHair:" + emikoHairKey, "dress:" + outfitId, "remove:6->2", "remove:2->0", "remove:0->0",
                "add:0->2", "add:2->6", "add:6->6", "setHair:none", "hairPreserved:none", "hairPreserved:" + emikoHairKey, "independentOwners", "secondActorDisposed" };
            var missing = required.Where(name => !report.assertions.Any(assertion => assertion.name == name && assertion.passed)).ToArray();
            report.error = missing.Length == 0 ? null : "Missing successful cases: " + string.Join(", ", missing);
            report.result = report.assertions.All(x => x.passed) && missing.Length == 0 ? "passed" : "failed_or_incomplete";
            var repoRoot = Directory.GetParent(Application.dataPath).Parent.Parent;
            string path = Path.Combine(repoRoot.FullName, "docs/wardrobe-directing/evidence/gate-a-native.json");
            Directory.CreateDirectory(Path.GetDirectoryName(path)); File.WriteAllText(path, JsonUtility.ToJson(report, true)); status = report.result + ": " + path + (report.error != null ? "\n" + report.error : string.Empty);
        }
    }
}
