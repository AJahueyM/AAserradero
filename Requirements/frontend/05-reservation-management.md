# Reservation Management — Refactor Tracker

> Delivers: creating, reviewing, and editing reservations, including payments and notifications.
> Focus on target behavior; check items off as they are delivered.

## Target functionality

### Create a reservation
- [ ] Choose room, status, and promotor; set fare (defaulting from the room, overridable).
- [ ] Set stay dates/times with sensible defaults.
- [ ] Enter occupants (adults, children, infants, pets) and notes.
- [ ] Find or create the client inline (see [Client Lookup](06-client-lookup.md)).
- [ ] Prevent submission when occupants exceed capacity or dates are invalid.
- [ ] Surface booking conflicts and permission errors clearly.

### Review a reservation
- [ ] Show booking summary, client, occupants, and a financial summary (charged/paid/outstanding).
- [ ] Show the movements (charges/payments) history.
- [ ] Clearly indicate cancelled reservations.

### Edit
- [ ] Edit client, booking information (room/status/promotor/dates/fare), and occupants.
- [ ] Add, edit, and delete movements, with confirmation for deletions.
- [ ] Edit notes to the client.
- [ ] Cancel the reservation, with confirmation.

### Notify
- [ ] Generate a guest notification (with or without payment instructions) and share it with the guest.

### Search results list
- [ ] Present multiple matching reservations with key details and totals; open one on selection.

## Non-functional targets
- [ ] Disable actions while pending to prevent duplicate submissions.
- [ ] Surface server validation errors at the relevant fields.
- [ ] Confirm before discarding unsaved edits.

## Implementation recommendations
- Build modular forms and derive totals/status from server responses rather than local math.
- Replace magic defaults (status, payment method/location) with values from reference data.
- Ideally have the server perform client-save + booking-create atomically; otherwise handle partial-failure gracefully.
- Make notes saving explicit (or clearly indicate auto-save success/failure).
- Split large dialogs into focused components with separated data-access logic.
