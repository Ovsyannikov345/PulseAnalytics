using ApiGateway.Api.GraphQL.Errors;
using ApiGateway.Api.GraphQL.Queries;
using ApiGateway.Api.GraphQL.Types;
using HotChocolate;
using HotChocolate.Types;

namespace ApiGateway.Api;

public static class DependencyInjection
{
    // Introspection (Nitro, codegen) is deeper than any real query, so it isn't counted.
    private const int MaxExecutionDepth = 8;

    // Longer than the database command timeout, so a slow query is reported as a database failure.
    private static readonly TimeSpan ExecutionTimeout = TimeSpan.FromSeconds(15);

    public static IServiceCollection AddGraphQLApi(this IServiceCollection services, IHostEnvironment environment)
    {
        services
            .AddGraphQLServer()
            .AddQueryType()
            .AddTypeExtension<MeterReadingQueries>()
            .AddType<MeterReadingInterfaceType>()
            .AddType<MotionReadingType>()
            .AddType<EnergyReadingType>()
            .AddType<AirQualityReadingType>()
            .AddType<ReadingCountType>()
            .AddType<ReadingFilterInputType>()
            .AddType<LatestReadingsFilterInputType>()
            // The schema has its own service container; the filter needs application services (environment, logging).
            .AddErrorFilter(schemaServices =>
                ActivatorUtilities.CreateInstance<GraphQLErrorFilter>(schemaServices.GetRootServiceProvider()))
            .AddMaxExecutionDepthRule(MaxExecutionDepth, skipIntrospectionFields: true)
            .ModifyOptions(options =>
            {
                // Each query resolver gets its own DI scope (and DbContext), so root fields can run in parallel.
                options.DefaultQueryDependencyInjectionScope = DependencyInjectionScope.Resolver;
            })
            .ModifyRequestOptions(options =>
            {
                options.ExecutionTimeout = ExecutionTimeout;
                options.IncludeExceptionDetails = environment.IsDevelopment();
            });

        return services;
    }
}
