using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using DazPose.Performer;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DazPose.UnityValidation
{
    /// <summary>Idempotent migration of the three accepted legacy outfit packages.</summary>
    public static class WardrobeCatalogBuilder
    {
        private const string CatalogPath = "Assets/Wardrobe/WardrobeCatalog.asset";
        private const string ConfigPath = "Assets/Wardrobe/WardrobeConfiguration.asset";
        private const string PresetRoot = "Assets/Wardrobe/Presets";
        private const string StateRoot = "Assets/TestData/WardrobeRuntime/FitStates";
        private const string CandidateScene = "Assets/TestData/LaraCandidate/FirstPerformanceVoidLaraCandidate.unity";
        private const string CanonicalBodyPath = "Assets/TestData/LaraCandidate/LaraBody.asset";
        private const string CanonicalBindingsPath = "Assets/TestData/LaraCandidate/LaraSurfaceBindings.asset";
        private const string CanonicalProfilePath = "Assets/TestData/LaraCandidate/LaraDissolveProfile.asset";
        private static readonly string[] PackageIds = { "first-outfit", "second-outfit", "third-outfit" };

        [Serializable] public sealed class Summary
        {
            public string catalogPath;
            public string characterSignature;
            public int presets, fitStates, hairPresets, footwearPresets;
            public bool idempotent;
            public string[] presetIds;
        }

        public static Summary BuildKnownPackages()
        {
            EnsureFolder("Assets/Wardrobe");
            EnsureFolder(PresetRoot);
            EnsureFolder(StateRoot);
            string[] packagePaths = PackageIds.Select(id => "Assets/TestData/Wardrobe/" + id + "/Outfit.asset").ToArray();
            var packages = packagePaths.Select(path => AssetDatabase.LoadAssetAtPath<WardrobeOutfitDefinition>(path)).ToArray();
            if (packages.Any(p => p == null)) throw new FileNotFoundException("All three accepted WardrobeOutfitDefinition packages are required.");
            if (!packages.Select(p => p.id).SequenceEqual(PackageIds)) throw new InvalidDataException("Source package IDs/order do not match the approved migration set.");

            Mesh canonicalMesh = AssetDatabase.LoadAssetAtPath<Mesh>(CanonicalBodyPath);
            var canonicalBindings = AssetDatabase.LoadAssetAtPath<PerformerSurfaceBindingAsset>(CanonicalBindingsPath);
            var canonicalProfile = AssetDatabase.LoadAssetAtPath<PerformerDissolveProfile>(CanonicalProfilePath);
            if (canonicalMesh == null || canonicalBindings == null || canonicalProfile == null)
                throw new FileNotFoundException("Canonical body/effect baseline assets are incomplete.");

            EditorSceneManager.OpenScene(CandidateScene, OpenSceneMode.Single);
            var canonicalRenderer = UnityEngine.Object.FindObjectsByType<SkinnedMeshRenderer>(FindObjectsInactive.Include)
                .SingleOrDefault(r => r.sharedMesh == canonicalMesh);
            if (canonicalRenderer == null) throw new InvalidDataException("Canonical actor scene has no renderer using LaraBody.asset.");
            string signature = CharacterSignature(canonicalMesh, canonicalRenderer);

            var existing = AssetDatabase.LoadAssetAtPath<WardrobeCatalog>(CatalogPath);
            if (existing != null) return ValidateExisting(existing, packages, signature);
            if (PackageIds.Any(id => AssetDatabase.LoadAssetAtPath<WardrobePreset>(PresetRoot + "/" + id + ".asset") != null))
                throw new InvalidDataException("Partial catalog migration found; preserve assets and repair explicitly rather than overwriting.");

            var presets = new WardrobePreset[packages.Length];
            int fitCount = 0, hairCount = 0, shoeCount = 0;
            for (int i = 0; i < packages.Length; ++i)
            {
                var package = packages[i];
                ValidatePackageAgainstCanonical(package, canonicalMesh, signature);
                var hair = package.pieces.Where(p => string.Equals(p.role, "hair", StringComparison.OrdinalIgnoreCase)).ToArray();
                if (hair.Length > 1) throw new InvalidDataException("Multiple hair pieces need an explicit artist choice: " + package.id);
                string presetPath = PresetRoot + "/" + package.id + ".asset";
                var preset = ScriptableObject.CreateInstance<WardrobePreset>();
                AssetDatabase.CreateAsset(preset, presetPath);
                PopulatePreset(preset, package, signature, hair.SingleOrDefault());
                presets[i] = preset;
                if (hair.Length == 1) hairCount++;
                if (package.footwear != null) shoeCount++;

                var naked = CreateFitState(package.id, "Naked", 0, signature, canonicalMesh,
                    Array.Empty<Mesh>(), canonicalBindings, canonicalProfile, Array.Empty<string>(), false, null);
                var fullPath = package.id + "/Full";
                var fullBindings = AssetDatabase.LoadAssetAtPath<PerformerSurfaceBindingAsset>("Assets/TestData/Wardrobe/" + package.id + "/SurfaceBindings.asset");
                var fullProfile = AssetDatabase.LoadAssetAtPath<PerformerDissolveProfile>("Assets/TestData/Wardrobe/" + package.id + "/DissolveProfile.asset");
                ValidateBindings(package.reviewBody, fullBindings, fullProfile, package.id + " fully dressed");
                var coverage = package.pieces.Where(p => p.coverageChannel >= 0).Select(p => p.id).ToArray();
                var attachments = package.pieces.Select(p => p.mesh).ToArray();
                bool shoesVisible = package.footwear != null && package.pieces.Any(p =>
                    string.Equals(p.role, "footwear", StringComparison.OrdinalIgnoreCase));
                CreateFitState(package.id, "Full", AllMask(package), signature, package.reviewBody,
                    attachments, fullBindings, fullProfile, coverage, shoesVisible, package.footwear);
                fitCount += 2;
                ConfigurePresetFitStates(preset, new[] { naked, AssetDatabase.LoadAssetAtPath<WardrobeFitState>(StateRoot + "/" + package.id + "/Full.asset") });
                EditorUtility.SetDirty(preset);
            }

            var catalog = ScriptableObject.CreateInstance<WardrobeCatalog>();
            AssetDatabase.CreateAsset(catalog, CatalogPath);
            catalog.ConfigureCandidate(presets);
            EditorUtility.SetDirty(catalog);

            var config = ScriptableObject.CreateInstance<WardrobeConfiguration>();
            AssetDatabase.CreateAsset(config, ConfigPath);
            var assignments = packages.SelectMany(p => p.pieces.Where(x => !string.Equals(x.role, "hair", StringComparison.OrdinalIgnoreCase))
                .Select(x => new { preset = p.id, piece = x.id })).ToArray();
            config.ConfigureGenerated(assignments.Select(a => new WardrobeConfiguration.PieceAssignment
            { presetId = a.preset, sourcePieceId = a.piece, layer = 0 }).ToArray());
            EditorUtility.SetDirty(config);
            foreach (var preset in presets) preset.SetGeneratedFitStates(
                new[] { AssetDatabase.LoadAssetAtPath<WardrobeFitState>(StateRoot + "/" + preset.PresetId + "/Naked.asset"),
                    AssetDatabase.LoadAssetAtPath<WardrobeFitState>(StateRoot + "/" + preset.PresetId + "/Full.asset") });
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (!catalog.Validate(out string error)) throw new InvalidDataException("Generated catalog failed validation: " + error);
            return new Summary { catalogPath = CatalogPath, characterSignature = signature,
                presets = presets.Length, fitStates = fitCount, hairPresets = hairCount,
                footwearPresets = shoeCount, idempotent = false, presetIds = PackageIds };
        }

        private static Summary ValidateExisting(WardrobeCatalog catalog, WardrobeOutfitDefinition[] packages, string signature)
        {
            var expected = PackageIds.Select(id => AssetDatabase.LoadAssetAtPath<WardrobePreset>(PresetRoot + "/" + id + ".asset")).ToArray();
            if (expected.Any(p => p == null) || expected.Any(p => p.CharacterSignature != signature))
                throw new InvalidDataException("Existing catalog is partial or its character signature changed; no files were overwritten.");
            var catalogItems = catalog.Presets;
            if (catalogItems.Length != expected.Length || expected.Any(e => !catalogItems.Contains(e)))
                throw new InvalidDataException("Existing catalog references do not match the known packages; no files were overwritten.");
            if (!catalog.Validate(out string error)) throw new InvalidDataException("Existing catalog failed validation: " + error);
            foreach (var preset in expected)
            {
                if (!preset.Validate(out error) || preset.Package == null || !packages.Any(p => p == preset.Package))
                    throw new InvalidDataException("Existing preset does not validate against its package: " + preset.PresetId + "; " + error);
            }
            return new Summary { catalogPath = CatalogPath, characterSignature = signature,
                presets = expected.Length, fitStates = expected.Sum(p => p.FitStates.Length),
                hairPresets = expected.Count(p => p.HairAction == WardrobeHairAction.Set),
                footwearPresets = expected.Count(p => p.Footwear != null), idempotent = true, presetIds = PackageIds };
        }

        private static void ValidatePackageAgainstCanonical(WardrobeOutfitDefinition package, Mesh canonical, string signature)
        {
            if (package.characterReference != canonical) throw new InvalidDataException("Package canonical mesh identity mismatch: " + package.id);
            if (package.reviewBody == null || package.reviewBody.vertexCount != canonical.vertexCount ||
                !package.reviewBody.triangles.SequenceEqual(canonical.triangles))
                throw new InvalidDataException("Package review body topology/order differs from canonical: " + package.id);
            if (package.reviewBody.blendShapeCount < canonical.blendShapeCount)
                throw new InvalidDataException("Package dropped canonical morph channels: " + package.id);
            var ids = package.pieces.Select(p => p.id).ToArray();
            if (ids.Any(string.IsNullOrWhiteSpace) || ids.Distinct(StringComparer.Ordinal).Count() != ids.Length || package.pieces.Any(p => p.mesh == null))
                throw new InvalidDataException("Package piece IDs/meshes are missing or duplicated: " + package.id);
            // Bindings are strict to mesh identity and topology; this is part of the accepted source package.
            var binding = AssetDatabase.LoadAssetAtPath<PerformerSurfaceBindingAsset>("Assets/TestData/Wardrobe/" + package.id + "/SurfaceBindings.asset");
            var profile = AssetDatabase.LoadAssetAtPath<PerformerDissolveProfile>("Assets/TestData/Wardrobe/" + package.id + "/DissolveProfile.asset");
            ValidateBindings(package.reviewBody, binding, profile, package.id);
            if (string.IsNullOrWhiteSpace(signature)) throw new InvalidDataException("Canonical character signature is empty.");
        }

        private static void ValidateBindings(Mesh body, PerformerSurfaceBindingAsset bindings,
            PerformerDissolveProfile profile, string label)
        {
            if (body == null || bindings == null || profile == null || bindings.SourceMesh != body ||
                bindings.BindingCount != PerformerSurfaceBindingAsset.RequiredBindingCount ||
                profile.SurfaceBindings != bindings || profile.ParticleBodyVfxAsset == null)
                throw new InvalidDataException("Body/effect binding identity is incomplete: " + label);
        }

        private static void PopulatePreset(WardrobePreset preset, WardrobeOutfitDefinition package,
            string signature, WardrobeOutfitDefinition.Piece hair)
        {
            var parts = package.pieces.Where(p => !string.Equals(p.role, "hair", StringComparison.OrdinalIgnoreCase)).ToArray();
            preset.ConfigureGenerated(package.id, package.id == "third-outfit" ? "Maid" : package.id,
                signature, 1, package, parts.Select(p => new WardrobePreset.Part
                { sourcePieceId = p.id, layer = 0, requiredOwnerPieceIds = Array.Empty<string>() }).ToArray(),
                package.id == "third-outfit" ? new[] { "maid" } : Array.Empty<string>(),
                hair == null ? WardrobeHairAction.Keep : WardrobeHairAction.Set,
                hair != null ? hair.id : string.Empty, package.footwear);
            EditorUtility.SetDirty(preset);
        }

        private static WardrobeFitState CreateFitState(string presetId, string name, int mask,
            string signature, Mesh body, Mesh[] attachments, PerformerSurfaceBindingAsset bindings,
            PerformerDissolveProfile profile, string[] coverage, bool footwearActive, PerformerFootwearProfile footwear)
        {
            string folder = StateRoot + "/" + presetId;
            EnsureFolder(folder);
            string path = folder + "/" + name + ".asset";
            var state = AssetDatabase.LoadAssetAtPath<WardrobeFitState>(path);
            if (state != null) return state;
            state = ScriptableObject.CreateInstance<WardrobeFitState>();
            AssetDatabase.CreateAsset(state, path);
            state.ConfigureGenerated(mask, signature, WardrobeFitStatus.Validated, body,
                attachments, bindings, profile, coverage, footwearActive, footwear);
            EditorUtility.SetDirty(state);
            return state;
        }

        private static void ConfigurePresetFitStates(WardrobePreset preset, WardrobeFitState[] states)
        {
            var so = new SerializedObject(preset);
            var list = so.FindProperty("fitStates"); list.arraySize = states.Length;
            for (int i = 0; i < states.Length; ++i) list.GetArrayElementAtIndex(i).objectReferenceValue = states[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetObjectArray(UnityEngine.Object target, string propertyName, UnityEngine.Object[] values)
        {
            var so = new SerializedObject(target); var list = so.FindProperty(propertyName); list.arraySize = values.Length;
            for (int i = 0; i < values.Length; ++i) list.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(target);
        }

        private static int AllMask(WardrobeOutfitDefinition package)
        {
            int mask = 0;
            foreach (var piece in package.pieces) if (!string.Equals(piece.role, "hair", StringComparison.OrdinalIgnoreCase)) mask |= 1;
            return mask;
        }

        private static string CharacterSignature(Mesh mesh, SkinnedMeshRenderer renderer)
        {
            var text = new StringBuilder(mesh.vertexCount * 48);
            text.Append(mesh.vertexCount).Append('|').Append(string.Join(",", mesh.triangles)).Append('|');
            foreach (var vertex in mesh.vertices) Append(text, vertex.x, vertex.y, vertex.z);
            foreach (var bone in renderer.bones) text.Append("B:").Append(bone != null ? bone.name : "<null>").Append('|');
            foreach (var bind in mesh.bindposes)
                for (int row = 0; row < 4; ++row) for (int col = 0; col < 4; ++col) Append(text, bind[row, col]);
            foreach (var material in renderer.sharedMaterials)
                text.Append("M:").Append(material != null ? AssetDatabase.GetAssetPath(material) : "<null>").Append('|');
            for (int i = 0; i < mesh.blendShapeCount; ++i)
                text.Append("S:").Append(mesh.GetBlendShapeName(i)).Append(':').Append(mesh.GetBlendShapeFrameCount(i)).Append('|');
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(text.ToString()))).Replace("-", "").ToLowerInvariant();
        }

        private static void Append(StringBuilder text, float x, float y, float z)
        { text.Append(x.ToString("R", CultureInfo.InvariantCulture)).Append(',').Append(y.ToString("R", CultureInfo.InvariantCulture)).Append(',').Append(z.ToString("R", CultureInfo.InvariantCulture)).Append('|'); }
        private static void Append(StringBuilder text, float value)
        { text.Append(value.ToString("R", CultureInfo.InvariantCulture)).Append('|'); }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/');
            string parent = path.Substring(0, slash), name = path.Substring(slash + 1);
            EnsureFolder(parent);
            if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(parent, name);
        }
    }
}
