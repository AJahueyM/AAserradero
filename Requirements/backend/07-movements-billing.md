# Movements & Billing — Refactor Tracker

> Delivers: financial transactions on reservations — charges, payments, discounts — and the
> resulting balances/status. Focus on target behavior; check items off as delivered.

## Target functionality

### Read
- [ ] List movements for a reservation.
- [ ] Retrieve a single movement.

### Create, update, delete
- [ ] Add a movement (charge and/or payment) with concept, payment method, location, and date.
- [ ] Update a movement.
- [ ] Delete a movement.
- [ ] Record who performed each movement.
- [ ] All movement changes require reservations permission.

### Balances
- [ ] Keep each reservation's total charged, total paid, and outstanding balance accurate after any movement change.
- [ ] Treat discount concepts as reductions to the amount owed rather than payments.
- [ ] Recompute reservation payment status after each change.

### Data integrity
- [ ] Provide a way to recompute balances/statuses from movements to correct any drift.

## Business rules
- [ ] Charges and payments are non-negative.
- [ ] Referenced concept, method, and location must be valid.
- [ ] Discounts are identified by a concept attribute, not hardcoded IDs.

## Non-functional targets
- [ ] Movement change + balance update happen atomically.
- [ ] Money uses exact decimal types to avoid rounding errors.

## Implementation recommendations
- Treat movements as the source of truth and derive balances (avoid long-lived denormalized totals; if cached, keep them in the same transaction).
- Model discounts via a concept attribute; avoid overloading a "payment" field to mean "discount".
- Consider immutable/append-style financial records with adjustments rather than in-place edits/deletes, plus receipt numbering.
- Add integration tests covering add/update/delete effects on balances and status.
