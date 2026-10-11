using System;

namespace DazPose.Performer
{
    /// <summary>Author-facing failure that identifies the actor, verb and original runtime outcome.</summary>
    public sealed class PerformanceCommandException : InvalidOperationException
    {
        public string ActorName { get; }
        public string Verb { get; }
        public string OriginalStatus { get; }
        public WardrobeFailureCode WardrobeFailureCode { get; }
        public string WardrobeReason { get; }

        internal PerformanceCommandException(string actorName, string verb, string status,
            string reason, WardrobeFailureCode wardrobeCode = WardrobeFailureCode.None)
            : base(BuildMessage(actorName, verb, status, reason, wardrobeCode))
        {
            ActorName = actorName ?? string.Empty;
            Verb = verb ?? string.Empty;
            OriginalStatus = status ?? string.Empty;
            WardrobeFailureCode = wardrobeCode;
            WardrobeReason = reason ?? string.Empty;
        }

        private static string BuildMessage(string actor, string verb, string status,
            string reason, WardrobeFailureCode wardrobeCode)
        {
            string suffix = string.IsNullOrWhiteSpace(reason) ? string.Empty : ": " + reason;
            string wardrobe = wardrobeCode == WardrobeFailureCode.None ? string.Empty
                : " (wardrobe " + wardrobeCode + ")";
            return (actor ?? "Performer") + "/" + (verb ?? "command") + " failed with "
                + (status ?? "unknown") + wardrobe + suffix;
        }
    }
}
