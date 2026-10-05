using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Daz3D;
using DazPose.UnityValidation.Partner;
using Unity.Collections;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DazPose.UnityValidation.Editor.Partner
{
    /// <summary>Prepare the existing partner without replacing its scene or camera.</summary>
    public static class PartnerCharacterPreparation
    {
        public const string Folder = "Assets/TestData/PartnerCharacter";
        public const string PrefabPath = "Assets/PartnerProof/PartnerCharacter.prefab";
        private const string ShellPath = "Assets/PartnerProof/Generated/G8M_ErectShell.asset";
        private const string ScenePath = "Assets/Scenes/PartnerRigAcceptance.unity";
        private const string Shape = "ShellErectionToFlaccid";
        private const float MappingTolerance = 0.000002f;
        private const float EndpointTolerance = 0.00001f;

        [Serializable] internal sealed class Manifest
        {
            public string erectAsset, flaccidAsset, erectSHA256, flaccidSHA256, sourceDUF, dufSHA256, topologyProof;
            public PointPair[] shellControlPoints;
            public Binding[] bindings;
            public int sourceTextureCount;
        }
        [Serializable] internal sealed class PointPair
        {
            public int shellIndex, bodyIndex;
            public Vector3 shellPosition, bodyPosition;
        }
        [Serializable] internal sealed class Binding
        {
            public string mesh, fbxMaterial, surface;
            public int slot, material;
        }
        [Serializable] internal sealed class Audit
        {
            public int sourceControlPointPairs, shellUnityVertices, weightedVertices, boneCount, maximumInfluences;
            public int materialSlots, sourceTextures;
            public float maximumMappingErrorMm, maximumErectErrorMm, maximumFlaccidErrorMm, rmsErectErrorMm, rmsFlaccidErrorMm;
            public float maximumBendSurfaceGapMm, smallestTestedShellMovementMm;
            public bool copiedWeightsExactly, endpointsPreserved, shellVisibleDuringBend, sceneSaved;
            public bool materialTexturesVerified, prefabReferencesVerified;
            public string prefab, scene, sourceDUF, sourceDUFSHA256, runtimeWeightCap, glossReference;
            public string[] materialShaders, appearanceLimits;
        }
        internal static Audit LastAudit { get; private set; }

        internal static Manifest ReadManifest()
        {
            var manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(Folder + "/manifest.json"));
            Require(Hash(manifest.erectAsset) == manifest.erectSHA256 && Hash(manifest.flaccidAsset) == manifest.flaccidSHA256,
                "Partner endpoint sources changed; rerun scripts/prepare-partner-character.py.");
            Require(Hash(manifest.sourceDUF) == manifest.dufSHA256,
                "Partner DUF changed; rerun scripts/prepare-partner-character.py.");
            Require(manifest.shellControlPoints.Length == 2205, "Expected the verified 2,205 shell/body control-point pairs.");
            return manifest;
        }

        [MenuItem("Tools/DAZ Pose/Partner Proof/Prepare Skinned Character and Materials")]
        public static void Prepare()
        {
            Require(!EditorApplication.isPlayingOrWillChangePlaymode, "Prepare partner assets outside Play mode.");
            Manifest manifest = ReadManifest();
            Scene existingScene = SceneManager.GetSceneByPath(ScenePath);
            bool previouslyDirty = existingScene.IsValid() && existingScene.isDirty;
            Scene preview = EditorSceneManager.NewPreviewScene();
            GameObject erect = null, flaccid = null;
            try
            {
                erect = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(manifest.erectAsset));
                flaccid = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(manifest.flaccidAsset));
                SceneManager.MoveGameObjectToScene(erect, preview);
                SceneManager.MoveGameObjectToScene(flaccid, preview);
                SkinnedMeshRenderer body = SourceBody(erect);
                MeshFilter shell = SourceShell(erect);
                MeshFilter otherShell = SourceShell(flaccid);
                Mesh generated = CreateSkinnedShell(body, shell.sharedMesh, shell.transform, otherShell.sharedMesh, otherShell.transform);
                Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(ShellPath);
                Require(existing != null, "Build the artist-authored erection proof before preparing its character.");
                EditorUtility.CopySerialized(generated, existing);
                UnityEngine.Object.DestroyImmediate(generated);
                EditorUtility.SetDirty(existing);
                AssetDatabase.SaveAssets();
                Directory.CreateDirectory("TestOutput/PartnerCharacter");
                File.WriteAllText("TestOutput/PartnerCharacter/SkinTransfer.json", JsonUtility.ToJson(LastAudit, true) + "\n");
                Material[] materials = BuildMaterials();
                AssetDatabase.SaveAssets();
                PatchAcceptanceScene(existing, materials, manifest, preview, previouslyDirty);
                LastAudit.materialShaders = materials.Select(m => m.shader.name).Distinct().ToArray();
                LastAudit.materialSlots = materials.Length;
                LastAudit.sourceTextures = manifest.sourceTextureCount;
                LastAudit.materialTexturesVerified = true;
                LastAudit.sourceDUF = manifest.sourceDUF;
                LastAudit.sourceDUFSHA256 = manifest.dufSHA256;
                LastAudit.runtimeWeightCap = QualitySettings.skinWeights.ToString();
                LastAudit.glossReference = "Accepted Lara runtime skin: roughness/lobe roughness, glossy/dual-lobe/top-coat weights and ratio; partner maps and colors retained.";
                LastAudit.appearanceLimits = new[] {
                    "Bridge conversion approximates Iray; final appearance awaits user review.",
                    "Shell uses authored opacity coverage with an alpha-blended bridge specular graph and reduced gloss.",
                    "Skin weights follow bones. A future mesh squeeze needs corresponding shell vertex deltas/morphs.",
                    "Contact-frame samples remain the existing endpoint-derived line; they do not follow procedural bending yet." };
                Directory.CreateDirectory("TestOutput/PartnerCharacter");
                File.WriteAllText("TestOutput/PartnerCharacter/Preparation.json", JsonUtility.ToJson(LastAudit, true) + "\n");
                AssetDatabase.SaveAssets();
                Debug.Log("PARTNER_CHARACTER_PREPARED: " + PrefabPath + " ; report=TestOutput/PartnerCharacter/Preparation.json");
            }
            finally
            {
                if (erect != null) UnityEngine.Object.DestroyImmediate(erect);
                if (flaccid != null) UnityEngine.Object.DestroyImmediate(flaccid);
                EditorSceneManager.ClosePreviewScene(preview);
            }
        }

        internal static Mesh CreateSkinnedShell(SkinnedMeshRenderer body, Mesh shell, Transform shellTransform,
            Mesh flaccidShell, Transform flaccidTransform)
        {
            Manifest manifest = ReadManifest();
            Require(shell.vertexCount == flaccidShell.vertexCount, "Shell endpoint vertex correspondence changed.");
            for (int i = 0; i < shell.subMeshCount; i++)
                Require(shell.GetIndices(i).SequenceEqual(flaccidShell.GetIndices(i)), "Shell endpoint triangle ordering changed.");
            Vector3[] bodyVertices = body.sharedMesh.vertices, shellVertices = shell.vertices;
            BoneWeight1[][] sourceWeights = Weights(body.sharedMesh);
            int[][] bodyCandidates = manifest.shellControlPoints.Select(p => Matches(bodyVertices, p.bodyPosition)).ToArray();
            var shellToBody = new int[shellVertices.Length];
            var counts = new byte[shellVertices.Length];
            var weights = new List<BoneWeight1>();
            var audit = new Audit { sourceControlPointPairs = manifest.shellControlPoints.Length,
                shellUnityVertices = shellVertices.Length, boneCount = body.bones.Length, copiedWeightsExactly = true };
            Vector3[] rawShellPositions = manifest.shellControlPoints.Select(p => p.shellPosition).ToArray();
            for (int i = 0; i < shellVertices.Length; i++)
            {
                int[] matches = Matches(rawShellPositions, shellVertices[i]);
                Require(matches.Length == 1, "Ambiguous or missing raw shell control point for Unity vertex " + i);
                int raw = matches[0];
                int[] candidates = bodyCandidates[raw];
                Require(candidates.Length > 0, "Underlying body vertex is missing for shell control point " + raw);
                BoneWeight1[] influence = sourceWeights[candidates[0]];
                foreach (int candidate in candidates)
                    Require(InfluencesEqual(influence, sourceWeights[candidate]), "Body UV-seam duplicates have different skin weights at " + raw);
                Require(influence.Length > 0 && influence.Length <= byte.MaxValue, "Shell source vertex has no usable skin influences.");
                Require(Mathf.Abs(influence.Sum(w => w.weight) - 1f) <= .001f, "Imported source weights are not normalized.");
                shellToBody[i] = candidates[0];
                counts[i] = (byte)influence.Length;
                weights.AddRange(influence);
                audit.maximumInfluences = Mathf.Max(audit.maximumInfluences, influence.Length);
                audit.maximumMappingErrorMm = Mathf.Max(audit.maximumMappingErrorMm,
                    1000f * Vector3.Distance(shellVertices[i], manifest.shellControlPoints[raw].shellPosition));
                audit.weightedVertices++;
            }
            Mesh result = UnityEngine.Object.Instantiate(shell);
            result.name = Shape;
            result.ClearBlendShapes();
            // A bind pose depends on the renderer's local coordinate frame.
            result.bindposes = body.sharedMesh.bindposes.Select(b => b * body.transform.worldToLocalMatrix * shellTransform.localToWorldMatrix).ToArray();
            using (var nativeCounts = new NativeArray<byte>(counts, Allocator.Temp))
            using (var nativeWeights = new NativeArray<BoneWeight1>(weights.ToArray(), Allocator.Temp))
                result.SetBoneWeights(nativeCounts, nativeWeights);
            BoneWeight1[][] copied = Weights(result);
            audit.copiedWeightsExactly = copied.Select((w, i) => InfluencesEqual(w, sourceWeights[shellToBody[i]])).All(v => v);
            Require(audit.copiedWeightsExactly, "Unity changed the transferred shell influences.");
            var temporary = new GameObject("Partner shell skin solve");
            temporary.transform.SetParent(shellTransform.parent, false);
            temporary.transform.localPosition = shellTransform.localPosition;
            temporary.transform.localRotation = shellTransform.localRotation;
            temporary.transform.localScale = shellTransform.localScale;
            var skin = temporary.AddComponent<SkinnedMeshRenderer>();
            ConfigureSkin(skin, body, result);
            try
            {
                // Preserve BOTH authored shell endpoints through the new skinning.
                // Merely attaching weights could otherwise move the existing morph.
                Matrix4x4[] matrices = body.bones.Select((b, i) => skin.transform.worldToLocalMatrix * b.localToWorldMatrix * result.bindposes[i]).ToArray();
                Matrix4x4 otherToBase = skin.transform.worldToLocalMatrix * flaccidTransform.localToWorldMatrix;
                Matrix4x4 otherNormalToBase = otherToBase.inverse.transpose;
                Vector3[] other = flaccidShell.vertices, baseNormals = shell.normals, otherNormals = flaccidShell.normals;
                Vector4[] baseTangents = shell.tangents, otherTangents = flaccidShell.tangents;
                var solved = new Vector3[shellVertices.Length];
                var normals = new Vector3[solved.Length];
                var tangents = new Vector4[solved.Length];
                var deltas = new Vector3[solved.Length];
                var normalDeltas = new Vector3[solved.Length];
                var tangentDeltas = new Vector3[solved.Length];
                int offset = 0;
                Bounds bounds = new Bounds();
                for (int i = 0; i < solved.Length; i++)
                {
                    Matrix4x4 pointSkin = default, normalSkin = default;
                    for (int j = 0; j < counts[i]; j++)
                    {
                        BoneWeight1 w = weights[offset++];
                        Add(ref pointSkin, matrices[w.boneIndex], w.weight);
                        Add(ref normalSkin, matrices[w.boneIndex].inverse.transpose, w.weight);
                    }
                    Require(Mathf.Abs(pointSkin.determinant) > 1e-8f && Mathf.Abs(normalSkin.determinant) > 1e-8f,
                        "Singular transferred skin matrix at shell vertex " + i);
                    Matrix4x4 inverse = pointSkin.inverse;
                    solved[i] = inverse.MultiplyPoint3x4(shellVertices[i]);
                    Vector3 target = inverse.MultiplyPoint3x4(otherToBase.MultiplyPoint3x4(other[i]));
                    deltas[i] = target - solved[i];
                    normals[i] = normalSkin.inverse.MultiplyVector(baseNormals[i]).normalized;
                    normalDeltas[i] = normalSkin.inverse.MultiplyVector(otherNormalToBase.MultiplyVector(otherNormals[i]).normalized).normalized - normals[i];
                    Vector3 tangent = inverse.MultiplyVector(new Vector3(baseTangents[i].x, baseTangents[i].y, baseTangents[i].z)).normalized;
                    tangents[i] = new Vector4(tangent.x, tangent.y, tangent.z, baseTangents[i].w);
                    Vector3 otherTangent = otherToBase.MultiplyVector(new Vector3(otherTangents[i].x, otherTangents[i].y, otherTangents[i].z)).normalized;
                    tangentDeltas[i] = inverse.MultiplyVector(otherTangent).normalized - tangent;
                    if (i == 0) bounds = new Bounds(solved[i], Vector3.zero);
                    bounds.Encapsulate(solved[i]); bounds.Encapsulate(target);
                }
                result.vertices = solved; result.normals = normals; result.tangents = tangents;
                result.AddBlendShapeFrame(Shape, 100f, deltas, normalDeltas, tangentDeltas);
                bounds.Expand(.03f); result.bounds = bounds;
                ValidateEndpoints(skin, shellVertices, other.Select(v => otherToBase.MultiplyPoint3x4(v)).ToArray(), audit);
                ValidateBending(body, skin, shellToBody, audit);
                LastAudit = audit;
                return result;
            }
            catch { UnityEngine.Object.DestroyImmediate(result); throw; }
            finally { UnityEngine.Object.DestroyImmediate(temporary); }
        }

        internal static void ConfigureSkin(SkinnedMeshRenderer shell, SkinnedMeshRenderer body, Mesh mesh)
        {
            shell.sharedMesh = mesh;
            shell.bones = body.bones;
            shell.rootBone = body.rootBone;
            shell.quality = SkinQuality.Auto;
            shell.updateWhenOffscreen = true;
            shell.localBounds = mesh.bounds;
        }

        internal static Material[] BuildMaterials()
        {
            Manifest source = ReadManifest();
            var bridge = JsonUtility.FromJson<LaraCandidateBuilder.Manifest>(File.ReadAllText(Folder + "/manifest.json"));
            foreach (string directory in new[] { "TextureSources", "Textures", "SourceMaterials", "RuntimeMaterials" })
                Directory.CreateDirectory(Folder + "/" + directory);
            AssetDatabase.Refresh();
            var converter = new DTU { DTUPath = Folder + "/manifest.json" };
            var materials = new Material[bridge.materials.Length];
            bool legacy = Daz3DDTUImporter.UseLegacyShaders;
            try
            {
                Daz3DDTUImporter.UseLegacyShaders = false;
                foreach (LaraCandidateBuilder.Surface surface in bridge.materials)
                {
                    var properties = new List<DTUMaterialProperty>();
                    foreach (LaraCandidateBuilder.Property input in surface.properties)
                    {
                        var value = new DTUValue(input.number);
                        if (input.kind == "color") { value.Type = DTUValue.DataType.Color; value.AsColor = input.color; }
                        if (input.kind == "string") { value.Type = DTUValue.DataType.String; value.AsString = input.text; }
                        string texture = input.texture;
                        if (!string.IsNullOrEmpty(texture))
                        {
                            Require(File.Exists(texture), "Missing partner texture: " + texture);
                            string role = input.name == "Normal Map" ? "normal" : input.kind == "color" ? "color" : "data";
                            string staged = Folder + "/TextureSources/" + role + "_" + Hash(texture).Substring(0, 16) + Path.GetExtension(texture);
                            if (!File.Exists(staged)) File.Copy(texture, staged);
                            texture = Path.GetFullPath(staged);
                        }
                        properties.Add(new DTUMaterialProperty { Name = input.name, Value = value, Texture = texture ?? "", Exists = true });
                    }
                    var inputMaterial = new DTUMaterial { AssetName = surface.node, MaterialName = surface.surface,
                        MaterialType = "Iray Uber", Value = "Actor/Character", Properties = properties,
                        ProductName = "Partner", ProductComponentName = surface.node };
                    Material converted = converter.ConvertToUnityIrayUber(inputMaterial, Folder + "/Textures");
                    Require(converted != null, "Bridge could not convert " + surface.node + "/" + surface.surface);
                    converted.name = "M_" + surface.node + "_" + surface.surface;
                    string filename = WardrobeImporter.Safe(converted.name);
                    string sourcePath = Folder + "/SourceMaterials/" + filename + ".mat";
                    Material oldSource = AssetDatabase.LoadAssetAtPath<Material>(sourcePath);
                    Material baseline = oldSource == null ? null : UnityEngine.Object.Instantiate(oldSource);
                    converted = SaveMaterial(converted, sourcePath);
                    string runtimePath = Folder + "/RuntimeMaterials/" + filename + ".mat";
                    Material runtime = AssetDatabase.LoadAssetAtPath<Material>(runtimePath);
                    if (runtime == null) runtime = SaveMaterial(UnityEngine.Object.Instantiate(converted), runtimePath);
                    else
                    {
                        // Direct bridge shaders are sufficient here; partner dissolve
                        // behavior is a separate slice, not a prerequisite for contact.
                        runtime.shader = converted.shader;
                        if (baseline != null) LaraCandidateBuilder.MergeUneditedMaterialProperties(baseline, converted, runtime);
                        else runtime.CopyPropertiesFromMaterial(converted);
                    }
                    if (baseline != null) UnityEngine.Object.DestroyImmediate(baseline);
                    LaraCandidateBuilder.RepairOpacityTexture(runtime);
                    Require(!ShaderUtil.ShaderHasError(runtime.shader), "Partner bridge shader has compile errors: " + runtime.shader.name);
                    materials[surface.slot] = runtime;
                    EditorUtility.SetDirty(runtime);
                }
            }
            finally { Daz3DDTUImporter.UseLegacyShaders = legacy; }
            string[] response = { "_Roughness", "_SpecularLobe1Roughness", "_SpecularLobe2Roughness", "_GlossyLayeredWeight",
                "_DualLobeSpecularWeight", "_DualLobeSpecularRatio", "_TopCoatWeight", "_TopCoatRoughness" };
            foreach (Binding binding in source.bindings)
            {
                Material material = materials[binding.material];
                if (!material.shader.name.Contains("SSS")) continue;
                string surface = new[] { "Face", "Lips", "Ears", "Legs", "EyeSocket", "Arms" }.Contains(binding.surface) ? binding.surface : "Torso";
                int slot = surface == "Face" ? 1 : surface == "Lips" ? 2 : surface == "Ears" ? 4
                    : surface == "Legs" ? 5 : surface == "EyeSocket" ? 6 : surface == "Arms" ? 8 : 0;
                string prefix = "Assets/DazPose/Effects/Dissolve/LaraRuntimeMaterials/M_Lara_Runtime_" + slot.ToString("D2") + "_";
                string path = AssetDatabase.FindAssets("t:Material", new[] { "Assets/DazPose/Effects/Dissolve/LaraRuntimeMaterials" })
                    .Select(AssetDatabase.GUIDToAssetPath).Single(p => p.StartsWith(prefix, StringComparison.Ordinal));
                Material accepted = AssetDatabase.LoadAssetAtPath<Material>(path);
                foreach (string property in response)
                    if (material.HasProperty(property) && accepted.HasProperty(property)) material.SetFloat(property, accepted.GetFloat(property));
                // Retain grayscale cutout as coverage, including in the alpha channel.
                LaraCandidateBuilder.RepairOpacityTexture(material);
                if (binding.mesh == "Dicktator Shell")
                {
                    ConfigureShellMaterial(material);
                }
                EditorUtility.SetDirty(material);
            }
            // Check the supported core map assignments against the actual DUF
            // files, not merely the presence of a Texture object.
            var maps = new Dictionary<string, string> { { "Diffuse Color", "_DiffuseMap" },
                { "Normal Map", "_NormalMap" }, { "Bump Strength", "_HeightMap" },
                { "Cutout Opacity", "_AlphaMap" }, { "Dual Lobe Specular Reflectivity", "_DualLobeSpecularReflectivityMap" } };
            foreach (LaraCandidateBuilder.Surface surface in bridge.materials)
            {
                Material material = materials[surface.slot];
                foreach (LaraCandidateBuilder.Property input in surface.properties)
                {
                    if (string.IsNullOrEmpty(input.texture) || !maps.TryGetValue(input.name, out string property) || !material.HasProperty(property)) continue;
                    Texture texture = material.GetTexture(property);
                    Require(texture != null, "Converted partner material lost " + surface.node + "/" + surface.surface + "/" + input.name);
                    string texturePath = AssetDatabase.GetAssetPath(texture);
                    Require(Hash(texturePath) == Hash(input.texture), "Converted partner map differs from its DUF source: " + texturePath);
                    var importer = AssetImporter.GetAtPath(texturePath) as TextureImporter;
                    if (input.name == "Normal Map") Require(importer.textureType == TextureImporterType.NormalMap, "Partner normal map has the wrong import role.");
                    if (input.name == "Cutout Opacity") Require(!importer.sRGBTexture, "Partner cutout map must use linear coverage.");
                }
            }
            return materials;
        }

        private static void ConfigureShellMaterial(Material material)
        {
            material.shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/PartnerProof/PartnerShell.shadergraph");
            Require(material.shader != null, "Partner alpha blend shell shader is missing.");
            material.SetFloat("_SurfaceType", 1);
            material.SetFloat("_AlphaCutoffEnable", 0);
            material.SetFloat("_TransparentZWrite", 0);
            material.SetFloat("_ZWrite", 0);
            material.SetFloat("_EnableBlendModePreserveSpecularLighting", 0);
            material.SetFloat("_Roughness", .8f);
            material.SetFloat("_SpecularLobe1Roughness", .8f);
            material.SetFloat("_SpecularLobe2Roughness", .75f);
            material.SetFloat("_DualLobeSpecularWeight", .2f);
            material.SetFloat("_GlossyLayeredWeight", .2f);
            material.SetFloat("_TopCoatWeight", 0);
            material.DisableKeyword("_ALPHATEST_ON");
            UnityEditor.Rendering.HighDefinition.HDShaderUtils.ResetMaterialKeywords(material);
            Require(!ShaderUtil.ShaderHasError(material.shader), "Partner shell shader has compile errors.");
        }

        public static void RefreshShellMaterials()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { Folder + "/RuntimeMaterials" }))
            {
                Material material = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                if (!material.name.StartsWith("M_Dicktator_Shell_", StringComparison.Ordinal)) continue;
                LaraCandidateBuilder.RepairOpacityTexture(material);
                ConfigureShellMaterial(material);
                EditorUtility.SetDirty(material);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("Partner shell materials updated: alpha blending, authored opacity map, reduced gloss.");
        }

        private static Material SaveMaterial(Material value, string path)
        {
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing == null) { AssetDatabase.CreateAsset(value, path); return value; }
            EditorUtility.CopySerialized(value, existing);
            UnityEngine.Object.DestroyImmediate(value);
            EditorUtility.SetDirty(existing);
            return existing;
        }

        internal static void ApplyMaterials(GameObject root, Material[] materials, Manifest manifest)
        {
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                string mesh = renderer.name.Contains("Eyelashes") ? "Genesis8MaleEyelashes"
                    : renderer.name.Contains("Dicktator Shell") ? "Dicktator Shell"
                    : renderer.name.Contains("Genesis8Male") ? "Genesis8Male" : null;
                if (mesh == null) continue;
                Binding[] bindings = manifest.bindings.Where(b => b.mesh == mesh).OrderBy(b => b.slot).ToArray();
                int count = renderer is SkinnedMeshRenderer skinned ? skinned.sharedMesh.subMeshCount : renderer.GetComponent<MeshFilter>().sharedMesh.subMeshCount;
                Require(bindings.Length == count, "Partner material slot count differs for " + renderer.name);
                renderer.sharedMaterials = bindings.Select(b => materials[b.material]).ToArray();
                EditorUtility.SetDirty(renderer);
            }
        }

        private static void PatchAcceptanceScene(Mesh mesh, Material[] materials, Manifest manifest, Scene preview, bool previouslyDirty)
        {
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            bool opened = !scene.IsValid() || !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            bool wasDirty = previouslyDirty;
            try
            {
                var controller = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<PartnerAnatomyTestController>(true)).Single();
                GameObject root = controller.gameObject;
                SkinnedMeshRenderer body = SourceBody(root);
                var shell = root.GetComponentsInChildren<SkinnedMeshRenderer>(true).Single(r => r.name.Contains("Dicktator Shell"));
                ConfigureSkin(shell, body, mesh);
                ApplyMaterials(root, materials, manifest);
                var state = new SerializedObject(controller);
                state.FindProperty("shellRenderer").objectReferenceValue = shell;
                state.FindProperty("shellVisible").boolValue = true;
                state.ApplyModifiedPropertiesWithoutUndo();
                shell.enabled = true;
                PrefabUtility.RecordPrefabInstancePropertyModifications(shell);
                EditorSceneManager.MarkSceneDirty(scene);
                // Keep preexisting editor changes unsaved; do not save them incidentally.
                LastAudit.sceneSaved = !wasDirty && EditorSceneManager.SaveScene(scene);
                LastAudit.scene = ScenePath;
                GameObject copy = UnityEngine.Object.Instantiate(root);
                SceneManager.MoveGameObjectToScene(copy, preview);
                try
                {
                    copy.name = "PartnerCharacter";
                    copy.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                    var prefabController = new SerializedObject(copy.GetComponent<PartnerAnatomyTestController>());
                    prefabController.FindProperty("viewCamera").objectReferenceValue = null;
                    prefabController.ApplyModifiedPropertiesWithoutUndo();
                    Require(PrefabUtility.SaveAsPrefabAsset(copy, PrefabPath) != null, "Could not save the prepared partner prefab.");
                    var saved = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath).GetComponent<PartnerAnatomyTestController>();
                    Require(saved.ShellFollowsBones && saved.StructuralBody != null && saved.ShellRenderer != null,
                        "Saved partner prefab lost its skin/controller references.");
                    Require(saved.ShellRenderer.bones.All(b => b != null), "Saved partner prefab has missing bone references.");
                    Require(new SerializedObject(saved).FindProperty("viewCamera").objectReferenceValue == null,
                        "Prepared partner prefab still owns a scene camera.");
                    LastAudit.prefabReferencesVerified = true;
                }
                finally { UnityEngine.Object.DestroyImmediate(copy); }
                LastAudit.prefab = PrefabPath;
            }
            finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
        }

        private static void ValidateEndpoints(SkinnedMeshRenderer shell, Vector3[] erect, Vector3[] flaccid, Audit audit)
        {
            for (int step = 0; step <= 4; step++)
            {
                float t = step / 4f;
                shell.SetBlendShapeWeight(0, 100f * t);
                Vector3[] actual = Bake(shell);
                float[] errors = actual.Select((v, i) => Vector3.Distance(v, Vector3.Lerp(erect[i], flaccid[i], t))).ToArray();
                float error = errors.Max();
                Require(error <= EndpointTolerance, "Skinned shell endpoint/intermediate error exceeds 0.01 mm: " + error * 1000f);
                if (step == 0) { audit.maximumErectErrorMm = error * 1000f; audit.rmsErectErrorMm = Mathf.Sqrt(errors.Sum(e => e * e) / errors.Length) * 1000f; }
                if (step == 4) { audit.maximumFlaccidErrorMm = error * 1000f; audit.rmsFlaccidErrorMm = Mathf.Sqrt(errors.Sum(e => e * e) / errors.Length) * 1000f; }
            }
            shell.SetBlendShapeWeight(0, 0);
            audit.endpointsPreserved = true;
        }

        private static void ValidateBending(SkinnedMeshRenderer body, SkinnedMeshRenderer shell, int[] mapping, Audit audit)
        {
            Transform[] shaft = body.bones.Where(b => b.name == "shaft1" || b.name == "shaft4").ToArray();
            Require(shaft.Length == 2, "Expected both shaft1 and shaft4 in imported body bones.");
            Vector3[] rest = Bake(shell);
            float smallestMove = float.PositiveInfinity;
            foreach (Transform bone in shaft)
            {
                Quaternion original = bone.localRotation;
                try
                {
                    foreach (float angle in new[] { -5f, 5f })
                    {
                        bone.localRotation = original * Quaternion.Euler(angle, 0, 0);
                        Vector3[] moved = Bake(shell), surface = Bake(body);
                        float movement = moved.Select((p, i) => Vector3.Distance(p, rest[i])).Max();
                        smallestMove = Mathf.Min(smallestMove, movement);
                        float gap = moved.Select((p, i) => Vector3.Distance(shell.transform.TransformPoint(p), body.transform.TransformPoint(surface[mapping[i]]))).Max();
                        audit.maximumBendSurfaceGapMm = Mathf.Max(audit.maximumBendSurfaceGapMm, gap * 1000f);
                        Require(movement > .0001f, "Copied shell weights did not respond to " + bone.name);
                        Require(gap < .001f, "Shell/body surface separation exceeds 1 mm under " + bone.name);
                    }
                }
                finally { bone.localRotation = original; }
            }
            audit.smallestTestedShellMovementMm = smallestMove * 1000f;
            audit.shellVisibleDuringBend = true;
        }

        private static Vector3[] Bake(SkinnedMeshRenderer skin)
        {
            var mesh = new Mesh();
            try { skin.BakeMesh(mesh, true); return mesh.vertices; }
            finally { UnityEngine.Object.DestroyImmediate(mesh); }
        }
        private static int[] Matches(Vector3[] points, Vector3 target) => Enumerable.Range(0, points.Length)
            .Where(i => (points[i] - target).sqrMagnitude <= MappingTolerance * MappingTolerance).ToArray();
        private static BoneWeight1[][] Weights(Mesh mesh)
        {
            using (var counts = mesh.GetBonesPerVertex())
            using (var weights = mesh.GetAllBoneWeights())
            {
                var result = new BoneWeight1[mesh.vertexCount][];
                int offset = 0;
                for (int i = 0; i < result.Length; i++)
                {
                    result[i] = new BoneWeight1[counts[i]];
                    for (int j = 0; j < counts[i]; j++) result[i][j] = weights[offset++];
                }
                Require(offset == weights.Length, "Imported skin-weight buffer length differs.");
                return result;
            }
        }
        private static bool InfluencesEqual(BoneWeight1[] a, BoneWeight1[] b) => a.Length == b.Length &&
            a.Select((w, i) => w.boneIndex == b[i].boneIndex && w.weight == b[i].weight).All(v => v);
        private static SkinnedMeshRenderer SourceBody(GameObject root) => root.GetComponentsInChildren<SkinnedMeshRenderer>(true)
            .Single(r => r.name.Contains("Genesis8Male") && !r.name.Contains("Eyelashes"));
        private static MeshFilter SourceShell(GameObject root) => root.GetComponentsInChildren<MeshFilter>(true)
            .Single(r => r.name.Contains("Dicktator Shell"));
        private static void Add(ref Matrix4x4 sum, Matrix4x4 value, float weight)
        {
            for (int r = 0; r < 4; r++) for (int c = 0; c < 4; c++) sum[r, c] += value[r, c] * weight;
        }
        private static string Hash(string path)
        {
            using (var sha = SHA256.Create()) using (var file = File.OpenRead(path))
                return BitConverter.ToString(sha.ComputeHash(file)).Replace("-", "").ToLowerInvariant();
        }
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidDataException(message); }
    }

    [InitializeOnLoad]
    internal static class PartnerCharacterPreparationRequest
    {
        private static double nextCheck;
        static PartnerCharacterPreparationRequest() { EditorApplication.update += Check; }
        private static void Check()
        {
            if (EditorApplication.timeSinceStartup < nextCheck) return;
            nextCheck = EditorApplication.timeSinceStartup + 1;
            const string request = "Library/PartnerCharacterPreparation.once";
            if (!File.Exists(request) || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            File.Delete(request);
            try { PartnerCharacterPreparation.Prepare(); }
            catch (Exception error)
            {
                Directory.CreateDirectory("TestOutput/PartnerCharacter");
                File.WriteAllText("TestOutput/PartnerCharacter/Failure.txt", error.ToString());
                Debug.LogException(error);
            }
        }
    }
}
