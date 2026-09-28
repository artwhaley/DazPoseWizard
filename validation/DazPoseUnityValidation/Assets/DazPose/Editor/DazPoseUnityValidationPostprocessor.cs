using System;
using UnityEditor;
using UnityEngine;

namespace DazPose.UnityValidation
{
    public sealed class DazPoseUnityValidationPostprocessor : AssetPostprocessor
    {
        private void OnPreprocessModel()
        {
            if (!string.Equals(assetPath.Replace('\\', '/'), "Assets/TestCharacter/lara.fbx", StringComparison.OrdinalIgnoreCase)) return;
            var importer = (ModelImporter)assetImporter;
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.optimizeGameObjects = false;
        }
    }
}
