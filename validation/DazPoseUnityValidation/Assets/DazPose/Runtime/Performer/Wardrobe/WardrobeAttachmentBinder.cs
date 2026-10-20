using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DazPose.Performer
{
    public sealed class WardrobeAttachmentLease : IDisposable
    {
        private GameObject instance;
        private readonly LaraWardrobe.Piece[] pieces;
        private readonly GameObject[] localBones;
        private bool disposed;
        internal WardrobeAttachmentLease(GameObject root, LaraWardrobe.Piece[] boundPieces, GameObject[] ownedLocalBones)
        { instance = root; pieces = boundPieces ?? Array.Empty<LaraWardrobe.Piece>(); localBones = ownedLocalBones ?? Array.Empty<GameObject>(); }
        public LaraWardrobe.Piece[] Pieces => pieces.ToArray();
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            foreach (var node in localBones)
                if (node != null) { if (Application.isPlaying) UnityEngine.Object.Destroy(node); else UnityEngine.Object.DestroyImmediate(node); }
            if (Application.isPlaying) UnityEngine.Object.Destroy(instance);
            else UnityEngine.Object.DestroyImmediate(instance);
            instance = null;
        }
    }

    /// <summary>One canonical/local bone binder shared by released prefabs and synthesized legacy package data.</summary>
    public static class WardrobeAttachmentBinder
    {
        public static WardrobeAttachmentLease Bind(WardrobeAttachmentDescriptor prefabDescriptor,
            SkinnedMeshRenderer body, Transform attachmentRoot, string expectedGeneration, string expectedSignature)
        {
            if (prefabDescriptor == null || body == null || body.sharedMesh == null)
                throw new InvalidOperationException("Wardrobe descriptor and canonical body renderer are required.");
            if (!prefabDescriptor.Validate(out string error)) throw new InvalidOperationException(error);
            if (prefabDescriptor.GenerationId != expectedGeneration || prefabDescriptor.CharacterSignature != expectedSignature)
                throw new InvalidOperationException("Wardrobe attachment generation/signature does not match the actor.");
            var templateParts = prefabDescriptor.Parts;
            var canonical = BuildCanonicalBoneMap(body);
            foreach (var part in templateParts) ValidatePartPlan(part, canonical);

            GameObject instance = null;
            var ownedLocalBones = new List<GameObject>();
            try
            {
                Transform parent = attachmentRoot != null ? attachmentRoot : body.transform;
                // Package meshes and bind poses are expressed in the canonical body's frame.
                // Starting under the body preserves that frame even when the actor root is offset/scaled.
                instance = UnityEngine.Object.Instantiate(prefabDescriptor.gameObject, body.transform, false);
                if (parent != body.transform) instance.transform.SetParent(parent, true);
                instance.name = prefabDescriptor.StableKey;
                var descriptor = instance.GetComponent<WardrobeAttachmentDescriptor>();
                if (descriptor == null || !descriptor.Validate(out error)) throw new InvalidOperationException(error ?? "Instantiated wardrobe prefab lost its descriptor.");
                var bound = new List<LaraWardrobe.Piece>();
                foreach (var part in descriptor.Parts)
                {
                    var renderer = part.renderer;
                    if (renderer == null) throw new InvalidOperationException("Wardrobe prefab renderer reference was not remapped: " + part.stablePieceId);
                    var bones = BindBones(part, canonical, ownedLocalBones);
                    renderer.rootBone = ResolveCanonical(canonical, part.rootBoneName, "root bone");
                    renderer.bones = bones;
                    CopyRendererPresentation(body, renderer);
                    renderer.enabled = false;
                    renderer.updateWhenOffscreen = true;
                    renderer.allowOcclusionWhenDynamic = false;
                    bound.Add(new LaraWardrobe.Piece
                    {
                        name = prefabDescriptor.StableKey + "/" + part.stablePieceId,
                        stableKey = string.IsNullOrEmpty(part.stableKey) ? prefabDescriptor.StableKey : part.stableKey,
                        layer = part.layer,
                        renderer = renderer,
                        shell = part.shell,
                        visible = false,
                        persistentHair = prefabDescriptor.AttachmentRole == WardrobeAttachmentRole.Hair ||
                            string.Equals(part.role, "hair", StringComparison.OrdinalIgnoreCase),
                        coverageChannel = part.coverageChannel,
                        materialOpacities = Enumerable.Repeat(-1f, renderer.sharedMaterials.Length).ToArray()
                    });
                }
                return new WardrobeAttachmentLease(instance, bound.ToArray(), ownedLocalBones.ToArray());
            }
            catch
            {
                if (instance != null)
                {
                    if (Application.isPlaying) UnityEngine.Object.Destroy(instance);
                    else UnityEngine.Object.DestroyImmediate(instance);
                }
                foreach (var node in ownedLocalBones)
                    if (node != null) { if (Application.isPlaying) UnityEngine.Object.Destroy(node); else UnityEngine.Object.DestroyImmediate(node); }
                throw;
            }
        }

        public static WardrobeAttachmentLease BindLegacyPreset(WardrobePreset preset, SkinnedMeshRenderer body,
            Transform attachmentRoot, string generationId)
        {
            if (preset == null || preset.Package == null) throw new InvalidOperationException("Legacy wardrobe preset package is missing.");
            var root = new GameObject("Legacy wardrobe descriptor " + preset.PresetId);
            try
            {
                var descriptor = root.AddComponent<WardrobeAttachmentDescriptor>();
                var records = new List<WardrobeAttachmentDescriptor.PartRecord>();
                foreach (var definition in preset.Package.pieces ?? Array.Empty<WardrobeOutfitDefinition.Piece>())
                {
                    var pieceNode = new GameObject(definition.id);
                    pieceNode.transform.SetParent(root.transform, false);
                    var renderer = pieceNode.AddComponent<SkinnedMeshRenderer>();
                    renderer.sharedMesh = definition.mesh;
                    renderer.sharedMaterials = definition.materials != null ? (Material[])definition.materials.Clone() : Array.Empty<Material>();
                    renderer.enabled = false;
                    var locals = (definition.localBones ?? Array.Empty<WardrobeOutfitDefinition.LocalBone>())
                        .Select(b => new WardrobeAttachmentDescriptor.LocalBoneRecord
                        { name = b.name, parent = b.parent, position = b.position, rotation = b.rotation, scale = b.scale }).ToArray();
                    records.Add(new WardrobeAttachmentDescriptor.PartRecord
                    {
                        stablePieceId = definition.id,
                        stableKey = string.Equals(definition.role, "hair", StringComparison.OrdinalIgnoreCase)
                            ? preset.PresetId + "/" + definition.id : preset.PresetId,
                        role = definition.role,
                        rootBoneName = body.rootBone != null ? body.rootBone.name : string.Empty,
                        renderer = renderer,
                        layer = ResolvePresetLayer(preset, definition.id),
                        coverageChannel = definition.coverageChannel,
                        shell = definition.shell,
                        requiresBentFootPose = definition.requiresBentFootPose,
                        boneNames = definition.boneNames != null ? (string[])definition.boneNames.Clone() : Array.Empty<string>(),
                        localBones = locals
                    });
                }
                descriptor.ConfigureTransient(WardrobeAttachmentRole.Clothing, generationId, preset.CharacterSignature,
                    preset.PresetId, preset.PresetId, "legacy:" + preset.PresetId, records.ToArray());
                return Bind(descriptor, body, attachmentRoot, generationId, preset.CharacterSignature);
            }
            finally
            {
                if (Application.isPlaying) UnityEngine.Object.Destroy(root);
                else UnityEngine.Object.DestroyImmediate(root);
            }
        }

        public static WardrobeRigManifest ValidateOwner(PerformerWardrobe owner, SkinnedMeshRenderer body,
            WardrobeRigManifest manifest)
        {
            string error = null;
            if (owner == null || body == null || owner.Body != body || !body.transform.IsChildOf(owner.transform))
                throw new InvalidOperationException("Wardrobe owner and body renderer must belong to the same actor.");
            if (manifest == null || !manifest.Validate(owner.Catalog, out error))
                throw new InvalidOperationException(error ?? "Wardrobe rig manifest is missing.");
            return manifest;
        }

        private static Dictionary<string, Transform> BuildCanonicalBoneMap(SkinnedMeshRenderer body)
        {
            var names = body.bones.Where(b => b != null).Select(b => b.name).ToArray();
            var canonical = new HashSet<string>(StringComparer.Ordinal);
            foreach (var name in names)
                if (!canonical.Add(name)) throw new InvalidOperationException("Canonical skeleton bone name is ambiguous: " + name);
            var result = new Dictionary<string, Transform>(StringComparer.Ordinal);
            foreach (var bone in body.bones)
            {
                if (bone == null) continue;
                result.Add(bone.name, bone);
            }
            return result;
        }

        private static int ResolvePresetLayer(WardrobePreset preset, string pieceId)
        {
            foreach (var part in preset.Parts) if (part.sourcePieceId == pieceId) return part.layer;
            return 0;
        }

        private static void ValidatePartPlan(WardrobeAttachmentDescriptor.PartRecord part, Dictionary<string, Transform> canonical)
        {
            var localFacts = (part.localBones ?? Array.Empty<WardrobeAttachmentDescriptor.LocalBoneRecord>())
                .Select(local => local == null ? new WardrobeLocalBoneFact(null, null) : new WardrobeLocalBoneFact(local.name, local.parent)).ToArray();
            if (!WardrobeBoneBindingPlan.TryCreate(canonical.Keys.ToArray(), part.boneNames,
                part.rootBoneName, localFacts, out _, out string error))
                throw new InvalidOperationException(error + " (piece " + part.stablePieceId + ")");
        }

        private static Transform[] BindBones(WardrobeAttachmentDescriptor.PartRecord part, Dictionary<string, Transform> canonical,
            List<GameObject> ownedLocalBones)
        {
            var resolved = new Dictionary<string, Transform>(canonical, StringComparer.Ordinal);
            var byName = (part.localBones ?? Array.Empty<WardrobeAttachmentDescriptor.LocalBoneRecord>()).ToDictionary(x => x.name, StringComparer.Ordinal);
            if (!WardrobeBoneBindingPlan.TryCreate(canonical.Keys.ToArray(), part.boneNames, part.rootBoneName,
                byName.Values.Select(local => new WardrobeLocalBoneFact(local.name, local.parent)).ToArray(), out var order, out string error))
                throw new InvalidOperationException(error + " (piece " + part.stablePieceId + ")");
            foreach (var name in order)
            {
                var local = byName[name];
                var node = new GameObject(local.name).transform;
                ownedLocalBones.Add(node.gameObject);
                node.SetParent(resolved[local.parent], false);
                node.localPosition = local.position; node.localRotation = local.rotation; node.localScale = local.scale;
                resolved.Add(local.name, node);
            }
            return part.boneNames.Select(name => resolved[name]).ToArray();
        }

        private static Transform ResolveCanonical(Dictionary<string, Transform> canonical, string name, string role)
        {
            if (string.IsNullOrWhiteSpace(name) || !canonical.TryGetValue(name, out var value))
                throw new InvalidOperationException("Canonical " + role + " is missing: " + name);
            return value;
        }

        private static void CopyRendererPresentation(SkinnedMeshRenderer source, SkinnedMeshRenderer target)
        {
            target.renderingLayerMask = source.renderingLayerMask;
            target.shadowCastingMode = source.shadowCastingMode;
            target.receiveShadows = source.receiveShadows;
            target.lightProbeUsage = source.lightProbeUsage;
            target.reflectionProbeUsage = source.reflectionProbeUsage;
            target.probeAnchor = source.probeAnchor;
            target.motionVectorGenerationMode = source.motionVectorGenerationMode;
            target.sortingLayerID = source.sortingLayerID;
            target.sortingOrder = source.sortingOrder;
            target.localBounds = source.localBounds;
            target.forceRenderingOff = source.forceRenderingOff;
        }
    }
}
