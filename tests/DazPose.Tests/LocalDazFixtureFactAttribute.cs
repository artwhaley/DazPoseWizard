using Xunit;

namespace DazPose.Tests;

public sealed class LocalDazFixtureFactAttribute : FactAttribute
{
    public LocalDazFixtureFactAttribute()
    {
        if (!File.Exists(FixtureData.FigurePath) || !File.Exists(FixtureData.PosePath))
            Skip = "Place the licensed G8F figure and Cherish pose in fixtures/private to run these local integration checks.";
    }
}
