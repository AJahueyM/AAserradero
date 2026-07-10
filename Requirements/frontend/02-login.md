# Login — Refactor Tracker

> Delivers: the staff sign-in experience. Focus on target behavior; check items off as delivered.

## Target functionality

- [ ] Provide a sign-in form with username and password.
- [ ] Submit credentials and establish an authenticated session.
- [ ] Show a loading state while signing in.
- [ ] On success, take the user to the main application.
- [ ] On invalid credentials, show a clear, field-level error.
- [ ] On connection/server errors, show a distinct, actionable message.

## Non-functional targets
- [ ] Do not store session/role data in client-accessible storage.
- [ ] Validate inputs (non-empty, trimmed) before submit.
- [ ] Prevent duplicate submissions while a request is pending.
- [ ] Accessible labels, focus handling, and error announcements.

## Implementation recommendations
- Rely on secure server-set cookies/tokens for the session rather than persisting user data client-side.
- Distinguish "wrong credentials" from "cannot reach server" in the UI.
- Provide entry points for password reset once the backend flow exists.
- Keep the login screen isolated from the authenticated shell (separate bundle/route).
