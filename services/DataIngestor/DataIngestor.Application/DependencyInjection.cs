using DataIngestor.Application.ExternalMeters;
using Microsoft.Extensions.DependencyInjection;

namespace DataIngestor.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<IExternalMetersService, ExternalMetersService>();

        return services;
    }
}
