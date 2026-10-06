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
                    if (stateIssues.Count == 0)
                    {
                        string content = StableStateHash(preset, mask, parts, bodyMesh);
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
                    bodyMesh != null ? bodyMesh : package.reviewBody);
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

        private static string StableStateHash(WardrobePreset preset, int mask,
            WardrobePreset.Part[] parts, Mesh mesh)
        {
            var text = new StringBuilder().Append(preset.PresetId).Append('|').Append(mask).Append('|');
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
