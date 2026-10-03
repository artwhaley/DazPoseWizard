using System;
using System.IO;
using System.Text;
using DazPose.Motion;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace DazPose.Motion.Editor
{
    /// <summary>Imports project-authored .funscript JSON directly as a FunscriptMotionProgram asset.</summary>
    [ScriptedImporter(1, "funscript")]
    public sealed class FunscriptImporter : ScriptedImporter
    {
        public override void OnImportAsset(AssetImportContext context)
        {
            FunscriptMotionProgram program = null;
            try
            {
                string json = File.ReadAllText(context.assetPath, Encoding.UTF8);
                program = FunscriptJsonParser.Parse(json);
                program.name = Path.GetFileNameWithoutExtension(context.assetPath);
                context.AddObjectToAsset("program", program);
                context.SetMainObject(program);
            }
            catch (Exception exception)
            {
                if (program != null) DestroyImmediate(program);
                context.LogImportError("Could not import Funscript '" + context.assetPath + "': " + exception.Message);
            }
        }
    }
}
