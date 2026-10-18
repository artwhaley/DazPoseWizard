using UnityEditor;
using UnityEditor.SceneManagement;

namespace DazPose.Editor.AnimationAudit
{
    public static class AnimationAuditSceneSetup
    {
        public const string ScenePath = "Assets/Scenes/AnimationAudit.unity";
        // Separate from the former controller whose states were repeatedly removed.
        public const string ControllerPath = "Assets/DazPose/AnimationAudit/LibraryPlayback.controller";
        public const string FootIKControllerPath = "Assets/DazPose/AnimationAudit/LibraryPlaybackWithFootIK.controller";


        public static void PrepareAndOpenAuditSceneMenu()
        {
            LaraHumanoidKawaiiTestSetup.CreateAuditScene();
        }

        public static void CreateScene()
        {
            LaraHumanoidKawaiiTestSetup.CreateAuditScene();
        }


        public static void OpenSceneMenu()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
            {
                PrepareAndOpenAuditSceneMenu();
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }
    }
}
