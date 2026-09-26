using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Infrastructure.Persistence;

/// <summary>
/// Usada apenas em tempo de design (CLI do EF Core: migrations, script, etc.).
/// Permite ao `dotnet ef` construir o <see cref="AppDbContext"/> sem inicializar
/// a API e sem precisar de conexao valida com o PostgreSQL.
/// A conexao pode vir de ASPNETCORE_ENVIRONMENT + appsettings ou da variavel
/// de ambiente "ConnectionStrings__DefaultConnection" (usada pelo Docker Compose).
/// </summary>
public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    /// <summary>
    /// Conexão de fallback usada quando nada é configurado. Só desenvolvimento local.
    /// </summary>
    public const string FallbackConnectionString =
        "Host=localhost;Port=5432;Database=cyberprotech_lab;Username=postgres;Password=changeme";

    public AppDbContext CreateDbContext(string[] args)
    {
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";

        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile($"appsettings.{environment}.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        // Design-time nao deve falhar por conexao ausente: o provider e configurado
        // com um placeholder apenas para permitir a geracao do codigo da migration.
        var connectionString = configuration.GetConnectionString("DefaultConnection")
                               ?? FallbackConnectionString;

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString, npgsql => npgsql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName))
            .Options;

        return new AppDbContext(options);
    }
}
