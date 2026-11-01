FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

COPY Directory.Build.props ./
COPY DataProcessor.Api/DataProcessor.Api.csproj DataProcessor.Api/
COPY DataProcessor.Application/DataProcessor.Application.csproj DataProcessor.Application/
COPY DataProcessor.Domain/DataProcessor.Domain.csproj DataProcessor.Domain/
COPY DataProcessor.Infrastructure/DataProcessor.Infrastructure.csproj DataProcessor.Infrastructure/

RUN dotnet restore DataProcessor.Api/DataProcessor.Api.csproj

COPY . .

RUN dotnet publish DataProcessor.Api/DataProcessor.Api.csproj \
    --configuration $BUILD_CONFIGURATION \
    --no-restore \
    --output /app/publish \
    -p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "DataProcessor.Api.dll"]
