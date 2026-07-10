using AntiguoAserradero.Domain.Errors;

namespace AntiguoAserradero.Application.Reports;

public static class ExportCatalog
{
    private static readonly Dictionary<string, IReadOnlyList<string>> AllowList =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["areas"] = ["id", "name", "checkInTime", "checkOutTime", "isActive"],
            ["rooms"] = ["id", "area", "name", "capacity", "unitCount", "nightlyFare", "description", "displayOrder", "isActive"],
            ["clients"] = ["id", "name", "email", "phone", "cellphone", "isVip", "isActive"],
            ["reservations"] = ["id", "client", "room", "area", "entryDate", "exitDate", "checkInTime", "checkOutTime", "adults", "children", "infants", "pets", "fare", "status", "notes", "createdAt", "isActive"],
            ["movements"] = ["id", "reservationId", "concept", "paymentMethod", "paymentLocation", "charge", "payment", "date", "createdAt"],
            ["concepts"] = ["id", "code", "name", "isDiscount", "isProtected", "isActive"],
            ["paymentMethods"] = ["id", "code", "name", "isActive"],
            ["paymentLocations"] = ["id", "code", "name", "isActive"],
            ["reservationStatuses"] = ["id", "code", "label", "sortOrder"],
        };

    public static ExportMetadata GetMetadata()
    {
        return new ExportMetadata(AllowList
            .OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
            .Select(pair => new ExportEntityMetadata(pair.Key, pair.Value))
            .ToArray());
    }

    public static IReadOnlyList<string> Validate(string entity, IReadOnlyList<string> fields)
    {
        if (!AllowList.TryGetValue(entity, out var allowedFields))
        {
            throw new ValidationException("Reports.Export.EntityNotAllowed", "La entidad solicitada no está permitida para exportación.", new { entity });
        }

        if (fields.Count == 0)
        {
            throw new ValidationException("Reports.Export.FieldsRequired", "Seleccione al menos un campo para exportar.");
        }

        var allowed = allowedFields.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var invalid = fields.Where(field => !allowed.Contains(field)).ToArray();
        if (invalid.Length > 0)
        {
            throw new ValidationException("Reports.Export.FieldNotAllowed", "Uno o más campos no están permitidos para exportación.", new { entity, fields = invalid });
        }

        return fields.Select(field => allowedFields.First(allowedField => string.Equals(allowedField, field, StringComparison.OrdinalIgnoreCase))).ToArray();
    }
}
