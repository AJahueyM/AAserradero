# Client Lookup — Refactor Tracker

> Delivers: an inline way to find an existing client or capture a new one during booking/admin.
> Focus on target behavior; check items off as they are delivered.

## Target functionality

- [ ] Toggle between selecting an existing client and entering a new one.
- [ ] Search existing clients by name, with an option to filter VIPs.
- [ ] Clearly flag VIP and blacklisted clients in results.
- [ ] On selecting a client, populate their details and show recent activity (e.g. reservations in the last year).
- [ ] View a selected client's reservation history.
- [ ] Capture/edit client fields: name, address, cellphone, phone, email, tax ID.
- [ ] Return the chosen/entered client to the hosting form.

## Business rules
- [ ] Name and cellphone are required before the client can be used.
- [ ] Blacklisted selection is visibly flagged (and optionally warns/blocks).

## Non-functional targets
- [ ] Debounced search to limit request volume.
- [ ] Non-color cues for VIP/blacklisted states (accessibility).

## Implementation recommendations
- Avoid silently switching to "new client" on edits; make the mode explicit to prevent accidental duplicates.
- Encapsulate the widget with a clear value/callback contract instead of imperative getters.
- Drive status highlighting from shared style tokens rather than inline colors.
