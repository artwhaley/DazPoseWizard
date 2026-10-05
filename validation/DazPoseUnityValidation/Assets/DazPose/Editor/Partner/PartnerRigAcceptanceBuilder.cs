using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using DazPose.UnityValidation.Partner;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DazPose.UnityValidation.Editor.Partner
{
    /// <summary>Builds and numerically checks the isolated artist-authored G8M endpoint proof.</summary>
    public static class PartnerRigAcceptanceBuilder
    {
        private const string ErectPath = "Assets/TestCharacter/playererect.fbx";
        private const string FlaccidPath = "Assets/TestCharacter/playerflacid.fbx";
        private const string Generated = "Assets/PartnerProof/Generated";
        private const string ScenePath = "Assets/Scenes/PartnerRigAcceptance.unity";
        private const string BodyShape = "ErectionToFlaccid";
        private const string ShellShape = "ShellErectionToFlaccid";
        private const string ErectHash = "ba2bcca1dadb350a59e86e118a1b9b0195fef6033c00c472cc1523bf9c3481f1";
        private const string FlaccidHash = "7a8e9188ca04e205870f0ef7619cc0f10ab59af81dab2cff4ff3590ed6a0cda4";
        private const float PositionTolerance = 0.00001f;
        private static readonly string[] ShaftPathNames = { "shaft1", "shaft2", "shaft3", "shaft4", "shaft5", "shaft6", "shaft7" };
        private static readonly string[] BodySlots = { "Face", "Lips", "Teeth", "Torso", "Ears", "Legs", "EyeSocket", "Mouth", "Arms", "Pupils", "EyeMoisture", "Fingernails", "Cornea", "Irises", "Sclera", "Toenails", "Glans", "Shaft", "Testicles", "Torso_Front", "Torso_Middle", "Torso_Back", "Rectum" };

        [MenuItem("Tools/DAZ Pose/Partner Proof/Build Acceptance Scene")]
        public static void BuildAcceptanceScene()
        {
            Build();
        }

        [MenuItem("Tools/DAZ Pose/Partner Proof/Recheck Generated Endpoint Data")]
        public static void Recheck()
        {
            string report = Path.GetFullPath(Path.Combine(Application.dataPath, "../TestOutput/G8MArtistErectionProof/UnityAcceptanceProof.json"));
            if (!File.Exists(report)) throw new FileNotFoundException("Build the acceptance scene first.", report);
            Debug.Log("Partner endpoint proof data exists at " + report);
        }

        public static void BuildFromOneShotRequest()
        {
            Build();
        }

        public static void BuildInIsolatedBatchProject()
        {
            EnsureAssetFolder(Generated);
            Scene anchor = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            string anchorPath = AssetDatabase.GenerateUniqueAssetPath(Generated + "/PartnerProofBatchAnchor.unity");
            Require(EditorSceneManager.SaveScene(anchor, anchorPath), "Could not save the isolated batch scene anchor.");
            try { Build(); }
            finally
            {
                if (anchor.IsValid() && anchor.isLoaded) EditorSceneManager.CloseScene(anchor, true);
                AssetDatabase.DeleteAsset(anchorPath);
            }
        }

        private static void Build()
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string evidenceRoot = Path.Combine(projectRoot, "TestOutput", "G8MArtistErectionProof");
            Directory.CreateDirectory(evidenceRoot);
            string erectDiskPath = Path.Combine(projectRoot, ErectPath.Replace('/', Path.DirectorySeparatorChar));
            string flaccidDiskPath = Path.Combine(projectRoot, FlaccidPath.Replace('/', Path.DirectorySeparatorChar));
            string actualErectHash = HashFile(erectDiskPath);
            string actualFlaccidHash = HashFile(flaccidDiskPath);
            if (!string.Equals(actualErectHash, ErectHash, StringComparison.OrdinalIgnoreCase) || !string.Equals(actualFlaccidHash, FlaccidHash, StringComparison.OrdinalIgnoreCase))
            {
                WriteSourceChangeStop(evidenceRoot, actualErectHash, actualFlaccidHash);
                throw new InvalidDataException("Endpoint FBX SHA-256 changed since the accepted comparison. See TestOutput/G8MArtistErectionProof/source-change-stop.json; characterization stopped.");
            }
            ValidateParameterAndComplianceMath();

            var erectImporter = AssetImporter.GetAtPath(ErectPath) as ModelImporter;
            var flaccidImporter = AssetImporter.GetAtPath(FlaccidPath) as ModelImporter;
            Require(erectImporter != null && flaccidImporter != null, "Both endpoint FBXs must be imported as ModelImporter assets.");
            ConfigureEndpointImporter(ErectPath, ref erectImporter);
            ConfigureEndpointImporter(FlaccidPath, ref flaccidImporter);
            var erectAsset = AssetDatabase.LoadAssetAtPath<GameObject>(ErectPath);
            var flaccidAsset = AssetDatabase.LoadAssetAtPath<GameObject>(FlaccidPath);
            Require(erectAsset != null && flaccidAsset != null, "Endpoint FBX imports are not available.");
            EnsureAssetFolder(Generated);

            var erectPrefab = UnityEngine.Object.Instantiate(erectAsset);
            var flaccidPrefab = UnityEngine.Object.Instantiate(flaccidAsset);
            erectPrefab.name = "Erect endpoint source";
            flaccidPrefab.name = "Flaccid endpoint source";
            try
            {
                RendererBundle erect = FindBundle(erectPrefab.transform);
                RendererBundle flaccid = FindBundle(flaccidPrefab.transform);
                ValidatePair(erectPrefab.transform, flaccidPrefab.transform, erect, flaccid);
                Mesh bodyGenerated = SaveGeneratedMesh(MakeSkinnedBodyShapeMesh(erect.body, flaccid.body, BodyShape), Generated + "/G8M_ErectBody.asset");
                Mesh shellGenerated = SaveGeneratedMesh(MakeShapeMesh(erect.shellMesh, erect.shellTransform, erectPrefab.transform, flaccid.shellMesh, flaccid.shellTransform, flaccidPrefab.transform, ShellShape), Generated + "/G8M_ErectShell.asset");
                Material bodyProofMaterial = SaveProofMaterial("PartnerProof_Body", new Color(.72f, .43f, .33f, 1f));
                Material shellProofMaterial = SaveProofMaterial("PartnerProof_Shell", new Color(.28f, .58f, .76f, 1f));

                LineData erectLine = DeriveGeometryLine(erectPrefab.transform, erect.body, BodySlots, 16, 17, 33);
                LineData flaccidLine = DeriveGeometryLine(flaccidPrefab.transform, flaccid.body, BodySlots, 16, 17, 33);
                BuildScene(erectAsset, bodyGenerated, shellGenerated, bodyProofMaterial, shellProofMaterial, erectLine, flaccidLine);
                WriteProof(evidenceRoot, actualErectHash, actualFlaccidHash, erectImporter, flaccidImporter, erect, flaccid, bodyGenerated, shellGenerated, erectPrefab.transform, flaccidPrefab.transform, erectLine, flaccidLine);
                Debug.Log("PARTNER_RIG_ACCEPTANCE_BUILT: " + ScenePath + " ; evidence=" + evidenceRoot);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(erectPrefab);
                UnityEngine.Object.DestroyImmediate(flaccidPrefab);
            }
        }

        private sealed class RendererBundle
        {
            public SkinnedMeshRenderer body;
            public Transform shellTransform;
            public Mesh shellMesh;
            public MeshRenderer shellRenderer;
        }

        private sealed class LineData
        {
            public Vector3[] pointsRoot;
            public float nominalRadius;
            public float length;
            public int[] sectionPointCounts;
        }

        private static RendererBundle FindBundle(Transform root)
        {
            var skins = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            var body = skins.SingleOrDefault(r => r.sharedMesh != null && r.name.IndexOf("Genesis8Male", StringComparison.OrdinalIgnoreCase) >= 0 && r.name.IndexOf("Eyelashes", StringComparison.OrdinalIgnoreCase) < 0);
            var filters = root.GetComponentsInChildren<MeshFilter>(true);
            Require(body != null, "Could not identify the merged G8M body/graft renderer. Imported renderers: " + string.Join(", ", skins.Select(r => r.name + ":" + (r.sharedMesh == null ? 0 : r.sharedMesh.vertexCount))));
            var shellFilter = filters.SingleOrDefault(f => f.sharedMesh != null && ((f.sharedMesh.name + "/" + f.name).IndexOf("Dicktator Shell", StringComparison.OrdinalIgnoreCase) >= 0));
            Require(shellFilter != null, "Could not identify the static Dicktator shell mesh. Imported mesh filters: " + string.Join(", ", filters.Select(f => f.name + ":" + (f.sharedMesh == null ? 0 : f.sharedMesh.vertexCount) + "/" + (f.sharedMesh == null ? "" : f.sharedMesh.name))));
            var shellRenderer = shellFilter.GetComponent<MeshRenderer>();
            Require(shellRenderer != null, "Dicktator shell has no MeshRenderer.");
            return new RendererBundle { body = body, shellTransform = shellFilter.transform, shellMesh = shellFilter.sharedMesh, shellRenderer = shellRenderer };
        }

        private static void ValidatePair(Transform erectRoot, Transform flaccidRoot, RendererBundle erect, RendererBundle flaccid)
        {
            CompareMesh(erect.body.sharedMesh, flaccid.body.sharedMesh, "structural body");
            CompareMesh(erect.shellMesh, flaccid.shellMesh, "unskinned shell");
            using (var shellWeights = erect.shellMesh.GetBonesPerVertex()) Require(shellWeights.Length == 0, "The source shell unexpectedly contains skin weights; this proof requires the documented unskinned shell.");
            Require(erect.body.sharedMesh.blendShapeCount == 0 && flaccid.body.sharedMesh.blendShapeCount == 0, "Expected clean source meshes with no existing blendshape channels.");
            Require(erect.shellMesh.blendShapeCount == 0 && flaccid.shellMesh.blendShapeCount == 0, "Expected clean source shell meshes with no existing blendshape channels.");
            string[] erectBonePaths = erect.body.bones.Select(b => HierarchyPath(erectRoot, b)).ToArray();
            string[] flaccidBonePaths = flaccid.body.bones.Select(b => HierarchyPath(flaccidRoot, b)).ToArray();
            Require(erectBonePaths.SequenceEqual(flaccidBonePaths), "Imported body skeleton paths differ.");
            Require(erect.body.sharedMaterials.Select(m => m == null ? "" : m.name).SequenceEqual(flaccid.body.sharedMaterials.Select(m => m == null ? "" : m.name)), "Imported body/graft material slot names differ.");
            Require(erect.shellRenderer.sharedMaterials.Select(m => m == null ? "" : m.name).SequenceEqual(flaccid.shellRenderer.sharedMaterials.Select(m => m == null ? "" : m.name)), "Imported shell material slot names differ.");
            Require(SkinWeightsMatch(erect.body.sharedMesh, flaccid.body.sharedMesh), "Unity imported skin weights/index arrays differ across endpoints.");
            var ep = FindAnatomyTransforms(erectRoot);
            FindAnatomyTransforms(flaccidRoot);
            Require(ep.Length == 7, "Expected seven exact shaft chain paths in canonical erect skeleton.");
        }

        private static void CompareMesh(Mesh a, Mesh b, string label)
        {
            Require(a != null && b != null, label + " is missing.");
            Require(a.vertexCount == b.vertexCount, label + " vertex count mismatch.");
            Require(a.subMeshCount == b.subMeshCount, label + " submesh/material-slot count mismatch.");
            for (int channel = 0; channel < 8; channel++)
            {
                var ua = new List<Vector4>(); var ub = new List<Vector4>();
                a.GetUVs(channel, ua); b.GetUVs(channel, ub);
                Require(ua.SequenceEqual(ub), label + " UV channel " + channel + " differs.");
            }
            Require(a.normals.Length == b.normals.Length, label + " normal count differs.");
            for (int i = 0; i < a.subMeshCount; i++) Require(a.GetTopology(i) == b.GetTopology(i) && a.GetIndices(i).SequenceEqual(b.GetIndices(i)), label + " triangle/index stream differs at material slot " + i + ".");
        }

        private static bool SkinWeightsMatch(Mesh a, Mesh b)
        {
            using (var ca = a.GetBonesPerVertex())
            using (var cb = b.GetBonesPerVertex())
            using (var wa = a.GetAllBoneWeights())
            using (var wb = b.GetAllBoneWeights())
            {
                if (ca.Length != cb.Length || wa.Length != wb.Length) return false;
                for (int i = 0; i < ca.Length; i++) if (ca[i] != cb[i]) return false;
                for (int i = 0; i < wa.Length; i++) if (wa[i].boneIndex != wb[i].boneIndex || wa[i].weight != wb[i].weight) return false;
            }
            return true;
        }

        private static Mesh MakeShapeMesh(Mesh baseMesh, Transform baseRenderer, Transform baseRoot, Mesh otherMesh, Transform otherRenderer, Transform otherRoot, string shapeName)
        {
            Vector3[] baseVertices = baseMesh.vertices;
            Vector3[] otherVertices = otherMesh.vertices;
            var delta = new Vector3[baseVertices.Length];
            var targetLocal = new Vector3[baseVertices.Length];
            Matrix4x4 otherToBase = baseRenderer.worldToLocalMatrix * otherRenderer.localToWorldMatrix;
            for (int i = 0; i < delta.Length; i++)
            {
                targetLocal[i] = otherToBase.MultiplyPoint3x4(otherVertices[i]);
                delta[i] = targetLocal[i] - baseVertices[i];
            }
            Vector3[] baseNormals = baseMesh.normals;
            Vector3[] otherNormals = otherMesh.normals;
            Vector3[] normalDeltas = new Vector3[delta.Length];
            if (baseNormals.Length == delta.Length && otherNormals.Length == delta.Length)
            {
                Matrix4x4 normalMatrix = otherRenderer.localToWorldMatrix.inverse.transpose;
                Matrix4x4 intoBaseNormal = baseRenderer.localToWorldMatrix.transpose;
                for (int i = 0; i < delta.Length; i++)
                {
                    Vector3 worldNormal = normalMatrix.MultiplyVector(otherNormals[i]).normalized;
                    Vector3 localNormal = intoBaseNormal.MultiplyVector(worldNormal).normalized;
                    normalDeltas[i] = localNormal - baseNormals[i];
                }
            }
            Vector3[] tangentDeltas = new Vector3[delta.Length];
            Vector4[] baseTangents = baseMesh.tangents;
            Vector4[] otherTangents = otherMesh.tangents;
            if (baseTangents.Length == delta.Length && otherTangents.Length == delta.Length)
            {
                for (int i = 0; i < delta.Length; i++)
                {
                    Vector3 source = otherRenderer.localToWorldMatrix.MultiplyVector(new Vector3(otherTangents[i].x, otherTangents[i].y, otherTangents[i].z));
                    Vector3 target = baseRenderer.worldToLocalMatrix.MultiplyVector(source).normalized;
                    tangentDeltas[i] = target - new Vector3(baseTangents[i].x, baseTangents[i].y, baseTangents[i].z);
                }
            }

            Mesh generated = UnityEngine.Object.Instantiate(baseMesh);
            generated.name = shapeName;
            generated.ClearBlendShapes();
            generated.AddBlendShapeFrame(shapeName, 100f, delta, normalDeltas, tangentDeltas);
            Bounds bounds = generated.bounds;
            foreach (Vector3 p in targetLocal) bounds.Encapsulate(p);
            generated.bounds = bounds;
            return generated;
        }

        private static Mesh MakeSkinnedBodyShapeMesh(SkinnedMeshRenderer baseRenderer, SkinnedMeshRenderer otherRenderer, string shapeName)
        {
            Mesh baseMesh = baseRenderer.sharedMesh;
            Mesh otherMesh = otherRenderer.sharedMesh;
            var baseBaked = new Mesh { name = "ErectEndpointBakedForShape" };
            var otherBaked = new Mesh { name = "FlaccidEndpointBakedForShape" };
            try
            {
                baseRenderer.BakeMesh(baseBaked, true);
                otherRenderer.BakeMesh(otherBaked, true);
                Vector3[] baseVertices = baseMesh.vertices;
                Vector3[] otherVertices = otherBaked.vertices;
                Require(baseVertices.Length == otherVertices.Length, "Baked body endpoint vertex correspondence changed.");
                Vector3[] delta = new Vector3[baseVertices.Length];
                Vector3[] solvedTarget = new Vector3[baseVertices.Length];
                Vector3[] baseNormals = baseMesh.normals;
                Vector3[] targetNormals = otherBaked.normals;
                Vector3[] normalDeltas = new Vector3[baseVertices.Length];
                Vector4[] baseTangents = baseMesh.tangents;
                Vector4[] targetTangents = otherBaked.tangents;
                Vector3[] tangentDeltas = new Vector3[baseVertices.Length];
                Matrix4x4[] pointSkinMatrices = BuildBoneSkinMatrices(baseRenderer);
                Matrix4x4[] normalSkinMatrices = pointSkinMatrices.Select(m => m.inverse.transpose).ToArray();
                Matrix4x4 sourceNormalToWorld = otherRenderer.transform.localToWorldMatrix.inverse.transpose;
                Matrix4x4 worldNormalToBase = baseRenderer.transform.localToWorldMatrix.transpose;
                float smallestDeterminant = float.PositiveInfinity;

                using (var counts = baseMesh.GetBonesPerVertex())
                using (var weights = baseMesh.GetAllBoneWeights())
                {
                    int offset = 0;
                    for (int i = 0; i < baseVertices.Length; i++)
                    {
                        Matrix4x4 pointSkin = default;
                        Matrix4x4 normalSkin = default;
                        Matrix4x4 tangentSkin = default;
                        int count = counts[i];
                        if (count == 0)
                        {
                            pointSkin = normalSkin = tangentSkin = Matrix4x4.identity;
                        }
                        else
                        {
                            for (int j = 0; j < count; j++)
                            {
                                BoneWeight1 bw = weights[offset++];
                                Require(bw.boneIndex >= 0 && bw.boneIndex < pointSkinMatrices.Length, "Body skin weight references a missing canonical bone.");
                                AccumulateMatrix(ref pointSkin, pointSkinMatrices[bw.boneIndex], bw.weight);
                                AccumulateMatrix(ref normalSkin, normalSkinMatrices[bw.boneIndex], bw.weight);
                                AccumulateMatrix(ref tangentSkin, pointSkinMatrices[bw.boneIndex], bw.weight);
                            }
                        }
                        Vector3 targetWorld = otherRenderer.transform.TransformPoint(otherVertices[i]);
                        Vector3 targetRendererLocal = baseRenderer.transform.InverseTransformPoint(targetWorld);
                        float determinant = Mathf.Abs(pointSkin.determinant);
                        smallestDeterminant = Mathf.Min(smallestDeterminant, determinant);
                        Require(determinant > 1e-8f, "Canonical blended skin transform is singular at vertex " + i + ".");
                        solvedTarget[i] = pointSkin.inverse.MultiplyPoint3x4(targetRendererLocal);
                        delta[i] = solvedTarget[i] - baseVertices[i];

                        if (baseNormals.Length == baseVertices.Length && targetNormals.Length == baseVertices.Length)
                        {
                            Vector3 targetNormalWorld = sourceNormalToWorld.MultiplyVector(targetNormals[i]).normalized;
                            Vector3 targetNormalBase = worldNormalToBase.MultiplyVector(targetNormalWorld).normalized;
                            Require(Mathf.Abs(normalSkin.determinant) > 1e-8f, "Canonical blended normal transform is singular at vertex " + i + ".");
                            Vector3 solvedNormal = normalSkin.inverse.MultiplyVector(targetNormalBase).normalized;
                            normalDeltas[i] = solvedNormal - baseNormals[i];
                        }
                        if (baseTangents.Length == baseVertices.Length && targetTangents.Length == baseVertices.Length)
                        {
                            Vector3 targetTangentWorld = otherRenderer.transform.localToWorldMatrix.MultiplyVector(new Vector3(targetTangents[i].x, targetTangents[i].y, targetTangents[i].z));
                            Vector3 targetTangentBase = baseRenderer.transform.worldToLocalMatrix.MultiplyVector(targetTangentWorld).normalized;
                            Require(Mathf.Abs(tangentSkin.determinant) > 1e-8f, "Canonical blended tangent transform is singular at vertex " + i + ".");
                            Vector3 solvedTangent = tangentSkin.inverse.MultiplyVector(targetTangentBase).normalized;
                            tangentDeltas[i] = solvedTangent - new Vector3(baseTangents[i].x, baseTangents[i].y, baseTangents[i].z);
                        }
                    }
                    Require(offset == weights.Length, "Body skin-weight buffer length mismatch during the canonical endpoint solve.");
                }

                Mesh generated = UnityEngine.Object.Instantiate(baseMesh);
                generated.name = shapeName;
                generated.ClearBlendShapes();
                generated.AddBlendShapeFrame(shapeName, 100f, delta, normalDeltas, tangentDeltas);
                Bounds bounds = generated.bounds;
                foreach (Vector3 p in solvedTarget) bounds.Encapsulate(p);
                generated.bounds = bounds;
                Debug.Log("Partner body endpoint solve complete. Minimum absolute canonical skin determinant=" + smallestDeterminant.ToString("G6", System.Globalization.CultureInfo.InvariantCulture));
                return generated;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(baseBaked);
                UnityEngine.Object.DestroyImmediate(otherBaked);
            }
        }

        private static Matrix4x4[] BuildBoneSkinMatrices(SkinnedMeshRenderer renderer)
        {
            Transform[] bones = renderer.bones;
            Matrix4x4[] bindposes = renderer.sharedMesh.bindposes;
            Require(bones.Length == bindposes.Length, "Canonical renderer bone and bind-pose counts differ.");
            var matrices = new Matrix4x4[bones.Length];
            Matrix4x4 rendererWorldToLocal = renderer.transform.worldToLocalMatrix;
            for (int i = 0; i < bones.Length; i++)
            {
                Require(bones[i] != null, "Canonical renderer has a null bone at index " + i + ".");
                matrices[i] = rendererWorldToLocal * bones[i].localToWorldMatrix * bindposes[i];
            }
            return matrices;
        }

        private static void AccumulateMatrix(ref Matrix4x4 target, Matrix4x4 source, float weight)
        {
            target.m00 += source.m00 * weight; target.m01 += source.m01 * weight; target.m02 += source.m02 * weight; target.m03 += source.m03 * weight;
            target.m10 += source.m10 * weight; target.m11 += source.m11 * weight; target.m12 += source.m12 * weight; target.m13 += source.m13 * weight;
            target.m20 += source.m20 * weight; target.m21 += source.m21 * weight; target.m22 += source.m22 * weight; target.m23 += source.m23 * weight;
            target.m30 += source.m30 * weight; target.m31 += source.m31 * weight; target.m32 += source.m32 * weight; target.m33 += source.m33 * weight;
        }

        private static Mesh SaveGeneratedMesh(Mesh generated, string path)
        {
            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null) { AssetDatabase.CreateAsset(generated, path); return generated; }
            else
            {
                EditorUtility.CopySerialized(generated, existing);
                existing.name = generated.name;
                UnityEngine.Object.DestroyImmediate(generated);
                EditorUtility.SetDirty(existing);
                return existing;
            }
        }

        private static Material SaveProofMaterial(string name, Color color)
        {
            string path = Generated + "/" + name + ".mat";
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            Shader shader = Shader.Find("HDRP/Lit") ?? Shader.Find("Standard");
            Require(shader != null, "No HDRP/Lit or Standard shader is available for the temporary proof material.");
            Material result = existing != null ? existing : new Material(shader);
            result.shader = shader;
            result.name = name;
            if (result.HasProperty("_BaseColor")) result.SetColor("_BaseColor", color);
            if (result.HasProperty("_Color")) result.SetColor("_Color", color);
            if (result.HasProperty("_Smoothness")) result.SetFloat("_Smoothness", .28f);
            if (existing == null) AssetDatabase.CreateAsset(result, path);
            else EditorUtility.SetDirty(result);
            return result;
        }

        private static string[] FindAnatomyTransforms(Transform root)
        {
            var result = new string[7];
            Transform parent = FindPath(root, "Genesis8Male/Dicktator_Genitalia_G8M/hip/pelvis/shaftRoot");
            foreach (int i in Enumerable.Range(0, 7))
            {
                parent = parent == null ? null : parent.Find(ShaftPathNames[i]);
                Require(parent != null, "Missing required full shaft path at " + ShaftPathNames[i] + ".");
                result[i] = HierarchyPath(root, parent);
            }
            Require(FindPath(root, "Genesis8Male/hip/pelvis") != null, "Main pelvis path not found.");
            Require(FindPath(root, "Genesis8Male/Dicktator_Genitalia_G8M/hip/pelvis") != null, "Donor pelvis path not found.");
            return result;
        }

        private static LineData DeriveGeometryLine(Transform root, SkinnedMeshRenderer body, string[] slots, int glansSlot, int shaftSlot, int sampleCount)
        {
            Mesh mesh = body.sharedMesh;
            Require(mesh.subMeshCount == slots.Length, "Body slot count differs from characterized FBX material slots.");
            var baked = new Mesh { name = "EndpointGeometryForCenterline" };
            body.BakeMesh(baked, true);
            Vector3[] bakedVertices = baked.vertices;
            Vector3[] surface = mesh.GetIndices(shaftSlot).Distinct().Select(i => root.InverseTransformPoint(body.transform.TransformPoint(bakedVertices[i]))).ToArray();
            Vector3[] glans = mesh.GetIndices(glansSlot).Distinct().Select(i => root.InverseTransformPoint(body.transform.TransformPoint(bakedVertices[i]))).ToArray();
            UnityEngine.Object.DestroyImmediate(baked);
            Transform[] shaft = FindAnatomyTransforms(root).Select(path => FindPath(root, path)).ToArray();
            Vector3[] guide = shaft.Select(t => root.InverseTransformPoint(t.position)).ToArray();
            Vector3 axis = (guide[6] - guide[5]).normalized;
            Vector3 tip = guide[6] + axis * glans.Max(p => Vector3.Dot(p - guide[6], axis));
            Vector3[] guides = guide.Concat(new[] { tip }).ToArray();
            float[] cumulative = new float[guides.Length];
            for (int i = 1; i < guides.Length; i++) cumulative[i] = cumulative[i - 1] + Vector3.Distance(guides[i - 1], guides[i]);
            float totalLength = cumulative[cumulative.Length - 1];
            var points = new Vector3[sampleCount];
            var counts = new int[sampleCount];
            var radii = new List<float>();
            for (int k = 0; k < sampleCount; k++)
            {
                float normalized = k / (float)(sampleCount - 1);
                float along = totalLength * normalized;
                int seg = 0;
                while (seg < cumulative.Length - 2 && cumulative[seg + 1] < along) seg++;
                float span = cumulative[seg + 1] - cumulative[seg];
                float u = span > 1e-8f ? (along - cumulative[seg]) / span : 0f;
                Vector3 guidePoint = Vector3.Lerp(guides[seg], guides[seg + 1], u);
                Vector3 tangent = (guides[seg + 1] - guides[seg]).normalized;
                if (seg > 0 && u < 0.2f) tangent = Vector3.Slerp((guides[seg] - guides[seg - 1]).normalized, tangent, (u + .8f) / 1f).normalized;
                if (seg < guides.Length - 2 && u > .8f) tangent = Vector3.Slerp(tangent, (guides[seg + 2] - guides[seg + 1]).normalized, u - .8f).normalized;
                Vector3 basisA = Vector3.Cross(tangent, Vector3.up);
                if (basisA.sqrMagnitude < 1e-5f) basisA = Vector3.Cross(tangent, Vector3.right);
                basisA.Normalize();
                Vector3 basisB = Vector3.Cross(tangent, basisA).normalized;
                float window = Mathf.Max(totalLength * .012f, span * .12f);
                var candidates = surface.Where(p => Mathf.Abs(Vector3.Dot(p - guidePoint, tangent)) <= window).ToArray();
                if (candidates.Length < 6)
                {
                    window = Mathf.Max(totalLength * .025f, span * .22f);
                    candidates = surface.Where(p => Mathf.Abs(Vector3.Dot(p - guidePoint, tangent)) <= window).ToArray();
                }
                counts[k] = candidates.Length;
                if (candidates.Length < 3)
                {
                    points[k] = guidePoint;
                    continue;
                }
                float minA = float.PositiveInfinity, maxA = float.NegativeInfinity, minB = float.PositiveInfinity, maxB = float.NegativeInfinity;
                foreach (Vector3 p in candidates)
                {
                    Vector3 d = p - guidePoint;
                    float a = Vector3.Dot(d, basisA), b = Vector3.Dot(d, basisB);
                    minA = Mathf.Min(minA, a); maxA = Mathf.Max(maxA, a); minB = Mathf.Min(minB, b); maxB = Mathf.Max(maxB, b);
                }
                Vector3 center = guidePoint + basisA * ((minA + maxA) * .5f) + basisB * ((minB + maxB) * .5f);
                points[k] = center;
                if (normalized >= .2f && normalized <= .8f)
                {
                    float[] radial = candidates.Select(p => (p - center).magnitude).Where(r => r > 1e-5f).OrderBy(r => r).ToArray();
                    if (radial.Length > 0) radii.Add(radial[radial.Length / 2]);
                }
            }
            float length = 0f;
            for (int i = 1; i < points.Length; i++) length += Vector3.Distance(points[i - 1], points[i]);
            radii.Sort();
            float radius = radii.Count == 0 ? 0f : radii[radii.Count / 2];
            Require(points.All(IsFinite) && length > 0f && radius > 0f, "Geometry-derived line or radius could not be estimated from imported shaft surface sections.");
            return new LineData { pointsRoot = points, nominalRadius = radius, length = length, sectionPointCounts = counts };
        }

        private static bool IsFinite(Vector3 p) => !(float.IsNaN(p.x) || float.IsNaN(p.y) || float.IsNaN(p.z) || float.IsInfinity(p.x) || float.IsInfinity(p.y) || float.IsInfinity(p.z));

        private static void BuildScene(GameObject erectAsset, Mesh bodyMesh, Mesh shellMesh, Material bodyMaterial, Material shellMaterial, LineData erectLine, LineData flaccidLine)
        {
            var current = SceneManager.GetSceneByPath(ScenePath);
            if (current.IsValid()) throw new InvalidOperationException("PartnerRigAcceptance scene is already open; builder will not replace an open scene.");
            Scene previousActive = SceneManager.GetActiveScene();
            string anchorPath = null;
            if (!previousActive.IsValid() || string.IsNullOrEmpty(previousActive.path))
            {
                Require(previousActive.IsValid() && previousActive.rootCount == 0 && !previousActive.isDirty, "The active scene is untitled and contains edits; save it before building the additive partner proof scene.");
                anchorPath = AssetDatabase.GenerateUniqueAssetPath(Generated + "/PartnerProofAnchor.unity");
                Require(EditorSceneManager.SaveScene(previousActive, anchorPath), "Could not preserve the empty untitled editor scene while building the acceptance scene.");
            }
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            var root = (GameObject)PrefabUtility.InstantiatePrefab(erectAsset, scene);
            root.name = "Partner";
            root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            RendererBundle bundle = FindBundle(root.transform);
            bundle.body.sharedMesh = bodyMesh;
            bundle.body.updateWhenOffscreen = true;
            bundle.body.quality = SkinQuality.Auto;
            bundle.body.sharedMaterials = Enumerable.Repeat(bodyMaterial, bodyMesh.subMeshCount).ToArray();
            Transform shell = bundle.shellTransform;
            var oldRenderer = shell.GetComponent<MeshRenderer>();
            if (oldRenderer != null) UnityEngine.Object.DestroyImmediate(oldRenderer);
            var oldFilter = shell.GetComponent<MeshFilter>();
            if (oldFilter != null) UnityEngine.Object.DestroyImmediate(oldFilter);
            var shellSkin = shell.gameObject.AddComponent<SkinnedMeshRenderer>();
            shellSkin.sharedMesh = shellMesh;
            shellSkin.sharedMaterials = Enumerable.Repeat(shellMaterial, shellMesh.subMeshCount).ToArray();
            shellSkin.rootBone = null;
            shellSkin.bones = Array.Empty<Transform>();
            shellSkin.updateWhenOffscreen = true;

            var controller = root.AddComponent<PartnerAnatomyTestController>();
            var serialized = new SerializedObject(controller);
            serialized.FindProperty("structuralBody").objectReferenceValue = bundle.body;
            serialized.FindProperty("shellRenderer").objectReferenceValue = shellSkin;
            Transform[] shaft = FindAnatomyTransforms(root.transform).Select(p => FindPath(root.transform, p)).ToArray();
            serialized.FindProperty("shaftBones").arraySize = shaft.Length;
            for (int i = 0; i < shaft.Length; i++) serialized.FindProperty("shaftBones").GetArrayElementAtIndex(i).objectReferenceValue = shaft[i];
            Transform[] distributed = { shaft[1], shaft[2], shaft[3], shaft[4] };
            serialized.FindProperty("distributedBones").arraySize = distributed.Length;
            for (int i = 0; i < distributed.Length; i++) serialized.FindProperty("distributedBones").GetArrayElementAtIndex(i).objectReferenceValue = distributed[i];
            serialized.FindProperty("mainPelvis").objectReferenceValue = FindPath(root.transform, "Genesis8Male/hip/pelvis");
            serialized.FindProperty("donorPelvis").objectReferenceValue = FindPath(root.transform, "Genesis8Male/Dicktator_Genitalia_G8M/hip/pelvis");
            SetVectorArray(serialized.FindProperty("flaccidCenterlineRoot"), flaccidLine.pointsRoot);
            SetVectorArray(serialized.FindProperty("erectCenterlineRoot"), erectLine.pointsRoot);
            serialized.FindProperty("flaccidRadius").floatValue = flaccidLine.nominalRadius;
            serialized.FindProperty("erectRadius").floatValue = erectLine.nominalRadius;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            var cameraObject = new GameObject("Acceptance Camera");
            SceneManager.MoveGameObjectToScene(cameraObject, scene);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.nearClipPlane = 0.01f;
            camera.farClipPlane = 100f;
            camera.fieldOfView = 42f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.12f, .13f, .16f, 1f);
            cameraObject.AddComponent<AudioListener>();
            serialized.FindProperty("viewCamera").objectReferenceValue = camera;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            var lightObject = new GameObject("Acceptance Key Light");
            SceneManager.MoveGameObjectToScene(lightObject, scene);
            lightObject.transform.rotation = Quaternion.Euler(48f, -28f, 0f);
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.15f;
            light.color = new Color(1f, .91f, .82f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.34f, .36f, .4f);
            SceneView.lastActiveSceneView?.LookAt(root.transform.position + new Vector3(0f, .9f, 0f), Quaternion.Euler(8f, 0f, 0f), 2.5f);
            EditorSceneManager.SaveScene(scene, ScenePath);
            if (!string.IsNullOrEmpty(anchorPath))
            {
                EditorSceneManager.CloseScene(previousActive, true);
                AssetDatabase.DeleteAsset(anchorPath);
            }
            AssetDatabase.SaveAssets();
        }

        private static void SetVectorArray(SerializedProperty property, Vector3[] points)
        {
            property.arraySize = points.Length;
            for (int i = 0; i < points.Length; i++) property.GetArrayElementAtIndex(i).vector3Value = points[i];
        }

        private static void EnsureAssetFolder(string path)
        {
            string[] parts = path.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        private static void ValidateParameterAndComplianceMath()
        {
            Require(PartnerAnatomyTestController.BlendShapeWeightForErection(1f) == 0f, "Erection01=1 must map to zero endpoint shape weight.");
            Require(PartnerAnatomyTestController.BlendShapeWeightForErection(0f) == 100f, "Erection01=0 must map to full endpoint shape weight.");
            Require(PartnerAnatomyTestController.BlendShapeWeightForErection(-1f) == 100f && PartnerAnatomyTestController.BlendShapeWeightForErection(2f) == 0f, "Erection input must clamp to [0,1].");
            Quaternion basis = Quaternion.Euler(13f, -7f, 24f);
            Quaternion first = PartnerAnatomyTestController.ApplyComplianceOffset(basis, new Vector3(5f, 0f, 0f));
            Quaternion repeated = PartnerAnatomyTestController.ApplyComplianceOffset(basis, new Vector3(5f, 0f, 0f));
            Quaternion restored = PartnerAnatomyTestController.ApplyComplianceOffset(basis, Vector3.zero);
            Require(first.Equals(repeated), "Compliance application accumulated across repeated frames.");
            Require(restored.Equals(basis), "Zero compliance did not restore the exact erect base rotation.");
        }

        private static void ConfigureEndpointImporter(string path, ref ModelImporter importer)
        {
            bool changed = false;
            if (importer.skinWeights != ModelImporterSkinWeights.Custom) { importer.skinWeights = ModelImporterSkinWeights.Custom; changed = true; }
            if (importer.maxBonesPerVertex != 10) { importer.maxBonesPerVertex = 10; changed = true; }
            if (Mathf.Abs(importer.minBoneWeight - 0.001f) > 1e-7f) { importer.minBoneWeight = 0.001f; changed = true; }
            if (!importer.isReadable) { importer.isReadable = true; changed = true; }
            if (importer.optimizeGameObjects) { importer.optimizeGameObjects = false; changed = true; }
            if (importer.importConstraints) { importer.importConstraints = false; changed = true; }
            if (!importer.importBlendShapes) { importer.importBlendShapes = true; changed = true; }
            if (changed) importer.SaveAndReimport();
            importer = AssetImporter.GetAtPath(path) as ModelImporter;
            Require(importer != null && importer.skinWeights == ModelImporterSkinWeights.Custom && importer.maxBonesPerVertex >= 10 && Mathf.Abs(importer.minBoneWeight - 0.001f) <= 1e-7f && importer.isReadable && !importer.optimizeGameObjects && !importer.importConstraints, "Unity did not apply the project-local endpoint import settings: " + path + " (weights=" + (importer == null ? "null" : importer.skinWeights.ToString()) + ", max=" + (importer == null ? -1 : importer.maxBonesPerVertex) + ", minimumWeight=" + (importer == null ? -1f : importer.minBoneWeight) + ").");
        }

        private static void WriteProof(string evidenceRoot, string erectHash, string flaccidHash, ModelImporter erectImporter, ModelImporter flaccidImporter, RendererBundle erect, RendererBundle flaccid, Mesh bodyGenerated, Mesh shellGenerated, Transform erectRoot, Transform flaccidRoot, LineData erectLine, LineData flaccidLine)
        {
            Debug.Log("Partner proof: measuring imported weight data and rendered endpoints.");
            MeshResult body = Measure(erect.body.sharedMesh, flaccid.body.sharedMesh, bodyGenerated, erect.body.transform, erectRoot, flaccid.body.transform, flaccidRoot, BodyShape);
            Debug.Log("Partner proof: evaluating generated body at both skinned endpoints.");
            RenderedEndpointError bodyEndpointError = MeasureSkinnedEndpoints(erect.body, erectRoot, flaccid.body, flaccidRoot, bodyGenerated, BodyShape);
            body.maximumErectMm = bodyEndpointError.maximumErectMm;
            body.rmsErectMm = bodyEndpointError.rmsErectMm;
            body.maximumFlaccidMm = bodyEndpointError.maximumFlaccidMm;
            body.rmsFlaccidMm = bodyEndpointError.rmsFlaccidMm;
            body.endpointMeasurement = "SkinnedMeshRenderer.BakeMesh output compared in root-relative world space with canonical erect skeleton retained at its imported base pose.";
            MeshResult shell = Measure(erect.shellMesh, flaccid.shellMesh, shellGenerated, erect.shellTransform, erectRoot, flaccid.shellTransform, flaccidRoot, ShellShape);
            var nodes = new List<BoneMismatch>();
            Transform[] shaftErect = FindAnatomyTransforms(erectRoot).Select(p => FindPath(erectRoot, p)).ToArray();
            for (int state = 0; state <= 4; state++)
            {
                float t = state / 4f;
                Vector3[] line = LerpPoints(flaccidLine.pointsRoot, erectLine.pointsRoot, t);
                for (int i = 0; i < shaftErect.Length; i++) nodes.Add(new BoneMismatch { erection01 = t, bone = ShaftPathNames[i], distanceMm = 1000f * DistanceToPolyline(erectRoot.InverseTransformPoint(shaftErect[i].position), line) });
            }
            var proof = new ProofReport
            {
                sourceHashes = new[] { new SourceHash { path = ErectPath, sha256 = erectHash }, new SourceHash { path = FlaccidPath, sha256 = flaccidHash } },
                canonicalBody = ErectPath,
                canonicalSkeleton = ErectPath,
                canonicalShell = ErectPath + " (Dicktator Shell; unskinned)",
                body = body,
                shell = shell,
                importer = new ImportProof { erectWeights = erectImporter.skinWeights.ToString(), flaccidWeights = flaccidImporter.skinWeights.ToString(), erectMaxBonesPerVertex = erectImporter.maxBonesPerVertex, flaccidMaxBonesPerVertex = flaccidImporter.maxBonesPerVertex, erectMinBoneWeight = erectImporter.minBoneWeight, flaccidMinBoneWeight = flaccidImporter.minBoneWeight, erectOptimizeGameObjects = erectImporter.optimizeGameObjects, constraintsImported = erectImporter.importConstraints, runtimeQualityCap = QualitySettings.skinWeights.ToString() },
                geometryCenterline = new CenterlineProof { method = "33 stations by equal arc distance on the endpoint's own shaft1..shaft7 skeleton guide plus a glans support-plane tip; at each station, center is the midpoint of the bounding box of actual Shaft-slot surface vertices within a narrow perpendicular slice. Bone positions guide local station/tangent only; no cross-endpoint bone interpolation is used.", erectLengthCm = erectLine.length * 100f, flaccidLengthCm = flaccidLine.length * 100f, erectRadiusCm = erectLine.nominalRadius * 100f, flaccidRadiusCm = flaccidLine.nominalRadius * 100f, erectSectionPointCounts = erectLine.sectionPointCounts, flaccidSectionPointCounts = flaccidLine.sectionPointCounts, pivotMismatch = nodes.ToArray(), states01 = new[] { 0f, .25f, .5f, .75f, 1f }, linearInterpolatedCenterlinePointCount = erectLine.pointsRoot.Length, noZeroSegments = Enumerable.Range(0, 5).All(k => SegmentsNonzero(LerpPoints(flaccidLine.pointsRoot, erectLine.pointsRoot, k / 4f))), minimumContinuousSegmentCm = MinimumContinuousSegment(flaccidLine.pointsRoot, erectLine.pointsRoot) * 100f, sampleLengthsCm = Enumerable.Range(0, 5).Select(k => PolylineLength(LerpPoints(flaccidLine.pointsRoot, erectLine.pointsRoot, k / 4f)) * 100f).ToArray() },
                endpointsNumericallyVerified = body.maximumErectMm <= PositionTolerance * 1000f && body.maximumFlaccidMm <= PositionTolerance * 1000f && shell.maximumErectMm <= PositionTolerance * 1000f && shell.maximumFlaccidMm <= PositionTolerance * 1000f,
                visualAcceptance = "PENDING USER REVIEW; numeric endpoint checks do not accept intermediate appearance or compliance",
                donorPelvis = "Importer constraints are disabled per the source-specific FBX settings; the scene controller owns one explicit donor-to-main pelvis follow using the measured initial relative transform.",
                sourceFilesUnmodified = HashFile(Path.Combine(Directory.GetParent(Application.dataPath).FullName, ErectPath.Replace('/', Path.DirectorySeparatorChar))) == erectHash && HashFile(Path.Combine(Directory.GetParent(Application.dataPath).FullName, FlaccidPath.Replace('/', Path.DirectorySeparatorChar))) == flaccidHash
            };
            Require(proof.endpointsNumericallyVerified, "Generated endpoint numeric error exceeds the 0.01 mm tolerance.");
            Require(body.unityMaximumInfluences >= 10, "Unity did not retain the characterized ten influences on the generated body mesh.");
            File.WriteAllText(Path.Combine(evidenceRoot, "UnityAcceptanceProof.json"), JsonUtility.ToJson(proof, true) + "\n", new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(evidenceRoot, "GeometryCenterlines.csv"), CenterlineCsv(erectLine, flaccidLine), new UTF8Encoding(false));
            Require(proof.sourceFilesUnmodified, "Source FBX changed while the proof assets were being built.");
        }

        [Serializable] private sealed class MeshResult
        {
            public string shapeName;
            public string endpointMeasurement;
            public int vertexCount;
            public int submeshCount;
            public int blendShapeCount;
            public float maximumErectMm;
            public float rmsErectMm;
            public float maximumFlaccidMm;
            public float rmsFlaccidMm;
            public int unityMaximumInfluences;
            public int unityVerticesAboveOneWeight;
            public int unityVerticesAboveFourWeights;
            public float unityMaximumWeightSum;
            public float unityMinimumPositiveWeightSum;
            public int unityVerticesWithWeights;
            public int unityWeightSumsOutsideTolerance;
            public int allBoneWeightRecords;
            public bool topologyEqual;
            public bool uvEqual;
        }

        private static MeshResult Measure(Mesh erectMesh, Mesh flaccidMesh, Mesh generated, Transform erectRenderer, Transform erectRoot, Transform flaccidRenderer, Transform flaccidRoot, string name)
        {
            Vector3[] a = erectMesh.vertices;
            Vector3[] b = flaccidMesh.vertices;
            Vector3[] actualErect = new Vector3[a.Length];
            Vector3[] actualFlaccid = new Vector3[a.Length];
            Matrix4x4 aToRoot = erectRoot.worldToLocalMatrix * erectRenderer.localToWorldMatrix;
            Matrix4x4 bToRoot = flaccidRoot.worldToLocalMatrix * flaccidRenderer.localToWorldMatrix;
            Matrix4x4 rootToA = aToRoot.inverse;
            for (int i = 0; i < a.Length; i++)
            {
                actualErect[i] = rootToA.MultiplyPoint3x4(aToRoot.MultiplyPoint3x4(a[i]));
                actualFlaccid[i] = rootToA.MultiplyPoint3x4(bToRoot.MultiplyPoint3x4(b[i]));
            }
            Vector3[] delta = new Vector3[a.Length], zero = new Vector3[a.Length];
            generated.GetBlendShapeFrameVertices(0, 0, delta, zero, zero);
            float maxE = 0f, maxF = 0f, sumE = 0f, sumF = 0f;
            for (int i = 0; i < a.Length; i++)
            {
                float e = Vector3.Distance(a[i], actualErect[i]);
                float f = Vector3.Distance(a[i] + delta[i], actualFlaccid[i]);
                maxE = Mathf.Max(maxE, e); maxF = Mathf.Max(maxF, f); sumE += e * e; sumF += f * f;
            }
            int maxInfluences = 0, above1 = 0, above4 = 0, offset = 0, active = 0, outsideTolerance = 0;
            float maxWeightSum = 0f, minPositiveWeightSum = float.PositiveInfinity;
            using (var counts = erectMesh.GetBonesPerVertex())
            using (var weights = erectMesh.GetAllBoneWeights())
            {
                for (int i = 0; i < counts.Length; i++)
                {
                    int count = counts[i]; maxInfluences = Mathf.Max(maxInfluences, count);
                    if (count > 1) above1++; if (count > 4) above4++;
                    float sum = 0f;
                    for (int j = 0; j < count; j++) sum += weights[offset++].weight;
                    maxWeightSum = Mathf.Max(maxWeightSum, sum);
                    if (count > 0)
                    {
                        active++;
                        minPositiveWeightSum = Mathf.Min(minPositiveWeightSum, sum);
                        if (Mathf.Abs(sum - 1f) > .001f) outsideTolerance++;
                    }
                }
                Require(offset == weights.Length, "Unity skin-weight buffer length mismatch.");
            }
            return new MeshResult { shapeName = name, vertexCount = generated.vertexCount, submeshCount = generated.subMeshCount, blendShapeCount = generated.blendShapeCount, maximumErectMm = maxE * 1000f, rmsErectMm = Mathf.Sqrt(sumE / a.Length) * 1000f, maximumFlaccidMm = maxF * 1000f, rmsFlaccidMm = Mathf.Sqrt(sumF / a.Length) * 1000f, unityMaximumInfluences = maxInfluences, unityVerticesAboveOneWeight = above1, unityVerticesAboveFourWeights = above4, unityMaximumWeightSum = maxWeightSum, unityMinimumPositiveWeightSum = active > 0 ? minPositiveWeightSum : 0f, unityVerticesWithWeights = active, unityWeightSumsOutsideTolerance = outsideTolerance, allBoneWeightRecords = offset, topologyEqual = true, uvEqual = true };
        }

        [Serializable] private sealed class RenderedEndpointError
        {
            public float maximumErectMm;
            public float rmsErectMm;
            public float maximumFlaccidMm;
            public float rmsFlaccidMm;
        }

        private static RenderedEndpointError MeasureSkinnedEndpoints(SkinnedMeshRenderer erectRenderer, Transform erectRoot, SkinnedMeshRenderer flaccidRenderer, Transform flaccidRoot, Mesh generated, string shapeName)
        {
            Mesh sourceErect = new Mesh { name = "SourceErectRenderedForAcceptance" };
            Mesh sourceFlaccid = new Mesh { name = "SourceFlaccidRenderedForAcceptance" };
            Mesh generatedErect = new Mesh { name = "GeneratedErectRenderedForAcceptance" };
            Mesh generatedFlaccid = new Mesh { name = "GeneratedFlaccidRenderedForAcceptance" };
            Mesh originalMesh = erectRenderer.sharedMesh;
            try
            {
                erectRenderer.BakeMesh(sourceErect, true);
                flaccidRenderer.BakeMesh(sourceFlaccid, true);
                erectRenderer.sharedMesh = generated;
                int shapeIndex = generated.GetBlendShapeIndex(shapeName);
                Require(shapeIndex >= 0, "Generated body endpoint shape was not found for evaluated endpoint checks.");
                erectRenderer.SetBlendShapeWeight(shapeIndex, 0f);
                erectRenderer.BakeMesh(generatedErect, true);
                erectRenderer.SetBlendShapeWeight(shapeIndex, 100f);
                erectRenderer.BakeMesh(generatedFlaccid, true);
                Vector3[] sourceErectRoot = RenderedVerticesInRoot(sourceErect, erectRenderer.transform, erectRoot);
                Vector3[] generatedErectRoot = RenderedVerticesInRoot(generatedErect, erectRenderer.transform, erectRoot);
                Vector3[] sourceFlaccidRoot = RenderedVerticesInRoot(sourceFlaccid, flaccidRenderer.transform, flaccidRoot);
                Vector3[] generatedFlaccidRoot = RenderedVerticesInRoot(generatedFlaccid, erectRenderer.transform, erectRoot);
                Require(sourceErectRoot.Length == generatedErectRoot.Length && sourceFlaccidRoot.Length == generatedFlaccidRoot.Length && sourceErectRoot.Length == sourceFlaccidRoot.Length, "Rendered endpoint vertex correspondence changed during BakeMesh verification.");
                float maxE = 0f, maxF = 0f, sumE = 0f, sumF = 0f;
                for (int i = 0; i < sourceErectRoot.Length; i++)
                {
                    float e = Vector3.Distance(sourceErectRoot[i], generatedErectRoot[i]);
                    float f = Vector3.Distance(sourceFlaccidRoot[i], generatedFlaccidRoot[i]);
                    maxE = Mathf.Max(maxE, e); maxF = Mathf.Max(maxF, f); sumE += e * e; sumF += f * f;
                }
                return new RenderedEndpointError { maximumErectMm = maxE * 1000f, rmsErectMm = Mathf.Sqrt(sumE / sourceErectRoot.Length) * 1000f, maximumFlaccidMm = maxF * 1000f, rmsFlaccidMm = Mathf.Sqrt(sumF / sourceFlaccidRoot.Length) * 1000f };
            }
            finally
            {
                erectRenderer.sharedMesh = originalMesh;
                UnityEngine.Object.DestroyImmediate(sourceErect);
                UnityEngine.Object.DestroyImmediate(sourceFlaccid);
                UnityEngine.Object.DestroyImmediate(generatedErect);
                UnityEngine.Object.DestroyImmediate(generatedFlaccid);
            }
        }

        private static Vector3[] RenderedVerticesInRoot(Mesh baked, Transform renderer, Transform root)
        {
            Vector3[] vertices = baked.vertices;
            for (int i = 0; i < vertices.Length; i++) vertices[i] = root.InverseTransformPoint(renderer.TransformPoint(vertices[i]));
            return vertices;
        }

        private static void WriteSourceChangeStop(string root, string erect, string flaccid)
        {
            File.WriteAllText(Path.Combine(root, "source-change-stop.json"), JsonUtility.ToJson(new SourceChange { expectedErect = ErectHash, actualErect = erect, expectedFlaccid = FlaccidHash, actualFlaccid = flaccid }, true) + "\n");
        }

        private static string HashFile(string path)
        {
            using (var sha = SHA256.Create()) using (var stream = File.OpenRead(path)) return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }

        private static Transform FindPath(Transform root, string path)
        {
            string normalized = path.StartsWith(root.name + "/", StringComparison.Ordinal) ? path.Substring(root.name.Length + 1) : path;
            Transform current = root;
            foreach (string part in normalized.Split('/'))
            {
                if (part == root.name) continue;
                current = current.Find(part);
                if (current == null) return null;
            }
            return current;
        }

        private static string HierarchyPath(Transform root, Transform target)
        {
            if (target == null) return string.Empty;
            var stack = new Stack<string>();
            Transform p = target;
            while (p != null && p != root) { stack.Push(p.name); p = p.parent; }
            return string.Join("/", stack.ToArray());
        }

        private static Vector3[] LerpPoints(Vector3[] a, Vector3[] b, float t)
        {
            var result = new Vector3[a.Length];
            for (int i = 0; i < a.Length; i++) result[i] = Vector3.LerpUnclamped(a[i], b[i], t);
            return result;
        }

        private static bool SegmentsNonzero(Vector3[] line)
        {
            for (int i = 1; i < line.Length; i++) if ((line[i] - line[i - 1]).sqrMagnitude < 1e-10f) return false;
            return true;
        }

        private static float PolylineLength(Vector3[] points)
        {
            float result = 0f;
            for (int i = 1; i < points.Length; i++) result += Vector3.Distance(points[i - 1], points[i]);
            return result;
        }

        private static float MinimumContinuousSegment(Vector3[] a, Vector3[] b)
        {
            float minimum = float.PositiveInfinity;
            for (int i = 1; i < a.Length; i++)
            {
                Vector3 start = a[i] - a[i - 1];
                Vector3 change = (b[i] - b[i - 1]) - start;
                float t = change.sqrMagnitude < 1e-12f ? 0f : Mathf.Clamp01(-Vector3.Dot(start, change) / change.sqrMagnitude);
                minimum = Mathf.Min(minimum, (start + change * t).magnitude);
            }
            return minimum;
        }

        private static float DistanceToPolyline(Vector3 point, Vector3[] line)
        {
            float min = float.PositiveInfinity;
            for (int i = 1; i < line.Length; i++)
            {
                Vector3 v = line[i] - line[i - 1];
                float t = Mathf.Clamp01(Vector3.Dot(point - line[i - 1], v) / Mathf.Max(v.sqrMagnitude, 1e-12f));
                min = Mathf.Min(min, Vector3.Distance(point, line[i - 1] + t * v));
            }
            return min;
        }

        private static string CenterlineCsv(LineData erect, LineData flaccid)
        {
            var text = new StringBuilder("position01,erectX,erectY,erectZ,flaccidX,flaccidY,flaccidZ,erectRadiusCm,flaccidRadiusCm\n");
            for (int i = 0; i < erect.pointsRoot.Length; i++)
            {
                float t = i / (float)(erect.pointsRoot.Length - 1);
                Vector3 e = erect.pointsRoot[i], f = flaccid.pointsRoot[i];
                text.AppendFormat(System.Globalization.CultureInfo.InvariantCulture, "{0:F4},{1:F7},{2:F7},{3:F7},{4:F7},{5:F7},{6:F7},{7:F5},{8:F5}\n", t, e.x, e.y, e.z, f.x, f.y, f.z, erect.nominalRadius * 100f, flaccid.nominalRadius * 100f);
            }
            return text.ToString();
        }

        private static void Require(bool condition, string message) { if (!condition) throw new InvalidDataException(message); }

        [Serializable] private sealed class ProofReport
        {
            public SourceHash[] sourceHashes;
            public string canonicalBody;
            public string canonicalSkeleton;
            public string canonicalShell;
            public MeshResult body;
            public MeshResult shell;
            public ImportProof importer;
            public CenterlineProof geometryCenterline;
            public bool endpointsNumericallyVerified;
            public string visualAcceptance;
            public string donorPelvis;
            public bool sourceFilesUnmodified;
        }
        [Serializable] private sealed class SourceHash { public string path; public string sha256; }
        [Serializable] private sealed class SourceChange { public string expectedErect; public string actualErect; public string expectedFlaccid; public string actualFlaccid; }
        [Serializable] private sealed class ImportProof { public string erectWeights; public string flaccidWeights; public int erectMaxBonesPerVertex; public int flaccidMaxBonesPerVertex; public float erectMinBoneWeight; public float flaccidMinBoneWeight; public bool erectOptimizeGameObjects; public bool constraintsImported; public string runtimeQualityCap; }
        [Serializable] private sealed class BoneMismatch { public float erection01; public string bone; public float distanceMm; }
        [Serializable] private sealed class CenterlineProof
        {
            public string method; public float erectLengthCm; public float flaccidLengthCm; public float erectRadiusCm; public float flaccidRadiusCm;
            public int[] erectSectionPointCounts; public int[] flaccidSectionPointCounts; public BoneMismatch[] pivotMismatch; public float[] states01;
            public int linearInterpolatedCenterlinePointCount; public bool noZeroSegments; public float minimumContinuousSegmentCm; public float[] sampleLengthsCm;
        }
    }

    [InitializeOnLoad]
    internal static class PartnerRigAcceptanceOneShot
    {
        private const string Trigger = "Library/PartnerRigAcceptanceBuild.once";
        static PartnerRigAcceptanceOneShot()
        {
            EditorApplication.delayCall += () =>
            {
                string path = Path.GetFullPath(Path.Combine(Directory.GetParent(Application.dataPath).FullName, Trigger));
                if (!File.Exists(path)) return;
                File.Delete(path);
                try { PartnerRigAcceptanceBuilder.BuildFromOneShotRequest(); }
                catch (Exception e)
                {
                    string root = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "TestOutput", "G8MArtistErectionProof");
                    Directory.CreateDirectory(root);
                    File.WriteAllText(Path.Combine(root, "UnityBuildFailure.txt"), e.ToString());
                    Debug.LogException(e);
                }
            };
        }
    }
}
