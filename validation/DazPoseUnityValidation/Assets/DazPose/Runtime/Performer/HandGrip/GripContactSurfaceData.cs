using System;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

namespace DazPose.Performer.HandGrip
{
    /// <summary>
    /// Prepared immutable-at-runtime triangle geometry for local signed-distance contact queries.
    /// Configure this asset during content preparation, then treat its cloned data as a release snapshot.
    /// </summary>
    [CreateAssetMenu(menuName = "DazPose/Performer/Grip Contact Surface", fileName = "GripContactSurface")]
    public sealed class GripContactSurfaceData : ScriptableObject
    {
        public const int LeafTriangleLimit = 8;
        private const float NormalEpsilonSq = 1e-12f;
        private const float DegenerateAreaEpsilonSq = 1e-16f;

        [Serializable]
        public struct Triangle
        {
            public Vector3 A;
            public Vector3 B;
            public Vector3 C;
            public Vector3 NormalA;
            public Vector3 NormalB;
            public Vector3 NormalC;

            public Triangle(Vector3 a, Vector3 b, Vector3 c, Vector3 normalA, Vector3 normalB, Vector3 normalC)
            {
                A = a; B = b; C = c;
                NormalA = normalA; NormalB = normalB; NormalC = normalC;
            }
        }

        [Serializable]
        public struct Node
        {
            public Vector3 BoundsMin;
            public Vector3 BoundsMax;
            public int Left;
            public int Right;
            public int Start;
            public int Count;

            public bool IsLeaf => Count > 0;

            public Node(Vector3 boundsMin, Vector3 boundsMax, int left, int right, int start, int count)
            {
                BoundsMin = boundsMin; BoundsMax = boundsMax;
                Left = left; Right = right; Start = start; Count = count;
            }
        }

        [SerializeField] private string identity;
        [SerializeField] private int revision;
        [SerializeField] private int droppedDegenerateTriangleCount;
        [SerializeField] private Triangle[] triangles = Array.Empty<Triangle>();
        [SerializeField] private Node[] nodes = Array.Empty<Node>();

        [NonSerialized] private int _validatedRevision = int.MinValue;
        [NonSerialized] private bool _isReady;
        [NonSerialized] private string _validationReason;

        public string Identity => identity ?? string.Empty;
        public int Revision => revision;
        public int DroppedDegenerateTriangleCount => droppedDegenerateTriangleCount;
        public Triangle[] Triangles => triangles != null ? (Triangle[])triangles.Clone() : Array.Empty<Triangle>();
        public Node[] Nodes => nodes != null ? (Node[])nodes.Clone() : Array.Empty<Node>();
        public bool IsValid => IsReady(out _);

        private void OnEnable() => RefreshValidation();

        private void OnValidate()
        {
            EnsureIdentity();
            IncrementRevision();
            _validatedRevision = int.MinValue;
            RefreshValidation();
        }

        /// <summary>True only when nonempty finite triangle and BVH data are internally consistent.</summary>
        public bool IsReady(out string reason)
        {
            if (_validatedRevision != revision) RefreshValidation();
            reason = _validationReason;
            return _isReady;
        }

        /// <summary>Builds a deterministic median-split BVH and replaces this asset's prepared snapshot.</summary>
        public void Configure(Triangle[] sourceTriangles)
        {
            if (!TryConfigure(sourceTriangles, out string error))
                throw new ArgumentException(error ?? "Grip contact surface data is invalid.", nameof(sourceTriangles));
        }

        /// <summary>Validates, filters degenerate triangles and atomically stores the resulting BVH.</summary>
        public bool TryConfigure(Triangle[] sourceTriangles, out string error)
        {
            error = null;
            if (sourceTriangles == null || sourceTriangles.Length == 0)
            { error = "Triangle source is empty."; return false; }

            var prepared = new List<Triangle>(sourceTriangles.Length);
            int dropped = 0;
            for (int i = 0; i < sourceTriangles.Length; i++)
            {
                Triangle input = sourceTriangles[i];
                if (!Finite(input.A) || !Finite(input.B) || !Finite(input.C) ||
                    !Finite(input.NormalA) || !Finite(input.NormalB) || !Finite(input.NormalC))
                { error = "Triangle " + i + " contains a non-finite position or normal."; return false; }

                Vector3 face = Vector3.Cross(input.B - input.A, input.C - input.A);
                if (face.sqrMagnitude <= DegenerateAreaEpsilonSq)
                { dropped++; continue; }
                face.Normalize();
                Vector3 reference = ValidNormal(input.NormalA) + ValidNormal(input.NormalB) + ValidNormal(input.NormalC);
                if (reference.sqrMagnitude > NormalEpsilonSq)
                {
                    reference.Normalize();
                    if (Vector3.Dot(face, reference) < 0f) face = -face;
                }
                else reference = face;
                input.NormalA = OutwardNormal(input.NormalA, face, reference);
                input.NormalB = OutwardNormal(input.NormalB, face, reference);
                input.NormalC = OutwardNormal(input.NormalC, face, reference);
                prepared.Add(input);
            }
            if (prepared.Count == 0)
            { error = "All source triangles are degenerate; a contact surface cannot be empty."; return false; }

            Triangle[] ordered = prepared.ToArray();
            var builtNodes = new List<Node>(Math.Max(1, ordered.Length / LeafTriangleLimit * 2));
            BuildNode(ordered, 0, ordered.Length, builtNodes);
            string nextIdentity = string.IsNullOrWhiteSpace(identity) ? Guid.NewGuid().ToString("N") : identity;

            triangles = ordered;
            nodes = builtNodes.ToArray();
            identity = nextIdentity;
            droppedDegenerateTriangleCount = dropped;
            IncrementRevision();
            RefreshValidation();
            if (!_isReady)
            { error = _validationReason; return false; }
            return true;
        }

        private void RefreshValidation()
        {
            _validatedRevision = revision;
            _isReady = false;
            _validationReason = null;
            if (string.IsNullOrWhiteSpace(identity)) { _validationReason = "Surface identity is missing."; return; }
            if (revision <= 0) { _validationReason = "Surface revision is missing."; return; }
            if (triangles == null || triangles.Length == 0) { _validationReason = "Surface has no prepared triangles."; return; }
            if (nodes == null || nodes.Length == 0) { _validationReason = "Surface BVH has no root node."; return; }
            for (int i = 0; i < triangles.Length; i++)
            {
                Triangle triangle = triangles[i];
                if (!Finite(triangle.A) || !Finite(triangle.B) || !Finite(triangle.C) ||
                    !Finite(triangle.NormalA) || !Finite(triangle.NormalB) || !Finite(triangle.NormalC))
                { _validationReason = "Prepared triangle " + i + " contains non-finite data."; return; }
                if (!Unit(triangle.NormalA) || !Unit(triangle.NormalB) || !Unit(triangle.NormalC))
                { _validationReason = "Prepared triangle " + i + " has an invalid vertex normal."; return; }
            }
            for (int i = 0; i < nodes.Length; i++)
            {
                Node node = nodes[i];
                if (!Finite(node.BoundsMin) || !Finite(node.BoundsMax) ||
                    node.BoundsMin.x > node.BoundsMax.x || node.BoundsMin.y > node.BoundsMax.y || node.BoundsMin.z > node.BoundsMax.z)
                { _validationReason = "BVH node " + i + " has invalid bounds."; return; }
                if (node.IsLeaf)
                {
                    if (node.Count > LeafTriangleLimit || node.Start < 0 || node.Start > triangles.Length - node.Count)
                    { _validationReason = "BVH leaf " + i + " has an invalid triangle range."; return; }
                }
                else if (node.Left < 0 || node.Left >= nodes.Length || node.Right < 0 || node.Right >= nodes.Length ||
                         node.Left == i || node.Right == i || node.Left == node.Right)
                { _validationReason = "BVH branch " + i + " has invalid child indices."; return; }
            }
            _isReady = true;
        }

        private void EnsureIdentity()
        {
            if (string.IsNullOrWhiteSpace(identity)) identity = Guid.NewGuid().ToString("N");
        }

        private void IncrementRevision() => revision = revision == int.MaxValue ? 1 : revision + 1;

        private static int BuildNode(Triangle[] data, int start, int count, List<Node> output)
        {
            Bounds(data, start, count, out Vector3 boundsMin, out Vector3 boundsMax,
                out Vector3 centroidMin, out Vector3 centroidMax);
            int index = output.Count;
            output.Add(default);
            if (count <= LeafTriangleLimit)
            {
                output[index] = new Node(boundsMin, boundsMax, -1, -1, start, count);
                return index;
            }

            Vector3 extent = centroidMax - centroidMin;
            int axis = extent.x >= extent.y && extent.x >= extent.z ? 0 : extent.y >= extent.z ? 1 : 2;
            Array.Sort(data, start, count, new CentroidComparer(axis));
            int leftCount = count / 2;
            int left = BuildNode(data, start, leftCount, output);
            int right = BuildNode(data, start + leftCount, count - leftCount, output);
            output[index] = new Node(boundsMin, boundsMax, left, right, 0, 0);
            return index;
        }

        private static void Bounds(Triangle[] data, int start, int count, out Vector3 min,
            out Vector3 max, out Vector3 centroidMin, out Vector3 centroidMax)
        {
            min = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
            max = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
            centroidMin = min; centroidMax = max;
            for (int i = start; i < start + count; i++)
            {
                Triangle triangle = data[i];
                Include(triangle.A, ref min, ref max);
                Include(triangle.B, ref min, ref max);
                Include(triangle.C, ref min, ref max);
                Vector3 center = (triangle.A + triangle.B + triangle.C) / 3f;
                Include(center, ref centroidMin, ref centroidMax);
            }
        }

        private static void Include(Vector3 point, ref Vector3 min, ref Vector3 max)
        {
            min = Vector3.Min(min, point);
            max = Vector3.Max(max, point);
        }

        private sealed class CentroidComparer : IComparer<Triangle>
        {
            private readonly int _axis;
            public CentroidComparer(int axis) { _axis = axis; }
            public int Compare(Triangle a, Triangle b)
            {
                float ca = Component((a.A + a.B + a.C) / 3f, _axis);
                float cb = Component((b.A + b.B + b.C) / 3f, _axis);
                return ca.CompareTo(cb);
            }
        }

        private static Vector3 ValidNormal(Vector3 normal) => Finite(normal) && normal.sqrMagnitude > NormalEpsilonSq ? normal.normalized : Vector3.zero;

        private static Vector3 OutwardNormal(Vector3 normal, Vector3 face, Vector3 reference)
        {
            Vector3 result = ValidNormal(normal);
            if (result.sqrMagnitude <= NormalEpsilonSq) result = face;
            else if (reference.sqrMagnitude > NormalEpsilonSq && Vector3.Dot(result, reference) < 0f) result = -result;
            return result;
        }

        private static float Component(Vector3 value, int axis) => axis == 0 ? value.x : axis == 1 ? value.y : value.z;
        private static bool Unit(Vector3 value) => Finite(value) && Mathf.Abs(value.sqrMagnitude - 1f) <= 0.001f;
        private static bool Finite(Vector3 value) => GripFrame.IsFinite(value);
    }

    /// <summary>Rigid world pose for one prepared contact surface snapshot.</summary>
    public readonly struct GripContactSurfaceSnapshot
    {
        public GripContactSurfaceData Data { get; }
        public Vector3 Position { get; }
        public Quaternion Rotation { get; }
        public bool IsValid => Data != null && Data.IsReady(out _) && GripFrame.IsFinite(Position) && ValidRotation(Rotation);

        public GripContactSurfaceSnapshot(GripContactSurfaceData data, Vector3 position, Quaternion rotation)
        { Data = data; Position = position; Rotation = rotation; }

        private static bool ValidRotation(Quaternion value) =>
            GripFrame.IsFinite(value.x) && GripFrame.IsFinite(value.y) && GripFrame.IsFinite(value.z) && GripFrame.IsFinite(value.w) &&
            Mathf.Abs(value.x * value.x + value.y * value.y + value.z * value.z + value.w * value.w - 1f) <= 0.001f;
    }

    /// <summary>Optional evaluated triangle-surface capability on a grip target.</summary>
    public interface IGripTargetContactSurface
    {
        bool TryGetContactSurface(out GripContactSurfaceSnapshot snapshot);
    }

    /// <summary>Allocation-free closest-triangle signed distance query for Burst/job callers.</summary>
    public static class GripContactSurfaceQuery
    {
        private const float NormalEpsilonSq = 1e-12f;
        private const int MaximumStackDepth = 100;

        /// <summary>
        /// Returns signed distance in rigid-local surface units (world metres under unit scale).
        /// Sign is local to the interpolated normal of the closest triangle; this is not a global
        /// inside/outside test for open or merged surfaces. Invalid input returns +Infinity.
        /// </summary>
        public static float SignedDistance(Vector3 worldPoint, Vector3 position, Quaternion rotation,
            NativeArray<GripContactSurfaceData.Triangle> triangles, NativeArray<GripContactSurfaceData.Node> nodes)
        {
            if (!GripFrame.IsFinite(worldPoint) || !GripFrame.IsFinite(position) || !ValidRotation(rotation) ||
                !triangles.IsCreated || triangles.Length == 0 || !nodes.IsCreated || nodes.Length == 0)
                return float.PositiveInfinity;

            Vector3 localPoint = Quaternion.Inverse(rotation) * (worldPoint - position);
            if (!GripFrame.IsFinite(localPoint)) return float.PositiveInfinity;
            FixedList512Bytes<int> stack = default;
            stack.Add(0);
            float bestDistanceSq = float.PositiveInfinity;
            Vector3 bestPoint = default;
            Vector3 bestNormal = default;
            int visitBudget = nodes.Length * 2 + 1;
            while (stack.Length > 0 && visitBudget-- > 0)
            {
                int nodeIndex = stack[stack.Length - 1];
                stack.RemoveAt(stack.Length - 1);
                if (nodeIndex < 0 || nodeIndex >= nodes.Length) return float.PositiveInfinity;
                var node = nodes[nodeIndex];
                float boundsDistanceSq = BoundsDistanceSquared(localPoint, node.BoundsMin, node.BoundsMax);
                if (float.IsNaN(boundsDistanceSq)) return float.PositiveInfinity;
                if (boundsDistanceSq > bestDistanceSq) continue;
                if (node.IsLeaf)
                {
                    if (node.Start < 0 || node.Count < 1 || node.Start > triangles.Length - node.Count)
                        return float.PositiveInfinity;
                    for (int i = node.Start; i < node.Start + node.Count; i++)
                    {
                        var triangle = triangles[i];
                        if (!FiniteTriangle(triangle)) return float.PositiveInfinity;
                        ClosestPoint(localPoint, triangle, out Vector3 closest, out Vector3 barycentric);
                        float distanceSq = (localPoint - closest).sqrMagnitude;
                        if (float.IsNaN(distanceSq) || distanceSq >= bestDistanceSq) continue;
                        Vector3 normal = triangle.NormalA * barycentric.x + triangle.NormalB * barycentric.y + triangle.NormalC * barycentric.z;
                        if (!GripFrame.IsFinite(normal)) return float.PositiveInfinity;
                        if (normal.sqrMagnitude <= NormalEpsilonSq)
                            normal = Vector3.Cross(triangle.B - triangle.A, triangle.C - triangle.A);
                        if (normal.sqrMagnitude <= NormalEpsilonSq) return float.PositiveInfinity;
                        bestDistanceSq = distanceSq;
                        bestPoint = closest;
                        bestNormal = normal.normalized;
                    }
                }
                else
                {
                    if (node.Left < 0 || node.Left >= nodes.Length || node.Right < 0 || node.Right >= nodes.Length ||
                        stack.Length + 2 > MaximumStackDepth)
                        return float.PositiveInfinity;
                    var left = nodes[node.Left];
                    var right = nodes[node.Right];
                    float leftDistanceSq = BoundsDistanceSquared(localPoint, left.BoundsMin, left.BoundsMax);
                    float rightDistanceSq = BoundsDistanceSquared(localPoint, right.BoundsMin, right.BoundsMax);
                    if (float.IsNaN(leftDistanceSq) || float.IsNaN(rightDistanceSq)) return float.PositiveInfinity;
                    if (leftDistanceSq <= rightDistanceSq)
                    {
                        if (rightDistanceSq <= bestDistanceSq) stack.Add(node.Right);
                        if (leftDistanceSq <= bestDistanceSq) stack.Add(node.Left);
                    }
                    else
                    {
                        if (leftDistanceSq <= bestDistanceSq) stack.Add(node.Left);
                        if (rightDistanceSq <= bestDistanceSq) stack.Add(node.Right);
                    }
                }
            }
            if (visitBudget < 0 || float.IsPositiveInfinity(bestDistanceSq)) return float.PositiveInfinity;
            float distance = Mathf.Sqrt(bestDistanceSq);
            float side = Vector3.Dot(localPoint - bestPoint, bestNormal);
            return side < 0f ? -distance : distance;
        }

        private static void ClosestPoint(Vector3 point, GripContactSurfaceData.Triangle triangle,
            out Vector3 closest, out Vector3 barycentric)
        {
            Vector3 a = triangle.A, b = triangle.B, c = triangle.C;
            Vector3 ab = b - a, ac = c - a, ap = point - a;
            float d1 = Vector3.Dot(ab, ap), d2 = Vector3.Dot(ac, ap);
            if (d1 <= 0f && d2 <= 0f) { closest = a; barycentric = new Vector3(1f, 0f, 0f); return; }
            Vector3 bp = point - b;
            float d3 = Vector3.Dot(ab, bp), d4 = Vector3.Dot(ac, bp);
            if (d3 >= 0f && d4 <= d3) { closest = b; barycentric = new Vector3(0f, 1f, 0f); return; }
            float vc = d1 * d4 - d3 * d2;
            if (vc <= 0f && d1 >= 0f && d3 <= 0f)
            {
                float v = d1 / (d1 - d3);
                closest = a + ab * v; barycentric = new Vector3(1f - v, v, 0f); return;
            }
            Vector3 cp = point - c;
            float d5 = Vector3.Dot(ab, cp), d6 = Vector3.Dot(ac, cp);
            if (d6 >= 0f && d5 <= d6) { closest = c; barycentric = new Vector3(0f, 0f, 1f); return; }
            float vb = d5 * d2 - d1 * d6;
            if (vb <= 0f && d2 >= 0f && d6 <= 0f)
            {
                float w = d2 / (d2 - d6);
                closest = a + ac * w; barycentric = new Vector3(1f - w, 0f, w); return;
            }
            float va = d3 * d6 - d5 * d4;
            if (va <= 0f && (d4 - d3) >= 0f && (d5 - d6) >= 0f)
            {
                float w = (d4 - d3) / ((d4 - d3) + (d5 - d6));
                closest = b + (c - b) * w; barycentric = new Vector3(0f, 1f - w, w); return;
            }
            float denominator = 1f / (va + vb + vc);
            float faceV = vb * denominator, faceW = vc * denominator;
            closest = a + ab * faceV + ac * faceW;
            barycentric = new Vector3(1f - faceV - faceW, faceV, faceW);
        }

        private static float BoundsDistanceSquared(Vector3 point, Vector3 min, Vector3 max)
        {
            if (!GripFrame.IsFinite(min) || !GripFrame.IsFinite(max) ||
                min.x > max.x || min.y > max.y || min.z > max.z) return float.NaN;
            float x = point.x < min.x ? min.x - point.x : point.x > max.x ? point.x - max.x : 0f;
            float y = point.y < min.y ? min.y - point.y : point.y > max.y ? point.y - max.y : 0f;
            float z = point.z < min.z ? min.z - point.z : point.z > max.z ? point.z - max.z : 0f;
            return x * x + y * y + z * z;
        }

        private static bool FiniteTriangle(GripContactSurfaceData.Triangle triangle) =>
            GripFrame.IsFinite(triangle.A) && GripFrame.IsFinite(triangle.B) && GripFrame.IsFinite(triangle.C) &&
            GripFrame.IsFinite(triangle.NormalA) && GripFrame.IsFinite(triangle.NormalB) && GripFrame.IsFinite(triangle.NormalC);

        private static bool ValidRotation(Quaternion value) =>
            GripFrame.IsFinite(value.x) && GripFrame.IsFinite(value.y) && GripFrame.IsFinite(value.z) && GripFrame.IsFinite(value.w) &&
            Mathf.Abs(value.x * value.x + value.y * value.y + value.z * value.z + value.w * value.w - 1f) <= 0.001f;
    }
}
