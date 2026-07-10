# Catalog — Areas & Rooms — Refactor Tracker

> Delivers: management of areas (property zones) and the bookable rooms within them.
> Focus on target behavior; check items off as they are delivered.

## Target functionality

### Areas
- [ ] List areas, with the ability to search by name.
- [ ] Retrieve a single area.
- [ ] Create an area with its name and operating hours (check-in, check-out, reception window).
- [ ] Update an area's details and hours.
- [ ] Deactivate an area without destroying historical data.
- [ ] Only users with catalog permission may create/update/deactivate.

### Rooms
- [ ] List rooms in a defined display order, with the ability to search by name.
- [ ] Retrieve a single room.
- [ ] Create a room belonging to an area with capacity, unit count, nightly fare, and description.
- [ ] Update a room's details.
- [ ] Deactivate a room without destroying historical data.
- [ ] Only users with catalog permission may create/update/deactivate.

## Business rules
- [ ] A room always belongs to a valid area.
- [ ] Capacity, unit count, and fare are non-negative.
- [ ] Deactivating an area/room must not corrupt existing reservations that reference it.
- [ ] Warn (or prevent) when deactivating an item with future reservations.

## Non-functional targets
- [ ] Server-side validation of all inputs.
- [ ] List endpoints support ordering and, where useful, pagination.

## Implementation recommendations
- Use soft-deactivation (active flag) so historical reservations keep meaningful references.
- Store operating hours in a clear, timezone-aware format; default new areas explicitly.
- Give the room "unit count" a clear, unambiguous name distinct from "capacity".
- Validate `area` existence when creating/updating a room; return a typed validation error.
