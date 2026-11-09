using System.Text.Json.Serialization;
using ApiGateway.Api.Configuration;
using ApiGateway.Api.GraphQL.Errors;
using ApiGateway.Api.GraphQL.Inputs;
using ApiGateway.Api.GraphQL.Queries;
using ApiGateway.Api.GraphQL.Types.Readings;
using ApiGateway.Api.GraphQL.Types.Statistics;
using ApiGateway.Api.Rest.Errors;
using HotChocolate;
using HotChocolate.Types;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;
using Serilog;

namespace ApiGateway.Api;

public static class DependencyInjection
{
    private const int MaxExecutionDepth = 8;

    private const string OpenApiDocumentPath = "/openapi/v1.json";

    private const string OpenApiDocumentTitle = "Pulse Analytics API Gateway";

    private static readonly TimeSpan ExecutionTimeout = TimeSpan.FromSeconds(15);

    private static readonly TimeSpan PreflightMaxAge = TimeSpan.FromMinutes(10);

    public static IServiceCollection AddSerilogLogging(this IServiceCollection services, IConfiguration configuration)
    {
        var seqSettings = configuration.GetSection(SeqSettings.SectionName).Get<SeqSettings>();

        services.AddLogging(logging => logging.ClearProviders());

        services.AddSerilog(
            (serviceProvider, loggerConfiguration) =>
            {
                loggerConfiguration
                    .ReadFrom.Configuration(configuration)
                    .ReadFrom.Services(serviceProvider)
                    .Enrich.FromLogContext();

                if (!string.IsNullOrWhiteSpace(seqSettings?.ServerUrl))
                {
                    loggerConfiguration.WriteTo.Seq(seqSettings.ServerUrl, apiKey: seqSettings.ApiKey);
                }
            },
            writeToProviders: true);

        return services;
    }

    public static IServiceCollection AddCrossOriginRequests(this IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddOptions<CorsSettings>()
            .Bind(configuration.GetSection(CorsSettings.SectionName))
            .ValidateOnStart()
            .ValidateDataAnnotations();

        services.AddCors();

        services
            .AddOptions<CorsOptions>()
            .Configure<IOptions<CorsSettings>>((corsOptions, corsSettings) => corsOptions.AddDefaultPolicy(policy => policy
                .WithOrigins(corsSettings.Value.AllowedOrigins)
                .WithMethods(HttpMethods.Get, HttpMethods.Post)
                .AllowAnyHeader()
                .WithExposedHeaders(HeaderNames.ContentDisposition)
                .SetPreflightMaxAge(PreflightMaxAge)));

        return services;
    }

    public static IServiceCollection AddGraphQLApi(this IServiceCollection services)
    {
        services
            .AddGraphQLServer()
            .AddQueryType()
            .AddTypeExtension(typeof(MeterReadingQueries))
            .AddTypeExtension(typeof(ReadingStatisticsQueries))
            .AddType<MeterReadingInterfaceType>()
            .AddType<MotionReadingType>()
            .AddType<EnergyReadingType>()
            .AddType<AirQualityReadingType>()
            .AddType<ReadingCountType>()
            .AddType<EnergyStatisticsType>()
            .AddType<AirQualityStatisticsType>()
            .AddType<MetricStatisticsType>()
            .AddType<MotionStatisticsType>()
            .AddType<ReadingFilterInputType>()
            .AddType<LatestReadingsFilterInputType>()
            .AddType<StatisticsFilterInputType>()
            // The schema has its own service container; the filter needs application services (logging).
            .AddErrorFilter(schemaServices =>
                ActivatorUtilities.CreateInstance<GraphQLErrorFilter>(schemaServices.GetRootServiceProvider()))
            .AddMaxExecutionDepthRule(MaxExecutionDepth, skipIntrospectionFields: true)
            .AddInstrumentation(options => options.IncludeOperationNameInSpanName = true)
            .ModifyOptions(options =>
            {
                // Each query resolver gets its own DI scope (and DbContext), so root fields can run in parallel.
                options.DefaultQueryDependencyInjectionScope = DependencyInjectionScope.Resolver;
            })
            .ModifyRequestOptions(options =>
            {
                options.ExecutionTimeout = ExecutionTimeout;
            });

        return services;
    }

    public static IServiceCollection AddRestApi(this IServiceCollection services)
    {
        services.AddControllers();

        services.AddProblemDetails();
        services.AddExceptionHandler<ApiExceptionHandler>();

        services.ConfigureHttpJsonOptions(options => options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

        services.AddOpenApi(options => options.AddDocumentTransformer((document, _, _) =>
        {
            document.Info.Title = OpenApiDocumentTitle;
            document.Info.Description = "REST endpoints of the API gateway. Dashboard data is served by GraphQL at /graphql.";

            return Task.CompletedTask;
        }));

        return services;
    }

    public static WebApplication UseOpenApiDocumentation(this WebApplication app)
    {
        app.MapOpenApi();

        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint(OpenApiDocumentPath, OpenApiDocumentTitle);
            options.DocumentTitle = OpenApiDocumentTitle;
        });

        return app;
    }
}
