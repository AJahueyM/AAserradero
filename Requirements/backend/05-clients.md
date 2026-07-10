# Clients — Refactor Tracker

> Delivers: guest/client records with search and VIP/blacklist handling.
> Focus on target behavior; check items off as they are delivered.

## Target functionality

### Search & read
- [ ] Search clients by name (partial match) and filter by VIP.
- [ ] Bounded, paginated results.
- [ ] Retrieve a single client.
- [ ] Report a client's recent activity (e.g. count of non-cancelled reservations in the last year).

### Create & update
- [ ] Create a client with contact details (name, tax ID, address, email, phone, cellphone).
- [ ] Update a client, including VIP status, blacklist status, and blacklist reason.
- [ ] Client changes require reservations permission.

### Lifecycle
- [ ] Deactivate a client consistently (soft-delete) without breaking historical reservations.

## Business rules
- [ ] Name and cellphone are required.
- [ ] A blacklisted client must carry a reason.
- [ ] Blacklisted clients are flagged (and optionally blocked) during booking.

## Non-functional targets
- [ ] Validate email, tax ID, and phone formats server-side.
- [ ] Normalize inputs to reduce duplicate client records.

## Implementation recommendations
- Standardize on soft-delete for clients to match other catalog entities and preserve history.
- Add normalization (trim/casing/phone formatting) and optional duplicate detection/merge tooling.
- Expose recent-activity as part of the client resource to avoid a separate round-trip.
- Return typed validation errors for required/format failures.
