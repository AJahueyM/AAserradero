using Azure.Core;
using Azure.Identity;
using Microsoft.Data.SqlClient;

namespace AntiguoAserradero.Infrastructure.Persistence;

internal static class SqlConnectionFactory
{
    private static readonly string[] SqlScopes = ["https://database.windows.net/.default"];
    private static readonly DefaultAzureCredential Credential = new();

    public static SqlConnection Create(string connectionString)
    {
        var builder = new SqlConnectionStringBuilder(connectionString);
        var authentication = builder.ContainsKey("Authentication") ? builder["Authentication"]?.ToString() : null;
        var usePasswordless = authentication?.Contains("Active Directory Default", StringComparison.OrdinalIgnoreCase) == true;

        if (!usePasswordless)
        {
            return new SqlConnection(connectionString);
        }

        builder.Remove("Authentication");
        return new SqlConnection(builder.ConnectionString)
        {
            AccessToken = Credential.GetToken(new TokenRequestContext(SqlScopes), CancellationToken.None).Token,
        };
    }
}
