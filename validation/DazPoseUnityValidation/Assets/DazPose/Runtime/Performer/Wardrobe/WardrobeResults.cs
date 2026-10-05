using System;

namespace DazPose.Performer
{
    public enum WardrobeChangeStatus
    {
        Applied, AlreadyEquipped, NoChange, Superseded, Cancelled, PerformerDisabled, Failed
    }

    public enum WardrobeFailureCode
    {
        None, UnknownPreset, AmbiguousAlias, NotReleased, IncompatibleCharacter,
        ConflictingItems, FitNotAvailable, MissingAsset, InvalidBindings,
        EffectRebindFailed, QueueFull
    }

    public enum WardrobeNoChangeReason { None, AlreadyNaked, FullyDressed, NoOutfitSelected }
    public enum WardrobeTransitionKind { Cut, Dissolve }

    [Serializable]
    public struct WardrobeTransition
    {
        public WardrobeTransitionKind kind;
        public float outSeconds;
        public float inSeconds;
        public static WardrobeTransition Cut => default;
        public static WardrobeTransition Dissolve(float outSeconds, float inSeconds) => new WardrobeTransition
        { kind = WardrobeTransitionKind.Dissolve, outSeconds = Math.Max(0, outSeconds), inSeconds = Math.Max(0, inSeconds) };
    }

    public class WardrobeChangeResult
    {
        public WardrobeChangeStatus Status { get; }
        public WardrobeFailureCode FailureCode { get; }
        public WardrobeNoChangeReason NoChangeReason { get; }
        public WardrobeState PreviousState { get; }
        public WardrobeState CurrentState { get; }
        public string Message { get; }
        public bool Succeeded => Status != WardrobeChangeStatus.Failed;

        public WardrobeChangeResult(WardrobeChangeStatus status, WardrobeState previousState,
            WardrobeState currentState, WardrobeFailureCode failureCode = WardrobeFailureCode.None,
            WardrobeNoChangeReason noChangeReason = WardrobeNoChangeReason.None, string message = null)
        {
            Status = status; PreviousState = previousState; CurrentState = currentState;
            FailureCode = failureCode; NoChangeReason = noChangeReason; Message = message;
        }
    }

    public sealed class WardrobeLayerChangeResult : WardrobeChangeResult
    {
        public WardrobeLayerChangeResult(WardrobeChangeStatus status, WardrobeState previousState,
            WardrobeState currentState, WardrobeFailureCode failureCode = WardrobeFailureCode.None,
            WardrobeNoChangeReason noChangeReason = WardrobeNoChangeReason.None, string message = null)
            : base(status, previousState, currentState, failureCode, noChangeReason, message) { }
    }
}
