using System;
using System.IO;
using System.Linq;
using DazPose.Performer;
using DazPose.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace DazPose.FirstPerformanceVoid.Editor
{
    /// <summary>Focused initial room construction. Existing scenes are opened, never rebuilt.</summary>
    public static class FirstPerformanceVoidBuilder
    {
        public const string ScenePath = "Assets/Scenes/FirstPerformanceVoid.unity";
        private const string SourceScenePath = "Assets/Scenes/PoseValidation.unity";
        private const string Root = "Assets/FirstPerformanceVoid";
        private const string ProfilePath = Root + "/Settings/V_FirstPerformanceVoid.asset";
        private const string SeatPrefabPath = Root + "/Prefabs/ValidatedLoungeSeat.prefab";
        private const string MaskPath = Root + "/Textures/T_FogDensity32.asset";
        private const string RingPath = Root + "/Meshes/StageGlowRing.asset";
        private static readonly Color Neon = new Color(1f, 0.015f, 1f);

        [MenuItem("Tools/DAZ Pose/First Performance Void/Build or Open Lounge")]
        public static void BuildOrOpen()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
            {
                EditorSceneManager.OpenScene(ScenePath);
                return;
            }
            Build(SourceScenePath);
        }

        // Used only for native asset generation in a separate staging project. No Play Mode tests.
        public static void BuildFromCommandLine()
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "-firstPerformanceSource");
            string path = index >= 0 && index + 1 < args.Length ? args[index + 1] : SourceScenePath;
            // Batch startup has an unsaved untitled scene; replace it before additive construction.
            EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            Build(path);
        }

        [MenuItem("Tools/DAZ Pose/First Performance Void/Refresh Generated Materials and Volume Defaults")]
        public static void RefreshLookDefaults()
        {
            // Explicitly requested refresh resets only these owned assets, never scene placement.
            EnsureFolders();
            CreateLook(true, out _, out _, out _);
            AssetDatabase.SaveAssets();
            Debug.Log("Refreshed First Performance Void material/Volume defaults. Chair, anchors, performer and fog-bank transforms were not rebuilt.");
        }

        private static void Build(string sourcePath)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
            {
                Debug.Log("First Performance Void already exists. Initial builder leaves all manual tuning intact.");
                return;
            }
            EnsureFolders();
            Scene previousActive = SceneManager.GetActiveScene();
            Scene source = SceneManager.GetSceneByPath(sourcePath);
            bool openedSource = !source.IsValid() || !source.isLoaded;
            if (openedSource) source = EditorSceneManager.OpenScene(sourcePath, OpenSceneMode.Additive);
            Scene scene = default;
            try
            {
                SuccubusPerformer sourcePerformer = source.GetRootGameObjects()
                    .SelectMany(item => item.GetComponentsInChildren<SuccubusPerformer>(true)).SingleOrDefault();
                if (sourcePerformer == null) throw new InvalidOperationException("Source scene must contain exactly one working SuccubusPerformer.");
                PerformerPoseSmokeHarness sourceSmoke = sourcePerformer.GetComponent<PerformerPoseSmokeHarness>();
                if (sourceSmoke == null) throw new InvalidOperationException("The source performer must already have its working smoke harness.");
                var sourceSmokeData = new SerializedObject(sourceSmoke);
                PerformerSeat sourceSeat = sourceSmokeData.FindProperty("seatingTestSeat").objectReferenceValue as PerformerSeat;
                if (sourceSeat == null || sourceSeat.ApproachAnchor == null || sourceSeat.SeatAnchor == null)
                    throw new InvalidOperationException("Source scene has no configured working chair. Save the manually tuned working PoseValidation setup first; the builder will not recreate or guess its geometry.");
                if (sourceSeat.gameObject.scene != source)
                    throw new InvalidOperationException("The working chair must belong to the source scene.");
                if (sourceSeat.SeatingProfile == null) throw new InvalidOperationException("Working chair has no seating profile.");
                if (!sourceSeat.SeatingProfile.IsReady(out string seatReason)) throw new InvalidOperationException(seatReason);
                if (sourcePerformer.LocomotionProfile == null || !sourcePerformer.LocomotionProfile.IsReady(out _))
                    throw new InvalidOperationException("Source performer must have its working baked locomotion profile.");

                scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                SceneManager.SetActiveScene(scene);
                CreateLook(false, out Material black, out Material neon, out Material accent);
                Transform room = Group("FirstPerformanceVoid", null);
                Transform environment = Group("Environment", room);
                Transform floor = Group("Floor", environment);
                Primitive("FloorBase", floor, PrimitiveType.Cube, new Vector3(0f, -0.075f, 0f),
                    new Vector3(18f, 0.15f, 14f), black, true);
                Primitive("GlowEdge_Front", floor, PrimitiveType.Cube, new Vector3(0f, 0.014f, -6.95f), new Vector3(17.9f, 0.028f, 0.09f), neon);
                Primitive("GlowEdge_Back", floor, PrimitiveType.Cube, new Vector3(0f, 0.014f, 6.95f), new Vector3(17.9f, 0.028f, 0.09f), neon);
                Primitive("GlowEdge_Left", floor, PrimitiveType.Cube, new Vector3(-8.95f, 0.014f, 0f), new Vector3(0.09f, 0.028f, 13.9f), neon);
                Primitive("GlowEdge_Right", floor, PrimitiveType.Cube, new Vector3(8.95f, 0.014f, 0f), new Vector3(0.09f, 0.028f, 13.9f), neon);

                Transform platform = Group("Platform", environment);
                platform.localPosition = new Vector3(-3f, 0f, 2f);
                Primitive("PlatformBase", platform, PrimitiveType.Cylinder, new Vector3(0f, 0.175f, 0f), new Vector3(4f, 0.175f, 4f), black, true);
                GameObject ring = new GameObject("PlatformGlow", typeof(MeshFilter), typeof(MeshRenderer));
                ring.transform.SetParent(platform, false);
                ring.transform.localPosition = new Vector3(0f, 0.337f, 0f);
                ring.GetComponent<MeshFilter>().sharedMesh = GetRing();
                ring.GetComponent<MeshRenderer>().sharedMaterial = neon;

                Transform lounge = Group("Lounge", environment);
                GameObject sourceChairRoot = sourceSeat.transform.root.gameObject;
                GameObject chair = Object.Instantiate(sourceChairRoot);
                SceneManager.MoveGameObjectToScene(chair, scene);
                chair.name = "Validated Lounge Chair";
                // Capture all authored local geometry, scale and anchor offsets as reusable furniture.
                if (AssetDatabase.LoadAssetAtPath<GameObject>(SeatPrefabPath) == null)
                    PrefabUtility.SaveAsPrefabAssetAndConnect(chair, SeatPrefabPath, InteractionMode.AutomatedAction);
                chair.transform.SetParent(lounge, true);
                chair.transform.position = new Vector3(4.5f, chair.transform.position.y, 2f);
                PerformerSeat seat = chair.GetComponentInChildren<PerformerSeat>(true);
                foreach (Renderer renderer in chair.GetComponentsInChildren<Renderer>(true))
                    renderer.sharedMaterials = renderer.sharedMaterials.Select(_ => accent).ToArray();

                Transform performerGroup = Group("Performer", room);
                GameObject actor = Object.Instantiate(sourcePerformer.transform.root.gameObject);
                SceneManager.MoveGameObjectToScene(actor, scene);
                actor.name = "Lara — Validated Performer";
                actor.transform.SetParent(performerGroup, true);
                SuccubusPerformer performer = actor.GetComponentInChildren<SuccubusPerformer>(true);
                // Translate the complete hierarchy, preserving authored vertical placement and scale.
                actor.transform.position += new Vector3(-performer.transform.position.x, 0f, -3.5f - performer.transform.position.z);
                Transform markers = Group("PerformanceMarkers", room);
                Transform across = Group("Walk Across Floor", markers);
                across.position = new Vector3(5.5f, performer.transform.position.y, -3.5f);
                across.rotation = Quaternion.LookRotation(Vector3.left);
                Transform nearStage = Group("Walk Beside Platform", markers);
                nearStage.position = new Vector3(-3f, performer.transform.position.y, -1.1f);
                nearStage.rotation = Quaternion.LookRotation(Vector3.forward);
                Transform stageSpawn = Group("Manual Platform Composition Marker", markers);
                stageSpawn.position = new Vector3(-3f, 0.35f + performer.transform.position.y, 2f);
                // This is only an editor placement marker; WalkTo never targets the elevated stage.

                Transform laraHead = FirstPerformanceVoidPlayerMigration.FindLaraHead(performer);
                if (laraHead == null) throw new InvalidOperationException("Could not find Lara's animated head bone for FaceViewTarget.");
                Transform faceViewTarget = FirstPerformanceVoidPlayerMigration.EnsureFaceViewTarget(laraHead);
                Camera camera = CreateCamera(room, out PlayerController playerController, out Transform headPose);
                RemapSceneReferences(actor, source, sourceChairRoot.transform, chair.transform, seat, headPose, across);
                FirstPerformanceVoidPlayerMigration.EnsureViewMarkers(room, camera, faceViewTarget, seat,
                    out Transform wide, out Transform lara, out Transform loungeView);
                FirstPerformanceVoidPlayerMigration.RebindGazeTargets(performer, playerController.HeadTransform);
                FirstPerformanceVoidControls controls = room.gameObject.AddComponent<FirstPerformanceVoidControls>();
                controls.Configure(performer, across, nearStage, playerController.HeadTransform);
                controls.ConfigurePlayerView(playerController, wide, lara, loungeView, faceViewTarget);
                CreateLights(Group("Lights", environment));
                CreateVolume(Group("SceneVolumes", room));
                CreateFogBanks(Group("Volumetrics", environment));
                FirstPerformanceVoidLighting.ApplyToScene(scene);
                RenderSettings.skybox = null;
                RenderSettings.ambientMode = AmbientMode.Flat;
                RenderSettings.ambientLight = Color.black;
                RenderSettings.fog = false; // HDRP Fog is supplied by the scene Volume, not legacy fog.

                EditorSceneManager.MarkSceneDirty(scene);
                AssetDatabase.SaveAssets();
                if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new IOException("Could not save the permanent lounge scene.");
                File.WriteAllText(Path.Combine(Root, "Settings", "ConstructionRecord.txt"),
                    "P0.C native Unity scene construction\nSource: " + sourcePath
                    + "\nPerformer: " + sourcePerformer.name + "\nChair: " + sourceChairRoot.name
                    + "\nSource chair world position: " + sourceChairRoot.transform.position.ToString("F4")
                    + "\nApproach local position preserved: " + sourceSeat.ApproachAnchor.localPosition.ToString("F4")
                    + "\nSeat local position preserved: " + sourceSeat.SeatAnchor.localPosition.ToString("F4")
                    + "\nSource chair and performer references copied; external scene harness references remapped to this scene."
                    + "\nFloor 18 x 14 x 0.15 m; stage diameter 4 m, height 0.35 m."
                    + "\nThree realtime lights, four Local Volumetric Fog banks, one shared 32^3 RGBA32 density mask."
                    + "\nScene serialized by Unity. No Play Mode or aesthetic acceptance performed.\n");
                AssetDatabase.Refresh();
                Debug.Log("P0C_BUILD_COMPLETE: " + ScenePath + ". Visual and functional review remains manual.");
            }
            finally
            {
                if (openedSource && source.IsValid() && source.isLoaded) EditorSceneManager.CloseScene(source, true);
                if (!scene.IsValid() && previousActive.IsValid()) SceneManager.SetActiveScene(previousActive);
            }
        }

        private static void RemapSceneReferences(GameObject actor, Scene source, Transform oldChair,
            Transform newChair, PerformerSeat seat, Transform camera, Transform across)
        {
            foreach (MonoBehaviour component in actor.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (component == null) throw new InvalidOperationException("Source performer contains a missing script.");
                var serialized = new SerializedObject(component);
                SerializedProperty property = serialized.GetIterator();
                while (property.Next(true))
                {
                    if (property.propertyType != SerializedPropertyType.ObjectReference) continue;
                    Object value = property.objectReferenceValue;
                    if (property.name == "gazeTarget") property.objectReferenceValue = camera;
                    else if (property.name == "locomotionTarget") property.objectReferenceValue = across;
                    else if (property.name == "seatingTestSeat") property.objectReferenceValue = seat;
                    else
                    {
                        Transform oldTransform = value is GameObject go ? go.transform : (value as Component)?.transform;
                        if (oldTransform == null || oldTransform.gameObject.scene != source) continue;
                        if (oldTransform == oldChair || oldTransform.IsChildOf(oldChair))
                        {
                            string path = AnimationUtility.CalculateTransformPath(oldTransform, oldChair);
                            Transform target = path.Length == 0 ? newChair : newChair.Find(path);
                            if (target == null) throw new InvalidOperationException("Could not remap chair reference " + path);
                            property.objectReferenceValue = value is GameObject ? target.gameObject
                                : value is Transform ? target : target.GetComponent(value.GetType());
                        }
                        else throw new InvalidOperationException("Performer retains an unsupported external validation-scene reference: "
                            + component.GetType().Name + "." + property.propertyPath + " → " + value.name);
                    }
                }
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static Camera CreateCamera(Transform room, out PlayerController playerController, out Transform headPose)
        {
            Transform player = Group("Player", room);
            player.position = new Vector3(9f, 3.2f, -10f);
            player.rotation = Quaternion.identity;
            Transform rig = Group("ViewRig", player);
            rig.position = player.position;
            rig.rotation = Quaternion.LookRotation(new Vector3(0.1f, 1f, 0.7f) - rig.position);
            headPose = Group("HeadPose", rig);
            GameObject cameraObject = new GameObject("MainCamera");
            cameraObject.transform.SetParent(headPose, false);
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 58f;
            camera.nearClipPlane = 0.08f;
            camera.farClipPlane = 70f;
            camera.allowHDR = true;
            cameraObject.AddComponent<AudioListener>();
            HDAdditionalCameraData hd = cameraObject.AddComponent<HDAdditionalCameraData>();
            hd.clearColorMode = HDAdditionalCameraData.ClearColorMode.Color;
            hd.backgroundColorHDR = new Color(0.001f, 0.001f, 0.002f, 1f);
            hd.volumeLayerMask = 1;
            hd.customRenderingSettings = true;
            // The installed pipeline supports volumetrics, but its camera defaults disable them.
            // Override only this room's camera rather than changing project-wide settings.
            foreach (FrameSettingsField field in new[] { FrameSettingsField.AtmosphericScattering,
                FrameSettingsField.Volumetrics, FrameSettingsField.ReprojectionForVolumetrics,
                FrameSettingsField.Postprocess, FrameSettingsField.Bloom, FrameSettingsField.ExposureControl })
            {
                hd.renderingPathCustomFrameSettings.SetEnabled(field, true);
                var mask = hd.renderingPathCustomFrameSettingsOverrideMask;
                mask.mask[(uint)field] = true;
                hd.renderingPathCustomFrameSettingsOverrideMask = mask;
            }
            PlayerView view = player.gameObject.AddComponent<PlayerView>();
            playerController = player.gameObject.AddComponent<PlayerController>();
            view.Configure(rig, headPose, camera);
            playerController.Configure(rig, headPose, camera);
            return camera;
        }

        private static void CreateLights(Transform parent)
        {
            Spot("Soft Neutral Key", parent, new Vector3(1f, 4.5f, -3.2f), new Vector3(0f, 1f, 0f),
                new Color(0.8f, 0.86f, 1f), 6500f, 105f, true);
            Spot("Magenta Platform Accent", parent, new Vector3(-4.2f, 2.8f, 4f), new Vector3(-2f, 0.1f, 1f),
                Neon, 4200f, 100f, false);
            Spot("Magenta Lounge Accent", parent, new Vector3(6.7f, 2.4f, 3.8f), new Vector3(4.5f, 0.4f, 1.5f),
                Neon, 2600f, 105f, false);
        }

        private static void Spot(string name, Transform parent, Vector3 position, Vector3 target,
            Color color, float lumens, float angle, bool shadows)
        {
            Transform transform = Group(name, parent);
            transform.position = position;
            transform.rotation = Quaternion.LookRotation(target - position);
            Light light = transform.gameObject.AddComponent<Light>();
            light.type = LightType.Spot;
            light.range = 20f;
            light.spotAngle = angle;
            light.innerSpotAngle = angle * 0.65f;
            light.color = color;
            light.lightUnit = LightUnit.Lumen;
            light.intensity = lumens;
            light.lightmapBakeType = LightmapBakeType.Realtime;
            light.shadows = shadows ? LightShadows.Soft : LightShadows.None;
            HDAdditionalLightData hd = transform.gameObject.AddComponent<HDAdditionalLightData>();
            hd.volumetricDimmer = 0.45f;
            hd.SetShadowResolution(512);
        }

        private static void CreateVolume(Transform parent)
        {
            Volume volume = Group("GlobalVolume", parent).gameObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 50f;
            volume.sharedProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
        }

        private static void CreateFogBanks(Transform parent)
        {
            Texture3D mask = GetDensityMask();
            FogBank(parent, "FogBank_Left", new Vector3(-6f, 0.35f, 0f), new Vector3(4f, 0.85f, 5f), -18f, 8f, mask, 0);
            FogBank(parent, "FogBank_Rear", new Vector3(0f, 0.5f, 5f), new Vector3(7f, 1.2f, 3.2f), 7f, 12f, mask, 1);
            FogBank(parent, "FogBank_Right", new Vector3(7f, 0.28f, -0.5f), new Vector3(3.5f, 0.65f, 4f), 25f, 10f, mask, 2);
            FogBank(parent, "FogBank_Lounge", new Vector3(4.5f, 0.25f, 4.4f), new Vector3(4f, 0.6f, 2.2f), -12f, 16f, mask, 3);
        }

        private static void FogBank(Transform parent, string name, Vector3 position, Vector3 size,
            float yaw, float meanFreePath, Texture3D mask, int index)
        {
            Transform transform = Group(name, parent);
            transform.position = position;
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            LocalVolumetricFog fog = transform.gameObject.AddComponent<LocalVolumetricFog>();
            var p = new LocalVolumetricFogArtistParameters(new Color(0.58f, 0.6f, 0.65f), meanFreePath, 0f);
            p.size = size;
            p.volumeMask = mask;
            p.maskMode = LocalVolumetricFogMaskMode.Texture;
            p.textureTiling = new Vector3(1f + index * 0.17f, 1f, 1f + index * 0.11f);
            p.textureScrollingSpeed = new Vector3(0.004f + index * 0.0006f, 0f, -0.002f);
            p.positiveFade = new Vector3(0.35f, 0.45f, 0.35f);
            p.negativeFade = new Vector3(0.35f, 0.35f, 0.35f);
            p.distanceFadeStart = 25f;
            p.distanceFadeEnd = 45f;
            p.falloffMode = LocalVolumetricFogFalloffMode.Exponential;
            fog.parameters = p;
        }

        private static void CreateLook(bool refresh, out Material black, out Material neon, out Material accent)
        {
            black = MaterialAsset("M_VoidGlossBlack", new Color(0.012f, 0.014f, 0.021f), 0.84f, 0.08f, false, refresh);
            neon = MaterialAsset("M_NeonMagenta", new Color(0.05f, 0.002f, 0.014f), 0.55f, 0f, true, refresh);
            accent = MaterialAsset("M_DarkAccent", new Color(0.027f, 0.023f, 0.037f), 0.55f, 0.03f, false, refresh);
            VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
            bool create = profile == null;
            if (create)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                profile.name = "V_FirstPerformanceVoid";
                AssetDatabase.CreateAsset(profile, ProfilePath);
            }
            if (!create && !refresh) return;
            Bloom bloom = Component<Bloom>(profile);
            bloom.intensity.Override(0.35f);
            bloom.scatter.Override(0.5f);
            bloom.threshold.Override(1f);
            bloom.dirtIntensity.Override(0f);
            Exposure exposure = Component<Exposure>(profile);
            exposure.mode.Override(ExposureMode.Fixed);
            exposure.fixedExposure.Override(7f);
            exposure.compensation.Override(0f);
            VisualEnvironment environment = Component<VisualEnvironment>(profile);
            environment.skyType.Override(0);
            Fog fog = Component<Fog>(profile);
            fog.enabled.Override(true);
            fog.enableVolumetricFog.Override(true);
            fog.colorMode.Override(FogColorMode.ConstantColor);
            fog.color.Override(new Color(0.035f, 0.035f, 0.045f));
            fog.albedo.Override(new Color(0.55f, 0.58f, 0.62f));
            fog.meanFreePath.Override(120f);
            fog.baseHeight.Override(-0.2f);
            fog.maximumHeight.Override(1.8f);
            fog.maxFogDistance.Override(60f);
            fog.depthExtent.Override(40f);
            fog.anisotropy.Override(0.2f);
            fog.globalLightProbeDimmer.Override(0.15f);
            EditorUtility.SetDirty(profile);
            foreach (VolumeComponent item in profile.components) EditorUtility.SetDirty(item);
        }

        private static T Component<T>(VolumeProfile profile) where T : VolumeComponent
        {
            if (profile.TryGet(out T component)) return component;
            component = profile.Add<T>(true);
            component.name = typeof(T).Name;
            AssetDatabase.AddObjectToAsset(component, profile);
            return component;
        }

        private static Material MaterialAsset(string name, Color color, float smoothness, float metallic,
            bool emissive, bool refresh)
        {
            string path = Root + "/Materials/" + name + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool create = material == null;
            if (create)
            {
                Shader shader = Shader.Find("HDRP/Lit");
                if (shader == null) throw new InvalidOperationException("Installed HDRP/Lit shader was not found.");
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            if (!create && !refresh) return material;
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_Metallic", metallic);
            HDMaterial.SetSurfaceType(material, false);
            HDMaterial.SetUseEmissiveIntensity(material, emissive);
            HDMaterial.SetEmissiveColor(material, emissive ? Neon : Color.black);
            HDMaterial.SetEmissiveIntensity(material, emissive ? 3500f : 0f, EmissiveIntensityUnit.Nits);
            if (!HDMaterial.ValidateMaterial(material)) throw new InvalidOperationException("HDRP rejected generated material " + name);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Texture3D GetDensityMask()
        {
            Texture3D existing = AssetDatabase.LoadAssetAtPath<Texture3D>(MaskPath);
            if (existing != null) return existing;
            const int size = 32;
            var pixels = new Color32[size * size * size];
            for (int z = 0; z < size; z++)
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                Vector3 p = new Vector3(x, y, z) / size;
                float noise = Noise(p, 2) * 0.62f + Noise(p, 4) * 0.28f + Noise(p, 8) * 0.1f;
                byte density = (byte)Mathf.RoundToInt(255f * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.28f, 0.76f, noise)));
                pixels[x + size * (y + size * z)] = new Color32(density, density, density, density);
            }
            var texture = new Texture3D(size, size, size, TextureFormat.RGBA32, false)
            { name = "T_FogDensity32", wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear };
            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            AssetDatabase.CreateAsset(texture, MaskPath);
            return texture;
        }

        // Periodic trilinear value noise with smooth interpolation; three octaves, not static grain.
        private static float Noise(Vector3 p, int period)
        {
            p *= period;
            int x = Mathf.FloorToInt(p.x), y = Mathf.FloorToInt(p.y), z = Mathf.FloorToInt(p.z);
            float tx = Smooth(p.x - x), ty = Smooth(p.y - y), tz = Smooth(p.z - z);
            float a = Mathf.Lerp(Hash(x, y, z, period), Hash(x + 1, y, z, period), tx);
            float b = Mathf.Lerp(Hash(x, y + 1, z, period), Hash(x + 1, y + 1, z, period), tx);
            float c = Mathf.Lerp(Hash(x, y, z + 1, period), Hash(x + 1, y, z + 1, period), tx);
            float d = Mathf.Lerp(Hash(x, y + 1, z + 1, period), Hash(x + 1, y + 1, z + 1, period), tx);
            return Mathf.Lerp(Mathf.Lerp(a, b, ty), Mathf.Lerp(c, d, ty), tz);
        }

        private static float Smooth(float value) => value * value * (3f - 2f * value);
        private static float Hash(int x, int y, int z, int period)
        {
            unchecked
            {
                uint value = (uint)((x % period) * 73856093 ^ (y % period) * 19349663 ^ (z % period) * 83492791) + 137u;
                value ^= value >> 13;
                value *= 1274126177u;
                return (value & 0xffff) / 65535f;
            }
        }

        private static Mesh GetRing()
        {
            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(RingPath);
            if (existing != null) return existing;
            const int segments = 96;
            var vertices = new Vector3[segments * 4];
            var triangles = new int[segments * 24];
            int t = 0;
            for (int i = 0; i < segments; i++)
            {
                float angle = i * Mathf.PI * 2f / segments;
                Vector3 direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                vertices[i * 4] = direction * 2.035f + Vector3.down * 0.018f;
                vertices[i * 4 + 1] = direction * 1.995f + Vector3.down * 0.018f;
                vertices[i * 4 + 2] = direction * 2.035f + Vector3.up * 0.018f;
                vertices[i * 4 + 3] = direction * 1.995f + Vector3.up * 0.018f;
                int j = ((i + 1) % segments) * 4, k = i * 4;
                Quad(triangles, ref t, k + 2, k + 3, j + 3, j + 2);
                Quad(triangles, ref t, k, j, j + 1, k + 1);
                Quad(triangles, ref t, k, k + 2, j + 2, j);
                Quad(triangles, ref t, k + 1, j + 1, j + 3, k + 3);
            }
            var mesh = new Mesh { name = "StageGlowRing", vertices = vertices, triangles = triangles };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, RingPath);
            return mesh;
        }

        private static void Quad(int[] indices, ref int index, int a, int b, int c, int d)
        {
            indices[index++] = a; indices[index++] = b; indices[index++] = c;
            indices[index++] = a; indices[index++] = c; indices[index++] = d;
        }

        private static Transform Group(string name, Transform parent)
        {
            var go = new GameObject(name);
            if (parent != null) go.transform.SetParent(parent, false);
            return go.transform;
        }

        private static void Primitive(string name, Transform parent, PrimitiveType type, Vector3 position,
            Vector3 scale, Material material, bool collider = false)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = material;
            if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
        }

        private static void EnsureFolders()
        {
            foreach (string folder in new[] { Root, Root + "/Materials", Root + "/Settings", Root + "/Textures",
                Root + "/Prefabs", Root + "/Meshes", "Assets/Scenes" })
                if (!AssetDatabase.IsValidFolder(folder))
                {
                    string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
                    AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
                }
        }
    }
}
