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
        private bool _showOpacity;
        private bool _showFootwear = true;
        private bool _showInspection;
        private string _draftRevision;
        private string _compiledRevision;
        private string _compileError;
        private bool _isCompiling;

        [MenuItem("DazPose/Wardrobe/Outfit Configuration")]
        public static void Open() => GetWindow<ClothingSetupWindow>("Wardrobe Setup");

        private void OnEnable() { minSize = new Vector2(780, 600); RefreshBinding(); }
        private void OnFocus() => RefreshBinding();
        private void OnGUI()
        {
            EditorGUILayout.LabelField("Clothing Setup", EditorStyles.largeLabel);
            EditorGUILayout.HelpBox("Choose an outfit, edit each garment's layer, and inspect the next runtime change. Drafts save independently from releases; compile explicitly to update the production preview.", MessageType.Info);
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
            var preset = presets[_presetIndex];
            DrawPersistentState(preset);
            DrawPreviewControls(preset);
            DrawRuntimeState(presets);

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUILayout.VerticalScope(GUILayout.Width(210)))
                {
                    EditorGUILayout.LabelField("Outfits", EditorStyles.boldLabel);
                    for (int i = 0; i < presets.Length; i++)
                        if (GUILayout.Toggle(i == _presetIndex, presets[i].DisplayName, "Button")) _presetIndex = i;
                }
                using (new EditorGUILayout.VerticalScope())
                {
                    preset = presets[_presetIndex];
                    EditorGUILayout.LabelField("Garment layers", EditorStyles.boldLabel);
                    EditorGUILayout.HelpBox("Removal order: Layer 2 → Layer 1 → Base, skipping empty layers. Add restores the lowest hidden populated layer.", MessageType.Info);
                    DrawCompleteSequence(preset);
                    DrawGarmentRows(preset);
                    DrawOpacity(preset);
                    DrawFootwear(preset);
                    DrawFitStatus(preset);
                    DrawInspection();
                }
            }
            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(4);
            using (new EditorGUILayout.HorizontalScope())
            {
                GUI.enabled = _session.CanUndo;
                if (GUILayout.Button("Undo")) _session.Undo();
                GUI.enabled = _session.CanRedo;
                if (GUILayout.Button("Redo")) _session.Redo();
                GUI.enabled = _session.IsDirty && !Application.isPlaying;
                if (GUILayout.Button("Save Draft")) SaveConfiguration();
                GUI.enabled = !Application.isPlaying;
                if (GUILayout.Button("Revert")) { _session.Reload(_catalog); _draftRevision = WardrobeCandidateWorkflow.DraftHash(_configuration); _message = "Working edits reverted to saved draft."; }
                GUI.enabled = true;
                EditorGUILayout.LabelField(_session.IsDirty ? "● Unsaved edits" : "Saved draft", GUILayout.Width(110));
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
                GUI.enabled = !_session.IsDirty && !Application.isPlaying;
                if (GUILayout.Button("Publish validated generation")) PublishCandidate();
                GUI.enabled = true;
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                GUI.enabled = !Application.isPlaying && !_isCompiling;
                if (GUILayout.Button(_isCompiling ? "Compiling…" : "Compile / validate preview")) CompileCandidate();
                GUI.enabled = true;
                if (!string.IsNullOrEmpty(_compileError)) EditorGUILayout.LabelField("Error: " + _compileError, EditorStyles.wordWrappedLabel);
            }
            if (!string.IsNullOrEmpty(_message)) EditorGUILayout.HelpBox(_message, MessageType.None);
        }

        private void DrawPersistentState(WardrobePreset preset)
        {
            string draft = _configuration != null ? WardrobeCandidateWorkflow.DraftHash(_configuration) : "unavailable";
            EditorGUILayout.LabelField("Selected outfit", preset.DisplayName + "  [" + preset.PresetId + "]");
            bool stale = _session.IsDirty || !IsPreviewBound() ||
                (!_catalog.IsReleased && !string.Equals(draft, _compiledRevision, StringComparison.Ordinal));
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(_catalog.IsReleased ? "Released catalog" : "Candidate catalog", GUILayout.Width(145));
                EditorGUILayout.LabelField("Draft rev " + _configuration.Revision + " · edit " + _session.EditGeneration + (_session.IsDirty ? " *" : string.Empty), GUILayout.Width(190));
                EditorGUILayout.LabelField("Preview rev " + PreviewRevision(preset), GUILayout.Width(145));
                if (stale) EditorGUILayout.LabelField("Preview stale", EditorStyles.boldLabel);
            }
            _draftRevision = draft;
        }

        private void DrawPreviewControls(WardrobePreset preset)
        {
            EditorGUILayout.Space(4);
            using (new EditorGUILayout.HorizontalScope())
            {
                GUI.enabled = CanPreviewBoundDraft();
                if (GUILayout.Button("Dress / redress")) RunPreview(_performer != null ? _performer.OutfitAsync(preset) : null);
                if (GUILayout.Button("Remove next: " + NextRemovalName(preset))) RunLayer(_performer != null ? _performer.TryRemoveLayerAsync() : null);
                if (GUILayout.Button("Add next: " + NextAdditionName(preset))) RunLayer(_performer != null ? _performer.TryAddLayerAsync() : null);
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Preview naked (keep selection)")) PreviewNakedKeepingSelection();
                if (GUILayout.Button("Select Unclothed")) RunPreview(_performer != null ? _performer.OutfitAsync("unclothed") : null);
                GUI.enabled = true;
            }
        }

        private void DrawCompleteSequence(WardrobePreset preset)
        {
            var remove = new[] { SequenceNames(preset, 2), SequenceNames(preset, 1), SequenceNames(preset, 0) }
                .Where(x => x != null).ToArray();
            EditorGUILayout.LabelField("Remove", string.Join("  →  ", remove));
            EditorGUILayout.LabelField("Add", string.Join("  →  ", remove.Reverse()));
        }

        private string SequenceNames(WardrobePreset preset, int layer)
        {
            var names = LayerNames(preset, layer);
            return names.Length == 0 ? null : (layer == 0 ? "Base" : "Layer " + layer) + ": " + string.Join(", ", names);
        }

        private void DrawGarmentRows(WardrobePreset preset)
        {
            var assignments = _session.Assignments.Where(x => x.presetId == preset.PresetId).ToDictionary(x => x.sourcePieceId, StringComparer.Ordinal);
            var parts = preset.Parts.Select(part => new { Part = part, Piece = preset.Package.pieces.FirstOrDefault(x => x.id == part.sourcePieceId) })
                .Where(x => x.Piece != null).OrderByDescending(x => assignments.TryGetValue(x.Part.sourcePieceId, out var row) ? row.layer : x.Part.layer)
                .ThenBy(x => x.Piece.sourceNode, StringComparer.Ordinal).ToArray();
            foreach (int layer in new[] { 2, 1, 0 })
            {
                var group = parts.Where(x => (assignments.TryGetValue(x.Part.sourcePieceId, out var row) ? row.layer : x.Part.layer) == layer).ToArray();
                EditorGUILayout.LabelField(layer == 0 ? "Base" : "Layer " + layer, EditorStyles.boldLabel);
                if (group.Length == 0) EditorGUILayout.LabelField("(empty)", EditorStyles.miniLabel);
                foreach (var item in group)
                {
                    assignments.TryGetValue(item.Part.sourcePieceId, out var assignment);
                    int current = assignment != null ? assignment.layer : item.Part.layer;
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField(item.Piece.sourceNode + "  (" + item.Piece.role + ")", GUILayout.MinWidth(230));
                        int selected = EditorGUILayout.Popup(current, new[] { "Base", "Layer 1", "Layer 2" }, GUILayout.Width(105));
                        if (selected != current && !_session.TrySetLayer(preset.PresetId, item.Part.sourcePieceId, selected, out string error)) _message = error;
                    }
                }
            }
        }

        private string NextRemovalName(WardrobePreset preset)
        {
            var state = _wardrobe != null ? _wardrobe.CurrentWardrobe : null;
            int mask = state != null && state.OutfitId == preset.PresetId ? state.VisibleMask : PopulatedMask(preset);
            for (int layer = 2; layer >= 0; layer--)
                if ((mask & (1 << layer)) != 0) return string.Join(", ", LayerNames(preset, layer));
            return "none";
        }

        private string NextAdditionName(WardrobePreset preset)
        {
            var state = _wardrobe != null ? _wardrobe.CurrentWardrobe : null;
            int populated = PopulatedMask(preset);
            int visible = state != null && state.OutfitId == preset.PresetId ? state.VisibleMask : 0;
            for (int layer = 0; layer < 3; layer++)
                if ((populated & (1 << layer)) != 0 && (visible & (1 << layer)) == 0) return string.Join(", ", LayerNames(preset, layer));
            return "none";
        }

        private string[] LayerNames(WardrobePreset preset, int layer) => preset.Parts
            .Where(part => (_session.Assignments.FirstOrDefault(x => x.presetId == preset.PresetId && x.sourcePieceId == part.sourcePieceId)?.layer ?? part.layer) == layer)
            .Select(part => preset.Package.pieces.FirstOrDefault(x => x.id == part.sourcePieceId)?.sourceNode ?? part.sourcePieceId).ToArray();

        private int PopulatedMask(WardrobePreset preset)
        {
            int mask = 0;
            foreach (var part in preset.Parts)
                mask |= 1 << (_session.Assignments.FirstOrDefault(x => x.presetId == preset.PresetId && x.sourcePieceId == part.sourcePieceId)?.layer ?? part.layer);
            return mask;
        }

        private bool IsPreviewBound() => _performer != null && _wardrobe != null &&
            _wardrobe.Catalog == _catalog && _wardrobe.Configuration == _configuration &&
            (_catalog.IsReleased || WardrobeCandidateWorkflow.IsCompiled(_catalog, _configuration));

        private bool CanPreviewBoundDraft() => Application.isPlaying && !_session.IsDirty && IsPreviewBound();

        private string PreviewRevision(WardrobePreset preset)
        {
            if (Application.isPlaying && _wardrobe != null && _wardrobe.CurrentWardrobe != null)
                return "r" + _wardrobe.CurrentWardrobe.ConfigurationRevision;
            var bound = _wardrobe != null && _wardrobe.Catalog != null
                ? _wardrobe.Catalog.Presets.FirstOrDefault(x => x.PresetId == preset.PresetId) : null;
            return bound != null ? "r" + bound.ConfigurationRevision : "stale";
        }

        private void DrawRuntimeState(WardrobePreset[] presets)
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Preview state", EditorStyles.boldLabel);
            if (!Application.isPlaying) { EditorGUILayout.HelpBox("Enter Play Mode in Clothing Setup to inspect and change the saved, compiled draft.", MessageType.Info); return; }
            var state = _wardrobe != null ? _wardrobe.CurrentWardrobe : null;
            if (state == null) { EditorGUILayout.HelpBox("Preview wardrobe is unavailable.", MessageType.Warning); return; }
            if (!CanPreviewBoundDraft())
                EditorGUILayout.HelpBox(_wardrobe != null && !string.IsNullOrEmpty(_wardrobe.BindingDiagnostic)
                    ? _wardrobe.BindingDiagnostic : "Compile this saved draft and enter Play Mode before previewing it.", MessageType.Warning);
            EditorGUILayout.LabelField("Selected / current", presets[_presetIndex].PresetId + " / " + (state.OutfitId ?? "unclothed") + " · visible " + state.VisibleMask + " / populated " + state.PopulatedMask);
            EditorGUILayout.LabelField("Effective hair / footwear", (state.EffectiveHairId ?? "none") + " / " + state.EffectiveFootwearId);
            var hairChoices = _catalog.Presets.SelectMany(p => p.Package.pieces.Where(x => string.Equals(x.role, "hair", StringComparison.OrdinalIgnoreCase))
                .Select(x => new { Label = p.DisplayName + " / " + x.sourceNode, Id = p.PresetId + "/" + x.id })).ToArray();
            var options = new[] { "Keep current hair", "Clear hair" }.Concat(hairChoices.Select(x => x.Label)).ToArray();
            _hairIndex = EditorGUILayout.Popup("Independent hair inspection", _hairIndex, options);
            GUI.enabled = CanPreviewBoundDraft();
            if (GUILayout.Button("Apply selected hair") && _performer != null && _hairIndex > 0)
                RunPreview(_performer.SetHairAsync(_hairIndex == 1 ? "clear" : hairChoices[_hairIndex - 2].Id));
            GUI.enabled = true;
        }

        private async void PreviewNakedKeepingSelection()
        {
            if (_performer == null || _wardrobe == null) { _message = "Runtime performer is unavailable."; return; }
            if (!CanPreviewBoundDraft()) { _message = "Compile the saved draft and enter Play Mode before previewing it."; Repaint(); return; }
            var state = _wardrobe.CurrentWardrobe;
            var selected = _catalog.Presets[Mathf.Clamp(_presetIndex, 0, _catalog.Presets.Length - 1)];
            if (state == null || state.OutfitId != selected.PresetId)
            {
                var dressed = await _performer.OutfitAsync(selected);
                if (dressed.Status != WardrobeChangeStatus.Applied && dressed.Status != WardrobeChangeStatus.AlreadyEquipped)
                { _message = "Could not select outfit for naked preview: " + dressed.Status + " / " + dressed.Message; Repaint(); return; }
            }
            for (int i = 0; i < 3 && _wardrobe.CurrentWardrobe != null && _wardrobe.CurrentWardrobe.CanRemoveLayer; i++)
            {
                var result = await _performer.TryRemoveLayerAsync();
                if (result.Status != WardrobeChangeStatus.Applied && result.Status != WardrobeChangeStatus.NoChange)
                { _message = "Naked preview stopped: " + result.Status + " / " + result.FailureCode + " " + result.Message; Repaint(); return; }
            }
            if (_wardrobe.CurrentWardrobe != null && !_wardrobe.CurrentWardrobe.IsNaked)
            { _message = "Naked preview stopped before reaching a naked state."; Repaint(); return; }
            _message = "Naked preview reached; outfit selection is retained for Add.";
            Repaint();
        }

        private void DrawOpacity(WardrobePreset preset)
        {
            EditorGUILayout.Space(6);
            _showOpacity = EditorGUILayout.Foldout(_showOpacity, "Advanced · material opacity", true);
            if (!_showOpacity) return;
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
                            var sourceMaterial = value != null && value.material != null ? value.material : piece.materials[slot];
                            float defaultOpacity = WardrobeMaterialOpacity.TryRead(sourceMaterial, out float sourceOpacity) ? sourceOpacity : 1f;
                            float opacity = EditorGUILayout.Slider(value != null && value.opacityOverride ? value.opacity : defaultOpacity, 0f, 1f);
                            if (value == null || !value.opacityOverride || Mathf.Abs(value.opacity - opacity) > .001f)
                                _session.SetOpacity(preset.PresetId, part.sourcePieceId, slotId, opacity);
                        }
                        else if (value != null && value.opacityOverride) _session.ClearOpacity(preset.PresetId, part.sourcePieceId, slotId);
                    }
                }
            }
            EditorGUILayout.HelpBox("Opacity changes transparency strength. The material's rendering mode and cutout settings still apply.", MessageType.None);
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
            if (_session.IsDirty) EditorGUILayout.HelpBox("Draft edits are unsaved. Save Draft persists them even when fitting needs work; Compile / validate updates the runtime preview after the fit states pass.", MessageType.Warning);
            if (_compiledRevision != null && !string.Equals(_compiledRevision, WardrobeCandidateWorkflow.DraftHash(_configuration), StringComparison.Ordinal))
                EditorGUILayout.HelpBox("Compiled appearance is stale. The layer-order preview above reflects current edits immediately.", MessageType.Warning);
        }

        private void DrawFootwear(WardrobePreset preset)
        {
            _showFootwear = EditorGUILayout.Foldout(_showFootwear, "Advanced · footwear calibration", true);
            if (!_showFootwear || preset.Footwear == null) return;
            EditorGUILayout.Space(6);
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

        private void DrawInspection()
        {
            EditorGUILayout.Space(6);
            _showInspection = EditorGUILayout.Foldout(_showInspection, "Inspection · body and movement", true);
            if (!_showInspection) return;
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
            if (!TrySaveConfiguration(out string error)) _message = error;
        }

        private bool TrySaveConfiguration(out string error)
        {
            error = null;
            if (_catalog == null || _configuration == null || _session == null) { error = "Catalog or draft configuration is unavailable."; return false; }
            if (Application.isPlaying) { error = "Stop Play Mode before saving a draft."; return false; }
            var body = _wardrobe != null ? _wardrobe.Body : FindSceneBody();
            if (!WardrobeCandidateWorkflow.TrySaveDraft(_catalog, _configuration, _session, body,
                out var savedCatalog, out var savedConfiguration, out error)) return false;
            _catalog = savedCatalog;
            _configuration = savedConfiguration;
            if (_performer != null)
            {
                var binding = _performer.GetComponent<ClothingSetupDraftBinding>();
                if (binding == null) binding = Undo.AddComponent<ClothingSetupDraftBinding>(_performer.gameObject);
                binding.Configure(_catalog, _configuration);
                EditorUtility.SetDirty(binding);
            }
            AssetDatabase.SaveAssets();
            _session = new WardrobeEditSession(_configuration, _catalog);
            _compiledRevision = WardrobeCandidateWorkflow.IsCompiled(_catalog, _configuration)
                ? WardrobeCandidateWorkflow.DraftHash(_configuration) : null;
            _draftRevision = WardrobeCandidateWorkflow.DraftHash(_configuration);
            _message = "Saved candidate draft revision " + _configuration.Revision + ". Compile it explicitly to update the production preview.";
            if (EditorSceneManager.GetActiveScene().path == ClothingSetupBuilder.ScenePath)
            {
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
                EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            }
            return true;
        }

        private void CompileCandidate()
        {
            if (_catalog == null || _configuration == null || _session == null) return;
            if (_session.IsDirty) { _compileError = "Save Draft before compiling."; return; }
            if (Application.isPlaying) { _compileError = "Stop Play Mode before compiling."; return; }
            _isCompiling = true;
            _compileError = null;
            Repaint();
            EditorUtility.DisplayProgressBar("Wardrobe candidate", "Resolving and compiling reachable outfit states…", .25f);
            try
            {
                var body = _wardrobe != null ? _wardrobe.Body : FindSceneBody();
                if (!WardrobeCandidateWorkflow.TryCompile(_catalog, _configuration, body, out string error))
                { _compileError = error; _message = "Compile failed. The current production wardrobe binding remains active."; return; }
                _compiledRevision = WardrobeCandidateWorkflow.DraftHash(_configuration);
                if (_performer != null)
                {
                    if (_wardrobe == null) _wardrobe = _performer.GetComponent<PerformerWardrobe>();
                    if (_wardrobe != null)
                    {
                        _wardrobe.ConfigureBinding(_catalog, _configuration, body, _performer, true);
                        EditorUtility.SetDirty(_wardrobe);
                        PrefabUtility.RecordPrefabInstancePropertyModifications(_wardrobe);
                    }
                }
                AssetDatabase.SaveAssets();
                if (EditorSceneManager.GetActiveScene().path == ClothingSetupBuilder.ScenePath)
                { EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene()); EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene()); }
                _message = "Compiled and bound candidate revision " + _configuration.Revision + " to the setup preview.";
                RefreshBinding();
            }
            finally { EditorUtility.ClearProgressBar(); _isCompiling = false; Repaint(); }
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
            if (!WardrobeCandidateWorkflow.IsCompiled(_catalog, _configuration))
            { _message = "Compile and validate this saved candidate before publishing."; return; }
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
                string generation = HashText(_catalog.ReleasedGenerationId + "/working-variant/" + Guid.NewGuid().ToString("N"));
                if (!TryForkReleasedCatalog(generation, out var forkCatalog, out var forkConfig, out _, out string forkError))
                { _message = "Could not create a working candidate from the release: " + forkError; return; }
                _catalog = forkCatalog; _configuration = forkConfig;
                if (_performer != null)
                {
                    var draftBinding = _performer.GetComponent<ClothingSetupDraftBinding>();
                    if (draftBinding == null) draftBinding = Undo.AddComponent<ClothingSetupDraftBinding>(_performer.gameObject);
                    draftBinding.Configure(_catalog, _configuration);
                    EditorUtility.SetDirty(draftBinding);
                }
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
                    "Assets/TestData/WardrobeRuntime/SetupVariants/" + Guid.NewGuid().ToString("N"), false);
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
            _catalog.SetAuthoringConfiguration(_configuration);
            _catalog.SetCompiledConfigurationHash(string.Empty);
            if (_performer != null)
            {
                var draftBinding = _performer.GetComponent<ClothingSetupDraftBinding>();
                if (draftBinding == null) draftBinding = Undo.AddComponent<ClothingSetupDraftBinding>(_performer.gameObject);
                draftBinding.Configure(_catalog, _configuration);
                EditorUtility.SetDirty(draftBinding);
            }
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
            var draftBinding = _performer != null ? _performer.GetComponent<ClothingSetupDraftBinding>() : null;
            var nextCatalog = draftBinding != null && draftBinding.Catalog != null ? draftBinding.Catalog :
                (_wardrobe != null ? _wardrobe.Catalog : AssetDatabase.LoadAssetAtPath<WardrobeCatalog>(ClothingSetupBuilder.CatalogPath));
            var nextConfiguration = draftBinding != null && draftBinding.Configuration != null ? draftBinding.Configuration :
                (_wardrobe != null ? _wardrobe.Configuration : AssetDatabase.LoadAssetAtPath<WardrobeConfiguration>(ClothingSetupBuilder.ConfigurationPath));
            if (_session != null && _session.IsDirty && (_catalog != nextCatalog || _configuration != nextConfiguration)) return;
            bool changed = _catalog != nextCatalog || _configuration != nextConfiguration;
            _catalog = nextCatalog;
            _configuration = nextConfiguration;
            if (_catalog != null && _configuration != null && (_session == null || changed))
            {
                _session = new WardrobeEditSession(_configuration, _catalog);
                _compiledRevision = WardrobeCandidateWorkflow.IsCompiled(_catalog, _configuration)
                    ? WardrobeCandidateWorkflow.DraftHash(_configuration) : null;
                _draftRevision = WardrobeCandidateWorkflow.DraftHash(_configuration);
                if (_presetIndex >= _catalog.Presets.Length) _presetIndex = 0;
            }
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
