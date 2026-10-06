using System;
using System.Collections.Generic;
using DazPose.Performer.HandGrip;
using DazPose.UnityValidation.Partner;
using UnityEngine;

namespace DazPose.UnityValidation
{
    /// <summary>
    /// Bounded sampled witness comparing live solver probes with baked body and shell
    /// triangles. This reports sampled distances only; it is not continuous collision proof.
    /// </summary>
    public static class Day4ContactSurfaceWitness
    {
        [Serializable]
        public sealed class Metrics
        {
            public bool valid;
            public string error;
            public string interpretation = "Discrete solver-probe samples against baked body/shell triangles; no continuous collision or rasterized shell-alpha claim.";
            public int probeCount;
            public int nearCount;
            public int penetratingCount;
            public int surfaceNearCount;
            public int surfacePenetratingCount;
            public int surfaceIntersectionCount;
            public int localSignDisagreementCount;
            public float minimumClearanceMeters;
            public float maximumClearanceMeters;
            public int failedProbeIndex = -1;
            public ProbeMetric[] probes = Array.Empty<ProbeMetric>();
        }

        [Serializable]
        public sealed class ProbeMetric
        {
            public int index;
            public Vector3 worldPosition;
            public float radiusMeters;
            public float bodySignedClearanceMeters;
            public float shellSignedClearanceMeters;
            public float combinedSignedClearanceMeters;
            public float triangleSurfaceClearanceMeters;
            public Vector3 bodyClosestPoint;
            public Vector3 shellClosestPoint;
            public int bodySubmesh;
            public int shellSubmesh;
            public HandGripProbeState solverState;
        }

        private readonly struct Triangle
        {
            public readonly Vector3 A, B, C;
            public readonly Vector3 NormalA, NormalB, NormalC, FaceNormal;
            public readonly int Submesh;

            public Triangle(Vector3 a, Vector3 b, Vector3 c, Vector3 normalA,
                Vector3 normalB, Vector3 normalC, int submesh)
            {
                A = a; B = b; C = c;
                NormalA = normalA; NormalB = normalB; NormalC = normalC;
                FaceNormal = Vector3.Cross(b - a, c - a).normalized;
                Submesh = submesh;
            }
        }

        private sealed class BakedSurface
        {
            public Mesh Mesh;
            public Triangle[] Triangles;
        }

        /// <summary>Samples every active hand probe against body submeshes 17/16 and shell 1/0.</summary>
        public static Metrics Sample(PartnerBody body, PerformerHandGripController grip)
        {
            var result = new Metrics
            {
                minimumClearanceMeters = float.PositiveInfinity,
                maximumClearanceMeters = float.NegativeInfinity
            };
            BakedSurface bodySurface = null;
            BakedSurface shellSurface = null;
            try
            {
                if (body == null || !body.IsReady)
                    throw new InvalidOperationException(body != null ? body.ValidationError : "PartnerBody is missing.");
                if (grip == null || grip.Profile == null)
                    throw new InvalidOperationException("An active hand-grip controller and profile are required.");
                if (body.StructuralBody == null || body.ShellRenderer == null)
                    throw new InvalidOperationException("Both structural-body and visible shell renderers are required.");
                if (!body.StructuralBody.enabled || !body.StructuralBody.gameObject.activeInHierarchy
                    || !body.ShellRenderer.enabled || !body.ShellRenderer.gameObject.activeInHierarchy)
                    throw new InvalidOperationException("Structural body and shell must both be active and visible for the witness.");

                bodySurface = BakeSelected(body.StructuralBody, 17, 16, "structural body");
                shellSurface = BakeSelected(body.ShellRenderer, 1, 0, "shell");
                result.probeCount = CountProfileProbes(grip.Profile);
                if (result.probeCount <= 0)
                    throw new InvalidOperationException("Grip profile contains no configured probes.");

                var samples = new List<ProbeMetric>(result.probeCount);
                for (int index = 0; index < result.probeCount; index++)
                {
                    if (!grip.TryGetProbe(index, out Vector3 point, out float radius, out HandGripProbeState state))
                        throw new InvalidOperationException("Solver probe " + index + " is unavailable.");
                    if (!GripFrame.IsFinite(point) || !GripFrame.IsFinite(radius) || radius <= 0f)
                        throw new InvalidOperationException("Solver probe " + index + " has non-finite position or non-positive radius.");
                    if (state != HandGripProbeState.Clear && state != HandGripProbeState.NearContact
                        && state != HandGripProbeState.Penetrating)
                        throw new InvalidOperationException("Solver probe " + index + " has an unknown solver state.");

                    SurfaceDistance bodyDistance = NearestSignedDistance(bodySurface.Triangles, point);
                    SurfaceDistance shellDistance = NearestSignedDistance(shellSurface.Triangles, point);
                    float bodyClearance = bodyDistance.SignedDistance - radius;
                    float shellClearance = shellDistance.SignedDistance - radius;
                    // These material patches are open triangle surfaces, not two closed
                    // volumes. The combined local-sign field uses the nearest triangle
                    // across both patches, matching the defined prepared-surface query.
                    // Also independently test sphere/triangle separation on EACH patch.
                    float combined = Mathf.Abs(bodyDistance.SignedDistance) <= Mathf.Abs(shellDistance.SignedDistance)
                        ? bodyClearance : shellClearance;
                    float surfaceClearance = Mathf.Min(Mathf.Abs(bodyDistance.SignedDistance),
                        Mathf.Abs(shellDistance.SignedDistance)) - radius;
                    if ((bodyDistance.SignedDistance < 0f) != (shellDistance.SignedDistance < 0f))
                        result.localSignDisagreementCount++;
                    if (!GripFrame.IsFinite(bodyClearance) || !GripFrame.IsFinite(shellClearance)
                        || !GripFrame.IsFinite(combined))
                        throw new InvalidOperationException("Surface distance for probe " + index + " is non-finite.");

                    samples.Add(new ProbeMetric
                    {
                        index = index,
                        worldPosition = point,
                        radiusMeters = radius,
                        bodySignedClearanceMeters = bodyClearance,
                        shellSignedClearanceMeters = shellClearance,
                        combinedSignedClearanceMeters = combined,
                        triangleSurfaceClearanceMeters = surfaceClearance,
                        bodyClosestPoint = bodyDistance.ClosestPoint,
                        shellClosestPoint = shellDistance.ClosestPoint,
                        bodySubmesh = bodyDistance.Submesh,
                        shellSubmesh = shellDistance.Submesh,
                        solverState = state
                    });
                    if (state == HandGripProbeState.NearContact) result.nearCount++;
                    if (state == HandGripProbeState.Penetrating) result.penetratingCount++;
                    if (combined >= -.0001f && combined <= grip.Profile.NearContactDistance) result.surfaceNearCount++;
                    if (combined < -.0001f) result.surfacePenetratingCount++;
                    if (surfaceClearance < -.0001f) result.surfaceIntersectionCount++;
                    result.minimumClearanceMeters = Mathf.Min(result.minimumClearanceMeters, combined);
                    result.maximumClearanceMeters = Mathf.Max(result.maximumClearanceMeters, combined);
                }

                if (samples.Count != result.probeCount)
                    throw new InvalidOperationException("Probe witness count did not match the configured solver probe count.");
                result.probes = samples.ToArray();
                result.valid = true;
                return result;
            }
            catch (Exception error)
            {
                result.valid = false;
                result.error = error.Message;
                result.failedProbeIndex = FindProbeIndex(error.Message);
                result.probes = Array.Empty<ProbeMetric>();
                result.minimumClearanceMeters = float.NaN;
                result.maximumClearanceMeters = float.NaN;
                return result;
            }
            finally
            {
                DestroyBaked(bodySurface);
                DestroyBaked(shellSurface);
            }
        }

        private readonly struct SurfaceDistance
        {
            public readonly float SignedDistance;
            public readonly Vector3 ClosestPoint;
            public readonly int Submesh;
            public SurfaceDistance(float distance, Vector3 point, int submesh)
            { SignedDistance = distance; ClosestPoint = point; Submesh = submesh; }
        }

        private static BakedSurface BakeSelected(SkinnedMeshRenderer renderer,
            int firstSubmesh, int secondSubmesh, string label)
        {
            var baked = new Mesh { name = "Day4 " + label + " contact witness", hideFlags = HideFlags.DontSave };
            try
            {
                renderer.BakeMesh(baked);
                Vector3[] vertices = baked.vertices;
                Vector3[] normals = baked.normals;
                if (vertices == null || vertices.Length == 0)
                    throw new InvalidOperationException("BakeMesh produced no vertices for " + label + ".");
                if (baked.subMeshCount <= Mathf.Max(firstSubmesh, secondSubmesh))
                    throw new InvalidOperationException(label + " is missing required witness submeshes " + firstSubmesh + " and " + secondSubmesh + ".");
                if (baked.GetTopology(firstSubmesh) != MeshTopology.Triangles
                    || baked.GetTopology(secondSubmesh) != MeshTopology.Triangles)
                    throw new InvalidOperationException(label + " witness submeshes must use triangle topology.");

                var triangles = new List<Triangle>();
                AppendTriangles(baked, vertices, normals, renderer.transform, firstSubmesh, triangles, label);
                AppendTriangles(baked, vertices, normals, renderer.transform, secondSubmesh, triangles, label);
                if (triangles.Count == 0)
                    throw new InvalidOperationException(label + " witness submeshes contain no triangles.");
                return new BakedSurface { Mesh = baked, Triangles = triangles.ToArray() };
            }
            catch
            {
                DestroyMesh(baked);
                throw;
            }
        }

        private static void AppendTriangles(Mesh mesh, Vector3[] vertices, Vector3[] normals,
            Transform rendererTransform, int submesh, List<Triangle> destination, string label)
        {
            int[] indices = mesh.GetTriangles(submesh);
            if (indices == null || indices.Length == 0 || indices.Length % 3 != 0)
                throw new InvalidOperationException(label + " submesh " + submesh + " has no complete triangles.");
            Matrix4x4 normalMatrix = rendererTransform.worldToLocalMatrix.transpose;
            for (int i = 0; i < indices.Length; i += 3)
            {
                int ia = indices[i], ib = indices[i + 1], ic = indices[i + 2];
                if (ia < 0 || ib < 0 || ic < 0 || ia >= vertices.Length || ib >= vertices.Length || ic >= vertices.Length)
                    throw new InvalidOperationException(label + " submesh " + submesh + " contains an out-of-range vertex index.");
                Vector3 a = rendererTransform.TransformPoint(vertices[ia]);
                Vector3 b = rendererTransform.TransformPoint(vertices[ib]);
                Vector3 c = rendererTransform.TransformPoint(vertices[ic]);
                Vector3 na = TransformNormal(normalMatrix, normals, ia);
                Vector3 nb = TransformNormal(normalMatrix, normals, ib);
                Vector3 nc = TransformNormal(normalMatrix, normals, ic);
                if (!GripFrame.IsFinite(a) || !GripFrame.IsFinite(b) || !GripFrame.IsFinite(c))
                    throw new InvalidOperationException(label + " contains a non-finite baked vertex.");
                if (Vector3.Cross(b - a, c - a).sqrMagnitude <= 1e-16f)
                    continue;
                destination.Add(new Triangle(a, b, c, na, nb, nc, submesh));
            }
        }

        private static Vector3 TransformNormal(Matrix4x4 matrix, Vector3[] normals, int index)
        {
            if (normals == null || index >= normals.Length || !GripFrame.IsFinite(normals[index])
                || normals[index].sqrMagnitude <= 1e-12f) return Vector3.zero;
            Vector3 transformed = matrix.MultiplyVector(normals[index]);
            return transformed.sqrMagnitude > 1e-12f && GripFrame.IsFinite(transformed)
                ? transformed.normalized : Vector3.zero;
        }

        private static SurfaceDistance NearestSignedDistance(Triangle[] triangles, Vector3 point)
        {
            float bestSquared = float.PositiveInfinity;
            Vector3 bestPoint = default;
            Vector3 bestNormal = default;
            int bestSubmesh = -1;
            foreach (Triangle triangle in triangles)
            {
                Vector3 closest = ClosestPointOnTriangle(point, triangle.A, triangle.B, triangle.C, out Vector3 barycentric);
                float squared = (point - closest).sqrMagnitude;
                if (squared >= bestSquared) continue;
                Vector3 normal = triangle.NormalA * barycentric.x + triangle.NormalB * barycentric.y + triangle.NormalC * barycentric.z;
                if (normal.sqrMagnitude <= 1e-12f || !GripFrame.IsFinite(normal)) normal = triangle.FaceNormal;
                if (normal.sqrMagnitude <= 1e-12f || !GripFrame.IsFinite(normal)) continue;
                bestSquared = squared;
                bestPoint = closest;
                bestNormal = normal.normalized;
                bestSubmesh = triangle.Submesh;
            }
            if (bestSubmesh < 0) throw new InvalidOperationException("No nondegenerate triangle with a usable normal was available for a probe.");
            // The normal chooses the side. Distance is the Euclidean closest-point
            // distance; a normal projection underestimates it at triangle boundaries.
            float distance = Mathf.Sqrt(bestSquared);
            float signed = Vector3.Dot(point - bestPoint, bestNormal) < 0f ? -distance : distance;
            return new SurfaceDistance(signed, bestPoint, bestSubmesh);
        }

        /// <summary>Ericson region tests with segment fallback for numerically degenerate triangles.</summary>
        private static Vector3 ClosestPointOnTriangle(Vector3 p, Vector3 a, Vector3 b, Vector3 c,
            out Vector3 barycentric)
        {
            Vector3 ab = b - a, ac = c - a, ap = p - a;
            float d1 = Vector3.Dot(ab, ap), d2 = Vector3.Dot(ac, ap);
            if (d1 <= 0f && d2 <= 0f) { barycentric = new Vector3(1f, 0f, 0f); return a; }
            Vector3 bp = p - b;
            float d3 = Vector3.Dot(ab, bp), d4 = Vector3.Dot(ac, bp);
            if (d3 >= 0f && d4 <= d3) { barycentric = new Vector3(0f, 1f, 0f); return b; }
            float vc = d1 * d4 - d3 * d2;
            if (vc <= 0f && d1 >= 0f && d3 <= 0f)
            {
                float v = d1 / (d1 - d3);
                barycentric = new Vector3(1f - v, v, 0f); return a + ab * v;
            }
            Vector3 cp = p - c;
            float d5 = Vector3.Dot(ab, cp), d6 = Vector3.Dot(ac, cp);
            if (d6 >= 0f && d5 <= d6) { barycentric = new Vector3(0f, 0f, 1f); return c; }
            float vb = d5 * d2 - d1 * d6;
            if (vb <= 0f && d2 >= 0f && d6 <= 0f)
            {
                float w = d2 / (d2 - d6);
                barycentric = new Vector3(1f - w, 0f, w); return a + ac * w;
            }
            float va = d3 * d6 - d5 * d4;
            if (va <= 0f && d4 - d3 >= 0f && d5 - d6 >= 0f)
            {
                float w = (d4 - d3) / ((d4 - d3) + (d5 - d6));
                barycentric = new Vector3(0f, 1f - w, w); return b + (c - b) * w;
            }
            float denominator = va + vb + vc;
            if (Mathf.Abs(denominator) <= 1e-20f)
                return ClosestPointOnDegenerateTriangle(p, a, b, c, out barycentric);
            float inverse = 1f / denominator;
            float faceV = vb * inverse, faceW = vc * inverse;
            barycentric = new Vector3(1f - faceV - faceW, faceV, faceW);
            return a + ab * faceV + ac * faceW;
        }

        private static Vector3 ClosestPointOnDegenerateTriangle(Vector3 point, Vector3 a, Vector3 b,
            Vector3 c, out Vector3 barycentric)
        {
            Vector3 abPoint = ClosestPointOnSegment(point, a, b, out float abT);
            Vector3 bcPoint = ClosestPointOnSegment(point, b, c, out float bcT);
            Vector3 caPoint = ClosestPointOnSegment(point, c, a, out float caT);
            float abD = (point - abPoint).sqrMagnitude, bcD = (point - bcPoint).sqrMagnitude,
                caD = (point - caPoint).sqrMagnitude;
            if (abD <= bcD && abD <= caD) { barycentric = new Vector3(1f - abT, abT, 0f); return abPoint; }
            if (bcD <= caD) { barycentric = new Vector3(0f, 1f - bcT, bcT); return bcPoint; }
            barycentric = new Vector3(caT, 0f, 1f - caT); return caPoint;
        }

        private static Vector3 ClosestPointOnSegment(Vector3 point, Vector3 a, Vector3 b, out float t)
        {
            Vector3 delta = b - a;
            float lengthSquared = delta.sqrMagnitude;
            t = lengthSquared <= 1e-20f ? 0f : Mathf.Clamp01(Vector3.Dot(point - a, delta) / lengthSquared);
            return a + delta * t;
        }

        private static int CountProfileProbes(HandGripRigProfile profile)
        {
            int count = 0;
            foreach (HandGripDigitProfile digit in profile.Digits)
            {
                if (digit == null) throw new InvalidOperationException("Grip profile contains a null digit profile.");
                count += digit.Probes.Length;
            }
            return count;
        }

        private static int FindProbeIndex(string error)
        {
            const string marker = "probe ";
            int start = error != null ? error.IndexOf(marker, StringComparison.OrdinalIgnoreCase) : -1;
            if (start < 0) return -1;
            start += marker.Length;
            int end = start;
            while (end < error.Length && char.IsDigit(error[end])) end++;
            return end > start && int.TryParse(error.Substring(start, end - start), out int index) ? index : -1;
        }

        private static void DestroyBaked(BakedSurface surface)
        {
            if (surface != null) DestroyMesh(surface.Mesh);
        }

        private static void DestroyMesh(Mesh mesh)
        {
            if (mesh == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(mesh);
            else UnityEngine.Object.DestroyImmediate(mesh);
        }
    }
}
