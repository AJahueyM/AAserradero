# Reports & Export — Refactor Tracker

> Delivers: occupancy/financial reporting and data export.
> Focus on target behavior; check items off as they are delivered.

## Target functionality

### Data export
- [ ] Expose the exportable data structure (tables/fields) for selection.
- [ ] Export selected data to a spreadsheet file with a meaningful filename.
- [ ] Restrict exportable fields to a safe allow-list.
- [ ] Requires catalog or reservations permission.

### Occupancy & financial reports
- [ ] Generate monthly and annual reports to a spreadsheet.
- [ ] Let the user choose which weekdays count toward occupancy.
- [ ] Report per room across the selected period.
- [ ] Include: occupancy %, nights, reservation count, cancellations, occupants, income,
      discounts, and income grouped by payment date.
- [ ] Requires catalog or reservations permission.

## Business rules
- [ ] Occupancy = occupied days / counted days in the period.
- [ ] Check-in and check-out days count as partial occupancy.
- [ ] Income-by-payment-date groups by when payment occurred, excluding discounts.

## Non-functional targets
- [ ] Never export sensitive fields (e.g. credentials).
- [ ] Handle large exports without exhausting memory (streaming/pagination or async jobs).
- [ ] Validate report parameters (period, selected days) before running.

## Implementation recommendations
- Define an explicit allow-list of exportable entities/fields; reject anything outside it.
- Keep report calculations in tested domain services separate from spreadsheet formatting.
- Offer additional formats (CSV/PDF) where useful, and consider async generation for big reports.
- Externalize report labels for localization instead of embedding strings in logic.
