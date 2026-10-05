using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DazPose.Performer;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DazPose.UnityValidation
{
    public static class WardrobeRuntimeFixtureBuilder
    {
        private const string CandidateScene = "Assets/TestData/LaraCandidate/FirstPerformanceVoidLaraCandidate.unity";
        private const string CandidateCatalog = "Assets/Wardrobe/WardrobeCatalog.asset";
        private const string ConfigPath = "Assets/Wardrobe/WardrobeConfiguration.asset";
        private const string CanonicalBody = "Assets/TestData/LaraCandidate/LaraBody.asset";

        public static void StartRuntimeGate()
        {
            EnsureFolder("Assets/TestData/WardrobeRuntime/Validation");
            string suffix = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
            string catalogPath = "Assets/TestData/WardrobeRuntime/Validation/Catalog-" + suffix + ".asset";
            string configPath = "Assets/TestData/WardrobeRuntime/Validation/Configuration-" + suffix + ".asset";
            string maidPath = "Assets/TestData/WardrobeRuntime/Validation/MaidPreset-" + suffix + ".asset";
            string fixturePath = "Assets/TestData/WardrobeRuntime/Validation/Runtime-" + suffix + ".unity";
            EditorSceneManager.OpenScene(CandidateScene, OpenSceneMode.Single);
            var sourceCatalog = AssetDatabase.LoadAssetAtPath<WardrobeCatalog>(CandidateCatalog);
            var sourceConfig = AssetDatabase.LoadAssetAtPath<WardrobeConfiguration>(ConfigPath);
            if (sourceCatalog == null || sourceConfig == null) throw new InvalidDataException("E2 catalog/configuration is missing.");
            var performer = UnityEngine.Object.FindAnyObjectByType<SuccubusPerformer>();
            var canonical = AssetDatabase.LoadAssetAtPath<Mesh>(CanonicalBody);
            var body = UnityEngine.Object.FindObjectsByType<SkinnedMeshRenderer>(FindObjectsInactive.Include)
                .FirstOrDefault(r => r.sharedMesh == canonical);
            if (performer == null || body == null) throw new InvalidDataException("Canonical performer/body not found in runtime fixture scene.");

            var sourcePresets = sourceCatalog.Presets;
            var sourceMaid = sourcePresets.Single(p => p.PresetId == "third-outfit");
            var assignments = new List<WardrobeConfiguration.PieceAssignment>();
            foreach (var preset in sourcePresets)
            foreach (var part in preset.Parts)
            {
                var piece = preset.Package.pieces.Single(p => p.id == part.sourcePieceId);
                int layer = preset.PresetId == sourceMaid.PresetId
                    ? string.Equals(piece.role, "hosiery", StringComparison.OrdinalIgnoreCase) ? 0
                    : string.Equals(piece.role, "footwear", StringComparison.OrdinalIgnoreCase) ? 2 : 1
                    : 0;
                assignments.Add(new WardrobeConfiguration.PieceAssignment
                    { presetId = preset.PresetId, sourcePieceId = part.sourcePieceId, layer = layer });
            }
            var config = UnityEngine.Object.Instantiate(sourceConfig);
            config.name = "Wardrobe Runtime Validation Configuration";
            config.ConfigureGenerated(assignments.ToArray());
            config.CommitRevision("validation-fixture-only", new[] { "runtime-fixture" });
            AssetDatabase.CreateAsset(config, configPath);

            var compiled = WardrobeFitCompiler.Compile(sourceMaid, config, body,
                "Assets/TestData/WardrobeRuntime/Validation/Compiled", false);
            if (compiled.needsFitStates != 0 || compiled.reachableMasks.Length != 4)
                throw new InvalidDataException("Technical maid layer fixture did not compile every expected state: " +
                    string.Join(";", compiled.issues));
            var maid = UnityEngine.Object.Instantiate(sourceMaid);
            maid.name = "Wardrobe Runtime Validation Maid Preset";
            maid.ConfigureGenerated(sourceMaid.PresetId, sourceMaid.DisplayName, sourceMaid.CharacterSignature,
                sourceMaid.ConfigurationRevision, sourceMaid.Package,
                sourceMaid.Parts.Select(part =>
                {
                    int layer = assignments.Single(a => a.presetId == sourceMaid.PresetId && a.sourcePieceId == part.sourcePieceId).layer;
                    return new WardrobePreset.Part { sourcePieceId = part.sourcePieceId, layer = layer,
                        requiredOwnerPieceIds = part.requiredOwnerPieceIds };
                }).ToArray(), sourceMaid.Aliases, sourceMaid.HairAction, sourceMaid.HairId, sourceMaid.Footwear);
            maid.SetGeneratedFitStates(compiled.fitStates);
            AssetDatabase.CreateAsset(maid, maidPath);

            var runtimeCatalog = UnityEngine.Object.Instantiate(sourceCatalog);
            runtimeCatalog.name = "Wardrobe Runtime Validation Catalog";
            runtimeCatalog.ConfigureCandidate(sourcePresets.Where(p => p.PresetId != sourceMaid.PresetId).Append(maid).ToArray());
            runtimeCatalog.SetReleasedGeneration("validation-fixture-only");
            AssetDatabase.CreateAsset(runtimeCatalog, catalogPath);
            AssetDatabase.SaveAssets();

            var wardrobe = performer.GetComponent<PerformerWardrobe>() ?? performer.gameObject.AddComponent<PerformerWardrobe>();
            wardrobe.ConfigureBinding(runtimeCatalog, config, body, performer);
            var secondObject = UnityEngine.Object.Instantiate(performer.gameObject);
            secondObject.name = "Wardrobe Runtime Validation Performer B";
            secondObject.transform.position = performer.transform.position + new Vector3(3.25f, 0f, 1.5f);
            secondObject.transform.rotation = Quaternion.Euler(0f, 37f, 0f);
            secondObject.transform.localScale = performer.transform.localScale;
            var secondPerformer = secondObject.GetComponent<SuccubusPerformer>();
            var secondBody = secondObject.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                .FirstOrDefault(r => r.sharedMesh == canonical);
            if (secondPerformer == null || secondBody == null)
                throw new InvalidDataException("Could not create the independent second performer/body fixture.");
            var sourceRig = performer.GetComponent<PerformerDissolveRig>();
            var secondRig = secondPerformer.GetComponent<PerformerDissolveRig>();
            if (sourceRig == null || sourceRig.ParticleBody == null || secondRig == null)
                throw new InvalidDataException("The second performer fixture needs its dissolve rig and particle-body source.");
            var particleObject = UnityEngine.Object.Instantiate(sourceRig.ParticleBody.gameObject);
            particleObject.name = "Wardrobe Runtime Validation Performer B Particle Body";
            Vector3 actorOffset = secondObject.transform.position - performer.transform.position;
            particleObject.transform.position = sourceRig.ParticleBody.transform.position + actorOffset;
            particleObject.transform.rotation = secondObject.transform.rotation * Quaternion.Inverse(performer.transform.rotation) *
                sourceRig.ParticleBody.transform.rotation;
            particleObject.transform.localScale = sourceRig.ParticleBody.transform.localScale;
            var secondParticleBody = particleObject.GetComponent<PerformerParticleBody>();
            if (secondParticleBody == null)
                throw new InvalidDataException("Could not duplicate the second performer's particle-body component.");
            SetObjectReference(secondPerformer, "dissolveRig", secondRig);
            SetObjectReference(secondRig, "targetRenderer", secondBody);
            SetObjectReference(secondRig, "particleBody", secondParticleBody);
            SetObjectReference(secondParticleBody, "targetRenderer", secondBody);
            SetObjectReference(secondParticleBody, "debugVisibilityOwner", secondPerformer);
            var secondWardrobe = secondObject.GetComponent<PerformerWardrobe>() ?? secondObject.AddComponent<PerformerWardrobe>();
            secondWardrobe.ConfigureBinding(runtimeCatalog, config, secondBody, secondPerformer);
            var harnessObject = new GameObject("Wardrobe Runtime Execution Harness");
            var harness = harnessObject.AddComponent<WardrobeRuntimeExecutionHarness>();
            harness.Configure(performer, wardrobe, secondPerformer, secondWardrobe, secondParticleBody);
            Scene scene = EditorSceneManager.GetActiveScene();
            if (!EditorSceneManager.SaveScene(scene, fixturePath)) throw new IOException("Could not save the isolated wardrobe runtime fixture.");
            AssetDatabase.SaveAssets();
            EditorApplication.delayCall += () => EditorApplication.isPlaying = true;
        }

        public static void StartIntegrationGate() => StartRuntimeGate();

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/');
            string parent = path.Substring(0, slash), name = path.Substring(slash + 1);
            EnsureFolder(parent);
            if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(parent, name);
        }

        private static void SetObjectReference(UnityEngine.Object target, string propertyName, UnityEngine.Object value)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(propertyName);
            if (property == null || property.propertyType != SerializedPropertyType.ObjectReference)
                throw new InvalidDataException("Serialized object reference is missing: " + target.name + "/" + propertyName);
            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }
    }
}
