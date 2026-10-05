using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Daz3D;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace DazPose.UnityValidation
{
    // Local character assets remain in ignored TestData. Neither FBX nor vendor shaders are edited.
    public static class LaraCandidateBuilder
    {
        public const string Folder = "Assets/TestData/LaraCandidate";
        public const string PreviewScene = Folder + "/LaraCandidatePreview.unity";
        public const string MeshPath = Folder + "/LaraBody.asset";
        private const string ShaderFolder = "Assets/DazPose/Effects/Dissolve/Shaders/";
        private const float Tolerance = 2e-6f;
        [Serializable] public class Manifest
        {
            public string closedAsset, openAsset, firstAsset, closedSHA256, openSHA256, firstSHA256, originalSHA256, sourceDUF, dufSHA256;
            public Point[] points;
            public Frame[] frames;
            public Surface[] materials;
            public float breastsBakedValue;
            public string[] warnings;
        }
        [Serializable] public class Point { public int index, materialMask; public Vector3 position; public bool graftOnly; }
        [Serializable] public class Frame { public string name; public bool facial; public Entry[] entries; }
        [Serializable] public class Entry { public int index; public Vector3 delta, normal; }
        [Serializable] public class Surface { public int slot; public string node, surface, assetType; public bool iray; public Property[] properties; }
        [Serializable] public class Property { public string name, kind, text, texture; public double number; public Color color; }
        [Serializable] private class Audit
        {
            public int vertices, channels, canonicalChannels, facialChannels, matchedVertices, materialSlots;
            public float maximumPointError;
            public bool facialGraftMovementZero, canonicalChannelOrderPreserved, torsoInputsEqual;
            public string normalRecipe = "Area-weighted target geometry minus base geometry, grouped by welded source point; imported rest normals preserved";
            public string[] shaders, warnings;
        }
        public static Manifest ReadManifest()
        {
            var manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(Folder + "/manifest.json"));
            if (Hash(manifest.closedAsset) != manifest.closedSHA256 || Hash(manifest.openAsset) != manifest.openSHA256
                || Hash(manifest.firstAsset) != manifest.firstSHA256 || Hash("Assets/TestCharacter/lara.fbx") != manifest.originalSHA256
                || Hash(manifest.sourceDUF) != manifest.dufSHA256)
                throw new IOException("Lara candidate sources changed. Run scripts/prepare-lara-candidate.py again.");
            return manifest;
        }

        [MenuItem("Tools/DAZ Pose/Development/Open Lara Candidate Preview")]
        public static void OpenPreview()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Build();
            var body = UnityEngine.Object.FindObjectsByType<SkinnedMeshRenderer>().Single(r => r.enabled);
            Selection.activeGameObject = body.gameObject;
            LaraCandidateWindow.ShowFor(body);
            var view = SceneView.lastActiveSceneView ?? EditorWindow.GetWindow<SceneView>();
            view.sceneLighting = false;
            view.LookAtDirect(new Vector3(0, 1.05f, 0), Quaternion.Euler(0, 180, 0), 1.05f);
        }

        // Headless generation/validation entry, without changing FirstPerformanceVoid.
        public static void Build()
        {
            Manifest manifest = ReadManifest();
            var sourceObject = AssetDatabase.LoadAssetAtPath<GameObject>(manifest.closedAsset);
            var sourceBody = Body(sourceObject);
            var original = Body(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/TestCharacter/lara.fbx")).sharedMesh;
            Mesh source = sourceBody.sharedMesh;
            var audit = new Audit { vertices = source.vertexCount, channels = manifest.frames.Length,
                canonicalChannels = original.blendShapeCount, facialGraftMovementZero = true,
                canonicalChannelOrderPreserved = true, warnings = manifest.warnings };
            int[] pointIndices = MapPoints(source, manifest.points, manifest.frames, audit);
            Mesh candidate = ReadableCopy(source);
            candidate.name = "Lara body with graft and facial morphs";
            candidate.ClearBlendShapes();
            Vector3[] basePositions = source.vertices;
            int[] triangles = source.triangles;
            Vector3[] baseSmoothNormals = SmoothPointNormals(basePositions,triangles,pointIndices,manifest.points.Length);
            var frames = manifest.frames.ToDictionary(f => f.name);
            var ordered = Enumerable.Range(0, original.blendShapeCount).Select(original.GetBlendShapeName)
                .Concat(manifest.frames.Where(f => original.GetBlendShapeIndex(f.name) < 0).Select(f => f.name)).ToArray();
            foreach (string name in ordered)
            {
                Frame frame = frames[name];
                var entries = frame.entries.ToDictionary(e => e.index);
                var positions = new Vector3[source.vertexCount];
                var normals = new Vector3[source.vertexCount];
                for (int i = 0; i < positions.Length; i++)
                {
                    if (entries.TryGetValue(pointIndices[i], out var entry)) positions[i] = entry.delta;
                    if (frame.facial && manifest.points[pointIndices[i]].graftOnly && (positions[i] != Vector3.zero || normals[i] != Vector3.zero))
                        throw new IOException("A facial frame still changes the graft: " + name);
                }
                // Raw FBX shape normal arrays produced grey surfaces under deformation.
                // Derive changes from the actual target geometry. Group UV/material splits
                // by their welded source point so they receive identical smoothing changes.
                var targetPositions = basePositions.Select((v,i) => v+positions[i]).ToArray();
                Vector3[] targetSmoothNormals = SmoothPointNormals(targetPositions,triangles,pointIndices,manifest.points.Length);
                for (int i = 0; i < normals.Length; i++)
                {
                    normals[i] = frame.facial && manifest.points[pointIndices[i]].graftOnly ? Vector3.zero
                        : targetSmoothNormals[i]-baseSmoothNormals[i];
                    if (!float.IsFinite(normals[i].x) || !float.IsFinite(normals[i].y) || !float.IsFinite(normals[i].z))
                        throw new IOException("Invalid derived normal in " + name);
                }
                candidate.AddBlendShapeFrame(name, 100, positions, normals, null);
                var stored = new Vector3[positions.Length];
                candidate.GetBlendShapeFrameVertices(candidate.blendShapeCount - 1, 0, stored, null, null);
                for (int i = 0; i < stored.Length; i++)
                    if (stored[i] != positions[i]) throw new IOException("Stored frame differs: " + name);
                if (frame.facial) audit.facialChannels++;
            }
            for (int i = 0; i < original.blendShapeCount; i++)
                if (candidate.GetBlendShapeName(i) != original.GetBlendShapeName(i)) throw new IOException("Canonical morph indices changed.");
            candidate = Save(candidate, MeshPath);
            Material[] materials = ConvertMaterials(manifest);
            audit.materialSlots = materials.Length;
            audit.shaders = materials.Select(m => m.shader.name).ToArray();
            foreach (string property in new[] { "_DiffuseMap", "_NormalMap", "_HeightMap" })
                if (materials[0].GetTexture(property) != materials[16].GetTexture(property)) throw new IOException("Torso maps differ at graft seam: " + property);
            audit.torsoInputsEqual = true;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var preview = UnityEngine.Object.Instantiate(sourceObject);
            preview.name = "Lara Candidate";
            var body = Body(preview);
            foreach (var renderer in preview.GetComponentsInChildren<Renderer>(true)) renderer.enabled = renderer == body;
            body.sharedMesh = candidate;
            body.sharedMaterials = materials;
            body.quality = SkinQuality.Auto;
            for (int i = 0; i < candidate.blendShapeCount; i++) body.SetBlendShapeWeight(i, 0);
            EditorSceneManager.SaveScene(scene, PreviewScene);
            Directory.CreateDirectory("TestOutput/appearance-evidence");
            File.WriteAllText("TestOutput/appearance-evidence/lara-candidate.validation.json", JsonUtility.ToJson(audit, true));
            AssetDatabase.SaveAssets();
            Debug.Log("LARA_CANDIDATE_VALIDATED: " + JsonUtility.ToJson(audit));
        }
        internal static Vector3[] SmoothPointNormals(Vector3[] vertices,int[] triangles,int[] points,int pointCount)
        {
            var sums = new Vector3[pointCount];
            for (int i=0;i<triangles.Length;i+=3)
            {
                int a=triangles[i],b=triangles[i+1],c=triangles[i+2];
                Vector3 area = Vector3.Cross(vertices[b]-vertices[a],vertices[c]-vertices[a]);
                sums[points[a]] += area; sums[points[b]] += area; sums[points[c]] += area;
            }
            for (int i=0;i<sums.Length;i++) sums[i] = sums[i].normalized;
            return points.Select(p => sums[p]).ToArray();
        }

        private static int[] MapPoints(Mesh source, Point[] points, Frame[] frames, Audit audit)
        {
            Vector3[] vertices = source.vertices;
            var masks = new int[vertices.Length];
            for (int s = 0; s < source.subMeshCount; s++) foreach (int i in source.GetIndices(s)) masks[i] |= 1 << s;
            var grid = new Dictionary<Vector3Int, List<Point>>();
            foreach (Point p in points)
            {
                if (p.index < 0 || p.index >= points.Length || points[p.index] != p) throw new IOException("Noncanonical point indices.");
                var key = Cell(p.position);
                if (!grid.TryGetValue(key, out var list)) grid[key] = list = new List<Point>();
                list.Add(p);
            }
            var result = new int[vertices.Length];
            var frameMaps = frames.Select(f => f.entries.ToDictionary(e => e.index)).ToArray();
            for (int i = 0; i < vertices.Length; i++)
            {
                var candidates = new List<Point>();
                var center = Cell(vertices[i]);
                for (int x = -1; x <= 1; x++) for (int y = -1; y <= 1; y++) for (int z = -1; z <= 1; z++)
                    if (grid.TryGetValue(center + new Vector3Int(x,y,z), out var list))
                        candidates.AddRange(list.Where(p => (p.position-vertices[i]).sqrMagnitude <= Tolerance*Tolerance && (p.materialMask & masks[i]) == masks[i]));
                if (candidates.Count == 0) throw new IOException("Raw point correspondence is missing at " + i);
                Point match = candidates.OrderBy(p => (p.position-vertices[i]).sqrMagnitude).First();
                // Coincident raw points are safe only if every transferred channel and region agree.
                foreach (Point other in candidates)
                {
                    if (other.graftOnly != match.graftOnly) throw new IOException("Ambiguous graft ownership at " + i);
                    foreach (var frameMap in frameMaps)
                    {
                        frameMap.TryGetValue(match.index,out Entry a);
                        frameMap.TryGetValue(other.index,out Entry b);
                        if (((a?.delta ?? Vector3.zero)-(b?.delta ?? Vector3.zero)).sqrMagnitude > 1e-12f
                            || ((a?.normal ?? Vector3.zero)-(b?.normal ?? Vector3.zero)).sqrMagnitude > 1e-8f)
                            throw new IOException("Coincident points have different morph deltas at " + i);
                    }
                }
                result[i] = match.index;
                audit.matchedVertices++;
                audit.maximumPointError = Mathf.Max(audit.maximumPointError, (match.position-vertices[i]).magnitude);
            }
            return result;
        }

        public static Material[] ConvertMaterials(Manifest manifest,string destination = Folder,bool applySkinResponse = true,bool stableSurfaceNames = false)
        {
            Directory.CreateDirectory(destination + "/Textures");
            Directory.CreateDirectory(destination + "/TextureSources");
            Directory.CreateDirectory(destination + "/SourceMaterials");
            Directory.CreateDirectory(destination + "/RuntimeMaterials");
            AssetDatabase.Refresh();
            var converter = new DTU { DTUPath = Folder + "/manifest.json" };
            bool legacy = Daz3DDTUImporter.UseLegacyShaders;
            var results = new Material[manifest.materials.Length];
            try
            {
                Daz3DDTUImporter.UseLegacyShaders = false;
                foreach (Surface surface in manifest.materials)
                {
                    var properties = new List<DTUMaterialProperty>();
                    foreach (Property input in surface.properties)
                    {
                        var value = new DTUValue(input.number);
                        if (input.kind == "color") { value.Type = DTUValue.DataType.Color; value.AsColor = input.color; }
                        if (input.kind == "string") { value.Type = DTUValue.DataType.String; value.AsString = input.text; }
                        string texture = input.texture;
                        if (!string.IsNullOrEmpty(texture))
                        {
                            if (!File.Exists(texture)) throw new IOException("Missing texture " + texture);
                            string role = input.name == "Normal Map" ? "normal" : input.kind == "color" ? "color" : "data";
                            string staged = destination + "/TextureSources/" + role + "_" + Hash(texture).Substring(0,16) + Path.GetExtension(texture);
                            if (!File.Exists(staged)) File.Copy(texture, staged);
                            texture = Path.GetFullPath(staged);
                        }
                        properties.Add(new DTUMaterialProperty { Name = input.name, Value = value, Texture = texture ?? "", Exists = true });
                    }
                    var inputMaterial = new DTUMaterial { AssetName = surface.node, MaterialName = surface.surface,
                        MaterialType = surface.iray ? "Iray Uber" : "Daz Studio Default", Value = surface.assetType ?? "Actor/Character",
                        Properties = properties, ProductName = "Lara", ProductComponentName = surface.node };
                    Material converted = surface.iray ? converter.ConvertToUnityIrayUber(inputMaterial, destination + "/Textures")
                        : converter.ConvertToUnityDazStudioDefault(inputMaterial, destination + "/Textures");
                    if (converted == null) throw new IOException("Bridge could not convert " + surface.node + "/" + surface.surface);
                    string family = converted.shader.name;
                    string owned = family == "Daz3D/Wet" ? "Wet Dissolve.shadergraph"
                        : family == "Daz3D/uDTU HDRP.SSS" ? "uDTU HDRP SSS Dissolve.shadergraph"
                        : family == "Daz3D/uDTU HDRP.Specular" ? "uDTU HDRP Specular Dissolve.shadergraph"
                        : family == "Daz3D/uDTU HDRP.Metallic" ? "uDTU HDRP Metallic Dissolve.shadergraph"
                        : family == "Daz3D/uDTU HDRP.Hair" ? "uDTU HDRP Hair Dissolve.shadergraph" : null;
                    if (owned == null) throw new IOException("No owned dissolve variant for " + family);
                    if(!applySkinResponse && converted.HasProperty("_SurfaceType") && converted.GetFloat("_SurfaceType")>.5f
                        && (family=="Daz3D/uDTU HDRP.Metallic" || family=="Daz3D/uDTU HDRP.Specular"))
                        owned=owned.Replace(" Dissolve"," Transparent Dissolve");
                    RepairOpacityTexture(converted);
                    converted.name = (stableSurfaceNames ? "M" : surface.slot.ToString("D2")) + "_" + surface.node + "_" + surface.surface;
                    string materialName=converted.name;
                    string materialFile=stableSurfaceNames?WardrobeImporter.Safe(converted.name):converted.name;
                    string sourcePath=destination+"/SourceMaterials/"+materialFile+".mat";
                    var previousSource=stableSurfaceNames?AssetDatabase.LoadAssetAtPath<Material>(sourcePath):null;
                    var baseline=previousSource!=null?UnityEngine.Object.Instantiate(previousSource):null;
                    converted = Save(converted, sourcePath);
                    converted.name=materialName;
                    EditorUtility.SetDirty(converted);
                    string runtimePath = destination + "/RuntimeMaterials/" + materialFile + ".mat";
                    Material runtime = AssetDatabase.LoadAssetAtPath<Material>(runtimePath);
                    // Source regeneration is separate; existing runtime assets preserve artistic edits.
                    if (runtime == null)
                    {
                        runtime = UnityEngine.Object.Instantiate(converted);
                        runtime.shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderFolder + owned);
                        if (runtime.shader == null) throw new IOException("Missing owned shader " + owned);
                        runtime.SetFloat("_DissolveEnabled", 0);
                        runtime.SetFloat("_DissolveProgress", 0);
                        if (runtime.HasProperty("_AlphaCutoffEnable")) runtime.SetFloat("_AlphaCutoffEnable", 1);
                        runtime.EnableKeyword("_ALPHATEST_ON");
                        if(owned.Contains("Transparent"))UnityEditor.Rendering.HighDefinition.HDShaderUtils.ResetMaterialKeywords(runtime);
                        if (applySkinResponse) ApplyAcceptedSkinSettings(runtime,surface.slot);
                        runtime = Save(runtime, runtimePath);
                    }
                    if(stableSurfaceNames && runtime.name!=materialName){runtime.name=materialName;EditorUtility.SetDirty(runtime);}
                    if(baseline!=null)
                    {
                        MergeUneditedMaterialProperties(baseline,converted,runtime);
                        UnityEngine.Object.DestroyImmediate(baseline);
                    }
                    if(stableSurfaceNames)
                    {
                        string graph=owned;
                        if(runtime.HasProperty("_SurfaceType") && runtime.GetFloat("_SurfaceType")<.5f)graph=graph.Replace(" Transparent Dissolve"," Dissolve");
                        if(runtime.HasProperty("_SurfaceType") && runtime.GetFloat("_SurfaceType")>.5f && !graph.Contains("Transparent") && (graph.Contains("Metallic")||graph.Contains("Specular")))graph=graph.Replace(" Dissolve"," Transparent Dissolve");
                        runtime.shader=AssetDatabase.LoadAssetAtPath<Shader>(ShaderFolder+graph);
                        UnityEditor.Rendering.HighDefinition.HDShaderUtils.ResetMaterialKeywords(runtime);
                        EditorUtility.SetDirty(runtime);
                    }
                    foreach (string property in new[] { "_DissolveEnabled", "_DissolveProgress", "_DissolveBoundsMin", "_DissolveBoundsSize",
                        "_DissolveFieldParams", "_DissolveEdgeWidth", "_DissolveEdgeColor", "_DissolveEdgeEmission" })
                        if (!runtime.HasProperty(property)) throw new IOException("Dissolve contract missing: " + converted.name + "/" + property);
                    if (ShaderUtil.ShaderHasError(runtime.shader)) throw new IOException("Shader compile error: " + runtime.shader.name);
                    results[surface.slot] = runtime;
                }
            }
            finally { Daz3DDTUImporter.UseLegacyShaders = legacy; }
            return results;
        }
        // Source changes flow through only where the artist has retained the old source value.
        // This permits updated texture maps without resetting an adjusted opacity/roughness.
        internal static void MergeUneditedMaterialProperties(Material oldSource,Material newSource,Material runtime)
        {
            var shader=newSource.shader;
            for(int i=0;i<shader.GetPropertyCount();i++)
            {
                string property=shader.GetPropertyName(i);
                if(!oldSource.HasProperty(property)||!runtime.HasProperty(property))continue;
                switch(shader.GetPropertyType(i))
                {
                    case ShaderPropertyType.Float:case ShaderPropertyType.Range:
                        if(Mathf.Approximately(runtime.GetFloat(property),oldSource.GetFloat(property)))runtime.SetFloat(property,newSource.GetFloat(property));break;
                    case ShaderPropertyType.Int:
                        if(runtime.GetInteger(property)==oldSource.GetInteger(property))runtime.SetInteger(property,newSource.GetInteger(property));break;
                    case ShaderPropertyType.Color:
                        if(runtime.GetColor(property)==oldSource.GetColor(property))runtime.SetColor(property,newSource.GetColor(property));break;
                    case ShaderPropertyType.Vector:
                        if(runtime.GetVector(property)==oldSource.GetVector(property))runtime.SetVector(property,newSource.GetVector(property));break;
                    case ShaderPropertyType.Texture:
                        if(runtime.GetTexture(property)==oldSource.GetTexture(property))runtime.SetTexture(property,newSource.GetTexture(property));
                        if(runtime.GetTextureScale(property)==oldSource.GetTextureScale(property))runtime.SetTextureScale(property,newSource.GetTextureScale(property));
                        if(runtime.GetTextureOffset(property)==oldSource.GetTextureOffset(property))runtime.SetTextureOffset(property,newSource.GetTextureOffset(property));break;
                }
            }
            EditorUtility.SetDirty(runtime);
        }
        internal static void RepairOpacityTexture(Material material)
        {
            if(!material.HasProperty("_AlphaMap"))return;
            var texture=material.GetTexture("_AlphaMap");if(texture==null)return;
            var importer=AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(texture)) as TextureImporter;
            // Preserve dedicated grayscale opacity in both RGB and alpha for
            // shader families that sample either channel. Metallic currently uses R.
            if(importer==null || importer.DoesSourceTextureHaveAlpha() || importer.alphaSource==TextureImporterAlphaSource.FromGrayScale)return;
            importer.alphaSource=TextureImporterAlphaSource.FromGrayScale;importer.sRGBTexture=false;importer.SaveAndReimport();
        }
        internal static Material[] MatchImportedSurfaces(Renderer source,Material[] converted)
        {
            if(source.sharedMaterials.Length!=converted.Length)throw new IOException("Imported surface count differs: "+source.name);
            return source.sharedMaterials.Select(s=>converted.Single(m=>m.name.EndsWith("_"+s.name,StringComparison.Ordinal))).ToArray();
        }
        private static void ApplyAcceptedSkinSettings(Material runtime,int slot)
        {
            // Copy only the accepted skin response; keep this character's maps and colors.
            int referenceSlot = slot == 16 ? 0 : slot;
            if (!new[] { 0,1,2,4,5,6,8 }.Contains(referenceSlot)) return;
            string prefix = "Assets/DazPose/Effects/Dissolve/LaraRuntimeMaterials/M_Lara_Runtime_" + referenceSlot.ToString("D2") + "_";
            var reference = AssetDatabase.FindAssets("t:Material",new[] { "Assets/DazPose/Effects/Dissolve/LaraRuntimeMaterials" })
                .Select(AssetDatabase.GUIDToAssetPath).Single(p => p.StartsWith(prefix,StringComparison.Ordinal));
            var accepted = AssetDatabase.LoadAssetAtPath<Material>(reference);
            foreach (string property in new[] { "_Roughness", "_SpecularLobe1Roughness", "_SpecularLobe2Roughness",
                "_GlossyLayeredWeight", "_DualLobeSpecularWeight", "_DualLobeSpecularRatio", "_TopCoatWeight", "_TopCoatRoughness" })
                if (runtime.HasProperty(property) && accepted.HasProperty(property)) runtime.SetFloat(property,accepted.GetFloat(property));
            EditorUtility.SetDirty(runtime);
        }
        [MenuItem("Tools/DAZ Pose/Development/Copy Accepted Lara Skin Response")]
        public static void CopyAcceptedSkinResponse()
        {
            foreach (var surface in ReadManifest().materials)
            {
                var runtime = AssetDatabase.LoadAssetAtPath<Material>(Folder + "/RuntimeMaterials/" + surface.slot.ToString("D2")
                    + "_" + surface.node + "_" + surface.surface + ".mat");
                if (runtime == null) throw new IOException("Build Lara materials first.");
                ApplyAcceptedSkinSettings(runtime,surface.slot);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("LARA_ACCEPTED_SKIN_RESPONSE_COPIED");
        }
        internal static SkinnedMeshRenderer Body(GameObject root) => root.GetComponentsInChildren<SkinnedMeshRenderer>(true)
            .Single(r => r.sharedMesh != null && r.sharedMesh.name.StartsWith("Genesis8Female", StringComparison.Ordinal));
        internal static Mesh ReadableCopy(Mesh source)
        {
            var result = new Mesh { name = source.name, indexFormat = source.indexFormat,
                vertices = source.vertices, normals = source.normals, tangents = source.tangents,
                colors32 = source.colors32, bindposes = source.bindposes };
            for (int channel = 0; channel < 8; channel++)
            {
                var attribute = (VertexAttribute)((int)VertexAttribute.TexCoord0+channel);
                if (!source.HasVertexAttribute(attribute)) continue;
                int dimension = source.GetVertexAttributeDimension(attribute);
                if (dimension == 2) { var uv = new List<Vector2>(); source.GetUVs(channel,uv); result.SetUVs(channel,uv); }
                else if (dimension == 3) { var uv = new List<Vector3>(); source.GetUVs(channel,uv); result.SetUVs(channel,uv); }
                else { var uv = new List<Vector4>(); source.GetUVs(channel,uv); result.SetUVs(channel,uv); }
            }
            var influences = source.GetBonesPerVertex();
            var weights = source.GetAllBoneWeights();
            try { if(influences.Length==source.vertexCount && weights.Length>0)result.SetBoneWeights(influences,weights); }
            finally { influences.Dispose(); weights.Dispose(); }
            result.subMeshCount = source.subMeshCount;
            for (int s = 0; s < source.subMeshCount; s++) result.SetIndices(source.GetIndices(s,true),source.GetTopology(s),s,false);
            result.bounds = source.bounds;
            return result;
        }
        internal static T Save<T>(T generated, string path) where T : UnityEngine.Object
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing == null) { AssetDatabase.CreateAsset(generated, path); return generated; }
            EditorUtility.CopySerialized(generated, existing);
            UnityEngine.Object.DestroyImmediate(generated);
            EditorUtility.SetDirty(existing);
            return existing;
        }
        private static Vector3Int Cell(Vector3 p) => new Vector3Int(Mathf.FloorToInt(p.x/Tolerance), Mathf.FloorToInt(p.y/Tolerance), Mathf.FloorToInt(p.z/Tolerance));
        private static string Hash(string path)
        {
            using var algorithm = SHA256.Create(); using var stream = File.OpenRead(path);
            return BitConverter.ToString(algorithm.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }
    }

    public sealed class LaraCandidateWindow : EditorWindow
    {
        private SkinnedMeshRenderer body;
        private float opening, blink, mouth, nipples, breasts = .5555556f, breastsBakedValue = .5555556f, dissolve;
        public static void ShowFor(SkinnedMeshRenderer renderer)
        {
            var window = GetWindow<LaraCandidateWindow>("Lara Candidate");
            window.body = renderer;
            window.breastsBakedValue = LaraCandidateBuilder.ReadManifest().breastsBakedValue;
            window.breasts = window.breastsBakedValue;
            window.Show();
        }
        [MenuItem("Tools/DAZ Pose/Development/Lara Candidate Controls")]
        public static void Connect()
        {
            var renderer = Selection.activeGameObject == null ? null : Selection.activeGameObject.GetComponent<SkinnedMeshRenderer>();
            if (renderer == null) throw new InvalidOperationException("Select Lara's body renderer.");
            ShowFor(renderer);
        }
        private void OnGUI()
        {
            body = (SkinnedMeshRenderer)EditorGUILayout.ObjectField("Body", body, typeof(SkinnedMeshRenderer), true);
            if (body == null) return;
            EditorGUI.BeginChangeCheck();
            opening = EditorGUILayout.Slider("Captured opening", opening, 0, 100);
            blink = EditorGUILayout.Slider("Left blink", blink, 0, 100);
            mouth = EditorGUILayout.Slider("Speech AA", mouth, 0, 100);
            nipples = EditorGUILayout.Slider("Nipples", nipples, 0, 100);
            breasts = EditorGUILayout.Slider("Lara breasts (mesh preview)", breasts, 0, 1);
            using (new EditorGUI.DisabledScope(Application.isPlaying))
                dissolve = EditorGUILayout.Slider("Dissolve", dissolve, 0, 1);
            EditorGUILayout.HelpBox("Breasts adjust the saved Daz value; joint ERC is not included yet. In Play Mode use the existing scene controls for effects. Runtime materials preserve edits on regeneration.", MessageType.Info);
            if (!EditorGUI.EndChangeCheck()) return;
            Set("CapturedOpening", opening);
            Set("Genesis8Female__eCTRLEyesClosedL", blink);
            Set("Genesis8Female__eCTRLvAA", mouth);
            Set("Genesis8Female__PBMNipples", nipples);
            Set("LaraBreastsAdjustment", (breasts-breastsBakedValue)*100);
            if (Application.isPlaying && body.GetComponentInParent<DazPose.Performer.LaraAnatomyControls>() is var anatomy && anatomy != null)
            {
                anatomy.CapturedOpening = opening/100;
                anatomy.Nipples = nipples/100;
                anatomy.LaraBreastsMeshPreview = breasts;
                anatomy.Apply();
            }
            if (Application.isPlaying) { SceneView.RepaintAll(); return; }
            var block = new MaterialPropertyBlock(); body.GetPropertyBlock(block);
            block.SetFloat("_DissolveEnabled", dissolve > 0 ? 1 : 0);
            block.SetFloat("_DissolveProgress", dissolve);
            block.SetVector("_DissolveBoundsMin", body.localBounds.min);
            block.SetVector("_DissolveBoundsSize", body.localBounds.size);
            block.SetVector("_DissolveFieldParams", new Vector4(3.5f,.85f,17f,1.15f));
            block.SetFloat("_DissolveEdgeWidth", .035f);
            block.SetColor("_DissolveEdgeColor", new Color(3,.06f,4,1));
            block.SetFloat("_DissolveEdgeEmission", 4);
            body.SetPropertyBlock(block); SceneView.RepaintAll();
        }
        private void Set(string name, float value)
        {
            int i = body.sharedMesh.GetBlendShapeIndex(name);
            if (i < 0) throw new InvalidOperationException("Missing morph " + name);
            body.SetBlendShapeWeight(i,value);
        }
    }
}
