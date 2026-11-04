using ApiGateway.Api.HealthCheck;
using Shouldly;
using Xunit;

namespace ApiGateway.UnitTests.Architecture;

// The compiler records an assembly reference only for assemblies whose types the code actually uses,
// so these checks catch Api code that reaches past BusinessLogic into DataAccess.
public sealed class LayerDependencyTests
{
    private static readonly string[] ApiReferences =
    [
        .. typeof(HealthCheckConstants).Assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name!)
    ];

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

    private static string AssemblyName(Type type)
    {
        return type.Assembly.GetName().Name!;
    }
}
