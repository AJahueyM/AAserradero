# syntax=docker/dockerfile:1

# ---------------------------------------------------------------------------
# Antiguo Aserradero Reserva — API container image.
# The React SPA is hosted separately on Azure Static Web Apps, so this image
# ships the ASP.NET Core API only. (The API keeps a wwwroot SPA fallback, so a
# single-container deployment remains possible by adding the built SPA later.)
# ---------------------------------------------------------------------------

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first (better layer caching) using only project + central package files.
COPY global.json Directory.Build.props Directory.Packages.props ./
COPY src/Domain/AntiguoAserradero.Domain.csproj src/Domain/
COPY src/Application/AntiguoAserradero.Application.csproj src/Application/
COPY src/Infrastructure/AntiguoAserradero.Infrastructure.csproj src/Infrastructure/
COPY src/Api/AntiguoAserradero.Api.csproj src/Api/
RUN dotnet restore src/Api/AntiguoAserradero.Api.csproj

# Copy the rest of the backend source and publish.
COPY src/Domain/ src/Domain/
COPY src/Application/ src/Application/
COPY src/Infrastructure/ src/Infrastructure/
COPY src/Api/ src/Api/
RUN dotnet publish src/Api/AntiguoAserradero.Api.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_TieredPGO=1
COPY --from=build /app/publish ./
EXPOSE 8080
# Run as the non-root user provided by the .NET runtime image.
USER $APP_UID
ENTRYPOINT ["dotnet", "AntiguoAserradero.Api.dll"]
