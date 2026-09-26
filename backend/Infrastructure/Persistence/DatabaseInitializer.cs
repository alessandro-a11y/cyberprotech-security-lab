using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Persistence;

/// <summary>
/// Aplica as migrations pendentes e popula o banco com os dados iniciais do laboratório.
/// Executado na inicialização da API (ver <c>Database:MigrateOnStartup</c>).
/// </summary>
public static class DatabaseInitializer
{
    /// <summary>
    /// Valor usado no seed até a Fase 2 conectar o hasher de senha de verdade.
    /// Não é um hash válido: o login só passa a funcionar na Fase 2.
    /// </summary>
    private const string PendingPasswordHash = "!pending:fase-2";

    public static async Task InitializeDatabaseAsync(
        this IServiceProvider services,
        IConfiguration configuration,
        CancellationToken cancellationToken = default)
    {
        if (!configuration.GetValue("Database:MigrateOnStartup", true))
        {
            return;
        }

        await using var scope = services.CreateAsyncScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(DatabaseInitializer).FullName!);
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await dbContext.Database.MigrateAsync(cancellationToken);
        logger.LogInformation("Banco migrado com sucesso.");

        if (configuration.GetValue("Database:Seed", true))
        {
            await SeedAsync(dbContext, logger, cancellationToken);
        }
    }

    private static async Task SeedAsync(AppDbContext dbContext, ILogger logger, CancellationToken cancellationToken)
    {
        // users é pequena; AnyAsync evita materialize a tabela inteira.
        if (await dbContext.Users.AnyAsync(cancellationToken))
        {
            logger.LogInformation("Seed ignorado: a tabela users já possui dados.");
            return;
        }

        // Os Ids e as datas acompanham os dados de exemplo do frontend
        // (frontend/src/data/sampleUsers.js), para a demonstração ficar coerente
        // mesmo com VITE_USE_SAMPLE_DATA=false.
        dbContext.Users.AddRange(
            new User
            {
                Id = Guid.Parse("3f6c1a52-8d1e-4b7a-9c0f-1a2b3c4d5e01"),
                Username = "admin",
                Email = "admin@cyberprotech.lab",
                Role = "Admin",
                Bio = "Conta administrativa do laboratório.",
                PasswordHash = PendingPasswordHash,
                CreatedAt = new DateTime(2026, 9, 1, 9, 0, 0, DateTimeKind.Utc),
            },
            new User
            {
                Id = Guid.Parse("3f6c1a52-8d1e-4b7a-9c0f-1a2b3c4d5e02"),
                Username = "aluno01",
                Email = "aluno01@cyberprotech.lab",
                Role = "User",
                Bio = "Estudante de segurança da informação.",
                PasswordHash = PendingPasswordHash,
                CreatedAt = new DateTime(2026, 9, 2, 14, 20, 0, DateTimeKind.Utc),
            },
            new User
            {
                Id = Guid.Parse("3f6c1a52-8d1e-4b7a-9c0f-1a2b3c4d5e03"),
                Username = "maria.souza",
                Email = "maria.souza@cyberprotech.lab",
                Role = "User",
                Bio = "Analista de suporte. Gosta de café e de logs bem escritos.",
                PasswordHash = PendingPasswordHash,
                CreatedAt = new DateTime(2026, 9, 3, 10, 5, 0, DateTimeKind.Utc),
            },
            new User
            {
                Id = Guid.Parse("3f6c1a52-8d1e-4b7a-9c0f-1a2b3c4d5e04"),
                Username = "joao.lima",
                Email = "joao.lima@cyberprotech.lab",
                Role = "User",
                Bio = "Desenvolvedor front-end em treinamento.",
                PasswordHash = PendingPasswordHash,
                CreatedAt = new DateTime(2026, 9, 5, 16, 40, 0, DateTimeKind.Utc),
            },
            new User
            {
                Id = Guid.Parse("3f6c1a52-8d1e-4b7a-9c0f-1a2b3c4d5e05"),
                Username = "carla.admin",
                Email = "carla@cyberprotech.lab",
                Role = "Admin",
                Bio = "Responsável pela gestão de acessos.",
                PasswordHash = PendingPasswordHash,
                CreatedAt = new DateTime(2026, 9, 8, 8, 30, 0, DateTimeKind.Utc),
            },
            new User
            {
                Id = Guid.Parse("3f6c1a52-8d1e-4b7a-9c0f-1a2b3c4d5e06"),
                Username = "pedro.alves",
                Email = "pedro.alves@cyberprotech.lab",
                Role = "User",
                Bio = null,
                PasswordHash = PendingPasswordHash,
                CreatedAt = new DateTime(2026, 9, 12, 11, 15, 0, DateTimeKind.Utc),
            });

        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Seed aplicado: 6 usuários de exemplo criados.");
    }
}
