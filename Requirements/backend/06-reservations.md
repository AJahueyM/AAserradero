# Reservations — Refactor Tracker

> Delivers: the booking lifecycle — search, create, update, cancel — with availability and
> status rules. Focus on target behavior; check items off as they are delivered.

## Target functionality

### Search & read
- [ ] Search reservations for a date range (calendar view).
- [ ] Search arrivals within a window (check-in date range).
- [ ] Search departures within a window (check-out date range).
- [ ] Search by client phone, client name, and by client.
- [ ] Retrieve a single reservation with its financial movements.

### Create
- [ ] Create a reservation for a room and client over a date range with occupants
  (adults, children, infants, pets), fare, status, promotor, and notes.
- [ ] Reject creation that conflicts with an existing booking for the same room.
- [ ] On creation, record the initial lodging charge for the stay automatically.
- [ ] Record who created the reservation and when.
- [ ] Requires reservations permission.

### Update
- [ ] Update reservation details (dates, room, occupants, fare, status, promotor, notes).
- [ ] Re-validate availability on update, excluding the reservation itself.
- [ ] Requires reservations permission.

### Cancel
- [ ] Cancel a reservation so it is excluded from availability but retained for records.
- [ ] Requires reservations permission.

## Business rules
- [ ] Entry date must be before exit date.
- [ ] No overlapping active reservations for the same room.
- [ ] Occupants must not exceed room capacity.
- [ ] Reservation status reflects payment progress: pending, partial, or paid.
- [ ] Nights are computed consistently from the stay dates.
- [ ] Changes trigger the live-update signal so other users' views refresh.

## Non-functional targets
- [ ] All rules enforced server-side; return typed errors (conflict, forbidden, validation).
- [ ] Reservation creation (booking + initial charge) is atomic.

## Implementation recommendations
- Represent status transitions explicitly and derive payment-based status from movements, not stored flags that can drift.
- Wrap booking + initial charge in a single transaction so a failure rolls back cleanly.
- Replace magic constants (status codes, default concept/method for the initial charge) with named domain values/config.
- Use a monetary type that supports high fares without overflow.
- Make the conflict check a well-tested domain function with clear overlap semantics.
