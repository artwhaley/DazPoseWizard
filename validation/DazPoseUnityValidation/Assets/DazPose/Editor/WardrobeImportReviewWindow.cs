using System.IO;
using DazPose.Performer;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DazPose.UnityValidation
{
    public sealed class WardrobeImportReviewWindow : EditorWindow
    {
        [System.Serializable] private sealed class Published {public string publishedReviewScene;}
        [MenuItem("Tools/DAZ Pose/Wardrobe/Imported Outfits")]
        public static void Open()=>GetWindow<WardrobeImportReviewWindow>("Imported Outfits").Show();
        private Vector2 scroll;
        private void OnGUI()
        {
            EditorGUILayout.HelpBox("Outfit assets are equipment data. Review scenes are disposable test hosts; save artistic scene arrangements under a separate name.",MessageType.Info);
            if (GUILayout.Button("Open Clothing Setup"))
            {
                if (ClothingSetupBuilder.TryOpenOrCreate(out _, out string error)) ClothingSetupWindow.Open();
                else EditorUtility.DisplayDialog("Clothing Setup", error, "OK");
            }
            scroll=EditorGUILayout.BeginScrollView(scroll);
            foreach(string guid in AssetDatabase.FindAssets("t:WardrobeOutfitDefinition"))
            {
                string path=AssetDatabase.GUIDToAssetPath(guid);var outfit=AssetDatabase.LoadAssetAtPath<WardrobeOutfitDefinition>(path);
                EditorGUILayout.BeginHorizontal();EditorGUILayout.ObjectField(outfit,typeof(WardrobeOutfitDefinition),false);
                if(GUILayout.Button("Review "+outfit.id))
                {
                    string scene=Path.GetDirectoryName(path).Replace('\\','/')+"/Review.unity";
                    string report="TestOutput/wardrobe-import/"+outfit.id+"/report.json";
                    if(File.Exists(report))
                    {
                        var published=JsonUtility.FromJson<Published>(File.ReadAllText(report));
                        if(!string.IsNullOrEmpty(published.publishedReviewScene))scene=published.publishedReviewScene;
                    }
                    if(!File.Exists(scene)){Debug.LogError("Publish the generated review scene first: "+scene);continue;}
                    if(EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                    {
                        EditorSceneManager.OpenScene(scene,OpenSceneMode.Single);LaraFirstOutfitBuilder.ConfigurePresentation();
                        LaraSecondOutfitReviewWindow.ShowFor(Object.FindAnyObjectByType<LaraWardrobe>());
                    }
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndScrollView();
        }
    }
}
