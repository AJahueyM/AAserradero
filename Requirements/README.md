# Antiguo Aserradero Reserva — Refactor Tracker

Task tracker for rebuilding the reservation-management system from scratch with proper
engineering practices. Each document describes the **target functionality** a component must
deliver — not how the legacy code works. Use the checkboxes to track refactor progress.

- Application: hotel/cabin booking & billing system (Spanish UI, locale `es-MX`).
- Target stack is open — these docs define *what* each part must do, not *how*.

## How to use this tracker

- Every item is a unit of work to be delivered by the refactor. Check it off when the new
  implementation satisfies it (built, tested, reviewed).
- `[ ]` — not yet refactored / pending.
- `[x]` — delivered in the new implementation.
- Each document ends with **Implementation recommendations** — guidance to build it well.

## Document map

### Backend — [`backend/`](backend/)

| # | Document | Delivers |
|---|----------|----------|
| 01 | [Infrastructure & Config](backend/01-infrastructure-config.md) | App server, configuration, environments, build/deploy |
| 02 | [Authentication & Authorization](backend/02-authentication-authorization.md) | Login, sessions, roles/permissions, live updates |
| 03 | [Catalog — Areas & Rooms](backend/03-catalog-areas-rooms.md) | Manage areas and bookable rooms |
| 04 | [Catalog — Concepts & Payments](backend/04-catalog-concepts-payments.md) | Billing concepts, payment methods, statuses, config |
| 05 | [Clients](backend/05-clients.md) | Guest records, search, VIP/blacklist |
| 06 | [Reservations](backend/06-reservations.md) | Booking lifecycle, availability, status |
| 07 | [Movements & Billing](backend/07-movements-billing.md) | Charges, payments, balances, discounts |
| 08 | [Reports & Export](backend/08-reports-export.md) | Occupancy/financial reports and data export |
| 09 | [Notifications & Email](backend/09-notifications-email.md) | Guest confirmations and notifications |
| 10 | [Data Model](backend/10-database-schema.md) | Entities, relationships, integrity |

### Frontend — [`frontend/`](frontend/)

| # | Document | Delivers |
|---|----------|----------|
| 01 | [App Shell & Navigation](frontend/01-app-shell-navigation.md) | Layout, navigation, role-based access |
| 02 | [Login](frontend/02-login.md) | Sign-in experience |
| 03 | [Shared UI Components](frontend/03-shared-widgets.md) | Confirmations, notifications, loading states |
| 04 | [Reservations & Calendar](frontend/04-reservations-calendar.md) | Availability calendar, live updates, search |
| 05 | [Reservation Management](frontend/05-reservation-management.md) | Create/review/edit bookings and payments |
| 06 | [Client Lookup](frontend/06-client-lookup.md) | Find/create guests inline |
| 07 | [Catalog Management](frontend/07-catalog-management.md) | Areas, rooms, concepts admin |
| 08 | [Check In / Out](frontend/08-check-in-out.md) | Arrivals and departures |
| 09 | [Administration](frontend/09-administration.md) | Client admin, payment notes |
| 10 | [Reports & Export](frontend/10-reports-export.md) | Report and export UIs |

## Reservation status model (target)

The system must express these booking states (names are the current Spanish labels; the new
implementation should treat them as data, not hardcoded numbers):

| State | Label (es-MX) | Meaning |
|-------|---------------|---------|
| Pending | Oferta | Created, no payment yet |
| Partial | Separada | Partially paid |
| Paid | No disponible | Paid in full |
| Maintenance | Mantenimiento | Blocked for maintenance |
| Courtesy | Cortesía | Comp/complimentary |
| Cancelled | — | Cancelled (excluded from availability) |

Availability display also distinguishes free days and past days.

## Domain glossary

- **Area** — a property/zone with its own check-in/out and reception hours.
- **Room** — a bookable unit in an area, with capacity and a nightly fare.
- **Client** — the guest/booker; may be VIP or blacklisted.
- **Reservation** — a room booked by a client for a date range, with occupants and a balance.
- **Movement** — a financial transaction (charge and/or payment) on a reservation.
- **Concept** — a billing category; some concepts represent discounts.
- **Promotor** — the user who owns/created the reservation.

## System-wide goals for the refactor

- [ ] Treat statuses, discount concepts, and other domain constants as configurable data, not magic numbers.
- [ ] Enforce all business rules server-side; the UI validates for UX only.
- [ ] Money handled with exact decimal types; balances derived from source-of-truth transactions.
- [ ] Secure auth (server-side sessions/tokens, HTTPS, secure cookies) — no trusting client-stored roles.
- [ ] Consistent, typed API error responses.
- [ ] Automated tests (unit + integration) and CI for every component.
- [ ] Accessible, responsive UI on a current, supported stack.
