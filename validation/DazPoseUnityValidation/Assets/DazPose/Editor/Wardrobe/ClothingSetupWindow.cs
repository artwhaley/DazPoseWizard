using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using DazPose.Performer;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DazPose.UnityValidation
{
    public sealed class ClothingSetupWindow : EditorWindow
    {
        private WardrobeCatalog _catalog;
        private WardrobeConfiguration _configuration;
        private PerformerWardrobe _wardrobe;
        private SuccubusPerformer _performer;
        private WardrobeEditSession _session;
        private int _presetIndex;
        private string _message;
        private int _hairIndex;
        private string _newVariantId = "outfit-variant";
        private Vector2 _scroll;

        [MenuItem("Tools/DAZ Pose/Wardrobe Setup/Configuration Window")]
        public static void Open() => GetWindow<ClothingSetupWindow>("Wardrobe Setup");

        private void OnEnable() { minSize = new Vector2(430, 520); RefreshBinding(); }
        private void OnFocus() => RefreshBinding();
        private void OnGUI()
        {
            EditorGUILayout.LabelField("Clothing Setup", EditorStyles.largeLabel);
            EditorGUILayout.HelpBox("Artist choices are held in this edit session until Save. Hair stays independent of clothing layers; shoe meshes remain part of their outfit.", MessageType.Info);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Open / create setup scene"))
                {
                    if (ClothingSetupBuilder.TryOpenOrCreate(out _, out string error)) { _message = "Setup scene ready."; RefreshBinding(); }
                    else _message = error;
                }
                if (GUILayout.Button("Refresh scene binding")) RefreshBinding();
            }
            if (_catalog == null || _configuration == null || _session == null)
            { EditorGUILayout.HelpBox(_message ?? "Open Clothing Setup to bind the catalog/configuration.", MessageType.Warning); return; }

            var presets = _catalog.Presets;
            if (presets.Length == 0) { EditorGUILayout.HelpBox("Catalog has no outfits.", MessageType.Warning); return; }
            _presetIndex = Mathf.Clamp(_presetIndex, 0, presets.Length - 1);
            _presetIndex = EditorGUILayout.Popup("Outfit", _presetIndex, presets.Select(x => x.DisplayName + "  [" + x.PresetId + "]").ToArray());
            var preset = presets[_presetIndex];
            EditorGUILayout.LabelField("Candidate catalog", _catalog.IsReleased ? "Released" : "Candidate preview");
            EditorGUILayout.LabelField("Configuration revision", _configuration.Revision.ToString());

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Mesh pieces and undress layers", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Base", "Always the first layer removed. Choose either 1 or 2 for each mesh; shoes are ordinary outfit pieces.");
            foreach (var part in preset.Parts)
            {
                var piece = preset.Package.pieces.First(x => x.id == part.sourcePieceId);
                var row = _session.Assignments.First(x => x.presetId == preset.PresetId && x.sourcePieceId == part.sourcePieceId);
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(piece.sourceNode + "  (" + piece.role + ")", GUILayout.MinWidth(190));
                    bool layer1 = GUILayout.Toggle(row.layer == 1, "Layer 1", "Button", GUILayout.Width(64));
                    bool layer2 = GUILayout.Toggle(row.layer == 2, "Layer 2", "Button", GUILayout.Width(64));
                    if (layer1 != (row.layer == 1)) _session.TrySetLayer(preset.PresetId, part.sourcePieceId, layer1 ? 1 : 0, out _);
                    else if (layer2 != (row.layer == 2)) _session.TrySetLayer(preset.PresetId, part.sourcePieceId, layer2 ? 2 : 0, out _);
                }
            }
            DrawOpacity(preset);
            DrawFootwear(preset);
            DrawFitStatus(preset);
            DrawRuntimePreview(presets);
            DrawInspection();
            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(4);
            using (new EditorGUILayout.HorizontalScope())
            {
                GUI.enabled = _session.IsDirty && !Application.isPlaying;
                if (GUILayout.Button("Save configuration")) SaveConfiguration();
                if (GUILayout.Button("Revert")) { _session.Reload(_catalog); _message = "Working edits reverted."; }
                GUI.enabled = true;
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                GUI.enabled = !_session.IsDirty && !Application.isPlaying;
                _newVariantId = EditorGUILayout.TextField("Variant ID", _newVariantId);
                if (GUILayout.Button("Create Variant", GUILayout.Width(120))) CreateVariant(preset, _newVariantId);
                GUI.enabled = true;
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                GUI.enabled = !_session.IsDirty && !Application.isPlaying && !_catalog.IsReleased;
                if (GUILayout.Button("Publish validated generation")) PublishCandidate();
                GUI.enabled = true;
            }
            if (!string.IsNullOrEmpty(_message)) EditorGUILayout.HelpBox(_message, MessageType.None);
        }

        private void DrawOpacity(WardrobePreset preset)
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Per-slot opacity overrides", EditorStyles.boldLabel);
            foreach (var part in preset.Parts)
            {
                var piece = preset.Package.pieces.First(x => x.id == part.sourcePieceId);
                for (int slot = 0; slot < piece.materials.Length; ++slot)
                {
                    string slotId = slot.ToString();
                    var value = _session.MaterialOverrides.FirstOrDefault(x => x.presetId == preset.PresetId &&
                        x.sourcePieceId == part.sourcePieceId && x.materialSlotId == slotId);
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField(piece.sourceNode + " / " + piece.materials[slot].name, GUILayout.MinWidth(220));
                        bool use = EditorGUILayout.Toggle(value != null && value.opacityOverride, GUILayout.Width(18));
                        if (use)
                        {
                            float opacity = EditorGUILayout.Slider(value != null && value.opacityOverride ? value.opacity : 1f, 0f, 1f);
                            if (value == null || !value.opacityOverride || Mathf.Abs(value.opacity - opacity) > .001f)
                                _session.SetOpacity(preset.PresetId, part.sourcePieceId, slotId, opacity);
                        }
                        else if (value != null && value.opacityOverride) _session.ClearOpacity(preset.PresetId, part.sourcePieceId, slotId);
                    }
                }
            }
            EditorGUILayout.HelpBox("Opacity uses the material's Base Color alpha (or Color alpha). Alpha clipping/render-queue setup still depends on that shader.", MessageType.None);
        }

        private void DrawFitStatus(WardrobePreset preset)
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Compiled fit states", EditorStyles.boldLabel);
            foreach (var fit in preset.FitStates.OrderBy(x => x.VisibleMask))
                EditorGUILayout.LabelField("Mask " + fit.VisibleMask, fit.Status +
                    (fit.Footwear.BentFootPoseActive ? " · bent-foot pose" : " · bare-foot pose") +
                    (fit.Footwear.FootwearActive ? " · shoes/support active" : " · shoe support off") +
                    (fit.IssueCodes.Length > 0 ? " · " + string.Join(", ", fit.IssueCodes) : string.Empty));
            if (_session.IsDirty) EditorGUILayout.HelpBox("Layer or opacity changes are unsaved and candidate fit states need recompilation. Save compiles every reachable mask before committing.", MessageType.Warning);
        }

        private void DrawFootwear(WardrobePreset preset)
        {
            if (preset.Footwear == null) return;
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Footwear calibration (configuration override)", EditorStyles.boldLabel);
            var row = _session.GetFootwear(preset.PresetId, preset.Footwear);
            bool heightOverride = EditorGUILayout.ToggleLeft("Override standing lift", row.standingHeightOverride);
            float height = heightOverride
                ? EditorGUILayout.Slider("Standing lift", row.standingHeight, 0f, .3f)
                : preset.Footwear.standingHeight;
            bool shrinkOverride = EditorGUILayout.ToggleLeft("Override foot shrink", row.footShrinkOverride);
            float shrink = shrinkOverride
                ? EditorGUILayout.Slider("Foot shrink", row.footShrink, 0f, .1f)
                : preset.Footwear.footShrink;
            if (heightOverride != row.standingHeightOverride || shrinkOverride != row.footShrinkOverride ||
                Mathf.Abs(height - row.standingHeight) > .0001f || Mathf.Abs(shrink - row.footShrink) > .0001f)
                _session.SetFootwear(preset.PresetId, heightOverride, height, shrinkOverride, shrink);
            EditorGUILayout.HelpBox("These values compile into actor fit states; shared source footwear profiles are never edited by this window.", MessageType.None);
        }

        private void DrawRuntimePreview(WardrobePreset[] presets)
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Production runtime preview", EditorStyles.boldLabel);
            if (!Application.isPlaying)
            { EditorGUILayout.HelpBox("Enter Play Mode in this scene to use the same queued wardrobe controller used by the game.", MessageType.Info); return; }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Load outfit")) RunPreview(_performer != null ? _performer.OutfitAsync(presets[_presetIndex]) : null);
                if (GUILayout.Button("Remove highest")) RunLayer(_performer != null ? _performer.TryRemoveLayerAsync() : null);
                if (GUILayout.Button("Add lowest")) RunLayer(_performer != null ? _performer.TryAddLayerAsync() : null);
                if (GUILayout.Button("Naked")) RunPreview(_performer != null ? _performer.OutfitAsync("unclothed") : null);
            }
            var state = _wardrobe != null ? _wardrobe.CurrentWardrobe : null;
            if (state != null)
            {
                EditorGUILayout.LabelField("CurrentWardrobe", state.OutfitId + " / " + state.DisplayName + " / mask " + state.VisibleMask + " of " + state.PopulatedMask);
                EditorGUILayout.LabelField("Feet / hair", state.EffectiveFootwearId + " / " + state.EffectiveHairId);
            }
            var hairChoices = _catalog.Presets.SelectMany(p => p.Package.pieces.Where(x => string.Equals(x.role, "hair", StringComparison.OrdinalIgnoreCase))
                .Select(x => new { Label = p.DisplayName + " / " + x.sourceNode, Id = p.PresetId + "/" + x.id }))
                .ToArray();
            var options = new[] { "Keep current hair", "Clear hair" }.Concat(hairChoices.Select(x => x.Label)).ToArray();
            _hairIndex = EditorGUILayout.Popup("Explicit hair test", _hairIndex, options);
            if (GUILayout.Button("Apply selected hair") && _performer != null)
            {
                if (_hairIndex == 0) _message = "Keeping current hair.";
                else RunPreview(_performer.SetHairAsync(_hairIndex == 1 ? "clear" : hairChoices[_hairIndex - 2].Id));
            }
        }

        private void DrawInspection()
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Body / movement inspection (session only)", EditorStyles.boldLabel);
            var body = _wardrobe != null ? _wardrobe.Body : null;
            var anatomy = _performer != null ? _performer.GetComponent<LaraAnatomyControls>() : null;
            if (Application.isPlaying && anatomy != null)
            {
                anatomy.CapturedOpening = EditorGUILayout.Slider("Opening", anatomy.CapturedOpening, 0, 1);
                anatomy.Nipples = EditorGUILayout.Slider("Nipples", anatomy.Nipples, 0, 1);
                anatomy.LaraBreastsMeshPreview = EditorGUILayout.Slider("Lara Breasts", anatomy.LaraBreastsMeshPreview, 0, 1);
            }
            else EditorGUILayout.LabelField("Morph controls", body != null ? "Available in Play Mode" : "No body bound");
            if (Application.isPlaying && _performer != null && GUILayout.Button("Walk 1.5 m forward"))
                _performer.WalkTo(_performer.transform.position + _performer.transform.forward * 1.5f);
        }

        private void SaveConfiguration()
        {
            if (_catalog == null || _configuration == null) return;
            var body = _wardrobe != null ? _wardrobe.Body : FindSceneBody();
            if (body == null) { _message = "Cannot save: setup scene has no canonical body renderer."; return; }
            string generation = AssignmentHash(_session.Assignments, _session.MaterialOverrides, _session.FootwearOverrides);
            if (_catalog.IsReleased) generation = HashText(_catalog.ReleasedGenerationId + "/" + generation);
            WardrobeCatalog targetCatalog = _catalog;
            WardrobeConfiguration targetConfig = _configuration;
            WardrobePreset[] targetPresets = _catalog.Presets;
            if (_catalog.IsReleased && !TryForkReleasedCatalog(generation, out targetCatalog, out targetConfig, out targetPresets, out string forkError))
            { _message = "Save stopped: could not fork released catalog: " + forkError; return; }

            var compiled = new System.Collections.Generic.Dictionary<WardrobePreset, WardrobeFitState[]>();
            var working = WorkingConfigurationCopy();
            try
            {
                foreach (var preset in targetPresets)
                {
                    var result = WardrobeFitCompiler.Compile(preset, working, body,
                        "Assets/TestData/WardrobeRuntime/SetupCandidates/" + generation, false);
                    if (result.needsFitStates != 0)
                    { _message = "Save stopped: " + preset.PresetId + " has Needs Fit states: " + string.Join(", ", result.issues); return; }
                    compiled.Add(preset, result.fitStates);
                }
            }
            catch (Exception exception) { _message = "Save stopped before configuration commit: " + exception.Message; return; }
            finally { DestroyImmediate(working); }
            if (!targetConfig.TrySetWorkingAssignments(_session.Assignments, out string error)) { _message = error; return; }
            targetConfig.ConfigureGenerated(_session.Assignments, _session.MaterialOverrides, _session.FootwearOverrides);
            targetConfig.CommitRevision(generation, new[] { generation });
            foreach (var pair in compiled)
            {
                pair.Key.ConfigureGenerated(pair.Key.PresetId, pair.Key.DisplayName, pair.Key.CharacterSignature,
                    targetConfig.Revision, pair.Key.Package, pair.Key.Parts, pair.Key.Aliases,
                    pair.Key.HairAction, pair.Key.HairId, pair.Key.Footwear);
                pair.Key.SetGeneratedFitStates(pair.Value);
                EditorUtility.SetDirty(pair.Key);
            }
            EditorUtility.SetDirty(targetConfig);
            if (targetCatalog != _catalog)
            {
                _catalog = targetCatalog;
                _configuration = targetConfig;
                _wardrobe.ConfigureBinding(_catalog, _configuration, body, _performer, true);
                EditorUtility.SetDirty(_wardrobe);
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            }
            AssetDatabase.SaveAssets();
            _session.MarkCommitted();
            _message = "Saved revision " + targetConfig.Revision + "; all outfit masks compiled and validated.";
            if (EditorSceneManager.GetActiveScene().path == ClothingSetupBuilder.ScenePath)
                EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            RefreshBinding();
        }

        private WardrobeConfiguration WorkingConfigurationCopy()
        {
            var copy = CreateInstance<WardrobeConfiguration>();
            copy.ConfigureGenerated(_session.Assignments, _session.MaterialOverrides, _session.FootwearOverrides);
            return copy;
        }

        private void PublishCandidate()
        {
            if (_catalog == null || _configuration == null || _session.IsDirty || Application.isPlaying)
            { _message = "Stop Play Mode and save or revert setup edits before publishing."; return; }
            var result = WardrobeGenerationPublisher.PublishCandidate(_catalog, _configuration,
                _wardrobe != null ? _wardrobe.Body : FindSceneBody());
            _message = result.published
                ? "Published " + result.generationId + " with " + result.presetCount + " presets and " + result.fitStateCount + " fit states."
                : "Publish stopped; the catalog was not advanced. " + string.Join("; ", result.issues ?? Array.Empty<string>());
            if (result.published)
            {
                if (_wardrobe != null) EditorUtility.SetDirty(_wardrobe);
                if (EditorSceneManager.GetActiveScene().path == ClothingSetupBuilder.ScenePath)
                { EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene()); EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene()); }
                RefreshBinding();
            }
        }

        private void CreateVariant(WardrobePreset source, string presetId)
        {
            if (!WardrobeCatalog.IsValidId(presetId)) { _message = "Variant ID must use lowercase ASCII, digits, hyphens or underscores."; return; }
            if (_catalog.Presets.Any(p => p.PresetId == presetId)) { _message = "That preset ID already exists."; return; }
            if (_catalog.IsReleased)
            {
                string generation = HashText(_catalog.ReleasedGenerationId + "/working-variant");
                if (!TryForkReleasedCatalog(generation, out var forkCatalog, out var forkConfig, out _, out string forkError))
                { _message = "Could not create a working candidate from the release: " + forkError; return; }
                _catalog = forkCatalog; _configuration = forkConfig;
                _wardrobe.ConfigureBinding(_catalog, _configuration, _wardrobe.Body, _performer, true);
                EditorUtility.SetDirty(_wardrobe);
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            }
            var clone = Instantiate(source);
            clone.name = presetId;
            var partLayer = _session.Assignments.Where(a => a.presetId == source.PresetId)
                .ToDictionary(a => a.sourcePieceId, a => a.layer, StringComparer.Ordinal);
            var variantParts = source.Parts.Select(part => new WardrobePreset.Part
            {
                sourcePieceId = part.sourcePieceId,
                layer = partLayer.TryGetValue(part.sourcePieceId, out int layer) ? layer : part.layer,
                requiredOwnerPieceIds = part.requiredOwnerPieceIds
            }).ToArray();
            clone.ConfigureGenerated(presetId, source.DisplayName + " Variant", source.CharacterSignature,
                _configuration.Revision, source.Package, variantParts, Array.Empty<string>(),
                source.HairAction, source.HairId, source.Footwear);
            var assignments = _session.Assignments.Concat(variantParts.Select(part => new WardrobeConfiguration.PieceAssignment
                { presetId = presetId, sourcePieceId = part.sourcePieceId, layer = part.layer })).ToArray();
            var overrides = _session.MaterialOverrides.Concat(_session.MaterialOverrides.Where(row => row.presetId == source.PresetId)
                .Select(row => new WardrobeConfiguration.MaterialOverride { presetId = presetId, sourcePieceId = row.sourcePieceId,
                    materialSlotId = row.materialSlotId, material = row.material, opacityOverride = row.opacityOverride, opacity = row.opacity })).ToArray();
            var shoeOverrides = _session.FootwearOverrides.Concat(_session.FootwearOverrides.Where(row => row.presetId == source.PresetId)
                .Select(row => new WardrobeConfiguration.FootwearOverride { presetId = presetId,
                    standingHeightOverride = row.standingHeightOverride, standingHeight = row.standingHeight,
                    footShrinkOverride = row.footShrinkOverride, footShrink = row.footShrink })).ToArray();
            var working = CreateInstance<WardrobeConfiguration>();
            working.ConfigureGenerated(assignments, overrides, shoeOverrides);
            WardrobeFitCompiler.Result result;
            try
            {
                result = WardrobeFitCompiler.Compile(clone, working, _wardrobe.Body,
                    "Assets/TestData/WardrobeRuntime/SetupVariants/" + HashText(presetId + "/" + AssignmentHash(assignments, overrides, shoeOverrides)), false);
            }
            catch (Exception exception)
            { DestroyImmediate(working); DestroyImmediate(clone); _message = "Variant compile failed: " + exception.Message; return; }
            DestroyImmediate(working);
            if (result.needsFitStates != 0) { DestroyImmediate(clone); _message = "Variant needs fitting: " + string.Join(", ", result.issues); return; }
            clone.SetGeneratedFitStates(result.fitStates);
            string presetPath = "Assets/Wardrobe/Candidates/Variants/" + presetId + ".asset";
            EnsureAssetFolder("Assets/Wardrobe/Candidates/Variants");
            if (AssetDatabase.LoadAssetAtPath<WardrobePreset>(presetPath) != null)
            { DestroyImmediate(clone); _message = "Candidate variant asset already exists: " + presetPath; return; }
            AssetDatabase.CreateAsset(clone, presetPath);
            _configuration.ConfigureGenerated(assignments, overrides, shoeOverrides);
            _configuration.CommitRevision(HashText(presetId + "/" + _configuration.Revision), _configuration.InputHashes);
            _catalog.ConfigureCandidate(_catalog.Presets.Concat(new[] { clone }).ToArray());
            EditorUtility.SetDirty(_configuration); EditorUtility.SetDirty(_catalog);
            AssetDatabase.SaveAssets();
            if (EditorSceneManager.GetActiveScene().path == ClothingSetupBuilder.ScenePath)
            {
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
                EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            }
            _session = new WardrobeEditSession(_configuration, _catalog);
            _message = "Created candidate variant " + presetId + "; the published catalog remains unchanged.";
        }

        private bool TryForkReleasedCatalog(string generation, out WardrobeCatalog forkCatalog,
            out WardrobeConfiguration forkConfig, out WardrobePreset[] forkPresets, out string error)
        {
            forkCatalog = null; forkConfig = null; forkPresets = Array.Empty<WardrobePreset>(); error = null;
            string root = "Assets/Wardrobe/Candidates/working-" + generation.Substring(0, Math.Min(16, generation.Length));
            try
            {
                EnsureAssetFolder(root + "/Presets");
                forkPresets = _catalog.Presets.Select(source =>
                {
                    string path = root + "/Presets/" + source.PresetId + ".asset";
                    var copy = AssetDatabase.LoadAssetAtPath<WardrobePreset>(path);
                    if (copy != null) return copy;
                    copy = Instantiate(source); copy.name = source.PresetId;
                    AssetDatabase.CreateAsset(copy, path); return copy;
                }).ToArray();
                string catalogPath = root + "/WardrobeCatalog.asset";
                forkCatalog = AssetDatabase.LoadAssetAtPath<WardrobeCatalog>(catalogPath);
                if (forkCatalog == null)
                {
                    forkCatalog = Instantiate(_catalog); forkCatalog.name = "Wardrobe Working Candidate";
                    forkCatalog.ConfigureCandidate(forkPresets); AssetDatabase.CreateAsset(forkCatalog, catalogPath);
                }
                string configPath = root + "/WardrobeConfiguration.asset";
                forkConfig = AssetDatabase.LoadAssetAtPath<WardrobeConfiguration>(configPath);
                if (forkConfig == null)
                { forkConfig = Instantiate(_configuration); forkConfig.name = "Wardrobe Working Configuration"; AssetDatabase.CreateAsset(forkConfig, configPath); }
                AssetDatabase.SaveAssets();
                return true;
            }
            catch (Exception exception) { error = exception.Message; return false; }
        }

        private static void EnsureAssetFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/');
            EnsureAssetFolder(path.Substring(0, slash));
            if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
        }

        private void RefreshBinding()
        {
            _performer = UnityEngine.Object.FindAnyObjectByType<SuccubusPerformer>();
            _wardrobe = _performer != null ? _performer.GetComponent<PerformerWardrobe>() : null;
            _catalog = _wardrobe != null ? _wardrobe.Catalog : AssetDatabase.LoadAssetAtPath<WardrobeCatalog>(ClothingSetupBuilder.CatalogPath);
            _configuration = _wardrobe != null ? _wardrobe.Configuration : AssetDatabase.LoadAssetAtPath<WardrobeConfiguration>(ClothingSetupBuilder.ConfigurationPath);
            if (_catalog != null && _configuration != null && (_session == null || _session.IsDirty == false))
                _session = new WardrobeEditSession(_configuration, _catalog);
        }

        private SkinnedMeshRenderer FindSceneBody() => UnityEngine.Object.FindObjectsByType<SkinnedMeshRenderer>(FindObjectsInactive.Include)
            .FirstOrDefault(r => r.sharedMesh != null && r.sharedMesh.GetBlendShapeIndex("CapturedOpening") >= 0);

        private static string AssignmentHash(WardrobeConfiguration.PieceAssignment[] rows,
            WardrobeConfiguration.MaterialOverride[] overrides, WardrobeConfiguration.FootwearOverride[] footwear)
        {
            string data = string.Join("|", rows.OrderBy(x => x.presetId, StringComparer.Ordinal).ThenBy(x => x.sourcePieceId, StringComparer.Ordinal)
                .Select(x => x.presetId + "/" + x.sourcePieceId + ":" + x.layer)) + "#" +
                string.Join("|", overrides.OrderBy(x => x.presetId, StringComparer.Ordinal).ThenBy(x => x.sourcePieceId, StringComparer.Ordinal)
                    .ThenBy(x => x.materialSlotId, StringComparer.Ordinal).Select(x => x.presetId + "/" + x.sourcePieceId + "/" + x.materialSlotId +
                        ":" + (x.material != null ? AssetDatabase.GetAssetPath(x.material) + "/" + AssetDatabase.GetAssetDependencyHash(AssetDatabase.GetAssetPath(x.material)) : "default") +
                        ":" + x.opacityOverride + ":" + x.opacity.ToString("R", System.Globalization.CultureInfo.InvariantCulture)));
            data += "#" + string.Join("|", footwear.OrderBy(x => x.presetId, StringComparer.Ordinal)
                .Select(x => x.presetId + ":" + x.standingHeightOverride + ":" + x.standingHeight.ToString("R", System.Globalization.CultureInfo.InvariantCulture) +
                    ":" + x.footShrinkOverride + ":" + x.footShrink.ToString("R", System.Globalization.CultureInfo.InvariantCulture)));
            using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(data))).Replace("-", "").ToLowerInvariant();
        }

        private static string HashText(string text)
        {
            using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(text ?? string.Empty))).Replace("-", "").ToLowerInvariant();
        }

        private async void RunPreview(Awaitable<WardrobeChangeResult> operation)
        {
            if (operation == null) { _message = "Runtime performer is unavailable."; return; }
            var result = await operation;
            _message = result.Status + (result.FailureCode != WardrobeFailureCode.None ? " / " + result.FailureCode + ": " + result.Message : string.Empty);
            Repaint();
        }
        private async void RunLayer(Awaitable<WardrobeLayerChangeResult> operation)
        {
            if (operation == null) { _message = "Runtime performer is unavailable."; return; }
            var result = await operation;
            _message = result.Status + (result.FailureCode != WardrobeFailureCode.None ? " / " + result.FailureCode + ": " + result.Message : string.Empty);
            Repaint();
        }
    }
}
