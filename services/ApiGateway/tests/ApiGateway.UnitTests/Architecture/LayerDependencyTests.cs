using ApiGateway.Api.HealthCheck;
using ApiGateway.Common.Constants.Enums;
using Shouldly;
using Xunit;

namespace ApiGateway.UnitTests.Architecture;

// The compiler records an assembly reference only for assemblies whose types the code actually uses,
// so these checks catch Api code that reaches past BusinessLogic into DataAccess.
public sealed class LayerDependencyTests
{
    private static readonly string[] ApiReferences = ReferencedAssemblyNames(typeof(HealthCheckConstants));

    [Fact]
    public void ApiAssembly_References_BusinessLogic()
    {
        // Assert
        ApiReferences.ShouldContain(AssemblyName(typeof(ApiGateway.BusinessLogic.DependencyInjection)));
    }

    [Fact]
    public void ApiAssembly_DoesNotReference_DataAccess()
    {
        // Assert
        ApiReferences.ShouldNotContain(AssemblyName(typeof(ApiGateway.DataAccess.DependencyInjection)));
    }

    // Common holds the types shared by all layers, so it must not depend on any of them.
    [Fact]
    public void CommonAssembly_DoesNotReference_OtherLayers()
    {
        // Arrange
        var commonReferences = ReferencedAssemblyNames(typeof(MeterType));

        // Assert
        commonReferences.ShouldNotContain(reference => reference.StartsWith("ApiGateway.", StringComparison.Ordinal));
    }

    private static string[] ReferencedAssemblyNames(Type type)
    {
        return [.. type.Assembly.GetReferencedAssemblies().Select(reference => reference.Name!)];
    }

    private static string AssemblyName(Type type)
    {
        return type.Assembly.GetName().Name!;
    }
}
