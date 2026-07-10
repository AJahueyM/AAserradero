# Administration — Refactor Tracker

> Delivers: administrative screens for clients and global payment notes.
> Focus on target behavior; check items off as they are delivered.

## Target functionality

### Shell
- [ ] Provide sections for client administration and payment notes.

### Client administration
- [ ] Find and select a client (see [Client Lookup](06-client-lookup.md)).
- [ ] View and edit client details and contact info.
- [ ] Toggle VIP and blacklist status; require a reason when blacklisting.
- [ ] Save changes with confirmation and clear success/failure feedback.

### Payment notes
- [ ] View and edit the global payment-instructions text used in guest notifications.
- [ ] Save with confirmation.

## Non-functional targets
- [ ] Enforce a blacklist reason when blacklisting.
- [ ] Restrict administration to authorized users (server-enforced).

## Implementation recommendations
- Rename the payment-notes section accurately (it edits notification text, not payment methods).
- Consider a searchable client list/table for bulk administration, not just single-select.
- Add a user-management surface (create/disable users, set permissions) as part of admin.
