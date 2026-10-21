using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using DazPose.Editor.ParticleBody;
using DazPose.Performer;
using UnityEditor;
using UnityEngine;

namespace DazPose.UnityValidation
{
    /// <summary>Compiles the reachable visibility masks for one wardrobe candidate.</summary>
    public static class WardrobeFitCompiler
    {
        // Changing this value intentionally invalidates candidate fit identities. It prevents
        // an older fitted body, generated before the R03 barefoot-region repair, from being
        // silently reused under the same outfit/mask key.
        public const string BarefootFootRegionRepairVersion = "r03-canonical-foot-region-v1";

        [Serializable] public sealed class Result
        {
            public string presetId;
            public int populatedMask;
            public int[] reachableMasks = Array.Empty<int>();
            public int validatedStates, needsFitStates;
            public string[] issues = Array.Empty<string>();
            public WardrobeFitState[] fitStates = Array.Empty<WardrobeFitState>();
        }

        public static Result Compile(WardrobePreset preset, WardrobeConfiguration configuration,
            SkinnedMeshRenderer canonicalRenderer, string outputRoot, bool bindToPreset = true)
        {
            if (preset == null || canonicalRenderer == null || canonicalRenderer.sharedMesh == null)
                throw new ArgumentNullException("Preset and canonical body renderer are required.");
            if (!preset.Validate(out string presetError)) throw new InvalidDataException(presetError);
            var package = preset.Package;
            var fullBindings = AssetDatabase.LoadAssetAtPath<PerformerSurfaceBindingAsset>(
                "Assets/TestData/Wardrobe/" + package.id + "/SurfaceBindings.asset");
            var fullProfile = AssetDatabase.LoadAssetAtPath<PerformerDissolveProfile>(
                "Assets/TestData/Wardrobe/" + package.id + "/DissolveProfile.asset");
            var priorFull = preset.FitStates.FirstOrDefault(f => f != null && f.BodyMesh == package.reviewBody);
            if (priorFull != null && priorFull.SurfaceBindings != null && priorFull.SurfaceBindings.SourceMesh == package.reviewBody)
            { fullBindings = priorFull.SurfaceBindings; fullProfile = priorFull.DissolveProfile; }
            var canonicalBindings = AssetDatabase.LoadAssetAtPath<PerformerSurfaceBindingAsset>(
                "Assets/TestData/LaraCandidate/LaraSurfaceBindings.asset");
            var canonicalProfile = AssetDatabase.LoadAssetAtPath<PerformerDissolveProfile>(
                "Assets/TestData/LaraCandidate/LaraDissolveProfile.asset");
            Mesh canonicalMesh = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/TestData/LaraCandidate/LaraBody.asset");
            if (fullBindings == null || fullProfile == null || canonicalBindings == null || canonicalProfile == null || canonicalMesh == null)
                throw new FileNotFoundException("The accepted source/canonical body, dissolve profiles, and surface bindings must exist.");

            var resolved = WardrobeCandidateSnapshot.Resolve(preset, configuration);
            var parts = resolved.Parts;
            foreach (var row in resolved.MaterialOverrides.Where(r => r.opacityOverride))
            {
                var piece = package.pieces.Single(p => p.id == row.sourcePieceId);
                var material = row.material != null ? row.material : piece.materials[int.Parse(row.materialSlotId)];
                if (!WardrobeMaterialOpacity.TryRead(material, out _)) throw new InvalidDataException("Unsupported opacity control: " + preset.PresetId + "/" + row.sourcePieceId + "/" + row.materialSlotId);
            }
            float standingLift = resolved.StandingLift;
            float footShrink = resolved.FootShrink;
            int populated = resolved.PopulatedMask;
            var masks = resolved.ReachableMasks.ToList();
            var states = new List<WardrobeFitState>(masks.Count);
            var issues = new List<string>();
            EnsureFolder(outputRoot);
            string signature = preset.CharacterSignature;

            foreach (int mask in masks)
            {
                var visible = parts.Where(p => (mask & (1 << p.layer)) != 0)
                    .Select(p => package.pieces.Single(piece => piece.id == p.sourcePieceId)).ToArray();
                bool shoesVisible = visible.Any(p => Role(p, "footwear"));
                bool shoeSupportActive = shoesVisible && preset.Footwear != null;
                bool footPoseActive = shoesVisible || visible.Any(p => p.requiresBentFootPose ||
                    Role(p, "hosiery"));
                var stateIssues = new List<string>();

                Mesh bodyMesh;
                PerformerSurfaceBindingAsset bindings;
                PerformerDissolveProfile profile;
                Mesh[] attachmentMeshes = visible.Select(p => p.mesh).ToArray();
                string[] coverageOwners = visible.Where(p => p.coverageChannel >= 0).Select(p => p.id).ToArray();
                if (mask == 0)
                {
                    bodyMesh = canonicalMesh; bindings = canonicalBindings; profile = canonicalProfile;
                }
                else
                {
                    bodyMesh = BuildCoverageMesh(package.reviewBody, package.pieces, visible, stateIssues);
                    if (stateIssues.Count == 0 && !footPoseActive)
                        ApplyCanonicalBarefootFootRegion(bodyMesh, canonicalMesh, canonicalRenderer, stateIssues);
                    if (stateIssues.Count == 0)
                    {
                        string content = StableStateHash(preset, mask, parts, bodyMesh, true);
                        string folder = outputRoot.TrimEnd('/') + "/" + preset.PresetId + "/" + content;
                        EnsureFolder(folder);
                        bodyMesh.name = preset.PresetId + " Body Fit " + mask;
                        string meshPath = folder + "/Body.asset";
                        var oldMesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                        if (oldMesh == null) AssetDatabase.CreateAsset(bodyMesh, meshPath);
                        else { UnityEngine.Object.DestroyImmediate(bodyMesh); bodyMesh = oldMesh; }

                        Mesh priorMesh = canonicalRenderer.sharedMesh;
                        try
                        {
                            canonicalRenderer.sharedMesh = bodyMesh;
                            bindings = PerformerSurfaceBindingBaker.Bake(canonicalRenderer,
                                PerformerSurfaceBindingAsset.RequiredBindingCount, 0x504f3942, folder + "/SurfaceBindings.asset");
                        }
                        finally { canonicalRenderer.sharedMesh = priorMesh; }
                        var priorProfile = AssetDatabase.LoadAssetAtPath<PerformerDissolveProfile>(folder + "/DissolveProfile.asset");
                        if (priorProfile == null)
                        {
                            profile = UnityEngine.Object.Instantiate(fullProfile);
                            profile.name = preset.PresetId + " Fit " + mask + " Dissolve Profile";
                            profile.ConfigureParticleBodyAssets(fullProfile.ParticleBodyVfxAsset, bindings);
                            AssetDatabase.CreateAsset(profile, folder + "/DissolveProfile.asset");
                            EditorUtility.SetDirty(profile);
                        }
                        else profile = priorProfile;
                    }
                    else
                    {
                        // Keep diagnostics inspectable while preventing unsafe use at runtime.
                        bodyMesh = package.reviewBody;
                        bindings = fullBindings;
                        profile = fullProfile;
                    }
                }

                string stateKey = mask == 0 ? "naked" : StableStateHash(preset, mask, parts,
                    bodyMesh != null ? bodyMesh : package.reviewBody, mask != 0 && !footPoseActive);
                string statePath = outputRoot.TrimEnd('/') + "/" + preset.PresetId + "/" + stateKey + "/FitState.asset";
                EnsureFolder(Path.GetDirectoryName(statePath).Replace('\\', '/'));
                var state = AssetDatabase.LoadAssetAtPath<WardrobeFitState>(statePath);
                if (state == null)
                {
                    state = ScriptableObject.CreateInstance<WardrobeFitState>();
                    AssetDatabase.CreateAsset(state, statePath);
                }
                state.ConfigureCandidate(mask, signature, stateIssues.ToArray(), bodyMesh, attachmentMeshes,
                    bindings, profile, coverageOwners, shoeSupportActive, footPoseActive, preset.Footwear,
                    standingLift, footShrink);
                EditorUtility.SetDirty(state);
                states.Add(state);
                issues.AddRange(stateIssues.Select(code => preset.PresetId + "/mask-" + mask + ":" + code));
            }

            if (bindToPreset && states.All(s => s.Status == WardrobeFitStatus.Validated))
            {
                preset.ConfigureGenerated(preset.PresetId, preset.DisplayName, preset.CharacterSignature,
                    resolved.ConfigurationRevision, preset.Package, resolved.Parts, preset.Aliases,
                    preset.HairAction, preset.HairId, preset.Footwear);
                preset.SetGeneratedFitStates(states.ToArray());
                EditorUtility.SetDirty(preset);
            }
            AssetDatabase.SaveAssets();
            return new Result { presetId = preset.PresetId, populatedMask = populated,
                reachableMasks = masks.ToArray(), validatedStates = states.Count(s => s.Status == WardrobeFitStatus.Validated),
                needsFitStates = states.Count(s => s.Status == WardrobeFitStatus.NeedsFit),
                issues = issues.ToArray(), fitStates = states.ToArray() };
        }

        public static Mesh BuildCoverageMesh(Mesh source, WardrobeOutfitDefinition.Piece[] allPieces,
            WardrobeOutfitDefinition.Piece[] visiblePieces, List<string> issues)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            var owners = (allPieces ?? Array.Empty<WardrobeOutfitDefinition.Piece>())
                .Where(p => p != null && p.coverageChannel >= 0).ToArray();
            var visibleIds = new HashSet<string>((visiblePieces ?? Array.Empty<WardrobeOutfitDefinition.Piece>())
                .Where(p => p != null).Select(p => p.id), StringComparer.Ordinal);
            if (owners.Any(p => p.coverageChannel > 1))
                issues.Add("coverage.unsupportedChannel:onlyUV3xAndYAreCurrentShaderCoverageChannels");
            foreach (var group in owners.GroupBy(p => p.coverageChannel))
            {
                bool any = group.Any(p => visibleIds.Contains(p.id));
                bool all = group.All(p => visibleIds.Contains(p.id));
                if (any && !all)
                    issues.Add("coverage.sharedChannelCannotSeparatePieces:channel=" + group.Key + ";owners=" + string.Join(",", group.Select(p => p.id)));
            }
            var sourceUv = new List<Vector4>();
            source.GetUVs(3, sourceUv);
            if (owners.Length > 0 && sourceUv.Count != source.vertexCount)
                issues.Add("coverage.sourceChannelMissing:mesh=" + source.name + ";uvChannel=3");
            var copy = UnityEngine.Object.Instantiate(source);
            copy.name = source.name + " (Compiled Coverage)";
            if (sourceUv.Count == source.vertexCount)
            {
                bool keepX = owners.Where(p => p.coverageChannel == 0).Any(p => visibleIds.Contains(p.id));
                bool keepY = owners.Where(p => p.coverageChannel == 1).Any(p => visibleIds.Contains(p.id));
                for (int i = 0; i < sourceUv.Count; ++i)
                {
                    var value = sourceUv[i];
                    if (!keepX) value.x = 0;
                    if (!keepY) value.y = 0;
                    sourceUv[i] = value;
                }
                copy.SetUVs(3, sourceUv);
            }
            return copy;
        }

        public static List<int> ReachableMasks(int populatedMask)
        {
            WardrobeLayerState.ValidateMask(populatedMask);
            var result = new List<int>();
            int ceiling = 2;
            while (true)
            {
                int visible = WardrobeLayerState.VisibleMask(populatedMask, ceiling);
                if (!result.Contains(visible)) result.Add(visible);
                if (visible == 0) break;
                int next = WardrobeLayerState.RemoveHighestCeiling(populatedMask, ceiling);
                if (next == ceiling) throw new InvalidOperationException("Layer state could not advance while compiling.");
                ceiling = next;
            }
            return result;
        }

        private static bool Role(WardrobeOutfitDefinition.Piece piece, string role) =>
            string.Equals(piece.role, role, StringComparison.OrdinalIgnoreCase);

        private static void ApplyCanonicalBarefootFootRegion(Mesh fitted, Mesh canonical,
            SkinnedMeshRenderer canonicalRenderer, List<string> issues)
        {
            if (fitted == null || canonical == null || canonicalRenderer == null)
            {
                issues.Add("footRegion.canonicalInputsMissing");
                return;
            }
            if (fitted.vertexCount != canonical.vertexCount || !fitted.triangles.SequenceEqual(canonical.triangles))
            {
                issues.Add("footRegion.topologyOrVertexOrderMismatch");
                return;
            }
            if (fitted.bindposes.Length != canonical.bindposes.Length ||
                canonicalRenderer.bones == null || canonicalRenderer.bones.Length != canonical.bindposes.Length)
            {
                issues.Add("footRegion.bindposeOrRendererOrderMismatch");
                return;
            }
            var weights = fitted.boneWeights;
            if (weights == null || weights.Length != fitted.vertexCount)
            {
                issues.Add("footRegion.skinWeightsMissing");
                return;
            }
            string[] boneNames = canonicalRenderer.bones.Select(b => b != null ? b.name : string.Empty).ToArray();
            var footBoneIndices = new HashSet<int>(Enumerable.Range(0, boneNames.Length)
                .Where(i => IsFootBone(boneNames[i])));
            if (footBoneIndices.Count == 0)
            {
                issues.Add("footRegion.noCanonicalFootBones");
                return;
            }
            var region = new List<int>();
            for (int i = 0; i < weights.Length; ++i)
            {
                var weight = weights[i];
                float footInfluence = 0f;
                if (footBoneIndices.Contains(weight.boneIndex0)) footInfluence += weight.weight0;
                if (footBoneIndices.Contains(weight.boneIndex1)) footInfluence += weight.weight1;
                if (footBoneIndices.Contains(weight.boneIndex2)) footInfluence += weight.weight2;
                if (footBoneIndices.Contains(weight.boneIndex3)) footInfluence += weight.weight3;
                if (footInfluence >= .5f) region.Add(i);
            }
            if (region.Count == 0)
            {
                issues.Add("footRegion.noCanonicalRegionVertices");
                return;
            }

            var positions = fitted.vertices;
            var canonicalPositions = canonical.vertices;
            for (int i = 0; i < region.Count; ++i) positions[region[i]] = canonicalPositions[region[i]];
            fitted.vertices = positions;
            CopyRegionVectors(fitted, canonical, region, fitted.normals, canonical.normals,
                "normals", (mesh, values) => mesh.normals = values, issues);
            CopyRegionTangents(fitted, canonical, region, fitted.tangents, canonical.tangents,
                "tangents", (mesh, values) => mesh.tangents = values, issues);
            if (issues.Count > 0) return;

            var bindposes = fitted.bindposes;
            var canonicalBindposes = canonical.bindposes;
            for (int i = 0; i < boneNames.Length; ++i)
                if (IsFootBone(boneNames[i])) bindposes[i] = canonicalBindposes[i];
            fitted.bindposes = bindposes;

            // Preserve the fitted mesh's shape list and every non-foot delta. For matching
            // canonical shapes, replace only the region deltas; no runtime resampling occurs.
            var shapeData = new List<(string name, float weight, Vector3[] vertices, Vector3[] normals, Vector3[] tangents)>();
            for (int shape = 0; shape < fitted.blendShapeCount; ++shape)
            {
                string shapeName = fitted.GetBlendShapeName(shape);
                int canonicalShape = canonical.GetBlendShapeIndex(shapeName);
                int frames = fitted.GetBlendShapeFrameCount(shape);
                if (canonicalShape >= 0 && frames != canonical.GetBlendShapeFrameCount(canonicalShape))
                {
                    issues.Add("footRegion.morphFrameMismatch:" + shapeName);
                    continue;
                }
                for (int frame = 0; frame < frames; ++frame)
                {
                    float frameWeight = fitted.GetBlendShapeFrameWeight(shape, frame);
                    if (canonicalShape >= 0 && Mathf.Abs(frameWeight -
                        canonical.GetBlendShapeFrameWeight(canonicalShape, frame)) > .0001f)
                    {
                        issues.Add("footRegion.morphWeightMismatch:" + shapeName);
                        break;
                    }
                    var delta = new Vector3[fitted.vertexCount];
                    var deltaNormals = new Vector3[fitted.vertexCount];
                    var deltaTangents = new Vector3[fitted.vertexCount];
                    fitted.GetBlendShapeFrameVertices(shape, frame, delta, deltaNormals, deltaTangents);
                    if (canonicalShape >= 0)
                    {
                        var canonicalDelta = new Vector3[canonical.vertexCount];
                        var canonicalDeltaNormals = new Vector3[canonical.vertexCount];
                        var canonicalDeltaTangents = new Vector3[canonical.vertexCount];
                        canonical.GetBlendShapeFrameVertices(canonicalShape, frame, canonicalDelta,
                            canonicalDeltaNormals, canonicalDeltaTangents);
                        foreach (int vertex in region)
                        {
                            delta[vertex] = canonicalDelta[vertex];
                            deltaNormals[vertex] = canonicalDeltaNormals[vertex];
                            deltaTangents[vertex] = canonicalDeltaTangents[vertex];
                        }
                    }
                    shapeData.Add((shapeName, frameWeight, delta, deltaNormals, deltaTangents));
                }
            }
            if (issues.Count > 0) return;
            fitted.ClearBlendShapes();
            int shapeCursor = 0;
            while (shapeCursor < shapeData.Count)
            {
                string name = shapeData[shapeCursor].name;
                while (shapeCursor < shapeData.Count && shapeData[shapeCursor].name == name)
                {
                    var frame = shapeData[shapeCursor++];
                    fitted.AddBlendShapeFrame(frame.name, frame.weight, frame.vertices, frame.normals, frame.tangents);
                }
            }
        }

        private static void CopyRegionVectors(Mesh fitted, Mesh canonical, List<int> region,
            Vector3[] fittedValues, Vector3[] canonicalValues, string label,
            Action<Mesh, Vector3[]> assign, List<string> issues)
        {
            bool fittedPresent = fittedValues != null && fittedValues.Length == fitted.vertexCount;
            bool canonicalPresent = canonicalValues != null && canonicalValues.Length == canonical.vertexCount;
            if (fittedPresent != canonicalPresent)
            {
                issues.Add("footRegion." + label + "ArrayMismatch");
                return;
            }
            if (!fittedPresent) return;
            foreach (int vertex in region) fittedValues[vertex] = canonicalValues[vertex];
            assign(fitted, fittedValues);
        }

        private static void CopyRegionTangents(Mesh fitted, Mesh canonical, List<int> region,
            Vector4[] fittedValues, Vector4[] canonicalValues, string label,
            Action<Mesh, Vector4[]> assign, List<string> issues)
        {
            bool fittedPresent = fittedValues != null && fittedValues.Length == fitted.vertexCount;
            bool canonicalPresent = canonicalValues != null && canonicalValues.Length == canonical.vertexCount;
            if (fittedPresent != canonicalPresent)
            {
                issues.Add("footRegion." + label + "ArrayMismatch");
                return;
            }
            if (!fittedPresent) return;
            foreach (int vertex in region) fittedValues[vertex] = canonicalValues[vertex];
            assign(fitted, fittedValues);
        }

        private static bool IsFootBone(string name) => !string.IsNullOrEmpty(name) &&
            (name.IndexOf("Foot", StringComparison.OrdinalIgnoreCase) >= 0 ||
             name.IndexOf("Toe", StringComparison.OrdinalIgnoreCase) >= 0 ||
             name.IndexOf("Metatarsals", StringComparison.OrdinalIgnoreCase) >= 0);

        private static string StableStateHash(WardrobePreset preset, int mask,
            WardrobePreset.Part[] parts, Mesh mesh, bool barefootFootRepair)
        {
            var text = new StringBuilder().Append(preset.PresetId).Append('|').Append(mask).Append('|');
            if (barefootFootRepair) text.Append(BarefootFootRegionRepairVersion).Append('|');
            foreach (var p in parts.OrderBy(p => p.sourcePieceId, StringComparer.Ordinal))
                text.Append(p.sourcePieceId).Append(':').Append(p.layer).Append('|');
            var uv = new List<Vector4>(); mesh.GetUVs(3, uv);
            foreach (var v in uv) text.Append(v.x.ToString("R", System.Globalization.CultureInfo.InvariantCulture)).Append(',')
                .Append(v.y.ToString("R", System.Globalization.CultureInfo.InvariantCulture)).Append(',')
                .Append(v.z.ToString("R", System.Globalization.CultureInfo.InvariantCulture)).Append(',')
                .Append(v.w.ToString("R", System.Globalization.CultureInfo.InvariantCulture)).Append('|');
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(text.ToString()))).Replace("-", "").ToLowerInvariant();
        }

        private static void EnsureFolder(string path)
        {
            if (string.IsNullOrEmpty(path) || AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/');
            if (slash <= 0) throw new InvalidDataException("Invalid Assets folder path: " + path);
            string parent = path.Substring(0, slash), name = path.Substring(slash + 1);
            EnsureFolder(parent);
            if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(parent, name);
        }
    }
}
