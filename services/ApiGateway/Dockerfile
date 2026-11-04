FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

COPY Directory.Build.props ./
COPY ApiGateway.Api/ApiGateway.Api.csproj ApiGateway.Api/
COPY ApiGateway.BusinessLogic/ApiGateway.BusinessLogic.csproj ApiGateway.BusinessLogic/
COPY ApiGateway.DataAccess/ApiGateway.DataAccess.csproj ApiGateway.DataAccess/

RUN dotnet restore ApiGateway.Api/ApiGateway.Api.csproj

COPY . .

RUN dotnet publish ApiGateway.Api/ApiGateway.Api.csproj \
    --configuration $BUILD_CONFIGURATION \
    --no-restore \
    --output /app/publish \
    -p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "ApiGateway.Api.dll"]
