namespace AntiguoAserradero.Application.Auth;

public static class ApplicationCapability
{
    public const string CatalogManage = "Catalog.Manage";
    public const string ReservationsManage = "Reservations.Manage";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        CatalogManage,
        ReservationsManage,
    };
}
