using System;
using System.Collections.Generic;
using UnityEngine;

namespace DazPose.UnityValidation
{
    public sealed class DazPoseRestBasisFit
    {
        public Matrix4x4 Basis;
        public float BasisDeterminant;
        public Vector3 Translation;
        public float RmsErrorMeters;
        public float MaxErrorMeters;
        public int SampleCount;
    }

    public static class DazPoseRestBasisCalibration
    {
        private const float DazCentimetersToMeters = 0.01f;

        public static DazPoseRestBasisFit Fit(IReadOnlyList<Vector3> dazPointsCm, IReadOnlyList<Vector3> unityPoints)
        {
            if (dazPointsCm == null || unityPoints == null || dazPointsCm.Count != unityPoints.Count || dazPointsCm.Count < 3)
                throw new ArgumentException("Rest basis fit needs at least three paired DAZ/Unity bone positions.");

            var sourceCenter = Vector3.zero;
            var targetCenter = Vector3.zero;
            for (var index = 0; index < dazPointsCm.Count; index++)
            {
                sourceCenter += dazPointsCm[index] * DazCentimetersToMeters;
                targetCenter += unityPoints[index];
            }
            sourceCenter /= dazPointsCm.Count;
            targetCenter /= unityPoints.Count;

            // Fit one orthogonal basis at the DAZ/Unity root boundary from the actual neutral bone positions.
            // The polar decomposition preserves a measured reflection when the source and target coordinate
            // systems differ in handedness; it does not invent a component swap or per-bone correction.
            var targetSource = new double[3, 3];
            var sourceSource = new double[3, 3];
            for (var index = 0; index < dazPointsCm.Count; index++)
            {
                var x = dazPointsCm[index] * DazCentimetersToMeters - sourceCenter;
                var y = unityPoints[index] - targetCenter;
                var source = new[] { (double)x.x, x.y, x.z };
                var target = new[] { (double)y.x, y.y, y.z };
                for (var row = 0; row < 3; row++)
                for (var column = 0; column < 3; column++)
                {
                    targetSource[row, column] += target[row] * source[column];
                    sourceSource[row, column] += source[row] * source[column];
                }
            }

            var basis = Multiply(targetSource, Inverse(sourceSource));
            for (var iteration = 0; iteration < 30; iteration++)
            {
                var inverseTranspose = Transpose(Inverse(basis));
                var next = new double[3, 3];
                var difference = 0.0;
                for (var row = 0; row < 3; row++)
                for (var column = 0; column < 3; column++)
                {
                    next[row, column] = 0.5 * (basis[row, column] + inverseTranspose[row, column]);
                    difference = Math.Max(difference, Math.Abs(next[row, column] - basis[row, column]));
                }
                basis = next;
                if (difference < 1e-10) break;
            }
            var unityBasis = ToUnityMatrix(basis);
            var determinant = Determinant(basis);
            var translation = targetCenter - MultiplyVector(unityBasis, sourceCenter);
            var sumSquares = 0f;
            var maxError = 0f;
            for (var index = 0; index < dazPointsCm.Count; index++)
            {
                var expected = MultiplyVector(unityBasis, dazPointsCm[index] * DazCentimetersToMeters) + translation;
                var error = Vector3.Distance(expected, unityPoints[index]);
                sumSquares += error * error;
                maxError = Mathf.Max(maxError, error);
            }
            return new DazPoseRestBasisFit
            {
                Basis = unityBasis,
                BasisDeterminant = (float)determinant,
                Translation = translation,
                RmsErrorMeters = Mathf.Sqrt(sumSquares / dazPointsCm.Count),
                MaxErrorMeters = maxError,
                SampleCount = dazPointsCm.Count
            };
        }

        public static Vector3 ConvertDazCentimeterDelta(Matrix4x4 dazToUnityWorldBasis, Vector3 deltaCm)
            => dazToUnityWorldBasis.MultiplyVector(deltaCm * DazCentimetersToMeters);

        public static Quaternion ConvertWorldRotationDelta(Matrix4x4 dazToUnityWorldBasis, Quaternion dazDelta)
        {
            var dazRotation = Matrix4x4.Rotate(dazDelta);
            var converted = dazToUnityWorldBasis * dazRotation * dazToUnityWorldBasis.transpose;
            var up4 = converted.GetColumn(1);
            var forward4 = converted.GetColumn(2);
            return Quaternion.Normalize(Quaternion.LookRotation(new Vector3(forward4.x, forward4.y, forward4.z), new Vector3(up4.x, up4.y, up4.z)));
        }

        public static float[] MatrixValues(Matrix4x4 value) => new[]
        {
            value.m00, value.m01, value.m02, value.m10, value.m11, value.m12, value.m20, value.m21, value.m22
        };

        private static Matrix4x4 ToUnityMatrix(double[,] value)
        {
            var result = Matrix4x4.identity;
            result.m00 = (float)value[0, 0]; result.m01 = (float)value[0, 1]; result.m02 = (float)value[0, 2];
            result.m10 = (float)value[1, 0]; result.m11 = (float)value[1, 1]; result.m12 = (float)value[1, 2];
            result.m20 = (float)value[2, 0]; result.m21 = (float)value[2, 1]; result.m22 = (float)value[2, 2];
            return result;
        }

        private static Vector3 MultiplyVector(Matrix4x4 m, Vector3 v)
            => new Vector3(m.m00 * v.x + m.m01 * v.y + m.m02 * v.z,
                m.m10 * v.x + m.m11 * v.y + m.m12 * v.z,
                m.m20 * v.x + m.m21 * v.y + m.m22 * v.z);

        private static double[,] Multiply(double[,] a, double[,] b)
        {
            var result = new double[3, 3];
            for (var row = 0; row < 3; row++)
            for (var column = 0; column < 3; column++)
            for (var inner = 0; inner < 3; inner++) result[row, column] += a[row, inner] * b[inner, column];
            return result;
        }

        private static double[,] Transpose(double[,] value)
        {
            var result = new double[3, 3];
            for (var row = 0; row < 3; row++)
            for (var column = 0; column < 3; column++) result[row, column] = value[column, row];
            return result;
        }

        private static double[,] Inverse(double[,] m)
        {
            var determinant = Determinant(m);
            if (Math.Abs(determinant) < 1e-14) throw new InvalidOperationException("Rest landmark positions do not span three dimensions; the coordinate basis cannot be derived.");
            return new[,]
            {
                { (m[1, 1] * m[2, 2] - m[1, 2] * m[2, 1]) / determinant, (m[0, 2] * m[2, 1] - m[0, 1] * m[2, 2]) / determinant, (m[0, 1] * m[1, 2] - m[0, 2] * m[1, 1]) / determinant },
                { (m[1, 2] * m[2, 0] - m[1, 0] * m[2, 2]) / determinant, (m[0, 0] * m[2, 2] - m[0, 2] * m[2, 0]) / determinant, (m[0, 2] * m[1, 0] - m[0, 0] * m[1, 2]) / determinant },
                { (m[1, 0] * m[2, 1] - m[1, 1] * m[2, 0]) / determinant, (m[0, 1] * m[2, 0] - m[0, 0] * m[2, 1]) / determinant, (m[0, 0] * m[1, 1] - m[0, 1] * m[1, 0]) / determinant }
            };
        }

        private static double Determinant(double[,] m)
            => m[0, 0] * (m[1, 1] * m[2, 2] - m[1, 2] * m[2, 1])
                - m[0, 1] * (m[1, 0] * m[2, 2] - m[1, 2] * m[2, 0])
                + m[0, 2] * (m[1, 0] * m[2, 1] - m[1, 1] * m[2, 0]);

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
