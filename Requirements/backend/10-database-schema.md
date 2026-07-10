# Data Model — Refactor Tracker

> Delivers: the persistent data model — entities, relationships, and integrity rules — that
> supports all features. Focus on target outcomes; check items off as they are delivered.

## Target entities

- [ ] **Users** — staff accounts with credentials, permissions, and active state.
- [ ] **Areas** — property zones with operating hours and active state.
- [ ] **Rooms** — bookable units within an area, with capacity, fare, and active state.
- [ ] **Clients** — guests with contact details, VIP/blacklist flags, and active state.
- [ ] **Reservations** — bookings linking a client and room over a date range, with occupants,
      fare, status, promotor, balances, and creation metadata.
- [ ] **Movements** — financial transactions on a reservation (charge/payment, concept,
      method, location, date, responsible user).
- [ ] **Concepts** — billing categories, including a discount indicator.
- [ ] **Payment methods** and **payment locations** — reference lists for movements.
- [ ] **Reservation statuses** — reference list for booking states.
- [ ] **Configuration values** — named global settings (unique keys).

## Integrity & relationships
- [ ] Enforce foreign keys with explicit on-delete/on-update behavior.
- [ ] Rooms reference a valid area; reservations reference valid client/room/area.
- [ ] Movements reference a valid reservation, concept, method, and location.
- [ ] Soft-delete/active flags preserve history instead of hard deletes.
- [ ] Configuration keys are unique.

## Data quality targets
- [ ] Monetary fields use exact decimal types (no floats).
- [ ] Required fields are `NOT NULL` with sensible defaults and check constraints.
- [ ] Indexes support calendar/date-range queries and common lookups.
- [ ] Status meaning is consistent and driven by reference data, not scattered constants.

## Implementation recommendations
- Derive reservation balances from movements; if caching totals, update them transactionally.
- Use consistent types across similar columns (e.g. all active flags the same type).
- Give ambiguous columns clear names (e.g. room "unit count" vs. "capacity").
- Manage schema with migrations and seed reference data (statuses, methods, concepts) reproducibly.
