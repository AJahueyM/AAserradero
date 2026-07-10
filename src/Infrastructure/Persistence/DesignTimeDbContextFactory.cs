using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace AntiguoAserradero.Infrastructure.Persistence;

public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AntiguoAserraderoDbContext>
{
    private const string LocalDefaultConnectionString = "Server=(localdb)\\MSSQLLocalDB;Database=AntiguoAserraderoReserva;Trusted_Connection=True;TrustServerCertificate=True;";

    public AntiguoAserraderoDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(ResolveConfigurationBasePath())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("Default") ?? LocalDefaultConnectionString;
        var options = new DbContextOptionsBuilder<AntiguoAserraderoDbContext>()
            .UseSqlServer(SqlConnectionFactory.Create(connectionString), sql => sql.EnableRetryOnFailure())
            .Options;

        return new AntiguoAserraderoDbContext(options);
    }

    private static string ResolveConfigurationBasePath()
    {
        var current = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (current is not null)
        {
            var apiPath = Path.Combine(current.FullName, "src", "Api");
            if (Directory.Exists(apiPath))
            {
                return apiPath;
            }

            current = current.Parent;
        }

        return Directory.GetCurrentDirectory();
    }
}
