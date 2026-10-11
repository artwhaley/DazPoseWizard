using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Reflection;
using DazPose.Performer;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DazPose.UnityValidation
{
    /// <summary>Transactional Day 5 persistence and immutable snapshot regressions.</summary>
    public static class Day5WardrobeValidation
    {
        [Serializable] public sealed class Assertion
        {
            public string name;
            public string expected;
            public string actual;
            public bool passed;
        }

        [Serializable] private sealed class Report
        {
            public int schemaVersion = 1;
            public string stage = "Day5Transactions";
            public bool passed;
            public string startedUtc;
            public string completedUtc;
            public string sourceHash;
            public string[] inputAssetHashes;
            public Assertion[] assertions;
            public string[] artifacts;
            public string[] limitations;
        }

        public static void RunBatch()
        {
            string projectRoot = Application.dataPath.Replace('\\', '/');
            if (projectRoot.IndexOf("/.dazposewizard/", StringComparison.OrdinalIgnoreCase) < 0)
            {
                Debug.LogError("Day 5 batch validation is restricted to an isolated .dazposewizard project. Refusing to run at " + projectRoot);
                EditorApplication.Exit(2);
                return;
            }
            string started = DateTime.UtcNow.ToString("O");
            var assertions = Run();
            string[] sourcePaths =
            {
                "Assets/DazPose/Validation/Editor/Legacy/Wardrobe/Day5WardrobeValidation.cs",
                "Assets/DazPose/Editor/Wardrobe/WardrobeCandidateWorkflow.cs",
                "Assets/DazPose/Editor/Wardrobe/WardrobeGenerationPublisher.cs",
                "Assets/DazPose/Editor/Wardrobe/WardrobeCandidateReimport.cs",
                "Assets/DazPose/Editor/Wardrobe/WardrobeEditSession.cs",
                "Assets/DazPose/Editor/Wardrobe/ClothingSetupWindow.cs",
                "Assets/DazPose/Editor/Wardrobe/ClothingSetupBuilder.cs",
                "Assets/DazPose/Editor/Wardrobe/WardrobeFitCompiler.cs",
                "Assets/DazPose/Runtime/Performer/Wardrobe/WardrobeCandidateSnapshot.cs",
                "Assets/DazPose/Runtime/Performer/Wardrobe/PerformerWardrobe.cs",
                "Assets/DazPose/Runtime/Performer/Wardrobe/WardrobeConfiguration.cs",
                "Assets/DazPose/Runtime/Performer/Wardrobe/WardrobePreset.cs",
                "Assets/DazPose/Runtime/Performer/Wardrobe/WardrobeCatalog.cs",
                "Assets/DazPose/Runtime/Performer/Wardrobe/WardrobeMaterialOpacity.cs",
                "Assets/DazPose/Runtime/Performer/Wardrobe/ClothingSetupDraftBinding.cs",
                "Assets/DazPose/Runtime/Performer/LaraWardrobe.cs"
            };
            string[] inputPaths =
            {
                "Assets/Wardrobe/WardrobeCatalog.asset",
                "Assets/Wardrobe/WardrobeConfiguration.asset",
                "Assets/Production/Wardrobe/LaraCatalog.asset",
                "Assets/Production/Wardrobe/LaraConfiguration.asset",
                "Assets/TestData/LaraCandidate/LaraBody.asset",
                "Assets/TestData/LaraCandidate/FirstPerformanceVoidLaraCandidate.unity",
                "Assets/TestData/Wardrobe/first-outfit/Outfit.asset",
                "Assets/TestData/Wardrobe/second-outfit/Outfit.asset",
                "Assets/TestData/Wardrobe/third-outfit/Outfit.asset"
            };
            string[] dependencyHashes = inputPaths.Select(path => path + ":" +
                (AssetDatabase.LoadMainAssetAtPath(path) != null ? AssetDatabase.GetAssetDependencyHash(path).ToString() : "missing"))
                .ToArray();
            var report = new Report
            {
                startedUtc = started,
                completedUtc = DateTime.UtcNow.ToString("O"),
                sourceHash = HashFiles(sourcePaths),
                inputAssetHashes = inputPaths.Select(path => path + ":" + (File.Exists(path) ? HashFile(path) : "missing"))
                    .Concat(dependencyHashes).ToArray(),
                assertions = assertions,
                artifacts = Array.Empty<string>(),
                limitations = new[] { "Runtime fresh-actor parity is validated by the separate Play Mode fixture." },
                passed = assertions.Length > 0 && assertions.All(assertion => assertion.passed)
            };
            const string output = "TestOutput/day5/transactions.json";
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            File.WriteAllText(output, JsonUtility.ToJson(report, true));
            Debug.Log((report.passed ? "DAY5_TRANSACTION_GATE_PASSED" : "DAY5_TRANSACTION_GATE_FAILED") +
                "; assertions=" + assertions.Count(assertion => assertion.passed) + "/" + assertions.Length + "; report=" + output);
            EditorApplication.Exit(report.passed ? 0 : 1);
        }

        public static Assertion[] Run()
        {
            var checks = new List<Assertion>();
            string suffix = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff");
            string root = "Assets/TestData/WardrobeRuntime/Day5/" + suffix;
            try
            {
                EnsureFolder(root);
                var scene = EditorSceneManager.OpenScene("Assets/TestData/LaraCandidate/FirstPerformanceVoidLaraCandidate.unity", OpenSceneMode.Single);
                AssetDatabase.ImportAsset("Assets/Wardrobe/WardrobeCatalog.asset", ImportAssetOptions.ForceUpdate);
                AssetDatabase.ImportAsset("Assets/Wardrobe/WardrobeConfiguration.asset", ImportAssetOptions.ForceUpdate);
                var sourceCatalog = AssetDatabase.LoadAssetAtPath<WardrobeCatalog>("Assets/Wardrobe/WardrobeCatalog.asset");
                var sourceConfig = AssetDatabase.LoadAssetAtPath<WardrobeConfiguration>("Assets/Wardrobe/WardrobeConfiguration.asset");
                var bodyMesh = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/TestData/LaraCandidate/LaraBody.asset");
                if (sourceCatalog == null || sourceConfig == null || bodyMesh == null)
                    throw new InvalidDataException("Day 5 requires the imported wardrobe catalog/configuration and canonical body mesh.");
                var body = UnityEngine.Object.FindObjectsByType<SkinnedMeshRenderer>(FindObjectsInactive.Include)
                    .FirstOrDefault(renderer => renderer.sharedMesh == bodyMesh);
                if (body == null) throw new InvalidDataException("The authored Lara candidate scene has no renderer bound to LaraBody.asset.");

                var sessionSource = CreateCandidateFixture(sourceCatalog, sourceConfig, root + "/Draft");
                var editablePreset = sessionSource.catalog.Presets.Single(preset => preset.PresetId == "third-outfit");
                var editSession = new WardrobeEditSession(sessionSource.config, sessionSource.catalog);
                var editable = FindEditableAssignment(editSession, editablePreset);
                int originalLayer = editable.row.layer;
                int editedLayer = (originalLayer + 1) % 3;
                bool changed = editSession.TrySetLayer(editablePreset.PresetId, editable.row.sourcePieceId, editedLayer, out string editError);
                var resolved = WardrobeCandidateSnapshot.Resolve(editablePreset, ConfigurationWithSession(sessionSource.config, editSession));
                var resolvedParts = resolved.Parts;
                int partMask = resolvedParts.Aggregate(0, (mask, part) => mask | (1 << part.layer));
                int[] masks = resolved.ReachableMasks;
                int[] alteredMasks = resolved.ReachableMasks;
                if (alteredMasks.Length > 0) alteredMasks[0] = 7;
                var stillResolvedMasks = resolved.ReachableMasks;
                checks.Add(Check("resolvedSnapshotPartsMasksAndCopies", "populated mask matches resolved parts; reachable masks are defensive copies",
                    "changed=" + changed + ";editError=" + (editError ?? "none") + ";partMask=" + partMask + ";snapshotMask=" + resolved.PopulatedMask +
                    ";masks=" + string.Join(",", masks) + ";afterCallerMutation=" + string.Join(",", stillResolvedMasks),
                    changed && partMask == resolved.PopulatedMask && masks.SequenceEqual(stillResolvedMasks) && masks.Contains(partMask) && masks.Contains(0)));

                var assignmentView = editSession.Assignments;
                assignmentView.First(row => row.presetId == editablePreset.PresetId && row.sourcePieceId == editable.row.sourcePieceId).layer = 2;
                bool sessionCopyProtected = editSession.Assignments.Single(row => row.presetId == editablePreset.PresetId && row.sourcePieceId == editable.row.sourcePieceId).layer == editedLayer;
                bool undo = editSession.Undo();
                int undoLayer = editSession.Assignments.Single(row => row.presetId == editablePreset.PresetId && row.sourcePieceId == editable.row.sourcePieceId).layer;
                bool redo = editSession.Redo();
                int redoLayer = editSession.Assignments.Single(row => row.presetId == editablePreset.PresetId && row.sourcePieceId == editable.row.sourcePieceId).layer;
                checks.Add(Check("undoRedoAndSessionDefensiveCopies", "undo restores prior layer, redo restores edited layer, caller array mutation has no effect",
                    "copyProtected=" + sessionCopyProtected + ";undo=" + undo + "/" + undoLayer + ";redo=" + redo + "/" + redoLayer,
                    sessionCopyProtected && undo && undoLayer == originalLayer && redo && redoLayer == editedLayer));

                // A missing canonical renderer binding makes compilation fail deterministically after draft save.
                var releasedCatalog = LoadReleasedCatalog();
                string releasedCatalogPath = AssetDatabase.GetAssetPath(releasedCatalog);
                string[] releasedFiles = ReferencedReleaseFiles(releasedCatalogPath, releasedCatalog);
                string[] releasedHashes = releasedFiles.Select(HashFile).ToArray();
                var releasedConfig = CloneConfiguration(sourceConfig, root + "/ReleasedInput/Configuration.asset");
                releasedConfig.CommitRevision(releasedCatalog.ReleasedGenerationId, releasedConfig.InputHashes);
                EditorUtility.SetDirty(releasedConfig);
                var releasedSession = new WardrobeEditSession(releasedConfig, releasedCatalog);
                var releasedPreset = releasedCatalog.Presets.Single(preset => preset.PresetId == "third-outfit");
                var releasedEdit = FindEditableAssignment(releasedSession, releasedPreset);
                bool releasedChanged = releasedSession.TrySetLayer(releasedPreset.PresetId, releasedEdit.row.sourcePieceId,
                    (releasedEdit.row.layer + 1) % 3, out string releasedEditError);
                bool saved = WardrobeCandidateWorkflow.TrySaveDraft(releasedCatalog, releasedConfig, releasedSession, body,
                    out var draftCatalog, out var draftConfig, out string saveError);
                bool forked = saved && draftCatalog != releasedCatalog && !draftCatalog.IsReleased && draftConfig != releasedConfig;
                // The workflow's released-catalog fork owns its generated asset path; load the returned object after a fresh import.
                AssetDatabase.SaveAssets();
                if (draftConfig != null)
                {
                    string savedPath = AssetDatabase.GetAssetPath(draftConfig);
                    if (!string.IsNullOrEmpty(savedPath)) AssetDatabase.ImportAsset(savedPath, ImportAssetOptions.ForceUpdate);
                    draftConfig = AssetDatabase.LoadAssetAtPath<WardrobeConfiguration>(savedPath);
                }
                bool draftPersisted = draftConfig != null && draftConfig.Assignments.Any(row => row.presetId == releasedPreset.PresetId &&
                    row.sourcePieceId == releasedEdit.row.sourcePieceId && row.layer == (releasedEdit.row.layer + 1) % 3);
                var failedBodyObject = new GameObject("Day5 Invalid Fit Binding Fixture");
                var failedBody = failedBodyObject.AddComponent<SkinnedMeshRenderer>();
                failedBody.sharedMesh = bodyMesh;
                failedBody.bones = Array.Empty<Transform>();
                failedBody.sharedMesh = null;
                bool compiledWithInvalidBinding = WardrobeCandidateWorkflow.TryCompile(draftCatalog, draftConfig, failedBody, out string compileError);
                UnityEngine.Object.DestroyImmediate(failedBodyObject);
                string[] releasedHashesAfter = releasedFiles.Select(HashFile).ToArray();
                bool releaseUnchanged = releasedCatalog.IsReleased && releasedCatalogPath == AssetDatabase.GetAssetPath(releasedCatalog) &&
                    releasedFiles.SequenceEqual(ReferencedReleaseFiles(releasedCatalogPath, releasedCatalog)) && releasedHashes.SequenceEqual(releasedHashesAfter);
                checks.Add(Check("saveDraftSurvivesFitFailureAndProtectsRelease", "draft layer persists after failed compile; previous release references and bytes remain unchanged",
                    "edited=" + releasedChanged + ";editError=" + (releasedEditError ?? "none") + ";saved=" + saved + ";saveError=" + (saveError ?? "none") +
                    ";forked=" + forked + ";draftPersisted=" + draftPersisted + ";compileSucceeded=" + compiledWithInvalidBinding +
                    ";compileError=" + (compileError ?? "none") + ";releaseUnchanged=" + releaseUnchanged,
                    releasedChanged && saved && forked && draftPersisted && !compiledWithInvalidBinding && !string.IsNullOrWhiteSpace(compileError) && releaseUnchanged));

                var unsupportedCatalog = CreateCatalogFixture(releasedCatalog, root + "/UnsupportedFit/Catalog.asset");
                var unsupportedConfig = CloneConfiguration(sourceConfig, root + "/UnsupportedFit/Configuration.asset");
                unsupportedConfig.CommitRevision(releasedCatalog.ReleasedGenerationId, unsupportedConfig.InputHashes);
                var unsupportedPreset = unsupportedCatalog.Presets.Single(preset => preset.PresetId == "first-outfit");
                string[] splitPieceIds = InstallSplitCoverageFixture(unsupportedPreset, root + "/UnsupportedFit/Packages/first-outfit/Outfit.asset");
                var unsupportedSession = new WardrobeEditSession(unsupportedConfig, unsupportedCatalog);
                bool firstLayerSet = unsupportedSession.TrySetLayer(unsupportedPreset.PresetId, splitPieceIds[0], 1, out string firstLayerError);
                bool secondLayerSet = unsupportedSession.TrySetLayer(unsupportedPreset.PresetId, splitPieceIds[1], 2, out string secondLayerError);
                bool unsupportedSaved = WardrobeCandidateWorkflow.TrySaveDraft(unsupportedCatalog, unsupportedConfig,
                    unsupportedSession, body, out var unsupportedDraftCatalog, out var unsupportedDraftConfig, out string unsupportedSaveError);
                string unsupportedCompileError = null;
                bool unsupportedCompiled = unsupportedSaved && WardrobeCandidateWorkflow.TryCompile(
                    unsupportedDraftCatalog, unsupportedDraftConfig, body, out unsupportedCompileError);
                string unsupportedError = unsupportedCompiled ? string.Empty : (unsupportedCompileError ?? string.Empty);
                var unsupportedPublish = unsupportedSaved
                    ? WardrobeGenerationPublisher.PublishCandidate(unsupportedDraftCatalog, unsupportedDraftConfig, body)
                    : new WardrobeGenerationPublisher.Result { issues = new[] { "draft was not saved" } };
                string[] releaseHashesAfterUnsupported = releasedFiles.Select(HashFile).ToArray();
                bool unsupportedPersisted = unsupportedSaved && splitPieceIds.All(id => unsupportedDraftConfig.Assignments.Any(row =>
                    row.presetId == unsupportedPreset.PresetId && row.sourcePieceId == id && row.layer == (id == splitPieceIds[0] ? 1 : 2)));
                bool unsupportedIssueExplicit = !unsupportedCompiled && unsupportedError.IndexOf(
                    "coverage.sharedChannelCannotSeparatePieces", StringComparison.Ordinal) >= 0;
                bool unsupportedReleaseUnchanged = releasedHashes.SequenceEqual(releaseHashesAfterUnsupported) &&
                    releasedFiles.SequenceEqual(ReferencedReleaseFiles(releasedCatalogPath, releasedCatalog));
                checks.Add(Check("unsupportedActualFitSavesDraftAndFailedPublishProtectsRelease", "real source meshes with shared coverage split across layers produce NeedsFit while draft saves and release bytes stay unchanged",
                    "layer1=" + firstLayerSet + "/" + (firstLayerError ?? "none") + ";layer2=" + secondLayerSet + "/" + (secondLayerError ?? "none") +
                    ";saved=" + unsupportedSaved + ";saveError=" + (unsupportedSaveError ?? "none") + ";persisted=" + unsupportedPersisted +
                    ";compileSucceeded=" + unsupportedCompiled + ";compileError=" + (unsupportedError.Length == 0 ? "none" : unsupportedError) +
                    ";issueExplicit=" + unsupportedIssueExplicit + ";publishSucceeded=" + unsupportedPublish.published +
                    ";publishIssues=" + string.Join(";", unsupportedPublish.issues ?? Array.Empty<string>()) + ";releaseUnchanged=" + unsupportedReleaseUnchanged,
                    firstLayerSet && secondLayerSet && unsupportedSaved && unsupportedPersisted && unsupportedIssueExplicit &&
                    !unsupportedPublish.published && unsupportedReleaseUnchanged));

                string mismatchCatalogPath = "Assets/TestData/WardrobeRuntime/Day5/" + suffix + "/Mismatch/Catalog.asset";
                var mismatchCatalog = CreateCatalogFixture(releasedCatalog, mismatchCatalogPath);
                var mismatchConfig = CloneConfiguration(sourceConfig, "Assets/TestData/WardrobeRuntime/Day5/" + suffix + "/Mismatch/Configuration.asset");
                mismatchConfig.CommitRevision("deliberately-mismatched-day5", mismatchConfig.InputHashes);
                bool mismatchAccepted = PerformerWardrobe.TryValidateConfigurationBinding(mismatchCatalog, mismatchConfig, out string mismatchDiagnostic);
                var releasedSnapshotPreset = mismatchCatalog.Presets.Single(preset => preset.PresetId == "third-outfit");
                var releasedSnapshot = WardrobeCandidateSnapshot.Resolve(releasedSnapshotPreset, null);
                int releasedSnapshotMask = releasedSnapshot.PopulatedMask;
                bool mismatchDiagnosticExplicit = !mismatchAccepted && !string.IsNullOrWhiteSpace(mismatchDiagnostic) &&
                    mismatchDiagnostic.IndexOf("generation", StringComparison.OrdinalIgnoreCase) >= 0 &&
                    mismatchDiagnostic.IndexOf("rebind", StringComparison.OrdinalIgnoreCase) >= 0;
                checks.Add(Check("generationMismatchRequiresExplicitRebindAndReleaseResolvesSelfContained", "mismatch fails with generation/rebind diagnostic; released snapshot resolves without draft overlay",
                    "accepted=" + mismatchAccepted + ";diagnostic=" + (mismatchDiagnostic ?? "none") + ";releasedMask=" + releasedSnapshotMask,
                    mismatchDiagnosticExplicit && releasedSnapshot.PresetId == "third-outfit" && releasedSnapshot.ConfigurationRevision == releasedSnapshotPreset.ConfigurationRevision));

                var successFixture = CreateCandidateFixture(sourceCatalog, sourceConfig, root + "/SuccessfulCompile");
                var successPreset = successFixture.catalog.Presets.Single(preset => preset.PresetId == "third-outfit");
                var successSession = new WardrobeEditSession(successFixture.config, successFixture.catalog);
                var successEdit = FindEditableAssignment(successSession, successPreset);
                bool successEditApplied = successSession.TrySetLayer(successPreset.PresetId, successEdit.row.sourcePieceId,
                    (successEdit.row.layer + 1) % 3, out string successEditError);
                bool successSaved = WardrobeCandidateWorkflow.TrySaveDraft(successFixture.catalog, successFixture.config,
                    successSession, body, out var successCatalog, out var successConfig, out string successSaveError);
                if (successSaved)
                {
                    successConfig.CommitRevision("day5-publish-fixture-" + suffix, successConfig.InputHashes);
                    EditorUtility.SetDirty(successConfig);
                }
                string successCompileError = null;
                bool compiled = successSaved && WardrobeCandidateWorkflow.TryCompile(successCatalog, successConfig, body, out successCompileError);
                bool isCompiled = compiled && WardrobeCandidateWorkflow.IsCompiled(successCatalog, successConfig);
                AssetDatabase.SaveAssets();
                string successConfigPath = AssetDatabase.GetAssetPath(successConfig);
                string successCatalogPath = AssetDatabase.GetAssetPath(successCatalog);
                AssetDatabase.ImportAsset(successConfigPath, ImportAssetOptions.ForceUpdate);
                AssetDatabase.ImportAsset(successCatalogPath, ImportAssetOptions.ForceUpdate);
                successConfig = AssetDatabase.LoadAssetAtPath<WardrobeConfiguration>(successConfigPath);
                successCatalog = AssetDatabase.LoadAssetAtPath<WardrobeCatalog>(successCatalogPath);
                successPreset = successCatalog.Presets.Single(preset => preset.PresetId == "third-outfit");
                var reloadedSnapshot = WardrobeCandidateSnapshot.Resolve(successPreset, successConfig);
                int compiledFitMask = successPreset.FitStates.Aggregate(0, (mask, fit) => mask | (1 << fit.VisibleMask));
                bool fitMasksMatch = reloadedSnapshot.ReachableMasks.OrderBy(mask => mask)
                    .SequenceEqual(successPreset.FitStates.Select(fit => fit.VisibleMask).OrderBy(mask => mask));
                var reloadedPart = reloadedSnapshot.Parts.Single(part => part.sourcePieceId == successEdit.row.sourcePieceId);
                bool savedPartMatches = reloadedPart.layer == (successEdit.row.layer + 1) % 3;
                checks.Add(Check("saveCompileReloadPreservesResolvedPartsAndReachableMasks", "saved assignment reloads; every resolved reachable mask has a compiled fit state",
                    "edited=" + successEditApplied + ";editError=" + (successEditError ?? "none") + ";saved=" + successSaved + ";saveError=" + (successSaveError ?? "none") +
                    ";compiled=" + compiled + ";isCompiled=" + isCompiled + ";mask=" + reloadedSnapshot.PopulatedMask + ";fitMaskBits=" + compiledFitMask +
                    ";reachable=" + string.Join(",", reloadedSnapshot.ReachableMasks) + ";compiled=" + string.Join(",", successPreset.FitStates.Select(fit => fit.VisibleMask)) +
                    ";savedPartLayer=" + reloadedPart.layer,
                    successEditApplied && successSaved && compiled && isCompiled && fitMasksMatch && savedPartMatches));

                string opacityPieceId = reloadedPart.sourcePieceId;
                successConfig.SetMaterialOpacity(successPreset.PresetId, opacityPieceId, "0", .37f);
                bool opacityCompiled = WardrobeCandidateWorkflow.TryCompile(successCatalog, successConfig, body, out string opacityCompileError);
                var publishResult = opacityCompiled
                    ? WardrobeGenerationPublisher.PublishCandidate(successCatalog, successConfig, body)
                    : new WardrobeGenerationPublisher.Result { issues = new[] { opacityCompileError ?? "opacity compile failed" } };
                AssetDatabase.SaveAssets();
                var releasedSuccessPreset = successCatalog.Presets.SingleOrDefault(preset => preset.PresetId == "third-outfit");
                var releasedSuccessPiece = releasedSuccessPreset != null
                    ? releasedSuccessPreset.Package.pieces.SingleOrDefault(piece => piece.id == opacityPieceId) : null;
                Material releasedOpacityMaterial = releasedSuccessPiece != null && releasedSuccessPiece.materials.Length > 0
                    ? releasedSuccessPiece.materials[0] : null;
                // Inspect shader values independently of the production opacity helper.
                float releasedAlpha = float.NaN;
                bool alphaReadable = releasedOpacityMaterial != null;
                if (alphaReadable && releasedOpacityMaterial.HasProperty("_Alpha")) releasedAlpha = releasedOpacityMaterial.GetFloat("_Alpha");
                else if (alphaReadable && releasedOpacityMaterial.HasProperty("_AlphaStrength")) releasedAlpha = releasedOpacityMaterial.GetFloat("_AlphaStrength");
                else if (alphaReadable && releasedOpacityMaterial.HasProperty("_BaseColor")) releasedAlpha = releasedOpacityMaterial.GetColor("_BaseColor").a;
                else if (alphaReadable && releasedOpacityMaterial.HasProperty("_Color")) releasedAlpha = releasedOpacityMaterial.GetColor("_Color").a;
                else alphaReadable = false;
                bool successfulReleaseMatches = publishResult.published && successCatalog.IsReleased &&
                    successCatalog.ReleasedGenerationId == publishResult.generationId && successConfig.GenerationHash == publishResult.generationId &&
                    releasedSuccessPreset != null && releasedSuccessPreset != successPreset && releasedSuccessPreset.Package != successPreset.Package &&
                    releasedSuccessPreset.Parts.Single(part => part.sourcePieceId == opacityPieceId).layer == reloadedPart.layer &&
                    alphaReadable && Mathf.Abs(releasedAlpha - .37f) < .001f && releasedSuccessPreset.FitStates.Select(fit => fit.VisibleMask).OrderBy(mask => mask)
                        .SequenceEqual(WardrobeCandidateSnapshot.Resolve(releasedSuccessPreset, null).ReachableMasks.OrderBy(mask => mask));
                string[] publishedReleaseFiles = ReferencedReleaseFiles(AssetDatabase.GetAssetPath(successCatalog), successCatalog);
                string[] publishedReleaseHashes = publishedReleaseFiles.Select(HashFile).ToArray();
                var rejectedRepeatPublish = WardrobeGenerationPublisher.PublishCandidate(successCatalog, successConfig, body);
                bool publishedReleaseProtectedAfterFailure = !rejectedRepeatPublish.published && publishedReleaseFiles.SequenceEqual(
                    ReferencedReleaseFiles(AssetDatabase.GetAssetPath(successCatalog), successCatalog)) &&
                    publishedReleaseHashes.SequenceEqual(publishedReleaseFiles.Select(HashFile));
                checks.Add(Check("publishedSnapshotCapturesResolvedLayerAndOpacityAndRejectsRewrite", "new release contains resolved parts, compiled masks and opacity; failed repeat publication preserves its bytes",
                    "opacityCompiled=" + opacityCompiled + ";compileError=" + (opacityCompileError ?? "none") + ";published=" + publishResult.published +
                    ";generation=" + (publishResult.generationId ?? "none") + ";publishIssues=" + string.Join(";", publishResult.issues ?? Array.Empty<string>()) +
                    ";alpha=" + releasedAlpha + ";releaseMatches=" + successfulReleaseMatches +
                    ";repeatPublished=" + rejectedRepeatPublish.published + ";repeatIssues=" + string.Join(";", rejectedRepeatPublish.issues ?? Array.Empty<string>()) +
                    ";releaseProtected=" + publishedReleaseProtectedAfterFailure,
                    opacityCompiled && successfulReleaseMatches && publishedReleaseProtectedAfterFailure));

                var exposedAssignments = successConfig.Assignments;
                exposedAssignments.First(row => row.presetId == successPreset.PresetId && row.sourcePieceId == successEdit.row.sourcePieceId).layer = 2;
                bool configCopyProtected = successConfig.Assignments.Single(row => row.presetId == successPreset.PresetId && row.sourcePieceId == successEdit.row.sourcePieceId).layer == reloadedPart.layer;
                var publishedPreset = releasedCatalog.Presets.Single(preset => preset.PresetId == "third-outfit");
                var releasedParts = publishedPreset.Parts;
                int releasedOriginalLayer = releasedParts[0].layer;
                releasedParts[0].layer = (releasedOriginalLayer + 1) % 3;
                bool presetCopyProtected = publishedPreset.Parts[0].layer == releasedOriginalLayer;
                checks.Add(Check("persistentAndReleasedDataAreDefensivelyCopied", "configuration and released preset reads cannot mutate stored state",
                    "configCopyProtected=" + configCopyProtected + ";presetCopyProtected=" + presetCopyProtected,
                    configCopyProtected && presetCopyProtected));

                var reimport = CreateReimportFixture(sourceCatalog, sourceConfig, root + "/Reimport");
                string reimportPieceId = reimport.imported.pieces.First(piece => !string.Equals(piece.role, "hair", StringComparison.OrdinalIgnoreCase)).id;
                var reimportRows = reimport.config.Assignments;
                int originalReimportLayer = reimportRows.Single(row => row.presetId == reimport.imported.id && row.sourcePieceId == reimportPieceId).layer;
                var changedRows = reimportRows.Select(row => new WardrobeConfiguration.PieceAssignment
                { presetId = row.presetId, sourcePieceId = row.sourcePieceId,
                    layer = row.presetId == reimport.imported.id && row.sourcePieceId == reimportPieceId ? 2 : row.layer }).ToArray();
                bool layerPrepared = reimport.config.TrySetWorkingAssignments(changedRows, out string reimportLayerError);
                reimport.config.ConfigureGenerated(changedRows, reimport.config.MaterialOverrides, reimport.config.FootwearOverrides);
                reimport.config.SetMaterialOpacity(reimport.imported.id, reimportPieceId, "0", .51f);
                bool shoePrepared = false;
                if (reimport.imported.footwear != null)
                {
                    reimport.config.SetFootwearTuning(reimport.imported.id, true,
                        Mathf.Clamp(reimport.imported.footwear.standingHeight + .01f, 0f, .3f), true,
                        Mathf.Clamp(reimport.imported.footwear.footShrink + .01f, 0f, .1f));
                    shoePrepared = true;
                }
                reimport.catalog.SetCompiledConfigurationHash("day5-prior-compiled-fixture");
                AssetDatabase.SaveAssets();
                var newPiece = ClonePieceWithId(reimport.imported.pieces.First(piece =>
                    !string.Equals(piece.role, "hair", StringComparison.OrdinalIgnoreCase)), "day5-new-stable-piece");
                reimport.imported.pieces = reimport.imported.pieces.Concat(new[] { newPiece }).ToArray();
                EditorUtility.SetDirty(reimport.imported);
                AssetDatabase.SaveAssets();
                bool synchronized = WardrobeCandidateReimport.Synchronize(reimport.catalog, reimport.config,
                    reimport.imported, out string synchronizeError);
                var synchronizedPreset = reimport.catalog.Presets.Single(preset => preset.PresetId == reimport.imported.id);
                int preservedLayer = reimport.config.Assignments.Single(row => row.presetId == reimport.imported.id && row.sourcePieceId == reimportPieceId).layer;
                bool newPieceBase = synchronizedPreset.Parts.Any(part => part.sourcePieceId == newPiece.id && part.layer == 0) &&
                    reimport.config.Assignments.Any(row => row.presetId == reimport.imported.id && row.sourcePieceId == newPiece.id && row.layer == 0);
                bool overridesPreserved = reimport.config.MaterialOverrides.Any(row => row.presetId == reimport.imported.id &&
                    row.sourcePieceId == reimportPieceId && row.opacityOverride && Mathf.Abs(row.opacity - .51f) < .001f) &&
                    (!shoePrepared || reimport.config.FootwearOverrides.Any(row => row.presetId == reimport.imported.id &&
                        row.standingHeightOverride && row.footShrinkOverride));
                bool oldCompileInvalidated = string.IsNullOrEmpty(reimport.catalog.CompiledConfigurationHash);
                reimport.imported.pieces = reimport.imported.pieces.Where(piece => piece.id != reimportPieceId).ToArray();
                EditorUtility.SetDirty(reimport.imported);
                AssetDatabase.SaveAssets();
                bool missingRejected = !WardrobeCandidateReimport.Synchronize(reimport.catalog, reimport.config,
                    reimport.imported, out string missingError);
                int missingPieceAssignmentStillPresent = reimport.config.Assignments.Single(row => row.presetId == reimport.imported.id && row.sourcePieceId == reimportPieceId).layer;
                bool missingNeedsRepair = missingRejected && !string.IsNullOrEmpty(missingError) &&
                    missingError.IndexOf("Needs Repair", StringComparison.OrdinalIgnoreCase) >= 0 &&
                    reimport.config.MaterialOverrides.Any(row => row.presetId == reimport.imported.id && row.sourcePieceId == reimportPieceId && row.opacityOverride) &&
                    (!shoePrepared || reimport.config.FootwearOverrides.Any(row => row.presetId == reimport.imported.id &&
                        row.standingHeightOverride && row.footShrinkOverride)) && missingPieceAssignmentStillPresent == preservedLayer;
                checks.Add(Check("reimportPreservesStableLayersOverridesAndReportsRemovedPieces", "new stable IDs start at Base; prior edits survive refresh; removed source ID is Needs Repair without discarding its overrides",
                    "layerPrepared=" + layerPrepared + ";layerError=" + (reimportLayerError ?? "none") + ";sync=" + synchronized +
                    ";syncError=" + (synchronizeError ?? "none") + ";oldLayer=" + originalReimportLayer + ";preservedLayer=" + preservedLayer +
                    ";newPieceBase=" + newPieceBase + ";overridesPreserved=" + overridesPreserved + ";compileInvalidated=" + oldCompileInvalidated +
                    ";missingRejected=" + missingRejected + ";missingError=" + missingError + ";missingLayerStillPresent=" + missingPieceAssignmentStillPresent,
                    layerPrepared && preservedLayer == 2 && synchronized && newPieceBase && overridesPreserved &&
                    oldCompileInvalidated && missingNeedsRepair));

                checks.Add(RunReleasedForkReimportRegression(releasedCatalog, sourceConfig, root));
                checks.Add(RunSetupHandlerRegression(sourceCatalog, sourceConfig, root, body));
            }
            catch (Exception exception)
            {
                checks.Add(Check("day5RegressionException", "all transactional assertions execute", exception.ToString(), false));
            }
            return checks.ToArray();
        }

        private sealed class Fixture
        {
            public WardrobeCatalog catalog;
            public WardrobeConfiguration config;
        }

        private sealed class ReimportFixture
        {
            public WardrobeCatalog catalog;
            public WardrobeConfiguration config;
            public WardrobeOutfitDefinition imported;
        }

        private static Fixture CreateCandidateFixture(WardrobeCatalog sourceCatalog, WardrobeConfiguration sourceConfig, string folder)
        {
            EnsureFolder(folder + "/Presets");
            var copies = sourceCatalog.Presets.Select(preset =>
            {
                var copy = UnityEngine.Object.Instantiate(preset);
                copy.name = preset.name + " Day5 Fixture";
                AssetDatabase.CreateAsset(copy, folder + "/Presets/" + preset.PresetId + ".asset");
                return copy;
            }).ToArray();
            var catalog = ScriptableObject.CreateInstance<WardrobeCatalog>();
            catalog.name = "Day5 Candidate Catalog";
            catalog.ConfigureCandidate(copies);
            AssetDatabase.CreateAsset(catalog, folder + "/Catalog.asset");
            var config = CloneConfiguration(sourceConfig, folder + "/Configuration.asset");
            AssetDatabase.SaveAssets();
            return new Fixture { catalog = catalog, config = config };
        }

        private static WardrobeCatalog CreateCatalogFixture(WardrobeCatalog source, string path)
        {
            string folder = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(folder + "/Presets");
            var copies = source.Presets.Select(preset =>
            {
                var copy = UnityEngine.Object.Instantiate(preset);
                copy.name = preset.name + " Immutable Release Fixture";
                AssetDatabase.CreateAsset(copy, folder + "/Presets/" + preset.PresetId + ".asset");
                return copy;
            }).ToArray();
            var catalog = ScriptableObject.CreateInstance<WardrobeCatalog>();
            catalog.name = "Day5 Released Snapshot Fixture";
            catalog.ConfigureCandidate(copies);
            catalog.SetReleasedGeneration(source.ReleasedGenerationId);
            AssetDatabase.CreateAsset(catalog, path);
            AssetDatabase.SaveAssets();
            return catalog;
        }

        private static ReimportFixture CreateReimportFixture(WardrobeCatalog sourceCatalog,
            WardrobeConfiguration sourceConfig, string folder)
        {
            EnsureFolder(folder + "/Presets");
            EnsureFolder(folder + "/Packages");
            var sourcePreset = sourceCatalog.Presets.Single(preset => preset.PresetId == "third-outfit");
            var imported = UnityEngine.Object.Instantiate(sourcePreset.Package);
            imported.name = "Day5 Reimport Candidate Package";
            string packagePath = folder + "/Packages/Outfit.asset";
            AssetDatabase.CreateAsset(imported, packagePath);
            var preset = UnityEngine.Object.Instantiate(sourcePreset);
            preset.name = "Day5 Reimport Candidate Preset";
            preset.ConfigureGenerated(sourcePreset.PresetId, sourcePreset.DisplayName, sourcePreset.CharacterSignature,
                sourcePreset.ConfigurationRevision, imported, sourcePreset.Parts, sourcePreset.Aliases,
                sourcePreset.HairAction, sourcePreset.HairId, sourcePreset.Footwear);
            string presetPath = folder + "/Presets/third-outfit.asset";
            AssetDatabase.CreateAsset(preset, presetPath);
            var catalog = ScriptableObject.CreateInstance<WardrobeCatalog>();
            catalog.name = "Day5 Reimport Candidate Catalog";
            catalog.ConfigureCandidate(new[] { preset });
            string catalogPath = folder + "/Catalog.asset";
            AssetDatabase.CreateAsset(catalog, catalogPath);
            var config = CloneConfiguration(sourceConfig, folder + "/Configuration.asset");
            catalog.SetAuthoringConfiguration(config);
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            return new ReimportFixture { catalog = catalog, config = config, imported = imported };
        }

        private static WardrobeOutfitDefinition.Piece ClonePieceWithId(WardrobeOutfitDefinition.Piece source, string id) =>
            new WardrobeOutfitDefinition.Piece
            {
                id = id, sourceNode = source.sourceNode + " (new stable ID)", role = source.role, shell = source.shell,
                requiresBentFootPose = source.requiresBentFootPose, coverageChannel = source.coverageChannel,
                mesh = source.mesh, materials = source.materials != null ? (Material[])source.materials.Clone() : Array.Empty<Material>(),
                boneNames = source.boneNames != null ? (string[])source.boneNames.Clone() : Array.Empty<string>(),
                localBones = source.localBones != null ? source.localBones.Select(bone => new WardrobeOutfitDefinition.LocalBone
                { name = bone.name, parent = bone.parent, position = bone.position, rotation = bone.rotation, scale = bone.scale }).ToArray() :
                    Array.Empty<WardrobeOutfitDefinition.LocalBone>()
            };

        private static WardrobeConfiguration CloneConfiguration(WardrobeConfiguration source, string path)
        {
            EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
            var copy = UnityEngine.Object.Instantiate(source);
            copy.name = "Day5 Configuration Fixture";
            AssetDatabase.CreateAsset(copy, path);
            return copy;
        }

        private static WardrobeConfiguration ConfigurationWithSession(WardrobeConfiguration source, WardrobeEditSession session)
        {
            var copy = ScriptableObject.CreateInstance<WardrobeConfiguration>();
            copy.ConfigureGenerated(session.Assignments, session.MaterialOverrides, session.FootwearOverrides);
            copy.CommitRevision("day5-resolver-fixture", source.InputHashes);
            return copy;
        }

        private static (WardrobeConfiguration.PieceAssignment row, WardrobeConfiguration.PieceAssignment[] rows)
            FindEditableAssignment(WardrobeEditSession session, WardrobePreset preset)
        {
            var rows = session.Assignments;
            var partsById = preset.Parts.ToDictionary(part => part.sourcePieceId, part => part, StringComparer.Ordinal);
            var row = rows.First(candidate => candidate.presetId == preset.PresetId &&
                partsById.ContainsKey(candidate.sourcePieceId) &&
                (partsById[candidate.sourcePieceId].requiredOwnerPieceIds ?? Array.Empty<string>()).Length == 0 &&
                candidate.layer != (candidate.layer + 1) % 3);
            return (row, rows);
        }

        private static WardrobeCatalog LoadReleasedCatalog()
        {
            const string path = "Assets/Production/Wardrobe/LaraCatalog.asset";
            var catalog = AssetDatabase.LoadAssetAtPath<WardrobeCatalog>(path);
            if (catalog == null || !catalog.IsReleased)
                throw new InvalidDataException("The production Lara wardrobe catalog is missing or is not a released snapshot: " + path);
            return catalog;
        }

        private static Assertion RunSetupHandlerRegression(WardrobeCatalog sourceCatalog,
            WardrobeConfiguration sourceConfig, string root, SkinnedMeshRenderer body)
        {
            string originalScenePath = EditorSceneManager.GetActiveScene().path;
            string fixtureScenePath = root + "/ClothingSetupHandlerFixture.unity";
            ClothingSetupWindow window = null;
            try
            {
                // Exercise the delivered prefab composition, preserving its original saved scene.
                EditorSceneManager.OpenScene(ClothingSetupBuilder.ScenePath, OpenSceneMode.Single);
                if (!EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), fixtureScenePath))
                    throw new IOException("Could not fork the saved setup scene for handler validation.");
                var performer = UnityEngine.Object.FindAnyObjectByType<SuccubusPerformer>();
                if (performer == null) throw new InvalidDataException("The authored candidate scene has no performer for Clothing Setup handler validation.");
                var wardrobe = performer.GetComponent<PerformerWardrobe>();
                if (wardrobe == null) wardrobe = performer.gameObject.AddComponent<PerformerWardrobe>();
                body = wardrobe.Body;
                var fixture = CreateCandidateFixture(sourceCatalog, sourceConfig, root + "/WindowHandler");
                wardrobe.ConfigureBinding(fixture.catalog, fixture.config, body, performer, true);
                EditorUtility.SetDirty(wardrobe);
                window = ScriptableObject.CreateInstance<ClothingSetupWindow>();
                SetField(window, "_catalog", fixture.catalog);
                SetField(window, "_configuration", fixture.config);
                var session = new WardrobeEditSession(fixture.config, fixture.catalog);
                var preset = fixture.catalog.Presets.Single(item => item.PresetId == "third-outfit");
                var editable = FindEditableAssignment(session, preset);
                int expectedLayer = (editable.row.layer + 1) % 3;
                if (!session.TrySetLayer(preset.PresetId, editable.row.sourcePieceId, expectedLayer, out string layerError))
                    throw new InvalidOperationException("Could not edit Clothing Setup fixture layer: " + layerError);
                SetField(window, "_session", session);
                SetField(window, "_wardrobe", wardrobe);
                SetField(window, "_performer", performer);
                InvokeHandler(window, "SaveConfiguration");
                var draftBinding = performer.GetComponent<ClothingSetupDraftBinding>();
                var savedCatalog = (WardrobeCatalog)GetField(window, "_catalog");
                var savedConfig = (WardrobeConfiguration)GetField(window, "_configuration");
                var savedSession = (WardrobeEditSession)GetField(window, "_session");
                string saveMessage = (string)GetField(window, "_message");
                int savedLayer = savedConfig != null ? savedConfig.Assignments.Single(row => row.presetId == preset.PresetId &&
                    row.sourcePieceId == editable.row.sourcePieceId).layer : -1;
                bool savePassed = draftBinding != null && draftBinding.Catalog == savedCatalog && draftBinding.Configuration == savedConfig &&
                    savedCatalog == fixture.catalog && savedConfig == fixture.config && savedLayer == expectedLayer && savedSession != null && !savedSession.IsDirty &&
                    !string.IsNullOrWhiteSpace(saveMessage) && saveMessage.IndexOf("Saved candidate draft", StringComparison.OrdinalIgnoreCase) >= 0;

                InvokeHandler(window, "CompileCandidate");
                var compiledCatalog = (WardrobeCatalog)GetField(window, "_catalog");
                var compiledConfig = (WardrobeConfiguration)GetField(window, "_configuration");
                var compileError = (string)GetField(window, "_compileError");
                bool compilePassed = string.IsNullOrEmpty(compileError) && compiledCatalog == fixture.catalog &&
                    compiledConfig == fixture.config && WardrobeCandidateWorkflow.IsCompiled(compiledCatalog, compiledConfig) &&
                    wardrobe.Catalog == compiledCatalog && wardrobe.Configuration == compiledConfig;

                var activeScene = EditorSceneManager.GetActiveScene();
                if (!EditorSceneManager.SaveScene(activeScene, fixtureScenePath))
                    throw new IOException("Could not save the temporary Clothing Setup handler scene fixture.");
                // A headless CreateInstance has no native window container to Close().
                UnityEngine.Object.DestroyImmediate(window);
                window = null;
                EditorSceneManager.OpenScene(fixtureScenePath, OpenSceneMode.Single);
                var reopenedPerformer = UnityEngine.Object.FindAnyObjectByType<SuccubusPerformer>();
                var reopenedBinding = reopenedPerformer != null ? reopenedPerformer.GetComponent<ClothingSetupDraftBinding>() : null;
                var reopenedWardrobe = reopenedPerformer != null ? reopenedPerformer.GetComponent<PerformerWardrobe>() : null;
                var reopenedCatalog = reopenedBinding != null ? reopenedBinding.Catalog : null;
                var reopenedConfig = reopenedBinding != null ? reopenedBinding.Configuration : null;
                int reopenedLayer = reopenedConfig != null ? reopenedConfig.Assignments.Single(row => row.presetId == preset.PresetId &&
                    row.sourcePieceId == editable.row.sourcePieceId).layer : -1;
                bool reopenPassed = reopenedBinding != null && reopenedCatalog == fixture.catalog && reopenedConfig == fixture.config &&
                    reopenedLayer == expectedLayer && reopenedWardrobe != null && reopenedWardrobe.Catalog == reopenedCatalog && reopenedWardrobe.Configuration == reopenedConfig &&
                    WardrobeCandidateWorkflow.IsCompiled(reopenedCatalog, reopenedConfig);
                string actual = "save=" + savePassed + ";saveMessage=" + (saveMessage ?? "none") + ";savedLayer=" + savedLayer +
                    ";compile=" + compilePassed + ";compileError=" + (compileError ?? "none") + ";reopen=" + reopenPassed +
                    ";reopenedLayer=" + reopenedLayer + ";fixtureScene=" + fixtureScenePath;
                if (!string.IsNullOrEmpty(originalScenePath) && originalScenePath != fixtureScenePath)
                    EditorSceneManager.OpenScene(originalScenePath, OpenSceneMode.Single);
                return Check("clothingSetupSaveCompileHandlersReloadActualFixtureScene",
                    "actual SaveConfiguration/CompileCandidate handlers persist and bind a saved candidate through close/reopen", actual,
                    savePassed && compilePassed && reopenPassed);
            }
            finally
            {
                if (window != null) UnityEngine.Object.DestroyImmediate(window);
                if (!string.IsNullOrEmpty(originalScenePath) && EditorSceneManager.GetActiveScene().path == fixtureScenePath && originalScenePath != fixtureScenePath)
                    EditorSceneManager.OpenScene(originalScenePath, OpenSceneMode.Single);
            }
        }

        private static Assertion RunReleasedForkReimportRegression(WardrobeCatalog releasedCatalog,
            WardrobeConfiguration sourceConfig, string root)
        {
            string fixtureRoot = root + "/ReleasedForkReimport";
            EnsureFolder(fixtureRoot + "/RawImport");
            EnsureFolder(fixtureRoot + "/VariantPresets");
            EnsureFolder(fixtureRoot + "/Decoy");
            string releasedCatalogPath = AssetDatabase.GetAssetPath(releasedCatalog);
            string[] releasedFiles = ReferencedReleaseFiles(releasedCatalogPath, releasedCatalog);
            string[] releasedHashes = releasedFiles.Select(HashFile).ToArray();
            var rawSource = AssetDatabase.LoadAssetAtPath<WardrobeOutfitDefinition>("Assets/TestData/Wardrobe/third-outfit/Outfit.asset");
            if (rawSource == null || rawSource.id != "third-outfit")
                throw new InvalidDataException("Release-fork reimport requires the actual third-outfit raw package with its stable package ID.");

            var forkConfig = CloneConfiguration(sourceConfig, fixtureRoot + "/Configuration.asset");
            forkConfig.CommitRevision(releasedCatalog.ReleasedGenerationId, forkConfig.InputHashes);
            var sourcePreset = releasedCatalog.Presets.Single(preset => preset.PresetId == "third-outfit");
            var sourceParts = sourcePreset.Parts;
            var ownerIds = new HashSet<string>(sourceParts.SelectMany(part => part.requiredOwnerPieceIds ?? Array.Empty<string>()), StringComparer.Ordinal);
            string editedPieceId = sourceParts.Where(part => (part.requiredOwnerPieceIds ?? Array.Empty<string>()).Length == 0 &&
                !ownerIds.Contains(part.sourcePieceId)).Select(part => part.sourcePieceId).First(id =>
            {
                var piece = rawSource.pieces.Single(item => item.id == id);
                return !string.Equals(piece.role, "hair", StringComparison.OrdinalIgnoreCase) &&
                    piece.materials != null && piece.materials.Length > 0;
            });
            var session = new WardrobeEditSession(forkConfig, releasedCatalog);
            int originalLayer = session.Assignments.Single(row => row.presetId == sourcePreset.PresetId && row.sourcePieceId == editedPieceId).layer;
            int editedLayer = originalLayer == 0 ? 2 : 0;
            bool layerEdited = session.TrySetLayer(sourcePreset.PresetId, editedPieceId, editedLayer, out string layerError);
            session.SetOpacity(sourcePreset.PresetId, editedPieceId, "0", .43f);
            bool footwearEdited = sourcePreset.Footwear != null;
            if (footwearEdited)
                session.SetFootwear(sourcePreset.PresetId, true,
                    Mathf.Clamp(sourcePreset.Footwear.standingHeight + .012f, 0f, .3f), true,
                    Mathf.Clamp(sourcePreset.Footwear.footShrink + .008f, 0f, .1f));
            bool saved = WardrobeCandidateWorkflow.TrySaveDraft(releasedCatalog, forkConfig, session, null,
                out var draftCatalog, out var draftConfig, out string saveError);
            var forkPreset = saved ? draftCatalog.Presets.Single(preset => preset.PresetId == "third-outfit") : null;
            bool releasePackageIsSnapshot = forkPreset != null && forkPreset != sourcePreset &&
                forkPreset.Package != rawSource && forkPreset.Package == sourcePreset.Package && forkPreset.Package.id == rawSource.id;

            var imported = UnityEngine.Object.Instantiate(rawSource);
            imported.name = "Day5 Raw Third Outfit Reimport Fixture";
            var firstPiece = imported.pieces.First(piece => !string.Equals(piece.role, "hair", StringComparison.OrdinalIgnoreCase));
            string newPieceId = "day5-fork-reimport-new-piece";
            imported.pieces = imported.pieces.Concat(new[] { ClonePieceWithId(firstPiece, newPieceId) }).ToArray();
            string importedPath = fixtureRoot + "/RawImport/Outfit.asset";
            AssetDatabase.CreateAsset(imported, importedPath);
            if (saved)
            {
                string variantId = "day5-variant-" + DateTime.UtcNow.ToString("HHmmssfff");
                var variant = UnityEngine.Object.Instantiate(forkPreset);
                variant.name = "Day5 Shared Source Variant";
                variant.ConfigureGenerated(variantId, "Day 5 Shared Source Variant", forkPreset.CharacterSignature,
                    forkPreset.ConfigurationRevision, forkPreset.Package, forkPreset.Parts, Array.Empty<string>(),
                    forkPreset.HairAction, forkPreset.HairId, forkPreset.Footwear);
                AssetDatabase.CreateAsset(variant, fixtureRoot + "/VariantPresets/SharedSource.asset");
                var allPresets = draftCatalog.Presets.Concat(new[] { variant }).ToArray();
                draftCatalog.ConfigureCandidate(allPresets);
                var assignments = draftConfig.Assignments.ToList();
                assignments.AddRange(assignments.Where(row => row.presetId == forkPreset.PresetId)
                    .Select(row => new WardrobeConfiguration.PieceAssignment
                    { presetId = variantId, sourcePieceId = row.sourcePieceId, layer = row.layer }).ToArray());
                bool variantRowsValid = draftConfig.TrySetWorkingAssignments(assignments.ToArray(), out string variantRowsError);
                draftConfig.ConfigureGenerated(assignments.ToArray(), draftConfig.MaterialOverrides, draftConfig.FootwearOverrides);
                draftCatalog.SetAuthoringConfiguration(draftConfig);
                EditorUtility.SetDirty(draftCatalog);
                EditorUtility.SetDirty(draftConfig);
                EditorUtility.SetDirty(variant);

                bool synced = WardrobeCandidateReimport.Synchronize(draftCatalog, draftConfig, imported, out string syncError);
                forkPreset = draftCatalog.Presets.Single(preset => preset.PresetId == "third-outfit");
                variant = draftCatalog.Presets.Single(preset => preset.PresetId == variantId);
                int savedLayer = draftConfig.Assignments.Single(row => row.presetId == "third-outfit" && row.sourcePieceId == editedPieceId).layer;
                bool newPieceBaseOnBoth = forkPreset.Parts.Any(part => part.sourcePieceId == newPieceId && part.layer == 0) &&
                    variant.Parts.Any(part => part.sourcePieceId == newPieceId && part.layer == 0) &&
                    draftConfig.Assignments.Any(row => row.presetId == "third-outfit" && row.sourcePieceId == newPieceId && row.layer == 0) &&
                    draftConfig.Assignments.Any(row => row.presetId == variantId && row.sourcePieceId == newPieceId && row.layer == 0);
                bool editsPreserved = savedLayer == editedLayer && draftConfig.MaterialOverrides.Any(row => row.presetId == "third-outfit" &&
                    row.sourcePieceId == editedPieceId && row.opacityOverride && Mathf.Abs(row.opacity - .43f) < .001f) &&
                    (!footwearEdited || draftConfig.FootwearOverrides.Any(row => row.presetId == "third-outfit" &&
                        row.standingHeightOverride && row.footShrinkOverride));
                bool packageIdentityMatched = synced && forkPreset.Package == imported && variant.Package == imported &&
                    forkPreset.PresetId != variant.PresetId && forkPreset.Package.id == variant.Package.id;

                // A decoy with the same preset ID but a different package ID must not be selected by preset identity.
                var decoyPackage = UnityEngine.Object.Instantiate(imported);
                decoyPackage.name = "Day5 Different Package ID Decoy";
                decoyPackage.id = "day5-different-source-package";
                AssetDatabase.CreateAsset(decoyPackage, fixtureRoot + "/Decoy/Outfit.asset");
                var decoyPreset = UnityEngine.Object.Instantiate(forkPreset);
                decoyPreset.name = "Day5 Preset ID Decoy";
                decoyPreset.ConfigureGenerated("third-outfit", "Day 5 Preset ID Decoy", forkPreset.CharacterSignature,
                    forkPreset.ConfigurationRevision, decoyPackage, forkPreset.Parts, Array.Empty<string>(),
                    forkPreset.HairAction, forkPreset.HairId, forkPreset.Footwear);
                AssetDatabase.CreateAsset(decoyPreset, fixtureRoot + "/Decoy/Preset.asset");
                var decoyCatalog = ScriptableObject.CreateInstance<WardrobeCatalog>();
                decoyCatalog.name = "Day5 Package Identity Decoy Catalog";
                decoyCatalog.ConfigureCandidate(new[] { decoyPreset });
                string decoyConfigPath = fixtureRoot + "/Decoy/Configuration.asset";
                var decoyConfig = CloneConfiguration(sourceConfig, decoyConfigPath);
                decoyCatalog.SetAuthoringConfiguration(decoyConfig);
                AssetDatabase.CreateAsset(decoyCatalog, fixtureRoot + "/Decoy/Catalog.asset");
                EditorUtility.SetDirty(decoyCatalog);
                bool decoyMatched = WardrobeCandidateReimport.Synchronize(decoyCatalog, decoyConfig, imported, out string decoyError);
                bool decoyRejectedByPackageId = !decoyMatched && !string.IsNullOrEmpty(decoyError) &&
                    decoyError.IndexOf("No candidate preset references", StringComparison.OrdinalIgnoreCase) >= 0;

                string[] releasedHashesAfter = releasedFiles.Select(HashFile).ToArray();
                bool releaseUnchanged = releasedCatalogPath == AssetDatabase.GetAssetPath(releasedCatalog) && releasedCatalog.IsReleased &&
                    releasedFiles.SequenceEqual(ReferencedReleaseFiles(releasedCatalogPath, releasedCatalog)) && releasedHashes.SequenceEqual(releasedHashesAfter);
                return Check("releasedForkReimportMatchesStablePackageIdentity", "release-derived draft fork and variants update by package ID, retain artist choices, and leave release bytes untouched",
                    "layerEdited=" + layerEdited + ";layerError=" + (layerError ?? "none") + ";saved=" + saved + ";saveError=" + (saveError ?? "none") +
                    ";snapshotPackage=" + releasePackageIsSnapshot + ";variantRowsValid=" + variantRowsValid + ";variantRowsError=" + (variantRowsError ?? "none") +
                    ";sync=" + synced + ";syncError=" + (syncError ?? "none") + ";packageMatched=" + packageIdentityMatched +
                    ";newPieceBase=" + newPieceBaseOnBoth + ";editsPreserved=" + editsPreserved + ";decoyRejected=" + decoyRejectedByPackageId +
                    ";decoyError=" + (decoyError ?? "none") + ";releaseUnchanged=" + releaseUnchanged,
                    layerEdited && saved && releasePackageIsSnapshot && variantRowsValid && synced && packageIdentityMatched &&
                    newPieceBaseOnBoth && editsPreserved && decoyRejectedByPackageId && releaseUnchanged);
            }
            return Check("releasedForkReimportMatchesStablePackageIdentity", "release-derived draft reimports by stable package ID", "Save Draft failed", false);
        }

        private static void InvokeHandler(object target, string methodName)
        {
            var method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (method == null) throw new MissingMethodException(target.GetType().FullName, methodName);
            method.Invoke(target, null);
        }

        private static object GetField(object target, string fieldName)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null) throw new MissingFieldException(target.GetType().FullName, fieldName);
            return field.GetValue(target);
        }

        private static void SetField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null) throw new MissingFieldException(target.GetType().FullName, fieldName);
            field.SetValue(target, value);
        }

        private static string[] ReferencedReleaseFiles(string catalogPath, WardrobeCatalog catalog)
        {
            var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { catalogPath };
            foreach (string dependency in AssetDatabase.GetDependencies(catalogPath, true)) AddAssetAndMeta(paths, dependency);
            foreach (var preset in catalog.Presets)
            {
                AddAssetAndMeta(paths, AssetDatabase.GetAssetPath(preset));
                AddAssetAndMeta(paths, AssetDatabase.GetAssetPath(preset.Package));
                foreach (var fit in preset.FitStates)
                {
                    AddAssetAndMeta(paths, AssetDatabase.GetAssetPath(fit));
                    AddAssetAndMeta(paths, AssetDatabase.GetAssetPath(fit.BodyMesh));
                    AddAssetAndMeta(paths, AssetDatabase.GetAssetPath(fit.SurfaceBindings));
                    AddAssetAndMeta(paths, AssetDatabase.GetAssetPath(fit.DissolveProfile));
                    foreach (var mesh in fit.AttachmentMeshes) AddAssetAndMeta(paths, AssetDatabase.GetAssetPath(mesh));
                }
            }
            return paths.Where(FileExists).OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToArray();
        }

        private static void AddAssetAndMeta(HashSet<string> paths, string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return;
            paths.Add(path);
            paths.Add(path + ".meta");
        }

        private static string[] InstallSplitCoverageFixture(WardrobePreset preset, string packagePath)
        {
            EnsureFolder(Path.GetDirectoryName(packagePath).Replace('\\', '/'));
            var package = UnityEngine.Object.Instantiate(preset.Package);
            package.name = "Day5 Unsupported Shared Coverage Fixture";
            var selectable = preset.Parts.Where(part => (part.requiredOwnerPieceIds ?? Array.Empty<string>()).Length == 0 &&
                package.pieces.Any(piece => piece.id == part.sourcePieceId && !string.Equals(piece.role, "hair", StringComparison.OrdinalIgnoreCase)))
                .Take(2).ToArray();
            if (selectable.Length != 2) throw new InvalidDataException("The imported first outfit needs two independent non-hair source pieces for split-coverage validation.");
            foreach (var part in selectable) package.pieces.Single(piece => piece.id == part.sourcePieceId).coverageChannel = 0;
            AssetDatabase.CreateAsset(package, packagePath);
            preset.ConfigureGenerated(preset.PresetId, preset.DisplayName, preset.CharacterSignature,
                preset.ConfigurationRevision, package, preset.Parts, preset.Aliases, preset.HairAction,
                preset.HairId, preset.Footwear);
            EditorUtility.SetDirty(preset);
            AssetDatabase.SaveAssets();
            return selectable.Select(part => part.sourcePieceId).ToArray();
        }

        private static Assertion Check(string name, string expected, string actual, bool passed) =>
            new Assertion { name = name, expected = expected, actual = actual, passed = passed };

        private static string HashFile(string path)
        {
            using (var sha = SHA256.Create())
            using (var stream = new FileStream(FileSystemPath(path), FileMode.Open, FileAccess.Read, FileShare.Read))
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }

        private static bool FileExists(string path) => File.Exists(FileSystemPath(path));

        private static string FileSystemPath(string path)
        {
            string fullPath = Path.GetFullPath(path);
            if (Application.platform != RuntimePlatform.WindowsEditor || fullPath.Length < 260 ||
                fullPath.StartsWith("\\\\?\\", StringComparison.Ordinal)) return fullPath;
            return fullPath.StartsWith("\\\\", StringComparison.Ordinal)
                ? "\\\\?\\UNC\\" + fullPath.Substring(2)
                : "\\\\?\\" + fullPath;
        }

        private static string HashFiles(IEnumerable<string> paths)
        {
            string text = string.Join("\n", paths.OrderBy(path => path, StringComparer.Ordinal)
                .Select(path => path + ":" + (FileExists(path) ? HashFile(path) : "missing")));
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(text))).Replace("-", "").ToLowerInvariant();
        }

        private static void EnsureFolder(string path)
        {
            path = path.Replace('\\', '/').TrimEnd('/');
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/');
            if (slash <= 0) throw new InvalidOperationException("Invalid asset folder: " + path);
            EnsureFolder(path.Substring(0, slash));
            AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
        }
    }
}
