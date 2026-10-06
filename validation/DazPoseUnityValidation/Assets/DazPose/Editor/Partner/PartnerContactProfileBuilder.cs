using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DazPose.UnityValidation.Editor.Partner
{
    /// <summary>Measures evaluated partner cross-sections without changing scene or asset state.</summary>
    public static class PartnerContactProfileBuilder
    {
        public const int GlansMaterialSlot = 16;
        public const int ShaftMaterialSlot = 17;
        public const int ShellGlansMaterialSlot = 0;
        public const int ShellShaftMaterialSlot = 1;

        private const float PlaneEpsilon = 1e-6f;
        private const float PointDeduplicationEpsilon = 1e-5f;

        [Serializable]
        public sealed class SubmeshCount
        {
            public string renderer;
            public int materialSlot;
            public string materialName;
            public int uniqueVertexCount;
            public int indexCount;
        }

        [Serializable]
        public sealed class Section
        {
            public int index;
            public float arcLengthRoot;
            public float normalizedArcLength;
            public Vector3 centerRoot;
            public Vector3 centerWorld;
            public float centerlineErrorRoot;
            public float minimumSurfaceRadiusRoot;
            public float maximumSurfaceRadiusRoot;
            public float bodyMinimumRadiusRoot;
            public float bodyMaximumRadiusRoot;
            public float shellMinimumRadiusRoot;
            public float shellMaximumRadiusRoot;
            public int bodyGlansIntersectionCount;
            public int bodyShaftIntersectionCount;
            public int shellGlansIntersectionCount;
            public int shellShaftIntersectionCount;

            /// <summary>Maximum absolute surface-radius error from a chosen radius, using true section extrema.</summary>
            public float MaximumAbsoluteRadialDeviationFrom(float chosenRadiusRoot)
            {
                if (float.IsNaN(minimumSurfaceRadiusRoot) || float.IsNaN(maximumSurfaceRadiusRoot) ||
                    float.IsNaN(chosenRadiusRoot) || float.IsInfinity(chosenRadiusRoot)) return float.NaN;
                return Mathf.Max(Mathf.Abs(minimumSurfaceRadiusRoot - chosenRadiusRoot),
                    Mathf.Abs(maximumSurfaceRadiusRoot - chosenRadiusRoot));
            }
        }

        [Serializable]
        public sealed class Measurement
        {
            public string measurementMethod;
            public string[] limitations;
            public Vector3[] inputCenterlineRoot;
            public float centerlineLengthRoot;
            public float interiorStart01;
            public float interiorEnd01;
            public bool useOverallGuideTangent;
            public SubmeshCount[] submeshes;
            public Section[] sections;
            public string[] errors;
        }

        private sealed class Surface
        {
            public string renderer;
            public int slot;
            public string materialName;
            public Vector3[] rootVertices;
            public int[] triangleIndices;
        }

        private sealed class Contour
        {
            public readonly List<Vector3> points = new List<Vector3>();
            public readonly List<LineSegment> segments = new List<LineSegment>();
        }

        private readonly struct LineSegment
        {
            public readonly Vector3 a;
            public readonly Vector3 b;
            public LineSegment(Vector3 a, Vector3 b) { this.a = a; this.b = b; }
        }

        /// <summary>
        /// Bakes the evaluated renderers and intersects their triangles with equal-arc planes along
        /// the supplied partner-root-space centerline. Body slots 17 (shaft) and 16 (glans) are
        /// sampled; the tip slot contributes only where a plane actually intersects it. Shell slots
        /// 1 (shaft) and 0 (glans) are sampled. Other shell material regions are intentionally excluded.
        /// </summary>
        public static Measurement Measure(Transform partnerRoot, SkinnedMeshRenderer structuralBody,
            SkinnedMeshRenderer shell, Vector3[] centerlineRoot, int sectionCount,
            float interiorStart01 = 0.1f, float interiorEnd01 = 0.9f, bool useOverallGuideTangent = false)
        {
            var result = new Measurement
            {
                measurementMethod = "Baked triangle-plane intersections at equal arc-length guide stations; " +
                    (useOverallGuideTangent ? "overall endpoint-chord plane normal" : "piecewise guide-tangent plane normal") +
                    "; contour segments deduplicated in partner-root space.",
                limitations = new[]
                {
                    "Cross-sections are evaluated mesh triangle contours, not a continuous collision solver.",
                    "Reported section centers use the transverse contour bounds midpoint; maximum radii use deduplicated contour vertices and minimum radii use nearest points on intersected contour segments.",
                    "Stations with no selected triangle-plane intersections are reported as errors and have NaN metrics.",
                    "Lengths and radii are partner-root local units and assume the accepted unit-scale partner policy.",
                    "Shell texture alpha coverage is not rasterized; only enabled shell submesh geometry is measured."
                },
                inputCenterlineRoot = centerlineRoot != null ? (Vector3[])centerlineRoot.Clone() : Array.Empty<Vector3>(),
                interiorStart01 = interiorStart01,
                interiorEnd01 = interiorEnd01,
                useOverallGuideTangent = useOverallGuideTangent,
                errors = Array.Empty<string>(),
                submeshes = Array.Empty<SubmeshCount>(),
                sections = Array.Empty<Section>()
            };
            var errors = new List<string>();
            if (partnerRoot == null) errors.Add("Partner root is required.");
            if (structuralBody == null || structuralBody.sharedMesh == null) errors.Add("Structural body renderer and mesh are required.");
            if (shell == null || shell.sharedMesh == null) errors.Add("Shell renderer and mesh are required.");
            if (shell != null && (!shell.enabled || !shell.gameObject.activeInHierarchy)) errors.Add("Shell renderer must be enabled and active to measure its visible geometry.");
            if (centerlineRoot == null || centerlineRoot.Length < 2) errors.Add("Centerline must contain at least two root-space points.");
            else if (centerlineRoot.Any(p => !Finite(p))) errors.Add("Centerline contains a non-finite point.");
            if (sectionCount < 1) errors.Add("Section count must be positive.");
            if (!(interiorStart01 >= 0f && interiorEnd01 <= 1f && interiorEnd01 > interiorStart01))
                errors.Add("Interior span must satisfy 0 <= start < end <= 1.");
            if (structuralBody != null && structuralBody.sharedMesh != null && structuralBody.sharedMesh.subMeshCount <= ShaftMaterialSlot)
                errors.Add("Structural body mesh does not contain accepted material slots 16 and 17.");
            if (shell != null && shell.sharedMesh != null && shell.sharedMesh.subMeshCount <= ShellShaftMaterialSlot)
                errors.Add("Shell mesh does not contain accepted shaft/glans slots 0 and 1.");
            if (errors.Count > 0) { result.errors = errors.ToArray(); return result; }

            float[] cumulative = BuildCumulativeLengths(centerlineRoot);
            result.centerlineLengthRoot = cumulative[cumulative.Length - 1];
            if (result.centerlineLengthRoot <= 1e-6f)
            {
                errors.Add("Centerline length is zero or too small.");
                result.errors = errors.ToArray();
                return result;
            }
            Vector3 overallTangent = (centerlineRoot[centerlineRoot.Length - 1] - centerlineRoot[0]).normalized;
            if (useOverallGuideTangent && overallTangent.sqrMagnitude <= 1e-10f)
            {
                errors.Add("Overall endpoint-chord tangent is undefined for this guide.");
                result.errors = errors.ToArray();
                return result;
            }

            var surfaces = new List<Surface>();
            var temporaryMeshes = new List<Mesh>();
            try
            {
                AddSurface(structuralBody, partnerRoot, GlansMaterialSlot, "body", surfaces, temporaryMeshes, errors);
                AddSurface(structuralBody, partnerRoot, ShaftMaterialSlot, "body", surfaces, temporaryMeshes, errors);
                AddSurface(shell, partnerRoot, ShellGlansMaterialSlot, "shell", surfaces, temporaryMeshes, errors);
                AddSurface(shell, partnerRoot, ShellShaftMaterialSlot, "shell", surfaces, temporaryMeshes, errors);
                result.submeshes = surfaces.Select(s => DescribeSubmesh(s)).ToArray();

                float startArc = result.centerlineLengthRoot * interiorStart01;
                float endArc = result.centerlineLengthRoot * interiorEnd01;
                var sections = new List<Section>(sectionCount);
                for (int index = 0; index < sectionCount; index++)
                {
                    float normalized = sectionCount == 1 ? (interiorStart01 + interiorEnd01) * .5f :
                        Mathf.Lerp(interiorStart01, interiorEnd01, index / (float)(sectionCount - 1));
                    float arc = normalized * result.centerlineLengthRoot;
                    var frame = Evaluate(centerlineRoot, cumulative, arc);
                    Vector3 tangent = useOverallGuideTangent ? overallTangent : frame.tangent.normalized;
                    var u = Vector3.Cross(tangent, Mathf.Abs(Vector3.Dot(tangent, Vector3.up)) > .95f ? Vector3.right : Vector3.up).normalized;
                    var v = Vector3.Cross(tangent, u).normalized;
                    var bodyGlans = new Contour();
                    var bodyShaft = new Contour();
                    var shellGlans = new Contour();
                    var shellShaft = new Contour();
                    foreach (var surface in surfaces)
                    {
                        var target = surface.renderer == "body"
                            ? surface.slot == GlansMaterialSlot ? bodyGlans : bodyShaft
                            : surface.slot == ShellGlansMaterialSlot ? shellGlans : shellShaft;
                        IntersectPlane(surface, frame.point, tangent, target);
                    }
                    var allContours = new[] { bodyGlans, bodyShaft, shellGlans, shellShaft };
                    var contour = allContours.SelectMany(c => c.points).ToArray();
                    if (contour.Length == 0)
                    {
                        errors.Add("No selected surface triangles intersect guide plane at section " + index + " (arc " + arc.ToString("F5") + ").");
                        sections.Add(new Section { index = index, arcLengthRoot = arc, normalizedArcLength = normalized,
                            centerRoot = frame.point, centerWorld = partnerRoot.TransformPoint(frame.point),
                            centerlineErrorRoot = float.NaN, minimumSurfaceRadiusRoot = float.NaN,
                            maximumSurfaceRadiusRoot = float.NaN, bodyMinimumRadiusRoot = float.NaN,
                            bodyMaximumRadiusRoot = float.NaN, shellMinimumRadiusRoot = float.NaN,
                            shellMaximumRadiusRoot = float.NaN });
                        continue;
                    }

                    float minU = float.PositiveInfinity, maxU = float.NegativeInfinity;
                    float minV = float.PositiveInfinity, maxV = float.NegativeInfinity;
                    foreach (var point in contour)
                    {
                        Vector3 delta = point - frame.point;
                        float pu = Vector3.Dot(delta, u), pv = Vector3.Dot(delta, v);
                        minU = Mathf.Min(minU, pu); maxU = Mathf.Max(maxU, pu);
                        minV = Mathf.Min(minV, pv); maxV = Mathf.Max(maxV, pv);
                    }
                    float centerU = (minU + maxU) * .5f, centerV = (minV + maxV) * .5f;
                    Vector3 center = frame.point + u * centerU + v * centerV;
                    float minRadius = MinimumRadius(center, allContours.SelectMany(c => c.segments), contour);
                    float maxRadius = MaximumRadius(center, u, v, contour);
                    float bodyMin = MinimumRadius(center, bodyGlans.segments.Concat(bodyShaft.segments),
                        bodyGlans.points.Concat(bodyShaft.points));
                    float bodyMax = MaximumRadius(center, u, v, bodyGlans.points.Concat(bodyShaft.points));
                    float shellMin = MinimumRadius(center, shellGlans.segments.Concat(shellShaft.segments),
                        shellGlans.points.Concat(shellShaft.points));
                    float shellMax = MaximumRadius(center, u, v, shellGlans.points.Concat(shellShaft.points));
                    sections.Add(new Section
                    {
                        index = index, arcLengthRoot = arc, normalizedArcLength = normalized,
                        centerRoot = center, centerWorld = partnerRoot.TransformPoint(center),
                        centerlineErrorRoot = Vector3.Distance(center, frame.point),
                        minimumSurfaceRadiusRoot = minRadius, maximumSurfaceRadiusRoot = maxRadius,
                        bodyMinimumRadiusRoot = bodyMin, bodyMaximumRadiusRoot = bodyMax,
                        shellMinimumRadiusRoot = shellMin, shellMaximumRadiusRoot = shellMax,
                        bodyGlansIntersectionCount = bodyGlans.points.Count, bodyShaftIntersectionCount = bodyShaft.points.Count,
                        shellGlansIntersectionCount = shellGlans.points.Count, shellShaftIntersectionCount = shellShaft.points.Count
                    });
                }
                result.sections = sections.ToArray();
            }
            finally
            {
                foreach (var mesh in temporaryMeshes) UnityEngine.Object.DestroyImmediate(mesh);
            }
            result.errors = errors.ToArray();
            return result;
        }

        private static void AddSurface(SkinnedMeshRenderer renderer, Transform root, int slot, string label,
            List<Surface> surfaces, List<Mesh> temporaryMeshes, List<string> errors)
        {
            Mesh source = renderer.sharedMesh;
            if (source.GetTopology(slot) != MeshTopology.Triangles)
            {
                errors.Add(label + " submesh " + slot + " is not triangle geometry.");
                return;
            }
            Mesh baked = new Mesh { name = "TemporaryPartnerContactProfileBake" };
            temporaryMeshes.Add(baked);
            renderer.BakeMesh(baked);
            var rootVertices = new Vector3[baked.vertexCount];
            Vector3[] bakedVertices = baked.vertices;
            for (int i = 0; i < bakedVertices.Length; i++)
                rootVertices[i] = root.InverseTransformPoint(renderer.transform.TransformPoint(bakedVertices[i]));
            Material[] materials = renderer.sharedMaterials;
            string materialName = slot < materials.Length && materials[slot] != null ? materials[slot].name : string.Empty;
            int[] triangleIndices = source.GetIndices(slot);
            if (triangleIndices.Any(index => index < 0 || index >= rootVertices.Length))
            {
                errors.Add(label + " submesh " + slot + " contains an out-of-range vertex index.");
                return;
            }
            surfaces.Add(new Surface { renderer = label, slot = slot, materialName = materialName,
                rootVertices = rootVertices, triangleIndices = triangleIndices });
        }

        private static SubmeshCount DescribeSubmesh(Surface surface)
        {
            return new SubmeshCount { renderer = surface.renderer, materialSlot = surface.slot, materialName = surface.materialName,
                uniqueVertexCount = surface.triangleIndices.Distinct().Count(), indexCount = surface.triangleIndices.Length };
        }

        private static void IntersectPlane(Surface surface, Vector3 origin, Vector3 normal, Contour contour)
        {
            int[] indices = surface.triangleIndices;
            for (int i = 0; i + 2 < indices.Length; i += 3)
            {
                Vector3 a = surface.rootVertices[indices[i]];
                Vector3 b = surface.rootVertices[indices[i + 1]];
                Vector3 c = surface.rootVertices[indices[i + 2]];
                float da = Vector3.Dot(a - origin, normal);
                float db = Vector3.Dot(b - origin, normal);
                float dc = Vector3.Dot(c - origin, normal);
                if (Mathf.Abs(da) <= PlaneEpsilon && Mathf.Abs(db) <= PlaneEpsilon && Mathf.Abs(dc) <= PlaneEpsilon)
                    continue;
                var triangleIntersections = new List<Vector3>(3);
                AddIntersection(a, b, da, db, triangleIntersections);
                AddIntersection(b, c, db, dc, triangleIntersections);
                AddIntersection(c, a, dc, da, triangleIntersections);
                Deduplicate(triangleIntersections);
                contour.points.AddRange(triangleIntersections);
                if (triangleIntersections.Count == 1)
                    contour.segments.Add(new LineSegment(triangleIntersections[0], triangleIntersections[0]));
                else if (triangleIntersections.Count == 2)
                    contour.segments.Add(new LineSegment(triangleIntersections[0], triangleIntersections[1]));
            }
            Deduplicate(contour.points);
        }

        private static void AddIntersection(Vector3 a, Vector3 b, float da, float db, List<Vector3> points)
        {
            bool aOn = Mathf.Abs(da) <= PlaneEpsilon;
            bool bOn = Mathf.Abs(db) <= PlaneEpsilon;
            if (aOn) points.Add(a);
            if (bOn) points.Add(b);
            if (!aOn && !bOn && ((da < 0f && db > 0f) || (da > 0f && db < 0f)))
                points.Add(Vector3.LerpUnclamped(a, b, da / (da - db)));
        }

        private static void Deduplicate(List<Vector3> points)
        {
            float epsilonSquared = PointDeduplicationEpsilon * PointDeduplicationEpsilon;
            var buckets = new Dictionary<Vector3Int, List<Vector3>>();
            var unique = new List<Vector3>();
            foreach (var point in points)
            {
                var cell = Cell(point);
                bool duplicate = false;
                for (int x = -1; x <= 1 && !duplicate; x++)
                    for (int y = -1; y <= 1 && !duplicate; y++)
                        for (int z = -1; z <= 1 && !duplicate; z++)
                        {
                            if (!buckets.TryGetValue(cell + new Vector3Int(x, y, z), out var candidates)) continue;
                            duplicate = candidates.Any(existing => (point - existing).sqrMagnitude <= epsilonSquared);
                        }
                if (duplicate) continue;
                unique.Add(point);
                if (!buckets.TryGetValue(cell, out var bucket)) buckets[cell] = bucket = new List<Vector3>();
                bucket.Add(point);
            }
            points.Clear();
            points.AddRange(unique);
        }

        private static float MinimumRadius(Vector3 center, IEnumerable<LineSegment> segments, IEnumerable<Vector3> points)
        {
            float minimum = float.PositiveInfinity;
            bool foundSegment = false;
            foreach (var segment in segments)
            {
                Vector3 delta = segment.b - segment.a;
                float t = delta.sqrMagnitude > 1e-12f
                    ? Mathf.Clamp01(Vector3.Dot(center - segment.a, delta) / delta.sqrMagnitude) : 0f;
                minimum = Mathf.Min(minimum, Vector3.Distance(center, segment.a + delta * t));
                foundSegment = true;
            }
            if (foundSegment) return minimum;
            foreach (var point in points) minimum = Mathf.Min(minimum, Vector3.Distance(center, point));
            return float.IsPositiveInfinity(minimum) ? float.NaN : minimum;
        }

        private static float MaximumRadius(Vector3 center, Vector3 u, Vector3 v, IEnumerable<Vector3> points)
        {
            float maximum = 0f;
            bool found = false;
            foreach (var point in points)
            {
                Vector3 delta = point - center;
                float radius = Mathf.Sqrt(Mathf.Pow(Vector3.Dot(delta, u), 2f) + Mathf.Pow(Vector3.Dot(delta, v), 2f));
                maximum = Mathf.Max(maximum, radius);
                found = true;
            }
            return found ? maximum : float.NaN;
        }

        private static Vector3Int Cell(Vector3 point)
        {
            float inverse = 1f / PointDeduplicationEpsilon;
            return new Vector3Int(Mathf.FloorToInt(point.x * inverse), Mathf.FloorToInt(point.y * inverse),
                Mathf.FloorToInt(point.z * inverse));
        }

        private static bool Finite(Vector3 point) =>
            !(float.IsNaN(point.x) || float.IsNaN(point.y) || float.IsNaN(point.z) ||
              float.IsInfinity(point.x) || float.IsInfinity(point.y) || float.IsInfinity(point.z));

        private static float[] BuildCumulativeLengths(Vector3[] line)
        {
            var cumulative = new float[line.Length];
            for (int i = 1; i < line.Length; i++) cumulative[i] = cumulative[i - 1] + Vector3.Distance(line[i - 1], line[i]);
            return cumulative;
        }

        private static (Vector3 point, Vector3 tangent) Evaluate(Vector3[] line, float[] cumulative, float arc)
        {
            int segment = cumulative.Length - 2;
            for (int i = 0; i < cumulative.Length - 1; i++)
                if (arc <= cumulative[i + 1]) { segment = i; break; }
            float length = cumulative[segment + 1] - cumulative[segment];
            float t = length > 1e-8f ? Mathf.Clamp01((arc - cumulative[segment]) / length) : 0f;
            Vector3 delta = line[segment + 1] - line[segment];
            return (Vector3.Lerp(line[segment], line[segment + 1], t), delta.sqrMagnitude > 1e-12f ? delta.normalized : Vector3.forward);
        }
    }
}
