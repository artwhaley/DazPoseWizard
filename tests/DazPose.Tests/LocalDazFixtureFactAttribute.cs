using Xunit;

namespace DazPose.Tests;

public sealed class LocalDazFixtureFactAttribute : FactAttribute
{
    public LocalDazFixtureFactAttribute(params string[] additionalFixtureFileNames)
    {
        if (!File.Exists(FixtureData.FigurePath) || !File.Exists(FixtureData.PosePath))
        {
            Skip = "Place the licensed G8F figure and Cherish pose in fixtures/private to run these local integration checks.";
            return;
        }

        foreach (var fileName in additionalFixtureFileNames ?? Array.Empty<string>())
        {
            var path = Path.Combine(AppContext.BaseDirectory, "fixtures", "private", fileName);
            if (File.Exists(path)) continue;
            Skip = $"Place the licensed '{fileName}' fixture in fixtures/private to run this local integration check.";
            return;
        }
    }
}
