using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;

namespace DazPose.Motion
{
    /// <summary>Immutable imported source actions and metadata for one single-axis Funscript.</summary>
    public sealed class FunscriptMotionProgram : ScriptableObject
    {
        [Serializable]
        private struct SerializedAction
        {
            public long atMilliseconds;
            public int position;

            public SerializedAction(FunscriptAction action)
            {
                atMilliseconds = action.AtMilliseconds;
                position = action.Position;
            }
        }

        [SerializeField] private SerializedAction[] actions = Array.Empty<SerializedAction>();
        [SerializeField] private string version = string.Empty;
        [SerializeField] private bool inverted;
        [SerializeField] private int range = 100;
        [SerializeField] private double metadataDurationSeconds;
        [SerializeField] private double durationSeconds;
        [SerializeField] private string title = string.Empty;
        [SerializeField] private string description = string.Empty;
        [SerializeField] private string creator = string.Empty;

        [NonSerialized] private FunscriptAction[] _actionCache;
        [NonSerialized] private ReadOnlyCollection<FunscriptAction> _readOnlyActions;

        public IReadOnlyList<FunscriptAction> Actions
        {
            get
            {
                EnsureActionCache();
                return _readOnlyActions;
            }
        }

        public int ActionCount => actions != null ? actions.Length : 0;
        public string Version => version ?? string.Empty;
        public bool Inverted => inverted;
        public int Range => range;
        public double MetadataDurationSeconds => metadataDurationSeconds;
        public double DurationSeconds => durationSeconds;
        public string Title => title ?? string.Empty;
        public string Description => description ?? string.Empty;
        public string Creator => creator ?? string.Empty;

        public FunscriptAction GetAction(int index)
        {
            if (actions == null || index < 0 || index >= actions.Length)
                throw new ArgumentOutOfRangeException(nameof(index));
            SerializedAction action = actions[index];
            return new FunscriptAction(action.atMilliseconds, action.position);
        }

        /// <summary>Returns the raw authored position normalized by Range, before file inversion.</summary>
        public float NormalizePosition(FunscriptAction action)
        {
            ValidateAction(action, range);
            return Mathf.Clamp01(action.Position / (float)range);
        }

        /// <summary>Returns the normalized playback target after applying the file-level inversion.</summary>
        public float InterpretPosition(FunscriptAction action)
        {
            float normalized = NormalizePosition(action);
            return inverted ? 1f - normalized : normalized;
        }

        /// <summary>Gets the authored segment beginning at an action, including zero-duration duplicates.</summary>
        public FunscriptSegment GetSegmentStartingAtAction(int fromActionIndex)
        {
            if (actions == null || fromActionIndex < 0 || fromActionIndex >= actions.Length - 1)
                throw new ArgumentOutOfRangeException(nameof(fromActionIndex));
            FunscriptAction from = GetAction(fromActionIndex);
            FunscriptAction to = GetAction(fromActionIndex + 1);
            return new FunscriptSegment(fromActionIndex, fromActionIndex + 1,
                from, to, InterpretPosition(from), InterpretPosition(to));
        }

        /// <summary>Finds the positive-time authored segment active at a timeline time in O(log n).</summary>
        public bool TryGetSegmentAt(double timeSeconds, out FunscriptSegment segment)
        {
            segment = default;
            if (double.IsNaN(timeSeconds) || double.IsInfinity(timeSeconds) || timeSeconds < 0d
                || timeSeconds >= durationSeconds || ActionCount < 2)
                return false;

            int fromIndex = FindLastActionAtOrBefore(timeSeconds);
            if (fromIndex < 0 || fromIndex >= ActionCount - 1) return false;
            FunscriptAction from = GetAction(fromIndex);
            FunscriptAction to = GetAction(fromIndex + 1);
            if (to.AtMilliseconds <= from.AtMilliseconds) return false;
            segment = new FunscriptSegment(fromIndex, fromIndex + 1,
                from, to, InterpretPosition(from), InterpretPosition(to));
            return true;
        }

        internal int FindLastActionAtOrBefore(double timeSeconds)
        {
            if (ActionCount == 0) return -1;
            int low = 0;
            int high = ActionCount;
            while (low < high)
            {
                int middle = low + ((high - low) / 2);
                if (actions[middle].atMilliseconds / 1000d <= timeSeconds) low = middle + 1;
                else high = middle;
            }
            return low - 1;
        }

        internal void ConfigureImportedData(FunscriptAction[] sourceActions, string sourceVersion,
            bool sourceInverted, int sourceRange, double sourceMetadataDurationSeconds,
            string sourceTitle, string sourceDescription, string sourceCreator)
        {
            if (sourceActions == null || sourceActions.Length == 0)
                throw new ArgumentException("A Funscript must contain at least one action.", nameof(sourceActions));
            if (sourceRange <= 0)
                throw new ArgumentOutOfRangeException(nameof(sourceRange), "Funscript range must be positive.");
            if (double.IsNaN(sourceMetadataDurationSeconds) || double.IsInfinity(sourceMetadataDurationSeconds)
                || sourceMetadataDurationSeconds < 0d)
                throw new ArgumentOutOfRangeException(nameof(sourceMetadataDurationSeconds));

            long previousTime = -1L;
            for (int i = 0; i < sourceActions.Length; i++)
            {
                FunscriptAction action = sourceActions[i];
                ValidateAction(action, sourceRange);
                if (action.AtMilliseconds < previousTime)
                    throw new ArgumentException("Funscript action timestamps must be ordered; duplicates are allowed.", nameof(sourceActions));
                previousTime = action.AtMilliseconds;
            }

            actions = new SerializedAction[sourceActions.Length];
            long lastAtMilliseconds = 0L;
            for (int i = 0; i < sourceActions.Length; i++)
            {
                actions[i] = new SerializedAction(sourceActions[i]);
                lastAtMilliseconds = sourceActions[i].AtMilliseconds;
            }
            version = sourceVersion ?? string.Empty;
            inverted = sourceInverted;
            range = sourceRange;
            metadataDurationSeconds = sourceMetadataDurationSeconds;
            durationSeconds = Math.Max(sourceMetadataDurationSeconds, lastAtMilliseconds / 1000d);
            title = sourceTitle ?? string.Empty;
            description = sourceDescription ?? string.Empty;
            creator = sourceCreator ?? string.Empty;
            _actionCache = null;
            _readOnlyActions = null;
        }

        private static void ValidateAction(FunscriptAction action, int sourceRange)
        {
            if (action.AtMilliseconds < 0L)
                throw new ArgumentOutOfRangeException(nameof(action), "Action timestamps cannot be negative.");
            if (sourceRange <= 0 || action.Position < 0 || action.Position > sourceRange)
                throw new ArgumentOutOfRangeException(nameof(action), "Action position must be within [0, Range].");
        }

        private void EnsureActionCache()
        {
            if (_readOnlyActions != null) return;
            int count = ActionCount;
            _actionCache = new FunscriptAction[count];
            for (int i = 0; i < count; i++)
                _actionCache[i] = new FunscriptAction(actions[i].atMilliseconds, actions[i].position);
            _readOnlyActions = Array.AsReadOnly(_actionCache);
        }
    }
}
