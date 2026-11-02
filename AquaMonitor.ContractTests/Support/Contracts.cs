using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Json.Schema;

namespace AquaMonitor.ContractTests.Support;

public sealed class Contracts
{
    public static string SchemaDirectory => Path.Combine(AppContext.BaseDirectory, "Schemas");
    public IReadOnlyDictionary<string, JsonElement> Documents { get; }
    private readonly Dictionary<string, JsonSchema> _schemas;
    public Contracts()
    {
        Documents = Directory.GetFiles(SchemaDirectory, "*.schema.json").ToDictionary(
            file => Path.GetFileName(file).Replace(".schema.json", "", StringComparison.Ordinal),
            file => JsonDocument.Parse(File.ReadAllText(file)).RootElement.Clone());
        var options = new BuildOptions { SchemaRegistry = new SchemaRegistry() };
        // Resource contracts register before the collection contracts that reference them.
        _schemas = Documents.OrderBy(pair => pair.Key.StartsWith("paged-", StringComparison.Ordinal))
            .ToDictionary(pair => pair.Key, pair => JsonSchema.Build(pair.Value, options));
    }

    public EvaluationResults Evaluate(string name, JsonElement body)
        => _schemas[name].Evaluate(body, new EvaluationOptions { RequireFormatValidation = true, OutputFormat = OutputFormat.List });

    public void AssertValid(string name, JsonElement body)
    {
        var result = Evaluate(name, body);
        // Never include the response body: authentication responses contain usable JWTs.
        Assert.True(result.IsValid, $"Contract {name} failed: {JsonSerializer.Serialize(result)}");
    }
}

/// <summary>A real xUnit Fact, linked to a required inventory outcome.</summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class OperationAttribute(string id, string outcome) : FactAttribute
{
    public string Id { get; } = id;
    public string Outcome { get; } = outcome;
}

public sealed record Expectation(string Test, int Status, string? Schema, bool EmptyBody);
public sealed record Operation(string Id, string Method, string Route, string Authorization, Expectation Positive, Expectation Negative);
public sealed record ExcludedOperation(string Method, string Route, string Reason);
public sealed record Inventory(Operation[] Operations, ExcludedOperation[] Exclusions)
{
    public static Inventory Load() => JsonSerializer.Deserialize<Inventory>(
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "endpoint-coverage.json")), new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
}

public abstract class ContractTest : IAsyncLifetime
{
    protected ApiScenario App { get; } = new();
    protected Contracts Schemas { get; } = new();
    protected static Task<JsonElement> Body(HttpResponseMessage response) => ApiScenario.Json(response);
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await App.DisposeAsync();

    /// <summary>Call last, after observable-behavior assertions. Evidence contains no payloads.</summary>
    protected async Task Complete(HttpResponseMessage response, [CallerMemberName] string test = "")
    {
        var method = GetType().GetMethod(test)!;
        var coverage = method.GetCustomAttribute<OperationAttribute>()!;
        var operation = Inventory.Load().Operations.Single(op => op.Id == coverage.Id);
        var expectation = coverage.Outcome == "positive" ? operation.Positive : operation.Negative;
        Assert.Equal(expectation.Test, test);
        var request = Assert.IsType<HttpRequestMessage>(response.RequestMessage);
        Assert.Equal(operation.Method, request.Method.Method);
        var pathPattern = "^" + Regex.Escape(operation.Route).Replace(Regex.Escape("{id:int}"), @"-?\d+", StringComparison.Ordinal) + "$";
        Assert.Matches(pathPattern, request.RequestUri!.AbsolutePath);
        Assert.Equal(expectation.Status, (int)response.StatusCode);
        if (expectation.EmptyBody)
            Assert.Equal("", await response.Content.ReadAsStringAsync());
        else
        {
            Assert.Contains("json", response.Content.Headers.ContentType?.MediaType ?? "");
            Schemas.AssertValid(expectation.Schema!, await Body(response));
        }
        var directory = Environment.GetEnvironmentVariable("AQUA_TEST_RESULTS") ?? Path.Combine(AppContext.BaseDirectory, "TestResults");
        directory = Path.Combine(directory, "contracts");
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(Path.Combine(directory, $"{coverage.Id}-{coverage.Outcome}.json"), JsonSerializer.Serialize(new
        {
            operationId = coverage.Id,
            outcome = coverage.Outcome,
            test = $"{GetType().FullName}.{test}",
            requestMethod = request.Method.Method,
            requestPath = request.RequestUri.AbsolutePath,
            status = (int)response.StatusCode,
            jsonBodyVerified = !expectation.EmptyBody,
            schema = expectation.Schema,
            schemaValidated = expectation.Schema is not null,
            emptyBodyVerified = expectation.EmptyBody
        }));
    }
}
