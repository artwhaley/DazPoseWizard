using DazPose.Performer;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DazPose.UnityValidation
{
    public sealed class WardrobeImportReviewWindow : EditorWindow
    {
        [MenuItem("DazPose/Wardrobe/Imported Outfits")]
        public static void Open()=>GetWindow<WardrobeImportReviewWindow>("Imported Outfits").Show();

        private Vector2 scroll;

        private void OnGUI()
        {
            EditorGUILayout.HelpBox("Outfit assets are equipment data. Review scenes are published review hosts; save artistic scene arrangements under a separate name.", MessageType.Info);
            if (GUILayout.Button("Open Clothing Setup"))
            {
                if (ClothingSetupBuilder.TryOpenOrCreate(out _, out string error)) ClothingSetupWindow.Open();
                else EditorUtility.DisplayDialog("Clothing Setup", error, "OK");
            }

            scroll = EditorGUILayout.BeginScrollView(scroll);
            foreach (string guid in AssetDatabase.FindAssets("t:WardrobeOutfitDefinition"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var outfit = AssetDatabase.LoadAssetAtPath<WardrobeOutfitDefinition>(path);
                if (outfit == null) continue;

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.ObjectField(outfit, typeof(WardrobeOutfitDefinition), false);
                if (GUILayout.Button("Review " + outfit.id))
                {
                    if (!WardrobeReviewMetadata.TryResolve(outfit, out string scene, out string error))
                    {
                        EditorUtility.DisplayDialog("Published Outfit Review", error, "OK");
                    }
                    else if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                    {
                        EditorSceneManager.OpenScene(scene, OpenSceneMode.Single);
                        LaraSecondOutfitReviewWindow.ShowFor(Object.FindAnyObjectByType<LaraWardrobe>());
                    }
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndScrollView();
        }
    }
}