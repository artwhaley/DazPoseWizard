using DazPose.Game;
namespace DazPose.Player.Tests;
public class CatalogViewportAnchorTests {
 [Theory][InlineData(100,73)][InlineData(300,280)]
 public void DeepDataEditPreservesIdentity(int count,int top){var ids=Enumerable.Range(0,count).Select(i=>"id"+i).ToArray();Assert.Equal(top,CatalogViewportAnchor.Resolve(ids,ids,top));}
 [Fact] public void RemovedAnchorUsesNextSurvivingNeighbour(){var old=Enumerable.Range(0,100).Select(i=>"id"+i).ToArray();var filtered=old.Where(i=>i!="id73").ToArray();Assert.Equal("id74",filtered[CatalogViewportAnchor.Resolve(old,filtered,73)]);}
 [Fact] public void EmptyAndUnrelatedResultsClamp(){Assert.Equal(0,CatalogViewportAnchor.Resolve(new[]{"old"},Array.Empty<string>(),73));Assert.Equal(1,CatalogViewportAnchor.Resolve(new[]{"a","b","c"},new[]{"x","y"},73));}
}
