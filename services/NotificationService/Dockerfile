FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

COPY Directory.Build.props ./
COPY NotificationService.Api/NotificationService.Api.csproj NotificationService.Api/
COPY NotificationService.Application/NotificationService.Application.csproj NotificationService.Application/
COPY NotificationService.Infrastructure/NotificationService.Infrastructure.csproj NotificationService.Infrastructure/

RUN dotnet restore NotificationService.Api/NotificationService.Api.csproj

COPY . .

RUN dotnet publish NotificationService.Api/NotificationService.Api.csproj \
    --configuration $BUILD_CONFIGURATION \
    --no-restore \
    --output /app/publish \
    -p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "NotificationService.Api.dll"]
