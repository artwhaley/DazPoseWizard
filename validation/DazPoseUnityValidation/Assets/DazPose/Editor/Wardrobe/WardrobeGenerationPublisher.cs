using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using DazPose.Performer;
using UnityEditor;
using UnityEngine;

namespace DazPose.UnityValidation
{
    /// <summary>Compiles and snapshots a candidate generation before moving its catalog reference.</summary>
    public static class WardrobeGenerationPublisher
    {
        [Serializable] public sealed class Result
        {
            public bool published;
            public string generationId;
            public string catalogPath;
            public string[] issues = Array.Empty<string>();
            public int presetCount, fitStateCount, snapshottedMeshCount, snapshottedMaterialCount;
            public string temporaryArtifactRoot;
        }

        private sealed class SnapshotContext
        {
            public string root, presetRoot, generation;
            public readonly Dictionary<Mesh, Mesh> meshes = new Dictionary<Mesh, Mesh>();
            public readonly Dictionary<Material, Material> materials = new Dictionary<Material, Material>();
            public readonly Dictionary<string, PerformerFootwearProfile> footwear = new Dictionary<string, PerformerFootwearProfile>(StringComparer.Ordinal);
            public readonly Dictionary<PerformerSurfaceBindingAsset, PerformerSurfaceBindingAsset> bindings = new Dictionary<PerformerSurfaceBindingAsset, PerformerSurfaceBindingAsset>();
            public readonly Dictionary<PerformerDissolveProfile, PerformerDissolveProfile> profiles = new Dictionary<PerformerDissolveProfile, PerformerDissolveProfile>();
        }

        public static Result PublishCandidate(WardrobeCatalog candidate, WardrobeConfiguration configuration,
            SkinnedMeshRenderer canonicalRenderer)
        {
            var result = new Result();
            if (candidate == null || configuration == null || canonicalRenderer == null || canonicalRenderer.sharedMesh == null)
                return Fail("Catalog, configuration and canonical body are required.");
            if (candidate.IsReleased) return Fail("A released catalog is immutable. Fork it into a candidate before publishing a new generation.");
            if (!candidate.Validate(out string catalogError)) return Fail("Candidate catalog is invalid: " + catalogError);

            string generation;
            try { generation = ComputeGeneration(candidate, configuration); }
            catch (Exception exception) { return Fail("Candidate resolution failed: " + exception.Message); }
            string root = "Assets/TestData/WardrobeRuntime/Generations/" + generation;
            string presetRoot = "Assets/Wardrobe/Releases/" + generation;
            // Never reuse a writable directory that may be referenced by an existing release.
            if (AssetDatabase.IsValidFolder(presetRoot) || AssetDatabase.IsValidFolder(root))
                return Fail("This immutable generation already exists. Use its released catalog; publication will not overwrite it.", generation);
            string attemptRoot = "Assets/TestData/WardrobeRuntime/ReleaseCandidates/" + Guid.NewGuid().ToString("N");
            var context = new SnapshotContext { root = attemptRoot + "/SnapshotData", presetRoot = attemptRoot + "/SnapshotPresets", generation = generation };
            var compiledByPreset = new Dictionary<WardrobePreset, WardrobeFitState[]>();
            var issues = new List<string>();
            try
            {
                foreach (var preset in candidate.Presets)
                {
                    var compiled = WardrobeFitCompiler.Compile(preset, configuration, canonicalRenderer,
                        attemptRoot, false);
                    if (compiled.needsFitStates != 0)
                        issues.AddRange(compiled.issues.Select(x => preset.PresetId + ":" + x));
                    else compiledByPreset.Add(preset, compiled.fitStates);
                }
                if (issues.Count > 0) return new Result { published = false, generationId = generation, issues = issues.ToArray(), temporaryArtifactRoot = attemptRoot };

                EnsureFolder(context.root); EnsureFolder(context.presetRoot);
                var snapshots = new List<WardrobePreset>();
                int fitCount = 0;
                foreach (var sourcePreset in candidate.Presets)
                {
                    WardrobePreset snapshot = SnapshotPreset(sourcePreset, configuration,
                        compiledByPreset[sourcePreset], context);
                    snapshots.Add(snapshot);
                    fitCount += snapshot.FitStates.Length;
                }
                var validationCatalog = ScriptableObject.CreateInstance<WardrobeCatalog>();
                validationCatalog.ConfigureCandidate(snapshots.ToArray());
                bool snapshotsValid = validationCatalog.Validate(out string snapshotError);
                UnityEngine.Object.DestroyImmediate(validationCatalog);
                if (!snapshotsValid) return Fail("Generated release snapshot is invalid: " + snapshotError, generation, attemptRoot);

                EnsureFolder("Assets/Wardrobe/Releases"); EnsureFolder("Assets/TestData/WardrobeRuntime/Generations");
                string moveError = AssetDatabase.MoveAsset(context.root, root);
                if (!string.IsNullOrEmpty(moveError)) throw new InvalidDataException(moveError);
                moveError = AssetDatabase.MoveAsset(context.presetRoot, presetRoot);
                if (!string.IsNullOrEmpty(moveError)) throw new InvalidDataException(moveError);

                string inputHash = ComputeInputHash(candidate, configuration);
                var releaseMaterials = configuration.MaterialOverrides;
                foreach (var materialOverride in releaseMaterials)
                    if (materialOverride != null && materialOverride.material != null &&
                        context.materials.TryGetValue(materialOverride.material, out var snapshotMaterial))
                        materialOverride.material = snapshotMaterial;
                // This is the only mutation of the catalog/config references and occurs after all compilation and snapshot checks.
                string priorConfiguration = EditorJsonUtility.ToJson(configuration);
                string priorCatalog = EditorJsonUtility.ToJson(candidate);
                try
                {
                    configuration.ConfigureGenerated(configuration.Assignments, releaseMaterials, configuration.FootwearOverrides);
                    candidate.ConfigureCandidate(snapshots.ToArray());
                    candidate.SetReleasedGeneration(generation);
                    configuration.CommitRevision(generation, new[] { inputHash });
                    foreach (var preset in snapshots)
                    {
                        preset.ConfigureGenerated(preset.PresetId, preset.DisplayName, preset.CharacterSignature,
                            configuration.Revision, preset.Package, preset.Parts, preset.Aliases,
                            preset.HairAction, preset.HairId, preset.Footwear);
                        EditorUtility.SetDirty(preset);
                    }
                    EditorUtility.SetDirty(configuration); EditorUtility.SetDirty(candidate);
                    AssetDatabase.SaveAssets();
                }
                catch
                {
                    EditorJsonUtility.FromJsonOverwrite(priorConfiguration, configuration);
                    EditorJsonUtility.FromJsonOverwrite(priorCatalog, candidate);
                    EditorUtility.SetDirty(configuration); EditorUtility.SetDirty(candidate); AssetDatabase.SaveAssets();
                    throw;
                }
                result.published = true; result.generationId = generation;
                result.catalogPath = AssetDatabase.GetAssetPath(candidate);
                result.presetCount = snapshots.Count; result.fitStateCount = fitCount;
                result.snapshottedMeshCount = context.meshes.Count; result.snapshottedMaterialCount = context.materials.Count;
                result.issues = Array.Empty<string>();
                result.temporaryArtifactRoot = attemptRoot;
                return result;
            }
            catch (Exception exception) { return Fail("Publication stopped before catalog advance: " + exception.Message, generation, attemptRoot); }
        }

        public static string ComputeGeneration(WardrobeCatalog catalog, WardrobeConfiguration configuration)
        {
            var text = new StringBuilder("wardrobe-release-v2|");
            text.Append(WardrobeCandidateWorkflow.DraftHash(configuration)).Append('|');
            text.Append(configuration != null ? configuration.GenerationHash : string.Empty).Append('|');
            foreach (var preset in catalog.Presets.OrderBy(x => x.PresetId, StringComparer.Ordinal))
            {
                text.Append(preset.PresetId).Append('|').Append(preset.CharacterSignature).Append('|');
                var package = preset.Package;
                text.Append(package != null ? package.recipeHash : string.Empty).Append('|');
                text.Append(package != null ? DependencyHash(package) : string.Empty).Append('|');
                foreach (var part in WardrobeCandidateSnapshot.Resolve(preset, configuration).Parts)
                    text.Append(part.sourcePieceId).Append(':').Append(part.layer).Append(':').Append(string.Join(",", part.requiredOwnerPieceIds)).Append('|');
                text.Append(preset.HairAction).Append(':').Append(preset.HairId).Append('|');
                text.Append(preset.Footwear != null ? DependencyHash(preset.Footwear) : "barefoot").Append('|');
            }
            return Hash(text.ToString());
        }

        private static WardrobePreset SnapshotPreset(WardrobePreset source, WardrobeConfiguration configuration,
            WardrobeFitState[] compiledFitStates, SnapshotContext context)
        {
            string folder = context.presetRoot + "/Presets/" + source.PresetId;
            EnsureFolder(folder);
            var resolved = WardrobeCandidateSnapshot.Resolve(source, configuration);
            var package = SnapshotPackage(source.Package, source.PresetId, resolved, context);
            var shoe = SnapshotFootwear(source, configuration, context);
            var parts = resolved.Parts;
            if (!compiledFitStates.Select(f => f.VisibleMask).OrderBy(x => x).SequenceEqual(resolved.ReachableMasks.OrderBy(x => x)))
                throw new InvalidDataException("Release fit masks do not match resolved parts: " + source.PresetId);
            var preset = LoadOrCreate<WardrobePreset>(folder + "/Preset.asset", () =>
            {
                var created = ScriptableObject.CreateInstance<WardrobePreset>();
                created.ConfigureGenerated(source.PresetId, source.DisplayName, source.CharacterSignature,
                    configuration.Revision + 1, package, parts, source.Aliases, source.HairAction, source.HairId, shoe);
                return created;
            });
            preset.ConfigureGenerated(source.PresetId, source.DisplayName, source.CharacterSignature,
                configuration.Revision + 1, package, parts, source.Aliases, source.HairAction, source.HairId, shoe);
            var fits = compiledFitStates.Select((fit, index) => SnapshotFitState(fit, source, package, configuration,
                configuration.Revision + 1, folder, index, context)).ToArray();
            preset.SetGeneratedFitStates(fits);
            EditorUtility.SetDirty(preset);
            return preset;
        }

        private static WardrobeOutfitDefinition SnapshotPackage(WardrobeOutfitDefinition source, string presetId,
            WardrobeCandidateSnapshot resolved, SnapshotContext context)
        {
            if (source == null) throw new InvalidDataException("Preset source package is missing: " + presetId);
            string path = context.root + "/Packages/" + presetId + "/Outfit.asset";
            EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
            var package = LoadOrCreate(path, () => UnityEngine.Object.Instantiate(source));
            package.name = source.name + " (Released " + context.generation.Substring(0, 8) + ")";
            package.id = source.id; package.sourceFbxHash = source.sourceFbxHash;
            package.sourceDufHash = source.sourceDufHash; package.recipeHash = source.recipeHash;
            package.characterReference = SnapshotMesh(source.characterReference, "character-reference", context);
            package.reviewBody = SnapshotMesh(source.reviewBody, "review-body", context);
            package.footwear = SnapshotFootwearProfile(source.footwear, presetId, context);
            package.pieces = source.pieces.Select(piece =>
            {
                var materials = piece.materials.Select((material, slot) => SnapshotMaterial(material,
                    "piece-" + piece.id + "-slot-" + slot, context)).ToArray();
                foreach (var materialOverride in resolved.MaterialOverrides.Where(x =>
                    x.sourcePieceId == piece.id && x.material != null && int.TryParse(x.materialSlotId, out _)))
                {
                    int slot = int.Parse(materialOverride.materialSlotId);
                    if (slot >= 0 && slot < materials.Length) materials[slot] = SnapshotMaterial(materialOverride.material,
                        "override-" + piece.id + "-slot-" + slot, context);
                }
                // Opacity must be in the immutable package, not supplied by a mutable runtime configuration.
                foreach (var row in resolved.MaterialOverrides.Where(x =>
                    x.sourcePieceId == piece.id && x.opacityOverride))
                {
                    if (!int.TryParse(row.materialSlotId, out int slot) || slot < 0 || slot >= materials.Length)
                        throw new InvalidDataException("Invalid opacity material slot: " + piece.id + "/" + row.materialSlotId);
                    var material = UnityEngine.Object.Instantiate(materials[slot]);
                    if (!WardrobeMaterialOpacity.Apply(material, row.opacity))
                    { UnityEngine.Object.DestroyImmediate(material); throw new InvalidDataException("Opacity shader has no supported transparency control: " + piece.id); }
                    string materialPath = context.root + "/Materials/Opacity-" + Safe(presetId) + "-" + Safe(piece.id) + "-" + slot + ".mat";
                    AssetDatabase.CreateAsset(material, materialPath); materials[slot] = material;
                }
                return new WardrobeOutfitDefinition.Piece
                {
                    id = piece.id, sourceNode = piece.sourceNode, role = piece.role, shell = piece.shell,
                    requiresBentFootPose = piece.requiresBentFootPose, coverageChannel = piece.coverageChannel,
                    mesh = SnapshotMesh(piece.mesh, "piece-" + piece.id, context), materials = materials,
                    boneNames = piece.boneNames != null ? (string[])piece.boneNames.Clone() : Array.Empty<string>(),
                    localBones = (piece.localBones ?? Array.Empty<WardrobeOutfitDefinition.LocalBone>()).Select(b => new WardrobeOutfitDefinition.LocalBone
                        { name = b.name, parent = b.parent, position = b.position, rotation = b.rotation, scale = b.scale }).ToArray()
                };
            }).ToArray();
            EditorUtility.SetDirty(package);
            return package;
        }

        private static PerformerFootwearProfile SnapshotFootwear(WardrobePreset source,
            WardrobeConfiguration configuration, SnapshotContext context)
        {
            var profile = SnapshotFootwearProfile(source.Footwear, source.PresetId, context);
            var resolved = WardrobeCandidateSnapshot.Resolve(source, configuration);
            if (profile != null) { profile.standingHeight = resolved.StandingLift; profile.footShrink = resolved.FootShrink; }
            return profile;
        }

        private static PerformerFootwearProfile SnapshotFootwearProfile(PerformerFootwearProfile source,
            string presetId, SnapshotContext context)
        {
            if (source == null) return null;
            string cacheKey = presetId + "/" + ObjectKey(source);
            if (context.footwear.TryGetValue(cacheKey, out var cached)) return cached;
            string path = context.root + "/Footwear/" + Safe(presetId) + "-" + ObjectKey(source) + ".asset";
            EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
            var clone = LoadOrCreate(path, () => UnityEngine.Object.Instantiate(source));
            clone.sourceName = source.sourceName; clone.standingHeight = source.standingHeight; clone.footShrink = source.footShrink;
            clone.rigidShoe = source.rigidShoe; clone.constrainToes = source.constrainToes;
            clone.leftHeel = source.leftHeel; clone.leftToe = source.leftToe; clone.rightHeel = source.rightHeel; clone.rightToe = source.rightToe;
            clone.groundOffset = source.groundOffset; clone.swingBlendHeight = source.swingBlendHeight;
            clone.referenceBody = SnapshotMesh(source.referenceBody, "footwear-reference-" + presetId, context);
            clone.referenceToeBones = (source.referenceToeBones ?? Array.Empty<WardrobeOutfitDefinition.LocalBone>())
                .Select(b => new WardrobeOutfitDefinition.LocalBone { name = b.name, parent = b.parent,
                    position = b.position, rotation = b.rotation, scale = b.scale }).ToArray();
            context.footwear[cacheKey] = clone; EditorUtility.SetDirty(clone); return clone;
        }

        private static WardrobeFitState SnapshotFitState(WardrobeFitState source, WardrobePreset sourcePreset,
            WardrobeOutfitDefinition package, WardrobeConfiguration configuration, int revision,
            string presetFolder, int index, SnapshotContext context)
        {
            string path = context.root + "/FitStates/" + sourcePreset.PresetId + "/Mask-" + source.VisibleMask + ".asset";
            EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
            var fit = LoadOrCreate(path, () => ScriptableObject.CreateInstance<WardrobeFitState>());
            Mesh mesh = SnapshotMesh(source.BodyMesh, "fit-body-" + sourcePreset.PresetId + "-" + source.VisibleMask, context);
            var attachmentMeshes = source.AttachmentMeshes.Select((attachment, i) =>
            {
                var sourcePiece = sourcePreset.Package.pieces.FirstOrDefault(p => p.mesh == attachment);
                var matching = sourcePiece != null ? package.pieces.FirstOrDefault(p => p.id == sourcePiece.id) : null;
                return matching != null ? matching.mesh : SnapshotMesh(attachment,
                    "fit-attachment-" + sourcePreset.PresetId + "-" + source.VisibleMask + "-" + i, context);
            }).ToArray();
            var binding = SnapshotBinding(source.SurfaceBindings, mesh, sourcePreset.PresetId, source.VisibleMask, context);
            var profile = SnapshotDissolveProfile(source.DissolveProfile, binding,
                sourcePreset.PresetId, source.VisibleMask, context);
            var shoe = SnapshotFootwear(sourcePreset, configuration, context);
            fit.ConfigureCandidate(source.VisibleMask, source.CharacterSignature, source.IssueCodes,
                mesh, attachmentMeshes, binding, profile, source.Coverage.VisiblePieceIds,
                source.Footwear.FootwearActive, source.Footwear.BentFootPoseActive, shoe,
                source.Footwear.StandingLift, source.Footwear.FootShrink);
            EditorUtility.SetDirty(fit);
            return fit;
        }

        private static PerformerSurfaceBindingAsset SnapshotBinding(PerformerSurfaceBindingAsset source,
            Mesh mesh, string presetId, int mask, SnapshotContext context)
        {
            if (source == null || mesh == null) throw new InvalidDataException("Fit state is missing body mesh or surface bindings.");
            string key = source.name + "-" + presetId + "-" + mask;
            string path = context.root + "/Bindings/" + key + ".asset";
            EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
            var binding = LoadOrCreate(path, () => ScriptableObject.CreateInstance<PerformerSurfaceBindingAsset>());
            binding.ConfigureBaked(mesh, context.generation + ":" + key, mesh.vertexCount,
                PerformerSurfaceBindingAsset.CountIndices(mesh), PerformerSurfaceBindingAsset.ComputeTopologyHash(mesh),
                source.RandomSeed, source.CopyBindings());
            var probe = new GameObject("Temporary release-binding validation");
            try
            {
                var renderer = probe.AddComponent<SkinnedMeshRenderer>(); renderer.sharedMesh = mesh;
                if (!binding.IsValidFor(renderer, out string reason)) throw new InvalidDataException("Snapshot binding failed topology validation: " + reason);
            }
            finally { UnityEngine.Object.DestroyImmediate(probe); }
            context.bindings[source] = binding; EditorUtility.SetDirty(binding); return binding;
        }

        private static PerformerDissolveProfile SnapshotDissolveProfile(PerformerDissolveProfile source,
            PerformerSurfaceBindingAsset binding, string presetId, int mask, SnapshotContext context)
        {
            if (source == null) throw new InvalidDataException("Fit state dissolve profile is missing.");
            string path = context.root + "/Profiles/" + presetId + "-" + mask + ".asset";
            EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
            var profile = LoadOrCreate(path, () => UnityEngine.Object.Instantiate(source));
            profile.ConfigureParticleBodyAssets(source.ParticleBodyVfxAsset, binding);
            context.profiles[source] = profile; EditorUtility.SetDirty(profile); return profile;
        }

        private static Mesh SnapshotMesh(Mesh source, string semantic, SnapshotContext context)
        {
            if (source == null) return null;
            if (context.meshes.TryGetValue(source, out var cached)) return cached;
            string path = context.root + "/Meshes/" + Safe(semantic) + "-" + ObjectKey(source) + ".asset";
            EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
            var mesh = LoadOrCreate(path, () => UnityEngine.Object.Instantiate(source));
            mesh.name = source.name + " (Release " + context.generation.Substring(0, 8) + ")";
            context.meshes[source] = mesh; EditorUtility.SetDirty(mesh); return mesh;
        }

        private static Material SnapshotMaterial(Material source, string semantic, SnapshotContext context)
        {
            if (source == null) return null;
            if (context.materials.TryGetValue(source, out var cached)) return cached;
            string path = context.root + "/Materials/" + Safe(semantic) + "-" + ObjectKey(source) + ".mat";
            EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
            var material = LoadOrCreate(path, () => UnityEngine.Object.Instantiate(source));
            material.name = source.name + " (Release " + context.generation.Substring(0, 8) + ")";
            context.materials[source] = material; EditorUtility.SetDirty(material); return material;
        }

        private static T LoadOrCreate<T>(string path, Func<T> create) where T : UnityEngine.Object
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null) return existing;
            var value = create();
            value.name = Path.GetFileNameWithoutExtension(path);
            AssetDatabase.CreateAsset(value, path);
            return value;
        }

        private static string ComputeInputHash(WardrobeCatalog catalog, WardrobeConfiguration configuration) =>
            Hash(ComputeGeneration(catalog, configuration) + "|" + string.Join("|", catalog.Presets.Select(p => DependencyHash(p.Package))));

        private static string DependencyHash(UnityEngine.Object asset)
        {
            string path = AssetDatabase.GetAssetPath(asset);
            return string.IsNullOrEmpty(path) ? "missing" : AssetDatabase.GetAssetDependencyHash(path).ToString();
        }

        private static string ObjectKey(UnityEngine.Object asset)
        {
            string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(asset));
            return string.IsNullOrEmpty(guid) ? Hash(asset.GetType().FullName + "|" + asset.name).Substring(0, 12) : guid;
        }

        private static string Safe(string value) => string.Concat((value ?? "asset").Select(c =>
            char.IsLetterOrDigit(c) || c == '-' || c == '_' ? c : '_'));

        private static string Hash(string value)
        { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-", "").ToLowerInvariant(); }

        private static Result Fail(string message, string generation = null, string artifactRoot = null) => new Result
        { published = false, generationId = generation, issues = new[] { message }, temporaryArtifactRoot = artifactRoot };

        private static void EnsureFolder(string path)
        {
            if (string.IsNullOrEmpty(path) || AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/');
            if (slash <= 0) throw new InvalidDataException("Invalid Assets folder: " + path);
            EnsureFolder(path.Substring(0, slash));
            if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
        }
    }
}
