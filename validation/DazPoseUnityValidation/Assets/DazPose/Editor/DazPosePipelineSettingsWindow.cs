using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace DazPose.UnityValidation
{
    [Serializable]
    internal sealed class DazPosePipelineProjectSettings
    {
        public string g8fReferenceModelGuid = string.Empty;
    }

    internal static class DazPosePipelineSettings
    {
        private const string SettingsFileName = "DazPoseWizardSettings.json";

        public static string SettingsPath => Path.Combine(ProjectRoot, "ProjectSettings", SettingsFileName);
        private static string ProjectRoot => Directory.GetParent(Application.dataPath).FullName;

        public static DazPosePipelineProjectSettings Load()
        {
            try
            {
                if (!File.Exists(SettingsPath)) return new DazPosePipelineProjectSettings();
                return JsonUtility.FromJson<DazPosePipelineProjectSettings>(File.ReadAllText(SettingsPath))
                    ?? new DazPosePipelineProjectSettings();
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                Debug.LogError("Could not read DAZ Pose Pipeline Settings: " + exception.Message);
                return new DazPosePipelineProjectSettings();
            }
        }

        public static void Save(DazPosePipelineProjectSettings settings)
        {
            var directory = Path.GetDirectoryName(SettingsPath);
            Directory.CreateDirectory(directory);
            var temporaryPath = SettingsPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(temporaryPath, JsonUtility.ToJson(settings, true), new UTF8Encoding(false));
                if (File.Exists(SettingsPath)) File.Replace(temporaryPath, SettingsPath, null);
                else File.Move(temporaryPath, SettingsPath);
            }
            finally
            {
                if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
            }
        }

        public static GameObject LoadReferenceModel(out string assetPath)
        {
            var settings = Load();
            assetPath = string.IsNullOrEmpty(settings.g8fReferenceModelGuid)
                ? string.Empty
                : AssetDatabase.GUIDToAssetPath(settings.g8fReferenceModelGuid);
            return string.IsNullOrEmpty(assetPath) ? null : AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
        }
    }

    public sealed class DazPosePipelineSettingsWindow : EditorWindow
    {
        private GameObject _referenceModel;

        [MenuItem("Tools/DAZ Pose/Pipeline Settings")]
        public static void Open()
        {
            var window = GetWindow<DazPosePipelineSettingsWindow>();
            window.titleContent = new GUIContent("DAZ Pose Pipeline");
            window.minSize = new Vector2(420, 180);
            window.Show();
        }

        private void OnEnable()
        {
            _referenceModel = DazPosePipelineSettings.LoadReferenceModel(out _);
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Browser Import Pipeline", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Choose the neutral Genesis 8 Female FBX or model used to resolve browser imports. The processor instantiates it in a temporary preview scene and never edits the open scene.", MessageType.Info);

            EditorGUI.BeginChangeCheck();
            _referenceModel = (GameObject)EditorGUILayout.ObjectField("G8F Reference Model", _referenceModel, typeof(GameObject), false);
            if (EditorGUI.EndChangeCheck()) SaveReferenceModel();

            var bridgePath = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "DazPoseWizard.project.json");
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Browser Path Configuration", EditorStyles.boldLabel);
            if (File.Exists(bridgePath))
                EditorGUILayout.LabelField("Loaded from project root", bridgePath, EditorStyles.wordWrappedMiniLabel);
            else
                EditorGUILayout.HelpBox("The Windows app creates DazPoseWizard.project.json after you configure a Unity project and enqueue a pose.", MessageType.Warning);

            var assetPath = _referenceModel == null ? string.Empty : AssetDatabase.GetAssetPath(_referenceModel);
            if (!string.IsNullOrEmpty(assetPath))
            {
                EditorGUILayout.LabelField("Model asset", assetPath);
                var importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;
                if (importer != null && (importer.animationType != ModelImporterAnimationType.Generic || importer.optimizeGameObjects || !importer.importBlendShapes))
                    EditorGUILayout.HelpBox("The reference FBX must use Generic rig import, Optimize Game Objects disabled, and Import BlendShapes enabled.", MessageType.Error);
            }

            if (_referenceModel == null)
                EditorGUILayout.HelpBox("No reference model is configured. Browser imports will be reported as failed until one is selected.", MessageType.Error);
        }

        private void SaveReferenceModel()
        {
            var assetPath = _referenceModel == null ? string.Empty : AssetDatabase.GetAssetPath(_referenceModel);
            var guid = string.IsNullOrEmpty(assetPath) ? string.Empty : AssetDatabase.AssetPathToGUID(assetPath);
            var settings = DazPosePipelineSettings.Load();
            settings.g8fReferenceModelGuid = guid;
            DazPosePipelineSettings.Save(settings);
        }
    }
}
