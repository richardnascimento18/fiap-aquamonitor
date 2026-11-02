using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace AquaMonitor.ContractTests.Tests;

[Trait("Category", "ContractInfrastructure")]
public class EndpointCoverageTests
{
    [Fact]
    public async Task RuntimeOperations_MatchInventory_AndHaveExecutablePositiveAndNegativeContracts()
    {
        using var app = new ApiScenario();
        using var client = app.Client();
        using var index = await client.GetAsync("/"); // Materialize routing before inspecting all endpoints.
        Assert.Equal(HttpStatusCode.OK, index.StatusCode);
        var routes = app.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>()
            .SelectMany(endpoint => (endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? [])
                .Select(method => new
                {
                    Method = method,
                    Route = "/" + endpoint.RoutePattern.RawText!.TrimStart('/'),
                    Action = endpoint.Metadata.GetMetadata<ControllerActionDescriptor>()
                })).ToArray();
        var descriptions = routes.Where(route => route.Action is not null).ToArray();
        var discovered = descriptions.Select(api => $"{api.Method} {api.Route}").Order().ToArray();
        var inventory = Inventory.Load();
        Assert.Equal(inventory.Operations.Length, inventory.Operations.Select(op => op.Id).Distinct().Count());
        Assert.Equal(inventory.Operations.Select(op => $"{op.Method} {op.Route}").Order(), discovered);
        Assert.All(inventory.Exclusions, exclusion => Assert.False(string.IsNullOrWhiteSpace(exclusion.Reason)));
        Assert.Equal(inventory.Exclusions.Select(op => $"{op.Method} {op.Route}").Order(),
            routes.Where(route => route.Action is null).Select(route => $"{route.Method} {route.Route}").Order());

        var methods = typeof(EndpointCoverageTests).Assembly.GetTypes().SelectMany(type => type.GetMethods())
            .Select(method => (Method: method, Coverage: method.GetCustomAttribute<OperationAttribute>()))
            .Where(item => item.Coverage is not null).ToArray();
        Assert.Equal(inventory.Operations.Length * 2, methods.Length);
        var contracts = new Contracts();
        var manifest = new List<object>();
        foreach (var operation in inventory.Operations)
        {
            var api = Assert.Single(descriptions, api => api.Method == operation.Method && api.Route == operation.Route);
            var action = api.Action!;
            var authorization = action.ControllerTypeInfo.GetCustomAttributes<AuthorizeAttribute>()
                .Concat(action.MethodInfo.GetCustomAttributes<AuthorizeAttribute>()).ToArray();
            if (operation.Authorization == "Anonymous") Assert.Empty(authorization);
            else Assert.NotEmpty(authorization);
            if (operation.Authorization.StartsWith("Admin", StringComparison.Ordinal))
                Assert.Contains(authorization, attribute => attribute.Roles == "Admin");
            else Assert.DoesNotContain(authorization, attribute => attribute.Roles is not null);
            foreach (var outcome in new[] { "positive", "negative" })
            {
                var expectation = outcome == "positive" ? operation.Positive : operation.Negative;
                var test = Assert.Single(methods, item => item.Coverage!.Id == operation.Id && item.Coverage.Outcome == outcome);
                Assert.Equal(expectation.Test, test.Method.Name);
                Assert.True(test.Method.IsPublic && test.Method.ReturnType == typeof(Task));
                Assert.Null(test.Coverage!.Skip);
                Assert.Equal(expectation.Schema is null, expectation.EmptyBody);
                if (expectation.Schema is not null) Assert.Contains(expectation.Schema, contracts.Documents.Keys);
                if (outcome == "positive") Assert.InRange(expectation.Status, 200, 299);
                else Assert.InRange(expectation.Status, 400, 499);
                manifest.Add(new { operation.Id, operation.Method, operation.Route, outcome, test = $"{test.Method.DeclaringType!.FullName}.{test.Method.Name}", expectation.Status, expectation.Schema, expectation.EmptyBody });
            }
        }
        var directory = Environment.GetEnvironmentVariable("AQUA_TEST_RESULTS") ?? Path.Combine(AppContext.BaseDirectory, "TestResults");
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "endpoint-manifest.json"), JsonSerializer.Serialize(manifest, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true }));
    }
}
