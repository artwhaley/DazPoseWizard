using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DazPose.Performer;
using UnityEngine;

namespace DazPose.Validation
{
    /// <summary>Saved-fixture witness for the reported second-outfit toe residual.</summary>
    public sealed class RemediationToeRunner : MonoBehaviour
    {
        [Serializable] private sealed class Assertion
        {
            public string name;
            public string expected;
            public string actual;
            public bool passed;
        }

        [Serializable] private sealed class BoneSample
        {
            public string name;
            public Vector3 localPosition;
            public Quaternion localRotation;
        }

        [Serializable] private sealed class Observation
        {
            public string station;
            public int visibleMask;
            public int populatedMask;
            public bool footwearPoseActive;
            public string effectiveFootwear;
            public bool driverFootwearActive;
            public float standingLift;
            public float footShrink;
            public int meshVertexCount;
            public int meshBlendShapeCount;
            public string meshHash;
            public float capturedHeelWeight;
            public float wardrobeFootFitWeight;
            public int toeRegionVertexCount;
            public float toeRegionMaxDifferenceFromBarefoot;
            public List<BoneSample> bones = new List<BoneSample>();
            [NonSerialized] public Mesh mesh;
            [NonSerialized] public Vector3[] bakedVertices;
            [NonSerialized] public int[] toeRegionIndices;
        }

        [Serializable] private sealed class Experiment
        {
            public string name;
            public bool passed;
            public string detail;
            public float maxToeDifference;
        }

        [Serializable] private sealed class Report
        {
            public string ticket = "R02";
            public string startedUtc;
            public string completedUtc;
            public string sourceHash;
            public string[] inputAssetHashes;
            public bool knownProductFailureObserved;
            public bool complete;
            public bool passed;
            public string exception;
            public List<Assertion> assertions = new List<Assertion>();
            public List<Observation> observations = new List<Observation>();
            public List<Experiment> attribution = new List<Experiment>();
        }

        [SerializeField] private SuccubusPerformer actor;
        [SerializeField] private string secondOutfitId = "second-outfit";
        [SerializeField] private string reportPath;
        [SerializeField] private string sourceHash;
        [SerializeField] private string inputHash;
        private bool finished;
        private Report report;

#if UNITY_EDITOR
        public void Configure(SuccubusPerformer performer, string output, string source, string input)
        {
            actor = performer;
            reportPath = output;
            sourceHash = source;
            inputHash = input;
        }
#endif

        private async void Start()
        {
            report = new Report
            {
                startedUtc = DateTime.UtcNow.ToString("O"),
                sourceHash = sourceHash ?? string.Empty,
                inputAssetHashes = new[] { inputHash ?? string.Empty }
            };
            try
            {
                await RunWitness();
                report.knownProductFailureObserved = HasKnownResidual();
                Check("knownProductFailureObserved", report.knownProductFailureObserved,
                    "Mask 1 after removing Layer 1 must retain a measured toe residual while footwear flags are inactive.");
                Check("attributionRecorded", report.attribution.Count >= 2,
                    "Transform and fitted-mesh attribution experiments were recorded.");
                Finish(report.assertions.All(item => item.passed) && report.knownProductFailureObserved, null);
            }
            catch (Exception exception)
            {
                Finish(false, exception.ToString());
            }
        }

        private async Awaitable RunWitness()
        {
            for (int frame = 0; frame < 300 && (actor == null || !actor.IsRuntimeReady); ++frame)
                await Awaitable.NextFrameAsync();
            Check("actorRuntimeReady", actor != null && actor.IsRuntimeReady, "Saved actor did not become runtime ready.");
            var wardrobe = actor.GetComponent<PerformerWardrobe>();
            Check("wardrobeRuntimeReady", wardrobe != null && wardrobe.CurrentWardrobe != null &&
                wardrobe.Catalog != null, "Saved actor has no usable wardrobe binding.");

            WardrobePreset secondOutfit = wardrobe.Catalog.Presets.FirstOrDefault(item => item != null && item.PresetId == secondOutfitId);
            Check("secondOutfitResolvesFromSavedCandidateCatalog", secondOutfit != null,
                "The saved candidate catalog must contain " + secondOutfitId + ".");
            Applied(await actor.OutfitAsync(secondOutfit), "dress second outfit");
            await StableFrames(30);
            Capture("mask3-dressed", wardrobe);

            Applied(await actor.TryRemoveLayerAsync(), "remove Layer 1");
            await StableFrames(1);
            Capture("mask1-after-remove-1-frame", wardrobe);
            await StableFrames(29);
            Capture("mask1-after-remove-30-frames", wardrobe);

            Applied(await actor.TryRemoveLayerAsync(), "remove Base");
            await StableFrames(30);
            Capture("mask0-barefoot", wardrobe);

            Applied(await actor.TryAddLayerAsync(), "add Base");
            await StableFrames(30);
            Capture("mask1-after-add", wardrobe);

            Applied(await actor.TryAddLayerAsync(), "add Layer 1");
            await StableFrames(30);
            Capture("mask3-restored", wardrobe);

            Observation mask1 = report.observations.Last(item => item.station == "mask1-after-remove-30-frames");
            Observation barefoot = report.observations.Single(item => item.station == "mask0-barefoot");
            Observation restored = report.observations.Single(item => item.station == "mask1-after-add");
            CompareToBarefoot(mask1, barefoot);
            CompareToBarefoot(restored, barefoot);
            Applied(await actor.TryRemoveLayerAsync(), "return to mask1 for attribution");
            await StableFrames(1);
            await RunAttribution(mask1, barefoot);
            Applied(await actor.TryAddLayerAsync(), "restore Layer 1 after attribution");

            Check("mask1LayerAssignment", mask1.visibleMask == 1, "Expected Base clothing to remain at mask 1.");
            Check("mask1FootwearFlagsOff", !mask1.driverFootwearActive && !mask1.footwearPoseActive,
                "Layer 1 removal must disable footwear support and bent-foot state in the saved fit.");
            Check("mask3RestoresFootwear", restored.visibleMask == 1 || restored.visibleMask == 3,
                "Adding Base after naked state must restore a valid selected outfit state.");
        }

        private async Awaitable RunAttribution(Observation mask1, Observation barefoot)
        {
            var body = actor.GetComponent<PerformerWardrobe>().Body;
            Transform[] toeBones = body.bones.Where(b => b != null && IsToe(b.name)).ToArray();
            var savedPosition = toeBones.Select(b => b.localPosition).ToArray();
            var savedRotation = toeBones.Select(b => b.localRotation).ToArray();
            foreach (var sample in barefoot.bones)
            {
                Transform bone = toeBones.FirstOrDefault(item => item.name == sample.name);
                if (bone == null) continue;
                bone.localPosition = sample.localPosition;
                bone.localRotation = sample.localRotation;
            }
            Vector3[] transformRestored = CaptureBaked(body, out _);
            float transformDifference = ToeDifference(transformRestored, barefoot.bakedVertices, mask1.toeRegionIndices);
            for (int i = 0; i < toeBones.Length; ++i)
            {
                toeBones[i].localPosition = savedPosition[i];
                toeBones[i].localRotation = savedRotation[i];
            }
            report.attribution.Add(new Experiment
            {
                name = "restore-canonical-toe-transforms-with-mask1-mesh",
                passed = true,
                maxToeDifference = transformDifference,
                detail = "Temporary transform restoration was reverted after capture; residual after restoration = " + transformDifference.ToString("G6") + " m."
            });

            Mesh mask1Mesh = body.sharedMesh;
            body.sharedMesh = barefoot.mesh;
            Vector3[] meshRestored = CaptureBaked(body, out _);
            float meshDifference = ToeDifference(meshRestored, barefoot.bakedVertices, mask1.toeRegionIndices);
            body.sharedMesh = mask1Mesh;
            report.attribution.Add(new Experiment
            {
                name = "canonical-foot-mesh-with-mask1-transforms",
                passed = true,
                maxToeDifference = meshDifference,
                detail = "Temporary canonical mesh substitution was reverted after capture; residual after substitution = " + meshDifference.ToString("G6") + " m."
            });
            await StableFrames(1);
        }

        private void Capture(string station, PerformerWardrobe wardrobe)
        {
            var state = wardrobe.CurrentWardrobe;
            var body = wardrobe.Body;
            Check("stateCaptured_" + station, state != null && body != null && body.sharedMesh != null,
                "Station has committed wardrobe state and body mesh.");
            var driver = actor.GetComponent<FootwearPoseDriver>();
            Mesh bakedMesh;
            Vector3[] vertices = CaptureBaked(body, out bakedMesh);
            var sample = new Observation
            {
                station = station,
                visibleMask = state.VisibleMask,
                populatedMask = state.PopulatedMask,
                footwearPoseActive = state.CurrentFootwearPoseActive,
                effectiveFootwear = state.EffectiveFootwearId,
                driverFootwearActive = driver != null && driver.IsFootwearActive,
                standingLift = driver != null ? driver.StandingLift : 0f,
                footShrink = driver != null ? driver.FootShrink : 0f,
                meshVertexCount = body.sharedMesh.vertexCount,
                meshBlendShapeCount = body.sharedMesh.blendShapeCount,
                meshHash = MeshHash(body.sharedMesh),
                capturedHeelWeight = BlendWeight(body, "CapturedHeelFootPose"),
                wardrobeFootFitWeight = BlendWeight(body, "WardrobeFootFit"),
                mesh = body.sharedMesh,
                bakedVertices = vertices,
                toeRegionIndices = ToeRegion(body)
            };
            foreach (Transform bone in body.bones.Where(b => b != null && IsToe(b.name)).OrderBy(b => b.name))
                sample.bones.Add(new BoneSample { name = bone.name, localPosition = bone.localPosition, localRotation = bone.localRotation });
            sample.toeRegionVertexCount = sample.toeRegionIndices.Length;
            report.observations.Add(sample);
        }

        private void CompareToBarefoot(Observation sample, Observation barefoot)
        {
            if (sample.bakedVertices == null || barefoot.bakedVertices == null || sample.bakedVertices.Length != barefoot.bakedVertices.Length)
            {
                sample.toeRegionMaxDifferenceFromBarefoot = float.PositiveInfinity;
                return;
            }
            sample.toeRegionMaxDifferenceFromBarefoot = ToeDifference(sample.bakedVertices, barefoot.bakedVertices,
                sample.toeRegionIndices.Intersect(barefoot.toeRegionIndices).ToArray());
        }

        private bool HasKnownResidual()
        {
            var station = report.observations.FirstOrDefault(item => item.station == "mask1-after-remove-30-frames");
            var barefoot = report.observations.FirstOrDefault(item => item.station == "mask0-barefoot");
            return station != null && barefoot != null && station.toeRegionMaxDifferenceFromBarefoot > 0.000001f &&
                !station.driverFootwearActive && !station.footwearPoseActive;
        }

        private static Vector3[] CaptureBaked(SkinnedMeshRenderer body, out Mesh mesh)
        {
            mesh = new Mesh { name = "Remediation Toe Capture" };
            body.BakeMesh(mesh, true);
            Vector3[] result = mesh.vertices;
            return result;
        }

        private static int[] ToeRegion(SkinnedMeshRenderer body)
        {
            var names = body.bones.Select(b => b != null ? b.name : string.Empty).ToArray();
            var indices = new List<int>();
            BoneWeight[] weights = body.sharedMesh.boneWeights;
            for (int i = 0; i < weights.Length; ++i)
            {
                BoneWeight weight = weights[i];
                float sum = WeightForBone(weight.boneIndex0, weight.weight0, names) +
                    WeightForBone(weight.boneIndex1, weight.weight1, names) +
                    WeightForBone(weight.boneIndex2, weight.weight2, names) +
                    WeightForBone(weight.boneIndex3, weight.weight3, names);
                if (sum >= .5f) indices.Add(i);
            }
            return indices.ToArray();
        }

        private static float WeightForBone(int index, float weight, string[] names)
            => index >= 0 && index < names.Length && IsToe(names[index]) ? weight : 0f;

        private static bool IsToe(string name)
            => !string.IsNullOrEmpty(name) && (name.Contains("Toe", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("Metatarsals", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("Foot", StringComparison.OrdinalIgnoreCase));

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

        private static async Awaitable StableFrames(int count)
        {
            for (int i = 0; i < count; ++i) await Awaitable.NextFrameAsync();
        }

        private void Applied(WardrobeChangeResult result, string phase)
        {
            bool passed = result.Status == WardrobeChangeStatus.Applied || result.Status == WardrobeChangeStatus.AlreadyEquipped;
            Check(phase, passed, result.Status + ": " + result.Message);
        }

        private void Check(string name, bool passed, string detail)
        {
            report.assertions.Add(new Assertion { name = name, expected = "pass", actual = detail, passed = passed });
            if (!passed) throw new InvalidOperationException(name + ": " + detail);
        }

        private void Finish(bool passed, string exception)
        {
            if (finished) return;
            finished = true;
            report.exception = exception;
            report.passed = passed;
            report.complete = true;
            report.completedUtc = DateTime.UtcNow.ToString("O");
            string output = string.IsNullOrWhiteSpace(reportPath) ? Path.Combine("TestOutput", "remediation", "R02.json") : reportPath;
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
