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

## Getting started (local)

Start local dependencies (SQL Server + a mail catcher) with Docker:

```powershell
docker compose up -d
```

Backend:

```powershell
dotnet restore
dotnet build
dotnet test
dotnet run --project src/Api
```

Frontend (in a second terminal):

```powershell
cd src/Web
npm install
npm run dev   # Vite dev server proxies /api to the backend on http://localhost:8080
```

Configuration is read from environment variables / `appsettings`. Copy `.env.example` to `.env`
and fill in values for local development. No secrets are committed to source control.

## Deployment (Azure)

Everything is provisioned with **Bicep** (`infra/bicep`) and deployed by **GitHub Actions**
(`.github/workflows/deploy.yml`) using OIDC federated login — no stored cloud secrets:

- **API**: containerized (`Dockerfile`), built server-side with `az acr build`, pushed to Azure
  Container Registry, and run on **Azure Container Apps** (scale-to-zero, `minReplicas: 0`).
- **Frontend**: the React SPA is built with production `VITE_*` values and deployed to **Azure
  Static Web Apps** (Free tier), so the UI loads instantly while the API scales to zero. The SWA
  URL is automatically added to the API's CORS allow-list.
- **Database**: **Azure SQL Database Serverless** with auto-pause; the app connects passwordlessly
  via the Container App's **managed identity** (`Authentication=Active Directory Default`).
- **Email**: Azure Communication Services (managed identity).
- **Identity**: Microsoft Entra External ID — see `infra/entra` for app-registration scripts and
  the runbook. User provisioning uses Microsoft Graph via the managed identity.

`.github/workflows/ci.yml` runs backend build+test, frontend lint+build, and Bicep build+lint on
every change. See `infra/bicep/README.md` and `infra/entra/README.md` for step-by-step details and
the required GitHub Actions variables/secrets.

## Conventions

- All timestamps are stored and transmitted in **UTC** (ISO-8601 `Z`); the frontend converts to
  local time for display.
- Money uses exact `decimal` types; reservation balances are derived from movements.
- Business rules are enforced server-side; the API returns typed errors as
  `{ "error": { "code", "message", "details"? } }`.
