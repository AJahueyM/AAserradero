# Shared UI Components — Refactor Tracker

> Delivers: reusable app-wide UI affordances. Focus on target behavior; check items off as delivered.

## Target functionality

### Confirmation dialog
- [ ] Prompt the user to confirm before destructive/irreversible actions.
- [ ] Return a clear confirm/cancel result to the caller.

### Notifications
- [ ] Show transient notifications for errors and, ideally, success/info.
- [ ] Auto-dismiss, with support for multiple/queued messages.

### Loading indicators
- [ ] Provide a consistent loading/blocking state for async operations.

## Non-functional targets
- [ ] Keyboard- and screen-reader-accessible dialogs and notifications.
- [ ] Consistent styling and behavior across all sections.

## Implementation recommendations
- Expose these as hooks/context/services (e.g. `useConfirm`, `useNotify`) rather than imperative refs.
- Support notification severities (error/success/info/warning), not error-only.
- Centralize so any feature can trigger confirmations, notifications, and loading uniformly.
