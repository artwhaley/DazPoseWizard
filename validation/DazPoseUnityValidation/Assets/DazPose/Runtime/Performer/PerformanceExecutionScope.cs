using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

namespace DazPose.Performer
{
    public enum PerformanceCommand { Pose, Expression, Speech, Movement, Seating, Action, Gesture, Visibility, Teleport, Grip, Wardrobe, Gaze, Spell, Aura }
    public enum LocomotionArrivalBehavior { RestorePose, Hold }
    public sealed class PerformanceBusyException : InvalidOperationException
    { public PerformanceBusyException(string message) : base(message) { } }

    /// <summary>A single run's waits and cancellation handles. Consumes each Awaitable once.</summary>
    [Obsolete("Legacy controller adapter. Production scripts use PerformanceRun through PerformanceHost.")]
    internal sealed class PerformanceExecutionScope : IDisposable
    {
        public sealed class Operation
        {
            public string Command { get; internal set; }
            public bool Done { get; internal set; }
            public string Result { get; internal set; }
            internal Action Cancel;
            internal bool Persistent;
        }
        readonly List<Operation> operations = new List<Operation>();
        readonly Func<bool> available;
        readonly string context;
        bool cancelled;
        string failure;
        public bool IsCancelled => cancelled;
        public string Failure => failure;
        public int PendingCount => operations.Count(x => !x.Done);
        public PerformanceExecutionScope(string context, Func<bool> available = null)
        { this.context = context ?? "Performance"; this.available = available; }
        public Operation Observe<T>(Awaitable<T> awaitable, T expected, string command, Action cancel = null, bool persistent = false)
        {
            Check();
            var operation = new Operation { Command = command, Cancel = cancel, Persistent = persistent };
            operations.Add(operation);
            ObserveOnce(awaitable, expected, operation);
            return operation;
        }
        async void ObserveOnce<T>(Awaitable<T> awaitable, T expected, Operation operation)
        {
            string error = null;
            try
            {
                var result = await awaitable;
                operation.Result = result == null ? "null" : result.ToString();
                if (!EqualityComparer<T>.Default.Equals(result, expected)) error = operation.Command + " returned " + operation.Result + "; expected " + expected;
            }
            catch (Exception ex) { error = operation.Command + ": " + ex.Message; }
            operation.Done = true;
            if (!cancelled && error != null) { failure = context + ": " + error; Cancel(); }
        }
        public void Own(string command, Action release)
        { Check(); operations.Add(new Operation { Command = command, Done = true, Persistent = true, Cancel = release }); }
        public Operation ObserveTask<T>(Task<T> task, Func<T, bool> success, string command, Action cancel = null)
        {
            Check();
            var operation = new Operation { Command = command, Cancel = cancel };
            operations.Add(operation); ObserveTaskOnce(task, success, operation); return operation;
        }
        async void ObserveTaskOnce<T>(Task<T> task, Func<T, bool> success, Operation operation)
        {
            string error = null;
            try { var result = await task; if (!success(result)) error = operation.Command + ": backend result failed"; }
            catch (Exception ex) { error = operation.Command + ": " + ex.Message; }
            await Awaitable.MainThreadAsync();
            operation.Done = true;
            if (!cancelled && error != null) { failure = context + ": " + error; Cancel(); }
        }
        public Operation Observe<T>(SuccubusPerformer actor, PerformanceCommand channel, Func<Awaitable<T>> start, T expected, string command, bool persistent = false)
        {
            Check();
            var awaitable = start();
            return Observe(awaitable, expected, actor.name + "/" + command, actor.CaptureCancellation(channel), persistent);
        }
        public async Awaitable Wait(params Operation[] required)
        {
            do
            {
                Check();
                if (required.All(x => x.Done)) return;
                await Awaitable.NextFrameAsync();
            } while (true);
        }
        public async Awaitable Delay(float seconds)
        {
            if (float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds < 0f) throw new ArgumentOutOfRangeException(nameof(seconds));
            float elapsed = 0f;
            while (elapsed < seconds) { Check(); await Awaitable.NextFrameAsync(); Check(); elapsed += Time.deltaTime; }
            Check();
        }
        public void Check()
        {
            if (failure != null) throw new InvalidOperationException(failure);
            if (cancelled) throw new OperationCanceledException(context + " stopped.");
            if (available != null && !available()) { failure = context + ": bound runtime became unavailable."; Cancel(); throw new InvalidOperationException(failure); }
        }
        public void Cancel()
        {
            if (cancelled) return;
            cancelled = true;
            foreach (var operation in operations.ToArray())
                if (!operation.Done || operation.Persistent)
                    try { operation.Cancel?.Invoke(); }
                    catch (Exception ex) { Debug.LogWarning(context + "/" + operation.Command + " cleanup: " + ex.Message); }
        }
        public void Dispose() => Cancel();
    }
}
