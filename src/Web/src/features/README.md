# Feature extension pattern

Add each feature under `src/features/<feature-name>` and export its page/route fragment. Then register it in `src/features/routes.tsx` as a `FeatureRoute`.

Use capability helpers for navigation visibility:

- `requireAllCapabilities('Catalog.Manage', 'Reservations.Manage')`
- `requireAnyCapability('Catalog.Manage', 'Reservations.Manage')`
- `{ mode: 'always' }`

The route registry feeds both `AppShell` navigation and `src/routes.tsx`. Client capability checks are UX only; the API must still enforce authorization.
