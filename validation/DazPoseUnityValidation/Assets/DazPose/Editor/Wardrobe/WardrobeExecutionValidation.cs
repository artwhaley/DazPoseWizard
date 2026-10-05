using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace DazPose.UnityValidation
{
    public static class WardrobeExecutionValidation
    {
        [Serializable] private sealed class Assertion
        {
            public string name, expected, actual;
            public bool passed;
        }
        [Serializable] private sealed class Report
        {
            public int schemaVersion = 1;
            public string stage, startedUtc, completedUtc, sourceHash;
            public string[] inputAssetHashes;
            public bool passed;
            public bool complete = true;
            public Assertion[] assertions;
            public string[] artifacts, limitations;
        }
        [Serializable] private sealed class EvidenceAssertion { public string name; public bool passed; }
        [Serializable] private sealed class StageEvidence
        {
            public string stage, sourceHash;
            public string[] inputAssetHashes;
            public EvidenceAssertion[] assertions;
        }
        [Serializable] private sealed class ReimportEvidence
        {
            public bool assetGuidsPreserved, artistOpacityPreserved, artistFootwearPreserved;
        }
        [Serializable] private sealed class PlayerRuntimeAssertion
        {
            public string name, expected, actual;
            public bool passed;
        }
        [Serializable] private sealed class PlayerRuntimeReport
        {
            public int schemaVersion;
            public bool passed;
            public PlayerRuntimeAssertion[] assertions;
        }

        public static void Runner()
        {
            var checks = new List<Assertion>();
            string root = Path.GetFullPath(".").TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            checks.Add(Check("isolatedProject", "p0c-native-generation", Path.GetFileName(root),
                Path.GetFileName(root).Equals("p0c-native-generation", StringComparison.OrdinalIgnoreCase)));
            checks.Add(Check("unityProjectLoaded", "ProjectSettings/ProjectVersion.txt", "present",
                File.Exists(Path.Combine(root, "ProjectSettings", "ProjectVersion.txt"))));
            checks.Add(Check("unityVersion", "nonempty", Application.unityVersion, !string.IsNullOrWhiteSpace(Application.unityVersion)));
            WriteReport("Runner", checks, Array.Empty<string>());
        }

        public static void Contract()
        {
            var checks = new List<Assertion>();
            for (int mask = 1; mask <= 7; ++mask)
            {
                int ceiling = 2;
                int visited = 0;
                while (ceiling >= -1)
                {
                    int visible = DazPose.Performer.WardrobeLayerState.VisibleMask(mask, ceiling);
                    if ((visible & ~mask) != 0) throw new InvalidOperationException("Visible mask contains an empty layer.");
                    int next = DazPose.Performer.WardrobeLayerState.RemoveHighestCeiling(mask, ceiling);
                    if (next == ceiling) { if (visible != 0) throw new InvalidOperationException("Remove did not change visible state."); break; }
                    if (next < -1 || next > 2) throw new InvalidOperationException("Remove produced an invalid ceiling.");
                    ceiling = next;
                    if (++visited > 3) throw new InvalidOperationException("Remove failed to converge.");
                }
                while (!DazPose.Performer.WardrobeLayerState.IsFullyDressed(mask, ceiling))
                {
                    int next = DazPose.Performer.WardrobeLayerState.AddLowestCeiling(mask, ceiling);
                    if (next == ceiling) throw new InvalidOperationException("Add failed before the outfit was fully visible.");
                    ceiling = next;
                }
                int finalVisible = DazPose.Performer.WardrobeLayerState.VisibleMask(mask, ceiling);
                checks.Add(Check("maskRoundTrip" + mask, mask.ToString(), finalVisible.ToString(),
                    finalVisible == mask && DazPose.Performer.WardrobeLayerState.IsFullyDressed(mask, ceiling)));
            }
            checks.Add(Check("emptyMask", "no operation", "no operation",
                DazPose.Performer.WardrobeLayerState.RemoveHighestCeiling(0, -1) == -1 &&
                DazPose.Performer.WardrobeLayerState.AddLowestCeiling(0, -1) == -1));
            bool badCeiling = false, badMask = false;
            try { DazPose.Performer.WardrobeLayerState.VisibleMask(1, 3); } catch (ArgumentOutOfRangeException) { badCeiling = true; }
            try { DazPose.Performer.WardrobeLayerState.VisibleMask(8, 2); } catch (ArgumentOutOfRangeException) { badMask = true; }
            checks.Add(Check("invalidCeilingRejected", "throw", badCeiling.ToString(), badCeiling));
            checks.Add(Check("invalidMaskRejected", "throw", badMask.ToString(), badMask));
            checks.Add(Check("stableIdValidation", "lowercase ASCII", "validated",
                DazPose.Performer.WardrobeCatalog.IsValidId("third-outfit") &&
                !DazPose.Performer.WardrobeCatalog.IsValidId("Third Outfit")));
            var source = new[] { new[] { "maid-dress" }, new[] { "apron" }, Array.Empty<string>() };
            var state = new DazPose.Performer.WardrobeState("third-outfit", "Maid", 1, false, false,
                1, 3, 3, "barefoot", "hair-a", source, source);
            source[0][0] = "mutated-source";
            var returned = state.LayerPieceIds;
            returned[0][0] = "mutated-read";
            checks.Add(Check("snapshotDefensiveCopy", "maid-dress", state.LayerPieceIds[0][0],
                state.LayerPieceIds[0][0] == "maid-dress"));
            var config = ScriptableObject.CreateInstance<DazPose.Performer.WardrobeConfiguration>();
            config.ConfigureGenerated(
                new[] { new DazPose.Performer.WardrobeConfiguration.PieceAssignment { presetId = "sample", sourcePieceId = "piece", layer = 1 } },
                new[] { new DazPose.Performer.WardrobeConfiguration.MaterialOverride { presetId = "sample", sourcePieceId = "piece", materialSlotId = "0", opacityOverride = true, opacity = .5f } },
                new[] { new DazPose.Performer.WardrobeConfiguration.FootwearOverride { presetId = "sample", standingHeightOverride = true, standingHeight = .1f } });
            var assignmentView = config.Assignments; assignmentView[0].layer = 2;
            var materialView = config.MaterialOverrides; materialView[0].opacity = 0;
            var footwearView = config.FootwearOverrides; footwearView[0].standingHeight = .3f;
            bool deepCopies = config.Assignments[0].layer == 1 && Math.Abs(config.MaterialOverrides[0].opacity - .5f) < .001f &&
                Math.Abs(config.FootwearOverrides[0].standingHeight - .1f) < .001f;
            checks.Add(Check("configurationRowsAreDeepCopied", "mutating returned rows leaves the asset unchanged",
                "layer=" + config.Assignments[0].layer + ";opacity=" + config.MaterialOverrides[0].opacity +
                ";lift=" + config.FootwearOverrides[0].standingHeight, deepCopies));
            UnityEngine.Object.DestroyImmediate(config);
            WriteReport("Contract", checks, Array.Empty<string>());
        }

        public static void Migration()
        {
            var summary = WardrobeCatalogBuilder.BuildKnownPackages();
            var catalog = AssetDatabase.LoadAssetAtPath<DazPose.Performer.WardrobeCatalog>(summary.catalogPath);
            var checks = new List<Assertion>();
            checks.Add(Check("threeKnownPresets", "3", summary.presets.ToString(), summary.presets == 3));
            checks.Add(Check("canonicalSignature", "nonempty", summary.characterSignature,
                !string.IsNullOrWhiteSpace(summary.characterSignature)));
            checks.Add(Check("knownHairPolicy", "hair represented as Set/Keep", summary.hairPresets.ToString(),
                summary.hairPresets >= 0 && summary.hairPresets <= 1));
            checks.Add(Check("candidateCatalogValid", "valid", "valid",
                catalog != null && !catalog.IsReleased && catalog.Validate(out _)));
            checks.Add(Check("catalogDoesNotResolveBeforeRelease", "NotReleased", "NotReleased",
                !catalog.TryResolve("maid", out _, out string error) && error == "NotReleased"));
            bool stable = true;
            foreach (string id in new[] { "first-outfit", "second-outfit", "third-outfit" })
            {
                var preset = AssetDatabase.LoadAssetAtPath<DazPose.Performer.WardrobePreset>("Assets/Wardrobe/Presets/" + id + ".asset");
                if (preset == null || !preset.Validate(out _) || preset.FitStates.Length != 2) { stable = false; continue; }
                stable &= preset.FitStates.Any(f => f.VisibleMask == 0 && f.BodyMesh == AssetDatabase.LoadAssetAtPath<Mesh>("Assets/TestData/LaraCandidate/LaraBody.asset") && f.SurfaceBindings.SourceMesh == f.BodyMesh);
                stable &= preset.FitStates.Any(f => f.VisibleMask == 1 && f.Status == DazPose.Performer.WardrobeFitStatus.Validated && f.SurfaceBindings.SourceMesh == f.BodyMesh);
            }
            var firstGuid = AssetDatabase.AssetPathToGUID(summary.catalogPath);
            var second = WardrobeCatalogBuilder.BuildKnownPackages();
            stable &= second.idempotent && AssetDatabase.AssetPathToGUID(second.catalogPath) == firstGuid;
            summary.idempotent = second.idempotent;
            checks.Add(Check("stableSecondGeneration", "same catalog GUID", firstGuid, stable));
            checks.Add(Check("canonicalMorphCount", ">=124",
                AssetDatabase.LoadAssetAtPath<Mesh>("Assets/TestData/LaraCandidate/LaraBody.asset").blendShapeCount.ToString(),
                AssetDatabase.LoadAssetAtPath<Mesh>("Assets/TestData/LaraCandidate/LaraBody.asset").blendShapeCount >= 124));
            File.WriteAllText("TestOutput/wardrobe-execution/migration/summary.json", JsonUtility.ToJson(summary, true));
            WriteReport("Migration", checks, Array.Empty<string>());
        }
        public static void Runtime() => WardrobeRuntimeFixtureBuilder.StartRuntimeGate();
        public static void Layers()
        {
            var checks = new List<Assertion>();
            var summary = new System.Text.StringBuilder();
            try
            {
                const string candidateScene = "Assets/TestData/LaraCandidate/FirstPerformanceVoidLaraCandidate.unity";
                EditorSceneManager.OpenScene(candidateScene, OpenSceneMode.Single);
                var canonicalMesh = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/TestData/LaraCandidate/LaraBody.asset");
                var canonicalRenderer = UnityEngine.Object.FindObjectsByType<SkinnedMeshRenderer>(FindObjectsInactive.Include)
                    .SingleOrDefault(r => r.sharedMesh == canonicalMesh);
                var first = AssetDatabase.LoadAssetAtPath<DazPose.Performer.WardrobePreset>("Assets/Wardrobe/Presets/first-outfit.asset");
                var third = AssetDatabase.LoadAssetAtPath<DazPose.Performer.WardrobePreset>("Assets/Wardrobe/Presets/third-outfit.asset");
                if (canonicalRenderer == null || first == null || third == null)
                    throw new InvalidDataException("Candidate scene, canonical body, and migrated presets are required.");

                var firstConfig = TestConfiguration(first, part =>
                    string.Equals(part.role, "dress", StringComparison.OrdinalIgnoreCase) ? 1 : 0);
                var firstSourceUv = Uv3(first.Package.reviewBody);
                var firstResult = WardrobeFitCompiler.Compile(first, firstConfig, canonicalRenderer,
                    "Assets/TestData/WardrobeRuntime/LayerGateGenerated", false);
                var firstMaskSet = new HashSet<int>(firstResult.reachableMasks);
                var underwearOnly = firstResult.fitStates.SingleOrDefault(s => s.VisibleMask == 1);
                var fullyLayered = firstResult.fitStates.SingleOrDefault(s => s.VisibleMask == 3);
                var underwearUv = underwearOnly != null ? Uv3(underwearOnly.BodyMesh) : new List<Vector4>();
                var fullUv = fullyLayered != null ? Uv3(fullyLayered.BodyMesh) : new List<Vector4>();
                bool xCoverage = underwearUv.Count == first.Package.reviewBody.vertexCount &&
                    underwearUv.Any(v => v.x > .5f) && underwearUv.All(v => Mathf.Abs(v.y) < 1e-6f);
                bool fullCoverage = fullUv.Count == first.Package.reviewBody.vertexCount &&
                    fullUv.Any(v => v.x > .5f) && fullUv.Any(v => v.y > .5f);
                checks.Add(Check("coverageCompilesReachableBaseAndOuterLayerMasks", "masks 0,1,3", string.Join(",", firstResult.reachableMasks.OrderBy(x => x)),
                    firstMaskSet.SetEquals(new[] { 0, 1, 3 }) && xCoverage && fullCoverage));
                checks.Add(Check("coverageCompilePreservesSourceMeshAndOtherUVData", "source unchanged; naked uses canonical baseline",
                    "source=" + (firstSourceUv.SequenceEqual(Uv3(first.Package.reviewBody))) + ";naked=" +
                    (firstResult.fitStates.Single(s => s.VisibleMask == 0).BodyMesh == canonicalMesh),
                    firstSourceUv.SequenceEqual(Uv3(first.Package.reviewBody)) && firstResult.fitStates.Single(s => s.VisibleMask == 0).BodyMesh == canonicalMesh));
                checks.Add(Check("compiledBindingsMatchFitMeshes", "32,768 and exact mesh identity",
                    string.Join(";", firstResult.fitStates.Where(s => s.Status == DazPose.Performer.WardrobeFitStatus.Validated)
                        .Select(s => s.VisibleMask + ":" + (s.SurfaceBindings.SourceMesh == s.BodyMesh) + ":" + s.SurfaceBindings.BindingCount)),
                    firstResult.fitStates.Where(s => s.Status == DazPose.Performer.WardrobeFitStatus.Validated)
                        .All(s => s.SurfaceBindings != null && s.SurfaceBindings.SourceMesh == s.BodyMesh &&
                            s.SurfaceBindings.BindingCount == DazPose.Performer.PerformerSurfaceBindingAsset.RequiredBindingCount)));

                var maidSplit = TestConfiguration(third, part =>
                    string.Equals(part.role, "hosiery", StringComparison.OrdinalIgnoreCase) ? 0 :
                    string.Equals(part.role, "footwear", StringComparison.OrdinalIgnoreCase) ? 2 : 1);
                var maid = WardrobeFitCompiler.Compile(third, maidSplit, canonicalRenderer,
                    "Assets/TestData/WardrobeRuntime/LayerGateGenerated", false);
                var stockingsWithoutShoes = maid.fitStates.SingleOrDefault(s => s.VisibleMask == 3);
                var stockingsOnly = maid.fitStates.SingleOrDefault(s => s.VisibleMask == 1);
                bool retainedPosture = stockingsWithoutShoes != null && stockingsOnly != null &&
                    stockingsWithoutShoes.Status == DazPose.Performer.WardrobeFitStatus.Validated &&
                    stockingsOnly.Status == DazPose.Performer.WardrobeFitStatus.Validated &&
                    stockingsWithoutShoes.Footwear.BentFootPoseActive && !stockingsWithoutShoes.Footwear.FootwearActive &&
                    stockingsOnly.Footwear.BentFootPoseActive && !stockingsOnly.Footwear.FootwearActive;
                checks.Add(Check("retainedHosieryKeepsBentFeetWithoutShoes", "validated foot pose with shoe support off",
                    "mask3=" + (stockingsWithoutShoes != null ? stockingsWithoutShoes.Status + "/pose=" + stockingsWithoutShoes.Footwear.BentFootPoseActive + "/shoes=" + stockingsWithoutShoes.Footwear.FootwearActive : "missing") +
                    ";mask1=" + (stockingsOnly != null ? stockingsOnly.Status + "/pose=" + stockingsOnly.Footwear.BentFootPoseActive + "/shoes=" + stockingsOnly.Footwear.FootwearActive : "missing"), retainedPosture));

                var shoeBase = TestConfiguration(third, part => 0);
                var shoeBaseResult = WardrobeFitCompiler.Compile(third, shoeBase, canonicalRenderer,
                    "Assets/TestData/WardrobeRuntime/LayerGateGenerated", false);
                var shoeVisible = shoeBaseResult.fitStates.SingleOrDefault(s => s.VisibleMask == 1);
                checks.Add(Check("shoeBasePersistsUntilNude", "footwear active at mask1 and inactive at naked mask0",
                    (shoeVisible != null && shoeVisible.Footwear.FootwearActive) + "/" +
                    shoeBaseResult.fitStates.Single(s => s.VisibleMask == 0).Footwear.FootwearActive,
                    shoeVisible != null && shoeVisible.Footwear.FootwearActive &&
                    !shoeBaseResult.fitStates.Single(s => s.VisibleMask == 0).Footwear.FootwearActive));
                checks.Add(Check("thirdOutfitAllReachableStatesClassified", "all 4 reachable states; pose persists until nude",
                    string.Join(";", maid.fitStates.OrderBy(s => s.VisibleMask).Select(s => s.VisibleMask + ":" + s.Status)),
                    maid.reachableMasks.Length == 4 && maid.needsFitStates == 0 && maid.validatedStates == 4));

                var noProfile = ScriptableObject.CreateInstance<DazPose.Performer.WardrobePreset>();
                noProfile.ConfigureGenerated(third.PresetId, "Maid without shoe profile", third.CharacterSignature,
                    third.ConfigurationRevision, third.Package, third.Parts, Array.Empty<string>(),
                    DazPose.Performer.WardrobeHairAction.Keep, string.Empty, null);
                var noProfileConfig = TestConfiguration(noProfile, part =>
                    string.Equals(part.role, "hosiery", StringComparison.OrdinalIgnoreCase) ? 0 :
                    string.Equals(part.role, "footwear", StringComparison.OrdinalIgnoreCase) ? 2 : 1);
                var profileless = WardrobeFitCompiler.Compile(noProfile, noProfileConfig, canonicalRenderer,
                    "Assets/TestData/WardrobeRuntime/LayerGateNoProfileGenerated", false);
                var profilelessHosiery = profileless.fitStates.SingleOrDefault(s => s.VisibleMask == 3);
                checks.Add(Check("hosieryWithoutShoeProfileDoesNotBlockFit", "all reachable states validate; pose remains active without shoe support",
                    "needsFit=" + profileless.needsFitStates + ";mask3=" + (profilelessHosiery != null ?
                        profilelessHosiery.Status + "/pose=" + profilelessHosiery.Footwear.BentFootPoseActive +
                        "/support=" + profilelessHosiery.Footwear.FootwearActive + "/id=" + profilelessHosiery.Footwear.SupportProfileId : "missing"),
                    profileless.needsFitStates == 0 && profileless.validatedStates == profileless.reachableMasks.Length &&
                    profilelessHosiery != null && profilelessHosiery.Status == DazPose.Performer.WardrobeFitStatus.Validated &&
                    profilelessHosiery.Footwear.BentFootPoseActive && !profilelessHosiery.Footwear.FootwearActive));
                UnityEngine.Object.DestroyImmediate(noProfile);
                UnityEngine.Object.DestroyImmediate(noProfileConfig);
                summary.AppendLine("first=" + JsonUtility.ToJson(firstResult));
                summary.AppendLine("maidLayerFixture=" + JsonUtility.ToJson(maid));
                summary.AppendLine("maidShoeBaseFixture=" + JsonUtility.ToJson(shoeBaseResult));
                File.WriteAllText("TestOutput/wardrobe-execution/layers/fit-compiler-summary.txt", summary.ToString());

                string runtimeReportPath = "TestOutput/wardrobe-execution/runtime/report.json";
                Report runtimeReport = File.Exists(runtimeReportPath)
                    ? JsonUtility.FromJson<Report>(File.ReadAllText(runtimeReportPath)) : null;
                string currentSourceHash = Argument("-wardrobeExecutionSourceHash");
                string currentInputHash = Argument("-wardrobeExecutionInputHash");
                bool runtimeIdentityMatches = runtimeReport != null && runtimeReport.stage == "Runtime" &&
                    runtimeReport.passed && runtimeReport.complete && runtimeReport.sourceHash == currentSourceHash &&
                    runtimeReport.inputAssetHashes != null && runtimeReport.inputAssetHashes.Contains(currentInputHash);
                checks.Add(Check("runtimeWalkEvidenceMatchesCurrentSources", "fresh passing Runtime report with matching source/input hashes",
                    runtimeReport == null ? "missing runtime report" : runtimeReport.stage + ";passed=" + runtimeReport.passed +
                    ";complete=" + runtimeReport.complete + ";sourceMatch=" + (runtimeReport.sourceHash == currentSourceHash) +
                    ";inputMatch=" + (runtimeReport.inputAssetHashes != null && runtimeReport.inputAssetHashes.Contains(currentInputHash)),
                    runtimeIdentityMatches));
                foreach (string metricName in new[] { "liveWalkingFrameSamples", "walkFootwearSupportAndFloorContact",
                    "walkRigidShoeInternalDistance", "walkLiveRendererBoundsAndCulling" })
                {
                    var metric = runtimeReport != null && runtimeReport.assertions != null
                        ? runtimeReport.assertions.FirstOrDefault(assertion => assertion.name == metricName) : null;
                    checks.Add(Check("runtimeEvidence_" + metricName, "matching Runtime assertion passed",
                        metric != null ? metric.actual : "missing assertion",
                        runtimeIdentityMatches && metric != null && metric.passed));
                }
            }
            catch (Exception exception)
            {
                checks.Add(Check("layerCompilerException", "no exception", exception.ToString(), false));
            }
            WriteReport("Layers", checks, Array.Empty<string>());
        }
        public static void Setup()
        {
            var checks = new List<Assertion>();
            var limitations = new List<string>();
            string candidatePath = Path.Combine(Directory.GetParent(Application.dataPath).FullName,
                ClothingSetupBuilder.CandidateScenePath);
            string candidateHash = File.Exists(candidatePath) ? HashFile(candidatePath) : "missing";
            try
            {
                if (!ClothingSetupBuilder.TryPrepareAndOpen(out var scene, out string error))
                    throw new InvalidOperationException(error);
                var wardrobe = UnityEngine.Object.FindAnyObjectByType<DazPose.Performer.PerformerWardrobe>();
                var performer = UnityEngine.Object.FindAnyObjectByType<DazPose.Performer.SuccubusPerformer>();
                var catalog = AssetDatabase.LoadAssetAtPath<DazPose.Performer.WardrobeCatalog>(ClothingSetupBuilder.CatalogPath);
                var config = AssetDatabase.LoadAssetAtPath<DazPose.Performer.WardrobeConfiguration>(ClothingSetupBuilder.ConfigurationPath);
                checks.Add(Check("singlePersistentSetupScene", ClothingSetupBuilder.ScenePath, scene.path,
                    scene.path == ClothingSetupBuilder.ScenePath && AssetDatabase.LoadAssetAtPath<SceneAsset>(ClothingSetupBuilder.ScenePath) != null));
                checks.Add(Check("setupUsesCandidatePreviewRuntime", "same runtime controller, candidate catalog and config bound",
                    (wardrobe != null ? wardrobe.GetType().FullName : "missing") + "/preview=" +
                    (wardrobe != null && wardrobe.AllowsCandidatePreview) + "/binding=" +
                    (wardrobe != null && wardrobe.Catalog == catalog && wardrobe.Configuration == config),
                    wardrobe != null && performer != null && wardrobe.AllowsCandidatePreview &&
                    wardrobe.Catalog == catalog && wardrobe.Configuration == config && wardrobe.Body != null));
                checks.Add(Check("runtimeFacadeUsesProductionCommands", "OutfitAsync, TryRemoveLayerAsync, TryAddLayerAsync, SetHairAsync",
                    string.Join(",", new[] { "OutfitAsync", "TryRemoveLayerAsync", "TryAddLayerAsync", "SetHairAsync" }.Where(name =>
                        typeof(DazPose.Performer.SuccubusPerformer).GetMethods().Any(method => method.Name == name))),
                    typeof(DazPose.Performer.SuccubusPerformer).GetMethod("OutfitAsync", new[] { typeof(string), typeof(DazPose.Performer.WardrobeTransition) }) != null &&
                    typeof(DazPose.Performer.SuccubusPerformer).GetMethod("TryRemoveLayerAsync") != null &&
                    typeof(DazPose.Performer.SuccubusPerformer).GetMethod("TryAddLayerAsync") != null &&
                    typeof(DazPose.Performer.SuccubusPerformer).GetMethod("SetHairAsync") != null));

                bool exclusive = false, invalidated = false, reverted = false;
                var preset = catalog.Presets.FirstOrDefault(p => p.Parts.Length > 0);
                if (preset != null)
                {
                    var part = preset.Parts[0];
                    int revision = config.Revision;
                    var session = new WardrobeEditSession(config, catalog);
                    int original = session.Assignments.Single(a => a.presetId == preset.PresetId && a.sourcePieceId == part.sourcePieceId).layer;
                    bool setOne = session.TrySetLayer(preset.PresetId, part.sourcePieceId, 1, out _);
                    bool layerOne = session.Assignments.Single(a => a.presetId == preset.PresetId && a.sourcePieceId == part.sourcePieceId).layer == 1;
                    bool setTwo = session.TrySetLayer(preset.PresetId, part.sourcePieceId, 2, out _);
                    exclusive = setOne && layerOne && setTwo && session.Assignments.Single(a => a.presetId == preset.PresetId && a.sourcePieceId == part.sourcePieceId).layer == 2;
                    invalidated = session.IsDirty;
                    session.Reload(catalog);
                    int revertedLayer = session.Assignments.Single(a => a.presetId == preset.PresetId && a.sourcePieceId == part.sourcePieceId).layer;
                    reverted = !session.IsDirty && revertedLayer == original && config.Revision == revision;
                }
                checks.Add(Check("exclusiveLayerAssignmentAndCandidateInvalidation", "one layer value 0..2; edit marks candidate dirty",
                    "exclusive=" + exclusive + ";dirty=" + invalidated, exclusive && invalidated));
                checks.Add(Check("revertDoesNotAdvanceRevision", "original assignment/revision restored", reverted.ToString(), reverted));
                checks.Add(Check("inspectionControlsBound", "anatomy component and canonical mesh exposed",
                    (performer != null && performer.GetComponent<DazPose.Performer.LaraAnatomyControls>() != null) + "/" +
                    (wardrobe != null && wardrobe.Body != null && wardrobe.Body.sharedMesh != null),
                    performer != null && performer.GetComponent<DazPose.Performer.LaraAnatomyControls>() != null &&
                    wardrobe != null && wardrobe.Body != null && wardrobe.Body.sharedMesh != null));
                checks.Add(Check("setupWindowAvailable", "Tools/DAZ Pose/Wardrobe Setup", typeof(ClothingSetupWindow).FullName,
                    typeof(ClothingSetupWindow) != null));
                string runtimeReportPath = Path.Combine(Directory.GetParent(Application.dataPath).FullName,
                    "TestOutput/wardrobe-execution/runtime/report.json");
                bool runtimeEvidence = false;
                string runtimeDetails = "missing";
                if (File.Exists(runtimeReportPath))
                {
                    var runtimeReport = JsonUtility.FromJson<StageEvidence>(File.ReadAllText(runtimeReportPath));
                    string sourceHash = Argument("-wardrobeExecutionSourceHash");
                    string inputHash = Argument("-wardrobeExecutionInputHash");
                    var passed = new HashSet<string>((runtimeReport.assertions ?? Array.Empty<EvidenceAssertion>())
                        .Where(a => a.passed).Select(a => a.name), StringComparer.Ordinal);
                    runtimeEvidence = runtimeReport.stage == "Runtime" && runtimeReport.sourceHash == sourceHash &&
                        runtimeReport.inputAssetHashes != null && runtimeReport.inputAssetHashes.Contains(inputHash) &&
                        passed.Contains("firstHairSelected") && passed.Contains("keepHairAcrossOutfit") &&
                        passed.Contains("relativeCommandsFifo") && passed.Contains("approvedAnatomyControlsPreserved");
                    runtimeDetails = runtimeReport.stage + "/hash=" + (runtimeReport.sourceHash == sourceHash) +
                        "/hair=" + passed.Contains("firstHairSelected") + "/keep=" + passed.Contains("keepHairAcrossOutfit") +
                        "/fifo=" + passed.Contains("relativeCommandsFifo") + "/anatomy=" + passed.Contains("approvedAnatomyControlsPreserved");
                }
                checks.Add(Check("liveSetupUsesRuntimePathAndPersistsHair", "fresh Runtime gate evidence for production calls/hair/anatomy",
                    runtimeDetails, runtimeEvidence));
                string candidateAfter = File.Exists(candidatePath) ? HashFile(candidatePath) : "missing";
                checks.Add(Check("candidateScenePreserved", candidateHash, candidateAfter, candidateHash == candidateAfter));
                if (!runtimeEvidence) limitations.Add("Setup requires a fresh Runtime gate report with matching source/input hashes before it can prove live production preview, hair persistence, queued commands and anatomy preservation.");
            }
            catch (Exception exception)
            { checks.Add(Check("setupException", "no exception", exception.ToString(), false)); }
            WriteReport("Setup", checks, limitations.ToArray());
        }
        public static void Persistence()
        {
            var checks = new List<Assertion>();
            var limitations = new List<string>();
            string suffix = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff");
            string tempRoot = "Assets/TestData/WardrobeRuntime/Validation/Persistence-" + suffix;
            string releaseGeneration = null;
            bool generatedReleaseWasNew = false;
            try
            {
                const string candidateScene = "Assets/TestData/LaraCandidate/FirstPerformanceVoidLaraCandidate.unity";
                EditorSceneManager.OpenScene(candidateScene, OpenSceneMode.Single);
                var sourceCatalog = AssetDatabase.LoadAssetAtPath<DazPose.Performer.WardrobeCatalog>("Assets/Wardrobe/WardrobeCatalog.asset");
                var sourceConfig = AssetDatabase.LoadAssetAtPath<DazPose.Performer.WardrobeConfiguration>("Assets/Wardrobe/WardrobeConfiguration.asset");
                var canonical = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/TestData/LaraCandidate/LaraBody.asset");
                var body = UnityEngine.Object.FindObjectsByType<SkinnedMeshRenderer>(FindObjectsInactive.Include)
                    .FirstOrDefault(r => r.sharedMesh == canonical);
                if (sourceCatalog == null || sourceConfig == null || body == null)
                    throw new InvalidDataException("Candidate catalog/config/body are required for E6 persistence checks.");
                EnsureAssetFolder(tempRoot);

                var maid = sourceCatalog.Presets.Single(p => p.PresetId == "third-outfit");
                var configPath = tempRoot + "/Configuration.asset";
                var savedConfig = UnityEngine.Object.Instantiate(sourceConfig);
                savedConfig.name = "Persistence Fixture Configuration";
                AssetDatabase.CreateAsset(savedConfig, configPath);
                var session = new WardrobeEditSession(savedConfig, sourceCatalog);
                var firstPart = maid.Parts.First();
                var rowBefore = session.Assignments.Single(a => a.presetId == maid.PresetId && a.sourcePieceId == firstPart.sourcePieceId);
                int originalLayer = rowBefore.layer;
                int initialRevision = savedConfig.Revision;
                session.TrySetLayer(maid.PresetId, firstPart.sourcePieceId, (originalLayer + 1) % 3, out _);
                session.SetOpacity(maid.PresetId, firstPart.sourcePieceId, "0", .63f);
                if (maid.Footwear != null)
                    session.SetFootwear(maid.PresetId, true, maid.Footwear.standingHeight + .012f,
                        true, Mathf.Clamp01(maid.Footwear.footShrink + .01f));
                bool unsavedProtected = savedConfig.Revision == initialRevision &&
                    savedConfig.Assignments.Single(a => a.presetId == maid.PresetId && a.sourcePieceId == firstPart.sourcePieceId).layer == originalLayer;
                checks.Add(Check("unsavedPreviewDoesNotMutateConfig", "persisted row/revision unchanged before Save",
                    savedConfig.Revision + "/" + savedConfig.Assignments.Single(a => a.presetId == maid.PresetId && a.sourcePieceId == firstPart.sourcePieceId).layer,
                    unsavedProtected && session.IsDirty));
                if (!savedConfig.TrySetWorkingAssignments(session.Assignments, out string assignmentError))
                    throw new InvalidDataException(assignmentError);
                savedConfig.ConfigureGenerated(session.Assignments, session.MaterialOverrides, session.FootwearOverrides);
                savedConfig.CommitRevision("persistence-save-fixture", new[] { "fixture-input" });
                EditorUtility.SetDirty(savedConfig); AssetDatabase.SaveAssets();
                AssetDatabase.ImportAsset(configPath, ImportAssetOptions.ForceUpdate);
                Resources.UnloadAsset(savedConfig);
                savedConfig = AssetDatabase.LoadAssetAtPath<DazPose.Performer.WardrobeConfiguration>(configPath);
                int persistedLayer = savedConfig.Assignments.Single(a => a.presetId == maid.PresetId && a.sourcePieceId == firstPart.sourcePieceId).layer;
                bool materialPersisted = savedConfig.MaterialOverrides.Any(x => x.presetId == maid.PresetId &&
                    x.sourcePieceId == firstPart.sourcePieceId && x.opacityOverride && Mathf.Abs(x.opacity - .63f) < .001f);
                bool footwearPersisted = maid.Footwear == null || savedConfig.FootwearOverrides.Any(x => x.presetId == maid.PresetId &&
                    x.standingHeightOverride && x.footShrinkOverride);
                checks.Add(Check("savedConfigReloadsLayerOpacityAndFootwear", "layer/material/footwear tuning survive import reload",
                    persistedLayer + "/" + materialPersisted + "/" + footwearPersisted + ";revision=" + savedConfig.Revision,
                    persistedLayer == (originalLayer + 1) % 3 && materialPersisted && footwearPersisted && savedConfig.Revision == initialRevision + 1));

                var revertedSession = new WardrobeEditSession(savedConfig, sourceCatalog);
                revertedSession.TrySetLayer(maid.PresetId, firstPart.sourcePieceId, (persistedLayer + 1) % 3, out _);
                revertedSession.Reload(sourceCatalog);
                checks.Add(Check("revertRestoresLastSavedRevision", persistedLayer.ToString(),
                    revertedSession.Assignments.Single(a => a.presetId == maid.PresetId && a.sourcePieceId == firstPart.sourcePieceId).layer.ToString(),
                    !revertedSession.IsDirty && revertedSession.Assignments.Single(a => a.presetId == maid.PresetId && a.sourcePieceId == firstPart.sourcePieceId).layer == persistedLayer));

                var variantPath = tempRoot + "/Variant.asset";
                var variant = UnityEngine.Object.Instantiate(maid);
                variant.name = "persistence-variant";
                var variantParts = maid.Parts;
                variantParts[0].layer = (variantParts[0].layer + 1) % 3;
                variant.ConfigureGenerated("persistence-variant", "Persistence Variant", maid.CharacterSignature,
                    savedConfig.Revision, maid.Package, variantParts, Array.Empty<string>(), DazPose.Performer.WardrobeHairAction.Keep, string.Empty, maid.Footwear);
                variant.SetGeneratedFitStates(maid.FitStates);
                AssetDatabase.CreateAsset(variant, variantPath); AssetDatabase.SaveAssets();
                bool variantIndependent = variant.PresetId != maid.PresetId && variant.Package == maid.Package &&
                    variant.Parts[0].layer != maid.Parts[0].layer && maid.PresetId == "third-outfit";
                checks.Add(Check("variantHasIndependentIdentityAndSharesPackage", "new ID/part config; shared unchanged source package",
                    variant.PresetId + "/sharedPackage=" + (variant.Package == maid.Package) + "/source=" + maid.Parts[0].layer,
                    variantIndependent));

                var duplicateRows = sourceConfig.Assignments.Concat(new[]
                {
                    new DazPose.Performer.WardrobeConfiguration.PieceAssignment { presetId = maid.PresetId, sourcePieceId = firstPart.sourcePieceId, layer = 0 },
                    new DazPose.Performer.WardrobeConfiguration.PieceAssignment { presetId = maid.PresetId, sourcePieceId = firstPart.sourcePieceId, layer = 2 }
                }).ToArray();
                var brokenConfig = UnityEngine.Object.Instantiate(sourceConfig);
                brokenConfig.name = "Deliberately Invalid Publisher Configuration";
                brokenConfig.ConfigureGenerated(duplicateRows, sourceConfig.MaterialOverrides, sourceConfig.FootwearOverrides);
                brokenConfig.CommitRevision("invalid-publisher-fixture", new[] { "invalid" });
                string failedCatalogPath = tempRoot + "/FailedCandidateCatalog.asset";
                var failedCatalog = UnityEngine.Object.Instantiate(sourceCatalog);
                failedCatalog.name = "Failed Candidate Catalog"; failedCatalog.ConfigureCandidate(sourceCatalog.Presets);
                AssetDatabase.CreateAsset(failedCatalog, failedCatalogPath); AssetDatabase.SaveAssets();
                string originalCatalogHash = HashAssetFile(failedCatalogPath);
                int originalConfigRevision = brokenConfig.Revision;
                var failedBuild = WardrobeGenerationPublisher.PublishCandidate(failedCatalog, brokenConfig, body);
                string afterCatalogHash = HashAssetFile(failedCatalogPath);
                checks.Add(Check("failedPublicationKeepsPriorCatalogAndConfig", "failure without release reference/hash/revision change",
                    "published=" + failedBuild.published + ";catalog=" + (originalCatalogHash == afterCatalogHash) +
                    ";revision=" + brokenConfig.Revision,
                    !failedBuild.published && originalCatalogHash == afterCatalogHash &&
                    brokenConfig.Revision == originalConfigRevision && !failedCatalog.IsReleased));

                var publishConfig = UnityEngine.Object.Instantiate(sourceConfig);
                publishConfig.name = "Successful Snapshot Publisher Configuration";
                publishConfig.CommitRevision("persistence-snapshot-fixture-" + suffix, new[] { "fixture-input" });
                var publishCatalog = UnityEngine.Object.Instantiate(sourceCatalog);
                publishCatalog.name = "Isolated Snapshot Publisher Catalog";
                publishCatalog.ConfigureCandidate(sourceCatalog.Presets);
                string publishCatalogPath = tempRoot + "/SnapshotCandidateCatalog.asset";
                AssetDatabase.CreateAsset(publishConfig, tempRoot + "/SnapshotConfiguration.asset");
                AssetDatabase.CreateAsset(publishCatalog, publishCatalogPath);
                AssetDatabase.SaveAssets();
                releaseGeneration = WardrobeGenerationPublisher.ComputeGeneration(publishCatalog, publishConfig);
                string releaseAssetPath = "Assets/Wardrobe/Releases/" + releaseGeneration;
                generatedReleaseWasNew = !AssetDatabase.IsValidFolder(releaseAssetPath);
                var publishResult = WardrobeGenerationPublisher.PublishCandidate(publishCatalog, publishConfig, body);
                var releasedPreset = publishCatalog.Presets.FirstOrDefault(p => p.PresetId == maid.PresetId);
                var releasedFit = releasedPreset != null ? releasedPreset.FitStates.FirstOrDefault(f => f.VisibleMask == 0) : null;
                bool snapshotValid = publishResult.published && publishCatalog.IsReleased && releasedPreset != null &&
                    releasedPreset != maid && releasedPreset.Package != maid.Package && releasedFit != null &&
                    releasedFit.BodyMesh != canonical && releasedFit.SurfaceBindings.SourceMesh == releasedFit.BodyMesh &&
                    releasedFit.DissolveProfile.SurfaceBindings == releasedFit.SurfaceBindings;
                checks.Add(Check("successfulPublicationUsesImmutableSnapshots", "published catalog references snapshotted package/body/bindings",
                    publishResult.published + "/" + publishResult.generationId + "/meshCount=" + publishResult.snapshottedMeshCount,
                    snapshotValid));
                checks.Add(Check("releaseReferencesInputHashes", "configuration hash matches released ID",
                    publishConfig.GenerationHash + "/" + publishCatalog.ReleasedGenerationId,
                    publishResult.published && publishConfig.GenerationHash == publishCatalog.ReleasedGenerationId));
                string projectRoot = Directory.GetParent(Application.dataPath).FullName;
                string[] outfitIds = { "first-outfit", "second-outfit", "third-outfit" };
                var reimportStates = outfitIds.Select(id =>
                {
                    string evidencePath = Path.Combine(projectRoot, "TestOutput", "wardrobe-import", id, "reimport-validation.json");
                    if (!File.Exists(evidencePath)) return id + ":missing";
                    var evidence = JsonUtility.FromJson<ReimportEvidence>(File.ReadAllText(evidencePath));
                    bool passed = evidence != null && evidence.assetGuidsPreserved && evidence.artistOpacityPreserved && evidence.artistFootwearPreserved;
                    return id + ":" + passed;
                }).ToArray();
                bool allReimportsPassed = reimportStates.Length == outfitIds.Length && reimportStates.All(x => x.EndsWith(":True", StringComparison.Ordinal));
                checks.Add(Check("allImportedOutfitsPreserveOverridesOnReimport", "asset GUIDs, artist opacity and footwear tuning survive all three outfit reimports",
                    string.Join(";", reimportStates), allReimportsPassed));

                string runtimeReportPath = Path.Combine(projectRoot, "TestOutput", "wardrobe-execution", "runtime", "report.json");
                bool runtimeSourcesImmutable = false;
                string runtimeDetails = "missing";
                if (File.Exists(runtimeReportPath))
                {
                    var runtimeReport = JsonUtility.FromJson<StageEvidence>(File.ReadAllText(runtimeReportPath));
                    string sourceHash = Argument("-wardrobeExecutionSourceHash");
                    string inputHash = Argument("-wardrobeExecutionInputHash");
                    var runtimeAssertions = new HashSet<string>((runtimeReport.assertions ?? Array.Empty<EvidenceAssertion>())
                        .Where(a => a.passed).Select(a => a.name), StringComparer.Ordinal);
                    runtimeSourcesImmutable = runtimeReport.stage == "Runtime" && runtimeReport.sourceHash == sourceHash &&
                        runtimeReport.inputAssetHashes != null && runtimeReport.inputAssetHashes.Contains(inputHash) &&
                        runtimeAssertions.Contains("sourceMaterialsAndProfilesRemainImmutable");
                    runtimeDetails = runtimeReport.stage + "/hash=" + (runtimeReport.sourceHash == sourceHash) +
                        "/immutable=" + runtimeAssertions.Contains("sourceMaterialsAndProfilesRemainImmutable");
                }
                checks.Add(Check("runtimePreviewLeavesSourceMaterialsAndProfilesUnchanged", "fresh Runtime gate proves source catalog materials/profiles are unchanged",
                    runtimeDetails, runtimeSourcesImmutable));
                if (!allReimportsPassed) limitations.Add("Run the shared importer ReimportTest for first-outfit, second-outfit and third-outfit in the isolated project; the stage requires fresh GUID/material/footwear preservation evidence.");
                if (!runtimeSourcesImmutable) limitations.Add("Run the Runtime gate after the source snapshot assertion is implemented; Persistence requires matching source/input-hash evidence.");
            }
            catch (Exception exception) { checks.Add(Check("persistenceException", "no exception", exception.ToString(), false)); }
            finally
            {
                AssetDatabase.DeleteAsset(tempRoot);
                if (generatedReleaseWasNew && !string.IsNullOrEmpty(releaseGeneration))
                {
                    AssetDatabase.DeleteAsset("Assets/Wardrobe/Releases/" + releaseGeneration);
                    AssetDatabase.DeleteAsset("Assets/TestData/WardrobeRuntime/Generations/" + releaseGeneration);
                    AssetDatabase.DeleteAsset("Assets/TestData/WardrobeRuntime/ReleaseCandidates/" + releaseGeneration);
                }
                AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
            }
            WriteReport("Persistence", checks, limitations.ToArray(), complete: limitations.Count == 0);
        }

        private static void EnsureAssetFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/');
            if (slash <= 0) throw new InvalidDataException("Invalid asset folder: " + path);
            EnsureAssetFolder(path.Substring(0, slash));
            if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
        }

        private static string HashAssetFile(string assetPath)
        {
            string physical = Path.Combine(Directory.GetParent(Application.dataPath).FullName, assetPath);
            return File.Exists(physical) ? HashFile(physical) : "missing";
        }
        public static void Integration() => WardrobeRuntimeFixtureBuilder.StartIntegrationGate();

        public static void Player()
        {
            var checks = new List<Assertion>();
            string reportPath = Argument("-wardrobeExecutionReport");
            string outputFolder = Path.GetDirectoryName(Path.GetFullPath(reportPath));
            string runtimeReportPath = Path.Combine(outputFolder, "player-runtime-report.json");
            string playerLogPath = Path.Combine(outputFolder, "player-runtime.log");
            string buildFolder = Path.Combine(outputFolder, "build-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff"));
            try
            {
                Directory.CreateDirectory(outputFolder);
                if (File.Exists(runtimeReportPath)) File.Delete(runtimeReportPath);
                string fixtureScene = PreparePlayerFixtureScene();
                string playerPath = Path.Combine(buildFolder, "WardrobePlayerValidation.exe");
                var build = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { fixtureScene },
                    locationPathName = playerPath,
                    target = BuildTarget.StandaloneWindows64,
                    options = BuildOptions.Development
                });
                bool buildSucceeded = build != null && build.summary.result == BuildResult.Succeeded && File.Exists(playerPath);
                checks.Add(Check("standaloneWindowsPlayerBuild", "Windows x64 player builds with the wardrobe fixture scene",
                    "result=" + (build != null ? build.summary.result.ToString() : "missing report") +
                    ";path=" + playerPath + ";exists=" + File.Exists(playerPath), buildSucceeded));
                if (!buildSucceeded)
                {
                    WriteReport("Player", checks, new[] { "Windows x64 player build did not succeed; see the Unity build log." });
                    return;
                }

                var start = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = playerPath,
                    Arguments = "-batchmode -nographics -logFile \"" + playerLogPath +
                        "\" -wardrobePlayerReport \"" + runtimeReportPath + "\"",
                    WorkingDirectory = buildFolder,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using (var process = System.Diagnostics.Process.Start(start))
                {
                    if (process == null) throw new InvalidOperationException("Could not start the built wardrobe player.");
                    if (!process.WaitForExit(240000))
                    {
                        try { process.Kill(); } catch { }
                        checks.Add(Check("standalonePlayerProcess", "player completes within 240 seconds",
                            "timed out; log=" + playerLogPath, false));
                    }
                    else
                    {
                        checks.Add(Check("standalonePlayerProcess", "exit code 0", process.ExitCode.ToString(), process.ExitCode == 0));
                    }
                }

                if (!File.Exists(runtimeReportPath))
                    checks.Add(Check("standalonePlayerRuntimeReport", "fresh report written by the standalone player",
                        "missing; log=" + playerLogPath, false));
                else
                {
                    var playerReport = JsonUtility.FromJson<PlayerRuntimeReport>(File.ReadAllText(runtimeReportPath));
                    checks.Add(Check("standalonePlayerRuntimeReport", "player reports passing authoring calls",
                        "passed=" + (playerReport != null && playerReport.passed) + ";path=" + runtimeReportPath,
                        playerReport != null && playerReport.passed && playerReport.assertions != null && playerReport.assertions.Length > 0));
                    if (playerReport != null && playerReport.assertions != null)
                        foreach (var assertion in playerReport.assertions)
                            checks.Add(Check("player." + assertion.name, assertion.expected, assertion.actual, assertion.passed));
                }
                checks.Add(Check("playerLogArtifact", "standalone player log captured", playerLogPath, File.Exists(playerLogPath)));
            }
            catch (Exception exception)
            {
                checks.Add(Check("playerValidationException", "no exception", exception.ToString(), false));
            }
            WriteReport("Player", checks, Array.Empty<string>());
        }

        private static string PreparePlayerFixtureScene()
        {
            const string candidateScene = "Assets/TestData/LaraCandidate/FirstPerformanceVoidLaraCandidate.unity";
            const string canonicalBodyPath = "Assets/TestData/LaraCandidate/LaraBody.asset";
            const string sourceCatalogPath = "Assets/Wardrobe/WardrobeCatalog.asset";
            const string sourceConfigurationPath = "Assets/Wardrobe/WardrobeConfiguration.asset";
            const string playerFolder = "Assets/TestData/WardrobeRuntime/PlayerValidation";
            const string playerCatalogPath = playerFolder + "/ReleasedPlayerCatalog.asset";
            const string playerScenePath = playerFolder + "/WardrobePlayerValidation.unity";
            EnsureAssetFolder(playerFolder);
            var sourceCatalog = AssetDatabase.LoadAssetAtPath<DazPose.Performer.WardrobeCatalog>(sourceCatalogPath);
            var configuration = AssetDatabase.LoadAssetAtPath<DazPose.Performer.WardrobeConfiguration>(sourceConfigurationPath);
            var canonical = AssetDatabase.LoadAssetAtPath<Mesh>(canonicalBodyPath);
            if (sourceCatalog == null || configuration == null || canonical == null)
                throw new InvalidDataException("The player fixture needs the migrated catalog, configuration and canonical body assets.");

            var playerCatalog = AssetDatabase.LoadAssetAtPath<DazPose.Performer.WardrobeCatalog>(playerCatalogPath);
            if (playerCatalog == null)
            {
                playerCatalog = UnityEngine.Object.Instantiate(sourceCatalog);
                playerCatalog.name = "Released Player Validation Catalog";
                AssetDatabase.CreateAsset(playerCatalog, playerCatalogPath);
            }
            playerCatalog.ConfigureCandidate(sourceCatalog.Presets);
            playerCatalog.SetReleasedGeneration("player-validation");
            EditorUtility.SetDirty(playerCatalog);

            var scene = EditorSceneManager.OpenScene(candidateScene, OpenSceneMode.Single);
            var performer = UnityEngine.Object.FindAnyObjectByType<DazPose.Performer.SuccubusPerformer>();
            var body = UnityEngine.Object.FindObjectsByType<SkinnedMeshRenderer>(FindObjectsInactive.Include)
                .FirstOrDefault(renderer => renderer.sharedMesh == canonical);
            if (performer == null || body == null)
                throw new InvalidDataException("The player fixture could not find Lara and the canonical body in its source scene.");
            var wardrobe = performer.GetComponent<DazPose.Performer.PerformerWardrobe>() ??
                performer.gameObject.AddComponent<DazPose.Performer.PerformerWardrobe>();
            wardrobe.ConfigureBinding(playerCatalog, configuration, body, performer);
            EditorUtility.SetDirty(wardrobe);
            var binding = performer.GetComponent<DazPose.Performer.SceneWardrobeBinding>() ??
                performer.gameObject.AddComponent<DazPose.Performer.SceneWardrobeBinding>();
            binding.SetPerformerBindingId("player-lara");
            EditorUtility.SetDirty(binding);
            var fixtureObject = new GameObject("Wardrobe Standalone Player Fixture");
            var fixture = fixtureObject.AddComponent<DazPose.Performer.WardrobePlayerExecutionFixture>();
            var fixtureData = new SerializedObject(fixture);
            fixtureData.FindProperty("performer").objectReferenceValue = performer;
            fixtureData.FindProperty("wardrobe").objectReferenceValue = wardrobe;
            fixtureData.FindProperty("sceneBinding").objectReferenceValue = binding;
            fixtureData.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(fixture);
            if (!EditorSceneManager.SaveScene(scene, playerScenePath))
                throw new IOException("Could not save the isolated standalone player fixture scene.");
            AssetDatabase.SaveAssets();
            return playerScenePath;
        }

        private static void NotImplemented(string stage, string message)
        {
            WriteReport(stage, new List<Assertion> { Check("implemented", "implemented", message, false) }, new[] { message });
        }

        private static Assertion Check(string name, string expected, string actual, bool passed) =>
            new Assertion { name = name, expected = expected, actual = actual, passed = passed };

        private static DazPose.Performer.WardrobeConfiguration TestConfiguration(
            DazPose.Performer.WardrobePreset preset, Func<DazPose.Performer.WardrobeOutfitDefinition.Piece, int> resolve)
        {
            var rows = preset.Parts.Select(part =>
            {
                var piece = preset.Package.pieces.Single(p => p.id == part.sourcePieceId);
                return new DazPose.Performer.WardrobeConfiguration.PieceAssignment
                { presetId = preset.PresetId, sourcePieceId = part.sourcePieceId, layer = resolve(piece) };
            }).ToArray();
            var result = ScriptableObject.CreateInstance<DazPose.Performer.WardrobeConfiguration>();
            result.ConfigureGenerated(rows);
            return result;
        }

        private static List<Vector4> Uv3(Mesh mesh)
        {
            var values = new List<Vector4>();
            if (mesh != null) mesh.GetUVs(3, values);
            return values;
        }

        private static string HashFile(string path)
        {
            using (var sha = System.Security.Cryptography.SHA256.Create())
            using (var stream = File.OpenRead(path))
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }

        private static void WriteReport(string stage, List<Assertion> assertions, string[] limitations, bool complete = true)
        {
            string requested = Argument("-wardrobeExecutionStage");
            string output = Argument("-wardrobeExecutionReport");
            if (string.IsNullOrWhiteSpace(output)) throw new InvalidOperationException("Missing -wardrobeExecutionReport.");
            var report = new Report
            {
                stage = stage,
                startedUtc = DateTime.UtcNow.ToString("O"),
                sourceHash = Argument("-wardrobeExecutionSourceHash"),
                inputAssetHashes = new[] { Argument("-wardrobeExecutionInputHash") },
                passed = stage == requested && assertions.Count > 0 && assertions.All(a => a.passed) && complete,
                complete = complete,
                assertions = assertions.ToArray(),
                artifacts = Array.Empty<string>(),
                limitations = limitations ?? Array.Empty<string>()
            };
            report.completedUtc = DateTime.UtcNow.ToString("O");
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output)));
            File.WriteAllText(output, JsonUtility.ToJson(report, true));
            if (!report.passed) Debug.LogError("WARDROBE_EXECUTION_GATE_FAILED: " + stage + (complete ? "" : " (incomplete evidence)") + "; see " + output);
            else Debug.Log("WARDROBE_EXECUTION_GATE_PASSED: " + stage + "; assertions=" + assertions.Count);
            EditorApplication.Exit(report.passed ? 0 : 1);
        }

        private static string Argument(string key)
        {
            var args = Environment.GetCommandLineArgs();
            int at = Array.IndexOf(args, key);
            return at >= 0 && at + 1 < args.Length ? args[at + 1].Trim('"') : string.Empty;
        }
    }
}
