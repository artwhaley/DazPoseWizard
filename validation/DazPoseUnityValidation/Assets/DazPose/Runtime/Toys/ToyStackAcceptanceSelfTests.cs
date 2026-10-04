using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DazPose.Motion;
using DazPose.Performer;
using UnityEngine;

namespace DazPose.Toys
{
    /// <summary>Hardware-free acceptance checks for automatic routes and bounded dispatch.</summary>
    public static class ToyStackAcceptanceSelfTests
    {
        private sealed class FakeBackend : IToyBackend
        {
            private readonly List<ToyDevice> _devices;
            private readonly object _gate = new object();
            public readonly List<Tuple<ToyOutputBinding, float, uint>> Commands = new List<Tuple<ToyOutputBinding, float, uint>>();
            public readonly List<ToyOutputBinding> Failures = new List<ToyOutputBinding>();
            public TaskCompletionSource<bool> BlockStarted { get; private set; } = new TaskCompletionSource<bool>();
            public TaskCompletionSource<bool> ReleaseBlock { get; private set; } = new TaskCompletionSource<bool>();
            public uint BlockedDevice = uint.MaxValue;
            public int StopAllCount;
            public bool IsConnected => true;
            public ToyConnectionState ConnectionState => ToyConnectionState.Connected;
            public string StatusMessage => "Connected (acceptance fake).";
            public string LastError => string.Empty;
            public bool IsScanning => false;
            public IReadOnlyList<ToyDevice> Devices { get { lock (_gate) return _devices.ToArray(); } }
            public int TotalCount { get { lock (_gate) return Commands.Count; } }
            public event Action<ToyConnectionState, string> StateChanged { add { } remove { } }
            public event Action<ToyDevice> DeviceAdded { add { } remove { } }
            public event Action<uint> DeviceRemoved { add { } remove { } }
            public event Action<bool> ScanningChanged { add { } remove { } }

            public FakeBackend(List<ToyDevice> devices) => _devices = devices;
            public void BlockDevice(uint device)
            {
                BlockedDevice = device;
                BlockStarted = new TaskCompletionSource<bool>();
                ReleaseBlock = new TaskCompletionSource<bool>();
            }
            public Task ConnectAsync(string address) => Task.CompletedTask;
            public Task DisconnectAsync() => Task.CompletedTask;
            public Task StartScanningAsync() => Task.CompletedTask;
            public Task StopScanningAsync() => Task.CompletedTask;
            public Task StopAllAsync() { StopAllCount++; return Task.CompletedTask; }

            public async Task SendOutputAsync(ToyOutputBinding binding, float value,
                uint durationMilliseconds = 0, Func<bool> stillCurrent = null)
            {
                if (Failures.Contains(binding)) throw new InvalidOperationException("Simulated feature failure.");
                if (binding.DeviceIndex == BlockedDevice)
                {
                    BlockStarted.TrySetResult(true);
                    await ReleaseBlock.Task;
                }
                if (stillCurrent != null && !stillCurrent()) return;
                lock (_gate) Commands.Add(Tuple.Create(binding, value, durationMilliseconds));
            }

            public void Dispose() { }
            public int Count(ToyOutputBinding binding)
            { lock (_gate) return Commands.Count(command => command.Item1 == binding); }
            public Tuple<ToyOutputBinding, float, uint> Last(ToyOutputBinding binding)
            {
                lock (_gate) return Commands.LastOrDefault(command => command.Item1 == binding);
            }
        }

        public static async Task<string[]> RunAsync(FunscriptMotionProgram program)
        {
            var failures = new List<string>();
            var devices = new List<ToyDevice>
            {
                Device(1, Feature(0, ToyOutputCapability.Vibrate), Feature(2, ToyOutputCapability.Vibrate),
                    Feature(4, ToyOutputCapability.Oscillate)),
                Device(2, Feature(0, ToyOutputCapability.Vibrate, ToyOutputCapability.Oscillate)),
                Device(3, Feature(0, ToyOutputCapability.Vibrate)),
                Device(4, Feature(0, ToyOutputCapability.Position, ToyOutputCapability.HwPositionWithDuration),
                    Feature(1, ToyOutputCapability.Position), Feature(2, ToyOutputCapability.HwPositionWithDuration))
            };
            var backend = new FakeBackend(devices);
            var host = new GameObject("ToyStackAcceptanceSelfTests");
            MotionDriver driver = null;
            ToyControlService service = null;
            try
            {
                service = host.AddComponent<ToyControlService>();
                service.SetBackendForAcceptance(backend);
                Check(service.CanVibrate && service.CanOscillate && service.CanStroke,
                    "capability availability reflects discovered outputs", failures);
                Check(service.HasVibrationChannel(1) && service.HasOscillationChannel(0)
                    && !service.HasOscillationChannel(1), "channels are numbered within each capability and device", failures);
                Check(backend.TotalCount == 0, "discovery alone sends no output", failures);

                var vibrate0 = new ToyOutputBinding(1, 0, ToyOutputCapability.Vibrate);
                var vibrate1 = new ToyOutputBinding(1, 2, ToyOutputCapability.Vibrate);
                var oscillate = new ToyOutputBinding(1, 4, ToyOutputCapability.Oscillate);
                service.Vibrate(1.5f);
                await WaitForCommands(backend, 4);
                Check(backend.Count(vibrate0) == 1 && backend.Count(vibrate1) == 1
                    && backend.Last(vibrate0).Item2 == 1f
                    && backend.Count(oscillate) == 0,
                    "Vibrate clamps and automatically routes across every vibration feature", failures);

                service.Vibrate(0.4f, 1);
                await WaitForCommands(backend, 5);
                Check(backend.Last(vibrate1).Item2 == 0.4f
                    && backend.Last(vibrate0).Item2 == 1f,
                    "channel one affects the second vibration feature without changing channel zero", failures);
                service.Oscillate(0.7f);
                await WaitForCommands(backend, 7);
                Check(backend.Last(oscillate).Item2 == 0.7f,
                    "oscillation has independent automatic output routing", failures);

                GameObject controlObject = new GameObject("ToyBindingAcceptanceControl");
                controlObject.transform.SetParent(host.transform, false);
                PerformerControlSurface control = controlObject.AddComponent<PerformerControlSurface>();
                control.Value01 = 0.33f;
                int beforeBinding = backend.Count(vibrate0);
                service.BindVibration(control);
                await WaitUntil(() => backend.Count(vibrate0) > beforeBinding);
                Check(Mathf.Abs(backend.Last(vibrate0).Item2 - 0.33f) < 0.001f,
                    "binding sends the current visible-control value", failures);
                service.Vibrate(0.8f);
                await WaitUntil(() => Mathf.Abs(backend.Last(vibrate0).Item2 - 0.8f) < 0.001f);
                int afterOverride = backend.Count(vibrate0);
                control.Value01 = 0.9f;
                await Task.Delay(30);
                Check(backend.Count(vibrate0) == afterOverride
                    && Mathf.Abs(backend.Last(vibrate0).Item2 - 0.8f) < 0.001f,
                    "a newer direct command prevents the old control binding from resuming", failures);
                service.UnbindControl(control);
                await Task.Delay(20);
                Check(Mathf.Abs(backend.Last(vibrate0).Item2 - 0.8f) < 0.001f,
                    "unbinding a superseded control does not overwrite the newer command", failures);

                GameObject ownedControlObject = new GameObject("ToyUnbindAcceptanceControl");
                ownedControlObject.transform.SetParent(host.transform, false);
                PerformerControlSurface ownedControl = ownedControlObject.AddComponent<PerformerControlSurface>();
                ownedControl.Value01 = 0.42f;
                service.BindVibration(ownedControl);
                await WaitUntil(() => Mathf.Abs(backend.Last(vibrate0).Item2 - 0.42f) < 0.001f);
                service.UnbindControl(ownedControl);
                await WaitUntil(() => Mathf.Abs(backend.Last(vibrate0).Item2) < 0.001f);
                Check(Mathf.Abs(backend.Last(vibrate0).Item2) < 0.001f,
                    "unbinding an active control zeros the outputs it still owns", failures);

                var missingFailure = new ToyOutputBinding(3, 0, ToyOutputCapability.Vibrate);
                backend.Failures.Add(missingFailure);
                var healthyVibration = new ToyOutputBinding(2, 0, ToyOutputCapability.Vibrate);
                int healthyBeforeFailure = backend.Count(healthyVibration);
                service.Vibrate(0.2f);
                await Task.Delay(50);
                Check(backend.Count(healthyVibration) == healthyBeforeFailure + 1,
                    "a failed feature does not prevent other devices from receiving the command", failures);
                backend.Failures.Clear();

                // Block one device while another gets the same capability command.
                backend.BlockDevice(1);
                int healthyBeforeBlock = backend.Count(healthyVibration);
                service.Vibrate(0.6f);
                await backend.BlockStarted.Task;
                await WaitUntil(() => backend.Count(healthyVibration) >= healthyBeforeBlock + 1);
                Check(backend.Count(healthyVibration) >= healthyBeforeBlock + 1,
                    "a blocked device does not hold up another device", failures);
                backend.ReleaseBlock.TrySetResult(true);
                await Task.Delay(50);
                backend.BlockedDevice = uint.MaxValue;

                // One active worker and one latest pending value per feature under a flood.
                int beforeFlood = backend.Count(vibrate0);
                backend.BlockDevice(1);
                service.Vibrate(0.1f);
                await backend.BlockStarted.Task;
                for (int i = 0; i < 40; i++) service.Vibrate(i / 40f);
                backend.ReleaseBlock.TrySetResult(true);
                await Task.Delay(150);
                Check(backend.Count(vibrate0) - beforeFlood == 1
                    && Mathf.Abs(backend.Last(vibrate0).Item2 - 0.975f) < 0.001f,
                    "repeated input keeps bounded work and delivers the newest pending value", failures);
                backend.BlockedDevice = uint.MaxValue;

                var bounded = new ToyMotionBinding(new ToyOutputBinding(4, 1, ToyOutputCapability.Position),
                    ToyMotionStrategy.Position, true, 0.2f, 0.8f);
                Check(Mathf.Abs(bounded.Map(0f) - 0.8f) < 0.0001f
                    && Mathf.Abs(bounded.Map(1f) - 0.2f) < 0.0001f,
                    "scene range and inversion map both movement endpoints", failures);

                if (program == null) failures.Add("The imported Funscript is required for timed-position checks.");
                else
                {
                    driver = host.AddComponent<MotionDriver>();
                    driver.SourceMode = MotionSourceMode.Funscript;
                    driver.FunscriptProgram = program;
                    driver.Seek(12.280d);
                    service.Follow(driver);
                    driver.StartMotion();
                    await Task.Delay(120);
                    var durationBinding = new ToyOutputBinding(4, 0, ToyOutputCapability.HwPositionWithDuration);
                    var positionBinding = new ToyOutputBinding(4, 1, ToyOutputCapability.Position);
                    Tuple<ToyOutputBinding, float, uint> firstDuration = backend.Last(durationBinding);
                    Check(firstDuration != null && Mathf.Abs(firstDuration.Item2 - 0.3f) < 0.0001f
                        && firstDuration.Item3 > 0,
                        "a timed-position feature receives the current authored Funscript target", failures);
                    Check(backend.Last(positionBinding) != null,
                        "direct Position features follow the same MotionDriver sample", failures);

                    int timedBeforeSampledAdvance = backend.Count(durationBinding);
                    for (int i = 0; i < 20; i++) driver.Advance(0.005f);
                    await Task.Delay(60);
                    Check(backend.Count(durationBinding) == timedBeforeSampledAdvance,
                        "timed-position output sends no frame-rate corrections between authored segments", failures);

                    int beforeNextSegment = backend.Count(durationBinding);
                    driver.Advance(0.8f);
                    await Task.Delay(60);
                    Check(backend.Count(durationBinding) == beforeNextSegment + 1,
                        "a new authored Funscript segment sends one new timed target", failures);

                    int beforeSourceReplacement = backend.Count(durationBinding);
                    backend.BlockDevice(4);
                    driver.Seek(14.0d);
                    await backend.BlockStarted.Task;
                    driver.SourceMode = MotionSourceMode.Sine;
                    backend.ReleaseBlock.TrySetResult(true);
                    await Task.Delay(60);
                    Check(backend.Count(durationBinding) == beforeSourceReplacement,
                        "source replacement cancels a timed command that has not reached the device", failures);
                    backend.BlockedDevice = uint.MaxValue;

                    driver.StopMotion();
                    int beforeSine = backend.Count(new ToyOutputBinding(4, 2, ToyOutputCapability.HwPositionWithDuration));
                    service.Follow(driver);
                    driver.StartMotion();
                    for (int i = 0; i < 20; i++) driver.Advance(0.01f);
                    await Task.Delay(100);
                    Check(backend.Count(new ToyOutputBinding(4, 2, ToyOutputCapability.HwPositionWithDuration)) == beforeSine,
                        "a duration-only feature stays idle for sampled sine motion", failures);
                    driver.StopMotion();
                }

                await service.RampVibrationAsync(0.35f, 0f);
                Check(backend.Last(vibrate0) != null && backend.Last(vibrate0).Item2 == 0.35f,
                    "zero-duration authored ramp completes at the requested target", failures);

                int stopCountBefore = backend.StopAllCount;
                service.Stop();
                await Task.Delay(30);
                Check(backend.StopAllCount == stopCountBefore + 1 && !service.IsFollowingMotion,
                    "Stop invalidates active effects and issues broad backend stop", failures);
                int beforePostStopCommand = backend.Count(vibrate0);
                service.Vibrate(0.25f);
                await WaitUntil(() => backend.Count(vibrate0) > beforePostStopCommand);
                Check(Mathf.Abs(backend.Last(vibrate0).Item2 - 0.25f) < 0.001f,
                    "an explicit command after Stop starts output normally", failures);
                return failures.ToArray();
            }
            catch (Exception exception)
            {
                failures.Add("Unexpected toy acceptance exception: " + exception);
                return failures.ToArray();
            }
            finally { UnityEngine.Object.DestroyImmediate(host); }
        }

        private static async Task WaitForCommands(FakeBackend backend, int expected)
        {
            await WaitUntil(() => backend.TotalCount >= expected);
        }

        private static async Task WaitUntil(Func<bool> condition)
        {
            for (int i = 0; i < 100 && !condition(); i++) await Task.Delay(10);
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

        private static void Check(bool condition, string description, List<string> failures)
        { if (!condition) failures.Add(description); }
    }
}
