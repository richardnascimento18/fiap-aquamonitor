using System.Reflection;
using AquaMonitor.Application.Contracts;
using AquaMonitor.Domain.Entities;

namespace AquaMonitor.Tests;

[Trait("Category", "Architecture")]
public class ArchitectureTests
{
    [Theory]
    [InlineData("AquaMonitor.Domain")]
    [InlineData("AquaMonitor.Application")]
    public void InnerLayers_ReferenceOnlyAllowedAssemblies(string name)
    {
        var assembly = Assembly.Load(name);
        Assert.All(assembly.GetReferencedAssemblies(), reference =>
        {
            Assert.DoesNotContain(reference.Name, new[] { "AquaMonitor.API", "AquaMonitor.Infrastructure" });
            Assert.DoesNotContain("Sqlite", reference.Name!, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("AspNetCore", reference.Name!, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public void Domain_HasNoProjectOrPackageDependencies()
    {
        var project = System.Xml.Linq.XDocument.Load(Path.Combine(SourceRoot, "AquaMonitor.Domain/AquaMonitor.Domain.csproj"));
        Assert.Empty(project.Descendants("ProjectReference"));
        Assert.Empty(project.Descendants("PackageReference"));
        Assert.Empty(project.Descendants("FrameworkReference"));
        Assert.NotEqual(typeof(WaterSensor).Assembly, typeof(SensorResponseVM).Assembly);
    }

    [Theory]
    [InlineData("AquaMonitor.API/Controllers")]
    [InlineData("AquaMonitor.Application")]
    public void RequestHandling_DoesNotExecuteSqlOrReferencePersistence(string directory)
    {
        foreach (var file in Directory.EnumerateFiles(Path.Combine(SourceRoot, directory), "*.cs", SearchOption.AllDirectories)
            .Where(file => !file.Split(Path.DirectorySeparatorChar).Any(part => part is "obj" or "bin")))
        {
            var source = File.ReadAllText(file);
            Assert.DoesNotMatch(@"\b(SELECT|INSERT|UPDATE|DELETE|PRAGMA|CREATE TABLE)\b", source);
            Assert.DoesNotContain("Sqlite", source, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("AquaMonitor.Infrastructure", source);
            Assert.DoesNotContain("IDbConnection", source);
        }
    }

    internal static string SourceRoot
    {
        get
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AquaMonitor.sln"))) directory = directory.Parent;
            return directory?.FullName ?? throw new InvalidOperationException("Solution source root not found.");
        }
    }
}
