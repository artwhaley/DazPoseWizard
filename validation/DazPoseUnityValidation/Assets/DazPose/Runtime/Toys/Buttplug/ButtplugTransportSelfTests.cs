using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Buttplug.Core.Messages;

namespace DazPose.Toys.Buttplug
{
    /// <summary>Offline BT.1 checks for capability projection and runtime registry snapshots.</summary>
    public static class ButtplugTransportSelfTests
    {
        public static string[] Run()
        {
            var failures = new List<string>();
            CheckCapabilityProjection(failures);
            CheckSnapshotRangesAndFeatureGranularity(failures);
            CheckRegistryLifecycle(failures);
            return failures.ToArray();
        }

        public static async Task<string[]> RunDispatchOrderChecksAsync()
        {
            var failures = new List<string>();
            try
            {
                var raceGate = new ToyTransportDispatchOrderGate();
                var raceSubmissions = new List<string>();
                var reachedPreSubmitBarrier = new TaskCompletionSource<bool>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                var releasePreSubmitBarrier = new TaskCompletionSource<bool>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                int activeGeneration = 1;

                Task<Task> outputSubmission = raceGate.SubmitAsync(
                    () => activeGeneration == 1,
                    () => RecordSubmission(raceSubmissions, "output"),
                    async () =>
                    {
                        reachedPreSubmitBarrier.TrySetResult(true);
                        await releasePreSubmitBarrier.Task.ConfigureAwait(false);
                    });

                Task barrier = await Task.WhenAny(reachedPreSubmitBarrier.Task, Task.Delay(1000))
                    .ConfigureAwait(false);
                if (barrier != reachedPreSubmitBarrier.Task)
                {
                    failures.Add("the dispatch race fake reaches its post-validation, pre-submit barrier");
                    releasePreSubmitBarrier.TrySetResult(true);
                    return failures.ToArray();
                }

                // StopAsync invalidates the active generation before asking the backend to submit Stop.
                activeGeneration = 2;
                Task<Task> stopSubmission = raceGate.SubmitAsync(
                    () => true, () => RecordSubmission(raceSubmissions, "stop"));
                bool stopQueuedBehindOutput = !stopSubmission.IsCompleted;
                releasePreSubmitBarrier.TrySetResult(true);

                Task outputRequest = await outputSubmission.ConfigureAwait(false);
                Task stopRequest = await stopSubmission.ConfigureAwait(false);
                Check(stopQueuedBehindOutput && outputRequest != null && stopRequest != null
                    && raceSubmissions.SequenceEqual(new[] { "output", "stop" }),
                    "a command paused after validation submits before queued Stop, never after Stop", failures);

                var stopFirstGate = new ToyTransportDispatchOrderGate();
                var stopFirstSubmissions = new List<string>();
                int currentGeneration = 2;
                Task stopFirstRequest = await stopFirstGate.SubmitAsync(
                    () => true, () => RecordSubmission(stopFirstSubmissions, "stop"))
                    .ConfigureAwait(false);
                Task staleOutputRequest = await stopFirstGate.SubmitAsync(
                    () => currentGeneration == 1,
                    () => RecordSubmission(stopFirstSubmissions, "obsolete output"))
                    .ConfigureAwait(false);
                Check(stopFirstRequest != null && staleOutputRequest == null
                    && stopFirstSubmissions.SequenceEqual(new[] { "stop" }),
                    "when Stop submits first, an older generation is rejected at the gate", failures);

                var acknowledgementGate = new ToyTransportDispatchOrderGate();
                var acknowledgementSubmissions = new List<string>();
                var deviceAAcknowledgement = new TaskCompletionSource<bool>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                var stopAcknowledgement = new TaskCompletionSource<bool>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                var deviceBAcknowledgement = new TaskCompletionSource<bool>(
                    TaskCreationOptions.RunContinuationsAsynchronously);

                Task deviceARequest = await acknowledgementGate.SubmitAsync(
                    () => true, () => RecordSubmission(acknowledgementSubmissions,
                        "device A", deviceAAcknowledgement.Task)).ConfigureAwait(false);
                Task broadStopRequest = await acknowledgementGate.SubmitAsync(
                    () => true, () => RecordSubmission(acknowledgementSubmissions,
                        "stop", stopAcknowledgement.Task)).ConfigureAwait(false);
                Task deviceBRequest = await acknowledgementGate.SubmitAsync(
                    () => true, () => RecordSubmission(acknowledgementSubmissions,
                        "device B", deviceBAcknowledgement.Task)).ConfigureAwait(false);

                Check(deviceARequest != null && broadStopRequest != null && deviceBRequest != null
                    && !deviceAAcknowledgement.Task.IsCompleted
                    && acknowledgementSubmissions.SequenceEqual(new[] { "device A", "stop", "device B" }),
                    "Stop and another device submit while device A acknowledgement remains blocked", failures);

                deviceAAcknowledgement.TrySetResult(true);
                stopAcknowledgement.TrySetResult(true);
                deviceBAcknowledgement.TrySetResult(true);
                await Task.WhenAll(deviceARequest, broadStopRequest, deviceBRequest).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                failures.Add("dispatch-order regression self-test threw: " + exception);
            }

            return failures.ToArray();
        }

        private static Task RecordSubmission(List<string> submissions, string name, Task request = null)
        {
            submissions.Add(name);
            return request ?? Task.CompletedTask;
        }

        private static void CheckCapabilityProjection(List<string> failures)
        {
            CheckMapped(OutputType.Vibrate, ToyOutputCapability.Vibrate, failures);
            CheckMapped(OutputType.Oscillate, ToyOutputCapability.Oscillate, failures);
            CheckMapped(OutputType.Position, ToyOutputCapability.Position, failures);
            CheckMapped(OutputType.HwPositionWithDuration,
                ToyOutputCapability.HwPositionWithDuration, failures);

            OutputType[] ignored =
            {
                OutputType.Rotate,
                OutputType.Constrict,
                OutputType.Spray,
                OutputType.Temperature,
                OutputType.Led,
                OutputType.Unknown
            };
            foreach (OutputType outputType in ignored)
                Check(!ButtplugDeviceAdapter.TryToCapability(outputType, out _),
                    "unsupported output " + outputType + " is excluded from the game capability model", failures);

            Check(Enum.GetValues(typeof(ToyOutputCapability)).Length == 4,
                "the project capability enum contains only the four in-scope outputs", failures);
        }

        private static void CheckMapped(OutputType source, ToyOutputCapability expected, List<string> failures)
        {
            Check(ButtplugDeviceAdapter.TryToCapability(source, out ToyOutputCapability actual)
                && actual == expected, source + " maps to " + expected, failures);
        }

        private static void CheckSnapshotRangesAndFeatureGranularity(List<string> failures)
        {
            var firstFeature = new ToyFeature(2, "Primary", new[]
            {
                ToyOutputRange.Value(ToyOutputCapability.Vibrate, 0, 20),
                ToyOutputRange.Value(ToyOutputCapability.Oscillate, 1, 8),
                ToyOutputRange.Value(ToyOutputCapability.Position, 0, 100),
                ToyOutputRange.PositionWithDuration(0, 100, 50, 5000)
            });
            var secondFeature = new ToyFeature(7, "Independent actuator", Array.Empty<ToyOutputRange>());
            var device = new ToyDevice(13, "Test device", "Bench unit", 32,
                new[] { secondFeature, firstFeature });

            Check(device.DeviceIndex == 13 && device.Name == "Test device"
                && device.DisplayName == "Bench unit" && device.MessageTimingGapMilliseconds == 32,
                "device identity, labels, and timing gap are retained", failures);
            Check(device.Features.Count == 2 && device.Features[0].FeatureIndex == 2
                && device.Features[1].FeatureIndex == 7,
                "independent feature indices remain separate and ordered", failures);
            Check(device.Features[1].Outputs.Count == 0,
                "a feature without supported outputs remains representable", failures);
            Check(device.Features[0].Outputs.Count == 4
                && device.Features[0].TryGetOutput(ToyOutputCapability.Vibrate, out ToyOutputRange vibrate)
                && vibrate.MinimumValue == 0 && vibrate.MaximumValue == 20,
                "Vibrate range is retained per feature", failures);
            Check(device.Features[0].TryGetOutput(ToyOutputCapability.Oscillate, out ToyOutputRange oscillate)
                && oscillate.MinimumValue == 1 && oscillate.MaximumValue == 8,
                "Oscillate range is retained per feature", failures);
            Check(device.Features[0].TryGetOutput(ToyOutputCapability.Position, out ToyOutputRange position)
                && position.MinimumValue == 0 && position.MaximumValue == 100,
                "Position range is retained per feature", failures);
            Check(device.Features[0].TryGetOutput(ToyOutputCapability.HwPositionWithDuration,
                    out ToyOutputRange positionWithDuration)
                && positionWithDuration.MinimumValue == 0 && positionWithDuration.MaximumValue == 100
                && positionWithDuration.HasDurationRange
                && positionWithDuration.MinimumDurationMilliseconds == 50
                && positionWithDuration.MaximumDurationMilliseconds == 5000,
                "hardware position and duration ranges are retained", failures);

            ToyOutputRange durationOnly = ToyOutputRange.PositionWithDurationRanges(
                false, 0, 0, true, 100, 2000);
            Check(!durationOnly.HasValueRange && durationOnly.HasDurationRange
                && durationOnly.MinimumDurationMilliseconds == 100
                && durationOnly.MaximumDurationMilliseconds == 2000,
                "duration limits survive when the server omits the position value range", failures);
        }

        private static void CheckRegistryLifecycle(List<string> failures)
        {
            var registry = new ToyDeviceRegistry();
            Check(registry.Count == 0 && registry.Snapshot.Count == 0,
                "an empty registry is safe", failures);

            var first = new ToyDevice(4, "Device A", "", 0, Array.Empty<ToyFeature>());
            var second = new ToyDevice(9, "Device B", "", 0, Array.Empty<ToyFeature>());
            registry.Set(first);
            registry.Set(second);
            Check(registry.Count == 2 && registry.Snapshot[0].DeviceIndex == 4
                && registry.Snapshot[1].DeviceIndex == 9,
                "device additions are retained in stable index order", failures);
            Check(registry.Remove(4, out ToyDevice removed) && removed == first
                && registry.Count == 1 && registry.Snapshot[0].DeviceIndex == 9,
                "device removal clears only the matching runtime index", failures);
            ToyDevice[] cleared = registry.Clear();
            Check(cleared.Length == 1 && cleared[0] == second && registry.Count == 0,
                "server disconnect clears runtime availability", failures);
        }

        private static void Check(bool condition, string description, List<string> failures)
        {
            if (!condition) failures.Add(description);
        }
    }
}
