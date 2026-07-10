# Antiguo Aserradero Reserva — Frontend

Vite + React 18 + TypeScript foundation for the staff booking and billing app. The UI locale is `es-MX`; component text must use translation keys from `src/i18n`.

## Run

```powershell
npm install
npm run dev
npm run lint
npm run build
```

Copy `.env.example` to `.env.local` and set the `VITE_*` values for Microsoft Entra External ID and the API. The Vite dev server proxies `/api` to `http://localhost:8080`; set `VITE_API_BASE_URL` only when the API is hosted elsewhere.

## Structure

- `src/app` — application providers and router host.
- `src/auth` — MSAL redirect configuration, login/logout helpers, protected route, and `/api/me` query.
- `src/api` — typed fetch client, `ApiError`, React Query client, and SSE hook.
- `src/lib` — shared utilities, especially UTC/local date-time helpers.
- `src/ui` — hook/context UI services: confirm, notify, and blocking loading overlay.
- `src/layout` — authenticated shell, capability-aware nav, and informational pages.
- `src/features` — feature route registry and extension pattern for later feature agents.
- `src/theme` — MUI theme using the required exact palette.
- `src/i18n` — `es-MX` resources and initialization.
- `src/routes.tsx` — top-level deep-linkable route tree.

## Environment variables

- `VITE_AAD_CLIENT_ID` — Entra External ID application client ID.
- `VITE_AAD_AUTHORITY` — authority such as `https://<tenant>.ciamlogin.com/<tenant>.onmicrosoft.com`.
- `VITE_AAD_REDIRECT_URI` — redirect URI registered in Entra.
- `VITE_API_SCOPE` — API scope requested by MSAL and attached to API calls.
- `VITE_API_BASE_URL` — optional absolute API origin; empty means same-origin `/api` via Vite proxy.

## API assumptions

All endpoints are under `/api`. The HTTP client attaches `Authorization: Bearer <token>` for normal API requests and throws `ApiError { code, message, details, status }` for error bodies shaped as `{ error: { code, message, details? } }`. `GET /api/me` returns `{ id, displayName, capabilities }` and drives navigation visibility only; the server remains authoritative.

`useServerEvents` connects to `GET /api/events` with an MSAL access token and exposes typed connection state plus the latest parsed event payload. Browser-native `EventSource` cannot set custom headers, so the hook passes the token as an `access_token` query parameter; if the backend requires an `Authorization` header for SSE, replace the connection factory in this hook with an EventSource polyfill that supports headers.

## UTC/local date-time convention

The backend stores and returns every timestamp as UTC ISO-8601 with trailing `Z`. Do not render raw API date strings in components. Always use `src/lib/datetime.ts`:

- `parseApiDate(utcIso)` validates and parses a UTC API timestamp.
- `formatLocal(utcIso, fmt)` converts UTC to the user's local time zone and formats with `date-fns` Spanish locale.
- `toUtcIso(localDate)` converts local user input (`Date`) to an ISO UTC string before sending it to the API.

## Feature plugin pattern

Each feature lives in `src/features/<feature-name>` with its pages and an exported route fragment. Register the feature in `src/features/routes.tsx` by adding a `FeatureRoute` object with an `id`, `path`, `labelKey`, optional `descriptionKey`, capability requirements, icon, and element.

The shell reads this registry to build both navigation and route objects. Use `requireAllCapabilities('Catalog.Manage', 'Reservations.Manage')` or `requireAnyCapability(...)` for UX visibility. Server-side authorization is still required for real access control.
