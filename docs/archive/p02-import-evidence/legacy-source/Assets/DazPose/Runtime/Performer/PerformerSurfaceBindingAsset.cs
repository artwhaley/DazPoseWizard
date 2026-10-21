using System;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.VFX;

namespace DazPose.Performer
{
    /// <summary>A stable, blittable address on one triangle of a skinned surface.</summary>
    [Serializable]
    [StructLayout(LayoutKind.Sequential, Pack = 4, Size = Stride)]
    [VFXType(VFXTypeAttribute.Usage.GraphicsBuffer)]
    public struct PerformerSurfaceBinding
    {
        public const int Stride = 16;

        public uint Triangle;
        public Vector2 Square;
        public float Seed;

        public PerformerSurfaceBinding(uint triangle, Vector2 square, float seed)
        {
            Triangle = triangle;
            Square = square;
            Seed = seed;
        }
    }

    /// <summary>Deterministic triangle/surface coordinates for one source mesh topology.</summary>
    [CreateAssetMenu(menuName = "DAZ Pose/Performer Surface Bindings", fileName = "PerformerSurfaceBindings")]
    public sealed class PerformerSurfaceBindingAsset : ScriptableObject
    {
        public const int RequiredBindingCount = 32768;

        [SerializeField] private Mesh sourceMesh;
        [SerializeField] private string sourceMeshIdentity;
        [SerializeField] private int sourceVertexCount;
        [SerializeField] private int sourceIndexCount;
        [SerializeField] private string sourceTopologyHash;
        [SerializeField] private int randomSeed;
        [SerializeField] private PerformerSurfaceBinding[] bindings = Array.Empty<PerformerSurfaceBinding>();

        public Mesh SourceMesh => sourceMesh;
        public string SourceMeshIdentity => sourceMeshIdentity;
        public int SourceVertexCount => sourceVertexCount;
        public int SourceIndexCount => sourceIndexCount;
        public string SourceTopologyHash => sourceTopologyHash;
        public int RandomSeed => randomSeed;
        public int BindingCount => bindings != null ? bindings.Length : 0;
        /// <summary>Returns a defensive copy for the editor baker's validation and runtime GPU upload.</summary>
        public PerformerSurfaceBinding[] CopyBindings() => bindings != null
            ? (PerformerSurfaceBinding[])bindings.Clone()
            : Array.Empty<PerformerSurfaceBinding>();

        /// <summary>Rejects a different mesh or any topology change before GPU upload.</summary>
        public bool IsValidFor(SkinnedMeshRenderer renderer, out string reason)
        {
            if (renderer == null)
            {
                reason = "A SkinnedMeshRenderer is required for performer surface bindings.";
                return false;
            }

            Mesh mesh = renderer.sharedMesh;
            if (mesh == null)
            {
                reason = "The target SkinnedMeshRenderer has no shared mesh.";
                return false;
            }
            if (!mesh.isReadable)
            {
                reason = "The target shared mesh is not readable; rebake bindings after enabling Read/Write on the source mesh.";
                return false;
            }
            if (sourceMesh == null || mesh != sourceMesh)
            {
                reason = "The binding asset was baked for a different mesh. Re-bake it for the current SkinnedMeshRenderer.";
                return false;
            }
            if (mesh.vertexCount != sourceVertexCount)
            {
                reason = "The source mesh vertex count changed from " + sourceVertexCount + " to " + mesh.vertexCount + "; surface bindings are stale.";
                return false;
            }
            if (CountIndices(mesh) != sourceIndexCount)
            {
                reason = "The source mesh index count changed; surface bindings are stale.";
                return false;
            }
            string currentTopologyHash = ComputeTopologyHash(mesh);
            if (!string.Equals(currentTopologyHash, sourceTopologyHash, StringComparison.Ordinal))
            {
                reason = "The source mesh triangle topology changed without changing its counts; surface bindings are stale.";
                return false;
            }
            if (BindingCount != RequiredBindingCount)
            {
                reason = "The particle body requires exactly " + RequiredBindingCount + " stable surface bindings; this asset contains " + BindingCount + ".";
                return false;
            }
            if (Marshal.SizeOf<PerformerSurfaceBinding>() != PerformerSurfaceBinding.Stride)
            {
                reason = "PerformerSurfaceBinding must remain exactly 16 bytes for the VFX GraphicsBuffer.";
                return false;
            }

            reason = null;
            return true;
        }

#if UNITY_EDITOR
        public void ConfigureBaked(Mesh mesh, string meshIdentity, int vertexCount, int indexCount,
            string topologyHash, int seed, PerformerSurfaceBinding[] bakedBindings)
        {
            sourceMesh = mesh;
            sourceMeshIdentity = meshIdentity;
            sourceVertexCount = vertexCount;
            sourceIndexCount = indexCount;
            sourceTopologyHash = topologyHash;
            randomSeed = seed;
            bindings = bakedBindings ?? Array.Empty<PerformerSurfaceBinding>();
        }
#endif

        public static int CountIndices(Mesh mesh)
        {
            if (mesh == null) return 0;
            int count = 0;
            for (int submesh = 0; submesh < mesh.subMeshCount; submesh++)
                count = checked(count + (int)mesh.GetIndexCount(submesh));
            return count;
        }

        /// <summary>Stable FNV-1a hash over submesh topology and global triangle indices.</summary>
        public static string ComputeTopologyHash(Mesh mesh)
        {
            if (mesh == null) return string.Empty;

            unchecked
            {
                ulong hash = 14695981039346656037UL;
                Mix(ref hash, (uint)mesh.subMeshCount);
                for (int submesh = 0; submesh < mesh.subMeshCount; submesh++)
                {
                    Mix(ref hash, (uint)mesh.GetTopology(submesh));
                    int[] indices = mesh.GetIndices(submesh, true);
                    Mix(ref hash, (uint)indices.Length);
                    for (int i = 0; i < indices.Length; i++)
                        Mix(ref hash, (uint)indices[i]);
                }
                return hash.ToString("x16");
            }
        }

        private static void Mix(ref ulong hash, uint value)
        {
            unchecked
            {
                for (int shift = 0; shift < 32; shift += 8)
                {
                    hash ^= (byte)(value >> shift);
                    hash *= 1099511628211UL;
                }
            }
        }
    }
}
