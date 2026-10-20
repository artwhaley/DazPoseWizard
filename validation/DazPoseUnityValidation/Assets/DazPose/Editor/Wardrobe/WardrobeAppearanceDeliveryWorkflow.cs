using System;

namespace DazPose.UnityValidation
{
    /// <summary>Resumable delivery order. Each asset step recovers its durable output before creating anything.</summary>
    public static class WardrobeAppearanceDeliveryWorkflow
    {
        public static bool CanRecover(bool published, string catalogGeneration, string configurationGeneration,
            bool authoringConfigurationMatches) => published
                ? !string.IsNullOrWhiteSpace(catalogGeneration) && string.Equals(catalogGeneration, configurationGeneration, StringComparison.Ordinal)
                : authoringConfigurationMatches;

        public static void Resume(Action prepareOrRecover, Func<bool> isReleased, Action publish,
            Action generatePrefabs, Action saveScene, Action<string> progress)
        {
            Run("Recover corrected candidate or release", prepareOrRecover, progress);
            if (!isReleased()) Run("Compile and publish candidate", publish, progress);
            if (!isReleased()) throw new InvalidOperationException("Publication did not produce a released catalog; later steps were not run.");
            Run("Generate or reuse wardrobe prefabs", generatePrefabs, progress);
            Run("Create or reuse saved Gate A scene", saveScene, progress);
        }

        private static void Run(string stage, Action operation, Action<string> progress)
        {
            progress?.Invoke(stage);
            try { operation(); }
            catch (Exception error) { throw new InvalidOperationException(stage + " stopped: " + error.Message, error); }
        }
    }
}
