namespace AquaMonitor.BddTests.Support;

public sealed class ScenarioWorld : IAsyncDisposable
{
    public ApiScenario App { get; } = new();
    public HttpClient Actor { get; set; } = null!;
    public HttpClient? Owner { get; set; }
    public int SensorId { get; set; }
    public int AlertId { get; set; }
    public string Email { get; } = "pessoa@example.test";
    public HttpResponseMessage Response { get; set; } = null!;

    public async ValueTask DisposeAsync()
    {
        Response?.Dispose();
        await App.DisposeAsync();
    }
}

[Binding]
public sealed class ScenarioHooks(ScenarioWorld world)
{
    [AfterScenario]
    public async Task Cleanup() => await world.DisposeAsync();
}
