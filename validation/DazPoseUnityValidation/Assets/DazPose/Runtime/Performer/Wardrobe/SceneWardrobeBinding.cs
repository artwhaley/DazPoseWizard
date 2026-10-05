using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DazPose.Performer
{
    [Serializable]
    public sealed class SceneWardrobeSnapshot
    {
        public string performerBindingId;
        public string presetId;
        public string variantId;
        [Range(-1, 2)] public int layerCeiling = 2;
        public WardrobeHairAction hairAction = WardrobeHairAction.Clear;
        public string resolvedHairId;

        public SceneWardrobeSnapshot Clone() => new SceneWardrobeSnapshot
        {
            performerBindingId = performerBindingId,
            presetId = presetId,
            variantId = variantId,
            layerCeiling = layerCeiling,
            hairAction = hairAction,
            resolvedHairId = resolvedHairId
        };
    }

    public sealed class SceneWardrobeRecallResult
    {
        public bool Succeeded { get; }
        public string Message { get; }
        public WardrobeChangeResult[] Changes { get; }

        internal SceneWardrobeRecallResult(bool succeeded, string message, IEnumerable<WardrobeChangeResult> changes)
        { Succeeded = succeeded; Message = message; Changes = changes?.ToArray() ?? Array.Empty<WardrobeChangeResult>(); }
    }

    [DisallowMultipleComponent]
    [AddComponentMenu("Performer/Scene Wardrobe Binding")]
    public sealed class SceneWardrobeBinding : MonoBehaviour
    {
        [SerializeField] private SceneWardrobeSnapshot snapshot = new SceneWardrobeSnapshot();

        public string PerformerBindingId => snapshot != null ? snapshot.performerBindingId : null;
        public SceneWardrobeSnapshot Snapshot => snapshot?.Clone();

        public void SetPerformerBindingId(string bindingId)
        {
            bindingId = (bindingId ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(bindingId)) throw new ArgumentException("Performer binding ID is required.", nameof(bindingId));
            snapshot ??= new SceneWardrobeSnapshot();
            snapshot.performerBindingId = bindingId;
        }

        public void SetSnapshot(SceneWardrobeSnapshot value)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            snapshot = value.Clone();
        }

        public void CaptureCurrent(string variantId = null)
        {
            var performer = GetComponent<SuccubusPerformer>();
            var wardrobe = GetComponent<PerformerWardrobe>();
            if (performer == null || wardrobe == null)
                throw new InvalidOperationException("Scene wardrobe binding must share a GameObject with SuccubusPerformer and PerformerWardrobe.");
            if (string.IsNullOrWhiteSpace(PerformerBindingId))
                throw new InvalidOperationException("Set a stable performer binding ID before capturing a scene snapshot.");
            WardrobeState state = wardrobe.CurrentWardrobe;
            if (state == null || state.IsChanging)
                throw new InvalidOperationException("Wait for the performer wardrobe queue to settle before capturing a scene snapshot.");
            string resolvedHair = HasResolvedHair(state.EffectiveHairId) ? state.EffectiveHairId : string.Empty;
            snapshot = new SceneWardrobeSnapshot
            {
                performerBindingId = snapshot.performerBindingId,
                presetId = state.OutfitId ?? "unclothed",
                variantId = (variantId ?? string.Empty).Trim(),
                layerCeiling = state.LayerCeiling,
                hairAction = resolvedHair.Length == 0 ? WardrobeHairAction.Clear : WardrobeHairAction.Set,
                resolvedHairId = resolvedHair
            };
        }

        public Awaitable<WardrobeChangeResult> RecallAsync(WardrobeTransition transition = default)
        {
            var wardrobe = GetComponent<PerformerWardrobe>();
            if (wardrobe == null)
                return CompletedFailure(WardrobeFailureCode.MissingAsset,
                    "Scene wardrobe binding has no PerformerWardrobe component.");
            return wardrobe.RecallSnapshotAsync(Snapshot, transition);
        }

        /// <summary>
        /// Validates every actor first, conceals all visible actors, commits exact snapshots,
        /// and only then reveals actors that were visible before the recall.
        /// </summary>
        public static async Awaitable<SceneWardrobeRecallResult> RecallAllAsync(
            SceneWardrobeBinding[] bindings, WardrobeTransition transition)
        {
            if (bindings == null || bindings.Length == 0)
                return new SceneWardrobeRecallResult(false, "At least one scene wardrobe binding is required.", null);
            var targets = new List<RecallTarget>(bindings.Length);
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var actors = new HashSet<SuccubusPerformer>();
            foreach (var binding in bindings)
            {
                if (binding == null) return new SceneWardrobeRecallResult(false, "A scene wardrobe binding is missing.", null);
                var performer = binding.GetComponent<SuccubusPerformer>();
                var wardrobe = binding.GetComponent<PerformerWardrobe>();
                var desired = binding.Snapshot;
                if (performer == null || wardrobe == null || desired == null)
                    return new SceneWardrobeRecallResult(false, "Binding " + binding.name + " lacks a performer, wardrobe or snapshot.", null);
                if (string.IsNullOrWhiteSpace(desired.performerBindingId) || !ids.Add(desired.performerBindingId))
                    return new SceneWardrobeRecallResult(false, "Performer binding IDs must be present and unique.", null);
                if (!actors.Add(performer))
                    return new SceneWardrobeRecallResult(false, "A performer cannot be targeted by more than one scene binding.", null);
                if (!wardrobe.TryPrepareSceneSnapshot(desired, out string error))
                    return new SceneWardrobeRecallResult(false, "Cannot prepare " + desired.performerBindingId + ": " + error, null);
                WardrobeState current = wardrobe.CurrentWardrobe;
                if (current == null || current.IsChanging)
                    return new SceneWardrobeRecallResult(false, "Performer " + desired.performerBindingId + " has pending wardrobe work.", null);
                var prior = CaptureSnapshot(desired.performerBindingId, current, string.Empty);
                bool wasVisible = performer.VisibilityState == PerformerVisibilityState.Visible;
                if (!wasVisible && performer.VisibilityState != PerformerVisibilityState.Hidden)
                    return new SceneWardrobeRecallResult(false, "Performer " + desired.performerBindingId + " is changing visibility.", null);
                targets.Add(new RecallTarget
                {
                    binding = binding, performer = performer, wardrobe = wardrobe,
                    desired = desired, prior = prior, wasVisible = wasVisible
                });
            }

            bool dissolve = transition.kind == WardrobeTransitionKind.Dissolve;
            if (!dissolve && targets.Any(target => target.wasVisible))
                return new SceneWardrobeRecallResult(false,
                    "A visible multi-performer recall requires a Dissolve transition so no actor reveals a partial scene.", null);
            if (transition.kind != WardrobeTransitionKind.Cut && !dissolve)
                return new SceneWardrobeRecallResult(false, "Unknown scene wardrobe transition kind.", null);

            object owner = new object();
            var acquired = new List<RecallTarget>();
            var changed = new List<WardrobeChangeResult>();
            var committed = new List<RecallTarget>();
            string failure = null;
            try
            {
                foreach (var target in targets)
                {
                    if (!target.performer.TryAcquireWardrobeEffectTransition(owner, out string error))
                    { failure = "Could not acquire transition ownership for " + target.desired.performerBindingId + ": " + error; break; }
                    acquired.Add(target);
                }
                if (failure != null) return new SceneWardrobeRecallResult(false, failure, changed);

                if (dissolve)
                foreach (var target in targets.Where(item => item.wasVisible))
                {
                    var hidden = await target.performer.DissolveOutForWardrobeAsync(owner,
                        Mathf.Max(.001f, transition.outSeconds));
                    if (hidden != VisibilityCompletion.Hidden || target.performer.VisibilityState != PerformerVisibilityState.Hidden)
                    { failure = "Dissolve-out failed for " + target.desired.performerBindingId + ": " + hidden; break; }
                }
                if (failure != null)
                {
                    await RestoreVisibility(targets, owner, transition.inSeconds);
                    return new SceneWardrobeRecallResult(false, failure, changed);
                }

                foreach (var target in targets)
                {
                    WardrobeChangeResult result = await target.wardrobe.RecallSnapshotWhileOwnedAsync(target.desired, owner);
                    changed.Add(result);
                    if (result.Status == WardrobeChangeStatus.Applied || result.Status == WardrobeChangeStatus.AlreadyEquipped ||
                        result.Status == WardrobeChangeStatus.NoChange)
                    { committed.Add(target); continue; }
                    failure = "Wardrobe commit failed for " + target.desired.performerBindingId + ": " +
                        result.FailureCode + " / " + result.Message;
                    break;
                }

                if (failure != null)
                {
                    foreach (var target in committed.AsEnumerable().Reverse())
                    {
                        WardrobeChangeResult rollback = await target.wardrobe.RecallSnapshotWhileOwnedAsync(target.prior, owner);
                        if (rollback.Status != WardrobeChangeStatus.Applied && rollback.Status != WardrobeChangeStatus.AlreadyEquipped &&
                            rollback.Status != WardrobeChangeStatus.NoChange)
                            failure += "; rollback failed for " + target.desired.performerBindingId + ": " + rollback.Message;
                    }
                    await RestoreVisibility(targets, owner, transition.inSeconds);
                    return new SceneWardrobeRecallResult(false, failure, changed);
                }

                if (dissolve)
                foreach (var target in targets.Where(item => item.wasVisible))
                {
                    var visible = await target.performer.DissolveInForWardrobeAsync(owner,
                        Mathf.Max(.001f, transition.inSeconds));
                    if (visible != VisibilityCompletion.Visible || target.performer.VisibilityState != PerformerVisibilityState.Visible)
                    { failure = "Dissolve-in failed for " + target.desired.performerBindingId + ": " + visible; break; }
                }
                if (failure != null)
                    return new SceneWardrobeRecallResult(false,
                        failure + "; committed scene snapshots remain explicit in each performer's current wardrobe state.", changed);
                return new SceneWardrobeRecallResult(true, "All scene wardrobe snapshots prepared and committed.", changed);
            }
            catch (Exception exception)
            {
                failure = exception.Message;
                foreach (var target in committed.AsEnumerable().Reverse())
                {
                    try { await target.wardrobe.RecallSnapshotWhileOwnedAsync(target.prior, owner); }
                    catch (Exception rollbackError) { failure += "; rollback exception: " + rollbackError.Message; }
                }
                try { await RestoreVisibility(targets, owner, transition.inSeconds); }
                catch (Exception visibilityError) { failure += "; visibility recovery exception: " + visibilityError.Message; }
                return new SceneWardrobeRecallResult(false, failure, changed);
            }
            finally
            {
                foreach (var target in acquired) target.performer.ReleaseWardrobeEffectTransition(owner);
            }
        }

        private sealed class RecallTarget
        {
            public SceneWardrobeBinding binding;
            public SuccubusPerformer performer;
            public PerformerWardrobe wardrobe;
            public SceneWardrobeSnapshot desired, prior;
            public bool wasVisible;
        }

        private static async Awaitable RestoreVisibility(IEnumerable<RecallTarget> targets, object owner, float durationSeconds)
        {
            foreach (var target in targets.Where(item => item.wasVisible && item.performer != null &&
                item.performer.VisibilityState == PerformerVisibilityState.Hidden))
                await target.performer.DissolveInForWardrobeAsync(owner, Mathf.Max(.001f, durationSeconds));
        }

        private static SceneWardrobeSnapshot CaptureSnapshot(string bindingId, WardrobeState state, string variantId)
        {
            string resolvedHair = HasResolvedHair(state.EffectiveHairId) ? state.EffectiveHairId : string.Empty;
            return new SceneWardrobeSnapshot
            {
                performerBindingId = bindingId,
                presetId = state.OutfitId ?? "unclothed",
                variantId = variantId ?? string.Empty,
                layerCeiling = state.LayerCeiling,
                hairAction = resolvedHair.Length == 0 ? WardrobeHairAction.Clear : WardrobeHairAction.Set,
                resolvedHairId = resolvedHair
            };
        }

        private static bool HasResolvedHair(string hairId) =>
            !string.IsNullOrWhiteSpace(hairId) && !string.Equals(hairId, "none", StringComparison.OrdinalIgnoreCase);

        private Awaitable<WardrobeChangeResult> CompletedFailure(WardrobeFailureCode code, string message)
        {
            var source = new AwaitableCompletionSource<WardrobeChangeResult>();
            source.TrySetResult(new WardrobeChangeResult(WardrobeChangeStatus.Failed, null, null, code, message: message));
            return source.Awaitable;
        }
    }
}
