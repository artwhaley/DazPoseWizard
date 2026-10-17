using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using DazPose.Performer.HandGrip;
using DazPose.Motion;
using DazPose.Player;
using DazPose.Toys;
using UnityEngine;

namespace DazPose.Performer
{
    /// <summary>Owns one authored performance and the exact operations it accepted.</summary>
    public sealed class PerformanceRun : IDisposable
    {
        private sealed class Operation
        {
            public string Verb;
            public Action Cancel;
            public Action Stop;
            public bool Settled;
            public bool ExplicitStop;
        }

        private static int mainThreadId;
        private readonly PerformanceSceneBindings bindings;
        private readonly SuccubusPerformer actor;
        private readonly PlayerController player;
        private readonly ToyControlService toys;
        private readonly MotionDriver playback;
        private readonly List<Operation> operations = new List<Operation>();
        private readonly List<Action> persistentCleanups = new List<Action>();
        private readonly object gate = new object();
        private bool stopped;
        private bool disposed;
        private PerformanceCommandException failure;

        public ScopedPerformer Performer { get; }
        public ScopedPlayer Player => player != null
            ? scopedPlayer : throw new PerformanceCommandException(actor != null ? actor.name : "scene",
                "Player", "MissingBinding", "This performance has no bound player view.");
        public ScopedToys Toys => scopedToys;
        [Obsolete("Use StartPlayback, StopPlayback and read-only playback state.")]
        public MotionDriver Playback => playback;
        // Internal adapters share the run-owned clock without exposing a mutable author API.
        internal MotionDriver PlaybackDriver => playback;
        public bool IsPlaybackRunning => playback!=null && playback.IsRunning;
        public float PlaybackPosition01 => playback!=null ? playback.CurrentSample.Position01 : 0f;
        public IGripTarget ContactTarget => bindings.ContactTarget;
        public bool HasPlayer => player != null && player.isActiveAndEnabled;
        [Obsolete("Toy availability belongs to Settings; author output is unconditional.")]
        public bool HasToys => toys != null && toys.isActiveAndEnabled;
        public bool IsStopped => stopped;
        public PerformanceCommandException Failure => failure;

        private readonly ScopedPlayer scopedPlayer;
        private readonly ScopedToys scopedToys;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetThreadIdentity() => mainThreadId = 0;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void CaptureMainThread() => mainThreadId = Thread.CurrentThread.ManagedThreadId;

        private PerformanceRun(PerformanceSceneBindings sceneBindings)
        {
            bindings = sceneBindings;
            actor = sceneBindings.Performer;
            player = sceneBindings.Player;
            toys = DazPose.Game.GameApplication.Current != null ? DazPose.Game.GameApplication.Current.Toys : sceneBindings.Toys;
            playback = sceneBindings.Playback;
            Performer = new ScopedPerformer(this, actor);
            scopedPlayer = player != null ? new ScopedPlayer(this, player) : null;
            scopedToys = new ScopedToys(this, toys);
        }

        public static PerformanceRun Begin(PerformanceSceneBindings sceneBindings)
        {
            if (sceneBindings == null) throw new PerformanceCommandException("scene", "Begin", "MissingBindings", "Assign the PerformanceSceneBindings component.");
            if (!Application.isPlaying) throw new PerformanceCommandException("scene", "Begin", "NotPlaying", "PerformanceRun.Begin requires Play Mode.");
            if (mainThreadId != 0 && Thread.CurrentThread.ManagedThreadId != mainThreadId)
                throw new PerformanceCommandException("scene", "Begin", "WrongThread", "PerformanceRun.Begin must run on Unity's main thread.");
            if (sceneBindings.Performer == null)
                throw new PerformanceCommandException("scene", "Begin", "MissingActor", "Assign the bound actor before beginning the performance.");

            if(!sceneBindings.Performer.isActiveAndEnabled) throw new PerformanceCommandException(sceneBindings.Performer.name,"Begin","InactiveActor","Enable the bound actor before starting.");
            return new PerformanceRun(sceneBindings);
        }

        public Awaitable Delay(float seconds)
        {
            if (float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds < 0f)
                throw new ArgumentOutOfRangeException(nameof(seconds), "Delay seconds must be finite and nonnegative.");
            return DelayCore(seconds);
        }

        public void StartPlayback(FunscriptMotionProgram program, bool loop = false)
        {
            ThrowIfStopped();
            if (program == null) throw new ArgumentNullException(nameof(program));
            if (playback == null)
                throw new PerformanceCommandException(actor.name, "StartPlayback", "MissingBinding",
                    "Assign the explicit scene playback MotionDriver before starting playback.");
            if (playback.IsRunning)
                throw new PerformanceCommandException(actor.name, "StartPlayback", "AlreadyRunning",
                    "The bound playback driver is already running; use Follow to consume an external clock.");

            playback.FunscriptProgram = program;
            playback.Loop = loop;
            playback.SourceMode = MotionSourceMode.Funscript;
            playback.RestartMotion();
            long generation = playback.CommandGeneration;
            RegisterPersistentCleanup(() =>
            {
                if (playback != null && playback.CommandGeneration == generation) playback.StopMotion();
            });
        }

        public void StopPlayback()
        {
            ThrowIfStopped();
            if (playback == null)
                throw new PerformanceCommandException(actor.name, "StopPlayback", "MissingBinding",
                    "Assign the explicit scene playback MotionDriver before stopping playback.");
            playback.StopMotion();
        }

        private async Awaitable DelayCore(float seconds)
        {
            float elapsed = 0f;
            while (elapsed < seconds)
            {
                ThrowIfStopped();
                await Awaitable.NextFrameAsync();
                ThrowIfStopped();
                elapsed += Mathf.Max(0f, Time.deltaTime);
            }
            ThrowIfStopped();
        }

        public void Stop() => Close(stopOutputs: true);

        private void Close(bool stopOutputs)
        {
            Action[] cleanup;
            lock (gate)
            {
                if (stopped) return;
                stopped = true;
                if (stopOutputs) scopedToys.StopSessionOutput();
                foreach (Operation operation in operations.ToArray())
                {
                    operation.ExplicitStop = true;
                    try { operation.Cancel?.Invoke(); }
                    catch (Exception exception) { Debug.LogWarning(actor.name + "/" + operation.Verb + " cancellation: " + exception.Message, actor); }
                    operation.Stop?.Invoke();
                    operation.Settled = true;
                }
                cleanup = persistentCleanups.AsEnumerable().Reverse().ToArray();
                persistentCleanups.Clear();
            }
            foreach (Action action in cleanup)
            {
                try { action?.Invoke(); }
                catch (Exception exception) { Debug.LogWarning(actor.name + "/persistent cleanup: " + exception.Message, actor); }
            }
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            Close(stopOutputs: false);
        }

        internal Awaitable<T> Execute<T>(string verb, Func<Awaitable<T>> start,
            Func<T, bool> success, Func<T, string> status = null,
            Func<T, PerformanceCommandException> failureFactory = null,
            Func<Action> captureCancellation = null,
            Func<Action> captureOwnership = null)
        {
            ThrowIfStopped();
            var completion = new AwaitableCompletionSource<T>();
            var operation = new Operation { Verb = verb ?? "command" };
            lock (gate) operations.Add(operation);
            try
            {
                Awaitable<T> raw = start();
                // Capture immediately after the owner's command has accepted the request.
                operation.Cancel = captureCancellation?.Invoke();
                operation.Stop = () => completion.TrySetException(new OperationCanceledException(
                    actor.name + "/" + operation.Verb + " stopped."));
                Observe(raw, completion, operation, success, status, failureFactory, captureOwnership);
            }
            catch (Exception exception)
            {
                var commandFailure = exception as PerformanceCommandException
                    ?? new PerformanceCommandException(actor.name, operation.Verb, "StartException", exception.Message);
                RecordFailure(commandFailure);
                completion.TrySetException(commandFailure);
                StopAfterFailure(operation);
            }
            return completion.Awaitable;
        }

        private async void Observe<T>(Awaitable<T> raw, AwaitableCompletionSource<T> completion,
            Operation operation, Func<T, bool> success, Func<T, string> status,
            Func<T, PerformanceCommandException> failureFactory, Func<Action> captureOwnership)
        {
            try
            {
                T result = await raw;
                if (operation.ExplicitStop || stopped)
                {
                    completion.TrySetException(new OperationCanceledException(actor.name + "/" + operation.Verb + " stopped."));
                    operation.Settled = true;
                    return;
                }
                if (success != null && success(result))
                {
                    operation.Cancel = captureOwnership?.Invoke() ?? operation.Cancel;
                    completion.TrySetResult(result);
                }
                else
                {
                    PerformanceCommandException commandFailure = failureFactory != null
                        ? failureFactory(result)
                        : new PerformanceCommandException(actor.name, operation.Verb,
                            status != null ? status(result) : (result == null ? "null" : result.ToString()),
                            "The underlying command did not reach its required success state.");
                    RecordFailure(commandFailure);
                    completion.TrySetException(commandFailure);
                    StopAfterFailure(operation);
                    operation.Settled = true;
                    return;
                }
                operation.Settled = true;
            }
            catch (Exception exception)
            {
                if (operation.ExplicitStop || stopped)
                    completion.TrySetException(new OperationCanceledException(actor.name + "/" + operation.Verb + " stopped."));
                else
                {
                    PerformanceCommandException commandFailure = exception as PerformanceCommandException
                        ?? new PerformanceCommandException(actor.name, operation.Verb, "Exception", exception.Message);
                    RecordFailure(commandFailure);
                    completion.TrySetException(commandFailure);
                    StopAfterFailure(operation);
                }
                operation.Settled = true;
            }
        }

        internal void RegisterPersistentCleanup(Action cleanup)
        {
            if (cleanup == null) return;
            bool runNow;
            lock (gate)
            {
                runNow = stopped;
                if (!runNow) persistentCleanups.Add(cleanup);
            }
            if (runNow)
            {
                try { cleanup(); }
                catch (Exception exception) { Debug.LogWarning(actor.name + "/persistent cleanup: " + exception.Message, actor); }
            }
        }

        internal void RegisterPersistentOwnership(Func<Action> capture)
        {
            if (capture == null) return;
            Action cleanup = null;
            try { cleanup = capture(); }
            catch (Exception exception)
            {
                Debug.LogWarning(actor.name + "/persistent ownership capture: " + exception.Message, actor);
            }
            RegisterPersistentCleanup(cleanup);
        }

        private void StopAfterFailure(Operation failed)
        {
            Action[] cleanup;
            lock (gate)
            {
                if (stopped) return;
                stopped = true;
                scopedToys.StopSessionOutput();
                foreach (Operation operation in operations.ToArray())
                {
                    operation.ExplicitStop = true;
                    try { operation.Cancel?.Invoke(); } catch (Exception exception) { Debug.LogWarning(actor.name + "/" + operation.Verb + " cancellation: " + exception.Message, actor); }
                    operation.Stop?.Invoke();
                    operation.Settled = true;
                }
                cleanup = persistentCleanups.AsEnumerable().Reverse().ToArray();
                persistentCleanups.Clear();
            }
            foreach (Action action in cleanup)
            {
                try { action?.Invoke(); }
                catch (Exception exception) { Debug.LogWarning(actor.name + "/persistent cleanup: " + exception.Message, actor); }
            }
        }

        private void RecordFailure(PerformanceCommandException commandFailure)
        {
            if (failure == null) failure = commandFailure;
        }

        private void ThrowIfStopped()
        {
            if (stopped) throw new OperationCanceledException(actor.name + "/PerformanceRun stopped.");
            if (actor == null || !actor.isActiveAndEnabled)
                throw new PerformanceCommandException(actor != null ? actor.name : "actor", "Run", "ActorUnavailable", "The bound actor is unavailable.");
        }

        internal SuccubusPerformer Actor => actor;
        internal PlayerController BoundPlayer => player;
        internal ToyControlService BoundToys => toys;
        internal void ThrowIfActive() => ThrowIfStopped();
    }
}
