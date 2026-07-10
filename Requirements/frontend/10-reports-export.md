# Reports & Export — Refactor Tracker

> Delivers: UIs for generating occupancy reports and exporting data.
> Focus on target behavior; check items off as they are delivered.

## Target functionality

### Shell
- [ ] Provide sections for data export and occupancy reports.

### Data export
- [ ] Present the exportable data grouped by entity, with per-entity and per-field selection.
- [ ] Generate and download the export file with a meaningful filename.

### Occupancy report
- [ ] Choose which weekdays count toward occupancy.
- [ ] Choose the reporting period (month/year).
- [ ] Account for special date ranges (e.g. holidays) where relevant.
- [ ] Generate and download the report file.

## Non-functional targets
- [ ] Prevent selecting sensitive fields for export.
- [ ] Show progress and error handling during generation.
- [ ] Validate that a period and at least one weekday are selected before submitting.

## Implementation recommendations
- Configure real holiday/special date ranges (the legacy UI used placeholders).
- Expose both monthly and annual reporting if the backend supports it.
- Drop obsolete browser-specific download handling in favor of a standard approach.
- Constrain export selection to a server-side allow-list (see backend Reports & Export).
