// This historical regression runner intentionally exercises the obsolete compatibility adapter.
#pragma warning disable CS0618
using System;
using System.Collections.Generic;
using System.IO;
using DazPose.FirstPerformanceVoid;
using DazPose.Performer;
using DazPose.Performer.HandGrip;
using DazPose.Motion;
using DazPose.Toys;
using UnityEngine;

namespace DazPose.Validation
{
    /// <summary>Scene-local regression runner for Day 3 command ownership and cancellation.</summary>
    public sealed class Day3RegressionRunner : MonoBehaviour
    {
        [SerializeField] private SuccubusPerformer actor;
        [SerializeField] private SuccubusPerformer other;
        [SerializeField] private FirstContactPerformance firstContact;
        [SerializeField] private Transform moveMark;
        [SerializeField] private AudioClip speech;
        [SerializeField] private GripContactRod contactRod;
        [SerializeField] private PerformerPose cancellationPose;
        [SerializeField] private PerformerExpression cancellationExpression;
        [SerializeField] private PerformerAction cancellationAction;
        [SerializeField] private PerformerGesture cancellationGesture;
        [SerializeField] private PerformerSeat cancellationSeat;
        [SerializeField] private PerformerSpell cancellationSpell;
        [SerializeField] private WardrobePreset firstOutfit;
        [SerializeField] private WardrobePreset secondOutfit;
        [SerializeField] private bool autoRun = true;
        [SerializeField] private FunscriptMotionProgram motionProgram;

        [Serializable]
        private sealed class Assertion
        {
            public string name;
            public bool passed;
            public string detail;
        }

        [Serializable]
        private sealed class Report
        {
            public string suite = "day3-command-cancellation";
            public string status;
            public string exception;
            public List<Assertion> assertions = new List<Assertion>();
        }

        private readonly Report report = new Report();
        private bool running;
        private string status = "Ready";
        private float startedRealtime;
        private bool finished;

        public string Status => status;

        private void Start()
        {
            startedRealtime = Time.realtimeSinceStartup;
            if (autoRun) RunSuite();
            Watchdog();
        }

        private async void Watchdog()
        {
            while (!finished && Time.realtimeSinceStartup - startedRealtime < 150f)
                await Awaitable.NextFrameAsync();
            if (!finished) Finish(false, "Day 3 regression runner exceeded its 150 second real-time watchdog.");
        }

        private async void RunSuite()
        {
            if (running || finished) return;
            running = true;
            startedRealtime = Time.realtimeSinceStartup;
            status = "Running Day 3 command regressions";
            try
            {
                Require(actor != null && actor.IsRuntimeReady, "Actor is assigned and runtime-ready.");
                Require(speech != null, "A speech clip is assigned.");
                Require(cancellationPose != null && cancellationExpression != null && cancellationAction != null
                    && cancellationGesture != null && cancellationSeat != null,
                    "Ready cancellation pose, expression, action, gesture and seat references are assigned.");
                await FiniteCommandCancellation();
                await SpeechOwnership();
                await MovementCancellation();
                await ScopeCancellationSettlesCancelledWait();
                await VisibilityCancellationAtPause();
                await AppearanceAtLocationAndTeleport();
                await GripCancellation();
                await WardrobeDissolveCancellation();
                MaskContractsAfterWardrobe();
                await DestroyCloneDuringWait();
                using (var toys = new PerformanceExecutionScope("Day 3 mock toy transport"))
                {
                    Require(motionProgram != null, "Imported motion program is assigned for mock toy checks.");
                    string[] failures = null;
                    var operation = toys.ObserveTask(ToyStackAcceptanceSelfTests.RunAsync(motionProgram),
                        result => { failures = result; if (result.Length != 0) throw new InvalidOperationException(string.Join("; ", result)); return true; }, "Toy runtime suite");
                    await toys.Wait(operation);
                    Record("Mock toy transport, playback ownership and pause contracts", failures.Length == 0, string.Join("; ", failures));
                }
                await FirstContactStopReplay();
                Record("Original actor remains available", actor != null && actor.IsRuntimeReady,
                    "The authored actor remains enabled after clone destruction.");
                Finish(true, null);
            }
            catch (Exception exception)
            {
                Finish(false, exception.ToString());
            }
        }

        private async Awaitable FiniteCommandCancellation()
        {
            status = "Finite command cancellation";
            await CancelFinite(actor, PerformanceCommand.Pose,
                () => actor.PoseAsync(cancellationPose, PoseTransition.Smooth(2f)),
                PoseCompletion.Cancelled, "pose transition");
            await CancelFinite(actor, PerformanceCommand.Expression,
                () => actor.ExpressionAsync(cancellationExpression, 1f, 2f),
                ExpressionCompletion.Cancelled, "expression transition");
            await CancelFinite(actor, PerformanceCommand.Action,
                () => actor.PerformAsync(cancellationAction),
                ActionCompletion.Cancelled, "full-body action");
            await CancelFinite(actor, PerformanceCommand.Gesture,
                () => actor.GestureAsync(cancellationGesture),
                GestureCompletion.Cancelled, "gesture");
            await CancelFinite(actor, PerformanceCommand.Seating,
                () => actor.SitAtAsync(cancellationSeat),
                SeatingCompletion.Cancelled, "seating transition");

            if (cancellationSpell != null && cancellationSpell.IsReady(out _)
                && actor.MagicAvailable)
            {
                await CancelFinite(actor, PerformanceCommand.Spell,
                    () => actor.CastAsync(cancellationSpell),
                    SpellCompletion.Cancelled, "spell");
            }
        }

        private async Awaitable CancelFinite<T>(SuccubusPerformer performer,
            PerformanceCommand channel, Func<Awaitable<T>> start, T cancelledResult,
            string name)
        {
            using var scope = new PerformanceExecutionScope("Day 3 " + name,
                () => performer != null && performer.IsRuntimeReady);
            var operation = scope.Observe(performer, channel, start, cancelledResult, name);
            Action capturedCancel = performer.CaptureCancellation(channel);
            await RealtimeFrames(2);
            Require(capturedCancel != null, name + " has no cancellation handle.");
            capturedCancel();
            await WaitFor(scope, operation);
            bool ownerIdle = channel switch
            {
                PerformanceCommand.Pose => !performer.IsTransitioning,
                PerformanceCommand.Expression => !performer.IsExpressionTransitioning,
                PerformanceCommand.Action => !performer.IsPerforming,
                PerformanceCommand.Gesture => !performer.IsGesturing,
                PerformanceCommand.Seating => performer.SeatingState == PerformerSeatingState.Standing
                    && !performer.IsLocomoting,
                PerformanceCommand.Spell => performer.ActiveSpellCount == 0,
                _ => false
            };
            Record("Cancelling " + name + " settles and releases its owner",
                operation.Result == cancelledResult.ToString() && ownerIdle,
                "Completion=" + operation.Result + "; owner idle=" + ownerIdle);
        }

        private async Awaitable SpeechOwnership()
        {
            status = "Speech ownership";
            var first = actor.SayAsync(speech);
            actor.LookAt(moveMark);
            Action cancelGaze = actor.CaptureCancellation(PerformanceCommand.Gaze);
            Action cancelFirst = actor.CaptureCancellation(PerformanceCommand.Speech);
            using (var scope = new PerformanceExecutionScope("Day 3 speech ownership", () => actor != null && actor.IsRuntimeReady))
            {
                var firstOp = scope.Observe(first, SpeechCompletion.Cancelled, "owned speech request", cancelFirst);
                var queuedOp = scope.Observe(actor.SayAsync(speech), SpeechCompletion.Finished, "queued speech request");
                await RealtimeFrames(2);
                Record("Speech and gaze coexist", actor.IsSpeaking, "Gaze acquisition did not stop queued speech.");
                cancelFirst();
                await WaitFor(scope, firstOp, queuedOp);
                Record("Cancelling one speech request preserves the next queued clip",
                    firstOp.Done && firstOp.Result == SpeechCompletion.Cancelled.ToString()
                    && queuedOp.Done && queuedOp.Result == SpeechCompletion.Finished.ToString(),
                    "First request=" + firstOp.Result + "; queued request=" + queuedOp.Result);
                cancelGaze();
            }

            var paused = actor.SayAsync(speech);
            var pausedOp = new PerformanceExecutionScope.Operation();
            using (var scope = new PerformanceExecutionScope("Day 3 paused speech", () => actor != null && actor.IsRuntimeReady))
            {
                pausedOp = scope.Observe(paused, SpeechCompletion.Finished, "paused speech");
                await RealtimeFrames(2);
                AudioSource source = actor.SpeechAudioSource;
                float sourceTime = source != null ? source.time : -1f;
                float previousScale = Time.timeScale;
                Time.timeScale = 0f;
                await RealtimeFrames(4);
                float pausedTime = source != null ? source.time : -2f;
                bool held = source != null && !source.isPlaying && Mathf.Abs(pausedTime - sourceTime) <= .08f;
                Time.timeScale = previousScale;
                await WaitFor(scope, pausedOp);
                Record("Speech pauses and resumes with gameplay time", held
                    && pausedOp.Result == SpeechCompletion.Finished.ToString(),
                    "Audio time before=" + sourceTime.ToString("F2") + ", during pause=" + pausedTime.ToString("F2")
                    + ", completion=" + pausedOp.Result);
            }
        }

        private async Awaitable MovementCancellation()
        {
            status = "Movement cancellation";
            Require(moveMark != null, "A movement mark is assigned.");
            Require(Vector3.Distance(actor.transform.position, moveMark.position) > 0.5f,
                "The movement mark is far enough from the actor to accept a cancellable move.");
            if (actor.IsLocomoting) actor.CancelMovement();
            using var scope = new PerformanceExecutionScope("Day 3 movement cancellation", () => actor != null && actor.IsRuntimeReady);
            var first = scope.Observe(actor, PerformanceCommand.Movement,
                () => actor.WalkToAsync(moveMark), LocomotionCompletion.Cancelled, "first movement");
            Action oldCancel = actor.CaptureCancellation(PerformanceCommand.Movement);
            await RealtimeFrames(2);
            oldCancel();
            await WaitFor(scope, first);
            var replacement = scope.Observe(actor, PerformanceCommand.Movement,
                () => actor.WalkToAsync(moveMark), LocomotionCompletion.Cancelled, "replacement movement");
            Action replacementCancel = actor.CaptureCancellation(PerformanceCommand.Movement);
            oldCancel();
            bool replacementSurvivedOldCancel = actor.IsLocomoting;
            await RealtimeFrames(2);
            replacementCancel();
            await WaitFor(scope, replacement);
            Record("A stale movement cancellation cannot stop replacement movement",
                first.Result == LocomotionCompletion.Cancelled.ToString() && replacementSurvivedOldCancel
                && replacement.Result == LocomotionCompletion.Cancelled.ToString(),
                "Old=" + first.Result + "; replacement active after old handle=" + replacementSurvivedOldCancel
                + "; replacement=" + replacement.Result);
        }

        private async Awaitable VisibilityCancellationAtPause()
        {
            status = "Visibility cancellation while paused";
            if (!actor.DissolveAvailable || actor.VisibilityState != PerformerVisibilityState.Visible)
            {
                throw new InvalidOperationException("Required dissolve cancellation fixture is unavailable or not visible.");
            }
            using var scope = new PerformanceExecutionScope("Day 3 dissolve cancellation", () => actor != null && actor.IsRuntimeReady);
            var operation = scope.Observe(actor, PerformanceCommand.Visibility,
                () => actor.DissolveOutAsync(2f), VisibilityCompletion.Cancelled, "dissolve out");
            Action cancel = actor.CaptureCancellation(PerformanceCommand.Visibility);
            float previousScale = Time.timeScale;
            Time.timeScale = 0f;
            await RealtimeFrames(3);
            cancel();
            Time.timeScale = previousScale;
            await WaitFor(scope, operation);
            Record("Cancelling dissolve while paused restores stable visibility and settles", 
                operation.Result == VisibilityCompletion.Cancelled.ToString()
                && actor.VisibilityState == PerformerVisibilityState.Visible && !actor.IsDissolving,
                "Completion=" + operation.Result + "; visibility=" + actor.VisibilityState
                + "; active=" + actor.IsDissolving);
        }

        private async Awaitable AppearanceAtLocationAndTeleport()
        {
            status = "Appearance at location and teleport cancellation";
            Vector3 position = actor.transform.position;
            Quaternion rotation = actor.transform.rotation;
            await actor.DissolveOutAsync(.1f);
            using (var scope = new PerformanceExecutionScope("Day 3 appear at location"))
            {
                var reveal = scope.Observe(actor, PerformanceCommand.Visibility,
                    () => actor.DissolveInAtAsync(moveMark, .1f, actor.InitialPose), VisibilityCompletion.Visible, "placed reveal");
                await scope.Wait(reveal);
                Record("Appearance places the hidden actor before revealing",
                    actor.VisibilityState == PerformerVisibilityState.Visible && Vector3.Distance(actor.transform.position, moveMark.position) < .001f,
                    "Visible at the requested mark.");
            }
            actor.ResetPerformance(position, rotation);
            using (var scope = new PerformanceExecutionScope("Day 3 teleport cancellation"))
            {
                var teleport = scope.Observe(actor, PerformanceCommand.Teleport,
                    () => actor.TeleportToAsync(moveMark), TeleportCompletion.Cancelled, "teleport");
                actor.CaptureCancellation(PerformanceCommand.Teleport)();
                await scope.Wait(teleport);
                Record("Teleport cancellation restores stable visibility", !actor.IsTeleporting && actor.VisibilityState == PerformerVisibilityState.Visible,
                    "Completion=" + teleport.Result);
            }
            actor.ResetPerformance(position, rotation);
        }

        private async Awaitable ScopeCancellationSettlesCancelledWait()
        {
            status = "Scope cancellation";
            Require(moveMark != null, "A movement mark is assigned for scope cancellation.");
            Require(Vector3.Distance(actor.transform.position, moveMark.position) > 0.5f,
                "The movement mark is far enough from the actor for scope cancellation.");
            var scope = new PerformanceExecutionScope("Day 3 scope cancellation", () => actor != null && actor.IsRuntimeReady);
            var operation = scope.Observe(actor, PerformanceCommand.Movement,
                () => actor.WalkToAsync(moveMark), LocomotionCompletion.Cancelled, "scope-owned movement");
            await RealtimeFrames(2);
            scope.Cancel();
            float deadline = Time.realtimeSinceStartup + 2f;
            while (!operation.Done && Time.realtimeSinceStartup < deadline)
                await Awaitable.NextFrameAsync();
            Record("Scope cancellation consumes its owned Cancelled result without reporting failure",
                scope.IsCancelled && scope.Failure == null && operation.Done
                && operation.Result == LocomotionCompletion.Cancelled.ToString(),
                "Cancelled=" + scope.IsCancelled + "; failure=" + (scope.Failure ?? "none")
                + "; operation=" + operation.Result);
            scope.Dispose();
        }

        private async Awaitable GripCancellation()
        {
            status = "Grip cancellation";
            if (contactRod == null)
            {
                throw new InvalidOperationException("Required grip cancellation fixture is not assigned.");
            }
            using var scope = new PerformanceExecutionScope("Day 3 grip cancellation", () => actor != null && actor.IsRuntimeReady);
            var operation = scope.Observe(actor, PerformanceCommand.Grip,
                () => actor.GripAsync(contactRod), GripCompletion.Released, "grip acquisition");
            Action cancel = actor.CaptureCancellation(PerformanceCommand.Grip);
            bool strictMovementWasBusy = false;
            try { _ = actor.WalkToAsync(moveMark, true); }
            catch (PerformanceBusyException) { strictMovementWasBusy = true; }
            Record("Strict movement reports Busy while contact owns the arm", strictMovementWasBusy,
                "Grip state at strict movement request=" + actor.GripState);
            cancel();
            await WaitFor(scope, operation);
            Record("Cancelling pending grip settles Released and releases the arm", 
                operation.Result == GripCompletion.Released.ToString() && !actor.IsGripping,
                "Completion=" + operation.Result + "; gripping=" + actor.IsGripping);
        }

        private async Awaitable DestroyCloneDuringWait()
        {
            status = "Destroyed binding during wait";
            SuccubusPerformer clone = Instantiate(other != null ? other : actor);
            clone.name = (other != null ? other.name : actor.name) + " Day3 Scoped Clone";
            await Awaitable.NextFrameAsync();
            if (clone == null || !clone.IsRuntimeReady)
            {
                if (clone != null) Destroy(clone.gameObject);
                Record("Destroying a cloned actor during a scoped wait is isolated", false,
                    "Cloned performer did not become runtime-ready.");
                return;
            }

            using var scope = new PerformanceExecutionScope("Day 3 destroyed clone", () => clone != null && clone.IsRuntimeReady);
            var pending = scope.Observe(clone.SayAsync(speech), SpeechCompletion.Finished, "clone speech",
                clone.CaptureCancellation(PerformanceCommand.Speech));
            await RealtimeFrames(2);
            Destroy(clone.gameObject);
            await RealtimeFrames(2);
            bool failedBoundWait = scope.Failure != null || pending.Done && pending.Result != SpeechCompletion.Finished.ToString();
            Record("Destroying a cloned actor during its wait fails that binding without touching the original",
                failedBoundWait && actor != null && actor.IsRuntimeReady,
                "Scope failure=" + (scope.Failure ?? "none") + "; clone result=" + pending.Result
                + "; original ready=" + (actor != null && actor.IsRuntimeReady));
        }

        private async Awaitable<WardrobeChangeStatus> OutfitStatus(PerformerWardrobe wardrobe,
            WardrobePreset preset, WardrobeTransition transition)
        {
            WardrobeChangeResult result = await wardrobe.OutfitAsync(preset, transition);
            return result.Status;
        }

        private async Awaitable WardrobeDissolveCancellation()
        {
            status = "Wardrobe dissolve cancellation";
            Require(firstOutfit != null && secondOutfit != null,
                "First and second wardrobe presets are assigned.");
            PerformerWardrobe wardrobe = actor.GetComponent<PerformerWardrobe>();
            Require(wardrobe != null, "The actor has a wardrobe runtime.");

            WardrobeChangeResult initial = await wardrobe.OutfitAsync(firstOutfit, WardrobeTransition.Cut);
            Require(initial.Status == WardrobeChangeStatus.Applied
                || initial.Status == WardrobeChangeStatus.AlreadyEquipped
                || initial.Status == WardrobeChangeStatus.NoChange,
                "The first outfit can be equipped before cancellation; result=" + initial.Status + ".");
            Require(actor.VisibilityState == PerformerVisibilityState.Visible,
                "The performer is stably visible before wardrobe cancellation.");

            using var scope = new PerformanceExecutionScope("Day 3 wardrobe dissolve cancellation",
                () => actor != null && actor.IsRuntimeReady);
            var operation = scope.Observe(actor, PerformanceCommand.Wardrobe,
                () => OutfitStatus(wardrobe, secondOutfit, WardrobeTransition.Dissolve(1.2f, 1.2f)),
                WardrobeChangeStatus.Cancelled, "second outfit dissolve");
            Action captureWardrobe = wardrobe.CaptureCancellation();
            float previousScale = Time.timeScale;
            bool transitionWasActive;
            try
            {
                Time.timeScale = 0f;
                await RealtimeFrames(3);
                transitionWasActive = actor.IsDissolving
                    && actor.VisibilityState == PerformerVisibilityState.DissolvingOut;
                captureWardrobe?.Invoke();
                Time.timeScale = previousScale;
                await WaitFor(scope, operation);
                Require(transitionWasActive,
                    "The second outfit reached dissolve-out before its request was cancelled.");
                Require(actor.VisibilityState == PerformerVisibilityState.Visible && !actor.IsDissolving,
                    "Cancelling wardrobe dissolve leaves the performer stably visible.");
                Require(operation.Result == WardrobeChangeStatus.Cancelled.ToString(),
                    "The wardrobe waiter settles Cancelled; actual result=" + operation.Result + ".");
            }
            finally { Time.timeScale = previousScale; }

            WardrobeChangeResult followup = await wardrobe.OutfitAsync(secondOutfit, WardrobeTransition.Cut);
            Record("Cancelling wardrobe dissolve preserves visibility and leaves the request queue usable",
                operation.Result == WardrobeChangeStatus.Cancelled.ToString()
                && actor.VisibilityState == PerformerVisibilityState.Visible
                && (followup.Status == WardrobeChangeStatus.Applied
                    || followup.Status == WardrobeChangeStatus.AlreadyEquipped
                    || followup.Status == WardrobeChangeStatus.NoChange)
                && wardrobe.CurrentWardrobe.OutfitId == secondOutfit.PresetId,
                "Cancelled request=" + operation.Result + "; follow-up=" + followup.Status
                + "; committed outfit=" + wardrobe.CurrentWardrobe.OutfitId);
        }

        private void MaskContractsAfterWardrobe()
        {
            Animator animator = actor.GetComponentInChildren<Animator>(true);
            Require(PerformerGestureMaskUtility.TryCreateMask(animator, out AvatarMask mask, out string reason), reason);
            try
            {
                bool valid = PerformerGestureMaskUtility.IsValidMask(animator, mask, out _);
                mask.SetTransformActive(0, false);
                bool missingRejected = !PerformerGestureMaskUtility.IsValidMask(animator, mask, out _);
                mask.SetTransformActive(0, true);
                mask.SetTransformPath(0, "");
                bool rootRejected = !PerformerGestureMaskUtility.IsValidMask(animator, mask, out _);
                Record("Wardrobe mask uses body bones and rejects missing or root paths", valid && missingRejected && rootRejected,
                    "Valid=" + valid + "; missing rejected=" + missingRejected + "; root rejected=" + rootRejected);
            }
            finally { Destroy(mask); }
        }

        private async Awaitable FirstContactStopReplay()
        {
            status = "First Contact stop and replay";
            if (firstContact == null)
            {
                throw new InvalidOperationException("Required FirstContactPerformance is not assigned.");
            }

            if (!firstContact.IsRunning && firstContact.CanRun) firstContact.Run();
            float phaseDeadline = Time.realtimeSinceStartup + 35f;
            while (firstContact.Status != "Speech A" && firstContact.IsRunning
                && Time.realtimeSinceStartup < phaseDeadline)
                await Awaitable.NextFrameAsync();
            bool reachedSpeech = firstContact.Status == "Speech A";
            if (reachedSpeech)
            {
                firstContact.Stop();
                await firstContact.ResetAsync();
            }
            bool stopResetReady = reachedSpeech && !firstContact.IsRunning
                && firstContact.Status == "Reset — ready to run" && actor.IsRuntimeReady;
            if (!stopResetReady)
            {
                Record("First Contact can stop and reset during Speech A before replay", false,
                    "Reached Speech A=" + reachedSpeech + "; status=" + firstContact.Status);
                return;
            }

            firstContact.Run();
            phaseDeadline = Time.realtimeSinceStartup + 45f;
            while (firstContact.Status != "Approaching" && firstContact.IsRunning && Time.realtimeSinceStartup < phaseDeadline)
                await Awaitable.NextFrameAsync();
            Require(firstContact.Status == "Approaching", "First Contact did not reach its movement beat: " + firstContact.Status);
            firstContact.Stop();
            await firstContact.ResetAsync();
            Record("First Contact stops and resets during movement", !actor.IsLocomoting && !actor.IsSpeaking && firstContact.CanRun,
                "Body and speech are idle, and the sequence is ready to replay.");
            firstContact.Run();
            float completionDeadline = Time.realtimeSinceStartup + 100f;
            while (firstContact.IsRunning && Time.realtimeSinceStartup < completionDeadline)
                await Awaitable.NextFrameAsync();
            bool naturallyCompleted = !firstContact.IsRunning
                && firstContact.Status.StartsWith("Complete", StringComparison.Ordinal);
            Record("First Contact runs to natural completion after stop and reset",
                naturallyCompleted, "Final status=" + firstContact.Status);
        }

        private async Awaitable WaitFor(PerformanceExecutionScope scope, params PerformanceExecutionScope.Operation[] operations)
        {
            try { await scope.Wait(operations); }
            catch (Exception exception) { throw new InvalidOperationException("Scoped regression wait failed: " + exception.Message, exception); }
        }

        private async Awaitable RealtimeFrames(int count)
        {
            for (int i = 0; i < count; i++) await Awaitable.NextFrameAsync();
        }

        private void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private void Record(string name, bool passed, string detail)
        {
            report.assertions.Add(new Assertion { name = name, passed = passed, detail = detail });
            if (!passed) throw new InvalidOperationException(name + " failed: " + detail);
            Debug.Log("Day 3 regression passed: " + name + " — " + detail, this);
        }

        private void Finish(bool passed, string exception)
        {
            if (finished) return;
            finished = true;
            Time.timeScale = 1f;
            if (!passed && firstContact != null) firstContact.Stop();
            running = false;
            report.status = passed ? "passed" : "failed";
            report.exception = exception;
            status = passed ? "Day 3 regressions passed" : "Day 3 regressions failed";
            try
            {
                string path = Application.isEditor ? Path.GetFullPath("TestOutput/day3/runtime.json") :
                    Path.Combine(Path.GetDirectoryName(Application.dataPath), "day3-runtime.json");
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, JsonUtility.ToJson(report, true));
                Debug.Log("Day 3 regression report: " + path, this);
            }
            catch (Exception writeException) { Debug.LogError("Could not write Day 3 report: " + writeException.Message, this); }

            if (Application.isBatchMode)
            {
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit(passed ? 0 : 1);
#endif
            }
        }

        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(20, 20, 440, 150), GUI.skin.box);
            GUILayout.Label("Day 3 Regression Runner: " + status);
            if (firstContact != null)
            {
                GUILayout.Label("First Contact: " + firstContact.Status);
                if (firstContact.IsRunning && GUILayout.Button("Stop First Contact")) firstContact.Stop();
                if (firstContact.CanRun && GUILayout.Button("Run First Contact")) firstContact.Run();
            }
            if (!running && !finished && GUILayout.Button("Run Regression Suite")) RunSuite();
            GUILayout.EndArea();
        }
    }
}
#pragma warning restore CS0618
