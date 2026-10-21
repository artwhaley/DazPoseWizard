using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace DazPose.UnityValidation
{
    /// <summary>Read-only imported-asset measurements; no scene, material, rig or mesh edits.</summary>
    public static class DazFbxEvidenceInspector
    {
        private const string OutputDirectory = "TestOutput/appearance-evidence";
        private const double MovedThreshold = 1e-7; // Mesh-local Unity units; recorded in every report.

        [MenuItem("Tools/DAZ Pose/Development/Inspect Selected FBX Evidence")]
        public static void InspectSelected()
        {
            string path = AssetDatabase.GetAssetPath(Selection.activeObject);
            Write(path);
        }

        // Batch entry point also works in an isolated copy of the validation project.
        public static void InspectFirstOutfit()
        {
            Write("Assets/TestCharacter/lara.fbx");
            Write("Assets/TestCharacter/larafirstoutfit.fbx");
        }

        public static void InspectSecondOutfit()
        {
            Write("Assets/TestCharacter/larasecondoutfit.fbx");
            Write("Assets/TestCharacter/laracleanreference.fbx");
        }

        public static string Write(string assetPath)
        {
            if (!assetPath.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Select an imported FBX model asset.");
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            var importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;
            if (model == null || importer == null) throw new IOException("FBX is not imported: " + assetPath);
            // Imported prefab transforms can be read directly. No instantiation or active-scene mutation.
            var report = new EvidenceReport
            {
                assetPath = assetPath,
                assetGuid = AssetDatabase.AssetPathToGUID(assetPath),
                unityVersion = Application.unityVersion,
                sha256 = Hash(assetPath),
                movedThreshold = MovedThreshold,
                animationType = importer.animationType.ToString(),
                optimizeGameObjects = importer.optimizeGameObjects,
                importBlendShapes = importer.importBlendShapes,
                isReadable = importer.isReadable,
                globalScale = importer.globalScale,
                useFileScale = importer.useFileScale,
                materialImportMode = importer.materialImportMode.ToString(),
                skinWeights = importer.skinWeights.ToString(),
                maxBonesPerVertex = importer.maxBonesPerVertex,
                minBoneWeight = importer.minBoneWeight,
                qualitySkinWeights = QualitySettings.skinWeights.ToString(),
                hierarchy = model.GetComponentsInChildren<Transform>(true).Select(t => new Node
                {
                    path = PathOf(t, model.transform), name = t.name,
                    localPosition = t.localPosition, localRotation = t.localRotation, localScale = t.localScale,
                    matrixInModel = Matrix(model.transform.worldToLocalMatrix * t.localToWorldMatrix)
                }).ToArray(),
                renderers = model.GetComponentsInChildren<Renderer>(true)
                    .Select(r => InspectRenderer(r, model.transform)).ToArray()
            };
            string directory = Path.GetFullPath(OutputDirectory);
            Directory.CreateDirectory(directory);
            string output = Path.Combine(directory, Path.GetFileNameWithoutExtension(assetPath) + ".unity-fbx.json");
            File.WriteAllText(output, JsonUtility.ToJson(report, true) + "\n");
            Debug.Log("FBX_EVIDENCE_WRITTEN: " + output);
            return output;
        }

        private static RendererEvidence InspectRenderer(Renderer renderer, Transform root)
        {
            var skin = renderer as SkinnedMeshRenderer;
            var filter = renderer.GetComponent<MeshFilter>();
            Mesh mesh = skin != null ? skin.sharedMesh : filter != null ? filter.sharedMesh : null;
            var result = new RendererEvidence
            {
                path = PathOf(renderer.transform, root), type = renderer.GetType().Name,
                enabled = renderer.enabled, activeSelf = renderer.gameObject.activeSelf,
                rootBone = skin != null ? PathOf(skin.rootBone, root) : null,
                skinQuality = skin != null ? skin.quality.ToString() : null,
                materials = renderer.sharedMaterials.Select(InspectMaterial).ToArray()
            };
            if (mesh == null) return result;
            result.meshName = mesh.name;
            result.vertexCount = mesh.vertexCount;
            result.bounds = mesh.bounds;
            result.submeshes = Enumerable.Range(0, mesh.subMeshCount).Select(i => new Submesh
            {
                index = i, indexCount = mesh.GetIndexCount(i), topology = mesh.GetTopology(i).ToString()
            }).ToArray();
            result.bones = skin != null ? skin.bones.Select(b => PathOf(b, root)).ToArray() : Array.Empty<string>();
            result.bindPoses = mesh.bindposes.Select(p => new MatrixEvidence { values = Matrix(p) }).ToArray();
            var bonesPerVertex = mesh.GetBonesPerVertex();
            var weights = mesh.GetAllBoneWeights();
            try
            {
                result.totalInfluences = weights.Length;
                result.verticesWithoutWeights = bonesPerVertex.Count(v => v == 0);
                result.maximumInfluencesPerVertex = bonesPerVertex.Length == 0 ? 0 : bonesPerVertex.Max(v => (int)v);
            }
            finally { bonesPerVertex.Dispose(); weights.Dispose(); }
            var vertices = mesh.vertices;
            var delta = new Vector3[mesh.vertexCount];
            var shapes = new List<Shape>();
            for (int s = 0; s < mesh.blendShapeCount; s++)
            {
                var frames = new List<ShapeFrame>();
                for (int f = 0; f < mesh.GetBlendShapeFrameCount(s); f++)
                {
                    mesh.GetBlendShapeFrameVertices(s, f, delta, null, null);
                    int moved = 0;
                    double maximumSquared = 0, sumSquared = 0, movedSquared = 0;
                    Bounds bounds = default;
                    for (int v = 0; v < delta.Length; v++)
                    {
                        double square = (double)delta[v].x * delta[v].x + (double)delta[v].y * delta[v].y
                            + (double)delta[v].z * delta[v].z;
                        sumSquared += square;
                        maximumSquared = Math.Max(maximumSquared, square);
                        if (square <= MovedThreshold * MovedThreshold) continue;
                        if (moved == 0) bounds = new Bounds(vertices[v], Vector3.zero);
                        else bounds.Encapsulate(vertices[v]);
                        moved++;
                        movedSquared += square;
                    }
                    frames.Add(new ShapeFrame
                    {
                        frameWeight = mesh.GetBlendShapeFrameWeight(s, f), movedVertexCount = moved,
                        maximum = Math.Sqrt(maximumSquared),
                        rmsAllVertices = delta.Length == 0 ? 0 : Math.Sqrt(sumSquared / delta.Length),
                        rmsMovedVertices = moved == 0 ? 0 : Math.Sqrt(movedSquared / moved),
                        movedVertexBounds = bounds
                    });
                }
                shapes.Add(new Shape { index = s, name = mesh.GetBlendShapeName(s),
                    defaultWeight = skin != null ? skin.GetBlendShapeWeight(s) : 0, frames = frames.ToArray() });
            }
            result.blendShapes = shapes.ToArray();
            return result;
        }

        private static MaterialEvidence InspectMaterial(Material material)
        {
            if (material == null) return new MaterialEvidence { name = "<null>" };
            Shader shader = material.shader;
            var properties = new List<MaterialProperty>();
            for (int i = 0; shader != null && i < shader.GetPropertyCount(); i++)
            {
                string key = shader.GetPropertyName(i);
                var item = new MaterialProperty { name = key, type = shader.GetPropertyType(i).ToString() };
                switch (shader.GetPropertyType(i))
                {
                    case ShaderPropertyType.Texture:
                        Texture texture = material.GetTexture(key);
                        item.texturePath = texture != null ? AssetDatabase.GetAssetPath(texture) : "";
                        item.textureName = texture != null ? texture.name : "";
                        item.textureScale = material.GetTextureScale(key);
                        item.textureOffset = material.GetTextureOffset(key);
                        break;
                    case ShaderPropertyType.Color: item.vector = material.GetColor(key); break;
                    case ShaderPropertyType.Vector: item.vector = material.GetVector(key); break;
                    case ShaderPropertyType.Int: item.number = material.GetInteger(key); break;
                    default: item.number = material.GetFloat(key); break;
                }
                properties.Add(item);
            }
            return new MaterialEvidence
            {
                name = material.name, assetPath = AssetDatabase.GetAssetPath(material),
                shader = shader != null ? shader.name : "<null>", renderQueue = material.renderQueue,
                renderType = material.GetTag("RenderType", false, ""), keywords = material.shaderKeywords,
                properties = properties.ToArray()
            };
        }

        private static string PathOf(Transform value, Transform root)
        {
            if (value == null) return "<null>";
            var parts = new List<string>();
            for (Transform t = value; t != null && t != root; t = t.parent) parts.Add(t.name);
            parts.Reverse();
            return string.Join("/", parts);
        }

        private static float[] Matrix(Matrix4x4 matrix)
        {
            var values = new float[16];
            for (int i = 0; i < 16; i++) values[i] = matrix[i];
            return values;
        }

        private static string Hash(string path)
        {
            using var stream = File.OpenRead(path);
            using var hash = SHA256.Create();
            return BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }

        [Serializable] private class EvidenceReport
        {
            public int schemaVersion = 1;
            public string assetPath, assetGuid, unityVersion, sha256, animationType, materialImportMode;
            public string skinWeights, qualitySkinWeights;
            public int maxBonesPerVertex;
            public float minBoneWeight;
            public string measurementSpace = "mesh-local imported Unity units";
            public double movedThreshold;
            public bool optimizeGameObjects, importBlendShapes, isReadable, useFileScale;
            public float globalScale;
            public Node[] hierarchy;
            public RendererEvidence[] renderers;
        }
        [Serializable] private class Node
        {
            public string path, name;
            public Vector3 localPosition, localScale;
            public Quaternion localRotation;
            public float[] matrixInModel;
        }
        [Serializable] private class RendererEvidence
        {
            public string path, type, meshName, rootBone, skinQuality;
            public bool enabled, activeSelf;
            public int vertexCount, totalInfluences, verticesWithoutWeights, maximumInfluencesPerVertex;
            public Bounds bounds;
            public Submesh[] submeshes;
            public string[] bones;
            public MatrixEvidence[] bindPoses;
            public MaterialEvidence[] materials;
            public Shape[] blendShapes;
        }
        [Serializable] private class Submesh { public int index; public uint indexCount; public string topology; }
        [Serializable] private class MatrixEvidence { public float[] values; }
        [Serializable] private class Shape
        {
            public int index;
            public string name;
            public float defaultWeight;
            public ShapeFrame[] frames;
        }
        [Serializable] private class ShapeFrame
        {
            public float frameWeight;
            public int movedVertexCount;
            public double maximum, rmsAllVertices, rmsMovedVertices;
            public Bounds movedVertexBounds;
        }
        [Serializable] private class MaterialEvidence
        {
            public string name, assetPath, shader, renderType;
            public int renderQueue;
            public string[] keywords;
            public MaterialProperty[] properties;
        }
        [Serializable] private class MaterialProperty
        {
            public string name, type, texturePath, textureName;
            public float number;
            public Vector4 vector;
            public Vector2 textureScale, textureOffset;
        }
    }
}
