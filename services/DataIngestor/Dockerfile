FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

COPY Directory.Build.props ./
COPY DataIngestor.Api/DataIngestor.Api.csproj DataIngestor.Api/
COPY DataIngestor.Application/DataIngestor.Application.csproj DataIngestor.Application/
COPY DataIngestor.Background/DataIngestor.Background.csproj DataIngestor.Background/
COPY DataIngestor.Contracts/DataIngestor.Contracts.csproj DataIngestor.Contracts/
COPY DataIngestor.Domain/DataIngestor.Domain.csproj DataIngestor.Domain/
COPY DataIngestor.Infrastructure/DataIngestor.Infrastructure.csproj DataIngestor.Infrastructure/

RUN dotnet restore DataIngestor.Api/DataIngestor.Api.csproj

COPY . .

RUN dotnet publish DataIngestor.Api/DataIngestor.Api.csproj \
    --configuration $BUILD_CONFIGURATION \
    --no-restore \
    --output /app/publish \
    -p:UseAppHost=false \
    -p:MinVerSkip=true

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "DataIngestor.Api.dll"]
