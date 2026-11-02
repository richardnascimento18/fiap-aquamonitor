using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.Extensions.DependencyInjection;

namespace AquaMonitor.ContractTests.Tests;

[Trait("Category", "ContractInfrastructure")]
public class EndpointCoverageTests
{
    [Fact]
    public void RuntimeOperations_MatchInventory_AndHaveExecutablePositiveAndNegativeContracts()
    {
        using var app = new ApiScenario();
        using var client = app.Client();
        var descriptions = app.Services.GetRequiredService<IApiDescriptionGroupCollectionProvider>()
            .ApiDescriptionGroups.Items.SelectMany(group => group.Items)
            .Where(api => api.ActionDescriptor is ControllerActionDescriptor)
            .ToArray();
        var discovered = descriptions.Select(api => $"{api.HttpMethod} /{((ControllerActionDescriptor)api.ActionDescriptor).AttributeRouteInfo!.Template}").Order().ToArray();
        var inventory = Inventory.Load();
        Assert.Equal(inventory.Operations.Length, inventory.Operations.Select(op => op.Id).Distinct().Count());
        Assert.Equal(inventory.Operations.Select(op => $"{op.Method} {op.Route}").Order(), discovered);

        var methods = typeof(EndpointCoverageTests).Assembly.GetTypes().SelectMany(type => type.GetMethods())
            .Select(method => (Method: method, Coverage: method.GetCustomAttribute<OperationAttribute>()))
            .Where(item => item.Coverage is not null).ToArray();
        Assert.Equal(inventory.Operations.Length * 2, methods.Length);
        var contracts = new Contracts();
        var manifest = new List<object>();
        foreach (var operation in inventory.Operations)
        {
            var api = Assert.Single(descriptions, api => api.HttpMethod == operation.Method &&
                "/" + ((ControllerActionDescriptor)api.ActionDescriptor).AttributeRouteInfo!.Template == operation.Route);
            var action = (ControllerActionDescriptor)api.ActionDescriptor;
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
