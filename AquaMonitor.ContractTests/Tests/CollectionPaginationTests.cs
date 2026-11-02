namespace AquaMonitor.ContractTests.Tests;

[Trait("Category", "Contract")]
public class CollectionPaginationTests : ContractTest
{
    [Theory]
    [InlineData("WaterReadings", "paged-readings")]
    [InlineData("LeakAlerts", "paged-alerts")]
    public async Task FilteredHistoryPages_DoNotDuplicateOrLoseRecords(string resource, string schema)
    {
        using var owner = await App.User();
        using var other = await App.User("other@example.test");
        var sensor = await ApiScenario.Sensor(owner);
        for (var index = 0; index < 3; index++) await ApiScenario.Alert(owner, sensor);
        await ApiScenario.Alert(other, await ApiScenario.Sensor(other));
        var first = await owner.GetFromJsonAsync<JsonElement>($"/api/{resource}?sensorId={sensor}&page=1&pageSize=2");
        var second = await owner.GetFromJsonAsync<JsonElement>($"/api/{resource}?sensorId={sensor}&page=2&pageSize=2");
        Schemas.AssertValid(schema, first);
        Schemas.AssertValid(schema, second);
        Assert.Equal(3, first.GetProperty("totalCount").GetInt32());
        Assert.Equal(2, first.GetProperty("totalPages").GetInt32());
        Assert.True(first.GetProperty("hasNextPage").GetBoolean());
        Assert.False(first.GetProperty("hasPreviousPage").GetBoolean());
        Assert.False(second.GetProperty("hasNextPage").GetBoolean());
        Assert.True(second.GetProperty("hasPreviousPage").GetBoolean());
        var page1 = first.GetProperty("data").EnumerateArray().ToArray();
        var page2 = second.GetProperty("data").EnumerateArray().ToArray();
        Assert.Equal(2, page1.Length);
        Assert.Single(page2);
        var rows = page1.Concat(page2).ToArray();
        Assert.Equal(3, rows.Select(row => row.GetProperty("id").GetInt32()).Distinct().Count());
        Assert.All(rows, row => Assert.Equal(sensor, row.GetProperty("sensorId").GetInt32()));
    }
}
