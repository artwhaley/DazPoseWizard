using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace DazPose.Performer
{
    /// <summary>Isolated Play Mode fixture. Compiled out of player builds.</summary>
    [DefaultExecutionOrder(11000)]
    public sealed class WardrobeRuntimeExecutionHarness : MonoBehaviour
    {
        [Serializable] private sealed class Assertion
        { public string name, expected, actual; public bool passed; }
        [Serializable] private sealed class Report
        {
            public int schemaVersion = 1;
            public string stage, startedUtc, completedUtc, sourceHash;
            public string[] inputAssetHashes, artifacts, limitations;
            public bool passed;
            public bool complete;
            public Assertion[] assertions;
        }
        private sealed class ShoeMetric
        {
            public SkinnedMeshRenderer renderer;
            public int[] edges;
            public float[] restEdgeLengths;
        }

        [SerializeField] private SuccubusPerformer performer;
        [SerializeField] private PerformerWardrobe wardrobe;
        [SerializeField] private SuccubusPerformer secondPerformer;
        [SerializeField] private PerformerWardrobe secondWardrobe;
        [SerializeField] private PerformerParticleBody secondParticleBody;
        private readonly List<Assertion> _assertions = new List<Assertion>();
        private bool _collectingWalk;
        private Mesh _walkBakedMesh;
        private Camera _walkCamera;
        private RenderTexture _walkTarget;
        private ShoeMetric[] _walkShoes = Array.Empty<ShoeMetric>();
        private LaraHeelReview _walkHeelReview;
        private PerformerFootwearProfile _walkFootwear;
        private Transform[] _walkFeet;
        private int _walkFrames, _walkSupportedFrames, _walkSupportSamples, _walkRejectedSupportSamples;
        private int _walkCullingExpectedSamples, _walkCullingMisses, _walkBoundsMisses, _walkVisibilityStateFailures;
        private float _walkMaximumSupportError, _walkMaximumShoeShapeError, _walkMaximumFloorPenetration;
        private string _walkSamplingError;

        public void Configure(SuccubusPerformer owner, PerformerWardrobe controller,
            SuccubusPerformer secondOwner, PerformerWardrobe secondController, PerformerParticleBody secondParticleController)
        {
            performer = owner; wardrobe = controller;
            secondPerformer = secondOwner; secondWardrobe = secondController;
            secondParticleBody = secondParticleController;
        }

        private async void Start()
        {
#if UNITY_EDITOR
            string started = DateTime.UtcNow.ToString("O");
            try { await RunAssertions(); }
            catch (Exception exception) { Check("runtimeException", "no exception", exception.ToString(), false); }
            WriteReport(started);
#endif
        }

#if UNITY_EDITOR
        private async Awaitable RunAssertions()
        {
            Check("runtimeInitialized", "initialized", "initialized", performer != null && wardrobe != null && wardrobe.Body != null);
            if (performer == null || wardrobe == null || wardrobe.Body == null) return;
            var animator = performer.GetComponent<Animator>();
            var bodyRenderer = wardrobe.Body;
            var secondBodyRenderer = secondWardrobe != null ? secondWardrobe.Body : null;
            Check("secondPerformerInitializedAndIsolated", "separate performer, wardrobe and body", 
                "performer=" + (secondPerformer != null) + ";wardrobe=" + (secondWardrobe != null) +
                ";body=" + (secondBodyRenderer != null) + ";distinct=" +
                (secondPerformer != performer && secondWardrobe != wardrobe && secondBodyRenderer != bodyRenderer),
                secondPerformer != null && secondWardrobe != null && secondBodyRenderer != null &&
                secondPerformer != performer && secondWardrobe != wardrobe && secondBodyRenderer != bodyRenderer);
            var secondDissolveRig = secondPerformer != null ? secondPerformer.GetComponent<PerformerDissolveRig>() : null;
            Check("secondPerformerOwnsItsDissolveRenderer", "second rig targets second body",
                "target=" + (secondDissolveRig != null && secondDissolveRig.TargetRenderer != null ? secondDissolveRig.TargetRenderer.name : "missing") +
                ";body=" + (secondBodyRenderer != null ? secondBodyRenderer.name : "missing") +
                ";same=" + (secondDissolveRig != null && secondDissolveRig.TargetRenderer == secondBodyRenderer),
                secondDissolveRig != null && secondDissolveRig.TargetRenderer == secondBodyRenderer);
            Check("secondPerformerOwnsItsParticleBody", "dissolve rig and particle body share second body's renderer",
                "particle=" + (secondDissolveRig != null && secondDissolveRig.ParticleBody != null ?
                    secondDissolveRig.ParticleBody.TargetRenderer != null ? secondDissolveRig.ParticleBody.TargetRenderer.name : "missing renderer" : "missing component") +
                ";same=" + (secondDissolveRig != null && secondDissolveRig.ParticleBody == secondParticleBody &&
                    secondParticleBody != null && secondParticleBody.TargetRenderer == secondBodyRenderer),
                secondDissolveRig != null && secondDissolveRig.ParticleBody == secondParticleBody &&
                secondParticleBody != null && secondParticleBody.TargetRenderer == secondBodyRenderer);
            var anatomy = performer.GetComponent<LaraAnatomyControls>();
            var serializedSources = wardrobe.Catalog.Presets
                .SelectMany(preset => preset.Package.pieces.SelectMany(piece => piece.materials)
                    .Concat(preset.FitStates.SelectMany(fit => fit.DissolveProfile != null
                        ? fit.DissolveProfile.LaraRuntimeMaterials : Array.Empty<Material>()))
                    .Concat(preset.FitStates.Where(fit => fit.DissolveProfile != null).Select(fit => (UnityEngine.Object)fit.DissolveProfile)))
                .Where(asset => asset != null).Distinct()
                .Select(asset => new KeyValuePair<UnityEngine.Object, string>(asset, UnityEditor.EditorJsonUtility.ToJson(asset)))
                .ToArray();
            Check("approvedAnatomyControlsReady", "opening/nipples/breasts ready", anatomy != null && anatomy.IsReady ? "ready" : "missing",
                anatomy != null && anatomy.IsReady);

            var first = await performer.OutfitAsync("first-outfit");
            Check("firstOutfitApplied", WardrobeChangeStatus.Applied.ToString(), first.Status.ToString(), first.Status == WardrobeChangeStatus.Applied);
            string firstHair = wardrobe.CurrentWardrobe.EffectiveHairId;
            Check("firstHairSelected", "first-outfit/EmikoHair_59875", firstHair,
                firstHair == "first-outfit/EmikoHair_59875");
            Check("firstOutfitBaseVisible", "mask 1", wardrobe.CurrentWardrobe.VisibleMask.ToString(),
                wardrobe.CurrentWardrobe.VisibleMask == 1);

            anatomy.CapturedOpening = .62f;
            anatomy.Nipples = .8f;
            anatomy.LaraBreastsMeshPreview = .8f;
            var maid = await performer.OutfitAsync("maid");
            Check("maidAliasApplied", "Applied", maid.Status.ToString(), maid.Status == WardrobeChangeStatus.Applied);
            Check("keepHairAcrossOutfit", firstHair, wardrobe.CurrentWardrobe.EffectiveHairId,
                wardrobe.CurrentWardrobe.EffectiveHairId == firstHair);
            int openingIndex = bodyRenderer.sharedMesh.GetBlendShapeIndex("CapturedOpening");
            int nipplesIndex = bodyRenderer.sharedMesh.GetBlendShapeIndex("Genesis8Female__PBMNipples");
            int breastsIndex = bodyRenderer.sharedMesh.GetBlendShapeIndex("LaraBreastsAdjustment");
            bool anatomyPreserved = openingIndex >= 0 && nipplesIndex >= 0 && breastsIndex >= 0 &&
                Math.Abs(bodyRenderer.GetBlendShapeWeight(openingIndex) - 62f) < .001f &&
                Math.Abs(bodyRenderer.GetBlendShapeWeight(nipplesIndex) - 80f) < .001f &&
                Math.Abs(bodyRenderer.GetBlendShapeWeight(breastsIndex) - (anatomy.LaraBreastsMeshPreview-anatomy.BakedLaraBreasts)*100f) < .001f;
            Check("approvedAnatomyControlsPreserved", "opening .62, nipples .8, breast .8", anatomyPreserved ? "preserved" : "lost after mesh rebind", anatomyPreserved);
            Check("animatorIdentityPreserved", "same Animator", "same Animator", animator == performer.GetComponent<Animator>());

            var removeShoes = await performer.TryRemoveLayerAsync();
            var footDriver = performer.GetComponent<FootwearPoseDriver>();
            var heelReview = performer.GetComponent<LaraHeelReview>();
            int heelPoseIndex = bodyRenderer.sharedMesh.GetBlendShapeIndex("CapturedHeelFootPose");
            bool poseWithoutShoes = removeShoes.Status == WardrobeChangeStatus.Applied &&
                wardrobe.CurrentWardrobe.VisibleMask == 3 && wardrobe.CurrentWardrobe.EffectiveFootwearId.Contains("bent-foot") &&
                footDriver != null && !footDriver.IsFootwearActive && footDriver.StandingLift > .05f &&
                heelReview != null && heelReview.LeftStance == 0 && heelReview.RightStance == 0 &&
                heelPoseIndex >= 0 && bodyRenderer.GetBlendShapeWeight(heelPoseIndex) >= 99f;
            Check("removeShoesKeepsStockingFootPose", "clothes remain; bent-foot pose/lift remain; shoe contact solver is off",
                "mask=" + wardrobe.CurrentWardrobe.VisibleMask + ";state=" + wardrobe.CurrentWardrobe.EffectiveFootwearId +
                ";shoeSupport=" + (footDriver != null && footDriver.IsFootwearActive) + ";lift=" +
                (footDriver != null ? footDriver.StandingLift : 0) + ";pose=" +
                (heelPoseIndex >= 0 ? bodyRenderer.GetBlendShapeWeight(heelPoseIndex) : -1), poseWithoutShoes);
            var removeOuter = await performer.TryRemoveLayerAsync();
            Check("removeOuterKeepsHosieryBent", "mask 1 and bent-foot pose active",
                "mask=" + wardrobe.CurrentWardrobe.VisibleMask + ";pose=" + wardrobe.CurrentWardrobe.CurrentFootwearPoseActive,
                removeOuter.Status == WardrobeChangeStatus.Applied && wardrobe.CurrentWardrobe.VisibleMask == 1 &&
                wardrobe.CurrentWardrobe.CurrentFootwearPoseActive);
            var removeBase = await performer.TryRemoveLayerAsync();
            int bareHeelPoseIndex = bodyRenderer.sharedMesh.GetBlendShapeIndex("CapturedHeelFootPose");
            bool bareRestoresFeet = removeBase.Status == WardrobeChangeStatus.Applied && wardrobe.CurrentWardrobe.IsNaked &&
                !wardrobe.CurrentWardrobe.CurrentFootwearPoseActive && footDriver != null && !footDriver.IsFootwearActive &&
                footDriver.StandingLift == 0 && (bareHeelPoseIndex < 0 || bodyRenderer.GetBlendShapeWeight(bareHeelPoseIndex) == 0);
            Check("removeLastPoseDependentPieceRestoresBareFeet", "naked with heel deformation/lift off",
                "naked=" + wardrobe.CurrentWardrobe.IsNaked + ";pose=" +
                (bareHeelPoseIndex >= 0 ? bodyRenderer.GetBlendShapeWeight(bareHeelPoseIndex) : 0) + ";lift=" +
                (footDriver != null ? footDriver.StandingLift : -1), bareRestoresFeet);
            Check("hairVisibleWhenNaked", firstHair, wardrobe.CurrentWardrobe.EffectiveHairId,
                wardrobe.CurrentWardrobe.EffectiveHairId == firstHair);
            var addBase = await performer.TryAddLayerAsync();
            var addOuter = await performer.TryAddLayerAsync();
            var addShoes = await performer.TryAddLayerAsync();
            Check("addRestoresThreeMaidLayersInOrder", "mask 1 -> 3 -> 7; shoes support returns last",
                "mask=" + wardrobe.CurrentWardrobe.VisibleMask + ";shoeSupport=" +
                (footDriver != null && footDriver.IsFootwearActive),
                addBase.Status == WardrobeChangeStatus.Applied && addOuter.Status == WardrobeChangeStatus.Applied &&
                addShoes.Status == WardrobeChangeStatus.Applied && wardrobe.CurrentWardrobe.VisibleMask == 7 &&
                footDriver != null && footDriver.IsFootwearActive);

            var a = performer.OutfitAsync("second-outfit");
            var b = performer.TryRemoveLayerAsync();
            var ra = await a; var rb = await b;
            Check("relativeCommandsFifo", "second outfit then naked", ra.CurrentState.OutfitId + "/" + wardrobe.CurrentWardrobe.IsNaked,
                ra.Status == WardrobeChangeStatus.Applied && rb.Status == WardrobeChangeStatus.Applied &&
                wardrobe.CurrentWardrobe.OutfitId == "second-outfit" && wardrobe.CurrentWardrobe.IsNaked);

            var unclothed = await performer.OutfitAsync("unclothed");
            Check("explicitUnclothedClearsSelection", "null outfit with retained hair",
                (wardrobe.CurrentWardrobe.OutfitId ?? "null") + "/" + wardrobe.CurrentWardrobe.EffectiveHairId,
                unclothed.Status == WardrobeChangeStatus.Applied && wardrobe.CurrentWardrobe.OutfitId == null &&
                wardrobe.CurrentWardrobe.EffectiveHairId == firstHair);

            var unknown = await performer.OutfitAsync("not-a-preset");
            Check("unknownPresetFailsWithoutMutation", WardrobeFailureCode.UnknownPreset.ToString(), unknown.FailureCode.ToString(),
                unknown.Status == WardrobeChangeStatus.Failed && wardrobe.CurrentWardrobe.OutfitId == null);

            var dressAgain = await performer.OutfitAsync("maid");
            Check("setupQueueFixture", "Maid applied", dressAgain.Status.ToString(), dressAgain.Status == WardrobeChangeStatus.Applied);
            var queued = new Awaitable<WardrobeLayerChangeResult>[36];
            for (int i = 0; i < queued.Length; ++i) queued[i] = performer.TryRemoveLayerAsync();
            int overflow = 0, accepted = 0;
            foreach (var item in queued)
            {
                var result = await item;
                if (result.FailureCode == WardrobeFailureCode.QueueFull) overflow++;
                if (result.Status != WardrobeChangeStatus.Failed || result.FailureCode != WardrobeFailureCode.QueueFull) accepted++;
            }
            Check("boundedQueue", "QueueFull after capacity", overflow.ToString(), overflow > 0 && accepted + overflow == queued.Length);

            var activeAtDisable = performer.TryRemoveLayerAsync();
            var disabledPending = performer.TryAddLayerAsync();
            wardrobe.enabled = false;
            var activeResult = await activeAtDisable;
            var disabledResult = await disabledPending;
            Check("disableSettlesPending", "PerformerDisabled", disabledResult.Status.ToString(),
                activeResult.Status != WardrobeChangeStatus.Failed &&
                disabledResult.Status == WardrobeChangeStatus.PerformerDisabled);
            wardrobe.enabled = true;
            Mesh originalBodyMesh = bodyRenderer.sharedMesh;
            var poseDependentPreset = wardrobe.Catalog.Presets.First(preset => preset.PresetId == "third-outfit");
            var poseDependentState = poseDependentPreset.FitStates.First(state => state.VisibleMask == 3);
            var priorWeights = new Dictionary<string, float>(StringComparer.Ordinal);
            for (int i = 0; i < originalBodyMesh.blendShapeCount; i++)
                priorWeights[originalBodyMesh.GetBlendShapeName(i)] = bodyRenderer.GetBlendShapeWeight(i);
            bodyRenderer.sharedMesh = poseDependentState.BodyMesh;
            int poseOnlyIndex = bodyRenderer.sharedMesh.GetBlendShapeIndex("CapturedHeelFootPose");
            var poseOnlyState = new WardrobeFootwearState(false, true, 0f, 0f, "bent-foot pose only");
            string poseOnlyError = footDriver == null ? "driver missing" : string.Empty;
            bool poseOnlyConfigured = footDriver != null && footDriver.Configure(bodyRenderer, null, poseOnlyState, out poseOnlyError);
            float poseOnlyWeight = poseOnlyIndex >= 0 ? bodyRenderer.GetBlendShapeWeight(poseOnlyIndex) : -1f;
            bool poseWithoutProfile = poseOnlyConfigured &&
                !footDriver.IsFootwearActive && footDriver.StandingLift == 0f && footDriver.FootShrink == 0f &&
                poseOnlyIndex >= 0 && poseOnlyWeight >= 99f;
            Check("poseDependentGarmentWorksWithoutShoeCalibration", "bent foot retained; no shoe support/lift/shrink; no configuration failure",
                "configured=" + poseOnlyConfigured + ";pose=" + poseOnlyWeight + ";support=" + (footDriver != null && footDriver.IsFootwearActive) +
                ";lift=" + (footDriver != null ? footDriver.StandingLift : -1f) + ";shrink=" + (footDriver != null ? footDriver.FootShrink : -1f) +
                ";error=" + (poseOnlyError ?? "none"), poseWithoutProfile);
            bodyRenderer.sharedMesh = originalBodyMesh;
            if (footDriver != null) footDriver.Configure(bodyRenderer, null,
                new WardrobeFootwearState(false, false, 0f, 0f, "barefoot"), out _);
            for (int i = 0; i < bodyRenderer.sharedMesh.blendShapeCount; i++)
                if (priorWeights.TryGetValue(bodyRenderer.sharedMesh.GetBlendShapeName(i), out float weight))
                    bodyRenderer.SetBlendShapeWeight(i, weight);

            if (secondPerformer != null && secondWardrobe != null && secondBodyRenderer != null && secondParticleBody != null)
            {
                var secondAnimator = secondPerformer.GetComponent<Animator>();
                Vector3 firstPosition = performer.transform.position;
                Quaternion firstRotation = performer.transform.rotation;
                Vector3 firstScale = performer.transform.localScale;
                Vector3 secondPosition = secondPerformer.transform.position;
                Quaternion secondRotation = secondPerformer.transform.rotation;
                Vector3 secondScale = secondPerformer.transform.localScale;
                var firstOutfit = await performer.OutfitAsync("maid");
                var secondFirstPreset = secondWardrobe.Catalog.Presets.Single(preset => preset.PresetId == "first-outfit");
                var secondFirstFit = secondFirstPreset.FitStates.Single(state => state.VisibleMask == 1);
                Mesh secondOriginalMesh = secondBodyRenderer.sharedMesh;
                secondBodyRenderer.sharedMesh = secondFirstFit.BodyMesh;
                string secondBindingError = secondDissolveRig == null ? "dissolve rig missing" : null;
                bool secondBindingValid = secondDissolveRig != null &&
                    secondFirstFit.SurfaceBindings.IsValidFor(secondDissolveRig.TargetRenderer, out secondBindingError) &&
                    secondDissolveRig.ParticleBody != null && secondDissolveRig.ParticleBody.TargetRenderer == secondBodyRenderer;
                secondBodyRenderer.sharedMesh = secondOriginalMesh;
                Check("secondPerformerFitBindingTargetsItsBody", "first-outfit binding accepts second body renderer",
                    "bindingValid=" + secondBindingValid + ";targetSame=" +
                    (secondDissolveRig != null && secondDissolveRig.TargetRenderer == secondBodyRenderer) +
                    ";reason=" + (secondBindingError ?? "none"), secondBindingValid);
                var secondOutfit = await secondPerformer.OutfitAsync("first-outfit");
                string secondHair = secondWardrobe.CurrentWardrobe.EffectiveHairId;
                var secondRemove = await secondPerformer.TryRemoveLayerAsync();
                bool independentActors = firstOutfit.Status == WardrobeChangeStatus.Applied &&
                    secondOutfit.Status == WardrobeChangeStatus.Applied && secondRemove.Status == WardrobeChangeStatus.Applied &&
                    wardrobe.CurrentWardrobe.VisibleMask == 7 && !wardrobe.CurrentWardrobe.IsNaked &&
                    secondWardrobe.CurrentWardrobe.VisibleMask == 0 && secondWardrobe.CurrentWardrobe.IsNaked &&
                    secondWardrobe.CurrentWardrobe.EffectiveHairId == secondHair &&
                    bodyRenderer.sharedMesh != secondBodyRenderer.sharedMesh;
                Check("performerOutfitsAreIndependent", "A stays fully dressed while B removes its base outfit and retains its hair",
                    "A=" + wardrobe.CurrentWardrobe.VisibleMask + "/" + wardrobe.CurrentWardrobe.EffectiveHairId +
                    ";B=" + secondWardrobe.CurrentWardrobe.VisibleMask + "/" + secondWardrobe.CurrentWardrobe.EffectiveHairId,
                    independentActors);
                var secondAdd = await secondPerformer.TryAddLayerAsync();
                bool independentAdd = secondAdd.Status == WardrobeChangeStatus.Applied &&
                    secondWardrobe.CurrentWardrobe.VisibleMask == 1 && wardrobe.CurrentWardrobe.VisibleMask == 7;
                Check("performerLayerCommandsAreIndependent", "B can restore its base layer without changing A",
                    "A=" + wardrobe.CurrentWardrobe.VisibleMask + ";B=" + secondWardrobe.CurrentWardrobe.VisibleMask,
                    independentAdd);

                var validationExpression = CreateValidationExpression(animator, bodyRenderer);
                var poseBeforeSwitch = performer.DesiredPose;
                var settledPoseBeforeSwitch = performer.SettledPose;
                if (validationExpression != null) performer.Expression(validationExpression, .42f, 5f);
                var gazeTargetObject = new GameObject("Wardrobe State Preservation Gaze Target");
                gazeTargetObject.transform.position = performer.transform.position + performer.transform.forward * 3f + Vector3.up * 1.4f;
                performer.LookAt(gazeTargetObject.transform);
                var speechProbe = AudioClip.Create("Wardrobe State Preservation Speech", 441000, 1, 44100, false);
                performer.Say(speechProbe);
                string gazeDescriptionBeforeSwitch = performer.GazeTargetDescription;
                Vector3 gazePositionBeforeSwitch = performer.RawGazeTargetPosition;
                var statePreservationSwitch = await performer.OutfitAsync("first-outfit");
                bool expressionPreserved = validationExpression != null &&
                    performer.DesiredExpression == validationExpression && Math.Abs(performer.DesiredExpressionIntensity - .42f) < .001f &&
                    performer.IsExpressionTransitioning;
                bool speechPreserved = performer.IsSpeaking && performer.CurrentSpeechClip == speechProbe;
                bool gazePreserved = performer.HasGazeTarget &&
                    performer.GazeTargetDescription == gazeDescriptionBeforeSwitch &&
                    Vector3.Distance(performer.RawGazeTargetPosition, gazePositionBeforeSwitch) < .0001f;
                bool posePreserved = performer.DesiredPose == poseBeforeSwitch && performer.SettledPose == settledPoseBeforeSwitch;
                Check("wardrobeSwitchPreservesActivePerformanceState", "expression transition, speech, gaze target, and pose persist through clothing switch",
                    "switch=" + statePreservationSwitch.Status + ";expression=" + expressionPreserved +
                    ";speech=" + speechPreserved + ";gaze=" + gazePreserved + ";pose=" + posePreserved,
                    statePreservationSwitch.Status == WardrobeChangeStatus.Applied && expressionPreserved &&
                    speechPreserved && gazePreserved && posePreserved);
                performer.StopSpeaking();
                performer.ClearGaze();
                performer.ClearExpression(0f);
                UnityEngine.Object.Destroy(gazeTargetObject);
                UnityEngine.Object.Destroy(speechProbe);
                if (validationExpression != null)
                {
                    var expressionClip = validationExpression.Clip;
                    UnityEngine.Object.Destroy(validationExpression);
                    if (expressionClip != null) UnityEngine.Object.Destroy(expressionClip);
                }
                await Awaitable.NextFrameAsync();

                var rollbackPriorSwitch = await performer.OutfitAsync("maid");
                var rollbackSourcePreset = wardrobe.Catalog.Presets.Single(preset => preset.PresetId == "first-outfit");
                var rollbackSourceFit = rollbackSourcePreset.FitStates.Single(state => state.VisibleMask == 1);
                var rollbackProfile = UnityEngine.Object.Instantiate(rollbackSourceFit.DissolveProfile);
                rollbackProfile.name = "Deliberately Invalid Wardrobe Rollback Profile";
                var rollbackProfileSerialized = new UnityEditor.SerializedObject(rollbackProfile);
                rollbackProfileSerialized.FindProperty("particleBodyVfxAsset").objectReferenceValue = null;
                rollbackProfileSerialized.ApplyModifiedPropertiesWithoutUndo();
                var rollbackFit = ScriptableObject.CreateInstance<WardrobeFitState>();
                rollbackFit.name = "Deliberately Invalid Wardrobe Rollback Fit";
                rollbackFit.ConfigureGenerated(rollbackSourceFit.VisibleMask, rollbackSourceFit.CharacterSignature,
                    WardrobeFitStatus.Validated, rollbackSourceFit.BodyMesh, rollbackSourceFit.AttachmentMeshes,
                    rollbackSourceFit.SurfaceBindings, rollbackProfile, rollbackSourceFit.Coverage.VisiblePieceIds,
                    rollbackSourceFit.Footwear.FootwearActive, rollbackSourcePreset.Footwear);
                var rollbackPreset = UnityEngine.Object.Instantiate(rollbackSourcePreset);
                rollbackPreset.name = "Deliberately Invalid Wardrobe Rollback Preset";
                var rollbackFits = rollbackSourcePreset.FitStates;
                int rollbackFitIndex = Array.FindIndex(rollbackFits, fit => fit.VisibleMask == rollbackFit.VisibleMask);
                rollbackFits[rollbackFitIndex] = rollbackFit;
                rollbackPreset.SetGeneratedFitStates(rollbackFits);
                var stateBeforeRejectedSwitch = wardrobe.CurrentWardrobe;
                Mesh meshBeforeRejectedSwitch = bodyRenderer.sharedMesh;
                var renderersBeforeRejectedSwitch = performer.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                bool[] rendererEnabledBeforeRejectedSwitch = renderersBeforeRejectedSwitch.Select(renderer => renderer.enabled).ToArray();
                var runtimeCatalog = wardrobe.Catalog;
                var catalogPresetsBeforeRejectedSwitch = runtimeCatalog.Presets;
                string catalogGenerationBeforeRejectedSwitch = runtimeCatalog.ReleasedGenerationId;
                runtimeCatalog.ConfigureCandidate(catalogPresetsBeforeRejectedSwitch.Select(preset =>
                    preset.PresetId == rollbackPreset.PresetId ? rollbackPreset : preset).ToArray());
                runtimeCatalog.SetReleasedGeneration(catalogGenerationBeforeRejectedSwitch);
                var rejectedSwitch = await performer.OutfitAsync(rollbackPreset);
                runtimeCatalog.ConfigureCandidate(catalogPresetsBeforeRejectedSwitch);
                runtimeCatalog.SetReleasedGeneration(catalogGenerationBeforeRejectedSwitch);
                var renderersAfterRejectedSwitch = performer.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                bool rendererVisibilityRestored = renderersAfterRejectedSwitch.Length == rendererEnabledBeforeRejectedSwitch.Length &&
                    renderersAfterRejectedSwitch.Select(renderer => renderer.enabled).SequenceEqual(rendererEnabledBeforeRejectedSwitch);
                bool preparationRolledBack = rollbackPriorSwitch.Status == WardrobeChangeStatus.Applied &&
                    stateBeforeRejectedSwitch.OutfitId == "third-outfit" && stateBeforeRejectedSwitch.VisibleMask == 7 &&
                    rejectedSwitch.Status == WardrobeChangeStatus.Failed &&
                    rejectedSwitch.FailureCode == WardrobeFailureCode.EffectRebindFailed && bodyRenderer.sharedMesh == meshBeforeRejectedSwitch &&
                    wardrobe.CurrentWardrobe.OutfitId == stateBeforeRejectedSwitch.OutfitId &&
                    wardrobe.CurrentWardrobe.VisibleMask == stateBeforeRejectedSwitch.VisibleMask &&
                    wardrobe.CurrentWardrobe.EffectiveHairId == stateBeforeRejectedSwitch.EffectiveHairId && rendererVisibilityRestored;
                Check("failedPreparationRollsBackRendererAndSelection", "invalid dissolve binding fails without changing the previous outfit, body mesh, hair, or visible renderers",
                    "failure=" + rejectedSwitch.FailureCode + ";meshRestored=" + (bodyRenderer.sharedMesh == meshBeforeRejectedSwitch) +
                    ";outfit=" + wardrobe.CurrentWardrobe.OutfitId + ";mask=" + wardrobe.CurrentWardrobe.VisibleMask +
                    ";hair=" + wardrobe.CurrentWardrobe.EffectiveHairId + ";visibilityRestored=" + rendererVisibilityRestored,
                    preparationRolledBack);
                UnityEngine.Object.Destroy(rollbackPreset);
                UnityEngine.Object.Destroy(rollbackFit);
                UnityEngine.Object.Destroy(rollbackProfile);

                int firstRendererCount = performer.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length;
                int firstTransformCount = performer.GetComponentsInChildren<Transform>(true).Length;
                int secondRendererCount = secondPerformer.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length;
                int secondTransformCount = secondPerformer.GetComponentsInChildren<Transform>(true).Length;
                bool cyclesApplied = true;
                string cycleFailure = "none";
                for (int cycle = 0; cycle < 100; ++cycle)
                {
                    var changed = await performer.OutfitAsync(cycle % 2 == 0 ? "first-outfit" : "maid");
                    var removed = await performer.TryRemoveLayerAsync();
                    var added = await performer.TryAddLayerAsync();
                    if (changed.Status != WardrobeChangeStatus.Applied || removed.Status != WardrobeChangeStatus.Applied ||
                        added.Status != WardrobeChangeStatus.Applied)
                    {
                        cyclesApplied = false;
                        cycleFailure = cycle + ":" + changed.Status + "/" + removed.Status + "/" + added.Status;
                        break;
                    }
                }
                await Awaitable.NextFrameAsync();
                int firstRendererCountAfter = performer.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length;
                int firstTransformCountAfter = performer.GetComponentsInChildren<Transform>(true).Length;
                int secondRendererCountAfter = secondPerformer.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length;
                int secondTransformCountAfter = secondPerformer.GetComponentsInChildren<Transform>(true).Length;
                int wardrobeProfiles = Resources.FindObjectsOfTypeAll<PerformerDissolveProfile>()
                    .Count(profile => profile != null && profile.name.EndsWith("(Performer Wardrobe Instance)", StringComparison.Ordinal));
                bool stableInventory = firstRendererCount == firstRendererCountAfter && firstTransformCount == firstTransformCountAfter &&
                    secondRendererCount == secondRendererCountAfter && secondTransformCount == secondTransformCountAfter;
                bool stableTransforms = performer.GetComponent<Animator>() == animator && secondPerformer.GetComponent<Animator>() == secondAnimator &&
                    Vector3.Distance(performer.transform.position, firstPosition) < .0001f &&
                    Quaternion.Angle(performer.transform.rotation, firstRotation) < .01f &&
                    Vector3.Distance(performer.transform.localScale, firstScale) < .0001f &&
                    Vector3.Distance(secondPerformer.transform.position, secondPosition) < .0001f &&
                    Quaternion.Angle(secondPerformer.transform.rotation, secondRotation) < .01f &&
                    Vector3.Distance(secondPerformer.transform.localScale, secondScale) < .0001f;
                Check("hundredSwitchCyclesRemainBounded", "300 successful commands; stable actor inventories and two live dissolve instances",
                    "cycles=" + cyclesApplied + ";failure=" + cycleFailure + ";inventory=" + stableInventory +
                    ";profiles=" + wardrobeProfiles,
                    cyclesApplied && stableInventory && wardrobeProfiles == 2);
                Check("outfitSwitchesPreserveActorTransforms", "root position/rotation/scale and Animator identities remain stable",
                    "stable=" + stableTransforms, stableTransforms);

                await RunLiveWalkChecks();
                if (Argument("-wardrobeExecutionStage") == "Integration")
                    await RunSceneWardrobeIntegrationChecks();

                UnityEngine.Object.Destroy(secondPerformer.gameObject);
                await Awaitable.NextFrameAsync();
                await Awaitable.NextFrameAsync();
                if (secondParticleBody != null) UnityEngine.Object.Destroy(secondParticleBody.gameObject);
                await Awaitable.NextFrameAsync();
                int remainingProfiles = Resources.FindObjectsOfTypeAll<PerformerDissolveProfile>()
                    .Count(profile => profile != null && profile.name.EndsWith("(Performer Wardrobe Instance)", StringComparison.Ordinal));
                bool disposedSecond = secondPerformer == null && secondWardrobe == null && remainingProfiles == 1 &&
                    performer != null && wardrobe != null && wardrobe.Body == bodyRenderer;
                Check("destroyedPerformerReleasesWardrobeRuntime", "second actor/profile disposed; first actor remains live",
                    "secondDestroyed=" + (secondPerformer == null) + ";profiles=" + remainingProfiles +
                    ";firstLive=" + (performer != null && wardrobe != null), disposedSecond);
            }
            else
            {
                Check("performerOutfitsAreIndependent", "two configured performers", "second performer fixture missing", false);
                Check("performerLayerCommandsAreIndependent", "two configured performers", "second performer fixture missing", false);
                Check("secondPerformerFitBindingTargetsItsBody", "second body binding preflight", "second performer fixture missing", false);
                Check("wardrobeSwitchPreservesActivePerformanceState", "two configured performers", "second performer fixture missing", false);
                Check("failedPreparationRollsBackRendererAndSelection", "rollback fixture", "second performer fixture missing", false);
                Check("hundredSwitchCyclesRemainBounded", "two configured performers", "second performer fixture missing", false);
                Check("outfitSwitchesPreserveActorTransforms", "two configured performers", "second performer fixture missing", false);
                Check("destroyedPerformerReleasesWardrobeRuntime", "two configured performers", "second performer fixture missing", false);
            }

            bool serializedSourcesUnchanged = serializedSources.Length > 0 && serializedSources.All(source =>
                source.Key != null && UnityEditor.EditorJsonUtility.ToJson(source.Key) == source.Value);
            Check("sourceMaterialsAndProfilesRemainImmutable", "all catalog materials/profiles unchanged after Play wardrobe preview",
                serializedSources.Length + " source assets; unchanged=" + serializedSourcesUnchanged, serializedSourcesUnchanged);
        }

        private async Awaitable RunLiveWalkChecks()
        {
            var binder = performer != null ? performer.GetComponent<LaraWardrobe>() : null;
            var body = wardrobe != null ? wardrobe.Body : null;
            VisibilityCompletion revealResult = VisibilityCompletion.Visible;
            if (performer != null && performer.VisibilityState == PerformerVisibilityState.Hidden)
                revealResult = await performer.DissolveInAsync(.25f);
            bool isVisible = performer != null && revealResult == VisibilityCompletion.Visible &&
                performer.VisibilityState == PerformerVisibilityState.Visible;
            Check("walkFixtureVisibleForCameraSampling", "performer reaches stable Visible state", 
                "completion=" + revealResult + ";state=" + (performer != null ? performer.VisibilityState.ToString() : "missing"), isVisible);
            var shoePieces = binder != null ? binder.Pieces.Where(piece => piece.visible && piece.renderer != null &&
                piece.name.IndexOf("/shoe_", StringComparison.OrdinalIgnoreCase) >= 0).ToArray() : Array.Empty<LaraWardrobe.Piece>();
            _walkHeelReview = performer != null ? performer.GetComponent<LaraHeelReview>() : null;
            _walkFootwear = _walkHeelReview != null ? _walkHeelReview.Footwear : null;
            _walkShoes = shoePieces.Select(piece => piece.renderer).Where(renderer => renderer != null).Select(renderer =>
            {
                var vertices = renderer.sharedMesh.vertices;
                var triangles = renderer.sharedMesh.triangles;
                var edgeSet = new HashSet<long>();
                for (int i = 0; i + 2 < triangles.Length; i += 3)
                {
                    AddEdge(edgeSet, triangles[i], triangles[i + 1]);
                    AddEdge(edgeSet, triangles[i + 1], triangles[i + 2]);
                    AddEdge(edgeSet, triangles[i + 2], triangles[i]);
                }
                var metric = new ShoeMetric { renderer = renderer };
                metric.edges = edgeSet.SelectMany(edge => new[] { (int)(edge >> 32), (int)edge }).ToArray();
                metric.restEdgeLengths = new float[edgeSet.Count];
                int edgeAt = 0;
                foreach (long edge in edgeSet)
                {
                    int a = (int)(edge >> 32), b = (int)edge;
                    metric.restEdgeLengths[edgeAt++] = Vector3.Distance(
                        renderer.transform.TransformPoint(vertices[a]), renderer.transform.TransformPoint(vertices[b]));
                }
                return metric;
            }).ToArray();
            string fixtureState = "outfit=" + (wardrobe != null ? wardrobe.CurrentWardrobe.OutfitId : "missing") +
                ";mask=" + (wardrobe != null ? wardrobe.CurrentWardrobe.VisibleMask : -1) +
                ";shoes=" + (_walkShoes.Length > 0 ? string.Join(",", _walkShoes.Select(shoe => shoe.renderer.name)) : "missing") +
                ";profile=" + (_walkFootwear != null ? _walkFootwear.name : "missing");
            bool fixtureReady = performer != null && body != null && binder != null &&
                isVisible &&
                wardrobe.CurrentWardrobe.OutfitId == "third-outfit" && wardrobe.CurrentWardrobe.VisibleMask == 7 &&
                _walkShoes.Length > 0 && _walkShoes.All(shoe => shoe.renderer.enabled && shoe.restEdgeLengths.Length > 0) && _walkFootwear != null &&
                _walkHeelReview != null && _walkHeelReview.Footwear != null;
            Check("liveWalkUsesFullyLayeredMaidWithShoes", "third-outfit mask 7 and calibrated shoes visible", fixtureState, fixtureReady);
            if (!fixtureReady)
            {
                AddFailedWalkMetrics("walk fixture not ready");
                return;
            }

            try
            {
                _walkFeet = new[] { "lFoot", "rFoot" }.Select(name => body.bones.Single(bone => bone.name == name)).ToArray();
                _walkBakedMesh = new Mesh { name = "Wardrobe walk validation baked mesh" };
                _walkBakedMesh.MarkDynamic();
                _walkCamera = new GameObject("Wardrobe walk culling validation camera").AddComponent<Camera>();
                _walkCamera.fieldOfView = 42f;
                _walkCamera.aspect = 0.8f;
                _walkCamera.nearClipPlane = 0.02f;
                _walkCamera.farClipPlane = 80f;
                _walkCamera.clearFlags = CameraClearFlags.SolidColor;
                _walkCamera.backgroundColor = Color.black;
                _walkCamera.cullingMask = -1;
                var additionalCamera = _walkCamera.GetComponent<HDAdditionalCameraData>() ??
                    _walkCamera.gameObject.AddComponent<HDAdditionalCameraData>();
                additionalCamera.antialiasing = HDAdditionalCameraData.AntialiasingMode.None;
                _walkTarget = new RenderTexture(128, 160, 16, RenderTextureFormat.ARGB32);
                _walkTarget.Create();
                _walkCamera.targetTexture = _walkTarget;

                _collectingWalk = true;
                Vector3 start = performer.transform.position;
                Vector3 target = start + performer.transform.forward * 12f;
                target.y = start.y;
                var walk = performer.WalkToAsync(target);
                int waitedFrames = 0;
                while (performer.IsLocomoting && waitedFrames < 2400)
                {
                    waitedFrames++;
                    await Awaitable.NextFrameAsync();
                }
                bool timedOut = performer.IsLocomoting;
                if (timedOut)
                {
                    var settle = performer.WalkToAsync(performer.transform.position + performer.transform.forward * .9f);
                    await settle;
                }
                _collectingWalk = false;
                LocomotionCompletion completion = await walk;
                await Awaitable.NextFrameAsync();

                bool enoughWalkFrames = completion == LocomotionCompletion.Arrived && _walkFrames >= 181 && !timedOut;
                Check("liveWalkingFrameSamples", "production WalkToAsync arrives after at least 181 walking frames",
                    "completion=" + completion + ";walkingFrames=" + _walkFrames + ";waitedFrames=" + waitedFrames + ";timedOut=" + timedOut,
                    enoughWalkFrames);
                bool supportPass = _walkSupportedFrames > 0 && _walkSupportSamples > 0 &&
                    _walkRejectedSupportSamples == 0 && _walkMaximumSupportError <= .003f &&
                    _walkMaximumFloorPenetration <= .003f;
                Check("walkFootwearSupportAndFloorContact", "supported frames > 0; rejected samples = 0; contact and penetration <= 0.003 m",
                    "supportedFrames=" + _walkSupportedFrames + ";supportSamples=" + _walkSupportSamples +
                    ";rejectedSamples=" + _walkRejectedSupportSamples + ";maxSupportErrorMeters=" + Number(_walkMaximumSupportError) +
                    ";maxFloorPenetrationMeters=" + Number(_walkMaximumFloorPenetration), supportPass);
                int shoeEdgeCount = _walkShoes.Sum(shoe => shoe.restEdgeLengths.Length);
                bool rigidShoePass = shoeEdgeCount > 0 &&
                    _walkMaximumShoeShapeError <= .0001f;
                Check("walkRigidShoeInternalDistance", "shoe internal-edge error <= 0.0001 m",
                    "shoes=" + _walkShoes.Length + ";edges=" + shoeEdgeCount + ";maximumInternalEdgeErrorMeters=" + Number(_walkMaximumShoeShapeError),
                    rigidShoePass);
                bool cullingPass = _walkFrames >= 181 && _walkCullingExpectedSamples > 0 &&
                    _walkBoundsMisses == 0 && _walkCullingMisses == 0 && _walkVisibilityStateFailures == 0 &&
                    string.IsNullOrWhiteSpace(_walkSamplingError);
                Check("walkLiveRendererBoundsAndCulling", "live geometry inside renderer bounds; zero expected-visible misses; visibility flags valid",
                    "frames=" + _walkFrames + ";expectedVisibleSamples=" + _walkCullingExpectedSamples +
                    ";cullingMisses=" + _walkCullingMisses + ";boundsMisses=" + _walkBoundsMisses +
                    ";visibilityStateFailures=" + _walkVisibilityStateFailures + ";error=" + (_walkSamplingError ?? "none"), cullingPass);
            }
            catch (Exception exception)
            {
                _collectingWalk = false;
                _walkSamplingError = exception.ToString();
                Check("liveWalkingFrameSamples", "production WalkToAsync arrives after at least 181 walking frames", exception.Message, false);
                Check("walkFootwearSupportAndFloorContact", "supported frame and support/floor tolerances pass", exception.Message, false);
                Check("walkRigidShoeInternalDistance", "shoe internal-edge error <= 0.0001 m", exception.Message, false);
                Check("walkLiveRendererBoundsAndCulling", "zero expected-visible misses and valid renderer state", exception.Message, false);
            }
            finally
            {
                _collectingWalk = false;
                if (_walkCamera != null) Destroy(_walkCamera.gameObject);
                if (_walkTarget != null) { _walkTarget.Release(); Destroy(_walkTarget); }
                if (_walkBakedMesh != null) Destroy(_walkBakedMesh);
                _walkCamera = null; _walkTarget = null; _walkBakedMesh = null;
            }
        }

        private void LateUpdate()
        {
            if (!_collectingWalk || !Application.isPlaying || performer == null || wardrobe == null) return;
            if (performer.LocomotionState != PerformerLocomotionState.Walking) return;
            try
            {
                _walkFrames++;
                _walkCamera.transform.position = performer.transform.position + Vector3.up * 1.2f - performer.transform.forward * 3.5f;
                _walkCamera.transform.LookAt(performer.transform.position + Vector3.up * .9f);
                var planes = GeometryUtility.CalculateFrustumPlanes(_walkCamera);
                var binder = performer.GetComponent<LaraWardrobe>();
                var expectedVisible = new List<SkinnedMeshRenderer>();
                foreach (var piece in binder.Pieces)
                {
                    var renderer = piece.renderer;
                    if (renderer == null || !piece.visible) continue;
                    if (!renderer.enabled || renderer.forceRenderingOff || !renderer.updateWhenOffscreen)
                        _walkVisibilityStateFailures++;
                    if (!renderer.enabled || renderer.forceRenderingOff) continue;
                    renderer.BakeMesh(_walkBakedMesh);
                    var vertices = _walkBakedMesh.vertices;
                    if (vertices.Length == 0) continue;
                    Bounds actual = new Bounds(renderer.transform.TransformPoint(vertices[0]), Vector3.zero);
                    for (int i = 1; i < vertices.Length; i++) actual.Encapsulate(renderer.transform.TransformPoint(vertices[i]));
                    Bounds envelope = renderer.bounds;
                    envelope.Expand(.004f);
                    if (!envelope.Contains(actual.min) || !envelope.Contains(actual.max)) _walkBoundsMisses++;
                    if (GeometryUtility.TestPlanesAABB(planes, actual))
                    {
                        _walkCullingExpectedSamples++;
                        expectedVisible.Add(renderer);
                    }
                }

                // Force the validation camera to cull and render the just-sampled live pose.
                _walkCamera.Render();
                foreach (var renderer in expectedVisible)
                    if (renderer == null || !renderer.isVisible) _walkCullingMisses++;

                foreach (var shoe in _walkShoes)
                {
                    var renderer = shoe.renderer;
                    renderer.BakeMesh(_walkBakedMesh);
                    var vertices = _walkBakedMesh.vertices;
                    for (int i = 0; i + 1 < shoe.edges.Length; i += 2)
                    {
                        int a = shoe.edges[i], b = shoe.edges[i + 1];
                        float current = Vector3.Distance(renderer.transform.TransformPoint(vertices[a]),
                            renderer.transform.TransformPoint(vertices[b]));
                        _walkMaximumShoeShapeError = Mathf.Max(_walkMaximumShoeShapeError,
                            Mathf.Abs(current - shoe.restEdgeLengths[i / 2]));
                    }
                    for (int i = 0; i < vertices.Length; i++)
                    {
                        float belowGround = _walkHeelReview.GroundY - renderer.transform.TransformPoint(vertices[i]).y;
                        _walkMaximumFloorPenetration = Mathf.Max(_walkMaximumFloorPenetration, belowGround);
                    }
                    for (int side = 0; side < 2; side++)
                    {
                        float stance = side == 0 ? _walkHeelReview.LeftStance : _walkHeelReview.RightStance;
                        if (stance <= .99f) continue;
                        if (_walkSupportedFrames < _walkFrames) _walkSupportedFrames++;
                        Vector3 heel = _walkFeet[side].TransformPoint(side == 0 ? _walkFootwear.leftHeel : _walkFootwear.rightHeel);
                        Vector3 toe = _walkFeet[side].TransformPoint(side == 0 ? _walkFootwear.leftToe : _walkFootwear.rightToe);
                        float error = Mathf.Abs(Mathf.Min(heel.y, toe.y) - _walkHeelReview.GroundY);
                        _walkSupportSamples++;
                        _walkMaximumSupportError = Mathf.Max(_walkMaximumSupportError, error);
                        if (error > .003f) _walkRejectedSupportSamples++;
                    }
                }
            }
            catch (Exception exception)
            {
                _walkSamplingError = exception.ToString();
                _collectingWalk = false;
            }
        }

        private void AddFailedWalkMetrics(string reason)
        {
            Check("liveWalkingFrameSamples", "production WalkToAsync arrives after at least 181 walking frames", reason, false);
            Check("walkFootwearSupportAndFloorContact", "supported frame and support/floor tolerances pass", reason, false);
            Check("walkRigidShoeInternalDistance", "shoe internal-edge error <= 0.0001 m", reason, false);
            Check("walkLiveRendererBoundsAndCulling", "zero expected-visible misses and valid renderer state", reason, false);
        }

        private static void AddEdge(HashSet<long> edges, int first, int second)
        {
            uint a = (uint)Math.Min(first, second), b = (uint)Math.Max(first, second);
            edges.Add(((long)a << 32) | b);
        }

        private static string Number(float value) => value.ToString("R", System.Globalization.CultureInfo.InvariantCulture);

        private async Awaitable RunSceneWardrobeIntegrationChecks()
        {
            if (performer == null || secondPerformer == null || wardrobe == null || secondWardrobe == null)
            {
                Check("sceneRecallTwoActorFixture", "two configured actors", "missing actor", false);
                Check("sceneRecallInvalidBindingIsAtomic", "invalid snapshot leaves both actors unchanged and visible", "missing actor", false);
                Check("sceneRecallRestoresPartialOutfitAndResolvedHair", "both captured snapshots restored without full-layer flash", "missing actor", false);
                Check("sceneRecallLayerCommandsRemainIndependent", "relative commands remain isolated after recall", "missing actor", false);
                Check("dissolveOutfitTransitionSettlesAndRebinds", "wardrobe transition returns stable Visible state", "missing actor", false);
                Check("activeDissolveRejectsWardrobeRebind", "active effect binding remains owned; prior state and mesh preserved", "missing actor", false);
                return;
            }

            const string hairId = "first-outfit/EmikoHair_59875";
            try
            {
                var bindingA = performer.GetComponent<SceneWardrobeBinding>() ?? performer.gameObject.AddComponent<SceneWardrobeBinding>();
                var bindingB = secondPerformer.GetComponent<SceneWardrobeBinding>() ?? secondPerformer.gameObject.AddComponent<SceneWardrobeBinding>();
                bindingA.SetPerformerBindingId("lara");
                bindingB.SetPerformerBindingId("partner");

                if (secondPerformer.VisibilityState == PerformerVisibilityState.Hidden)
                    await secondPerformer.DissolveInAsync(.15f);
                var targetAResult = await performer.TryRemoveLayerAsync();
                await WaitForWardrobeQueuesToSettle(wardrobe);
                bindingA.CaptureCurrent();
                var targetBOutfit = await secondPerformer.OutfitAsync("first-outfit");
                var targetBHair = await secondPerformer.SetHairAsync(hairId);
                await WaitForWardrobeQueuesToSettle(secondWardrobe);
                bindingB.CaptureCurrent();
                SceneWardrobeSnapshot targetA = bindingA.Snapshot;
                SceneWardrobeSnapshot targetB = bindingB.Snapshot;
                bool captured = targetAResult.Status == WardrobeChangeStatus.Applied &&
                    targetA.presetId == "third-outfit" && targetA.layerCeiling == 1 && targetA.hairAction == WardrobeHairAction.Set &&
                    targetBOutfit.Succeeded && targetBHair.Succeeded && targetB.presetId == "first-outfit" &&
                    targetB.layerCeiling == 0 && targetB.hairAction == WardrobeHairAction.Set &&
                    targetA.performerBindingId == "lara" && targetB.performerBindingId == "partner";
                Check("sceneRecallTwoActorFixture", "stable bindings capture outfit, exact ceiling and explicit resolved hair",
                    "A=" + targetA.presetId + "/" + targetA.layerCeiling + "/" + targetA.resolvedHairId +
                    ";B=" + targetB.presetId + "/" + targetB.layerCeiling + "/" + targetB.resolvedHairId,
                    captured);

                var unrelatedAOutfit = await performer.OutfitAsync("first-outfit");
                var unrelatedAHair = await performer.SetHairAsync("none");
                var unrelatedBOutfit = await secondPerformer.OutfitAsync("maid");
                var unrelatedBHair = await secondPerformer.SetHairAsync("none");
                await WaitForWardrobeQueuesToSettle(wardrobe, secondWardrobe);
                bool unrelatedChangesApplied = unrelatedAOutfit.Succeeded && unrelatedAHair.Succeeded &&
                    unrelatedBOutfit.Succeeded && unrelatedBHair.Succeeded &&
                    wardrobe.CurrentWardrobe.OutfitId == "first-outfit" && wardrobe.CurrentWardrobe.EffectiveHairId == "none" &&
                    secondWardrobe.CurrentWardrobe.OutfitId == "third-outfit" && secondWardrobe.CurrentWardrobe.VisibleMask == 7 &&
                    secondWardrobe.CurrentWardrobe.EffectiveHairId == "none";
                Check("sceneRecallStartsFromUnrelatedSelections", "both actors have changed outfits and hair since capture",
                    "A=" + wardrobe.CurrentWardrobe.OutfitId + "/" + wardrobe.CurrentWardrobe.EffectiveHairId +
                    ";B=" + secondWardrobe.CurrentWardrobe.OutfitId + "/" + secondWardrobe.CurrentWardrobe.VisibleMask +
                    "/" + secondWardrobe.CurrentWardrobe.EffectiveHairId + ";results=" +
                    unrelatedAOutfit.Status + "/" + unrelatedAHair.Status + "/" +
                    unrelatedBOutfit.Status + "/" + unrelatedBHair.Status, unrelatedChangesApplied);

                WardrobeState beforeA = wardrobe.CurrentWardrobe;
                WardrobeState beforeB = secondWardrobe.CurrentWardrobe;
                var invalidSnapshot = targetB.Clone();
                invalidSnapshot.resolvedHairId = "missing-hair/not-released";
                bindingB.SetSnapshot(invalidSnapshot);
                var rejected = await SceneWardrobeBinding.RecallAllAsync(new[] { bindingA, bindingB },
                    WardrobeTransition.Dissolve(.15f, .15f));
                bool invalidAtomic = !rejected.Succeeded && wardrobe.CurrentWardrobe.OutfitId == beforeA.OutfitId &&
                    wardrobe.CurrentWardrobe.VisibleMask == beforeA.VisibleMask && secondWardrobe.CurrentWardrobe.OutfitId == beforeB.OutfitId &&
                    secondWardrobe.CurrentWardrobe.VisibleMask == beforeB.VisibleMask &&
                    performer.VisibilityState == PerformerVisibilityState.Visible && secondPerformer.VisibilityState == PerformerVisibilityState.Visible;
                Check("sceneRecallInvalidBindingIsAtomic", "invalid snapshot leaves both actors unchanged and visible",
                    "failed=" + !rejected.Succeeded + ";A=" + wardrobe.CurrentWardrobe.OutfitId + "/" + wardrobe.CurrentWardrobe.VisibleMask +
                    ";B=" + secondWardrobe.CurrentWardrobe.OutfitId + "/" + secondWardrobe.CurrentWardrobe.VisibleMask +
                    ";visible=" + performer.VisibilityState + "/" + secondPerformer.VisibilityState + ";error=" + rejected.Message,
                    invalidAtomic);
                bindingB.SetSnapshot(targetB);

                bool observingRecall = true, committedAHidden = true, committedBHidden = true, fullyDressedFlash = false;
                int commitEventsA = 0, commitEventsB = 0;
                Action<WardrobeState> observeA = state =>
                {
                    if (!observingRecall) return;
                    commitEventsA++;
                    committedAHidden &= performer.VisibilityState == PerformerVisibilityState.Hidden;
                    fullyDressedFlash |= state.VisibleMask == 7;
                };
                Action<WardrobeState> observeB = state =>
                {
                    if (!observingRecall) return;
                    commitEventsB++;
                    committedBHidden &= secondPerformer.VisibilityState == PerformerVisibilityState.Hidden;
                    fullyDressedFlash |= state.VisibleMask == 7;
                };
                wardrobe.WardrobeChanged += observeA;
                secondWardrobe.WardrobeChanged += observeB;
                var recalled = await SceneWardrobeBinding.RecallAllAsync(new[] { bindingA, bindingB },
                    WardrobeTransition.Dissolve(.18f, .18f));
                await WaitForWardrobeQueuesToSettle(wardrobe, secondWardrobe);
                observingRecall = false;
                wardrobe.WardrobeChanged -= observeA;
                secondWardrobe.WardrobeChanged -= observeB;

                bool statesRestored = recalled.Succeeded && wardrobe.CurrentWardrobe.OutfitId == "third-outfit" &&
                    wardrobe.CurrentWardrobe.VisibleMask == 3 && wardrobe.CurrentWardrobe.EffectiveHairId == targetA.resolvedHairId &&
                    secondWardrobe.CurrentWardrobe.OutfitId == "first-outfit" && secondWardrobe.CurrentWardrobe.VisibleMask == 1 &&
                    secondWardrobe.CurrentWardrobe.EffectiveHairId == targetB.resolvedHairId &&
                    performer.VisibilityState == PerformerVisibilityState.Visible && secondPerformer.VisibilityState == PerformerVisibilityState.Visible;
                bool concealedCommit = commitEventsA == 1 && commitEventsB == 1 && committedAHidden && committedBHidden && !fullyDressedFlash;
                Check("sceneRecallRestoresPartialOutfitAndResolvedHair", "recall restores both captured partial states; every commit occurs hidden with no full-layer flash",
                    "succeeded=" + recalled.Succeeded + ";A=" + wardrobe.CurrentWardrobe.OutfitId + "/" + wardrobe.CurrentWardrobe.VisibleMask +
                    "/" + wardrobe.CurrentWardrobe.EffectiveHairId + ";B=" + secondWardrobe.CurrentWardrobe.OutfitId +
                    "/" + secondWardrobe.CurrentWardrobe.VisibleMask + "/" + secondWardrobe.CurrentWardrobe.EffectiveHairId +
                    ";commitEvents=" + commitEventsA + "/" + commitEventsB + ";hidden=" + committedAHidden + "/" + committedBHidden +
                    ";fullFlash=" + fullyDressedFlash + ";message=" + recalled.Message,
                    statesRestored && concealedCommit);

                var addA = await performer.TryAddLayerAsync();
                var removeA = await performer.TryRemoveLayerAsync();
                await WaitForWardrobeQueuesToSettle(wardrobe, secondWardrobe);
                bool independentLayers = addA.Status == WardrobeChangeStatus.Applied && removeA.Status == WardrobeChangeStatus.Applied &&
                    wardrobe.CurrentWardrobe.VisibleMask == 3 && secondWardrobe.CurrentWardrobe.VisibleMask == 1;
                Check("sceneRecallLayerCommandsRemainIndependent", "relative commands remain isolated after recall",
                    "A=" + wardrobe.CurrentWardrobe.VisibleMask + ";B=" + secondWardrobe.CurrentWardrobe.VisibleMask,
                    independentLayers);

                var transition = await performer.OutfitAsync("maid", WardrobeTransition.Dissolve(.15f, .15f));
                await WaitForWardrobeQueuesToSettle(wardrobe, secondWardrobe);
                bool transitionSettled = transition.Status == WardrobeChangeStatus.Applied &&
                    wardrobe.CurrentWardrobe.OutfitId == "third-outfit" && wardrobe.CurrentWardrobe.VisibleMask == 7 &&
                    performer.VisibilityState == PerformerVisibilityState.Visible && !performer.IsDissolving &&
                    secondWardrobe.CurrentWardrobe.OutfitId == "first-outfit" && !secondPerformer.IsDissolving;
                int liveWardrobeProfiles = Resources.FindObjectsOfTypeAll<PerformerDissolveProfile>()
                    .Count(profile => profile != null && profile.name.EndsWith("(Performer Wardrobe Instance)", StringComparison.Ordinal));
                transitionSettled &= liveWardrobeProfiles == 2;
                Check("dissolveOutfitTransitionSettlesAndRebinds", "outfit changes through out/hidden-rebind/in and settles with one profile per actor",
                    "status=" + transition.Status + ";A=" + wardrobe.CurrentWardrobe.OutfitId + "/" + wardrobe.CurrentWardrobe.VisibleMask +
                    ";visibility=" + performer.VisibilityState + ";active=" + performer.IsDissolving +
                    ";B=" + secondWardrobe.CurrentWardrobe.OutfitId + "/" + secondPerformer.VisibilityState + "/" + secondPerformer.IsDissolving +
                    ";changing=" + wardrobe.CurrentWardrobe.IsChanging + "/" + secondWardrobe.CurrentWardrobe.IsChanging +
                    ";profiles=" + liveWardrobeProfiles,
                    transitionSettled);

                Mesh priorMesh = wardrobe.Body.sharedMesh;
                WardrobeState priorState = wardrobe.CurrentWardrobe;
                var externalDissolve = performer.DissolveOutAsync(.24f);
                var contestedSwitch = await performer.OutfitAsync("first-outfit");
                bool ownershipProtected = contestedSwitch.Status == WardrobeChangeStatus.Failed &&
                    contestedSwitch.FailureCode == WardrobeFailureCode.EffectRebindFailed &&
                    wardrobe.Body.sharedMesh == priorMesh && wardrobe.CurrentWardrobe.OutfitId == priorState.OutfitId &&
                    wardrobe.CurrentWardrobe.VisibleMask == priorState.VisibleMask && performer.IsDissolving;
                var hiddenResult = await externalDissolve;
                var recovery = await performer.DissolveInAsync(.18f);
                bool recovered = hiddenResult == VisibilityCompletion.Hidden && recovery == VisibilityCompletion.Visible &&
                    performer.VisibilityState == PerformerVisibilityState.Visible && !performer.IsDissolving &&
                    wardrobe.CurrentWardrobe.OutfitId == priorState.OutfitId && wardrobe.Body.sharedMesh == priorMesh;
                Check("activeDissolveRejectsWardrobeRebind", "active effect binding remains owned; prior state and mesh preserved; dissolve recovery succeeds",
                    "switch=" + contestedSwitch.Status + "/" + contestedSwitch.FailureCode + ";mesh=" + (wardrobe.Body.sharedMesh == priorMesh) +
                    ";state=" + wardrobe.CurrentWardrobe.OutfitId + "/" + wardrobe.CurrentWardrobe.VisibleMask +
                    ";hidden=" + hiddenResult + ";recovery=" + recovery + ";visible=" + performer.VisibilityState,
                    ownershipProtected && recovered);
            }
            catch (Exception exception)
            {
                foreach (string assertion in new[] { "sceneRecallTwoActorFixture", "sceneRecallInvalidBindingIsAtomic",
                    "sceneRecallRestoresPartialOutfitAndResolvedHair", "sceneRecallLayerCommandsRemainIndependent",
                    "dissolveOutfitTransitionSettlesAndRebinds", "activeDissolveRejectsWardrobeRebind" })
                    if (!_assertions.Any(existing => existing.name == assertion))
                        Check(assertion, "scene recall integration completes", exception.ToString(), false);
                Check("sceneRecallIntegrationException", "no exception", exception.ToString(), false);
            }
        }

        private static async Awaitable WaitForWardrobeQueuesToSettle(params PerformerWardrobe[] wardrobes)
        {
            for (int frame = 0; frame < 16; ++frame)
            {
                if (wardrobes.All(item => item != null && !item.CurrentWardrobe.IsChanging)) return;
                await Awaitable.NextFrameAsync();
            }
            throw new TimeoutException("A wardrobe command queue did not settle within 16 frames.");
        }

        private static PerformerExpression CreateValidationExpression(Animator animator, SkinnedMeshRenderer body)
        {
            if (animator == null || body == null || body.sharedMesh == null ||
                body.sharedMesh.GetBlendShapeIndex("CapturedOpening") < 0) return null;
            string rendererPath = HierarchyPath(animator.transform, body.transform);
            if (rendererPath == null) return null;
            var expression = ScriptableObject.CreateInstance<PerformerExpression>();
            expression.name = "Wardrobe State Preservation Expression";
            var clip = new AnimationClip { name = "Wardrobe State Preservation Expression Clip" };
            var serialized = new UnityEditor.SerializedObject(expression);
            var clipProperty = serialized.FindProperty("clip");
            var channels = serialized.FindProperty("channels");
            if (clipProperty == null || channels == null)
            {
                UnityEngine.Object.Destroy(expression);
                UnityEngine.Object.Destroy(clip);
                return null;
            }
            clipProperty.objectReferenceValue = clip;
            channels.arraySize = 1;
            var channel = channels.GetArrayElementAtIndex(0);
            channel.FindPropertyRelative("rendererPath").stringValue = rendererPath;
            channel.FindPropertyRelative("blendShapeName").stringValue = "CapturedOpening";
            channel.FindPropertyRelative("targetWeight").floatValue = 72f;
            var boneChannels = serialized.FindProperty("boneChannels");
            if (boneChannels != null) boneChannels.arraySize = 0;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return expression;
        }

        private static string HierarchyPath(Transform root, Transform target)
        {
            if (root == null || target == null) return null;
            if (root == target) return string.Empty;
            var names = new List<string>();
            var current = target;
            while (current != root)
            {
                if (current == null) return null;
                names.Add(current.name);
                current = current.parent;
            }
            names.Reverse();
            return string.Join("/", names);
        }

        private void Check(string name, string expected, string actual, bool passed) =>
            _assertions.Add(new Assertion { name = name, expected = expected, actual = actual, passed = passed });

        private void WriteReport(string started)
        {
            string reportPath = Argument("-wardrobeExecutionReport");
            var report = new Report
            {
                stage = Argument("-wardrobeExecutionStage"), sourceHash = Argument("-wardrobeExecutionSourceHash"),
                inputAssetHashes = new[] { Argument("-wardrobeExecutionInputHash") },
                startedUtc = started, completedUtc = DateTime.UtcNow.ToString("O"),
                passed = false,
                complete = false,
                assertions = _assertions.ToArray(), artifacts = Array.Empty<string>(),
                limitations = Array.Empty<string>()
            };
            report.complete = _assertions.All(assertion => assertion.passed);
            report.passed = report.complete;
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath)));
            File.WriteAllText(reportPath, JsonUtility.ToJson(report, true));
            Debug.Log((report.passed ? "WARDROBE_RUNTIME_GATE_PASSED: " : "WARDROBE_RUNTIME_GATE_FAILED: ") + reportPath);
            UnityEditor.EditorApplication.Exit(report.passed ? 0 : 1);
        }

        private static string Argument(string key)
        {
            var args = Environment.GetCommandLineArgs(); int at = Array.IndexOf(args, key);
            return at >= 0 && at + 1 < args.Length ? args[at + 1].Trim('"') : string.Empty;
        }
#endif
    }
}
