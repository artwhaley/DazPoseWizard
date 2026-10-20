using System;
using System.Collections.Generic;
using System.Linq;
using DazPose.Performer;
using UnityEditor;
using UnityEngine;

namespace DazPose.UnityValidation
{
    /// <summary>Deterministically generates split clothing/hair rig prefabs from immutable released catalog inputs.</summary>
    public static class WardrobeRigAssetBuilder
    {
        public sealed class Result
        {
            public bool succeeded;
            public string root, manifestPath;
            public int createdPrefabCount, reusedPrefabCount;
            public readonly List<string> issues = new List<string>();
            public WardrobeRigManifest manifest;
        }
        private sealed class PlannedPrefab
        {
            public WardrobePreset preset;
            public WardrobeOutfitDefinition.Piece[] pieces;
            public WardrobeAttachmentRole role;
            public string key, path, contentHash;
            public WardrobeAttachmentDescriptor existing;
        }

        public static Result Generate(WardrobeCatalog catalog, SkinnedMeshRenderer canonicalBody)
        {
            var result = new Result();
            if (catalog == null || canonicalBody == null || canonicalBody.sharedMesh == null)
                return Fail(result, "Select a released wardrobe catalog and a canonical body renderer.");
            if (!catalog.IsReleased) return Fail(result, "Rig prefabs can only be generated from a validated immutable release.");
            if (!catalog.Validate(out string catalogError))
                return Fail(result, "Rig prefabs can only be generated from a validated immutable release. " + catalogError);
            if (canonicalBody.GetComponentInParent<PerformerWardrobe>() == null)
                return Fail(result, "Canonical body renderer must belong to a configured performer with PerformerWardrobe.");
            string signature = catalog.Presets.First().CharacterSignature;
            if (catalog.Presets.Any(p => p.CharacterSignature != signature)) return Fail(result, "Catalog presets do not share one character signature.");

            string root = "Assets/Production/Wardrobe/Rigs/" + catalog.ReleasedGenerationId;
            string manifestPath = root + "/WardrobeRigManifest.asset";
            result.root = root; result.manifestPath = manifestPath;
            var plans = new List<PlannedPrefab>();
            foreach (var preset in catalog.Presets.OrderBy(p => p.PresetId, StringComparer.Ordinal))
            {
                var clothes = preset.Package.pieces.Where(p => !IsHair(p)).ToArray();
                if (clothes.Length == 0) return Fail(result, "Preset has no clothing pieces to generate: " + preset.PresetId);
                plans.Add(Plan(root, catalog.ReleasedGenerationId, preset, WardrobeAttachmentRole.Clothing, preset.PresetId, clothes,
                    WardrobeRigAssetIdentity.PrefabPath(root, WardrobeAttachmentRole.Clothing, preset.PresetId)));
                foreach (var hair in preset.Package.pieces.Where(IsHair).OrderBy(p => p.id, StringComparer.Ordinal))
                {
                    string stableKey = preset.PresetId + "/" + hair.id;
                    plans.Add(Plan(root, catalog.ReleasedGenerationId, preset, WardrobeAttachmentRole.Hair, stableKey, new[] { hair },
                        WardrobeRigAssetIdentity.PrefabPath(root, WardrobeAttachmentRole.Hair, stableKey)));
                }
            }

            // Preflight every canonical/local name map before creating any asset directory or prefab.
            string[] canonicalBoneNames = canonicalBody.bones.Where(x => x != null).Select(x => x.name).ToArray();
            foreach (var plan in plans)
            foreach (var piece in plan.pieces)
            {
                var locals = (piece.localBones ?? Array.Empty<WardrobeOutfitDefinition.LocalBone>())
                    .Select(x => new WardrobeLocalBoneFact(x.name, x.parent)).ToArray();
                if (!WardrobeBoneBindingPlan.TryCreate(canonicalBoneNames,
                        piece.boneNames ?? Array.Empty<string>(), canonicalBody.rootBone != null ? canonicalBody.rootBone.name : string.Empty,
                        locals, out _, out string boneError))
                    return Fail(result, "Generated prefab skeleton binding is invalid: " + plan.key + "/" + piece.id + ": " + boneError);
            }

            // Fail before writing anything when a deterministic output path belongs to different authored content.
            foreach (var plan in plans)
            {
                var existing = AssetDatabase.LoadAssetAtPath<GameObject>(plan.path);
                if (existing == null) continue;
                plan.existing = existing.GetComponent<WardrobeAttachmentDescriptor>();
                if (plan.existing == null || !WardrobeRigAssetIdentity.MayReuseExisting(plan.existing.ContentHash, plan.contentHash))
                    return Fail(result, "Existing generated output differs and will not be overwritten: " + plan.path +
                        ". Generate a versioned derivative or inspect ownership before changing it.");
            }
            var priorManifest = AssetDatabase.LoadAssetAtPath<WardrobeRigManifest>(manifestPath);
            if (AssetDatabase.LoadMainAssetAtPath(manifestPath) != null && priorManifest == null)
                return Fail(result, "Existing manifest path is occupied by an incompatible asset: " + manifestPath);

            try
            {
                foreach (var plan in plans)
                {
                    if (plan.existing != null) { ++result.reusedPrefabCount; continue; }
                    EnsureAssetFolder(System.IO.Path.GetDirectoryName(plan.path).Replace('\\', '/'));
                    var temporary = new GameObject("Wardrobe " + plan.key);
                    try
                    {
                        var rows = new List<WardrobeAttachmentDescriptor.PartRecord>();
                        foreach (var piece in plan.pieces)
                        {
                            var child = new GameObject(piece.id); child.transform.SetParent(temporary.transform, false);
                            var renderer = child.AddComponent<SkinnedMeshRenderer>();
                            renderer.sharedMesh = piece.mesh; renderer.sharedMaterials = piece.materials;
                            renderer.enabled = false;
                            rows.Add(ToRecord(plan, piece, renderer, canonicalBody));
                        }
                        var descriptor = temporary.AddComponent<WardrobeAttachmentDescriptor>();
                        descriptor.ConfigureGenerated(plan.role, catalog.ReleasedGenerationId, signature,
                            plan.key, plan.preset.PresetId, AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(plan.preset)),
                            plan.contentHash, rows.ToArray());
                        if (!descriptor.Validate(out string error)) throw new InvalidOperationException("Generated descriptor is invalid: " + plan.path + " / " + error);
                        var saved = PrefabUtility.SaveAsPrefabAsset(temporary, plan.path);
                        if (saved == null) throw new InvalidOperationException("Unity did not save generated prefab: " + plan.path);
                    }
                    finally { UnityEngine.Object.DestroyImmediate(temporary); }
                    ++result.createdPrefabCount;
                }

                AssetDatabase.SaveAssets();
                var clothingRows = plans.Where(p => p.role == WardrobeAttachmentRole.Clothing)
                    .Select(p => new WardrobeRigManifest.ClothingEntry { presetId = p.preset.PresetId, prefab = LoadDescriptor(p.path) }).ToArray();
                var hairRows = plans.Where(p => p.role == WardrobeAttachmentRole.Hair)
                    .Select(p => new WardrobeRigManifest.HairEntry { stableHairKey = p.key, prefab = LoadDescriptor(p.path) }).ToArray();
                if (priorManifest != null)
                {
                    if (!priorManifest.Validate(catalog, out string priorError))
                        return Fail(result, "Existing manifest is invalid and will not be overwritten: " + priorError);
                    if (!SameRows(priorManifest, clothingRows, hairRows))
                        return Fail(result, "Existing valid manifest differs from this output plan and will not be overwritten: " + manifestPath);
                    result.manifest = priorManifest;
                }
                else
                {
                    EnsureAssetFolder(root);
                    result.manifest = ScriptableObject.CreateInstance<WardrobeRigManifest>();
                    result.manifest.ConfigureGenerated(catalog.ReleasedGenerationId,
                        AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(catalog)), signature, clothingRows, hairRows);
                    if (!result.manifest.Validate(catalog, out string manifestError))
                        throw new InvalidOperationException("Generated manifest is invalid: " + manifestError);
                    AssetDatabase.CreateAsset(result.manifest, manifestPath);
                    AssetDatabase.SaveAssets();
                }
                result.succeeded = true;
                return result;
            }
            catch (Exception error) { return Fail(result, error.ToString()); }
        }

        private static PlannedPrefab Plan(string root, string generationId, WardrobePreset preset, WardrobeAttachmentRole role,
            string key, WardrobeOutfitDefinition.Piece[] pieces, string path) => new PlannedPrefab
        { preset = preset, role = role, key = key, pieces = pieces, path = path, contentHash = Hash(generationId, preset, role, key, pieces) };

        private static WardrobeAttachmentDescriptor.PartRecord ToRecord(PlannedPrefab plan,
            WardrobeOutfitDefinition.Piece piece, SkinnedMeshRenderer renderer, SkinnedMeshRenderer body)
        {
            int layer = plan.preset.Parts.FirstOrDefault(p => p.sourcePieceId == piece.id)?.layer ?? 0;
            return new WardrobeAttachmentDescriptor.PartRecord
            {
                stablePieceId = piece.id, stableKey = plan.role == WardrobeAttachmentRole.Hair ? plan.key : plan.preset.PresetId,
                role = piece.role, renderer = renderer,
                rootBoneName = body.rootBone != null ? body.rootBone.name : string.Empty,
                layer = layer, coverageChannel = piece.coverageChannel, shell = piece.shell,
                requiresBentFootPose = piece.requiresBentFootPose,
                boneNames = piece.boneNames != null ? (string[])piece.boneNames.Clone() : Array.Empty<string>(),
                localBones = (piece.localBones ?? Array.Empty<WardrobeOutfitDefinition.LocalBone>()).Select(b =>
                    new WardrobeAttachmentDescriptor.LocalBoneRecord { name = b.name, parent = b.parent, position = b.position, rotation = b.rotation, scale = b.scale }).ToArray()
            };
        }
        private static WardrobeAttachmentDescriptor LoadDescriptor(string path) =>
            AssetDatabase.LoadAssetAtPath<GameObject>(path)?.GetComponent<WardrobeAttachmentDescriptor>();
        private static bool SameRows(WardrobeRigManifest manifest, WardrobeRigManifest.ClothingEntry[] clothing,
            WardrobeRigManifest.HairEntry[] hair) => manifest.Clothing.Length == clothing.Length && manifest.Hair.Length == hair.Length &&
            clothing.All(row => manifest.Clothing.Any(old => old.presetId == row.presetId && old.prefab == row.prefab)) &&
            hair.All(row => manifest.Hair.Any(old => old.stableHairKey == row.stableHairKey && old.prefab == row.prefab));
        private static bool IsHair(WardrobeOutfitDefinition.Piece piece) =>
            string.Equals(piece.role, "hair", StringComparison.OrdinalIgnoreCase);
        private static string Hash(string generationId, WardrobePreset preset, WardrobeAttachmentRole role, string key,
            WardrobeOutfitDefinition.Piece[] pieces)
        {
            var content = new List<string>();
            foreach (var piece in pieces)
            {
                content.Add(piece.id); content.Add(piece.role); content.Add(piece.coverageChannel.ToString()); content.Add(piece.shell.ToString());
                content.Add(GuidFor(piece.mesh));
                foreach (var material in piece.materials ?? Array.Empty<Material>()) content.Add(GuidFor(material));
                foreach (var bone in piece.boneNames ?? Array.Empty<string>()) content.Add(bone);
                foreach (var local in piece.localBones ?? Array.Empty<WardrobeOutfitDefinition.LocalBone>())
                { content.Add(local.name); content.Add(local.parent); content.Add(local.position.ToString()); content.Add(local.rotation.ToString()); content.Add(local.scale.ToString()); }
            }
            return WardrobeRigAssetIdentity.ContentHash(generationId, preset.PresetId, role, key, content);
        }
        private static string GuidFor(UnityEngine.Object value)
        { var path = AssetDatabase.GetAssetPath(value); return string.IsNullOrEmpty(path) ? string.Empty : AssetDatabase.AssetPathToGUID(path); }
        private static void EnsureAssetFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            var name = System.IO.Path.GetFileName(path);
            EnsureAssetFolder(parent);
            if (string.IsNullOrEmpty(AssetDatabase.CreateFolder(parent, name)) && !AssetDatabase.IsValidFolder(path))
                throw new InvalidOperationException("Could not create generated wardrobe folder: " + path);
        }
        private static Result Fail(Result result, string issue) { result.succeeded = false; result.issues.Add(issue); return result; }
    }
}
