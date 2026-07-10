# Reservations & Calendar — Refactor Tracker

> Delivers: the reservations hub — a monthly availability calendar, live updates, and search.
> Focus on target behavior; check items off as they are delivered.

## Target functionality

### Calendar view
- [ ] Show a monthly calendar with one row per room and one column per day.
- [ ] Navigate between months (picker and previous/next).
- [ ] Indicate each room/day state clearly: available, booked (by status), past, and
      transition days where a stay starts or ends (half-day).
- [ ] On hover, reveal booking details (client, dates, reference, outstanding balance).
- [ ] Click an available day to start a booking (prefilled room and default times).
- [ ] Click a booked day to open that reservation.

### Live updates
- [ ] Reflect changes made by other users without a manual refresh.

### Search
- [ ] Search reservations by reference, client phone, and client name.
- [ ] A single match opens the reservation; multiple matches show a selectable list.

## Non-functional targets
- [ ] Status indicators include non-color cues (accessibility).
- [ ] Efficient loading of reference data and referenced clients (avoid excessive requests).
- [ ] Performs well with many rooms and long months.

## Implementation recommendations
- Drive day states from server data and shared status definitions; avoid hardcoded color/number mappings.
- Prefer server push (WebSocket/SSE) over polling for live updates.
- Default check-in/out times should derive from the area's configured hours, not fixed constants.
- Batch client/reference lookups; consider a dedicated availability endpoint to simplify calendar rendering.
- Isolate the complex "shared/transition day" logic in a tested module.
