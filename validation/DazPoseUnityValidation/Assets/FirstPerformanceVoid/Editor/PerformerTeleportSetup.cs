using System;
using System.IO;
using System.Linq;
using DazPose.Performer;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DazPose.FirstPerformanceVoid.Editor
{
    /// <summary>Installs only P0.F assets, references, and floor-safe acceptance marks.</summary>
    public static class PerformerTeleportSetup
    {
        private const string ScenePath = "Assets/Scenes/FirstPerformanceVoid.unity";
        private const string Folder = "Assets/DazPose/Effects/Teleport";
        private const string ProfilePath = Folder + "/FirstContactTeleportProfile.asset";
        private const string PrefabPath = Folder + "/MagentaTeleportFlash.prefab";
        private const string SoundPath = "Assets/teleportsound.mp3";
        private const string ParticleShaderGraphPath = Folder + "/ParticleUnlit.shadergraph";
        private const string ParticleVertexColorPath = Folder + "/Particle Vertex Color.shadersubgraph";
        private const string ParticleShaderName = "Shader Graphs/Particles/ParticleUnlit";

        [MenuItem("Tools/DAZ Pose/First Performance Void/Install Teleport Acceptance Harness")]
        public static void Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Install teleport acceptance in Edit Mode.");
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            if (!scene.IsValid() || !scene.isLoaded)
                throw new InvalidOperationException("Open the existing FirstPerformanceVoid scene before installing teleport acceptance.");

            FirstPerformanceVoidControls controls = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<FirstPerformanceVoidControls>(true)).SingleOrDefault();
            if (controls == null) throw new InvalidOperationException("The loaded lounge has no FirstPerformanceVoidControls.");
            var controlsData = new SerializedObject(controls);
            SuccubusPerformer performer = Read<SuccubusPerformer>(controlsData, "performer");
            Transform acrossFloor = Read<Transform>(controlsData, "acrossFloor");
            if (performer == null || performer.gameObject.scene != scene || acrossFloor == null
                || acrossFloor.gameObject.scene != scene)
                throw new InvalidOperationException("The existing Lara reference and floor-level Walk across Floor marker are required.");

            EnsureFolder("Assets/DazPose/Effects");
            EnsureFolder(Folder);
            AssetDatabase.Refresh();
            Shader particleShader = ImportParticleShader();
            Material magenta = LoadOrCreateMaterial(Folder + "/M_TeleportMagenta.mat", particleShader, new Color(2.2f, 0.05f, 2.5f, 1f));
            Material violet = LoadOrCreateMaterial(Folder + "/M_TeleportViolet.mat", particleShader, new Color(0.48f, 0.04f, 2.8f, 1f));
            GameObject effectPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (effectPrefab == null) effectPrefab = CreateEffectPrefab(magenta, violet);

            AudioClip teleportAudio = AssetDatabase.LoadAssetAtPath<AudioClip>(SoundPath);
            var performerData = new SerializedObject(performer);
            SerializedProperty profileProperty = performerData.FindProperty("teleportProfile");
            if (profileProperty == null) throw new InvalidOperationException("SuccubusPerformer has no teleport profile field.");
            PerformerTeleportProfile profile = profileProperty.objectReferenceValue as PerformerTeleportProfile;
            bool assignDefaultProfile = profile == null;
            if (assignDefaultProfile) profile = AssetDatabase.LoadAssetAtPath<PerformerTeleportProfile>(ProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<PerformerTeleportProfile>();
                profile.ConfigureIfMissing(effectPrefab, teleportAudio, teleportAudio);
                AssetDatabase.CreateAsset(profile, ProfilePath);
            }
            else if (AssetDatabase.GetAssetPath(profile) == ProfilePath)
            {
                profile.ConfigureIfMissing(effectPrefab, teleportAudio, teleportAudio);
                EditorUtility.SetDirty(profile);
            }

            if (!profile.IsReady(out string profileReason))
                throw new InvalidOperationException(profileReason);
            if (EditorUtility.IsDirty(profile)) AssetDatabase.SaveAssetIfDirty(profile);
            if (assignDefaultProfile)
            {
                profileProperty.objectReferenceValue = profile;
                performerData.ApplyModifiedProperties();
            }

            PerformerPose arrivalPose = Read<PerformerPose>(controlsData, "teleportArrivalPose");
            PerformerPoseSmokeHarness smoke = performer.GetComponent<PerformerPoseSmokeHarness>();
            if (arrivalPose == null && smoke != null)
            {
                var smokeData = new SerializedObject(smoke);
                arrivalPose = Read<PerformerPose>(smokeData, "poseB")
                    ?? Read<PerformerPose>(smokeData, "poseA")
                    ?? Read<PerformerPose>(smokeData, "poseC");
            }
            if (arrivalPose == null || arrivalPose.Clip == null)
                throw new InvalidOperationException("Assign an existing PerformerPose with an AnimationClip on the smoke harness for the arrival-pose test.");

            Transform markers = controls.transform.Find("PerformanceMarkers");
            if (markers == null) throw new InvalidOperationException("The existing PerformanceMarkers group is missing.");
            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Install Teleport Acceptance Harness");
            Transform markA;
            Transform markB;
            try
            {
                Transform testMarks = Child(markers, "TeleportAcceptance", out _);
                markA = Child(testMarks, "TeleportMark_A", out bool createdA);
                markB = Child(testMarks, "TeleportMark_B", out bool createdB);
                if (createdA) markA.SetPositionAndRotation(performer.transform.position, performer.transform.rotation);
                if (createdB)
                {
                    Vector3 destination = acrossFloor.position;
                    destination.y = markA.position.y;
                    Vector3 delta = Vector3.ProjectOnPlane(destination - markA.position, Vector3.up);
                    if (delta.sqrMagnitude < 9f)
                    {
                        Vector3 forward = Vector3.ProjectOnPlane(performer.transform.forward, Vector3.up).normalized;
                        destination = markA.position + (forward.sqrMagnitude > 0.5f ? forward : Vector3.forward) * 5f;
                        destination.y = markA.position.y;
                    }
                    markB.SetPositionAndRotation(destination, acrossFloor.rotation);
                }

                Undo.RecordObject(controls, "Wire Teleport Acceptance Harness");
                controls.ConfigureTeleportAcceptance(markA, markB, arrivalPose);
                EditorUtility.SetDirty(performer);
                EditorUtility.SetDirty(controls);
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene))
                    throw new InvalidOperationException("Could not save the loaded FirstPerformanceVoid scene.");
                Undo.CollapseUndoOperations(undoGroup);
            }
            catch
            {
                Undo.RevertAllDownToGroup(undoGroup);
                throw;
            }

            bool audioWired = profile.DepartureAudio != null && profile.ArrivalAudio != null;
            Debug.Log("TELEPORT_ACCEPTANCE_INSTALLED: effect=" + AssetDatabase.GetAssetPath(effectPrefab)
                + "; profile=" + AssetDatabase.GetAssetPath(profile)
                + "; departure/arrival audio=" + (audioWired ? AssetDatabase.GetAssetPath(profile.DepartureAudio) : "not assigned")
                + "; arrival pose=" + arrivalPose.name
                + "; A=" + markA.position.ToString("F3") + "; B=" + markB.position.ToString("F3")
                + ". Existing room, First Contact choreography, camera, anchors, lights, smoke, materials and tuned markers are preserved.", controls);
        }

        private static Material LoadOrCreateMaterial(string path, Shader shader, Color emission)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null && material.shader == shader) return material;
            if (shader == null) throw new InvalidOperationException("The HDRP Particle Unlit Shader Graph could not be loaded.");
            if (material != null)
            {
                material.shader = shader;
                ConfigureParticleMaterial(material, emission);
                EditorUtility.SetDirty(material);
                return material;
            }
            material = new Material(shader) { name = System.IO.Path.GetFileNameWithoutExtension(path), enableInstancing = true };
            ConfigureParticleMaterial(material, emission);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static void ConfigureParticleMaterial(Material material, Color emission)
        {
            SetColorIfPresent(material, "_BaseColor", emission);
            SetColorIfPresent(material, "_EmissiveColor", emission);
            SetColorIfPresent(material, "_EmissiveColorLDR", emission);
            SetColorIfPresent(material, "_EmissionColor", emission);
            material.EnableKeyword("_EMISSION");
        }

        private static Shader ImportParticleShader()
        {
            UnityEditor.PackageManager.PackageInfo hdrp = UnityEditor.PackageManager.PackageInfo.FindForAssetPath("Packages/com.unity.render-pipelines.high-definition");
            UnityEditor.PackageManager.PackageInfo shaderGraph = UnityEditor.PackageManager.PackageInfo.FindForAssetPath("Packages/com.unity.shadergraph");
            if (hdrp == null || shaderGraph == null)
                throw new InvalidOperationException("HDRP and Shader Graph packages are required for the teleport particle effect.");

            string packageShader = Path.Combine(hdrp.resolvedPath, "Samples~", "ParticleSystemShaderSamples", "Shaders", "ParticleUnlit.shadergraph");
            string packageSubgraph = Path.Combine(shaderGraph.resolvedPath, "GraphTemplates", "Subgraphs", "Particle Vertex Color.shadersubgraph");
            string subgraphGuid = CopyShaderGraphAsset(packageSubgraph, ParticleVertexColorPath, null, null);
            CopyShaderGraphAsset(packageShader, ParticleShaderGraphPath,
                "73f7a6a1a6335484ba02a0b8f80fe10c", subgraphGuid);

            AssetDatabase.ImportAsset(ParticleVertexColorPath, ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.ImportAsset(ParticleShaderGraphPath, ImportAssetOptions.ForceSynchronousImport);

            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(ParticleShaderGraphPath);
            if (shader == null) shader = Shader.Find(ParticleShaderName);
            if (shader == null)
                throw new InvalidOperationException("Unity could not compile the HDRP Particle Unlit Shader Graph at " + ParticleShaderGraphPath + ". Check Shader Graph import errors in the Console.");
            return shader;
        }

        private static string CopyShaderGraphAsset(string sourcePath, string destinationPath,
            string dependencyGuid, string localDependencyGuid)
        {
            if (File.Exists(destinationPath)) return ReadAssetGuid(destinationPath + ".meta");
            if (!File.Exists(sourcePath) || !File.Exists(sourcePath + ".meta"))
                throw new InvalidOperationException("Required HDRP particle shader asset is missing from the installed package: " + sourcePath);

            string metadata = File.ReadAllText(sourcePath + ".meta");
            string sourceGuid = ReadAssetGuid(sourcePath + ".meta");
            string localGuid = Guid.NewGuid().ToString("N");
            metadata = metadata.Replace("guid: " + sourceGuid, "guid: " + localGuid);

            string contents = File.ReadAllText(sourcePath);
            if (!string.IsNullOrEmpty(dependencyGuid))
            {
                if (string.IsNullOrEmpty(localDependencyGuid))
                    throw new InvalidOperationException("Could not assign a local GUID to the particle vertex-color subgraph.");
                contents = contents.Replace(dependencyGuid, localDependencyGuid);
            }

            File.WriteAllText(destinationPath, contents);
            File.WriteAllText(destinationPath + ".meta", metadata);
            return localGuid;
        }

        private static string ReadAssetGuid(string metadataPath)
        {
            foreach (string line in File.ReadAllLines(metadataPath))
            {
                if (line.StartsWith("guid: ", StringComparison.Ordinal))
                    return line.Substring("guid: ".Length).Trim();
            }
            throw new InvalidOperationException("Unity asset metadata has no GUID: " + metadataPath);
        }

        private static GameObject CreateEffectPrefab(Material magenta, Material violet)
        {
            var root = new GameObject("Magenta Teleport Flash");
            try
            {
                CreateBurst(root.transform, "Core Flash", Vector3.up * 0.95f, magenta,
                    ParticleSystemShapeType.Sphere, 0.25f, 34, 0.18f, 0.2f, 0.7f, false);
                CreateBurst(root.transform, "Vertical Sparks Up", Vector3.up * 0.12f, violet,
                    ParticleSystemShapeType.Cone, 0.32f, 30, 0.32f, 0.25f, 5.5f, false);
                CreateBurst(root.transform, "Vertical Sparks Down", Vector3.up * 1.8f, magenta,
                    ParticleSystemShapeType.Cone, 0.32f, 26, 0.3f, 0.25f, 5.2f, true);
                CreateRing(root.transform, "Expanding Floor Ring", Vector3.up * 0.06f, magenta);
                CreateBurst(root.transform, "Residual Motes", Vector3.up * 0.85f, violet,
                    ParticleSystemShapeType.Sphere, 0.48f, 20, 0.65f, 0.075f, 1.1f, false);
                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                if (prefab == null) throw new InvalidOperationException("Unity could not save the HDRP teleport effect prefab.");
                return prefab;
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static void CreateBurst(Transform parent, string name, Vector3 localPosition, Material material,
            ParticleSystemShapeType shapeType, float radius, int count, float lifetime, float size, float speed, bool down)
        {
            var item = new GameObject(name);
            item.transform.SetParent(parent, false);
            item.transform.localPosition = localPosition;
            if (down) item.transform.localRotation = Quaternion.Euler(180f, 0f, 0f);
            var particles = item.AddComponent<ParticleSystem>();
            var main = particles.main;
            main.loop = false;
            main.playOnAwake = true;
            main.duration = 0.12f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime * 0.75f, lifetime);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.65f, speed);
            main.startSize = new ParticleSystem.MinMaxCurve(size * 0.65f, size);
            main.startColor = new ParticleSystem.MinMaxGradient(CreateGlowGradient());
            main.maxParticles = count + 4;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            var emission = particles.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });
            var shape = particles.shape;
            shape.shapeType = shapeType;
            shape.radius = radius;
            if (shapeType == ParticleSystemShapeType.Cone) shape.angle = 8f;
            var color = particles.colorOverLifetime;
            color.enabled = true;
            color.color = CreateFadeGradient();
            var sizeOverLifetime = particles.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f,
                new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f)));
            ConfigureRenderer(particles, material);
            particles.Play();
        }

        private static void CreateRing(Transform parent, string name, Vector3 localPosition, Material material)
        {
            var item = new GameObject(name);
            item.transform.SetParent(parent, false);
            item.transform.localPosition = localPosition;
            var particles = item.AddComponent<ParticleSystem>();
            var main = particles.main;
            main.loop = false;
            main.playOnAwake = true;
            main.duration = 0.1f;
            main.startLifetime = 0.36f;
            main.startSpeed = 0.4f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.045f, 0.09f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(2.3f, 0.06f, 2.7f, 1f));
            main.maxParticles = 72;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = particles.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)64) });
            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.12f;
            shape.rotation = new Vector3(90f, 0f, 0f);
            var velocity = particles.velocityOverLifetime;
            velocity.enabled = true;
            velocity.radial = new ParticleSystem.MinMaxCurve(2.7f);
            var color = particles.colorOverLifetime;
            color.enabled = true;
            color.color = CreateFadeGradient();
            ConfigureRenderer(particles, material);
            particles.Play();
        }

        private static void ConfigureRenderer(ParticleSystem particles, Material material)
        {
            var renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.alignment = ParticleSystemRenderSpace.View;
            renderer.material = material;
            renderer.sortingFudge = 4f;
        }

        private static Gradient CreateGlowGradient()
        {
            var gradient = new Gradient();
            gradient.SetKeys(new[]
            {
                new GradientColorKey(new Color(3f, 2.4f, 3.2f), 0f),
                new GradientColorKey(new Color(2.4f, 0.08f, 2.8f), 0.35f),
                new GradientColorKey(new Color(0.55f, 0.02f, 1.4f), 1f)
            }, new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.45f), new GradientAlphaKey(0f, 1f) });
            return gradient;
        }

        private static Gradient CreateFadeGradient()
        {
            var gradient = new Gradient();
            gradient.SetKeys(new[]
            {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(new Color(1f, 0.15f, 1f), 1f)
            }, new[] { new GradientAlphaKey(0.95f, 0f), new GradientAlphaKey(0.7f, 0.3f), new GradientAlphaKey(0f, 1f) });
            return gradient;
        }

        private static Transform Child(Transform parent, string name, out bool created)
        {
            Transform child = parent.Find(name);
            created = child == null;
            if (!created) return child;
            var gameObject = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(gameObject, "Create " + name);
            gameObject.transform.SetParent(parent, false);
            return gameObject.transform;
        }

        private static T Read<T>(SerializedObject source, string name) where T : UnityEngine.Object =>
            source.FindProperty(name)?.objectReferenceValue as T;

        private static void SetColorIfPresent(Material material, string property, Color value)
        {
            if (material.HasProperty(property)) material.SetColor(property, value);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int separator = path.LastIndexOf('/');
            EnsureFolder(path.Substring(0, separator));
            AssetDatabase.CreateFolder(path.Substring(0, separator), path.Substring(separator + 1));
        }
    }
}
