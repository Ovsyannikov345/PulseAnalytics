using NotificationService.Api.HealthCheck;
using NotificationService.Application.Telemetry;
using Shouldly;
using Xunit;
using InfrastructureDependencyInjection = NotificationService.Infrastructure.DependencyInjection;

namespace NotificationService.UnitTests.Architecture;

// The compiler records an assembly reference only for assemblies whose types the code actually uses,
// so these checks catch code that reaches past its layer.
public sealed class LayerDependencyTests
{
    private const string ServicePrefix = "NotificationService.";

    private const string RabbitMqClientAssemblyName = "RabbitMQ.Client";

    private const string SignalRAssemblyPrefix = "Microsoft.AspNetCore.SignalR";

    private const string BrokerContractsAssemblyName = "DataProcessor.Contracts";

    // The broker is an Infrastructure detail: Api reaches it only through Application's interfaces.
    [Fact]
    public void ApiAssembly_DoesNotReference_RabbitMqClient()
    {
        // Arrange
        var apiReferences = ReferencedAssemblyNames(typeof(HealthCheckConstants));

        // Assert
        apiReferences.ShouldNotContain(RabbitMqClientAssemblyName);
    }

    // Clients get the hub's own messages; the broker contract stays in Infrastructure.
    [Fact]
    public void ApiAssembly_DoesNotReference_BrokerContracts()
    {
        // Arrange
        var apiReferences = ReferencedAssemblyNames(typeof(HealthCheckConstants));

        // Assert
        apiReferences.ShouldNotContain(BrokerContractsAssemblyName);
    }

    // Application holds the models, interfaces and telemetry shared by the outer layers, so it must not depend on them.
    [Fact]
    public void ApplicationAssembly_DoesNotReference_OtherLayers()
    {
        // Arrange
        var applicationReferences = ReferencedAssemblyNames(typeof(NotificationServiceTelemetry));

        // Assert
        applicationReferences.ShouldNotContain(reference => reference.StartsWith(ServicePrefix, StringComparison.Ordinal));
    }

    [Fact]
    public void InfrastructureAssembly_DoesNotReference_ApiOrSignalR()
    {
        // Arrange
        var infrastructureReferences = ReferencedAssemblyNames(typeof(InfrastructureDependencyInjection));

        // Assert
        infrastructureReferences.ShouldNotContain(AssemblyName(typeof(HealthCheckConstants)));
        infrastructureReferences.ShouldNotContain(reference => reference.StartsWith(SignalRAssemblyPrefix, StringComparison.Ordinal));
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
