# Catalog Management — Refactor Tracker

> Delivers: admin UIs for areas, rooms, and billing concepts.
> Focus on target behavior; check items off as they are delivered.

## Target functionality

### Shell
- [ ] Provide sections for Areas, Rooms, and Concepts.

### Areas
- [ ] List areas.
- [ ] Create an area (name and operating hours).
- [ ] Edit an area.
- [ ] Deactivate an area, with confirmation.

### Rooms
- [ ] List rooms with key attributes (area, name, capacity, units, fare, description).
- [ ] Create a room (name, area, capacity, unit count, fare, description).
- [ ] Edit a room, including display order.
- [ ] Deactivate a room, with confirmation.

### Concepts
- [ ] List concepts (name, and whether it charges/credits/discounts).
- [ ] Create and edit concepts.
- [ ] Deactivate concepts, with confirmation; protect foundational concepts.

## Non-functional targets
- [ ] Client-side validation for required fields and numeric ranges (UX), enforced by the server.
- [ ] Consistent error/success feedback.
- [ ] Warn when deactivating items referenced by reservations.

## Implementation recommendations
- Reflect the concept "discount" attribute in the UI rather than protecting items by hidden IDs.
- Use clear labels distinguishing room "capacity" from "unit count".
- Consider adding admin surfaces for payment methods/locations (currently fixed lists).
- Reuse a shared table/form pattern across the three catalog sections.
