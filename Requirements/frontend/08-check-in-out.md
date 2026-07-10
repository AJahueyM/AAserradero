# Check In / Out — Refactor Tracker

> Delivers: a view of arrivals and departures for a chosen date window.
> Focus on target behavior; check items off as they are delivered.

## Target functionality

- [ ] Choose a start and end date to define the window.
- [ ] List arrivals (check-ins) within the window.
- [ ] List departures (check-outs) within the window.
- [ ] Show per-booking details: reference, area, room, client, occupants, nights, arrival and
      departure times, outstanding balance, and contact number.
- [ ] Present results in a consistent, sortable order.

## Non-functional targets
- [ ] Validate the date range (start ≤ end) before querying.
- [ ] Show explicit empty, loading, and error states.
- [ ] Handle large result sets gracefully.

## Implementation recommendations
- Consider recording an actual check-in/check-out action/state (the legacy view is read-only).
- Offer a printable/exportable arrivals & departures sheet for front-desk use.
- Reuse shared date-formatting and balance calculations across modules.
