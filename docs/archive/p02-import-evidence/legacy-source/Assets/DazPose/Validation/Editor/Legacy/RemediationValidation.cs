#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using DazPose.FirstPerformanceVoid;
using DazPose.Motion;
using DazPose.Player;
using DazPose.Performer;
using DazPose.Toys;
using DazPose.UnityValidation;
using DazPose.UnityValidation.Partner;
using DazPose.Validation;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DazPose.Remediation
{
    /// <summary>Batch-only entry point for the numbered remediation tickets.</summary>
    public static class RemediationValidation
    {
        private sealed class R03CompileResult
        {
            public int validatedStates;
            public int[] reachableMasks = Array.Empty<int>();
        }

        [Serializable] private sealed class Assertion
        {
            public string name;
            public string expected;
            public string actual;
            public bool passed;
        }

        [Serializable] private sealed class Report
        {
            public string ticket;
            public string startedUtc;
            public string completedUtc;
            public string sourceHash;
            public string[] inputAssetHashes;
            public bool complete;
            public bool passed;
            public string exception;
            public string r12BuildManifestPath;
            public List<Assertion> assertions = new List<Assertion>();
        }

        [Serializable] private sealed class R11Manifest
        {
            public R11Source[] files;
            public R11Source[] newFolderMetas;
            public R11Source[] newSourceMetas;
            public R11AssemblyDefinition[] assemblyDefinitions;
            public R11AssemblyReference[] assemblyReferences;
            public R11PackageAssembly[] packageAssemblies;
        }

        [Serializable] private sealed class R11Source
        {
            public string source;
            public string path;
            public string destination;
            public string guid;
        }

        [Serializable] private sealed class R11AssemblyDefinition
        {
            public string name;
            public string path;
            public string guid;
            public string[] references;
            public string[] includePlatforms;
            public string[] excludePlatforms;
            public bool allowUnsafeCode;
            public bool overrideReferences;
            public string[] precompiledReferences;
            public bool autoReferenced;
            public string[] defineConstraints;
            public R11VersionDefine[] versionDefines;
            public bool noEngineReferences;
        }

        [Serializable] private sealed class R11VersionDefine
        {
            public string name;
            public string expression;
            public string define;
        }

        [Serializable] private sealed class R11AssemblyDefinitionFile
        {
            public string name;
            public string[] references;
            public string[] includePlatforms;
            public string[] excludePlatforms;
            public bool allowUnsafeCode;
            public bool overrideReferences;
            public string[] precompiledReferences;
            public bool autoReferenced;
            public string[] defineConstraints;
            public R11VersionDefine[] versionDefines;
            public bool noEngineReferences;
        }

        [Serializable] private sealed class R11PackageAssembly
        {
            public string assembly;
            public string guid;
            public string package;
            public string assetPath;
        }

        [Serializable] private sealed class R11AssemblyReferenceFile
        {
            public string reference;
        }

        [Serializable] private sealed class R11AssemblyReference
        {
            public string path;
            public string target;
            public string guid;
            public string reference;
        }

        public static void RunBatch()
        {
            var report = new Report
            {
                ticket = Argument("-remediationTicket") ?? string.Empty,
                startedUtc = DateTime.UtcNow.ToString("O"),
                sourceHash = Argument("-remediationSourceHash") ?? string.Empty,
                inputAssetHashes = new[] { Argument("-remediationInputHash") ?? string.Empty }
            };
            string reportPath = Argument("-remediationReport");
            int exit = 1;
            bool deferExitToPlayModeRunner = false;
            try
            {
                if (string.Equals(report.ticket, "R02", StringComparison.Ordinal))
                {
                    deferExitToPlayModeRunner = true;
                    PrepareToeFixture(reportPath, report.sourceHash, report.inputAssetHashes[0]);
                    return;
                }
                if (string.Equals(report.ticket, "R03", StringComparison.Ordinal))
                {
                    deferExitToPlayModeRunner = true;
                    PrepareToeRepairFixture(reportPath, report.sourceHash, report.inputAssetHashes[0],
                        Argument("-remediationPriorReport"));
                    return;
                }
                if (string.Equals(report.ticket, "R04", StringComparison.Ordinal))
                {
                    deferExitToPlayModeRunner = true;
                    PreparePreflightFixture(reportPath, report.sourceHash, report.inputAssetHashes[0]);
                    return;
                }
                if (string.Equals(report.ticket, "R05", StringComparison.Ordinal))
                {
                    deferExitToPlayModeRunner = true;
                    PrepareContentPreflightFixture(reportPath, report.sourceHash, report.inputAssetHashes[0]);
                    return;
                }
                if (string.Equals(report.ticket, "R06", StringComparison.Ordinal))
                {
                    deferExitToPlayModeRunner = true;
                    PrepareScopedCommandFixture(reportPath, report.sourceHash, report.inputAssetHashes[0]);
                    return;
                }
                if (string.Equals(report.ticket, "R07", StringComparison.Ordinal))
                {
                    deferExitToPlayModeRunner = true;
                    PreparePersistentOwnershipFixture(reportPath, report.sourceHash, report.inputAssetHashes[0]);
                    return;
                }
                if (string.Equals(report.ticket, "R08", StringComparison.Ordinal))
                {
                    deferExitToPlayModeRunner = true;
                    PrepareAuthorSurfaceFixture(reportPath, report.sourceHash, report.inputAssetHashes[0]);
                    return;
                }
                if (string.Equals(report.ticket, "R09", StringComparison.Ordinal))
                {
                    RunR09(report, ref deferExitToPlayModeRunner);
                    report.passed = report.assertions.All(item => item.passed);
                    report.complete = report.passed;
                    exit = report.passed ? 0 : 1;
                }
                else if (string.Equals(report.ticket, "R11", StringComparison.Ordinal))
                {
                    RunR11(report);
                    report.passed = report.assertions.All(item => item.passed);
                    report.complete = true;
                    exit = report.passed ? 0 : 1;
                    return;
                }
                else if (string.Equals(report.ticket, "R12", StringComparison.Ordinal))
                {
                    RunR12(report);
                    report.passed = report.assertions.All(item => item.passed);
                    report.complete = true;
                    exit = report.passed ? 0 : 1;
                    return;
                }
                else if (!string.Equals(report.ticket, "R01", StringComparison.Ordinal))
                    throw new InvalidOperationException("Ticket is not implemented in the current runner. Unsupported tickets fail closed.");
                Assert(report, "ticketIsR01", "R01", report.ticket);
                Assert(report, "sourceHashPresent", "non-empty", report.sourceHash);
                Assert(report, "inputHashPresent", "non-empty", report.inputAssetHashes[0]);
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                AssertAsset(report, "authoredVoidSceneResolves", "Assets/Scenes/FirstPerformanceVoid.unity");
                AssertAsset(report, "clothingSetupSceneResolves", "Assets/Scenes/ClothingSetup.unity");
                AssertAnyAsset(report, "wardrobeCatalogResolves", "t:WardrobeCatalog");
                AssertAnyAsset(report, "wardrobePresetResolves", "t:WardrobePreset");
                AssertAsset(report, "laraPrefabResolves", "Assets/Production/Performers/Lara.prefab");
                var laraPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Production/Performers/Lara.prefab");
                bool performerFound = laraPrefab != null && laraPrefab.GetComponentInChildren<SuccubusPerformer>(true) != null;
                report.assertions.Add(new Assertion { name = "succubusPerformerResolves", expected = "Lara prefab contains SuccubusPerformer", actual = performerFound ? "found" : "missing", passed = performerFound });
                if (!performerFound) throw new InvalidOperationException("Lara prefab does not contain SuccubusPerformer.");
                AssertFile(report, "projectVersionResolves", Path.Combine(Directory.GetParent(Application.dataPath).FullName, "ProjectSettings", "ProjectVersion.txt"));
                AssertFile(report, "packageManifestResolves", Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Packages", "manifest.json"));
                report.passed = report.assertions.All(item => item.passed);
                report.complete = report.passed;
                exit = report.passed ? 0 : 1;
            }
            catch (Exception exception)
            {
                report.exception = exception.ToString();
                report.passed = false;
                report.complete = true;
                exit = 1;
                Debug.LogException(exception);
            }
            finally
            {
                if (!deferExitToPlayModeRunner)
                {
                    report.completedUtc = DateTime.UtcNow.ToString("O");
                    if (string.IsNullOrWhiteSpace(reportPath)) reportPath = Path.Combine("TestOutput", "remediation", report.ticket + ".json");
                    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath)));
                    File.WriteAllText(reportPath, JsonUtility.ToJson(report, true));
                    EditorApplication.Exit(exit);
                }
            }
        }

        private static void PrepareToeFixture(string reportPath, string sourceHash, string inputHash)
        {
            const string sourceScene = "Assets/Scenes/ClothingSetup.unity";
            const string fixtureScene = "Assets/Production/Scenes/RemediationToeValidation.unity";
            if (!AssetDatabase.LoadAssetAtPath<SceneAsset>(fixtureScene))
            {
                string directory = Path.GetDirectoryName(fixtureScene).Replace('\\', '/');
                if (!AssetDatabase.IsValidFolder(directory))
                {
                    Directory.CreateDirectory(Path.Combine(Application.dataPath, "Production/Scenes"));
                    AssetDatabase.Refresh();
                }
                if (!AssetDatabase.CopyAsset(sourceScene, fixtureScene))
                    throw new InvalidOperationException("Could not copy ClothingSetup to the remediation toe fixture.");
                AssetDatabase.ImportAsset(fixtureScene, ImportAssetOptions.ForceSynchronousImport);
            }
            var scene = EditorSceneManager.OpenScene(fixtureScene, OpenSceneMode.Single);
            foreach (var oldRunner in UnityEngine.Object.FindObjectsByType<ClothingSetupPreviewRunner>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                oldRunner.enabled = false;
            var actor = UnityEngine.Object.FindFirstObjectByType<SuccubusPerformer>(FindObjectsInactive.Include);
            if (actor == null) throw new InvalidOperationException("The copied ClothingSetup fixture has no SuccubusPerformer.");
            var runner = UnityEngine.Object.FindFirstObjectByType<RemediationToeRunner>(FindObjectsInactive.Include);
            if (runner == null)
            {
                var holder = new GameObject("Remediation Toe Runner");
                SceneManager.MoveGameObjectToScene(holder, scene);
                runner = holder.AddComponent<RemediationToeRunner>();
            }
            runner.Configure(actor, reportPath, sourceHash, inputHash);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save the remediation toe fixture.");
            EditorApplication.isPlaying = true;
        }

        private static void PrepareToeRepairFixture(string reportPath, string sourceHash, string inputHash,
            string priorReportPath)
        {
            const string fixtureScene = "Assets/Production/Scenes/RemediationToeValidation.unity";
            const string outputRoot = "Assets/Production/Wardrobe/Remediation/R03";
            var scene = EditorSceneManager.OpenScene(fixtureScene, OpenSceneMode.Single);
            foreach (var oldRunner in UnityEngine.Object.FindObjectsByType<ClothingSetupPreviewRunner>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                oldRunner.enabled = false;
            foreach (var oldWitness in UnityEngine.Object.FindObjectsByType<RemediationToeRunner>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                oldWitness.enabled = false;
            var actor = UnityEngine.Object.FindFirstObjectByType<SuccubusPerformer>(FindObjectsInactive.Include);
            if (actor == null) throw new InvalidOperationException("The toe fixture has no primary SuccubusPerformer.");
            var wardrobe = actor.GetComponent<PerformerWardrobe>();
            if (wardrobe == null || wardrobe.Catalog == null || wardrobe.Body == null)
                throw new InvalidOperationException("The toe fixture primary actor has no wardrobe binding.");
            var secondOutfit = wardrobe.Catalog.Presets.FirstOrDefault(item => item != null && item.PresetId == "second-outfit");
            if (secondOutfit == null) throw new InvalidOperationException("The candidate catalog has no second-outfit preset.");
            var compile = CompileR03(secondOutfit, wardrobe.Configuration, wardrobe.Body, outputRoot);
            if (compile.validatedStates <= 0 || !compile.reachableMasks.Contains(1))
                throw new InvalidOperationException("R03 fit generation did not produce a validated mask-1 state.");
            var thirdOutfit = wardrobe.Catalog.Presets.FirstOrDefault(item => item != null && item.PresetId == "third-outfit");
            if (thirdOutfit == null) throw new InvalidOperationException("The candidate catalog has no third-outfit preset.");
            var shoePart = thirdOutfit.Parts.FirstOrDefault(part =>
                thirdOutfit.Package.pieces.Any(piece => piece.id == part.sourcePieceId &&
                    string.Equals(piece.role, "footwear", StringComparison.OrdinalIgnoreCase)));
            if (shoePart == null) throw new InvalidOperationException("third-outfit has no footwear part to move into its hosiery-only fixture layer.");
            var thirdParts = thirdOutfit.Parts;
            var thirdShoe = thirdParts.Single(part => part.sourcePieceId == shoePart.sourcePieceId);
            thirdShoe.layer = 1;
            var assignments = wardrobe.Configuration.Assignments;
            var thirdAssignment = assignments.FirstOrDefault(row => row.presetId == thirdOutfit.PresetId && row.sourcePieceId == shoePart.sourcePieceId);
            if (thirdAssignment == null) throw new InvalidOperationException("third-outfit footwear has no configuration assignment.");
            thirdAssignment.layer = 1;
            wardrobe.Configuration.ConfigureGenerated(assignments, wardrobe.Configuration.MaterialOverrides,
                wardrobe.Configuration.FootwearOverrides);
            thirdOutfit.ConfigureGenerated(thirdOutfit.PresetId, thirdOutfit.DisplayName,
                thirdOutfit.CharacterSignature, wardrobe.Configuration.Revision, thirdOutfit.Package, thirdParts,
                thirdOutfit.Aliases, thirdOutfit.HairAction, thirdOutfit.HairId, thirdOutfit.Footwear);
            var hosieryCompile = CompileR03(thirdOutfit, wardrobe.Configuration, wardrobe.Body,
                outputRoot + "/third-outfit");
            if (hosieryCompile.validatedStates <= 0 || !hosieryCompile.reachableMasks.Contains(1))
                throw new InvalidOperationException("R03 hosiery fixture did not produce a validated mask-1 state.");
            wardrobe.Catalog.SetAuthoringConfiguration(wardrobe.Configuration);
            wardrobe.Catalog.SetCompiledConfigurationHash("r03-hosiery-fixture");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);

            var isolation = UnityEngine.Object.FindObjectsByType<SuccubusPerformer>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(item => item != actor && item.name == "Remediation Toe Isolation Actor");
            if (isolation == null)
            {
                var clone = UnityEngine.Object.Instantiate(actor.gameObject);
                clone.name = "Remediation Toe Isolation Actor";
                clone.transform.position += Vector3.right * 2f;
                SceneManager.MoveGameObjectToScene(clone, scene);
                isolation = clone.GetComponent<SuccubusPerformer>();
            }
            if (isolation == null) throw new InvalidOperationException("R03 could not create its isolation actor.");
            var runner = UnityEngine.Object.FindFirstObjectByType<RemediationToeRepairRunner>(FindObjectsInactive.Include);
            if (runner == null)
            {
                var holder = new GameObject("Remediation Toe Repair Runner");
                SceneManager.MoveGameObjectToScene(holder, scene);
                runner = holder.AddComponent<RemediationToeRepairRunner>();
            }
            runner.Configure(actor, isolation, reportPath, sourceHash, inputHash, priorReportPath,
                compile.validatedStates, compile.reachableMasks);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save the R03 toe fixture.");
            EditorApplication.isPlaying = true;
        }

        private static void PreparePreflightFixture(string reportPath, string sourceHash, string inputHash)
        {
            const string sourceScene = "Assets/Production/Scenes/Day4PartnerContactValidation.unity";
            const string fixtureScene = "Assets/Production/Scenes/RemediationPreflightValidation.unity";
            if (!AssetDatabase.LoadAssetAtPath<SceneAsset>(fixtureScene))
            {
                if (!AssetDatabase.CopyAsset(sourceScene, fixtureScene))
                    throw new InvalidOperationException("Could not copy the authored Day4 contact scene to the R04 preflight fixture.");
                AssetDatabase.ImportAsset(fixtureScene, ImportAssetOptions.ForceSynchronousImport);
            }
            var scene = EditorSceneManager.OpenScene(fixtureScene, OpenSceneMode.Single);
            foreach (var oldRunner in UnityEngine.Object.FindObjectsByType<Day4RegressionRunner>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                oldRunner.enabled = false;
            var actor = UnityEngine.Object.FindAnyObjectByType<SuccubusPerformer>();
            var partner = UnityEngine.Object.FindAnyObjectByType<PartnerBody>();
            var target = UnityEngine.Object.FindAnyObjectByType<PartnerContactTarget>();
            var bindings = UnityEngine.Object.FindAnyObjectByType<PerformanceSceneBindings>();
            if (actor == null || partner == null || target == null || bindings == null)
                throw new InvalidOperationException("The copied Day4 scene is missing the actor, partner, contact target or scene bindings.");
            var runner = UnityEngine.Object.FindAnyObjectByType<RemediationPreflightRunner>();
            if (runner == null)
            {
                var holder = new GameObject("Remediation Preflight Runner");
                SceneManager.MoveGameObjectToScene(holder, scene);
                runner = holder.AddComponent<RemediationPreflightRunner>();
                if (runner == null)
                    throw new InvalidOperationException("Unity could not attach RemediationPreflightRunner. It must compile into DazPose.Validation, not an Editor-only assembly.");
            }
            runner.Configure(actor, partner, target, bindings, reportPath, sourceHash, inputHash);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save the R04 preflight fixture.");
            EditorApplication.isPlaying = true;
        }

        private static void PrepareContentPreflightFixture(string reportPath, string sourceHash, string inputHash)
        {
            const string sourceScene = "Assets/Production/Scenes/Day4PartnerContactValidation.unity";
            const string fixtureScene = "Assets/Production/Scenes/RemediationContentPreflightValidation.unity";
            if (!AssetDatabase.LoadAssetAtPath<SceneAsset>(fixtureScene))
            {
                if (!AssetDatabase.CopyAsset(sourceScene, fixtureScene))
                    throw new InvalidOperationException("Could not copy the authored Day4 contact scene to the R05 content preflight fixture.");
                AssetDatabase.ImportAsset(fixtureScene, ImportAssetOptions.ForceSynchronousImport);
            }
            var scene = EditorSceneManager.OpenScene(fixtureScene, OpenSceneMode.Single);
            foreach (var oldRunner in UnityEngine.Object.FindObjectsByType<Day4RegressionRunner>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                oldRunner.enabled = false;
            var actor = UnityEngine.Object.FindAnyObjectByType<SuccubusPerformer>();
            var bindings = UnityEngine.Object.FindAnyObjectByType<PerformanceSceneBindings>();
            var gesture = AssetDatabase.LoadAssetAtPath<PerformerGesture>("Assets/DazPose/Generated/Gestures/Acceptance/RightHandWave.asset");
            var action = AssetDatabase.LoadAssetAtPath<PerformerAction>("Assets/DazPose/Generated/Actions/Acceptance/JumpForJoy.asset");
            var spell = AssetDatabase.LoadAssetAtPath<PerformerSpell>("Assets/DazPose/Generated/Magic/Spells/Spell_VioletSerenity.asset");
            var aura = AssetDatabase.LoadAssetAtPath<PerformerAura>("Assets/DazPose/Generated/Magic/Auras/Aura_VioletSerenity.asset");
            var style = AssetDatabase.LoadAssetAtPath<PerformerMagicStyle>("Assets/DazPose/Generated/Magic/Styles/Style_VioletSerenity.asset");
            if (actor == null || bindings == null || gesture == null || action == null || spell == null || aura == null || style == null)
                throw new InvalidOperationException("The R05 fixture is missing its actor, scene bindings or prepared Gesture/Action/Magic assets.");
            var runner = UnityEngine.Object.FindAnyObjectByType<RemediationContentPreflightRunner>();
            if (runner == null)
            {
                var holder = new GameObject("Remediation Content Preflight Runner");
                SceneManager.MoveGameObjectToScene(holder, scene);
                runner = holder.AddComponent<RemediationContentPreflightRunner>();
            }
            runner.Configure(actor, bindings, gesture, action, spell, aura, style, actor.transform,
                reportPath, sourceHash, inputHash);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save the R05 content preflight fixture.");
            EditorApplication.isPlaying = true;
        }

        private static void PrepareScopedCommandFixture(string reportPath, string sourceHash, string inputHash)
        {
            const string sourceScene = "Assets/Production/Scenes/Day4PartnerContactValidation.unity";
            const string fixtureScene = "Assets/Production/Scenes/RemediationScopedCommandValidation.unity";
            if (!AssetDatabase.LoadAssetAtPath<SceneAsset>(fixtureScene))
            {
                if (!AssetDatabase.CopyAsset(sourceScene, fixtureScene))
                    throw new InvalidOperationException("Could not copy the authored Day4 contact scene to the R06 scoped-command fixture.");
                AssetDatabase.ImportAsset(fixtureScene, ImportAssetOptions.ForceSynchronousImport);
            }
            var scene = EditorSceneManager.OpenScene(fixtureScene, OpenSceneMode.Single);
            foreach (var oldRunner in UnityEngine.Object.FindObjectsByType<Day4RegressionRunner>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                oldRunner.enabled = false;
            var actor = UnityEngine.Object.FindAnyObjectByType<SuccubusPerformer>();
            var bindings = UnityEngine.Object.FindAnyObjectByType<PerformanceSceneBindings>();
            AudioClip first = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/FemaleMovementAnimsetPro/Audio/Steps/Step1.wav");
            AudioClip second = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/FemaleMovementAnimsetPro/Audio/Steps/Step2.wav");
            AudioClip longClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Crazy Minnow Studio/Examples/Audio/Promo-male.mp3");
            if (actor == null || bindings == null || first == null || second == null)
                throw new InvalidOperationException("The R06 fixture is missing its actor, scene bindings or speech clips.");
            var runner = UnityEngine.Object.FindAnyObjectByType<RemediationScopedCommandRunner>();
            if (runner == null)
            {
                var holder = new GameObject("Remediation Scoped Command Runner");
                SceneManager.MoveGameObjectToScene(holder, scene);
                runner = holder.AddComponent<RemediationScopedCommandRunner>();
            }
            runner.Configure(actor, bindings, first, second, longClip, reportPath, sourceHash, inputHash);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save the R06 scoped-command fixture.");
            EditorApplication.isPlaying = true;
        }

        private static void PreparePersistentOwnershipFixture(string reportPath, string sourceHash, string inputHash)
        {
            const string sourceScene = "Assets/Production/Scenes/Day4PartnerContactValidation.unity";
            const string fixtureScene = "Assets/Production/Scenes/RemediationPersistentOwnershipValidation.unity";
            if (!AssetDatabase.LoadAssetAtPath<SceneAsset>(fixtureScene))
            {
                if (!AssetDatabase.CopyAsset(sourceScene, fixtureScene))
                    throw new InvalidOperationException("Could not copy the authored Day4 contact scene to the R07 ownership fixture.");
                AssetDatabase.ImportAsset(fixtureScene, ImportAssetOptions.ForceSynchronousImport);
            }
            var scene = EditorSceneManager.OpenScene(fixtureScene, OpenSceneMode.Single);
            foreach (var oldRunner in UnityEngine.Object.FindObjectsByType<Day4RegressionRunner>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                oldRunner.enabled = false;
            var actor = UnityEngine.Object.FindAnyObjectByType<SuccubusPerformer>();
            var bindings = UnityEngine.Object.FindAnyObjectByType<PerformanceSceneBindings>();
            var player = UnityEngine.Object.FindAnyObjectByType<PlayerController>();
            var toys = UnityEngine.Object.FindAnyObjectByType<ToyControlService>();
            var playback = actor != null ? actor.MotionSource : UnityEngine.Object.FindAnyObjectByType<MotionDriver>();
            var contact = UnityEngine.Object.FindAnyObjectByType<PartnerContactTarget>();
            var program = AssetDatabase.LoadAssetAtPath<FunscriptMotionProgram>("Assets/motiondrive.funscript");
            var expression = AssetDatabase.LoadAssetAtPath<PerformerExpression>("Assets/Production/ValidationData/Day3Expression.asset");
            var auras = AssetDatabase.FindAssets("t:PerformerAura")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<PerformerAura>)
                .Where(item => item != null && item.IsReady(out _)).Take(2).ToArray();
            var clips = AssetDatabase.FindAssets("t:AudioClip")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<AudioClip>)
                .Where(item => item != null).Take(2).ToArray();
            if (player != null && toys == null)
            {
                toys = player.gameObject.GetComponent<ToyControlService>();
                if (toys == null) toys = player.gameObject.AddComponent<ToyControlService>();
                player.ConfigureToyService(toys);
            }
            if (actor == null || bindings == null || player == null || toys == null || playback == null || contact == null || program == null || expression == null)
                throw new InvalidOperationException("The R07 fixture is missing actor, binding, player, toys, playback, contact, or Funscript references.");
            if (auras.Length < 2) throw new InvalidOperationException("R07 requires two ready PerformerAura assets for replacement checks.");
            if (clips.Length < 2) throw new InvalidOperationException("R07 requires two AudioClip assets for scoped run setup.");
            bindings.Configure(actor, player, playback, toys, PerformerCapability.Pose | PerformerCapability.Speech);
            bindings.ConfigureContactTarget(contact);
            bindings.DeclareAssets(null, new[] { expression }, clips);
            toys.ConfigurePlayback(playback);
            var runner = UnityEngine.Object.FindAnyObjectByType<RemediationPersistentOwnershipRunner>();
            if (runner == null)
            {
                var holder = new GameObject("Remediation Persistent Ownership Runner");
                SceneManager.MoveGameObjectToScene(holder, scene);
                runner = holder.AddComponent<RemediationPersistentOwnershipRunner>();
            }
            runner.Configure(actor, bindings, player, toys, playback, contact, program, auras[0], auras[1],
                expression, clips[0], clips[1], reportPath, sourceHash, inputHash);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save the R07 persistent ownership fixture.");
            EditorApplication.isPlaying = true;
        }

        private static void PrepareAuthorSurfaceFixture(string reportPath, string sourceHash, string inputHash)
        {
            const string sourceScene = "Assets/Scenes/FirstPerformanceVoid.unity";
            const string fixtureScene = "Assets/Production/Scenes/RemediationAuthorSurfaceValidation.unity";
            if (!AssetDatabase.LoadAssetAtPath<SceneAsset>(fixtureScene))
            {
                if (!AssetDatabase.CopyAsset(sourceScene, fixtureScene))
                    throw new InvalidOperationException("Could not copy the authored First Performance Void to the R08 author-surface fixture.");
                AssetDatabase.ImportAsset(fixtureScene, ImportAssetOptions.ForceSynchronousImport);
            }
            var scene = EditorSceneManager.OpenScene(fixtureScene, OpenSceneMode.Single);
            foreach (var controls in UnityEngine.Object.FindObjectsByType<FirstPerformanceVoidControls>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                controls.enabled = false;
            var firstContact = UnityEngine.Object.FindAnyObjectByType<FirstContactPerformance>();
            var actor = UnityEngine.Object.FindAnyObjectByType<SuccubusPerformer>();
            var player = UnityEngine.Object.FindAnyObjectByType<PlayerController>();
            var playback = actor != null ? actor.MotionSource : UnityEngine.Object.FindAnyObjectByType<MotionDriver>();
            var expression = AssetDatabase.LoadAssetAtPath<PerformerExpression>("Assets/Production/ValidationData/Day3Expression.asset");
            var program = AssetDatabase.LoadAssetAtPath<FunscriptMotionProgram>("Assets/motiondrive.funscript");
            var firstClip = AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath("4284b0d5624bc0f49a0463d2f8374245"));
            var secondClip = AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath("85425b705aedcf840ac41b53f1062cd1"));
            if (firstContact == null || actor == null || player == null || playback == null || expression == null || program == null || firstClip == null || secondClip == null)
                throw new InvalidOperationException("The R08 fixture is missing First Contact, actor/player/playback, expression, motion or speech references.");
            // The accepted First Contact choreography begins from a visible Lara. The source
            // Void is also used for dissolve acceptance and intentionally serializes Lara as
            // hidden, so make this copied validation composition's initial default explicit.
            if (!actor.IsRuntimeReady)
                actor.ConfigureInitialState(actor.InitialPose, actor.InitialExpression, actor.InitialExpressionIntensity, false);
            var runner = UnityEngine.Object.FindAnyObjectByType<RemediationAuthorSurfaceRunner>();
            if (runner == null)
            {
                var holder = new GameObject("Remediation Author Surface Runner");
                SceneManager.MoveGameObjectToScene(holder, scene);
                runner = holder.AddComponent<RemediationAuthorSurfaceRunner>();
            }
            runner.Configure(firstContact, actor, player, playback, expression, program, firstClip, secondClip,
                reportPath, sourceHash, inputHash);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save the R08 author-surface fixture.");
            EditorApplication.isPlaying = true;
        }

        private static void RunR11(Report report)
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            AssertR11(report, "sourceHashPresent", "non-empty source and assembly input hash", !string.IsNullOrWhiteSpace(report.sourceHash), report.sourceHash);
            AssertR11(report, "inputHashPresent", "non-empty serialized asset input hash", report.inputAssetHashes != null && report.inputAssetHashes.Length > 0
                && !string.IsNullOrWhiteSpace(report.inputAssetHashes[0]), report.inputAssetHashes != null && report.inputAssetHashes.Length > 0 ? report.inputAssetHashes[0] : "missing");

            string manifestPath = Argument("-remediationMoveManifest");
            if (string.IsNullOrWhiteSpace(manifestPath) || !File.Exists(manifestPath))
                throw new FileNotFoundException("R11 requires the exact R10 source-move manifest passed by the PowerShell runner.", manifestPath);
            R11Manifest manifest = JsonUtility.FromJson<R11Manifest>(File.ReadAllText(manifestPath));
            if (manifest == null || manifest.files == null || manifest.assemblyDefinitions == null || manifest.files.Length != 267)
                throw new InvalidDataException("R11 could not deserialize the complete 267-source R10 manifest.");

            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            CheckR11AssemblyFiles(report, manifest, projectRoot);
            CheckR11Assemblies(report, manifest);
            CheckR11GuidAndMoveResolution(report, manifest, projectRoot);
            CheckR11ScenesAndPrefabs(report);
            RunR11PlayerBuilds(report, projectRoot);
        }

        private static void CheckR11AssemblyFiles(Report report, R11Manifest manifest, string projectRoot)
        {
            var failures = new List<string>();
            foreach (R11AssemblyDefinition expected in manifest.assemblyDefinitions)
            {
                string path = Path.Combine(projectRoot, expected.path.Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(path)) { failures.Add(expected.path + " missing"); continue; }
                R11AssemblyDefinitionFile actual = JsonUtility.FromJson<R11AssemblyDefinitionFile>(File.ReadAllText(path));
                if (actual == null || actual.name != expected.name) { failures.Add(expected.path + " name mismatch"); continue; }
                if (!SameStrings(actual.references, expected.references)) failures.Add(expected.path + " references");
                if (!SameStrings(actual.includePlatforms, expected.includePlatforms)) failures.Add(expected.path + " includePlatforms");
                if (!SameStrings(actual.excludePlatforms, expected.excludePlatforms)) failures.Add(expected.path + " excludePlatforms");
                if (actual.allowUnsafeCode != expected.allowUnsafeCode || actual.overrideReferences != expected.overrideReferences
                    || actual.autoReferenced != expected.autoReferenced || actual.noEngineReferences != expected.noEngineReferences)
                    failures.Add(expected.path + " boolean importer settings");
                if (!SameStrings(actual.precompiledReferences, expected.precompiledReferences)) failures.Add(expected.path + " precompiledReferences");
                if (!SameStrings(actual.defineConstraints, expected.defineConstraints)) failures.Add(expected.path + " defineConstraints");
                if ((actual.versionDefines ?? Array.Empty<R11VersionDefine>()).Length != (expected.versionDefines ?? Array.Empty<R11VersionDefine>()).Length)
                    failures.Add(expected.path + " versionDefines");
            }
            foreach (R11AssemblyReference expected in manifest.assemblyReferences ?? Array.Empty<R11AssemblyReference>())
            {
                string path = Path.Combine(projectRoot, expected.path.Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(path)) { failures.Add(expected.path + " missing"); continue; }
                R11AssemblyReferenceFile actual = JsonUtility.FromJson<R11AssemblyReferenceFile>(File.ReadAllText(path));
                if (actual == null || actual.reference != expected.reference) failures.Add(expected.path + " reference target");
            }
            AssertR11(report, "asmdefAndAsmrefPayloadsMatchR10Exactly", "all five asmdefs and three asmrefs match their R10 JSON payloads",
                failures.Count == 0, "checked=" + (manifest.assemblyDefinitions.Length + (manifest.assemblyReferences ?? Array.Empty<R11AssemblyReference>()).Length)
                + "; failures=[" + string.Join("; ", failures) + "]");
        }

        private static bool SameStrings(string[] left, string[] right)
        {
            left = left ?? Array.Empty<string>();
            right = right ?? Array.Empty<string>();
            return left.Length == right.Length && !left.Except(right, StringComparer.Ordinal).Any() && !right.Except(left, StringComparer.Ordinal).Any();
        }

        private static void CheckR11Assemblies(Report report, R11Manifest manifest)
        {
            var byName = manifest.assemblyDefinitions.ToDictionary(item => item.name, item => item, StringComparer.Ordinal);
            var byGuid = manifest.assemblyDefinitions.ToDictionary(item => item.guid, item => item.name, StringComparer.OrdinalIgnoreCase);
            string[] requiredNames = { "DazPose.Runtime", "DazPose.Editor", "DazPose.Validation", "DazPose.Validation.Editor", "DazPose.Examples" };
            var missingAssemblies = new List<string>();
            var missingTypes = new List<string>();
            var actualEdges = new List<string>();
            var invalidEdges = new List<string>();
            var predefinedEdges = new List<string>();
            var allowed = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal)
            {
                { "DazPose.Runtime", new HashSet<string>(StringComparer.Ordinal) },
                { "DazPose.Examples", new HashSet<string>(new[] { "DazPose.Runtime" }, StringComparer.Ordinal) },
                { "DazPose.Editor", new HashSet<string>(new[] { "DazPose.Runtime", "DazPose.Validation", "DazPose.Examples" }, StringComparer.Ordinal) },
                { "DazPose.Validation", new HashSet<string>(new[] { "DazPose.Runtime", "DazPose.Examples" }, StringComparer.Ordinal) },
                { "DazPose.Validation.Editor", new HashSet<string>(new[] { "DazPose.Runtime", "DazPose.Validation", "DazPose.Editor", "DazPose.Examples" }, StringComparer.Ordinal) }
            };

            foreach (string name in requiredNames)
            {
                if (!byName.TryGetValue(name, out R11AssemblyDefinition definition))
                {
                    missingAssemblies.Add(name + " missing from manifest");
                    continue;
                }
                Assembly assembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(item => item.GetName().Name == name);
                if (assembly == null)
                {
                    missingAssemblies.Add(name + " not loaded after editor compilation");
                    continue;
                }
                foreach (AssemblyName reference in assembly.GetReferencedAssemblies())
                {
                    string target = reference.Name;
                    if (byName.ContainsKey(target))
                    {
                        actualEdges.Add(name + " -> " + target);
                        if (!allowed[name].Contains(target)) invalidEdges.Add(name + " -> " + target);
                    }
                    if (target == "Assembly-CSharp" || target == "Assembly-CSharp-Editor")
                        predefinedEdges.Add(name + " -> " + target);
                }

                foreach (string reference in definition.references ?? Array.Empty<string>())
                {
                    if (!reference.StartsWith("GUID:", StringComparison.Ordinal)) continue;
                    string guid = reference.Substring("GUID:".Length);
                    if (!byGuid.TryGetValue(guid, out string target)) continue;
                    if (!allowed[name].Contains(target)) invalidEdges.Add(name + " asmdef -> " + target);
                }
            }

            Assembly runtime = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(item => item.GetName().Name == "DazPose.Runtime");
            Assembly editor = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(item => item.GetName().Name == "DazPose.Editor");
            Assembly validation = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(item => item.GetName().Name == "DazPose.Validation");
            Assembly validationEditor = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(item => item.GetName().Name == "DazPose.Validation.Editor");
            Assembly examples = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(item => item.GetName().Name == "DazPose.Examples");
            if (runtime == null || runtime.GetType("DazPose.Performer.SuccubusPerformer", false) == null) missingTypes.Add("Runtime: SuccubusPerformer");
            if (editor == null || editor.GetType("DazPose.UnityValidation.WardrobeImporter", false) == null) missingTypes.Add("Editor: WardrobeImporter");
            if (validation == null || validation.GetType("DazPose.Validation.RemediationToeRunner", false) == null) missingTypes.Add("Validation: RemediationToeRunner");
            if (validationEditor == null || validationEditor.GetType("DazPose.Remediation.RemediationValidation", false) == null) missingTypes.Add("Validation.Editor: RemediationValidation");
            if (examples == null || examples.GetType("DazPose.FirstPerformanceVoid.FirstContactPerformance", false) == null) missingTypes.Add("Examples: FirstContactPerformance");
            Type reflectedCompiler = Type.GetType("DazPose.UnityValidation.WardrobeFitCompiler, DazPose.Editor", false);
            if (reflectedCompiler == null) missingTypes.Add("assembly-qualified WardrobeFitCompiler lookup did not resolve from DazPose.Editor");
            string[] runtimeFriends = GetInternalsVisibleTo(runtime);
            string[] editorFriends = GetInternalsVisibleTo(editor);
            string[] expectedRuntimeFriends = { "DazPose.Editor", "DazPose.Validation", "DazPose.Validation.Editor" };
            string[] expectedEditorFriends = { "DazPose.Validation.Editor", "Assembly-CSharp-Editor" };
            if (!SameStrings(runtimeFriends, expectedRuntimeFriends) || !SameStrings(editorFriends, expectedEditorFriends))
                missingTypes.Add("InternalsVisibleTo declarations do not match the fixed friend list");
            Type bridgeType = editor == null ? null : editor.GetType("DazPose.Daz3DConversion.DazPoseDtuConversionBridge", false);
            FieldInfo bridgeRegistration = bridgeType == null ? null : bridgeType.GetField("_converter", BindingFlags.NonPublic | BindingFlags.Static);
            if (bridgeRegistration == null || bridgeRegistration.GetValue(null) == null)
                missingTypes.Add("predefined Assembly-CSharp-Editor DAZ adapter did not register");
            if (actualEdges.Any(edge => edge.StartsWith("DazPose.Runtime -> ", StringComparison.Ordinal))) invalidEdges.Add("DazPose.Runtime must not reference another first-party assembly");
            if (actualEdges.Contains("DazPose.Editor -> DazPose.Validation.Editor")) invalidEdges.Add("DazPose.Editor must not reference DazPose.Validation.Editor");

            R11PackageAssembly vfxPackage = (manifest.packageAssemblies ?? Array.Empty<R11PackageAssembly>())
                .FirstOrDefault(item => item.assembly == "Unity.VisualEffectGraph.Runtime");
            R11AssemblyDefinition runtimeDefinition = byName.TryGetValue("DazPose.Runtime", out R11AssemblyDefinition runtimeRow) ? runtimeRow : null;
            bool declaredVfxReference = vfxPackage != null && runtimeDefinition != null
                && (runtimeDefinition.references ?? Array.Empty<string>()).Contains("GUID:" + vfxPackage.guid, StringComparer.Ordinal);
            bool compiledVfxReference = vfxPackage != null && runtime != null
                && runtime.GetReferencedAssemblies().Any(item => item.Name == vfxPackage.assembly);
            AssertR11(report, "runtimeVfxTypeAttributePackageEdgeIsExplicit",
                "DazPose.Runtime asmdef and compiled assembly reference Unity.VisualEffectGraph.Runtime for VFXTypeAttribute",
                declaredVfxReference && compiledVfxReference,
                "package=" + (vfxPackage == null ? "missing from manifest" : vfxPackage.assembly + " GUID:" + vfxPackage.guid)
                + "; declared=" + declaredVfxReference + "; compiled=" + compiledVfxReference);

            R11PackageAssembly corePackage = (manifest.packageAssemblies ?? Array.Empty<R11PackageAssembly>())
                .FirstOrDefault(item => item.assembly == "Unity.RenderPipelines.Core.Runtime");
            R11AssemblyDefinition editorDefinition = byName.TryGetValue("DazPose.Editor", out R11AssemblyDefinition editorRow) ? editorRow : null;
            bool declaredCoreReference = corePackage != null && editorDefinition != null
                && (editorDefinition.references ?? Array.Empty<string>()).Contains("GUID:" + corePackage.guid, StringComparer.Ordinal);
            bool compiledCoreReference = corePackage != null && editor != null
                && editor.GetReferencedAssemblies().Any(item => item.Name == corePackage.assembly);
            AssertR11(report, "editorVolumeTypesCorePackageEdgeIsExplicit",
                "DazPose.Editor asmdef and compiled assembly reference Unity.RenderPipelines.Core.Runtime for VolumeProfile/VolumeComponent",
                declaredCoreReference && compiledCoreReference,
                "package=" + (corePackage == null ? "missing from manifest" : corePackage.assembly + " GUID:" + corePackage.guid)
                + "; declared=" + declaredCoreReference + "; compiled=" + compiledCoreReference);

            AssertR11(report, "fiveNamedAssembliesCompileAndOwnExpectedTypes", string.Join(", ", requiredNames),
                missingAssemblies.Count == 0 && missingTypes.Count == 0,
                "missingAssemblies=[" + string.Join("; ", missingAssemblies) + "]; missingTypes=[" + string.Join("; ", missingTypes) + "]");
            AssertR11(report, "compiledAssemblyReferencesFollowDirectedBoundary", "compiled first-party references are within the contract graph; Runtime has no first-party edges",
                invalidEdges.Count == 0 && predefinedEdges.Count == 0,
                "actual=[" + string.Join("; ", actualEdges.Distinct().OrderBy(item => item)) + "]; invalid=[" + string.Join("; ", invalidEdges.Distinct())
                + "]; predefined=[" + string.Join("; ", predefinedEdges.Distinct()) + "]");
        }

        private static string[] GetInternalsVisibleTo(Assembly assembly)
        {
            if (assembly == null) return Array.Empty<string>();
            return assembly.GetCustomAttributes(false)
                .Where(attribute => attribute.GetType().FullName == "System.Runtime.CompilerServices.InternalsVisibleToAttribute")
                .Select(attribute => (string)attribute.GetType().GetProperty("AssemblyName").GetValue(attribute, null))
                .ToArray();
        }

        private static void CheckR11GuidAndMoveResolution(Report report, R11Manifest manifest, string projectRoot)
        {
            var failures = new List<string>();
            var guids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            Action<string, string> checkGuid = (guid, expectedPath) =>
            {
                if (string.IsNullOrWhiteSpace(guid) || !guids.Add(guid))
                {
                    failures.Add("missing or duplicate GUID " + (guid ?? "<null>") + " at " + expectedPath);
                    return;
                }
                string actualPath = AssetDatabase.GUIDToAssetPath(guid);
                if (!string.Equals(actualPath, expectedPath, StringComparison.Ordinal))
                    failures.Add(guid + " resolved to '" + actualPath + "' instead of '" + expectedPath + "'");
            };

            foreach (R11Source row in manifest.files)
            {
                string expected = row.destination.Replace('\\', '/');
                string absolute = Path.Combine(projectRoot, expected.Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(absolute)) failures.Add("moved source missing: " + expected);
                checkGuid(row.guid, expected);
                string prior = ManifestSourceAssetPath(row.source);
                if (!string.Equals(prior, expected, StringComparison.OrdinalIgnoreCase))
                {
                    string oldAbsolute = Path.Combine(projectRoot, prior.Replace('/', Path.DirectorySeparatorChar));
                    if (File.Exists(oldAbsolute)) failures.Add("stale moved source remains: " + prior);
                }
            }
            foreach (R11Source row in manifest.newFolderMetas ?? Array.Empty<R11Source>())
            {
                string expected = row.path.Replace('\\', '/');
                if (!AssetDatabase.IsValidFolder(expected)) failures.Add("planned folder missing: " + expected);
                checkGuid(row.guid, expected);
            }
            foreach (R11Source row in manifest.newSourceMetas ?? Array.Empty<R11Source>())
            {
                string expected = row.source.Replace('\\', '/');
                string absolute = Path.Combine(projectRoot, expected.Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(absolute)) failures.Add("new source missing: " + expected);
                checkGuid(row.guid, expected);
            }
            foreach (R11AssemblyDefinition row in manifest.assemblyDefinitions)
                checkGuid(row.guid, row.path.Replace('\\', '/'));
            foreach (R11AssemblyReference row in manifest.assemblyReferences ?? Array.Empty<R11AssemblyReference>())
                checkGuid(row.guid, row.path.Replace('\\', '/'));

            AssertR11(report, "sourceAndPlannedGuidsUniqueAndResolve", "all moved source metas and R10-planned metas resolve to their exact destinations with no duplicate GUIDs",
                failures.Count == 0, "checked=" + guids.Count + "; failures=[" + string.Join("; ", failures.Take(20)) + "]");
        }

        private static void CheckR11ScenesAndPrefabs(Report report)
        {
            string[] scenes =
            {
                "Assets/Scenes/FirstPerformanceVoid.unity",
                "Assets/Scenes/ClothingSetup.unity",
                "Assets/Production/Scenes/Day3CommandValidation.unity",
                "Assets/Production/Scenes/Day4PartnerContactValidation.unity",
                "Assets/Production/Scenes/Day5WardrobeValidation.unity"
            };
            var sceneFailures = new List<string>();
            foreach (string path in scenes)
            {
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null)
                {
                    sceneFailures.Add(path + " missing");
                    continue;
                }
                Scene loaded = default;
                try
                {
                    loaded = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                    if (!loaded.IsValid() || !loaded.isLoaded) sceneFailures.Add(path + " did not load");
                    else
                    {
                        int missing = CountMissingScripts(loaded.GetRootGameObjects());
                        if (missing != 0) sceneFailures.Add(path + " missingScripts=" + missing);
                    }
                }
                catch (Exception exception) { sceneFailures.Add(path + " load failed: " + exception.GetType().Name + ": " + exception.Message); }
                finally { if (loaded.IsValid() && loaded.isLoaded) EditorSceneManager.CloseScene(loaded, true); }
            }
            AssertR11(report, "fiveRepresentativeSavedScenesReloadWithoutMissingScripts", "all five authored scenes load additively with zero missing scripts",
                sceneFailures.Count == 0, "scenes=" + scenes.Length + "; failures=[" + string.Join("; ", sceneFailures) + "]");

            string[] prefabs =
            {
                "Assets/Production/Performers/Lara.prefab",
                "Assets/Production/Partners/Partner.prefab",
                "Assets/PartnerProof/PartnerCharacter.prefab"
            };
            var prefabFailures = new List<string>();
            foreach (string path in prefabs)
            {
                if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
                {
                    prefabFailures.Add(path + " missing");
                    continue;
                }
                GameObject contents = null;
                try
                {
                    contents = PrefabUtility.LoadPrefabContents(path);
                    if (contents == null) prefabFailures.Add(path + " returned no prefab root");
                    else
                    {
                        int missing = CountMissingScripts(new[] { contents });
                        if (missing != 0) prefabFailures.Add(path + " missingScripts=" + missing);
                    }
                }
                catch (Exception exception) { prefabFailures.Add(path + " load failed: " + exception.GetType().Name + ": " + exception.Message); }
                finally { if (contents != null) PrefabUtility.UnloadPrefabContents(contents); }
            }
            AssertR11(report, "laraAndPartnerPrefabsReloadWithoutMissingScripts", "Lara and both saved partner prefabs reload with zero missing scripts",
                prefabFailures.Count == 0, "prefabs=" + prefabs.Length + "; failures=[" + string.Join("; ", prefabFailures) + "]");
        }

        private static int CountMissingScripts(IEnumerable<GameObject> roots)
        {
            int total = 0;
            foreach (GameObject root in roots)
                if (root != null)
                    foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
                        total += GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject);
            return total;
        }

        private static void RunR11PlayerBuilds(Report report, string projectRoot)
        {
            string reportPath = Argument("-remediationReport");
            string runId = Path.GetFileName(Path.GetDirectoryName(reportPath));
            string assetRoot = "Assets/TestOutput/Remediation/R11";
            string productionScenePath = assetRoot + "/ProductionBoundaryBuild.unity";
            string validationScenePath = assetRoot + "/ValidationBoundaryBuild.unity";
            string sentinelPath = "Assets/DazPose/Validation/RemediationAssemblyBuildSentinel.cs";
            string sentinelAbsolute = Path.Combine(projectRoot, sentinelPath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(sentinelAbsolute)) throw new FileNotFoundException("The isolated R11 validation build sentinel was not staged before Unity started.", sentinelAbsolute);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            MonoScript sentinelAsset = AssetDatabase.LoadAssetAtPath<MonoScript>(sentinelPath);
            Type sentinelType = sentinelAsset == null ? null : sentinelAsset.GetClass();
            if (sentinelType == null || !typeof(MonoBehaviour).IsAssignableFrom(sentinelType))
                throw new InvalidOperationException("R11 could not resolve its isolated validation-only player sentinel script.");

            string symbolsBefore = PlayerSettings.GetScriptingDefineSymbolsForGroup(BuildTargetGroup.Standalone);
            string outputRoot = Path.Combine(projectRoot, "TestOutput", "Remediation", "R11", runId);
            string productionOutput = Path.Combine(outputRoot, "Production", "DazPoseBoundary.exe");
            string validationOutput = Path.Combine(outputRoot, "Validation", "DazPoseBoundary.exe");
            bool sceneFilesSaved = false;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(productionOutput));
                Directory.CreateDirectory(Path.GetDirectoryName(validationOutput));
                Scene productionScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                if (!EditorSceneManager.SaveScene(productionScene, productionScenePath))
                    throw new IOException("Could not save isolated production boundary build scene.");
                Scene validationScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var sentinel = new GameObject("R11 Validation Assembly Build Sentinel");
                sentinel.AddComponent(sentinelType);
                if (!EditorSceneManager.SaveScene(validationScene, validationScenePath))
                    throw new IOException("Could not save isolated validation boundary build scene.");
                sceneFilesSaved = true;
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);

                BuildReport production = BuildR11Player(new[] { productionScenePath }, productionOutput, Array.Empty<string>());
                var productionFiles = production.GetFiles();
                bool productionHasValidation = productionFiles != null && productionFiles.Any(file =>
                    string.Equals(Path.GetFileName(file.path), "DazPose.Validation.dll", StringComparison.OrdinalIgnoreCase));
                bool productionHasRuntime = productionFiles != null && productionFiles.Any(file =>
                    string.Equals(Path.GetFileName(file.path), "DazPose.Runtime.dll", StringComparison.OrdinalIgnoreCase));
                bool productionPassed = production.summary.result == BuildResult.Succeeded && production.summary.totalErrors == 0;
                AssertR11(report, "productionWindowsPlayerBuildWithoutValidationDefine", "Succeeded, zero errors, DazPose.Runtime present, DazPose.Validation absent",
                    productionPassed && productionHasRuntime && !productionHasValidation,
                    "result=" + production.summary.result + "; errors=" + production.summary.totalErrors + "; runtime=" + productionHasRuntime
                    + "; validation=" + productionHasValidation + "; output=" + productionOutput);

                BuildReport validation = BuildR11Player(new[] { validationScenePath }, validationOutput, new[] { "DAZPOSE_VALIDATION" });
                var validationFiles = validation.GetFiles();
                bool validationHasAssembly = validationFiles != null && validationFiles.Any(file =>
                    string.Equals(Path.GetFileName(file.path), "DazPose.Validation.dll", StringComparison.OrdinalIgnoreCase));
                bool validationPassed = validation.summary.result == BuildResult.Succeeded && validation.summary.totalErrors == 0;
                AssertR11(report, "validationWindowsPlayerBuildWithBuildOnlyDefine", "Succeeded, zero errors, DazPose.Validation player assembly present",
                    validationPassed && validationHasAssembly,
                    "result=" + validation.summary.result + "; errors=" + validation.summary.totalErrors + "; validation=" + validationHasAssembly
                    + "; output=" + validationOutput);
            }
            finally
            {
                string symbolsAfter = PlayerSettings.GetScriptingDefineSymbolsForGroup(BuildTargetGroup.Standalone);
                AssertR11(report, "validationDefineWasBuildOnly", "Standalone player scripting defines unchanged by R11",
                    string.Equals(symbolsBefore, symbolsAfter, StringComparison.Ordinal), "before='" + symbolsBefore + "'; after='" + symbolsAfter + "'");
                if (sceneFilesSaved)
                {
                    EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                    AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                }
            }
        }

        private static BuildReport BuildR11Player(string[] scenes, string outputPath, string[] extraDefines)
        {
            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = outputPath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development,
                extraScriptingDefines = extraDefines
            };
            return BuildPipeline.BuildPlayer(options);
        }

        private static string ManifestSourceAssetPath(string repositoryPath)
        {
            string normalized = (repositoryPath ?? string.Empty).Replace('\\', '/');
            int index = normalized.IndexOf("/Assets/", StringComparison.OrdinalIgnoreCase);
            if (index < 0) throw new InvalidDataException("Manifest source is outside the Unity Assets tree: " + repositoryPath);
            return normalized.Substring(index + 1);
        }

        private static void AssertR11(Report report, string name, string expected, bool passed, string actual)
        {
            report.assertions.Add(new Assertion { name = name, expected = expected, actual = actual ?? string.Empty, passed = passed });
        }

        private static void RunR12(Report report)
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            AssertR12(report, "ticketIsR12", "R12", string.Equals(report.ticket, "R12", StringComparison.Ordinal), report.ticket);
            AssertR12(report, "sourceHashPresent", "non-empty", !string.IsNullOrWhiteSpace(report.sourceHash), report.sourceHash);
            AssertR12(report, "inputHashPresent", "non-empty", report.inputAssetHashes.Length > 0 && !string.IsNullOrWhiteSpace(report.inputAssetHashes[0]),
                report.inputAssetHashes.Length > 0 ? report.inputAssetHashes[0] : string.Empty);

            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string outputRoot = Path.Combine(projectRoot, "TestOutput", "remediation");
            Directory.CreateDirectory(outputRoot);
            string manifestPath = Path.Combine(outputRoot, "R12-build-manifest.json");

            RemediationBuild.PreparedScenes scenes = RemediationBuild.PrepareScenes(report.sourceHash, report.inputAssetHashes[0]);
            RemediationBuild.BuildRecord validation = RemediationBuild.BuildValidation(
                Path.Combine(outputRoot, "validation-player"), report.sourceHash, report.inputAssetHashes[0], scenes.validationScene);
            RemediationBuild.BuildRecord production = RemediationBuild.BuildProduction(
                Path.Combine(outputRoot, "production-player"), report.sourceHash, report.inputAssetHashes[0], scenes.productionScene,
                scenes.productionBehaviourTypes);

            var manifest = new RemediationBuild.Manifest
            {
                schemaVersion = 1,
                capturedUtc = DateTime.UtcNow.ToString("O"),
                sourceHash = report.sourceHash,
                inputAssetHash = report.inputAssetHashes[0],
                scriptingBackend = PlayerSettings.GetScriptingBackend(BuildTargetGroup.Standalone).ToString(),
                authoredSourceScene = RemediationBuild.AuthoredSourceScene,
                authoredSourceSceneSha256Before = scenes.authoredSceneSha256Before,
                authoredSourceSceneSha256After = RemediationBuild.HashAsset(RemediationBuild.AuthoredSourceScene),
                validationScene = scenes.validationScene,
                productionScene = scenes.productionScene,
                validationSceneCamera = scenes.validationCamera,
                validationSceneBehaviourTypes = scenes.validationBehaviourTypes,
                productionSceneBehaviourTypes = scenes.productionBehaviourTypes,
                productionBoundaryPassed = scenes.productionBoundaryPassed,
                validation = validation,
                production = production
            };
            File.WriteAllText(manifestPath, JsonUtility.ToJson(manifest, true));
            report.r12BuildManifestPath = manifestPath;

            AssertR12(report, "sourceScenePreserved", "authored FirstPerformanceVoid bytes unchanged",
                string.Equals(manifest.authoredSourceSceneSha256Before, manifest.authoredSourceSceneSha256After, StringComparison.Ordinal),
                "before=" + manifest.authoredSourceSceneSha256Before + "; after=" + manifest.authoredSourceSceneSha256After);
            AssertR12(report, "remediationSceneSaved", RemediationBuild.ValidationScene,
                AssetDatabase.LoadAssetAtPath<SceneAsset>(scenes.validationScene) != null,
                scenes.validationScene + "; camera=" + scenes.validationCamera);
            AssertR12(report, "productionSmokeSceneSaved", RemediationBuild.ProductionScene,
                AssetDatabase.LoadAssetAtPath<SceneAsset>(scenes.productionScene) != null,
                scenes.productionScene);
            AssertR12(report, "validationBuildSucceededZeroErrors", "Succeeded, zero errors, DAZPOSE_VALIDATION build-only define",
                validation != null && validation.succeeded && validation.errors == 0 && validation.validationDefineOnly,
                validation != null ? validation.result + "; errors=" + validation.errors + "; warnings=" + validation.warnings
                    + "; defineOnly=" + validation.validationDefineOnly + "; exe=" + validation.executablePath : "missing build record");
            AssertR12(report, "productionBuildSucceededZeroErrors", "Succeeded, zero errors, no validation define",
                production != null && production.succeeded && production.errors == 0 && !production.validationDefineOnly,
                production != null ? production.result + "; errors=" + production.errors + "; warnings=" + production.warnings
                    + "; defineOnly=" + production.validationDefineOnly + "; exe=" + production.executablePath : "missing build record");
            AssertR12(report, "buildBackendMono2x", "existing Standalone Mono2x backend",
                string.Equals(manifest.scriptingBackend, ScriptingImplementation.Mono2x.ToString(), StringComparison.Ordinal),
                manifest.scriptingBackend + "; ProjectSettings was not changed by the build method");
            AssertR12(report, "buildSourceAndDependencyHashesRecorded", "both builds carry source/input/scene/dependency and managed file hashes",
                validation != null && production != null
                    && validation.sourceHash == report.sourceHash && production.sourceHash == report.sourceHash
                    && validation.inputAssetHash == report.inputAssetHashes[0] && production.inputAssetHash == report.inputAssetHashes[0]
                    && validation.sceneSha256 != "missing" && production.sceneSha256 != "missing"
                    && validation.sceneDependencySha256 != "missing" && production.sceneDependencySha256 != "missing"
                    && validation.managedFileHashes.Length > 0 && production.managedFileHashes.Length > 0,
                "validationSceneDeps=" + (validation != null ? validation.sceneDependencySha256 : "missing")
                    + "; productionSceneDeps=" + (production != null ? production.sceneDependencySha256 : "missing")
                    + "; managedFiles=" + (validation != null ? validation.managedFileHashes.Length : 0) + "/"
                    + (production != null ? production.managedFileHashes.Length : 0));
            AssertR12(report, "productionSceneHasNoAcceptanceComponents", "no DazPose.Validation component or acceptance harness in production scene",
                scenes.productionBoundaryPassed,
                string.Join(" | ", scenes.productionBehaviourTypes ?? Array.Empty<string>()));
            AssertR12(report, "buildManifestWritten", "R12 build and dependency manifest saved",
                File.Exists(manifestPath), manifestPath);
        }

        private static void AssertR12(Report report, string name, string expected, bool passed, string actual)
        {
            report.assertions.Add(new Assertion { name = name, expected = expected, actual = actual ?? string.Empty, passed = passed });
        }

        private static void RunR09(Report report, ref bool deferExitToPlayModeRunner)
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            AssertR09(report, "ticketIsR09", "R09", string.Equals(report.ticket, "R09", StringComparison.Ordinal), report.ticket);
            AssertR09(report, "sourceHashPresent", "non-empty", !string.IsNullOrWhiteSpace(report.sourceHash), report.sourceHash);
            AssertR09(report, "inputHashPresent", "non-empty", report.inputAssetHashes.Length > 0 && !string.IsNullOrWhiteSpace(report.inputAssetHashes[0]), report.inputAssetHashes.Length > 0 ? report.inputAssetHashes[0] : string.Empty);

            string runId = Path.GetFileName(Path.GetDirectoryName(Argument("-remediationReport") ?? string.Empty));
            if (string.IsNullOrWhiteSpace(runId)) throw new InvalidOperationException("R09 evidence report path does not identify a unique run directory.");
            string folder = "Assets/Content/Remediation/Folder With Spaces/" + runId;
            EnsureAssetFolder("Assets/Content");
            EnsureAssetFolder("Assets/Content/Remediation");
            EnsureAssetFolder("Assets/Content/Remediation/Folder With Spaces");
            EnsureAssetFolder(folder);
            string gesturePath = folder + "/FolderIndependentGesture.anim";
            string actionPath = folder + "/FolderIndependentAction.anim";
            string gestureAssetPath = folder + "/FolderIndependentGesture.asset";
            string actionAssetPath = folder + "/FolderIndependentAction.asset";
            string scenePath = "Assets/Production/Scenes/RemediationContentLocationValidation_" + runId + ".unity";

            try
            {
                AnimationClip sourceGesture = AssetDatabase.LoadAssetAtPath<AnimationClip>(
                    "Assets/DazPose/Generated/Gestures/Acceptance/RightHandWave_Gesture.anim");
                AnimationClip sourceAction = AssetDatabase.LoadAssetAtPath<AnimationClip>(
                    "Assets/DazPose/Generated/Actions/Acceptance/JumpForJoy_Action.anim");
                PerformerGesture sourceGestureAsset = AssetDatabase.LoadAssetAtPath<PerformerGesture>(
                    "Assets/DazPose/Generated/Gestures/Acceptance/RightHandWave.asset");
                PerformerAction sourceActionAsset = AssetDatabase.LoadAssetAtPath<PerformerAction>(
                    "Assets/DazPose/Generated/Actions/Acceptance/JumpForJoy.asset");
                AssertR09(report, "existingPreparedGestureAvailable", "prepared project-owned Gesture", sourceGestureAsset != null && sourceGesture != null, sourceGestureAsset != null && sourceGesture != null ? "found" : "missing");
                AssertR09(report, "existingPreparedActionAvailable", "prepared project-owned Action", sourceActionAsset != null && sourceAction != null, sourceActionAsset != null && sourceAction != null ? "found" : "missing");
                if (sourceGesture == null || sourceAction == null || sourceGestureAsset == null || sourceActionAsset == null)
                    throw new InvalidOperationException("R09 cannot find its existing prepared Gesture/Action source assets.");

                if (!AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(sourceGesture), gesturePath))
                    throw new IOException("Could not copy the prepared Gesture clip into the ordinary folder.");
                if (!AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(sourceAction), actionPath))
                    throw new IOException("Could not copy the prepared Action clip into the ordinary folder.");
                AssetDatabase.ImportAsset(gesturePath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                AssetDatabase.ImportAsset(actionPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                AnimationClip copiedGestureClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(gesturePath);
                AnimationClip copiedActionClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(actionPath);
                AssertR09(report, "gestureClipCopiedAsStandaloneAnim", "standalone .anim", copiedGestureClip != null && !AssetDatabase.IsSubAsset(copiedGestureClip), copiedGestureClip != null ? AssetDatabase.GetAssetPath(copiedGestureClip) : "missing/subasset");
                AssertR09(report, "actionClipCopiedAsStandaloneAnim", "standalone .anim", copiedActionClip != null && !AssetDatabase.IsSubAsset(copiedActionClip), copiedActionClip != null ? AssetDatabase.GetAssetPath(copiedActionClip) : "missing/subasset");
                if (copiedGestureClip == null || copiedActionClip == null)
                    throw new InvalidOperationException("Copied prepared clips failed to reload as standalone .anim assets.");

                PerformerGesture gesture = ScriptableObject.CreateInstance<PerformerGesture>();
                gesture.name = "FolderIndependentGesture";
                gesture.ConfigureInEditor(copiedGestureClip, sourceGestureAsset.BlendInSeconds, sourceGestureAsset.BlendOutSeconds);
                AssetDatabase.CreateAsset(gesture, gestureAssetPath);
                PerformerAction action = ScriptableObject.CreateInstance<PerformerAction>();
                action.name = "FolderIndependentAction";
                action.ConfigureInEditor(copiedActionClip,
                    CloneCurve(sourceActionAsset.RootX), CloneCurve(sourceActionAsset.RootY),
                    CloneCurve(sourceActionAsset.RootZ), CloneCurve(sourceActionAsset.RootYaw),
                    sourceActionAsset.DurationSeconds, sourceActionAsset.SourceAssetPath,
                    sourceActionAsset.BakeNotes, sourceActionAsset.BlendInSeconds, sourceActionAsset.BlendOutSeconds);
                AssetDatabase.CreateAsset(action, actionAssetPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                gesture = AssetDatabase.LoadAssetAtPath<PerformerGesture>(gestureAssetPath);
                action = AssetDatabase.LoadAssetAtPath<PerformerAction>(actionAssetPath);
                AssertR09(report, "ordinaryFolderAssetsReload", "Gesture and Action assets reload", gesture != null && action != null, gesture != null && action != null ? folder : "missing");
                if (gesture == null || action == null) throw new InvalidOperationException("Ordinary-folder authoring assets did not reload.");
                bool gestureReady = gesture.IsReady(out string gestureReason);
                bool actionReady = action.IsReady(out string actionReason);
                AssertR09(report, "ordinaryFolderGestureReady", "pass", gestureReady, gestureReady ? "ready" : gestureReason);
                AssertR09(report, "ordinaryFolderActionReady", "pass", actionReady, actionReady ? "ready" : actionReason);
                RunR09NegativeAssetChecks(report, sourceGesture, sourceAction, sourceGestureAsset, sourceActionAsset, folder);

                // The negative cases deliberately reimport malformed clips. Reacquire the
                // saved owner assets afterward so scene bindings never keep stale Unity
                // object handles across those imports.
                gesture = AssetDatabase.LoadAssetAtPath<PerformerGesture>(gestureAssetPath);
                action = AssetDatabase.LoadAssetAtPath<PerformerAction>(actionAssetPath);
                bool ownerAssetsSurviveNegativeReimports = gesture != null && action != null;
                AssertR09(report, "ordinaryFolderAssetsReloadAfterNegativeChecks", "both saved owner assets reload after malformed-clip imports",
                    ownerAssetsSurviveNegativeReimports,
                    ownerAssetsSurviveNegativeReimports ? gestureAssetPath + " | " + actionAssetPath : "Gesture=" + (gesture != null) + "; Action=" + (action != null));
                if (!ownerAssetsSurviveNegativeReimports)
                    throw new InvalidOperationException("R09 prepared owner assets did not reload after negative clip checks.");

                bool playerBodyIsEditorFree = HasNoUnguardedUnityEditorReference("Assets/DazPose/Runtime/Performer/PerformerGesture.cs")
                    && HasNoUnguardedUnityEditorReference("Assets/DazPose/Runtime/Performer/PerformerAction.cs");
                AssertR09(report, "playerUsesPrevalidatedSerializedMetadata", "UnityEditor references are excluded by UNITY_EDITOR guards", playerBodyIsEditorFree,
                    playerBodyIsEditorFree ? "AssetDatabase/AnimationUtility references occur only inside UNITY_EDITOR guards; player path uses common metadata." : "Found a UnityEditor-only API reference outside a UNITY_EDITOR guard.");
                SceneAsset sourceScene = AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Production/Scenes/Day4PartnerContactValidation.unity");
                AssertR09(report, "savedExecutionFixtureSourceAvailable", "Day4 saved contact scene", sourceScene != null, sourceScene != null ? "found" : "missing");
                if (sourceScene == null) throw new InvalidOperationException("R09's saved execution fixture source scene is missing.");
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) == null
                    && !AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(sourceScene), scenePath))
                    throw new IOException("Could not copy the saved Day4 fixture for R09 execution.");
                bool sceneWasPresent = AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) != null;
                var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                foreach (var oldRunner in UnityEngine.Object.FindObjectsByType<Day4RegressionRunner>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    oldRunner.enabled = false;
                SuccubusPerformer actorOwner = UnityEngine.Object.FindAnyObjectByType<SuccubusPerformer>(FindObjectsInactive.Include);
                PerformanceSceneBindings sceneBindings = UnityEngine.Object.FindAnyObjectByType<PerformanceSceneBindings>(FindObjectsInactive.Include);
                AssertR09(report, "savedFixtureActorAndBindings", "actor and explicit scene bindings", actorOwner != null && sceneBindings != null, actorOwner != null && sceneBindings != null ? "found" : "missing");
                if (actorOwner == null || sceneBindings == null) throw new InvalidOperationException("R09's saved execution fixture lacks actor/bindings.");

                // Opening a scene can unload unused serialized assets. Reload the
                // prepared owners only after that transition, immediately before binding.
                gesture = AssetDatabase.LoadAssetAtPath<PerformerGesture>(gestureAssetPath);
                action = AssetDatabase.LoadAssetAtPath<PerformerAction>(actionAssetPath);
                sceneBindings.Configure(actorOwner, sceneBindings.Player, sceneBindings.Playback, sceneBindings.Toys,
                    PerformerCapability.Gesture | PerformerCapability.Action);
                sceneBindings.DeclareContent(new[] { gesture }, new[] { action }, Array.Empty<PerformerSpell>(), Array.Empty<PerformerAura>());
                PerformerGesture[] declaredGestures = sceneBindings.DeclaredGestures;
                PerformerAction[] declaredActions = sceneBindings.DeclaredActions;
                bool declaredOwnerReferencesMatch = gesture != null && action != null
                    && declaredGestures.Length == 1 && declaredGestures[0] == gesture
                    && declaredActions.Length == 1 && declaredActions[0] == action;
                AssertR09(report, "ordinaryFolderReferencesCapturedByBindings", "one non-null Gesture and Action owner reference",
                    declaredOwnerReferencesMatch,
                    "gesture=" + (gesture != null ? AssetDatabase.GetAssetPath(gesture) : "null")
                    + ";declaredGestureCount=" + declaredGestures.Length
                    + ";declaredGesture=" + (declaredGestures.Length > 0 && declaredGestures[0] != null ? AssetDatabase.GetAssetPath(declaredGestures[0]) : "null")
                    + ";action=" + (action != null ? AssetDatabase.GetAssetPath(action) : "null")
                    + ";declaredActionCount=" + declaredActions.Length
                    + ";declaredAction=" + (declaredActions.Length > 0 && declaredActions[0] != null ? AssetDatabase.GetAssetPath(declaredActions[0]) : "null"));
                if (!declaredOwnerReferencesMatch)
                    throw new InvalidOperationException("R09 scene binding API did not retain the ordinary-folder Gesture/Action owner references.");
                BindingIssue[] preflight = sceneBindings.Validate(false);
                AssertR09(report, "ordinaryFolderScenePreflight", "Gesture and Action readiness pass under the saved owning asset references", preflight.Length == 0,
                    preflight.Length == 0 ? "ready" : string.Join(" | ", preflight.Select(issue => issue.ToString())));
                if (preflight.Length != 0) throw new InvalidOperationException("R09 ordinary-folder fixture preflight failed.");
                var runner = UnityEngine.Object.FindAnyObjectByType<RemediationContentLocationRunner>(FindObjectsInactive.Include);
                if (runner == null)
                {
                    var holder = new GameObject("Remediation Content Location Runner");
                    SceneManager.MoveGameObjectToScene(holder, scene);
                    runner = holder.AddComponent<RemediationContentLocationRunner>();
                }
                runner.Configure(actorOwner, sceneBindings, gesture, action,
                    Argument("-remediationReport"), report.sourceHash,
                    report.inputAssetHashes.Length > 0 ? report.inputAssetHashes[0] : string.Empty,
                    JsonUtility.ToJson(report));
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save the R09 ordinary-folder execution scene.");
                AssertR09(report, "savedExecutionSceneCreatedWithoutOverwriting", "saved execution scene", AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) != null, sceneWasPresent ? "existing scene updated in isolated project" : scenePath);
                deferExitToPlayModeRunner = true;
                EditorApplication.isPlaying = true;
                return;

            }
            catch
            {
                AssetDatabase.DeleteAsset(gestureAssetPath);
                AssetDatabase.DeleteAsset(actionAssetPath);
                AssetDatabase.DeleteAsset(gesturePath);
                AssetDatabase.DeleteAsset(actionPath);
                throw;
            }
        }

        private static void RunR09NegativeAssetChecks(Report report, AnimationClip sourceGestureClip,
            AnimationClip sourceActionClip, PerformerGesture sourceGesture, PerformerAction sourceAction, string folder)
        {
            CheckGestureNegative(report, "legacyGestureRejected", "Legacy", sourceGestureClip, sourceGesture, folder, clip => clip.legacy = true);
            CheckGestureNegative(report, "loopingGestureRejected", "looping", sourceGestureClip, sourceGesture, folder, clip =>
            {
                AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
                settings.loopTime = true;
                AnimationUtility.SetAnimationClipSettings(clip, settings);
                clip.wrapMode = WrapMode.Loop;
            });
            CheckGestureNegative(report, "additiveReferencePoseRejected", "reference pose", sourceGestureClip, sourceGesture, folder, clip =>
            {
                AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
                settings.hasAdditiveReferencePose = false;
                settings.additiveReferencePoseClip = null;
                settings.additiveReferencePoseTime = 0f;
                AnimationUtility.SetAnimationClipSettings(clip, settings);
            });
            CheckGestureNegative(report, "emptyGestureRejected", "nonempty", sourceGestureClip, sourceGesture, folder, clip =>
            {
                foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(clip)) AnimationUtility.SetEditorCurve(clip, binding, null);
                foreach (EditorCurveBinding binding in AnimationUtility.GetObjectReferenceCurveBindings(clip)) AnimationUtility.SetObjectReferenceCurve(clip, binding, null);
                AnimationUtility.SetAnimationEvents(clip, Array.Empty<AnimationEvent>());
            });
            CheckGestureNegative(report, "nonfiniteGestureKeyRejected", "non-finite", sourceGestureClip, sourceGesture, folder, clip =>
            {
                string path = AssetDatabase.GetAssetPath(clip);
                string yaml = File.ReadAllText(Path.Combine(Directory.GetParent(Application.dataPath).FullName, path));
                Match key = Regex.Match(yaml, @"(?m)^(\s*value:\s*)([-+]?(?:\d+(?:\.\d*)?|\.\d+)(?:[eE][-+]?\d+)?)(\s*)$");
                if (!key.Success) throw new InvalidOperationException("Could not identify a serialized numeric animation key to corrupt.");
                yaml = yaml.Substring(0, key.Index) + key.Groups[1].Value + ".nan" + key.Groups[3].Value + yaml.Substring(key.Index + key.Length);
                File.WriteAllText(Path.Combine(Directory.GetParent(Application.dataPath).FullName, path), yaml);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            });
            CheckGestureNegative(report, "nonskeletalGestureCurveRejected", "non-skeletal", sourceGestureClip, sourceGesture, folder, clip =>
            {
                var curve = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(clip.length, 1f));
                string path = AssetDatabase.GetAssetPath(clip);
                string absolutePath = Path.Combine(Directory.GetParent(Application.dataPath).FullName, path);
                string yaml = File.ReadAllText(absolutePath);
                Match pathMatch = Regex.Match(yaml, @"(?m)^    path: (Genesis8Female/hip[^\r\n]*)$");
                if (!pathMatch.Success) throw new InvalidOperationException("Could not find the prepared skeletal path for nonskeletal curve injection. First path: " + string.Join(" | ", yaml.Split('\n').Where(line => line.Contains("path: Genesis")).Take(2)));
                string body = "  m_FloatCurves:\n  - curve:\n      m_Curve:\n      - serializedVersion: 3\n        time: 0\n        value: 0\n      - serializedVersion: 3\n        time: 1\n        value: 1\n    attribute: m_LocalEulerAnglesRaw.x\n    path: " + pathMatch.Groups[1].Value + "\n    classID: 4\n    script: {fileID: 0}\n    flags: 0\n";
                yaml = yaml.Replace("  m_FloatCurves: []", body)
                    .Replace("    m_HasAdditiveReferencePose: 1", "    m_HasAdditiveReferencePose: 1");
                File.WriteAllText(absolutePath, yaml);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                AssertR09(report, "nonskeletalCurveFixtureCreated", "nonskeletal curve serialized in fixture", File.ReadAllText(absolutePath).Contains("attribute: m_LocalEulerAnglesRaw.x"), "curve serialized");
            });
            CheckGestureNegative(report, "gestureEventRejected", "object-reference curves or animation events", sourceGestureClip, sourceGesture, folder, clip =>
                AnimationUtility.SetAnimationEvents(clip, new[] { new AnimationEvent { time = 0f, functionName = "R09InvalidEvent" } }));
            CheckGestureNegative(report, "gestureObjectReferenceCurveRejected", "object-reference curves or animation events", sourceGestureClip, sourceGesture, folder, clip =>
            {
                var binding = EditorCurveBinding.PPtrCurve("Genesis8Female/hip/abdomenLower/abdomenUpper/chestLower/chestUpper/rCollar/rShldrBend", typeof(GameObject), "m_IsActive");
                AnimationUtility.SetObjectReferenceCurve(clip, binding,
                    new[] { new ObjectReferenceKeyframe { time = 0f, value = null }, new ObjectReferenceKeyframe { time = clip.length, value = null } });
                EditorUtility.SetDirty(clip);
                AssetDatabase.SaveAssets();
                int curveCount = AnimationUtility.GetObjectReferenceCurveBindings(clip).Length;
                if (curveCount == 0)
                {
                    string path = AssetDatabase.GetAssetPath(clip);
                    string absolutePath = Path.Combine(Directory.GetParent(Application.dataPath).FullName, path);
                    string yaml = File.ReadAllText(absolutePath);
                    string[] parts = yaml.Split(new[] { "  m_SampleRate:" }, 2, StringSplitOptions.None);
                    if (parts.Length != 2) throw new InvalidOperationException("Could not locate AnimationClip object curve serialization section.");
                    parts[0] = parts[0].Replace("  m_PPtrCurves: []", "  m_PPtrCurves:\n  - curve:\n      m_Curve:\n      - time: 0\n        value: {fileID: 0}\n      - time: 1.4\n        value: {fileID: 0}\n    attribute: m_IsActive\n    path: Genesis8Female/hip/abdomenLower/abdomenUpper/chestLower/chestUpper/rCollar/rShldrBend\n    classID: 1\n    script: {fileID: 0}");
                    parts[0] = parts[0].Replace("    pptrCurveMapping: []", "    pptrCurveMapping:\n    - {fileID: 0}\n    - {fileID: 0}");
                    File.WriteAllText(absolutePath, parts[0] + "  m_SampleRate:" + parts[1]);
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                    clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                    curveCount = AnimationUtility.GetObjectReferenceCurveBindings(clip).Length;
                }
                AssertR09(report, "objectReferenceCurveFixtureCreated", "object-reference curve appears", curveCount > 0, "count=" + curveCount);
            });
            CheckActionNegative(report, "legacyActionClipRejected", sourceActionClip, sourceAction, folder, clip => clip.legacy = true,
                action => !action.IsReady(out string reason) && reason.IndexOf("non-Legacy", StringComparison.OrdinalIgnoreCase) >= 0);
            CheckActionNegative(report, "loopingActionClipRejected", sourceActionClip, sourceAction, folder, clip =>
            {
                AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
                settings.loopTime = true;
                AnimationUtility.SetAnimationClipSettings(clip, settings);
                clip.wrapMode = WrapMode.Loop;
            }, action => !action.IsReady(out string reason) && reason.IndexOf("nonempty, non-looping", StringComparison.OrdinalIgnoreCase) >= 0);
            CheckActionNegative(report, "emptyActionClipRejected", sourceActionClip, sourceAction, folder, clip =>
            {
                foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(clip)) AnimationUtility.SetEditorCurve(clip, binding, null);
                foreach (EditorCurveBinding binding in AnimationUtility.GetObjectReferenceCurveBindings(clip)) AnimationUtility.SetObjectReferenceCurve(clip, binding, null);
                AnimationUtility.SetAnimationEvents(clip, Array.Empty<AnimationEvent>());
            }, action => !action.IsReady(out string reason) && reason.IndexOf("nonempty", StringComparison.OrdinalIgnoreCase) >= 0);
            CheckActionNegative(report, "nonfiniteActionClipKeyRejected", sourceActionClip, sourceAction, folder, clip =>
            {
                string path = AssetDatabase.GetAssetPath(clip);
                string absolutePath = Path.Combine(Directory.GetParent(Application.dataPath).FullName, path);
                string yaml = File.ReadAllText(absolutePath);
                Match key = Regex.Match(yaml, @"(?m)^(\s*value:\s*)([-+]?(?:\d+(?:\.\d*)?|\.\d+)(?:[eE][-+]?\d+)?)(\s*)$");
                if (!key.Success) throw new InvalidOperationException("Could not identify a serialized numeric Action animation key to corrupt.");
                yaml = yaml.Substring(0, key.Index) + key.Groups[1].Value + ".nan" + key.Groups[3].Value + yaml.Substring(key.Index + key.Length);
                File.WriteAllText(absolutePath, yaml);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            }, action => !action.IsReady(out string reason) && reason.IndexOf("non-finite", StringComparison.OrdinalIgnoreCase) >= 0);
            CheckActionNegative(report, "nonskeletalActionCurveRejected", sourceActionClip, sourceAction, folder, clip =>
            {
                string path = AssetDatabase.GetAssetPath(clip);
                string absolutePath = Path.Combine(Directory.GetParent(Application.dataPath).FullName, path);
                string yaml = File.ReadAllText(absolutePath);
                Match bodyPath = Regex.Match(yaml, @"(?m)^    path: ([^\r\n]+)$");
                if (!bodyPath.Success) throw new InvalidOperationException("Could not locate a body clip path for nonskeletal Action curve injection.");
                string body = "  m_FloatCurves:\n  - curve:\n      m_Curve:\n      - serializedVersion: 3\n        time: 0\n        value: 0\n      - serializedVersion: 3\n        time: 1\n        value: 1\n    attribute: m_LocalEulerAnglesRaw.x\n    path: " + bodyPath.Groups[1].Value + "\n    classID: 4\n    script: {fileID: 0}\n    flags: 0\n";
                yaml = yaml.Replace("  m_FloatCurves: []", body);
                File.WriteAllText(absolutePath, yaml);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            }, action => !action.IsReady(out string reason) && reason.IndexOf("root or non-skeletal", StringComparison.OrdinalIgnoreCase) >= 0);
            CheckActionNegative(report, "actionEventRejected", sourceActionClip, sourceAction, folder, clip =>
                AnimationUtility.SetAnimationEvents(clip, new[] { new AnimationEvent { time = 0f, functionName = "R09InvalidEvent" } }),
                action => !action.IsReady(out string reason) && reason.IndexOf("events", StringComparison.OrdinalIgnoreCase) >= 0);
            CheckActionNegative(report, "actionObjectReferenceCurveRejected", sourceActionClip, sourceAction, folder, clip =>
            {
                var binding = EditorCurveBinding.PPtrCurve("Genesis8Female", typeof(GameObject), "m_IsActive");
                AnimationUtility.SetObjectReferenceCurve(clip, binding, new[] { new ObjectReferenceKeyframe { time = 0f, value = null }, new ObjectReferenceKeyframe { time = clip.length, value = null } });
                EditorUtility.SetDirty(clip);
                AssetDatabase.SaveAssets();
                int count = AnimationUtility.GetObjectReferenceCurveBindings(clip).Length;
                if (count == 0)
                {
                    string path = AssetDatabase.GetAssetPath(clip);
                    string absolutePath = Path.Combine(Directory.GetParent(Application.dataPath).FullName, path);
                    string yaml = File.ReadAllText(absolutePath);
                    string[] parts = yaml.Split(new[] { "  m_SampleRate:" }, 2, StringSplitOptions.None);
                    if (parts.Length != 2) throw new InvalidOperationException("Could not locate serialized Action object curve metadata.");
                    parts[0] = parts[0].Replace("  m_PPtrCurves: []", "  m_PPtrCurves:\n  - curve:\n      m_Curve:\n      - time: 0\n        value: {fileID: 0}\n      - time: 1\n        value: {fileID: 0}\n    attribute: m_IsActive\n    path: Genesis8Female\n    classID: 1\n    script: {fileID: 0}");
                    parts[0] = parts[0].Replace("    pptrCurveMapping: []", "    pptrCurveMapping:\n    - {fileID: 0}\n    - {fileID: 0}");
                    File.WriteAllText(absolutePath, parts[0] + "  m_SampleRate:" + parts[1]);
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                    clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                    count = AnimationUtility.GetObjectReferenceCurveBindings(clip).Length;
                }
                AssertR09(report, "actionObjectReferenceCurveFixtureCreated", "object-reference curve appears", count > 0, "count=" + count);
            }, action => !action.IsReady(out string reason) && reason.IndexOf("object references or events", StringComparison.OrdinalIgnoreCase) >= 0);

            PerformerAction invalidTrajectory = ScriptableObject.CreateInstance<PerformerAction>();
            invalidTrajectory.name = "R09 invalid root trajectory";
            AnimationCurve invalidY = CloneCurve(sourceAction.RootY);
            Keyframe[] yKeys = invalidY.keys;
            yKeys[yKeys.Length - 1].value = 1f;
            invalidY.keys = yKeys;
            invalidTrajectory.ConfigureInEditor(sourceActionClip, CloneCurve(sourceAction.RootX), invalidY,
                    CloneCurve(sourceAction.RootZ), CloneCurve(sourceAction.RootYaw), sourceAction.DurationSeconds,
                    sourceAction.SourceAssetPath, sourceAction.BakeNotes, sourceAction.BlendInSeconds, sourceAction.BlendOutSeconds);
            bool actionRejected = !invalidTrajectory.IsReady(out string actionReason)
                && actionReason.IndexOf("ends", StringComparison.OrdinalIgnoreCase) >= 0;
            AssertR09(report, "invalidActionRootTrajectoryRejected", "trajectory endpoint rejected", actionRejected, actionRejected ? actionReason : "unexpectedly accepted: " + actionReason);
            UnityEngine.Object.DestroyImmediate(invalidTrajectory);
        }

        private static EditorCurveBinding AnimationCurveBindingForFirstSkeletal(AnimationClip clip) =>
            AnimationUtility.GetCurveBindings(clip).FirstOrDefault(binding => binding.type == typeof(Transform)
                && binding.propertyName == "m_LocalPosition.x");

        private static void CheckActionNegative(Report report, string assertionName, AnimationClip sourceClip,
            PerformerAction sourceAction, string folder, Action<AnimationClip> corrupt,
            Func<PerformerAction, bool> rejectsForCause)
        {
            string path = folder + "/" + assertionName + ".anim";
            PerformerAction probe = null;
            try
            {
                if (!AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(sourceClip), path))
                    throw new IOException("Could not create isolated negative clip copy " + path);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                if (clip == null) throw new InvalidOperationException("Negative clip copy did not import: " + path);
                corrupt(clip);
                probe = ScriptableObject.CreateInstance<PerformerAction>();
                probe.ConfigureInEditor(clip, CloneCurve(sourceAction.RootX), CloneCurve(sourceAction.RootY),
                    CloneCurve(sourceAction.RootZ), CloneCurve(sourceAction.RootYaw), sourceAction.DurationSeconds,
                    sourceAction.SourceAssetPath, sourceAction.BakeNotes, sourceAction.BlendInSeconds, sourceAction.BlendOutSeconds);
                bool rejected = rejectsForCause(probe);
                probe.IsReady(out string reason);
                AssertR09(report, assertionName, "rejected for source clip defect", rejected, rejected ? reason : "unexpectedly accepted or wrong reason: " + reason);
            }
            finally
            {
                if (probe != null) UnityEngine.Object.DestroyImmediate(probe);
                AssetDatabase.DeleteAsset(path);
                AssetDatabase.DeleteAsset(path + ".meta");
            }
        }

        private static void CheckGestureNegative(Report report, string assertionName, string expectedReason,
            AnimationClip sourceClip, PerformerGesture sourceGesture, string folder, Action<AnimationClip> corrupt)
        {
            string path = folder + "/" + assertionName + ".anim";
            PerformerGesture probe = null;
            try
            {
                if (!AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(sourceClip), path))
                    throw new IOException("Could not create isolated negative clip copy " + path);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                if (clip == null) throw new InvalidOperationException("Negative clip copy did not import: " + path);
                corrupt(clip);
                probe = ScriptableObject.CreateInstance<PerformerGesture>();
                probe.ConfigureInEditor(clip, sourceGesture.BlendInSeconds, sourceGesture.BlendOutSeconds);
                bool ready = probe.IsReady(out string reason);
                bool rejected = !ready && reason.IndexOf(expectedReason, StringComparison.OrdinalIgnoreCase) >= 0;
                string sampledCurve = AnimationUtility.GetEditorCurve(clip, AnimationCurveBindingForFirstSkeletal(clip))?.keys[0].value.ToString() ?? "none";
                if (!rejected && !ready && assertionName == "nonskeletalGestureCurveRejected")
                {
                    AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
                    settings.hasAdditiveReferencePose = true;
                    settings.additiveReferencePoseClip = null;
                    settings.additiveReferencePoseTime = 0f;
                    AnimationUtility.SetAnimationClipSettings(clip, settings);
                    ready = probe.IsReady(out reason);
                    rejected = !ready && reason.IndexOf(expectedReason, StringComparison.OrdinalIgnoreCase) >= 0;
                }
                AssertR09(report, assertionName, "rejected for " + expectedReason, rejected,
                    rejected ? reason : "ready=" + ready + "; reason=" + reason + "; firstSkeletalKey=" + sampledCurve);
            }
            finally
            {
                if (probe != null) UnityEngine.Object.DestroyImmediate(probe);
                AssetDatabase.DeleteAsset(path);
                AssetDatabase.DeleteAsset(path + ".meta");
            }
        }

        private static bool HasNoUnguardedUnityEditorReference(string assetPath)
        {
            string absolutePath = Path.Combine(Directory.GetParent(Application.dataPath).FullName,
                assetPath.Replace('/', Path.DirectorySeparatorChar));
            bool editorGuard = false;
            var guards = new Stack<bool>();
            foreach (string line in File.ReadAllLines(absolutePath))
            {
                string trimmed = line.Trim();
                if (trimmed.StartsWith("#if ", StringComparison.Ordinal))
                {
                    bool exactEditorGuard = string.Equals(trimmed, "#if UNITY_EDITOR", StringComparison.Ordinal);
                    guards.Push(editorGuard);
                    editorGuard = editorGuard || exactEditorGuard;
                }
                else if (trimmed.StartsWith("#endif", StringComparison.Ordinal))
                {
                    editorGuard = guards.Count > 0 && guards.Pop();
                }
                else if ((line.Contains("UnityEditor") || line.Contains("AssetDatabase") || line.Contains("AnimationUtility")) && !editorGuard)
                    return false;
            }
            return true;
        }

        private static void AssertR09(Report report, string name, string expected, bool passed, string actual)
        {
            report.assertions.Add(new Assertion { name = name, expected = expected, actual = actual ?? string.Empty, passed = passed });
            if (!passed) throw new InvalidOperationException(name + " failed: " + actual);
        }

        private static void EnsureAssetFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (string.IsNullOrEmpty(parent)) throw new InvalidOperationException("Invalid asset folder path: " + path);
            EnsureAssetFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        private static AnimationCurve CloneCurve(AnimationCurve source) => source == null ? null : new AnimationCurve(source.keys)
        { preWrapMode = source.preWrapMode, postWrapMode = source.postWrapMode };

        private static R03CompileResult CompileR03(WardrobePreset preset, WardrobeConfiguration configuration,
            SkinnedMeshRenderer body, string outputRoot)
        {
            var compilerType = Type.GetType("DazPose.UnityValidation.WardrobeFitCompiler, DazPose.Editor");
            if (compilerType == null) throw new InvalidOperationException("Editor WardrobeFitCompiler type is unavailable.");
            var method = compilerType.GetMethod("Compile", BindingFlags.Public | BindingFlags.Static);
            if (method == null) throw new InvalidOperationException("Editor WardrobeFitCompiler.Compile method is unavailable.");
            object result = method.Invoke(null, new object[] { preset, configuration, body, outputRoot, true });
            var resultType = result.GetType();
            int needs = (int)resultType.GetField("needsFitStates", BindingFlags.Public | BindingFlags.Instance).GetValue(result);
            var states = (WardrobeFitState[])resultType.GetField("fitStates", BindingFlags.Public | BindingFlags.Instance).GetValue(result);
            if (needs != 0 || states == null || states.Any(state => state == null || state.Status != WardrobeFitStatus.Validated))
                throw new InvalidOperationException("R03 fit generation returned non-validated states: " + needs);
            return new R03CompileResult
            {
                validatedStates = (int)resultType.GetField("validatedStates", BindingFlags.Public | BindingFlags.Instance).GetValue(result),
                reachableMasks = (int[])resultType.GetField("reachableMasks", BindingFlags.Public | BindingFlags.Instance).GetValue(result)
            };
        }

        private static string Argument(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i + 1 < args.Length; i++) if (args[i] == name) return args[i + 1].Trim('"');
            return null;
        }

        private static void Assert(Report report, string name, string expected, string actual)
        {
            report.assertions.Add(new Assertion { name = name, expected = expected, actual = actual ?? string.Empty, passed = !string.IsNullOrEmpty(actual) });
            if (report.assertions[report.assertions.Count - 1].passed == false) throw new InvalidOperationException(name + " failed.");
        }

        private static void AssertFile(Report report, string name, string path)
        {
            bool passed = File.Exists(path);
            report.assertions.Add(new Assertion { name = name, expected = "existing file", actual = path, passed = passed });
            if (!passed) throw new FileNotFoundException(name, path);
        }

        private static void AssertAsset(Report report, string name, string path)
        {
            string guid = AssetDatabase.AssetPathToGUID(path);
            bool passed = !string.IsNullOrEmpty(guid);
            report.assertions.Add(new Assertion { name = name, expected = path, actual = guid ?? string.Empty, passed = passed });
            if (!passed) throw new InvalidOperationException(name + " missing at " + path);
        }

        private static void AssertAnyAsset(Report report, string name, string filter)
        {
            string[] guids = AssetDatabase.FindAssets(filter);
            bool passed = guids != null && guids.Length > 0;
            report.assertions.Add(new Assertion { name = name, expected = filter, actual = passed ? guids.Length.ToString() : "0", passed = passed });
            if (!passed) throw new InvalidOperationException(name + " missing for " + filter);
        }
    }
}
#endif
