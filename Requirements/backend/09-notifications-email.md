# Notifications & Email — Refactor Tracker

> Delivers: guest-facing reservation confirmations/notifications.
> Focus on target behavior; check items off as they are delivered.

## Target functionality

- [ ] Generate a guest confirmation for a reservation containing: reservation reference,
      client name/contact, area/room, stay dates and times, occupants, and description.
- [ ] Include a payment ledger and outstanding balance.
- [ ] Optionally include payment instructions (bank/account details) when requested.
- [ ] Provide notification variants (e.g. standard and a compact version).
- [ ] Send the confirmation to the guest (not only render it) via email.
- [ ] Support the property's regulation/cancellation-policy content.

## Business rules
- [ ] All amounts and dates are formatted for the local locale.
- [ ] Payment/bank details come from configuration, not embedded in templates.

## Non-functional targets
- [ ] Escape/sanitize all guest-provided content in generated messages.
- [ ] Record delivery outcome; support retries on failure.

## Implementation recommendations
- Integrate an email/delivery provider so confirmations are actually sent, with logging and retries.
- Use a single parameterized template with variants/flags instead of multiple near-duplicate templates.
- Move bank/account and policy text into configuration so staff can update it without code changes.
- Separate content generation from delivery so notifications can be previewed and tested.
