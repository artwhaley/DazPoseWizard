using DazPose.Game;
namespace DazPose.Story.Tests;
public sealed class NavigationTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("missing")]
    public async Task Unavailable_scene_does_not_stop_or_load(string path)
    {
        int stops = 0, loads = 0;
        var nav = new SceneNavigationCoordinator(() => stops++, _ => { loads++; return Task.CompletedTask; }, _ => false);
        Assert.False(await nav.NavigateAsync(path));
        Assert.Equal(0, stops); Assert.Equal(0, loads);
        Assert.False(nav.IsNavigating); Assert.False(string.IsNullOrWhiteSpace(nav.LastError));
    }
    [Fact]
    public async Task Pending_start_rejects_double_click_and_stops_once()
    {
        int stops = 0, loads = 0;
        var gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var nav = new SceneNavigationCoordinator(() => stops++, _ => { loads++; return gate.Task; }, _ => true);
        var first = nav.NavigateAsync("FirstContact");
        Assert.True(nav.IsNavigating);
        Assert.False(await nav.NavigateAsync("FirstContact"));
        Assert.Equal(1, stops); Assert.Equal(1, loads);
        gate.SetResult(true);
        Assert.True(await first); Assert.False(nav.IsNavigating); Assert.Null(nav.LastError);
    }
    [Fact]
    public async Task Failure_preserves_usable_navigation_and_reports_reason()
    {
        bool broken = true; int stops = 0;
        var nav = new SceneNavigationCoordinator(() => stops++, _ => broken ?
            Task.FromException(new IOException("saved scene missing")) : Task.CompletedTask, _ => true);
        Assert.False(await nav.NavigateAsync("FirstContact"));
        Assert.Contains("saved scene missing", nav.LastError);
        Assert.False(nav.IsNavigating);
        broken = false;
        Assert.True(await nav.NavigateAsync("MainMenu"));
        Assert.Equal(2, stops); Assert.Null(nav.LastError);
    }
    [Fact]
    public async Task Stop_failure_prevents_load_and_releases_transition()
    {
        int loads = 0;
        var nav = new SceneNavigationCoordinator(() => throw new InvalidOperationException("stop failed"),
            _ => { loads++; return Task.CompletedTask; }, _ => true);
        Assert.False(await nav.NavigateAsync("MainMenu"));
        Assert.Equal(0, loads); Assert.False(nav.IsNavigating); Assert.Contains("stop failed", nav.LastError);
    }
    [Fact]
    public async Task Cancellation_is_observed_and_allows_return()
    {
        var nav = new SceneNavigationCoordinator(() => { }, p => p == "FirstContact" ?
            Task.FromCanceled(new CancellationToken(true)) : Task.CompletedTask, _ => true);
        Assert.False(await nav.NavigateAsync("FirstContact"));
        Assert.Contains("cancelled", nav.LastError);
        Assert.True(await nav.NavigateAsync("MainMenu")); Assert.Null(nav.LastError);
    }
    [Fact]
    public async Task Stop_precedes_loader_and_invalid_catalog_choice_is_explained()
    {
        var order = new List<string>();
        var nav = new SceneNavigationCoordinator(() => order.Add("stop"),
            p => { order.Add("load:" + p); return Task.CompletedTask; }, _ => true);
        Assert.False(nav.Reject("unpublished scene")); Assert.Equal("unpublished scene", nav.LastError);
        Assert.True(await nav.NavigateAsync("FirstContact"));
        Assert.Equal(new[] { "stop", "load:FirstContact" }, order);
    }
}
