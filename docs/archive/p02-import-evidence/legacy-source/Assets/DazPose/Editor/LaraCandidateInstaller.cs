using System;
using System.IO;
using System.Linq;
using DazPose.Editor.ParticleBody;
using DazPose.Performer;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DazPose.UnityValidation
{
    public static class LaraCandidateInstaller
    {
        public const string ValidationScene = LaraCandidateBuilder.Folder + "/FirstPerformanceVoidLaraCandidate.unity";
        private const string OriginalScene = "Assets/Scenes/FirstPerformanceVoid.unity";
        [Serializable] private class Audit
        {
            public int canonicalBonesReused, graftBonesAdded, channels, materials, particleBindings;
            public float maximumBindMatrixError;
            public bool dissolveReady, particleReady, anatomyReady, salsaReady, canonicalMorphIndicesPreserved;
        }

        [MenuItem("Tools/DAZ Pose/Development/Open First Performance Void Lara Candidate")]
        public static void OpenCandidate()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            BuildValidationScene();
            Selection.activeGameObject = UnityEngine.Object.FindAnyObjectByType<SuccubusPerformer>().gameObject;
        }

        // Build a full existing-scene replacement proof while preserving the source scene.
        public static void BuildAll()
        {
            LaraCandidateBuilder.Build();
            BuildValidationScene();
        }

        public static void BuildValidationScene()
        {
            LaraCandidateBuilder.ReadManifest();
            if (AssetDatabase.LoadAssetAtPath<Mesh>(LaraCandidateBuilder.MeshPath) == null) LaraCandidateBuilder.Build();
            EditorSceneManager.OpenScene(OriginalScene, OpenSceneMode.Single);
            ApplyToLoadedScene();
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), ValidationScene);
            AssetDatabase.SaveAssets();
        }

        [MenuItem("Tools/DAZ Pose/Development/Replace Lara In First Performance Void")]
        public static void ReplaceInOriginal()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            LaraCandidateBuilder.ReadManifest();
            string backup = LaraCandidateBuilder.Folder + "/FirstPerformanceVoidBeforeLara.unity";
            if (!File.Exists(backup))
            {
                File.Copy(OriginalScene, backup);
                AssetDatabase.ImportAsset(backup);
            }
            EditorSceneManager.OpenScene(OriginalScene, OpenSceneMode.Single);
            ApplyToLoadedScene();
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
        }

        private static void ApplyToLoadedScene()
        {
            var performer = UnityEngine.Object.FindAnyObjectByType<SuccubusPerformer>();
            if (performer == null) throw new InvalidOperationException("FirstPerformanceVoid has no performer.");
            var animator = performer.GetComponentInChildren<Animator>(true);
            if (!PerformerLipSyncMorphCatalog.TryResolveBindings(animator,out var speechBindings,out string speechReason))
                throw new InvalidOperationException(speechReason);
            var renderer = speechBindings[0].Renderer;
            var candidate = AssetDatabase.LoadAssetAtPath<Mesh>(LaraCandidateBuilder.MeshPath);
            if (candidate == null) throw new InvalidOperationException("Build the Lara candidate before installing.");
            var source = LaraCandidateBuilder.Body(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/TestCharacter/l.aranudeclosed.fbx"));
            var original = LaraCandidateBuilder.Body(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/TestCharacter/lara.fbx"));
            var audit = new Audit { channels = candidate.blendShapeCount, materials = candidate.subMeshCount, canonicalMorphIndicesPreserved = true };
            for (int i = 0; i < original.sharedMesh.blendShapeCount; i++)
                if (original.sharedMesh.GetBlendShapeName(i) != candidate.GetBlendShapeName(i)) throw new IOException("Canonical facial indices changed.");
            var canonical = renderer.bones.Where(b => b != null).GroupBy(b => b.name).ToDictionary(g => g.Key, g => g.Single());
            var originalIndices = original.bones.Select((b,i) => (b.name,i)).ToDictionary(x => x.name, x => x.i);
            Transform[] remapped = new Transform[source.bones.Length];
            Matrix4x4[] bind = candidate.bindposes;
            for (int i = 0; i < source.bones.Length; i++)
            {
                string name = source.bones[i].name;
                if (!originalIndices.TryGetValue(name, out int originalIndex)) continue;
                if (!canonical.TryGetValue(name, out remapped[i])) throw new IOException("Missing canonical bone " + name);
                float error = MatrixError(bind[i], original.sharedMesh.bindposes[originalIndex]);
                audit.maximumBindMatrixError = Mathf.Max(error,audit.maximumBindMatrixError);
                if (error > 1e-4f) throw new IOException("Canonical bind pose differs for " + name + ": " + error);
                audit.canonicalBonesReused++;
            }
            foreach (string name in new[] { "Vagina", "Fluid", "Anus" })
            {
                int index = Array.FindIndex(source.bones, b => b.name == name);
                if (index < 0) throw new IOException("Graft bone missing " + name);
                string parentName = name == "Fluid" ? "Vagina" : "pelvis";
                int parentIndex = Array.FindIndex(source.bones, b => b.name == parentName);
                Transform parent = remapped[parentIndex];
                if (parent == null) throw new IOException("Graft parent missing " + parentName);
                Transform bone = parent.Find("LaraGraft_" + name);
                if (bone == null) { bone = new GameObject("LaraGraft_" + name).transform; bone.SetParent(parent,false); }
                Matrix4x4 localRest = bind[parentIndex] * bind[index].inverse;
                bone.localPosition = localRest.GetColumn(3);
                bone.localRotation = localRest.rotation;
                bone.localScale = localRest.lossyScale;
                if (MatrixError(Matrix4x4.TRS(bone.localPosition,bone.localRotation,bone.localScale),localRest) > 1e-5f)
                    throw new IOException("Graft rest matrix contains unsupported shear " + name);
                remapped[index] = bone;
                audit.graftBonesAdded++;
            }
            if (remapped.Any(b => b == null)) throw new IOException("Unmapped donor bones remain.");
            if (audit.canonicalBonesReused != original.bones.Length) throw new IOException("Not all canonical bones reused.");
            Material[] materials = LaraCandidateBuilder.ReadManifest().materials.Select(s => AssetDatabase.LoadAssetAtPath<Material>(
                LaraCandidateBuilder.Folder + "/RuntimeMaterials/" + s.slot.ToString("D2") + "_" + s.node + "_" + s.surface + ".mat")).ToArray();
            if (materials.Any(m => m == null)) throw new IOException("Converted runtime materials missing.");
            renderer.sharedMesh = candidate;
            renderer.bones = remapped;
            renderer.sharedMaterials = materials;
            renderer.localBounds = source.localBounds;
            for (int i = 0; i < candidate.blendShapeCount; i++) renderer.SetBlendShapeWeight(i,0);
            var anatomy = performer.GetComponent<LaraAnatomyControls>() ?? performer.gameObject.AddComponent<LaraAnatomyControls>();
            anatomy.Configure(renderer,LaraCandidateBuilder.ReadManifest().breastsBakedValue);
            audit.anatomyReady = anatomy.IsReady;
            var performerData = new SerializedObject(performer);
            var oldProfile = performerData.FindProperty("dissolveProfile").objectReferenceValue as PerformerDissolveProfile;
            if (oldProfile == null) throw new IOException("Existing dissolve profile missing.");
            if (!oldProfile.IsShaderReady(out string originalProfileReason)) throw new IOException(originalProfileReason);
            var profile = LaraCandidateBuilder.Save(UnityEngine.Object.Instantiate(oldProfile), LaraCandidateBuilder.Folder + "/LaraDissolveProfile.asset");
            profile.ConfigureConvertedMaterials(oldProfile.SssDissolveShader,oldProfile.WetDissolveShader,materials,materials.Select(m => m.shader).ToArray());
            var rig = performer.GetComponent<PerformerDissolveRig>();
            var particle = rig == null ? null : rig.ParticleBody;
            if (particle == null) throw new IOException("Existing particle body missing.");
            var bindings = PerformerSurfaceBindingBaker.Bake(renderer,PerformerSurfaceBindingAsset.RequiredBindingCount,0x504f3942,
                LaraCandidateBuilder.Folder + "/LaraSurfaceBindings.asset");
            var particleData = new SerializedObject(particle);
            particleData.FindProperty("targetRenderer").objectReferenceValue = renderer;
            particleData.FindProperty("surfaceBindings").objectReferenceValue = bindings;
            particleData.ApplyModifiedPropertiesWithoutUndo();
            profile.ConfigureParticleBodyAssets(particle.VisualEffectAsset,bindings);
            performerData.FindProperty("dissolveProfile").objectReferenceValue = profile;
            performerData.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(profile);
            audit.dissolveReady = rig != null && rig.IsReady(profile,out string reason);
            if (!audit.dissolveReady) throw new IOException("Converted dissolve/particle configuration is not ready.");
            audit.particleReady = particle.ValidateConfiguration(out _);
            audit.particleBindings = bindings.BindingCount;
            audit.salsaReady = performer.GetComponent<PerformerSalsaLipSync>().ValidateConfiguration(out string salsaReason);
            if (!audit.salsaReady) throw new IOException(salsaReason);
            File.WriteAllText("TestOutput/appearance-evidence/lara-scene-integration.validation.json",JsonUtility.ToJson(audit,true));
            Debug.Log("LARA_SCENE_INTEGRATION_VALIDATED: " + JsonUtility.ToJson(audit));
        }
        private static float MatrixError(Matrix4x4 a, Matrix4x4 b)
        {
            float error = 0;
            for (int i = 0; i < 16; i++) error = Mathf.Max(error,Mathf.Abs(a[i]-b[i]));
            return error;
        }
    }
}
