# Antiguo Aserradero Reserva

Hotel/cabin booking & billing system (Spanish UI, `es-MX`). Rebuilt with a C# / ASP.NET Core
backend, a React + MUI frontend, and deployed to Azure Container Apps.

## Tech stack

| Layer | Technology |
|-------|------------|
| Backend | C# / ASP.NET Core on .NET 10 (LTS), Clean Architecture, EF Core |
| Database | Azure SQL Database (Serverless, auto-pause) |
| Frontend | React + TypeScript + Vite + MUI (Material UI), `es-MX` |
| Hosting | Azure Container Apps (scale-to-zero) |
| Identity | Microsoft Entra External ID (redirect / Auth Code + PKCE) |
| Email | Azure Communication Services |
| IaC | Bicep |
| CI/CD | GitHub Actions |

## Solution layout

```
src/
  Domain/          Entities, value objects, domain services (no external deps)
  Application/     Use cases, DTOs, validators, port interfaces
  Infrastructure/  EF Core, migrations, Graph, email, SSE
  Api/             ASP.NET Core host — endpoints, auth, serves the built SPA
  Web/             React + MUI SPA
tests/
  Domain.UnitTests/
  Application.UnitTests/
  Api.IntegrationTests/
infra/
  bicep/           Azure infrastructure modules
  entra/           Entra ID setup script & docs
```

Dependency rule: `Domain ← Application ← {Infrastructure, Api}`.

## Prerequisites

- .NET SDK 10.0+
- Node.js 20+ (frontend)
- Docker (local DB + container build)
- Azure CLI + Bicep (deployment)

## Getting started (backend)

```powershell
dotnet restore
dotnet build
dotnet test
dotnet run --project src/Api
```

Configuration is read from environment variables / `appsettings`. Copy `.env.example` to `.env`
and fill in values for local development. No secrets are committed to source control.

## Deployment

Infrastructure is provisioned with Bicep (`infra/bicep`) and the app is delivered as a container
to Azure Container Apps via GitHub Actions. See `infra/` and `.github/workflows/` for details.
