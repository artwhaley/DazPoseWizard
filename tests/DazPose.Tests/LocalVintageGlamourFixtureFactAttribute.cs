using Xunit;

namespace DazPose.Tests;

public sealed class LocalVintageGlamourFixtureFactAttribute : FactAttribute
{
    public LocalVintageGlamourFixtureFactAttribute()
    {
        if (!File.Exists(FixtureData.FigurePath) || !File.Exists(FixtureData.VintageGlamourPosePath))
            Skip = "Place the licensed G8F figure in fixtures/private and Vintage Glamour Genesis 8 Female 03.duf in the local stack3 poses folder to run this integration check.";
    }
}
