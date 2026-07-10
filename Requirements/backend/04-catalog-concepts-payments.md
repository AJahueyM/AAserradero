# Catalog — Concepts, Payments, Statuses & Config — Refactor Tracker

> Delivers: billing concepts, payment methods/locations, reservation statuses, user lookups,
> and global configuration values. Focus on target behavior; check items off as delivered.

## Target functionality

### Billing concepts
- [ ] List available billing concepts.
- [ ] Create, update, and deactivate concepts.
- [ ] Mark concepts that represent discounts/credits so billing treats them correctly.
- [ ] Protect foundational concepts required by the system from deletion.
- [ ] Concept changes require catalog permission.

### Payment methods & locations
- [ ] Provide selectable payment methods (forms) for movements.
- [ ] Provide selectable payment locations (places) for movements.
- [ ] Allow these lists to be managed rather than fixed in code.

### Reservation statuses
- [ ] Provide the set of reservation statuses as data for display and selection.

### User lookups
- [ ] Provide a safe list of users (identifier and display name only) for assigning promotors.

### Global configuration values
- [ ] Read and update named configuration values (e.g. payment instructions text).
- [ ] Updates require appropriate permission.

## Business rules
- [ ] Discount behavior is driven by a concept attribute, not hardcoded identifiers.
- [ ] Configuration keys are constrained to a known, documented set.

## Non-functional targets
- [ ] User lookups never expose credentials or permission internals.
- [ ] Validate configuration values before saving.

## Implementation recommendations
- Add an explicit `isDiscount` attribute on concepts to replace hardcoded discount ID lists.
- Manage statuses, payment methods, and locations as reference data with an admin surface.
- Store global config as key/value with a unique key constraint and a validated allow-list.
- Clarify the meaning of a concept's charge/credit attributes (amounts vs. flags) in the model.
