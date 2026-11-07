using ApiGateway.DataAccess.Database;
using ApiGateway.DataAccess.Database.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Npgsql;

namespace ApiGateway.DataAccess;

public static class DependencyInjection
{
    public static IServiceCollection AddDataAccess(this IServiceCollection services, IConfiguration configuration)
    {
        // Configuration
        services.AddOptions<DatabaseSettings>()
            .Bind(configuration.GetSection(DatabaseSettings.SectionName))
            .ValidateOnStart()
            .ValidateDataAnnotations();

        // Database
        services.AddDbContext<IReadOnlyDbContext, ReadOnlyApplicationDbContext>((serviceProvider, options) =>
        {
            var databaseSettings = serviceProvider.GetRequiredService<IOptions<DatabaseSettings>>().Value;

            var connectionString = new NpgsqlConnectionStringBuilder(databaseSettings.ConnectionString)
            {
                Timeout = databaseSettings.ConnectTimeoutSeconds
            };

            options
                .UseNpgsql(
                    connectionString.ConnectionString,
                    npgsql => npgsql.CommandTimeout(databaseSettings.CommandTimeoutSeconds))
                .UseSnakeCaseNamingConvention()
                .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
        });

        return services;
    }
}
