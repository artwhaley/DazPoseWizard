using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DazPose.Performer;
using UnityEngine;

namespace DazPose.Validation
{
    /// <summary>R03 saved-fixture witness for reversible footwear state and canonical foot-region fits.</summary>
    public sealed class RemediationToeRepairRunner : MonoBehaviour
    {
        [Serializable] private sealed class Assertion
        { public string name, expected, actual; public bool passed; }

        [Serializable] private sealed class BoneSample
        { public string name; public Vector3 localPosition; public Quaternion localRotation; }

        [Serializable] private sealed class Observation
        {
            public string station;
            public int visibleMask, populatedMask;
            public bool footwearPoseActive, driverFootwearActive;
            public string effectiveFootwear, meshHash;
            public float standingLift, footShrink, capturedHeelWeight, wardrobeFootFitWeight;
            public int meshVertexCount, meshBlendShapeCount, toeRegionVertexCount;
            public float toeRegionMaxDifferenceFromBarefoot;
            public List<BoneSample> bones = new List<BoneSample>();
            [NonSerialized] public Vector3[] bakedVertices;
            [NonSerialized] public int[] toeRegionIndices;
        }

        [Serializable] private sealed class PriorExperiment
        { public string name, detail; public float maxToeDifference; }

        [Serializable] private sealed class PriorReport
        { public List<PriorExperiment> attribution = new List<PriorExperiment>(); }

        [Serializable] private sealed class Report
        {
            public string ticket = "R03";
            public string startedUtc, completedUtc, sourceHash;
            public string[] inputAssetHashes;
            public string priorReportPath, rootCause;
            public float beforeResidual, afterResidual, maxCycleResidual, maxOwnedToePositionDifference;
            public bool knownProductFailureObserved, complete, passed;
            public string exception;
            public List<Assertion> assertions = new List<Assertion>();
            public List<Observation> observations = new List<Observation>();
        }

        [SerializeField] private SuccubusPerformer actor, isolationActor;
        [SerializeField] private string reportPath, sourceHash, inputHash, priorReportPath;
        [SerializeField] private int compiledStateCount;
        [SerializeField] private int[] compiledMasks = Array.Empty<int>();
        private bool finished;
        private Report report;

#if UNITY_EDITOR
        public void Configure(SuccubusPerformer primary, SuccubusPerformer isolation, string output,
            string source, string input, string prior, int validatedStates, int[] masks)
        {
            actor = primary; isolationActor = isolation; reportPath = output; sourceHash = source;
            inputHash = input; priorReportPath = prior; compiledStateCount = validatedStates;
            compiledMasks = masks ?? Array.Empty<int>();
        }
#endif

        private async void Start()
        {
            report = new Report
            {
                startedUtc = DateTime.UtcNow.ToString("O"), sourceHash = sourceHash ?? string.Empty,
                inputAssetHashes = new[] { inputHash ?? string.Empty }, priorReportPath = priorReportPath ?? string.Empty
            };
            try
            {
                LoadPriorEvidence();
                await RunWitness();
                Finish(report.assertions.All(item => item.passed), null);
            }
            catch (Exception exception) { Finish(false, exception.ToString()); }
        }

        private void LoadPriorEvidence()
        {
            Check("priorR02EvidenceResolves", File.Exists(priorReportPath),
                "R03 requires the completed R02 report at " + priorReportPath);
            var prior = JsonUtility.FromJson<PriorReport>(File.ReadAllText(priorReportPath));
            var transform = prior != null && prior.attribution != null
                ? prior.attribution.FirstOrDefault(item => item.name == "restore-canonical-toe-transforms-with-mask1-mesh")
                : null;
            Check("priorR02TransformMeasurementResolves", transform != null,
                "R02 attribution did not contain the transform-restoration measurement.");
            report.beforeResidual = transform.maxToeDifference;
            report.knownProductFailureObserved = report.beforeResidual > .0001f;
            report.rootCause = "R02 measured a residual after transform restoration while canonical mesh substitution removed it; the fitted body foot region was the dominant cause, with stale writer-owned toe positions also requiring rollback repair.";
            Check("priorR02FailureWasMaterial", report.knownProductFailureObserved,
                "R02 failed-before residual = " + report.beforeResidual.ToString("G9") + " m; expected > 0.0001 m.");
        }

        private async Awaitable RunWitness()
        {
            for (int frame = 0; frame < 300 && (actor == null || !actor.IsRuntimeReady ||
                isolationActor == null || !isolationActor.IsRuntimeReady); ++frame)
                await Awaitable.NextFrameAsync();
            Check("primaryActorRuntimeReady", actor != null && actor.IsRuntimeReady, "Primary actor did not become ready.");
            Check("isolationActorRuntimeReady", isolationActor != null && isolationActor.IsRuntimeReady, "Isolation actor did not become ready.");
            var wardrobe = actor != null ? actor.GetComponent<PerformerWardrobe>() : null;
            var isolationWardrobe = isolationActor != null ? isolationActor.GetComponent<PerformerWardrobe>() : null;
            Check("primaryWardrobeRuntimeReady", wardrobe != null && wardrobe.CurrentWardrobe != null && wardrobe.Body != null,
                "Primary actor has no usable wardrobe binding.");
            Check("isolationIsIndependent", isolationWardrobe != null && isolationWardrobe.Body != null &&
                isolationActor != actor && isolationWardrobe != wardrobe && isolationWardrobe.Body != wardrobe.Body,
                "R03 requires a second actor with an independent wardrobe/body.");
            Check("compiledFootRegionStates", compiledStateCount > 0 && compiledMasks.Contains(1),
                "R03 editor preparation did not report a validated mask-1 fit state.");

            string isolationMesh = MeshHash(isolationWardrobe.Body.sharedMesh);
            int isolationMask = isolationWardrobe.CurrentWardrobe.VisibleMask;
            bool isolationDriver = isolationActor.GetComponent<FootwearPoseDriver>()?.IsFootwearActive ?? false;

            var second = wardrobe.Catalog.Presets.FirstOrDefault(item => item != null && item.PresetId == "second-outfit");
            Check("secondOutfitResolvesFromSavedCandidateCatalog", second != null,
                "The saved candidate catalog must contain second-outfit.");
            Applied(await actor.OutfitAsync(second), "dress second outfit");
            await StableFrames(30);
            Capture("mask3-dressed", wardrobe);

            Applied(await actor.TryRemoveLayerAsync(), "remove Layer 1");
            await StableFrames(30);
            var mask1 = Capture("mask1-after-remove", wardrobe);
            Applied(await actor.TryRemoveLayerAsync(), "remove Base");
            await StableFrames(30);
            var barefoot = Capture("mask0-barefoot", wardrobe);
            Applied(await actor.TryAddLayerAsync(), "add Base");
            await StableFrames(30);
            var mask1AfterAdd = Capture("mask1-after-add", wardrobe);
            Applied(await actor.TryAddLayerAsync(), "add Layer 1");
            await StableFrames(30);
            var dressedAgain = Capture("mask3-restored", wardrobe);

            CheckMask1AgainstBarefoot(mask1, barefoot, "mask1-after-remove");
            CheckMask1AgainstBarefoot(mask1AfterAdd, barefoot, "mask1-after-add");
            report.afterResidual = mask1.toeRegionMaxDifferenceFromBarefoot;
            Check("mask1KeepsBaseGarments", mask1.visibleMask == 1 && mask1.populatedMask >= 1,
                "Layer 1 removal must leave Base garments visible.");
            Check("mask1FootwearChannelsReleased", !mask1.driverFootwearActive && !mask1.footwearPoseActive &&
                Mathf.Abs(mask1.standingLift) <= .000001f && Mathf.Abs(mask1.footShrink) <= .000001f,
                "Layer 1 removal must release shoe support, bent-foot pose and height/fit contributions.");
            Check("mask3RestoresReviewedFootwear", dressedAgain.visibleMask == 3 && dressedAgain.driverFootwearActive,
                "Adding Layer 1 must restore the reviewed footwear state.");
            Check("mask1ToeResidualAfterRepair", report.afterResidual <= .0001f,
                "R03 mask-1 toe residual = " + report.afterResidual.ToString("G9") + " m; threshold is 0.0001 m.");
            Check("mask1OwnedToePositionsAfterRepair", report.maxOwnedToePositionDifference <= .000001f,
                "R03 owned toe local-position difference = " + report.maxOwnedToePositionDifference.ToString("G9") + " m; threshold is 0.000001 m.");

            for (int cycle = 0; cycle < 10; ++cycle)
            {
                Applied(await actor.TryRemoveLayerAsync(), "cycle " + cycle + " remove Layer 1");
                await StableFrames(10);
                var cycleMask1 = Capture("cycle-" + cycle + "-mask1", wardrobe);
                cycleMask1.toeRegionMaxDifferenceFromBarefoot = ToeDifference(cycleMask1.bakedVertices,
                    barefoot.bakedVertices, cycleMask1.toeRegionIndices.Intersect(barefoot.toeRegionIndices).ToArray());
                report.maxCycleResidual = Mathf.Max(report.maxCycleResidual, cycleMask1.toeRegionMaxDifferenceFromBarefoot);
                Applied(await actor.TryAddLayerAsync(), "cycle " + cycle + " add Layer 1");
                await StableFrames(10);
            }
            Check("tenCompleteCyclesNoAccumulation", report.maxCycleResidual <= .0001f,
                "Maximum cycle mask-1 residual = " + report.maxCycleResidual.ToString("G9") + " m; threshold is 0.0001 m.");

            Applied(await actor.TryRemoveLayerAsync(), "prepare failed rebind rollback");
            await StableFrames(10);
            var rollbackBefore = Capture("rollback-before", wardrobe);
            var rejected = await actor.OutfitAsync("missing-remediation-outfit");
            await StableFrames(2);
            var rollbackAfter = Capture("rollback-after", wardrobe);
            Check("failedRebindRestoresPriorAppearance", rejected.Status == WardrobeChangeStatus.Failed &&
                rollbackAfter.visibleMask == rollbackBefore.visibleMask && rollbackAfter.meshHash == rollbackBefore.meshHash,
                "Failed outfit rebind must leave the prior mask and mesh committed.");
            Applied(await actor.TryAddLayerAsync(), "restore Layer 1 after rollback");
            await StableFrames(10);

            var heel = actor.GetComponent<LaraHeelReview>();
            if (heel != null)
            {
                heel.enabled = false;
                await StableFrames(2);
                heel.enabled = true;
                await StableFrames(10);
            }
            var reenabled = Capture("mask3-after-writer-reenable", wardrobe);
            Check("disablingReenablingWriterRestoresValidState", reenabled.visibleMask == 3 && reenabled.driverFootwearActive,
                "Disabling/re-enabling the footwear writer must restore the valid reviewed state.");

            var third = wardrobe.Catalog.Presets.FirstOrDefault(item => item != null && item.PresetId == "third-outfit");
            Check("thirdOutfitResolves", third != null, "The saved candidate catalog must contain third-outfit.");
            Applied(await actor.OutfitAsync(third), "apply hosiery-only third outfit");
            await StableFrames(30);
            var thirdDressed = Capture("third-outfit-with-shoes", wardrobe);
            Applied(await actor.TryRemoveLayerAsync(), "remove third-outfit shoes");
            await StableFrames(30);
            var hosiery = Capture("third-outfit-hosiery", wardrobe);
            Check("thirdOutfitStartsWithShoes", thirdDressed.visibleMask == 3 && thirdDressed.driverFootwearActive,
                "The isolated third-outfit fixture must start with footwear on Layer 1.");
            Check("hosieryKeepsBentFeetWithoutShoeSupport", hosiery.visibleMask == 1 && hosiery.footwearPoseActive && !hosiery.driverFootwearActive,
                "Hosiery-only third outfit must retain bent feet without shoe contact support.");

            Check("isolationActorUnaffected", MeshHash(isolationWardrobe.Body.sharedMesh) == isolationMesh &&
                isolationWardrobe.CurrentWardrobe.VisibleMask == isolationMask &&
                (isolationActor.GetComponent<FootwearPoseDriver>()?.IsFootwearActive ?? false) == isolationDriver,
                "Primary wardrobe transitions must not alter the independent second actor.");
        }

        private Observation Capture(string station, PerformerWardrobe wardrobe)
        {
            var state = wardrobe.CurrentWardrobe;
            var body = wardrobe.Body;
            Check("stateCaptured_" + station, state != null && body != null && body.sharedMesh != null,
                "Station has committed wardrobe state and body mesh.");
            var driver = actor.GetComponent<FootwearPoseDriver>();
            var baked = CaptureBaked(body);
            var sample = new Observation
            {
                station = station, visibleMask = state.VisibleMask, populatedMask = state.PopulatedMask,
                footwearPoseActive = state.CurrentFootwearPoseActive, effectiveFootwear = state.EffectiveFootwearId,
                driverFootwearActive = driver != null && driver.IsFootwearActive,
                standingLift = driver != null ? driver.StandingLift : 0f, footShrink = driver != null ? driver.FootShrink : 0f,
                meshVertexCount = body.sharedMesh.vertexCount, meshBlendShapeCount = body.sharedMesh.blendShapeCount,
                meshHash = MeshHash(body.sharedMesh), capturedHeelWeight = BlendWeight(body, "CapturedHeelFootPose"),
                wardrobeFootFitWeight = BlendWeight(body, "WardrobeFootFit"), bakedVertices = baked, toeRegionIndices = ToeRegion(body)
            };
            sample.toeRegionVertexCount = sample.toeRegionIndices.Length;
            foreach (Transform bone in body.bones.Where(b => b != null && IsToe(b.name)).OrderBy(b => b.name))
                sample.bones.Add(new BoneSample { name = bone.name, localPosition = bone.localPosition, localRotation = bone.localRotation });
            report.observations.Add(sample);
            return sample;
        }

        private void CheckMask1AgainstBarefoot(Observation sample, Observation barefoot, string label)
        {
            sample.toeRegionMaxDifferenceFromBarefoot = ToeDifference(sample.bakedVertices, barefoot.bakedVertices,
                sample.toeRegionIndices.Intersect(barefoot.toeRegionIndices).ToArray());
            report.maxOwnedToePositionDifference = Mathf.Max(report.maxOwnedToePositionDifference,
                BonePositionDifference(sample, barefoot));
            Check(label + "ToeResidual", sample.toeRegionMaxDifferenceFromBarefoot <= .0001f,
                label + " toe residual = " + sample.toeRegionMaxDifferenceFromBarefoot.ToString("G9") + " m.");
        }

        private static float BonePositionDifference(Observation a, Observation b)
        {
            float max = 0f;
            foreach (var bone in a.bones)
            {
                var other = b.bones.FirstOrDefault(item => item.name == bone.name);
                if (other != null) max = Mathf.Max(max, Vector3.Distance(bone.localPosition, other.localPosition));
            }
            return max;
        }

        private static Vector3[] CaptureBaked(SkinnedMeshRenderer body)
        {
            var mesh = new Mesh { name = "R03 Toe Capture" };
            body.BakeMesh(mesh, true);
            var vertices = mesh.vertices;
            Destroy(mesh);
            return vertices;
        }

        private static int[] ToeRegion(SkinnedMeshRenderer body)
        {
            string[] names = body.bones.Select(b => b != null ? b.name : string.Empty).ToArray();
            var result = new List<int>();
            BoneWeight[] weights = body.sharedMesh.boneWeights;
            for (int i = 0; i < weights.Length; ++i)
            {
                var weight = weights[i];
                float influence = Weight(weight.boneIndex0, weight.weight0, names) + Weight(weight.boneIndex1, weight.weight1, names) +
                    Weight(weight.boneIndex2, weight.weight2, names) + Weight(weight.boneIndex3, weight.weight3, names);
                if (influence >= .5f) result.Add(i);
            }
            return result.ToArray();
        }

        private static float Weight(int index, float value, string[] names) =>
            index >= 0 && index < names.Length && IsToe(names[index]) ? value : 0f;

        private static bool IsToe(string name) => !string.IsNullOrEmpty(name) &&
            (name.IndexOf("Toe", StringComparison.OrdinalIgnoreCase) >= 0 ||
             name.IndexOf("Metatarsals", StringComparison.OrdinalIgnoreCase) >= 0 ||
             name.IndexOf("Foot", StringComparison.OrdinalIgnoreCase) >= 0);

        private static float ToeDifference(Vector3[] a, Vector3[] b, int[] indices)
        {
            if (a == null || b == null || a.Length != b.Length) return float.PositiveInfinity;
            float max = 0f;
            foreach (int index in indices ?? Array.Empty<int>())
                if (index >= 0 && index < a.Length) max = Mathf.Max(max, Vector3.Distance(a[index], b[index]));
            return max;
        }

        private static float BlendWeight(SkinnedMeshRenderer body, string name)
        {
            int index = body.sharedMesh.GetBlendShapeIndex(name);
            return index >= 0 ? body.GetBlendShapeWeight(index) : 0f;
        }

        private static string MeshHash(Mesh mesh)
        {
            unchecked
            {
                int hash = 17;
                foreach (Vector3 value in mesh.vertices) hash = hash * 31 + value.GetHashCode();
                foreach (Matrix4x4 value in mesh.bindposes) hash = hash * 31 + value.GetHashCode();
                return hash.ToString("X8");
            }
        }

        private async Awaitable StableFrames(int count)
        { for (int i = 0; i < count; ++i) await Awaitable.NextFrameAsync(); }

        private static void Applied(WardrobeChangeResult result, string phase)
        {
            bool passed = result.Status == WardrobeChangeStatus.Applied || result.Status == WardrobeChangeStatus.AlreadyEquipped;
            // This is intentionally returned to the caller through the assertion helper below.
            if (!passed) throw new InvalidOperationException(phase + ": " + result.Status + " / " + result.Message);
        }

        private void Check(string name, bool passed, string detail)
        {
            report.assertions.Add(new Assertion { name = name, expected = "pass", actual = detail, passed = passed });
            if (!passed) throw new InvalidOperationException(name + ": " + detail);
        }

        private void Finish(bool passed, string exception)
        {
            if (finished) return;
            finished = true; report.exception = exception; report.passed = passed; report.complete = true;
            report.completedUtc = DateTime.UtcNow.ToString("O");
            string output = string.IsNullOrWhiteSpace(reportPath) ? Path.Combine("TestOutput", "remediation", "R03.json") : reportPath;
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output)));
            File.WriteAllText(output, JsonUtility.ToJson(report, true));
#if UNITY_EDITOR
            if (Application.isBatchMode) UnityEditor.EditorApplication.Exit(passed ? 0 : 1);
#else
            if (Application.isBatchMode) Application.Quit(passed ? 0 : 1);
#endif
        }
    }
}
