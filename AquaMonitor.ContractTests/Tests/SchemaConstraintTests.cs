using System.Text.Json.Nodes;

namespace AquaMonitor.ContractTests.Tests;

[Trait("Category", "ContractInfrastructure")]
public class SchemaConstraintTests
{
    [Theory]
    [InlineData("auth-response", "expiresAt", "not-a-date")]
    [InlineData("alert-response", "severity", "Unknown")]
    [InlineData("sensor-response", "alertThresholdLitersPerHour", -1)]
    [InlineData("paged-readings", "pageSize", 101)]
    public void FormatEnumAndRangeViolations_AreRejected(string schema, string property, object invalid)
    {
        var sample = JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Samples", schema + ".json")))!;
        sample[property] = JsonSerializer.SerializeToNode(invalid);
        Assert.False(new Contracts().Evaluate(schema, JsonSerializer.SerializeToElement(sample)).IsValid);
    }

    [Fact]
    public void ReferencedCollectionItems_AreValidated()
    {
        var sample = JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Samples", "paged-sensors.json")))!;
        sample["data"]![0]!["id"] = "incorrect-type";
        Assert.False(new Contracts().Evaluate("paged-sensors", JsonSerializer.SerializeToElement(sample)).IsValid);
    }

    [Fact]
    public void AlertRelationships_AreValidated()
    {
        var contracts = new Contracts();
        Assert.False(contracts.Evaluate("reading-created", JsonSerializer.SerializeToElement(new { readingId = 1, alertGenerated = true, alertId = (int?)null })).IsValid);
        var alert = JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Samples", "alert-response.json")))!;
        alert["status"] = "Resolved";
        Assert.False(contracts.Evaluate("alert-response", JsonSerializer.SerializeToElement(alert)).IsValid);
    }
}
