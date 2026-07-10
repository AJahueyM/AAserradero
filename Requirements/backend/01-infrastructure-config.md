# Infrastructure & Configuration — Refactor Tracker

> Delivers: the application server, configuration, environment management, and build/deploy
> foundation. Focus on target behavior; check items off as they are delivered.

## Target functionality

### Application server
- [ ] Serve the HTTP API and the client application.
- [ ] Configurable listen port with a sensible default.
- [ ] Parse JSON (and form) request bodies.
- [ ] Serve the built frontend and its static assets.
- [ ] Centralized request logging.
- [ ] Centralized error handling with consistent, typed error responses.
- [ ] Health/readiness endpoint for monitoring and deployment.

### Configuration & environments
- [ ] All settings (ports, DB connection, secrets, base URLs) sourced from environment/config.
- [ ] Documented `.env.example` and startup validation that fails fast on missing config.
- [ ] Separate config for development, staging, and production.
- [ ] No secrets, hosts, or credentials hardcoded in source.

### Data access
- [ ] Managed database connection pool with error handling and reconnection.
- [ ] Graceful startup and shutdown (drain connections, stop accepting requests).

### Build & delivery
- [ ] Modern, supported build toolchain producing optimized client bundles.
- [ ] Reproducible builds and a single documented start/build/deploy workflow.
- [ ] CI pipeline running lint, tests, and build on every change.

## Non-functional targets
- [ ] Serve exclusively over HTTPS/TLS.
- [ ] Security headers and a defined CORS policy.
- [ ] Rate limiting on public endpoints.
- [ ] Structured logs suitable for aggregation.

## Implementation recommendations
- Load config once at startup into a validated, typed config object; inject it rather than
  reading `process.env` throughout the code.
- Put a reverse proxy (or platform) in front for TLS termination, compression, and static caching.
- Add a global error-handling middleware that maps domain errors to HTTP status codes and a
  consistent JSON error shape (`{ error: { code, message } }`).
- Use a process manager or container orchestration with health checks for restarts and scaling.
- Provide a containerized dev environment so DB + app start with one command.
