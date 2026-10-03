using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DazPose.Motion;
using DazPose.Performer;
using UnityEngine;

namespace DazPose.Toys
{
    /// <summary>Hardware-free routing, fan-out, motion sync, and output-coalescing checks.</summary>
    public static class ToyStackAcceptanceSelfTests
    {
        private sealed class FakeBackend : IToyBackend
        {
            private readonly List<ToyDevice> _devices;
            public readonly List<Tuple<ToyOutputBinding, float, uint>> Commands = new List<Tuple<ToyOutputBinding, float, uint>>();
            public readonly List<ToyOutputBinding> Failures = new List<ToyOutputBinding>();
            public readonly List<string> Operations = new List<string>();
            public readonly TaskCompletionSource<bool> FirstOutputStarted = new TaskCompletionSource<bool>();
            public readonly TaskCompletionSource<bool> ReleaseFirstOutput = new TaskCompletionSource<bool>();
            public bool BlockNextOutput;
            public int StopAllCount;
            public bool IsConnected => true;
            public ToyConnectionState ConnectionState => ToyConnectionState.Connected;
            public string StatusMessage => "Connected (acceptance fake).";
            public string LastError => string.Empty;
            public bool IsScanning => false;
            public IReadOnlyList<ToyDevice> Devices => _devices.AsReadOnly();
            public event Action<ToyConnectionState, string> StateChanged;
            public event Action<ToyDevice> DeviceAdded;
            public event Action<uint> DeviceRemoved;
            public event Action<bool> ScanningChanged;

            public FakeBackend(List<ToyDevice> devices) => _devices = devices;
            public Task ConnectAsync(string address) => Task.CompletedTask;
            public Task DisconnectAsync() => Task.CompletedTask;
            public Task StartScanningAsync() => Task.CompletedTask;
            public Task StopScanningAsync() => Task.CompletedTask;
            public Task StopAllAsync()
            {
                StopAllCount++;
                Operations.Add("stop all");
                return Task.CompletedTask;
            }
            public async Task SendOutputAsync(ToyOutputBinding binding, float value, uint durationMilliseconds = 0)
            {
                if (Failures.Contains(binding)) throw new InvalidOperationException("Simulated feature failure.");
                Commands.Add(Tuple.Create(binding, value, durationMilliseconds));
                Operations.Add("output " + binding);
                if (BlockNextOutput)
                {
                    BlockNextOutput = false;
                    FirstOutputStarted.TrySetResult(true);
                    await ReleaseFirstOutput.Task;
                }
            }
            public Task StopFeatureAsync(ToyOutputBinding binding) => Task.CompletedTask;
            public void Dispose() { }
            public void Add(ToyDevice device) { _devices.Add(device); DeviceAdded?.Invoke(device); }
            public void Remove(uint index)
            {
                _devices.RemoveAll(device => device.DeviceIndex == index);
                DeviceRemoved?.Invoke(index);
            }
        }

        public static async Task<string[]> RunAsync(FunscriptMotionProgram program)
        {
            var failures = new List<string>();
            var devices = new List<ToyDevice>
            {
                Device(1, Feature(0, ToyOutputCapability.Vibrate)),
                Device(2, Feature(0, ToyOutputCapability.Oscillate)),
                Device(3, Feature(0, ToyOutputCapability.Vibrate)),
                Device(4, Feature(0, ToyOutputCapability.Position, ToyOutputCapability.HwPositionWithDuration),
                    Feature(1, ToyOutputCapability.Position))
            };
            var backend = new FakeBackend(devices);
            var host = new GameObject("ToyStackAcceptanceSelfTests");
            MotionDriver driver = null;
            ToyControlService service = null;
            try
            {
                service = host.AddComponent<ToyControlService>();
                service.SetBackendForAcceptance(backend);
                var vibrate = new ToyOutputBinding(1, 0, ToyOutputCapability.Vibrate);
                var oscillate = new ToyOutputBinding(2, 0, ToyOutputCapability.Oscillate);
                var isolatedFailure = new ToyOutputBinding(3, 0, ToyOutputCapability.Vibrate);
                backend.Failures.Add(isolatedFailure);
                Check(service.SetLevelBinding(vibrate, true) && service.SetLevelBinding(oscillate, true)
                    && service.SetLevelBinding(isolatedFailure, true), "compatible level bindings can be assigned", failures);
                Check(!service.SetLevelBinding(new ToyOutputBinding(2, 0, ToyOutputCapability.Position), true),
                    "a capability mismatch cannot be assigned as a level", failures);

                ToyCommandReport report = await service.SetLevelAsync(1.5f);
                Check(service.Level == 1f && report.SuccessfulBindings == 2 && report.Errors.Count == 1
                    && backend.Commands.Count == 2, "level clamps and per-feature failures do not stop fan-out", failures);
                Check(backend.Commands[0].Item1 == vibrate && backend.Commands[0].Item2 == 1f
                    && backend.Commands[1].Item1 == oscillate && backend.Commands[1].Item2 == 1f,
                    "level values route as normalized percentages to exact features", failures);
                report = await service.SetLevelAsync(0f);
                Check(report.SuccessfulBindings == 2 && backend.Commands[2].Item2 == 0f
                    && backend.Commands[3].Item2 == 0f, "zero explicitly stops each assigned level output", failures);
                bool rejectedNaN = false;
                try { await service.SetLevelAsync(float.NaN); }
                catch (ArgumentOutOfRangeException) { rejectedNaN = true; }
                Check(rejectedNaN, "non-finite level commands are rejected", failures);

                var bounded = new ToyMotionBinding(new ToyOutputBinding(4, 1, ToyOutputCapability.Position),
                    ToyMotionStrategy.Position, true, 0.2f, 0.8f);
                Check(Mathf.Abs(bounded.Map(0f) - 0.8f) < 0.0001f
                    && Mathf.Abs(bounded.Map(1f) - 0.2f) < 0.0001f,
                    "motion travel limits and inversion map both endpoints", failures);

                if (program == null) failures.Add("The imported Funscript is required for motion follower checks.");
                else
                {
                    driver = host.AddComponent<MotionDriver>();
                    driver.SourceMode = MotionSourceMode.Funscript;
                    driver.FunscriptProgram = program;
                    driver.Seek(12.280d);
                    var positionBinding = new ToyOutputBinding(4, 1, ToyOutputCapability.Position);
                    var durationBinding = new ToyOutputBinding(4, 0, ToyOutputCapability.HwPositionWithDuration);
                    service.SetMotionBinding(positionBinding, true, ToyMotionStrategy.Position, true, 0.2f, 0.8f);
                    service.SetMotionBinding(durationBinding, true);
                    service.Follow(driver);
                    driver.StartMotion();
                    await Task.Delay(140);

                    Tuple<ToyOutputBinding, float, uint> firstDuration = Last(backend, durationBinding);
                    Check(firstDuration != null && Mathf.Abs(firstDuration.Item2 - 0.3f) < 0.0001f
                        && firstDuration.Item3 == 840,
                        "a mid-segment bind sends its target with remaining Funscript duration", failures);
                    Tuple<ToyOutputBinding, float, uint> firstPosition = Last(backend, positionBinding);
                    Check(firstPosition != null && Mathf.Abs(firstPosition.Item2 - 0.68f) < 0.0001f,
                        "continuous position follows and maps the same MotionDriver sample", failures);

                    int durationCommands = Count(backend, durationBinding);
                    for (int i = 0; i < 20; i++) driver.Advance(0.005f);
                    await Task.Delay(100);
                    Check(Count(backend, durationBinding) == durationCommands,
                        "duration output sends once per authored Funscript segment rather than per frame", failures);
                    Tuple<ToyOutputBinding, float, uint> coalesced = Last(backend, positionBinding);
                    Check(Count(backend, positionBinding) <= 5 && coalesced != null
                        && Mathf.Abs(coalesced.Item2 - bounded.Map(driver.CurrentSample.Position01)) < 0.0051f,
                        "continuous positions coalesce updates and deliver the newest value", failures);

                    driver.Seek(12.700d);
                    await Task.Delay(100);
                    Tuple<ToyOutputBinding, float, uint> sought = Last(backend, durationBinding);
                    Check(sought != null && sought.Item3 == 420 && Mathf.Abs(sought.Item2 - 0.3f) < 0.0001f,
                        "seek immediately resynchronizes a duration follower to the remaining segment", failures);
                    driver.StopMotion();
                    int pausedPositionCommands = Count(backend, positionBinding);
                    int pausedDurationCommands = Count(backend, durationBinding);
                    driver.Advance(1f);
                    await Task.Delay(100);
                    Check(Count(backend, positionBinding) == pausedPositionCommands
                        && Count(backend, durationBinding) == pausedDurationCommands,
                        "pause stops future motion sends", failures);
                driver.StartMotion();
                await Task.Delay(100);
                Tuple<ToyOutputBinding, float, uint> resumed = Last(backend, durationBinding);
                Check(resumed != null && resumed.Item3 == 420,
                    "resume immediately resynchronizes from the current motion segment", failures);

                driver.StopMotion();
                driver.SourceMode = MotionSourceMode.Sine;
                driver.Seek(0.25d);
                var automaticPosition = new ToyOutputBinding(4, 0, ToyOutputCapability.Position);
                driver.StartMotion();
                await Task.Delay(100);
                Tuple<ToyOutputBinding, float, uint> sine = Last(backend, automaticPosition);
                Check(sine != null && Mathf.Abs(sine.Item2 - 0.5f) < 0.0051f,
                    "Auto strategy falls back to continuous Position for a Sine source", failures);
                driver.StopMotion();
                }

                int commandCountBeforeStop = backend.Commands.Count;
                backend.BlockNextOutput = true;
                Task<ToyCommandReport> pendingLevel = service.SetLevelAsync(0.42f);
                await backend.FirstOutputStarted.Task;
                Task stopDuringFanOut = service.StopAllAsync();
                backend.ReleaseFirstOutput.TrySetResult(true);
                await Task.WhenAll(pendingLevel, stopDuringFanOut);
                Check(backend.Commands.Count == commandCountBeforeStop + 1 && backend.StopAllCount == 1
                    && backend.Operations[backend.Operations.Count - 1] == "stop all"
                    && service.Level == 0f,
                    "Stop All cancels queued level fan-out and is the final hardware command", failures);

                await service.StopAllWithReportAsync();
                Check(!service.IsFollowingMotion && service.Level == 0f,
                    "emergency stop clears desired level and terminates the follower", failures);
                failures.AddRange(RunLaraControlAnimationChecks());
                return failures.ToArray();
            }
            catch (Exception exception)
            {
                failures.Add("Unexpected acceptance-test exception: " + exception);
                return failures.ToArray();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        private static ToyDevice Device(uint index, params ToyFeature[] features) =>
            new ToyDevice(index, "Generic test device", "Generic test device", 0, features);

        private static ToyFeature Feature(uint index, params ToyOutputCapability[] capabilities)
        {
            var ranges = new List<ToyOutputRange>();
            foreach (ToyOutputCapability capability in capabilities)
                ranges.Add(capability == ToyOutputCapability.HwPositionWithDuration
                    ? ToyOutputRange.PositionWithDuration(0, 100, 20, 5000)
                    : ToyOutputRange.Value(capability, 0, 100));
            return new ToyFeature(index, "Generic feature", ranges);
        }

        private static Tuple<ToyOutputBinding, float, uint> Last(FakeBackend backend, ToyOutputBinding binding)
        {
            for (int i = backend.Commands.Count - 1; i >= 0; i--)
                if (backend.Commands[i].Item1 == binding) return backend.Commands[i];
            return null;
        }

        private static int Count(FakeBackend backend, ToyOutputBinding binding)
        {
            int count = 0;
            foreach (var command in backend.Commands) if (command.Item1 == binding) count++;
            return count;
        }

        private static string[] RunLaraControlAnimationChecks()
        {
            var failures = new List<string>();
            var controlObject = new GameObject("BT.6 Control Animation Test");
            try
            {
                var handle = new GameObject("BT.6 Test Handle");
                handle.transform.SetParent(controlObject.transform, false);
                var grip = new GameObject("BT.6 Grip");
                grip.transform.SetParent(handle.transform, false);
                grip.transform.localPosition = Vector3.up * 0.15f;
                var surface = controlObject.AddComponent<PerformerControlSurface>();
                surface.ConfigureHandle(handle.transform, Vector3.zero, Vector3.up * 0.1f);
                surface.GripPoint = grip.transform;
                surface.Value01 = 0.3f;

                var hand = new GameObject("BT.6 Test Hand");
                hand.transform.SetParent(controlObject.transform, false);
                hand.transform.position = Vector3.zero;
                var animator = controlObject.AddComponent<LaraControlAnimator>();
                animator.Hand = hand.transform;
                float maximumReach = 0f;
                foreach (float target in new[] { 0.66f, 0.30f, 1f })
                {
                    Task action = animator.SetControlAsync(surface, target);
                    for (int i = 0; i < 120 && !action.IsCompleted; i++)
                    {
                        animator.AdvanceControl(0.02f);
                        maximumReach = Mathf.Max(maximumReach, hand.transform.position.magnitude);
                    }
                    if (!action.IsCompleted) failures.Add("Lara control animation did not finish for target " + target + ".");
                    else action.GetAwaiter().GetResult();
                    if (Mathf.Abs(surface.Value01 - target) > 0.0001f)
                        failures.Add("Lara control surface missed normalized target " + target + ".");
                    if (hand.transform.position.sqrMagnitude > 0.0001f)
                        failures.Add("Lara did not return her hand to its captured pose.");
                }
                if (maximumReach < 0.1f) failures.Add("Lara hand did not reach toward and track the control surface.");
            }
            catch (Exception exception) { failures.Add("Lara control animation check threw: " + exception.Message); }
            finally { UnityEngine.Object.DestroyImmediate(controlObject); }
            return failures.ToArray();
        }

        private static void Check(bool condition, string description, List<string> failures)
        { if (!condition) failures.Add(description); }
    }
}
