using System;
using DazPose.Performer.HandGrip;
using UnityEngine;

namespace DazPose.Performer
{
    /// <summary>Finite, typed author commands owned by one PerformanceRun.</summary>
    public sealed class ScopedPerformer
    {
        private readonly PerformanceRun run;
        private readonly SuccubusPerformer actor;

        internal ScopedPerformer(PerformanceRun owner, SuccubusPerformer boundActor)
        { run = owner; actor = boundActor; }

        public Awaitable<PoseCompletion> PoseAsync(PerformerPose pose) => PoseAsync(pose, PoseTransition.Snap);
        public Awaitable<PoseCompletion> PoseAsync(PerformerPose pose, PoseTransition transition) =>
            run.Execute("Pose", () => actor.PoseAsync(pose, transition), result => result == PoseCompletion.Settled,
                result => result.ToString(), captureCancellation: () => actor.CaptureCancellation(PerformanceCommand.Pose),
                captureOwnership: () => actor.CapturePersistentCancellation(PerformanceCommand.Pose));

        public Awaitable<ExpressionCompletion> ExpressionAsync(PerformerExpression expression) =>
            ExpressionAsync(expression, 1f, actor.DefaultExpressionBlendTime);
        public Awaitable<ExpressionCompletion> ExpressionAsync(PerformerExpression expression, float intensity) =>
            ExpressionAsync(expression, intensity, actor.DefaultExpressionBlendTime);
        public Awaitable<ExpressionCompletion> ExpressionAsync(PerformerExpression expression, float intensity, float blendTime) =>
            run.Execute("Expression", () => actor.ExpressionAsync(expression, intensity, blendTime), result => result == ExpressionCompletion.Settled,
                result => result.ToString(), captureCancellation: () => actor.CaptureCancellation(PerformanceCommand.Expression),
                captureOwnership: () => actor.CapturePersistentCancellation(PerformanceCommand.Expression));

        public Awaitable<SpeechCompletion> SayAsync(AudioClip clip) =>
            run.Execute("Say", () => actor.SayAsync(clip), result => result == SpeechCompletion.Finished,
                result => result.ToString(), captureCancellation: () => actor.CaptureCancellation(PerformanceCommand.Speech));

        public Awaitable<GazeCompletion> LookAtAsync(Transform target) =>
            run.Execute("LookAt", () => actor.LookAtAsync(target), result => result == GazeCompletion.Acquired,
                result => result.ToString(), captureCancellation: () => actor.CaptureCancellation(PerformanceCommand.Gaze),
                captureOwnership: () => actor.CapturePersistentCancellation(PerformanceCommand.Gaze));
        public Awaitable<GazeCompletion> LookAtAsync(Vector3 worldPosition) =>
            run.Execute("LookAt", () => actor.LookAtAsync(worldPosition), result => result == GazeCompletion.Acquired,
                result => result.ToString(), captureCancellation: () => actor.CaptureCancellation(PerformanceCommand.Gaze),
                captureOwnership: () => actor.CapturePersistentCancellation(PerformanceCommand.Gaze));

        public void LookAt(Transform target)
        {
            run.ThrowIfActive();
            actor.LookAt(target);
            run.RegisterPersistentOwnership(() => actor.CapturePersistentCancellation(PerformanceCommand.Gaze));
        }

        public void LookAt(Vector3 worldPosition)
        {
            run.ThrowIfActive();
            actor.LookAt(worldPosition);
            run.RegisterPersistentOwnership(() => actor.CapturePersistentCancellation(PerformanceCommand.Gaze));
        }

        public void ClearGaze()
        {
            run.ThrowIfActive();
            actor.ClearGaze();
        }

        public void Expression(PerformerExpression expression) => Expression(expression, 1f, actor.DefaultExpressionBlendTime);
        public void Expression(PerformerExpression expression, float intensity) => Expression(expression, intensity, actor.DefaultExpressionBlendTime);
        public void Expression(PerformerExpression expression, float intensity, float blendTime)
        {
            run.ThrowIfActive();
            actor.Expression(expression, intensity, blendTime);
            run.RegisterPersistentOwnership(() => actor.CapturePersistentCancellation(PerformanceCommand.Expression));
        }

        public void ClearExpression()
        {
            run.ThrowIfActive();
            actor.ClearExpression();
        }

        public Awaitable<LocomotionCompletion> WalkToAsync(Vector3 worldPosition) =>
            run.Execute("WalkTo", () => actor.WalkToAsync(worldPosition), result => result == LocomotionCompletion.Arrived,
                result => result.ToString(), captureCancellation: () => actor.CaptureCancellation(PerformanceCommand.Movement),
                captureOwnership: () => actor.CapturePersistentCancellation(PerformanceCommand.Movement));
        public Awaitable<LocomotionCompletion> WalkToAsync(Transform target) =>
            run.Execute("WalkTo", () => actor.WalkToAsync(target), result => result == LocomotionCompletion.Arrived,
                result => result.ToString(), captureCancellation: () => actor.CaptureCancellation(PerformanceCommand.Movement),
                captureOwnership: () => actor.CapturePersistentCancellation(PerformanceCommand.Movement));
        public Awaitable<LocomotionCompletion> WalkToAsync(Transform target, bool strictOwnership) =>
            run.Execute("WalkTo", () => actor.WalkToAsync(target, strictOwnership), result => result == LocomotionCompletion.Arrived,
                result => result.ToString(), captureCancellation: () => actor.CaptureCancellation(PerformanceCommand.Movement),
                captureOwnership: () => actor.CapturePersistentCancellation(PerformanceCommand.Movement));
        public Awaitable<LocomotionCompletion> WalkToAsync(Transform target, LocomotionArrivalBehavior arrival) =>
            run.Execute("WalkTo", () => actor.WalkToAsync(target, arrival), result => result == LocomotionCompletion.Arrived,
                result => result.ToString(), captureCancellation: () => actor.CaptureCancellation(PerformanceCommand.Movement),
                captureOwnership: () => actor.CapturePersistentCancellation(PerformanceCommand.Movement));

        public Awaitable<LocomotionCompletion> TurnToAsync(Transform target) =>
            run.Execute("TurnTo", () => actor.TurnToAsync(target), result => result == LocomotionCompletion.Arrived,
                result => result.ToString(), captureCancellation: () => actor.CaptureCancellation(PerformanceCommand.Movement),
                captureOwnership: () => actor.CapturePersistentCancellation(PerformanceCommand.Movement));
        public Awaitable<LocomotionCompletion> TurnToAsync(Vector3 worldPosition) =>
            run.Execute("TurnTo", () => actor.TurnToAsync(worldPosition), result => result == LocomotionCompletion.Arrived,
                result => result.ToString(), captureCancellation: () => actor.CaptureCancellation(PerformanceCommand.Movement),
                captureOwnership: () => actor.CapturePersistentCancellation(PerformanceCommand.Movement));

        public Awaitable<SeatingCompletion> SitAtAsync(PerformerSeat seat) =>
            run.Execute("SitAt", () => actor.SitAtAsync(seat), result => result == SeatingCompletion.Seated,
                result => result.ToString(), captureCancellation: () => actor.CaptureCancellation(PerformanceCommand.Seating));
        public Awaitable<SeatingCompletion> SitAtAsync(PerformerSeat seat, PerformerSeatedStyle style) =>
            run.Execute("SitAt", () => actor.SitAtAsync(seat, style), result => result == SeatingCompletion.Seated,
                result => result.ToString(), captureCancellation: () => actor.CaptureCancellation(PerformanceCommand.Seating));
        public Awaitable<SeatingCompletion> StandUpAsync() =>
            run.Execute("StandUp", actor.StandUpAsync, result => result == SeatingCompletion.Standing,
                result => result.ToString(), captureCancellation: () => actor.CaptureCancellation(PerformanceCommand.Seating));

        public Awaitable<ActionCompletion> PerformAsync(PerformerAction action) =>
            run.Execute("Perform", () => actor.PerformAsync(action), result => result == ActionCompletion.Completed,
                result => result.ToString(), captureCancellation: () => actor.CaptureCancellation(PerformanceCommand.Action));
        public Awaitable<GestureCompletion> GestureAsync(PerformerGesture gesture) =>
            run.Execute("Gesture", () => actor.GestureAsync(gesture), result => result == GestureCompletion.Completed,
                result => result.ToString(), captureCancellation: () => actor.CaptureCancellation(PerformanceCommand.Gesture));

        public Awaitable<VisibilityCompletion> DissolveInAtAsync(Transform target, float durationSeconds, PerformerPose pose = null) =>
            run.Execute("DissolveInAt", () => actor.DissolveInAtAsync(target, durationSeconds, pose), result => result == VisibilityCompletion.Visible,
                result => result.ToString(), captureCancellation: () => actor.CaptureCancellation(PerformanceCommand.Visibility));
        public Awaitable<VisibilityCompletion> DissolveInAsync(float durationSeconds) =>
            run.Execute("DissolveIn", () => actor.DissolveInAsync(durationSeconds), result => result == VisibilityCompletion.Visible,
                result => result.ToString(), captureCancellation: () => actor.CaptureCancellation(PerformanceCommand.Visibility));
        public Awaitable<VisibilityCompletion> DissolveOutAsync(float durationSeconds) =>
            run.Execute("DissolveOut", () => actor.DissolveOutAsync(durationSeconds), result => result == VisibilityCompletion.Hidden,
                result => result.ToString(), captureCancellation: () => actor.CaptureCancellation(PerformanceCommand.Visibility));
        public Awaitable<DissolveCompletion> DissolveToAsync(Vector3 worldPosition, float durationSeconds, PerformerPose pose = null) =>
            run.Execute("DissolveTo", () => actor.DissolveToAsync(worldPosition, durationSeconds, pose), result => result == DissolveCompletion.Arrived,
                result => result.ToString(), captureCancellation: () => actor.CaptureCancellation(PerformanceCommand.Visibility));
        public Awaitable<DissolveCompletion> DissolveToAsync(Transform target, float durationSeconds, PerformerPose pose = null) =>
            run.Execute("DissolveTo", () => actor.DissolveToAsync(target, durationSeconds, pose), result => result == DissolveCompletion.Arrived,
                result => result.ToString(), captureCancellation: () => actor.CaptureCancellation(PerformanceCommand.Visibility));
        public Awaitable<DissolveCompletion> DissolveToAsync(Vector3 worldPosition, PerformerPose pose) =>
            run.Execute("DissolveTo", () => actor.DissolveToAsync(worldPosition, pose), result => result == DissolveCompletion.Arrived,
                result => result.ToString(), captureCancellation: () => actor.CaptureCancellation(PerformanceCommand.Visibility));
        public Awaitable<DissolveCompletion> DissolveToAsync(Transform target, PerformerPose pose) =>
            run.Execute("DissolveTo", () => actor.DissolveToAsync(target, pose), result => result == DissolveCompletion.Arrived,
                result => result.ToString(), captureCancellation: () => actor.CaptureCancellation(PerformanceCommand.Visibility));

        public Awaitable<TeleportCompletion> TeleportToAsync(Vector3 worldPosition) =>
            run.Execute("TeleportTo", () => actor.TeleportToAsync(worldPosition), result => result == TeleportCompletion.Arrived,
                result => result.ToString(), captureCancellation: () => actor.CaptureCancellation(PerformanceCommand.Teleport));
        public Awaitable<TeleportCompletion> TeleportToAsync(Vector3 worldPosition, PerformerPose pose) =>
            run.Execute("TeleportTo", () => actor.TeleportToAsync(worldPosition, pose), result => result == TeleportCompletion.Arrived,
                result => result.ToString(), captureCancellation: () => actor.CaptureCancellation(PerformanceCommand.Teleport));
        public Awaitable<TeleportCompletion> TeleportToAsync(Transform target) =>
            run.Execute("TeleportTo", () => actor.TeleportToAsync(target), result => result == TeleportCompletion.Arrived,
                result => result.ToString(), captureCancellation: () => actor.CaptureCancellation(PerformanceCommand.Teleport));
        public Awaitable<TeleportCompletion> TeleportToAsync(Transform target, PerformerPose pose) =>
            run.Execute("TeleportTo", () => actor.TeleportToAsync(target, pose), result => result == TeleportCompletion.Arrived,
                result => result.ToString(), captureCancellation: () => actor.CaptureCancellation(PerformanceCommand.Teleport));

        public Awaitable<WardrobeChangeResult> OutfitAsync(WardrobePreset preset, WardrobeTransition transition = default) =>
            run.Execute("Outfit", () => actor.OutfitAsync(preset, transition), WardrobeSucceeded,
                result => result != null ? result.Status.ToString() : "null", WardrobeFailure, () => actor.CaptureCancellation(PerformanceCommand.Wardrobe));
        public Awaitable<WardrobeChangeResult> OutfitAsync(string presetId, WardrobeTransition transition = default) =>
            run.Execute("Outfit", () => actor.OutfitAsync(presetId, transition), WardrobeSucceeded,
                result => result != null ? result.Status.ToString() : "null", WardrobeFailure, () => actor.CaptureCancellation(PerformanceCommand.Wardrobe));
        public Awaitable<WardrobeChangeResult> SetHairAsync(string stableHairId, WardrobeTransition transition = default) =>
            run.Execute("SetHair", () => actor.SetHairAsync(stableHairId, transition), WardrobeSucceeded,
                result => result != null ? result.Status.ToString() : "null", WardrobeFailure, () => actor.CaptureCancellation(PerformanceCommand.Wardrobe));
        public Awaitable<WardrobeLayerChangeResult> TryRemoveLayerAsync(WardrobeTransition transition = default) =>
            run.Execute("TryRemoveLayer", () => actor.TryRemoveLayerAsync(transition), WardrobeSucceeded,
                result => result != null ? result.Status.ToString() : "null", WardrobeFailure, () => actor.CaptureCancellation(PerformanceCommand.Wardrobe));
        public Awaitable<WardrobeLayerChangeResult> TryAddLayerAsync(WardrobeTransition transition = default) =>
            run.Execute("TryAddLayer", () => actor.TryAddLayerAsync(transition), WardrobeSucceeded,
                result => result != null ? result.Status.ToString() : "null", WardrobeFailure, () => actor.CaptureCancellation(PerformanceCommand.Wardrobe));

        public Awaitable<GripCompletion> GripAsync(IGripTarget target) =>
            run.Execute("Grip", () => actor.GripAsync(target), result => result == GripCompletion.Acquired,
                result => result.ToString(), captureCancellation: () => actor.CaptureCancellation(PerformanceCommand.Grip),
                captureOwnership: () => actor.CapturePersistentCancellation(PerformanceCommand.Grip));

        public void Aura(PerformerAura aura) => Aura(aura, actor.transform);

        public void Aura(PerformerAura aura, Transform target)
        {
            run.ThrowIfActive();
            actor.Aura(aura, target);
            run.RegisterPersistentOwnership(() => actor.CaptureAuraCancellation());
        }

        public void ClearAura()
        {
            run.ThrowIfActive();
            actor.ClearAura();
        }

        public void OverrideTuning(PerformanceTuningOverride values)
        {
            run.ThrowIfActive();
            if (values == null) throw new ArgumentNullException(nameof(values));
            run.RegisterPersistentCleanup(values.ApplyTo(actor));
        }

        public void HoldGrip(float position01 = .5f)
        {
            run.ThrowIfActive();
            actor.HoldGrip(position01);
            run.RegisterPersistentOwnership(() => actor.CapturePersistentCancellation(PerformanceCommand.Grip));
        }

        public void FollowGripMotion()
        {
            run.ThrowIfActive();
            actor.FollowGripMotion();
            run.RegisterPersistentOwnership(() => actor.CapturePersistentCancellation(PerformanceCommand.Grip));
        }

        public void ReleaseGrip()
        {
            run.ThrowIfActive();
            actor.ReleaseGrip();
        }

        public Awaitable<GripCompletion> ReleaseGripAsync() =>
            run.Execute("ReleaseGrip", actor.ReleaseGripAsync, result => result == GripCompletion.Released,
                result => result.ToString(), captureCancellation: () => actor.CaptureCancellation(PerformanceCommand.Grip));

        public Awaitable<SpellCompletion> CastAsync(PerformerSpell spell) =>
            run.Execute("Cast", () => actor.CastAsync(spell), result => result == SpellCompletion.Completed,
                result => result.ToString(), captureCancellation: () => actor.CaptureCancellation(PerformanceCommand.Spell));
        public Awaitable<SpellCompletion> CastAsync(PerformerSpell spell, Transform target) =>
            run.Execute("Cast", () => actor.CastAsync(spell, target), result => result == SpellCompletion.Completed,
                result => result.ToString(), captureCancellation: () => actor.CaptureCancellation(PerformanceCommand.Spell));

        private static bool WardrobeSucceeded(WardrobeChangeResult result) => result != null
            && (result.Status == WardrobeChangeStatus.Applied
                || result.Status == WardrobeChangeStatus.AlreadyEquipped
                || result.Status == WardrobeChangeStatus.NoChange);

        private PerformanceCommandException WardrobeFailure(WardrobeChangeResult result) => new PerformanceCommandException(
            actor.name, "wardrobe", result != null ? result.Status.ToString() : "null",
            result != null ? result.Message ?? "The wardrobe command failed." : "The wardrobe command returned no result.",
            result != null ? result.FailureCode : WardrobeFailureCode.MissingAsset);
    }
}
