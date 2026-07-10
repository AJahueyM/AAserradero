# Authentication & Authorization — Refactor Tracker

> Delivers: staff sign-in, session management, role-based permissions, and live-update
> signaling. Focus on target behavior; check items off as they are delivered.

## Target functionality

### Sign-in & sessions
- [ ] Authenticate staff by username and password.
- [ ] Store passwords only as strong salted hashes; verify securely.
- [ ] Establish an authenticated session on success; reject invalid credentials clearly.
- [ ] Provide sign-out that fully invalidates the session.
- [ ] Sessions expire after inactivity and can be renewed on activity.
- [ ] Sessions survive server restarts and work across multiple instances.

### Authorization
- [ ] Support at least two capabilities: manage catalog and manage reservations.
- [ ] Enforce permissions server-side on every protected operation.
- [ ] Return a clear "not permitted" response when a user lacks a capability.
- [ ] Unauthenticated requests to protected resources are rejected/redirected.

### User administration
- [ ] Create, disable, and update staff users and their permissions.
- [ ] Support a password change/reset flow.

### Live updates
- [ ] Notify active sessions when reservation data changes elsewhere, so views can refresh.
- [ ] A session is not notified of its own changes.

## Non-functional targets
- [ ] HTTPS-only with secure, httpOnly, sameSite cookies (or equivalent token security).
- [ ] Brute-force protection (throttling/lockout) on sign-in.
- [ ] Audit trail of who performed sensitive actions.
- [ ] No trust in client-provided role/permission data.

## Implementation recommendations
- Use a vetted auth library/framework; avoid hand-rolling crypto. Keep bcrypt/argon2 for hashing.
- Persist sessions in a shared store (e.g. Redis) or use signed, short-lived tokens with refresh.
- Model permissions as roles/capabilities in the database so they can evolve without code changes.
- Replace polling-based "should update" checks with server push (WebSocket/SSE) for live refresh.
- Centralize authorization in middleware/guards keyed by capability, not scattered per-handler checks.
