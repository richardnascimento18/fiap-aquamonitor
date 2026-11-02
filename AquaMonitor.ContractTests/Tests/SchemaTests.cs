using System.Text.Json.Nodes;
using Json.Schema;

namespace AquaMonitor.ContractTests.Tests;

[Trait("Category", "ContractInfrastructure")]
public class SchemaTests
{
    public static IEnumerable<object[]> Names() => Directory.GetFiles(Contracts.SchemaDirectory, "*.schema.json")
        .Select(file => new object[] { Path.GetFileName(file).Replace(".schema.json", "", StringComparison.Ordinal) });

    [Theory]
    [MemberData(nameof(Names))]
    public void Schema_ValidatesDialectAndSample_RejectsMissingAndWrongTypes(string name)
    {
        var contracts = new Contracts();
        var document = contracts.Documents[name];
        Assert.Equal("https://json-schema.org/draft/2020-12/schema", document.GetProperty("$schema").GetString());
        Assert.True(MetaSchemas.Draft202012.Evaluate(document).IsValid, $"Invalid Draft 2020-12 schema: {name}");
        var sample = JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Samples", name + ".json")))!;
        contracts.AssertValid(name, JsonSerializer.SerializeToElement(sample));

        var missing = sample.DeepClone();
        var target = ObjectIn(missing);
        var property = target.First().Key;
        target.Remove(property);
        Assert.False(contracts.Evaluate(name, JsonSerializer.SerializeToElement(missing)).IsValid, $"{name} accepted a missing required property");

        var wrongType = sample.DeepClone();
        ObjectIn(wrongType)[property] = new JsonArray(1, 2);
        Assert.False(contracts.Evaluate(name, JsonSerializer.SerializeToElement(wrongType)).IsValid, $"{name} accepted a wrong property type");
        Assert.False(contracts.Evaluate(name, JsonSerializer.SerializeToElement(false)).IsValid);
    }

    private static JsonObject ObjectIn(JsonNode node) => node is JsonArray array ? array[0]!.AsObject() : node.AsObject();
}
