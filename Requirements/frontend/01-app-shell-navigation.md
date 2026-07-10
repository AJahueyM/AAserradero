# App Shell & Navigation — Refactor Tracker

> Delivers: the authenticated application layout, navigation, and role-based access.
> Focus on target behavior; check items off as they are delivered.

## Target functionality

- [ ] Provide a top bar with branding and a sign-out action.
- [ ] Sign-out ends the session and returns to the login screen.
- [ ] Present the main sections as navigation:
  - [ ] Reservations — always available.
  - [ ] Check In/Out — for users who manage reservations.
  - [ ] Catalog — for users who manage catalog.
  - [ ] Reports — for users with both capabilities.
  - [ ] Administration — for users with both capabilities.
  - [ ] Regulation/policy — informational section.
- [ ] Show only the sections the current user is permitted to see.
- [ ] Reflect the current section in the URL (deep-linkable navigation).
- [ ] Provide app-wide confirmation and notification affordances to all sections.

## Non-functional targets
- [ ] Handle expired/invalid sessions globally by redirecting to login.
- [ ] Do not rely on client-stored role data for real access control (server enforces).
- [ ] Responsive and accessible layout on a current, supported UI stack.

## Implementation recommendations
- Use a client router so each section has its own route/URL instead of index-based tabs.
- Derive visible navigation from the authenticated user's capabilities returned by the server.
- Provide shared UI services (confirm dialog, notifications) via context/hooks rather than global refs.
- Add a global HTTP layer that handles auth errors and surfaces failures consistently.
