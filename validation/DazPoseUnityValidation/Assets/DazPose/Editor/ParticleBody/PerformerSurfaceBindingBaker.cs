using System;
using System.Collections.Generic;
using System.Globalization;
using DazPose.Performer;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace DazPose.Editor.ParticleBody
{
    /// <summary>Bakes persistent, area-weighted surface addresses in Edit Mode only.</summary>
    public static class PerformerSurfaceBindingBaker
    {
        private const string DefaultAssetPath = "Assets/DazPose/Effects/ParticleBody/PerformerSurfaceBindings.asset";

        private readonly struct AreaTriangle
        {
            public readonly uint Triangle;
            public readonly double CumulativeArea;

            public AreaTriangle(uint triangle, double cumulativeArea)
            {
                Triangle = triangle;
                CumulativeArea = cumulativeArea;
            }
        }

        private struct StableRandom
        {
            private uint _state;

            public StableRandom(int seed)
            {
                _state = unchecked((uint)seed);
                if (_state == 0) _state = 0x9e3779b9u;
            }

            public uint NextUInt()
            {
                unchecked
                {
                    uint value = _state;
                    value ^= value << 13;
                    value ^= value >> 17;
                    value ^= value << 5;
                    _state = value;
                    return value;
                }
            }

            public double NextUnitDouble() => (NextUInt() + 0.5d) / 4294967296d;
            public float NextUnitFloat() => (NextUInt() >> 8) * (1f / 16777216f);
        }

        [MenuItem("Tools/DAZ Pose/First Performance Void/Bake Performer Surface Bindings")]
        public static void BakeSelectedRenderer()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Bake performer surface bindings in Edit Mode.");

            SkinnedMeshRenderer renderer = Selection.activeGameObject != null
                ? Selection.activeGameObject.GetComponent<SkinnedMeshRenderer>()
                : Selection.activeObject as SkinnedMeshRenderer;
            if (renderer == null)
                throw new InvalidOperationException("Select Lara's SkinnedMeshRenderer GameObject before baking surface bindings.");

            string path = EditorUtility.SaveFilePanelInProject("Bake Performer Surface Bindings",
                "PerformerSurfaceBindings", "asset", "Save the reusable stable triangle/surface address set.",
                System.IO.Path.GetDirectoryName(DefaultAssetPath));
            if (string.IsNullOrEmpty(path)) return;

            PerformerSurfaceBindingAsset asset = Bake(renderer, PerformerSurfaceBindingAsset.RequiredBindingCount, 0x504f3942, path);
            Selection.activeObject = asset;
            Debug.Log("PERFORMER_SURFACE_BINDINGS_BAKED: " + asset.BindingCount + " area-weighted, deterministic bindings from "
                + asset.SourceMeshIdentity + " (" + asset.SourceVertexCount + " vertices, " + asset.SourceIndexCount + " indices).", asset);
        }

        public static PerformerSurfaceBindingAsset Bake(SkinnedMeshRenderer renderer, int count, int seed, string assetPath)
        {
            if (renderer == null) throw new ArgumentNullException(nameof(renderer));
            if (count != PerformerSurfaceBindingAsset.RequiredBindingCount)
                throw new ArgumentOutOfRangeException(nameof(count), "P0.G2 requires exactly 32,768 surface bindings.");

            Mesh mesh = renderer.sharedMesh;
            if (mesh == null) throw new InvalidOperationException("The selected SkinnedMeshRenderer has no shared mesh.");
            if (!mesh.isReadable) throw new InvalidOperationException("The source mesh is not readable. Enable Read/Write on the canonical source mesh before baking.");
            if (mesh.vertexCount == 0) throw new InvalidOperationException("The source mesh has no vertices.");
            if (mesh.subMeshCount == 0) throw new InvalidOperationException("The source mesh has no submeshes.");

            Vector3[] vertices = mesh.vertices;
            var triangles = new List<AreaTriangle>();
            double totalArea = 0d;
            uint globalTriangle = 0;
            for (int submesh = 0; submesh < mesh.subMeshCount; submesh++)
            {
                if (mesh.GetTopology(submesh) != MeshTopology.Triangles)
                    throw new InvalidOperationException("The source mesh has non-triangle topology in submesh " + submesh + ". A triangle surface binding cannot represent it safely.");

                int[] indices = mesh.GetIndices(submesh, true);
                if (indices.Length % 3 != 0)
                    throw new InvalidOperationException("The source mesh submesh " + submesh + " has an incomplete triangle index list.");

                for (int index = 0; index < indices.Length; index += 3, globalTriangle++)
                {
                    int a = indices[index];
                    int b = indices[index + 1];
                    int c = indices[index + 2];
                    if ((uint)a >= (uint)vertices.Length || (uint)b >= (uint)vertices.Length || (uint)c >= (uint)vertices.Length)
                        throw new InvalidOperationException("The source mesh contains an out-of-range triangle index at triangle " + globalTriangle + ".");

                    Vector3 cross = Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]);
                    double area = Math.Sqrt((double)cross.x * cross.x + (double)cross.y * cross.y + (double)cross.z * cross.z) * 0.5d;
                    if (double.IsNaN(area) || double.IsInfinity(area))
                        throw new InvalidOperationException("The source mesh contains a triangle with non-finite area at triangle " + globalTriangle + ".");
                    if (area <= 1e-12d) continue;

                    totalArea += area;
                    triangles.Add(new AreaTriangle(globalTriangle, totalArea));
                }
            }

            if (globalTriangle == 0) throw new InvalidOperationException("The source mesh contains zero triangles.");
            if (triangles.Count == 0 || totalArea <= 1e-12d || double.IsNaN(totalArea) || double.IsInfinity(totalArea))
                throw new InvalidOperationException("The source mesh has degenerate total triangle area and cannot be sampled.");

            var baked = new PerformerSurfaceBinding[count];
            var random = new StableRandom(seed);
            for (int i = 0; i < baked.Length; i++)
            {
                double areaSample = random.NextUnitDouble() * totalArea;
                int triangleIndex = FindCumulativeArea(triangles, areaSample);
                uint selectedTriangle = triangles[triangleIndex].Triangle;
                var square = new Vector2(random.NextUnitFloat(), random.NextUnitFloat());
                float particleSeed = random.NextUnitFloat();
                baked[i] = new PerformerSurfaceBinding(selectedTriangle, square, particleSeed);
            }

            string identity = GetMeshIdentity(mesh);
            string topologyHash = PerformerSurfaceBindingAsset.ComputeTopologyHash(mesh);
            PerformerSurfaceBindingAsset asset = AssetDatabase.LoadAssetAtPath<PerformerSurfaceBindingAsset>(assetPath);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<PerformerSurfaceBindingAsset>();
                AssetDatabase.CreateAsset(asset, assetPath);
            }
            else
            {
                Undo.RecordObject(asset, "Rebake Performer Surface Bindings");
            }

            asset.ConfigureBaked(mesh, identity, mesh.vertexCount, PerformerSurfaceBindingAsset.CountIndices(mesh),
                topologyHash, seed, baked);
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            ValidateBakedAsset(asset, renderer);
            return asset;
        }

        public static void ValidateBakedAsset(PerformerSurfaceBindingAsset asset, SkinnedMeshRenderer renderer)
        {
            if (asset == null) throw new ArgumentNullException(nameof(asset));
            if (renderer == null) throw new ArgumentNullException(nameof(renderer));
            if (!asset.IsValidFor(renderer, out string reason)) throw new InvalidOperationException(reason);
            Mesh mesh = renderer.sharedMesh;
            var positiveAreaTriangles = new HashSet<uint>();
            Vector3[] vertices = mesh.vertices;
            uint triangle = 0;
            for (int submesh = 0; submesh < mesh.subMeshCount; submesh++)
            {
                if (mesh.GetTopology(submesh) != MeshTopology.Triangles)
                    throw new InvalidOperationException("Submesh " + submesh + " no longer uses triangle topology.");
                int[] indices = mesh.GetIndices(submesh, true);
                for (int i = 0; i < indices.Length; i += 3, triangle++)
                {
                    Vector3 cross = Vector3.Cross(vertices[indices[i + 1]] - vertices[indices[i]], vertices[indices[i + 2]] - vertices[indices[i]]);
                    if (cross.sqrMagnitude > 4e-24f) positiveAreaTriangles.Add(triangle);
                }
            }

            PerformerSurfaceBinding[] bindings = asset.CopyBindings();
            if (bindings == null || bindings.Length != PerformerSurfaceBindingAsset.RequiredBindingCount)
                throw new InvalidOperationException("The baked asset does not contain exactly 32,768 entries.");
            var firstBake = new PerformerSurfaceBinding[bindings.Length];
            Array.Copy(bindings, firstBake, bindings.Length);
            for (int i = 0; i < bindings.Length; i++)
            {
                PerformerSurfaceBinding binding = bindings[i];
                if (!positiveAreaTriangles.Contains(binding.Triangle))
                    throw new InvalidOperationException("Binding " + i + " addresses a missing or zero-area triangle " + binding.Triangle + ".");
                if (binding.Square.x < 0f || binding.Square.x > 1f || binding.Square.y < 0f || binding.Square.y > 1f)
                    throw new InvalidOperationException("Binding " + i + " has square coordinates outside [0,1].");
                if (binding.Seed < 0f || binding.Seed >= 1f || float.IsNaN(binding.Seed))
                    throw new InvalidOperationException("Binding " + i + " has an invalid deterministic seed.");
            }

            PerformerSurfaceBinding[] secondBake = BuildBindings(mesh, bindings.Length, asset.RandomSeed);
            for (int i = 0; i < firstBake.Length; i++)
            {
                if (firstBake[i].Triangle != secondBake[i].Triangle
                    || firstBake[i].Square != secondBake[i].Square
                    || firstBake[i].Seed != secondBake[i].Seed)
                    throw new InvalidOperationException("The deterministic rebake check differed at binding " + i + ".");
            }
        }

        private static PerformerSurfaceBinding[] BuildBindings(Mesh mesh, int count, int seed)
        {
            Vector3[] vertices = mesh.vertices;
            var triangles = new List<AreaTriangle>();
            double totalArea = 0d;
            uint globalTriangle = 0;
            for (int submesh = 0; submesh < mesh.subMeshCount; submesh++)
            {
                int[] indices = mesh.GetIndices(submesh, true);
                for (int index = 0; index < indices.Length; index += 3, globalTriangle++)
                {
                    Vector3 cross = Vector3.Cross(vertices[indices[index + 1]] - vertices[indices[index]], vertices[indices[index + 2]] - vertices[indices[index]]);
                    double area = Math.Sqrt((double)cross.x * cross.x + (double)cross.y * cross.y + (double)cross.z * cross.z) * 0.5d;
                    if (area <= 1e-12d) continue;
                    totalArea += area;
                    triangles.Add(new AreaTriangle(globalTriangle, totalArea));
                }
            }

            var result = new PerformerSurfaceBinding[count];
            var random = new StableRandom(seed);
            for (int i = 0; i < count; i++)
            {
                int selected = FindCumulativeArea(triangles, random.NextUnitDouble() * totalArea);
                result[i] = new PerformerSurfaceBinding(triangles[selected].Triangle,
                    new Vector2(random.NextUnitFloat(), random.NextUnitFloat()), random.NextUnitFloat());
            }
            return result;
        }

        private static int FindCumulativeArea(List<AreaTriangle> triangles, double value)
        {
            int low = 0;
            int high = triangles.Count - 1;
            while (low < high)
            {
                int middle = low + ((high - low) >> 1);
                if (value < triangles[middle].CumulativeArea) high = middle;
                else low = middle + 1;
            }
            return low;
        }

        private static string GetMeshIdentity(Mesh mesh)
        {
            if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(mesh, out string guid, out long localId))
                return guid + ":" + localId.ToString(CultureInfo.InvariantCulture);
            return GlobalObjectId.GetGlobalObjectIdSlow(mesh).ToString();
        }
    }
}
