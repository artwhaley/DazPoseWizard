using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DazPose.Performer
{
    /// <summary>One actor's outfit selection, ordered layer requests and attachment instances.</summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Performer/Wardrobe Runtime")]
    public sealed class PerformerWardrobe : MonoBehaviour
    {
        private enum RequestKind { Outfit, Remove, Add, Hair, SceneRecall }
        private sealed class Request
        {
            public RequestKind kind;
            public WardrobePreset preset;
            public string id;
            public WardrobeTransition transition;
            public bool explicitUnclothed;
            public SceneWardrobeSnapshot sceneSnapshot;
            public object transitionOwner;
            public Action<WardrobeChangeResult> complete;
            public bool completed;
            public void Complete(WardrobeChangeResult result)
            {
                if (completed) return;
                completed = true;
                complete?.Invoke(result);
            }
        }

        private sealed class RuntimePiece
        {
            public string key, sourceId;
            public WardrobePreset preset;
            public WardrobeOutfitDefinition.Piece definition;
            public LaraWardrobe.Piece inventory;
            public int layer;
            public bool hair, requiresBentFootPose;
        }

        [SerializeField] private WardrobeCatalog catalog;
        [SerializeField] private WardrobeConfiguration configuration;
        [SerializeField] private SkinnedMeshRenderer body;
        [SerializeField] private SuccubusPerformer performer;
        [SerializeField] private LaraWardrobe rendererBinder;
        [SerializeField] private bool allowCandidatePreview;

        private readonly Queue<Request> _queue = new Queue<Request>();
        private readonly Dictionary<string, RuntimePiece> _pieces = new Dictionary<string, RuntimePiece>(StringComparer.Ordinal);
        private RuntimePiece[] _inventory = Array.Empty<RuntimePiece>();
        private WardrobePreset _selectedPreset;
        private WardrobeFitState _currentFit;
        private int _layerCeiling = -1;
        private string _currentHairKey;
        private bool _processing;
        private Request _activeRequest;
        private FootwearPoseDriver _footwearDriver;
        private bool _initialized;
        private bool _isDisabling;
        private int _lastConfigurationRevision;

        public event Action<WardrobeState> WardrobeChanged;
        public WardrobeState CurrentWardrobe => BuildState(_activeRequest != null || _queue.Count > 0);

        public WardrobeCatalog Catalog => catalog;
        public WardrobeConfiguration Configuration => configuration;
        public SkinnedMeshRenderer Body => body;
        public bool AllowsCandidatePreview => allowCandidatePreview;

        public void ConfigureBinding(WardrobeCatalog sourceCatalog, WardrobeConfiguration sourceConfiguration,
            SkinnedMeshRenderer sourceBody, SuccubusPerformer owner, bool candidatePreview = false)
        {
            if (Application.isPlaying && _initialized)
                throw new InvalidOperationException("A running performer wardrobe cannot replace its catalog binding.");
            catalog = sourceCatalog; configuration = sourceConfiguration; body = sourceBody; performer = owner;
            allowCandidatePreview = candidatePreview;
            rendererBinder = owner != null ? owner.GetComponent<LaraWardrobe>() : null;
        }

        private void Awake()
        {
            if (performer == null) performer = GetComponent<SuccubusPerformer>();
            if (rendererBinder == null) rendererBinder = GetComponent<LaraWardrobe>();
            if (body == null) body = FindBody();
            if (Application.isPlaying) EnsureInitialized();
        }

        private void OnEnable()
        {
            _isDisabling = false;
            if (Application.isPlaying) EnsureInitialized();
        }

        private void OnDisable()
        {
            _isDisabling = true;
            var disabled = new WardrobeChangeResult(WardrobeChangeStatus.PerformerDisabled,
                BuildState(false), BuildState(false), message: "Performer wardrobe was disabled before this request ran.");
            _activeRequest?.Complete(disabled);
            _activeRequest = null;
            while (_queue.Count > 0) _queue.Dequeue().Complete(disabled);
        }

        private void OnDestroy()
        {
            foreach (var item in _inventory)
                if (item.inventory.renderer != null) Destroy(item.inventory.renderer.gameObject);
            _pieces.Clear();
        }

        private SkinnedMeshRenderer FindBody()
        {
            return GetComponentsInChildren<SkinnedMeshRenderer>(true)
                .FirstOrDefault(r => r.sharedMesh != null && r.sharedMesh.GetBlendShapeIndex("CapturedOpening") >= 0);
        }

        private void EnsureInitialized()
        {
            if (_initialized || !Application.isPlaying) return;
            if (catalog == null)
            { Debug.LogError("Performer wardrobe catalog is missing.", this); return; }
            if (!catalog.Validate(out string catalogError))
            { Debug.LogError("Performer wardrobe catalog is invalid: " + catalogError, this); return; }
            if (body == null) body = FindBody();
            if (body == null || body.sharedMesh == null)
            { Debug.LogError("Performer wardrobe has no canonical body renderer.", this); return; }
            if (performer == null) performer = GetComponent<SuccubusPerformer>();
            try
            {
                BuildPieceInventory();
                rendererBinder ??= gameObject.AddComponent<LaraWardrobe>();
                rendererBinder.Configure(body, _inventory.Select(p => p.inventory).ToArray());
                _footwearDriver = GetComponent<FootwearPoseDriver>();
                _selectedPreset = null;
                _layerCeiling = -1;
                _currentFit = catalog.Presets.SelectMany(p => p.FitStates).FirstOrDefault(s => s.VisibleMask == 0);
                _lastConfigurationRevision = configuration != null ? configuration.Revision : 0;
                ApplyRendererVisibility(0, null, _currentHairKey);
                _initialized = true;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
                Debug.LogError("Performer wardrobe initialization failed; no outfit commands are accepted.", this);
            }
        }

        private void BuildPieceInventory()
        {
            var sourcePresets = catalog.Presets;
            var canonicalBones = new Dictionary<string, Transform>(StringComparer.Ordinal);
            foreach (var bone in body.bones)
            {
                if (bone == null) continue;
                if (!canonicalBones.ContainsKey(bone.name)) canonicalBones.Add(bone.name, bone);
            }
            foreach (var preset in sourcePresets)
            {
                if (preset == null) throw new InvalidOperationException("Invalid preset data: null preset");
                if (!preset.Validate(out string error)) throw new InvalidOperationException("Invalid preset data: " + error);
                foreach (var piece in preset.Package.pieces)
                {
                    if (piece.mesh == null || piece.materials == null || piece.materials.Length != piece.mesh.subMeshCount)
                        throw new InvalidOperationException("Piece mesh/material binding is invalid: " + preset.PresetId + "/" + piece.id);
                    var localNames = new HashSet<string>(StringComparer.Ordinal);
                    foreach (var local in piece.localBones ?? Array.Empty<WardrobeOutfitDefinition.LocalBone>())
                    {
                        if (string.IsNullOrWhiteSpace(local.name) || !localNames.Add(local.name))
                            throw new InvalidOperationException("Duplicate local bone in " + preset.PresetId + "/" + piece.id);
                        if (!canonicalBones.ContainsKey(local.parent) && !(piece.localBones ?? Array.Empty<WardrobeOutfitDefinition.LocalBone>()).Any(b => b.name == local.parent))
                            throw new InvalidOperationException("Local bone parent is missing: " + local.parent);
                    }
                    foreach (var boneName in piece.boneNames ?? Array.Empty<string>())
                        if (!canonicalBones.ContainsKey(boneName) && !localNames.Contains(boneName))
                            throw new InvalidOperationException("Attachment references an unresolved bone: " + boneName);
                }
            }

            var newInventory = new List<RuntimePiece>();
            foreach (var preset in sourcePresets)
            foreach (var definition in preset.Package.pieces)
            {
                string key = preset.PresetId + "/" + definition.id;
                if (_pieces.ContainsKey(key)) throw new InvalidOperationException("Duplicate stable wardrobe piece ID: " + key);
                var bones = new Dictionary<string, Transform>(canonicalBones, StringComparer.Ordinal);
                foreach (var local in definition.localBones ?? Array.Empty<WardrobeOutfitDefinition.LocalBone>())
                {
                    if (!bones.TryGetValue(local.parent, out var parent)) throw new InvalidOperationException("Could not resolve accessory bone parent " + local.parent);
                    var node = new GameObject(local.name).transform;
                    node.SetParent(parent, false);
                    node.localPosition = local.position; node.localRotation = local.rotation; node.localScale = local.scale;
                    bones.Add(local.name, node);
                }
                var nodeObject = new GameObject("Wardrobe " + key);
                nodeObject.transform.SetParent(body.transform, false);
                var renderer = nodeObject.AddComponent<SkinnedMeshRenderer>();
                renderer.sharedMesh = definition.mesh;
                var materials = (Material[])definition.materials.Clone();
                var materialOpacities = Enumerable.Repeat(-1f, materials.Length).ToArray();
                if (ConfigurationApplies())
                foreach (var materialOverride in configuration.MaterialOverrides.Where(m => m != null &&
                    m.presetId == preset.PresetId && m.sourcePieceId == definition.id))
                {
                    if (!int.TryParse(materialOverride.materialSlotId, out int slot) || slot < 0 || slot >= materials.Length)
                        throw new InvalidOperationException("Material override must name a valid material slot index: " + key + "/" + materialOverride.materialSlotId);
                    if (materialOverride.material != null) materials[slot] = materialOverride.material;
                    if (materialOverride.opacityOverride) materialOpacities[slot] = materialOverride.opacity;
                }
                renderer.sharedMaterials = materials;
                renderer.rootBone = body.rootBone;
                renderer.bones = (definition.boneNames ?? Array.Empty<string>()).Select(name => bones[name]).ToArray();
                renderer.renderingLayerMask = body.renderingLayerMask;
                renderer.updateWhenOffscreen = true;
                renderer.allowOcclusionWhenDynamic = false;
                renderer.enabled = false;
                bool hair = string.Equals(definition.role, "hair", StringComparison.OrdinalIgnoreCase);
                int layer = ResolveLayer(preset, definition.id);
                var record = new RuntimePiece
                {
                    key = key, sourceId = definition.id, preset = preset,
                    definition = definition, layer = layer, hair = hair,
                    requiresBentFootPose = definition.requiresBentFootPose ||
                        string.Equals(definition.role, "footwear", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(definition.role, "hosiery", StringComparison.OrdinalIgnoreCase),
                    inventory = new LaraWardrobe.Piece
                    { name = key, renderer = renderer, shell = definition.shell, visible = false,
                        persistentHair = hair, coverageChannel = definition.coverageChannel,
                        materialOpacities = materialOpacities }
                };
                _pieces.Add(key, record); newInventory.Add(record);
            }
            _inventory = newInventory.ToArray();
        }

        private int ResolveLayer(WardrobePreset preset, string pieceId)
        {
            if (ConfigurationApplies())
            {
                foreach (var row in configuration.Assignments)
                    if (row.presetId == preset.PresetId && row.sourcePieceId == pieceId) return row.layer;
            }
            foreach (var part in preset.Parts) if (part.sourcePieceId == pieceId) return part.layer;
            // Hair lives outside the clothing layers.
            return 0;
        }

        private bool ConfigurationApplies() => configuration != null &&
            (!catalog.IsReleased || allowCandidatePreview || configuration.GenerationHash == catalog.ReleasedGenerationId);

        private readonly struct Selection
        {
            public readonly WardrobePreset Preset;
            public readonly int Ceiling, Populated, Visible;
            public readonly WardrobeFitState Fit;
            public readonly string HairKey;
            public Selection(WardrobePreset preset, int ceiling, int populated, int visible, WardrobeFitState fit, string hairKey)
            { Preset = preset; Ceiling = ceiling; Populated = populated; Visible = visible; Fit = fit; HairKey = hairKey; }
        }

        private bool TryPrepare(WardrobePreset preset, int ceiling, string hairKey, out Selection selection,
            out WardrobeFailureCode failure, out string message)
        {
            selection = default; failure = WardrobeFailureCode.None; message = null;
            if (preset == null) { failure = WardrobeFailureCode.UnknownPreset; message = "Preset is missing."; return false; }
            if (!preset.Validate(out message)) { failure = WardrobeFailureCode.MissingAsset; return false; }
            int populated = 0;
            foreach (var part in preset.Parts)
            {
                var definition = preset.Package.pieces.FirstOrDefault(p => p.id == part.sourcePieceId);
                if (definition == null) { failure = WardrobeFailureCode.MissingAsset; message = "Package piece not found: " + part.sourcePieceId; return false; }
                if (!_pieces.ContainsKey(preset.PresetId + "/" + part.sourcePieceId))
                { failure = WardrobeFailureCode.MissingAsset; message = "Runtime piece is not bound: " + part.sourcePieceId; return false; }
                int layer = ResolveLayer(preset, part.sourcePieceId);
                if (layer < 0 || layer > 2) { failure = WardrobeFailureCode.ConflictingItems; message = "Invalid layer assignment: " + part.sourcePieceId; return false; }
                populated |= 1 << layer;
                foreach (var owner in part.requiredOwnerPieceIds ?? Array.Empty<string>())
                    if (preset.Parts.All(p => p.sourcePieceId != owner))
                    { failure = WardrobeFailureCode.ConflictingItems; message = "Shell dependency is not in the outfit: " + owner; return false; }
            }
            WardrobeLayerState.ValidateCeiling(ceiling);
            int visible = WardrobeLayerState.VisibleMask(populated, ceiling);
            var fit = preset.FitStates.FirstOrDefault(s => s.VisibleMask == visible);
            if (fit == null || fit.Status != WardrobeFitStatus.Validated)
            { failure = WardrobeFailureCode.FitNotAvailable; message = "No validated fit state for visible layer mask " + visible + "."; return false; }
            if (fit.BodyMesh == null || fit.SurfaceBindings == null || fit.DissolveProfile == null ||
                fit.SurfaceBindings.SourceMesh != fit.BodyMesh || fit.SurfaceBindings.BindingCount != PerformerSurfaceBindingAsset.RequiredBindingCount ||
                fit.DissolveProfile.SurfaceBindings != fit.SurfaceBindings)
            { failure = WardrobeFailureCode.InvalidBindings; message = "Fit state body and particle bindings do not match."; return false; }
            if (body == null || fit.CharacterSignature != preset.CharacterSignature ||
                !MorphsCompatible(preset.Package.characterReference, fit.BodyMesh) ||
                !MorphsCompatible(preset.Package.characterReference, body.sharedMesh))
            { failure = WardrobeFailureCode.IncompatibleCharacter; message = "Body morph/topology contract differs from this preset."; return false; }
            if (preset.HairAction == WardrobeHairAction.Set && !_pieces.ContainsKey(preset.PresetId + "/" + preset.HairId))
            { failure = WardrobeFailureCode.MissingAsset; message = "Explicit hair asset is not bound."; return false; }
            if (!string.IsNullOrEmpty(hairKey) && !_pieces.ContainsKey(hairKey))
            { failure = WardrobeFailureCode.MissingAsset; message = "Retained hairstyle is not bound: " + hairKey; return false; }
            selection = new Selection(preset, ceiling, populated, visible, fit, hairKey);
            return true;
        }

        private static bool MorphsCompatible(Mesh source, Mesh target)
        {
            if (source == null || target == null || source.vertexCount != target.vertexCount ||
                !source.triangles.SequenceEqual(target.triangles)) return false;
            for (int i = 0; i < source.blendShapeCount; ++i)
            {
                int other = target.GetBlendShapeIndex(source.GetBlendShapeName(i));
                if (other < 0 || target.GetBlendShapeFrameCount(other) != source.GetBlendShapeFrameCount(i)) return false;
            }
            return true;
        }

        private Dictionary<string, float> CaptureMorphs()
        {
            var weights = new Dictionary<string, float>(StringComparer.Ordinal);
            if (body == null || body.sharedMesh == null) return weights;
            for (int i = 0; i < body.sharedMesh.blendShapeCount; ++i)
                weights[body.sharedMesh.GetBlendShapeName(i)] = body.GetBlendShapeWeight(i);
            return weights;
        }

        private void RestoreMorphs(Dictionary<string, float> weights)
        {
            for (int i = 0; i < body.sharedMesh.blendShapeCount; ++i)
                if (weights.TryGetValue(body.sharedMesh.GetBlendShapeName(i), out float value)) body.SetBlendShapeWeight(i, value);
        }

        private WardrobeChangeResult ApplyRequest(Request request)
        {
            var before = BuildState(false);
            if (!_initialized) return Failure(before, WardrobeFailureCode.MissingAsset, "Wardrobe runtime is not initialized.");

            WardrobePreset nextPreset = _selectedPreset;
            int nextCeiling = _layerCeiling;
            string nextHair = _currentHairKey;
            if (request.kind == RequestKind.SceneRecall)
            {
                if (!TryResolveSceneSnapshot(request.sceneSnapshot, out nextPreset, out nextCeiling, out nextHair,
                    out var snapshotFailure, out string snapshotError))
                    return Failure(before, snapshotFailure, snapshotError);
            }
            else if (request.kind == RequestKind.Outfit)
            {
                if (request.explicitUnclothed)
                {
                    nextPreset = null; nextCeiling = -1;
                }
                else
                {
                    WardrobePreset resolved = null;
                    string resolveError = null;
                    if (request.preset == null && !catalog.TryResolve(request.id, out resolved, out resolveError))
                        return Failure(before, resolveError == "AmbiguousAlias" ? WardrobeFailureCode.AmbiguousAlias :
                            resolveError == "NotReleased" ? WardrobeFailureCode.NotReleased : WardrobeFailureCode.UnknownPreset, resolveError);
                    nextPreset = request.preset ?? resolved;
                    if ((!catalog.IsReleased && !allowCandidatePreview) || !catalog.Presets.Contains(nextPreset))
                        return Failure(before, WardrobeFailureCode.NotReleased, "Preset is not part of the released catalog.");
                    if (ReferenceEquals(nextPreset, _selectedPreset) &&
                        VisibleMask(_selectedPreset, _layerCeiling) == PopulatedMask(_selectedPreset))
                        return new WardrobeChangeResult(WardrobeChangeStatus.AlreadyEquipped, before, before);
                    nextCeiling = 2;
                    if (nextPreset.HairAction == WardrobeHairAction.Set) nextHair = nextPreset.PresetId + "/" + nextPreset.HairId;
                    else if (nextPreset.HairAction == WardrobeHairAction.Clear) nextHair = null;
                }
            }
            else if (request.kind == RequestKind.Hair)
            {
                string requested = (request.id ?? string.Empty).Trim();
                if (string.Equals(requested, "none", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(requested, "clear", StringComparison.OrdinalIgnoreCase)) requested = string.Empty;
                if (!string.IsNullOrEmpty(requested) &&
                    (!_pieces.TryGetValue(requested, out var hairPiece) || !hairPiece.hair))
                    return Failure(before, WardrobeFailureCode.MissingAsset, "Hair asset is not in this wardrobe inventory: " + requested);
                nextHair = requested;
                if (string.Equals(nextHair, _currentHairKey, StringComparison.Ordinal))
                    return new WardrobeChangeResult(WardrobeChangeStatus.NoChange, before, before,
                        noChangeReason: WardrobeNoChangeReason.None);
            }
            else if (nextPreset == null)
            {
                var none = new WardrobeChangeResult(WardrobeChangeStatus.NoChange, before, before,
                    noChangeReason: WardrobeNoChangeReason.NoOutfitSelected);
                return none;
            }
            else if (request.kind == RequestKind.Remove)
            {
                int currentVisible = VisibleMask(nextPreset, nextCeiling);
                if (currentVisible == 0)
                    return new WardrobeChangeResult(WardrobeChangeStatus.NoChange, before, before,
                        noChangeReason: WardrobeNoChangeReason.AlreadyNaked);
                nextCeiling = WardrobeLayerState.RemoveHighestCeiling(PopulatedMask(nextPreset), nextCeiling);
            }
            else
            {
                int currentVisible = VisibleMask(nextPreset, nextCeiling);
                if (WardrobeLayerState.IsFullyDressed(PopulatedMask(nextPreset), nextCeiling))
                    return new WardrobeChangeResult(WardrobeChangeStatus.NoChange, before, before,
                        noChangeReason: WardrobeNoChangeReason.FullyDressed);
                nextCeiling = WardrobeLayerState.AddLowestCeiling(PopulatedMask(nextPreset), nextCeiling);
            }

            WardrobeFitState targetFit;
            Selection target;
            if (nextPreset == null)
            {
                targetFit = catalog.Presets.SelectMany(p => p.FitStates).FirstOrDefault(s => s.VisibleMask == 0);
                if (targetFit == null || targetFit.Status != WardrobeFitStatus.Validated)
                    return Failure(before, WardrobeFailureCode.FitNotAvailable, "No canonical naked fit state is released.");
                var owner = catalog.Presets.FirstOrDefault(p => p.FitStates.Contains(targetFit));
                if (owner == null) return Failure(before, WardrobeFailureCode.FitNotAvailable, "No canonical naked fit owner is released.");
                if (!TryPrepare(owner, -1, nextHair, out target, out var code, out var reason))
                    return Failure(before, code, reason);
                target = new Selection(null, -1, 0, 0, targetFit, nextHair);
            }
            else
            {
                if (!TryPrepare(nextPreset, nextCeiling, nextHair, out target, out var code, out var reason))
                    return Failure(before, code, reason);
                targetFit = target.Fit;
            }

            if (performer != null && performer.IsDissolving)
                return Failure(before, WardrobeFailureCode.EffectRebindFailed,
                    "The active dissolve owns the performer's effect profile; retry the wardrobe change after it settles.");

            var priorPreset = _selectedPreset;
            var priorFit = _currentFit;
            int priorCeiling = _layerCeiling;
            string priorHair = _currentHairKey;
            Mesh priorMesh = body.sharedMesh;
            var weights = CaptureMorphs();
            var priorVisible = _inventory.Select(p => p.inventory.visible).ToArray();
            try
            {
                body.sharedMesh = targetFit.BodyMesh;
                RestoreMorphs(weights);
                var anatomy = performer != null ? performer.GetComponent<LaraAnatomyControls>() : null;
                anatomy?.RebindPreservingValues(body);
                if (performer == null) throw new InvalidOperationException("Performer dissolve binding is unavailable.");
                if (!performer.TrySetWardrobeEffectProfile(targetFit.DissolveProfile,
                    targetFit.SurfaceBindings, request.transitionOwner, out string effectError))
                    throw new InvalidOperationException(effectError ?? "Performer dissolve binding is unavailable.");
                var sourceFootwear = target.Preset != null ? target.Preset.Footwear : null;
                _footwearDriver ??= GetComponent<FootwearPoseDriver>() ?? gameObject.AddComponent<FootwearPoseDriver>();
                if (!_footwearDriver.Configure(body, sourceFootwear, targetFit.Footwear, out string fitError))
                    throw new InvalidOperationException(fitError);
                _selectedPreset = target.Preset; _layerCeiling = target.Ceiling; _currentHairKey = target.HairKey;
                _currentFit = targetFit;
                ApplyRendererVisibility(target.Visible, target.Preset, target.HairKey);
                _lastConfigurationRevision = configuration != null ? configuration.Revision : 0;
                var after = BuildState(false);
                WardrobeChanged?.Invoke(after);
                return new WardrobeChangeResult(WardrobeChangeStatus.Applied, before, after);
            }
            catch (Exception exception)
            {
                body.sharedMesh = priorMesh;
                RestoreMorphs(weights);
                var anatomy = performer != null ? performer.GetComponent<LaraAnatomyControls>() : null;
                anatomy?.RebindPreservingValues(body);
                _selectedPreset = priorPreset; _currentFit = priorFit;
                _layerCeiling = priorCeiling; _currentHairKey = priorHair;
                for (int i = 0; i < _inventory.Length && i < priorVisible.Length; ++i)
                { var p = _inventory[i]; p.inventory.visible = priorVisible[i]; _inventory[i] = p; }
                ApplyRendererVisibility(VisibleMask(priorPreset, priorCeiling), priorPreset, priorHair);
                if (priorFit != null && performer != null)
                    performer.TrySetWardrobeEffectProfile(priorFit.DissolveProfile, priorFit.SurfaceBindings,
                        request.transitionOwner, out _);
                if (_footwearDriver != null)
                    _footwearDriver.Configure(body, priorPreset != null ? priorPreset.Footwear : null,
                        priorFit != null ? priorFit.Footwear : null, out _);
                Debug.LogException(exception, this);
                return Failure(before, WardrobeFailureCode.EffectRebindFailed, exception.Message);
            }
        }

        private void ApplyRendererVisibility(int visibleMask, WardrobePreset preset, string hairKey)
        {
            foreach (var piece in _inventory)
            {
                bool show = piece.hair ? piece.key == hairKey :
                    preset != null && piece.preset == preset && (visibleMask & (1 << ResolveLayer(preset, piece.sourceId))) != 0;
                piece.inventory.visible = show;
            }
            if (rendererBinder != null)
            {
                rendererBinder.Apply();
            }
        }

        private int PopulatedMask(WardrobePreset preset)
        {
            if (preset == null) return 0;
            int result = 0;
            foreach (var part in preset.Parts) result |= 1 << ResolveLayer(preset, part.sourcePieceId);
            return result;
        }
        private int VisibleMask(WardrobePreset preset, int ceiling) => WardrobeLayerState.VisibleMask(PopulatedMask(preset), ceiling);

        private WardrobeState BuildState(bool changing)
        {
            int populated = PopulatedMask(_selectedPreset);
            int visible = WardrobeLayerState.VisibleMask(populated, _layerCeiling);
            string[][] ids = { Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>() };
            string[][] names = { Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>() };
            if (_selectedPreset != null)
            {
                for (int layer = 0; layer < 3; ++layer)
                {
                    var parts = _selectedPreset.Parts.Where(p => ResolveLayer(_selectedPreset, p.sourcePieceId) == layer).ToArray();
                    ids[layer] = parts.Select(p => p.sourcePieceId).ToArray();
                    names[layer] = parts.Select(p => _selectedPreset.Package.pieces.First(x => x.id == p.sourcePieceId).sourceNode).ToArray();
                }
            }
            bool naked = visible == 0;
            string footwear = _selectedPreset != null && (visible & PopulatedMaskForRole(_selectedPreset, "footwear")) != 0
                ? _selectedPreset.Footwear != null ? _selectedPreset.Footwear.sourceName : "unknown"
                : _currentFit != null && _currentFit.Footwear.BentFootPoseActive ? "bent-foot pose; shoes removed" : "barefoot";
            return new WardrobeState(_selectedPreset != null ? _selectedPreset.PresetId : null,
                _selectedPreset != null ? _selectedPreset.DisplayName : "Unclothed",
                _selectedPreset != null ? _lastConfigurationRevision : 0, naked, changing,
                _layerCeiling, populated, visible, footwear,
                _currentFit != null && _currentFit.Footwear.BentFootPoseActive, _currentHairKey, ids, names);
        }

        private int PopulatedMaskForRole(WardrobePreset preset, string role)
        {
            int mask = 0;
            foreach (var part in preset.Parts)
            {
                var source = preset.Package.pieces.FirstOrDefault(p => p.id == part.sourcePieceId);
                if (source != null && string.Equals(source.role, role, StringComparison.OrdinalIgnoreCase))
                    mask |= 1 << ResolveLayer(preset, part.sourcePieceId);
            }
            return mask;
        }

        private WardrobeChangeResult Failure(WardrobeState before, WardrobeFailureCode code, string message) =>
            new WardrobeChangeResult(WardrobeChangeStatus.Failed, before, before, code, message: message);

        private bool Enqueue(Request request)
        {
            if (!isActiveAndEnabled || !Application.isPlaying || _isDisabling)
            { request.Complete(new WardrobeChangeResult(WardrobeChangeStatus.PerformerDisabled, BuildState(false), BuildState(false))); return false; }
            EnsureInitialized();
            if (!_initialized)
            { request.Complete(Failure(BuildState(false), WardrobeFailureCode.MissingAsset, "Wardrobe runtime is not initialized.")); return false; }
            if (_queue.Count >= 32)
            { request.Complete(Failure(BuildState(false), WardrobeFailureCode.QueueFull, "Wardrobe request queue is full.")); return false; }
            _queue.Enqueue(request);
            if (!_processing) ProcessQueue();
            return true;
        }

        private async void ProcessQueue()
        {
            if (_processing) return;
            _processing = true;
            try
            {
                while (_queue.Count > 0 && !_isDisabling)
                {
                    _activeRequest = _queue.Dequeue();
                    WardrobeChangeResult result;
                    try { result = await ApplyRequestAsync(_activeRequest); }
                    catch (Exception exception) { result = Failure(BuildState(false), WardrobeFailureCode.EffectRebindFailed, exception.Message); }
                    _activeRequest.Complete(result);
                    _activeRequest = null;
                    await Awaitable.NextFrameAsync();
                }
            }
            finally
            {
                _activeRequest = null;
                _processing = false;
                if (_queue.Count > 0 && !_isDisabling && isActiveAndEnabled) ProcessQueue();
            }
        }

        private async Awaitable<WardrobeChangeResult> ApplyRequestAsync(Request request)
        {
            if (request.transition.kind != WardrobeTransitionKind.Dissolve)
                return ApplyRequest(request);
            var before = BuildState(false);
            if (performer == null)
                return Failure(before, WardrobeFailureCode.EffectRebindFailed, "Performer dissolve binding is unavailable.");
            if (!performer.TryAcquireWardrobeEffectTransition(this, out string ownershipError))
                return Failure(before, WardrobeFailureCode.EffectRebindFailed, ownershipError);
            request.transitionOwner = this;
            try
            {
                if (performer.VisibilityState == PerformerVisibilityState.Hidden)
                    return ApplyRequest(request);
                if (performer.VisibilityState != PerformerVisibilityState.Visible)
                    return Failure(before, WardrobeFailureCode.EffectRebindFailed,
                        "Dissolve outfit changes require stable Visible or Hidden state.");
                var hidden = await performer.DissolveOutForWardrobeAsync(this, Mathf.Max(.001f, request.transition.outSeconds));
                if (request.completed || _isDisabling || !isActiveAndEnabled)
                    return new WardrobeChangeResult(WardrobeChangeStatus.PerformerDisabled, before, BuildState(false));
                if (hidden != VisibilityCompletion.Hidden || performer.VisibilityState != PerformerVisibilityState.Hidden)
                    return Failure(before, WardrobeFailureCode.EffectRebindFailed,
                        "Dissolve-out did not reach stable Hidden state: " + hidden + "/" + performer.VisibilityState);

                WardrobeChangeResult result = ApplyRequest(request);
                if (request.completed || _isDisabling || !isActiveAndEnabled)
                    return new WardrobeChangeResult(WardrobeChangeStatus.PerformerDisabled, before, BuildState(false));
                var visible = await performer.DissolveInForWardrobeAsync(this, Mathf.Max(.001f, request.transition.inSeconds));
                if (visible != VisibilityCompletion.Visible || performer.VisibilityState != PerformerVisibilityState.Visible)
                    return Failure(BuildState(false), WardrobeFailureCode.EffectRebindFailed,
                        "Dissolve-in did not reach stable Visible state: " + visible + "/" + performer.VisibilityState);
                return result;
            }
            catch (Exception exception)
            {
                if (performer != null && performer.VisibilityState == PerformerVisibilityState.Hidden &&
                    performer.isActiveAndEnabled && !_isDisabling && isActiveAndEnabled)
                {
                    try { await performer.DissolveInForWardrobeAsync(this, Mathf.Max(.001f, request.transition.inSeconds)); }
                    catch (Exception recoveryError)
                    { Debug.LogException(recoveryError, this); }
                }
                return Failure(before, WardrobeFailureCode.EffectRebindFailed, exception.Message);
            }
            finally
            {
                performer.ReleaseWardrobeEffectTransition(this);
                request.transitionOwner = null;
            }
        }

        internal bool TryPrepareSceneSnapshot(SceneWardrobeSnapshot snapshot, out string error)
        {
            if (!_initialized) EnsureInitialized();
            return TryResolveSceneSnapshot(snapshot, out _, out _, out _, out _, out error);
        }

        private bool TryResolveSceneSnapshot(SceneWardrobeSnapshot snapshot, out WardrobePreset preset,
            out int ceiling, out string hairKey, out WardrobeFailureCode failure, out string error)
        {
            preset = null; ceiling = -1; hairKey = null; failure = WardrobeFailureCode.None; error = null;
            if (snapshot == null) { failure = WardrobeFailureCode.MissingAsset; error = "Scene wardrobe snapshot is missing."; return false; }
            try { WardrobeLayerState.ValidateCeiling(snapshot.layerCeiling); }
            catch (ArgumentOutOfRangeException exception)
            { failure = WardrobeFailureCode.ConflictingItems; error = exception.Message; return false; }
            if (!string.IsNullOrWhiteSpace(snapshot.variantId) && !string.Equals(snapshot.variantId, "default", StringComparison.Ordinal))
            { failure = WardrobeFailureCode.MissingAsset; error = "This release has no material variant named " + snapshot.variantId + "."; return false; }
            if (snapshot.hairAction == WardrobeHairAction.Keep)
            { failure = WardrobeFailureCode.ConflictingItems; error = "A scene snapshot must store resolved hair, not Keep."; return false; }
            if (snapshot.hairAction == WardrobeHairAction.Set) hairKey = (snapshot.resolvedHairId ?? string.Empty).Trim();
            if (snapshot.hairAction == WardrobeHairAction.Set && string.IsNullOrEmpty(hairKey))
            { failure = WardrobeFailureCode.MissingAsset; error = "A scene snapshot with Set hair must include its resolved stable ID."; return false; }
            if (!string.IsNullOrEmpty(hairKey) && (!_pieces.TryGetValue(hairKey, out var hair) || !hair.hair))
            { failure = WardrobeFailureCode.MissingAsset; error = "Scene snapshot hair is not in this wardrobe inventory: " + hairKey; return false; }

            string presetId = (snapshot.presetId ?? string.Empty).Trim();
            bool naked = string.IsNullOrEmpty(presetId) || string.Equals(presetId, "unclothed", StringComparison.OrdinalIgnoreCase);
            if (naked)
            {
                preset = null; ceiling = -1;
                var owner = catalog.Presets.FirstOrDefault(item => item != null);
                if (owner == null || !TryPrepare(owner, -1, hairKey, out _, out failure, out error)) return false;
                return true;
            }
            preset = catalog.Presets.SingleOrDefault(item => item != null && item.PresetId == presetId);
            if (preset == null)
            { failure = WardrobeFailureCode.UnknownPreset; error = "Scene snapshot preset ID is not released: " + presetId; return false; }
            ceiling = snapshot.layerCeiling;
            if (!TryPrepare(preset, ceiling, hairKey, out _, out failure, out error)) return false;
            return true;
        }

        internal async Awaitable<WardrobeChangeResult> RecallSnapshotWhileOwnedAsync(SceneWardrobeSnapshot snapshot, object owner)
        {
            var source = new AwaitableCompletionSource<WardrobeChangeResult>();
            Enqueue(new Request { kind = RequestKind.SceneRecall, sceneSnapshot = snapshot,
                transitionOwner = owner, complete = result => source.TrySetResult(result) });
            return await source.Awaitable;
        }

        public Awaitable<WardrobeChangeResult> RecallSnapshotAsync(SceneWardrobeSnapshot snapshot,
            WardrobeTransition transition = default)
        {
            var source = new AwaitableCompletionSource<WardrobeChangeResult>();
            Enqueue(new Request { kind = RequestKind.SceneRecall, sceneSnapshot = snapshot, transition = transition,
                complete = result => source.TrySetResult(result) });
            return source.Awaitable;
        }

        private static bool Matches(SceneWardrobeSnapshot snapshot, WardrobeState state)
        {
            if (snapshot == null || state == null) return false;
            string preset = string.IsNullOrWhiteSpace(snapshot.presetId) ||
                string.Equals(snapshot.presetId, "unclothed", StringComparison.OrdinalIgnoreCase) ? null : snapshot.presetId;
            string hair = string.IsNullOrWhiteSpace(state.EffectiveHairId) ||
                string.Equals(state.EffectiveHairId, "none", StringComparison.OrdinalIgnoreCase) ? string.Empty : state.EffectiveHairId;
            return string.Equals(preset, state.OutfitId, StringComparison.Ordinal) && snapshot.layerCeiling == state.LayerCeiling &&
                string.Equals(snapshot.resolvedHairId ?? string.Empty, hair, StringComparison.Ordinal);
        }

        internal bool MatchesSceneSnapshot(SceneWardrobeSnapshot snapshot) => Matches(snapshot, CurrentWardrobe);

        public Awaitable<WardrobeChangeResult> OutfitAsync(WardrobePreset preset, WardrobeTransition transition = default)
        {
            var source = new AwaitableCompletionSource<WardrobeChangeResult>();
            Enqueue(new Request { kind = RequestKind.Outfit, preset = preset, transition = transition, complete = result => source.TrySetResult(result) });
            return source.Awaitable;
        }
        public Awaitable<WardrobeChangeResult> OutfitAsync(string presetId, WardrobeTransition transition = default)
        {
            var source = new AwaitableCompletionSource<WardrobeChangeResult>();
            Enqueue(new Request { kind = RequestKind.Outfit, id = presetId, explicitUnclothed =
                string.Equals((presetId ?? string.Empty).Trim(), "unclothed", StringComparison.OrdinalIgnoreCase),
                transition = transition, complete = result => source.TrySetResult(result) });
            return source.Awaitable;
        }
        public Awaitable<WardrobeChangeResult> SetHairAsync(string stableHairId, WardrobeTransition transition = default)
        {
            var source = new AwaitableCompletionSource<WardrobeChangeResult>();
            Enqueue(new Request { kind = RequestKind.Hair, id = stableHairId, transition = transition,
                complete = result => source.TrySetResult(result) });
            return source.Awaitable;
        }
        public Awaitable<WardrobeLayerChangeResult> TryRemoveLayerAsync(WardrobeTransition transition = default) => LayerAsync(RequestKind.Remove, transition);
        public Awaitable<WardrobeLayerChangeResult> TryAddLayerAsync(WardrobeTransition transition = default) => LayerAsync(RequestKind.Add, transition);
        private Awaitable<WardrobeLayerChangeResult> LayerAsync(RequestKind kind, WardrobeTransition transition)
        {
            var source = new AwaitableCompletionSource<WardrobeLayerChangeResult>();
            Enqueue(new Request { kind = kind, transition = transition,
                complete = result => source.TrySetResult(new WardrobeLayerChangeResult(result.Status, result.PreviousState,
                    result.CurrentState, result.FailureCode, result.NoChangeReason, result.Message)) });
            return source.Awaitable;
        }

        public void Outfit(WardrobePreset preset) => Observe(OutfitAsync(preset));
        public void Outfit(string presetId) => Observe(OutfitAsync(presetId));
        public void SetHair(string stableHairId) => Observe(SetHairAsync(stableHairId));
        public void TryRemoveLayer() => Observe(TryRemoveLayerAsync());
        public void TryAddLayer() => Observe(TryAddLayerAsync());
        private async void Observe(Awaitable<WardrobeChangeResult> operation)
        {
            try
            {
                var result = await operation;
                if (result.Status == WardrobeChangeStatus.Failed)
                    Debug.LogError("Wardrobe command failed: " + result.FailureCode + " / " + result.Message, this);
            }
            catch (Exception exception) { Debug.LogException(exception, this); }
        }
        private async void Observe(Awaitable<WardrobeLayerChangeResult> operation)
        {
            try
            {
                var result = await operation;
                if (result.Status == WardrobeChangeStatus.Failed)
                    Debug.LogError("Wardrobe layer command failed: " + result.FailureCode + " / " + result.Message, this);
            }
            catch (Exception exception) { Debug.LogException(exception, this); }
        }
    }
}
