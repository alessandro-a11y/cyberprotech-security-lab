using Application.Interfaces;
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
    /// Hash sem uso deixado pela Fase 1, antes de existir o hasher de senha.
    /// Usuários ainda com esse valor não conseguem fazer login, então o seed os
    /// reescreve com um hash de verdade.
    /// </summary>
    private const string LegacyPlaceholderHash = "!pending:fase-2";

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
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>()
            .CreateLogger(typeof(DatabaseInitializer).FullName!);
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        await dbContext.Database.MigrateAsync(cancellationToken);
        logger.LogInformation("Banco migrado com sucesso.");

        if (configuration.GetValue("Database:Seed", true))
        {
            var seedPassword = configuration.GetValue("Seed:Password", DefaultSeedPassword) ?? DefaultSeedPassword;

            await SeedAsync(dbContext, passwordHasher, seedPassword, logger, cancellationToken);
        }
    }

    private const string DefaultSeedPassword = "CyberProtech@2026";

    private static async Task SeedAsync(
        AppDbContext dbContext,
        IPasswordHasher passwordHasher,
        string seedPassword,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        // users é pequena; AnyAsync evita materializar a tabela inteira.
        if (!await dbContext.Users.AnyAsync(cancellationToken))
        {
            await CreateLabUsersAsync(dbContext, passwordHasher, seedPassword, logger, cancellationToken);
            return;
        }

        await RepairLegacyHashesAsync(dbContext, passwordHasher, seedPassword, logger, cancellationToken);
    }

    private static async Task RepairLegacyHashesAsync(
        AppDbContext dbContext,
        IPasswordHasher passwordHasher,
        string seedPassword,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var legacy = await dbContext.Users
            .Where(u => u.PasswordHash == LegacyPlaceholderHash)
            .ToListAsync(cancellationToken);

        if (legacy.Count == 0)
        {
            logger.LogInformation("Seed ignorado: a tabela users já possui dados.");
            return;
        }

        var hash = passwordHasher.Hash(seedPassword);

        foreach (var user in legacy)
        {
            user.PasswordHash = hash;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation(
            "Hash de senha placeholder substituído em {Count} usuário(s) do seed.",
            legacy.Count);
    }

    private static async Task CreateLabUsersAsync(
        AppDbContext dbContext,
        IPasswordHasher passwordHasher,
        string seedPassword,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        // Os Ids e as datas acompanham os dados de exemplo do frontend
        // (frontend/src/data/sampleUsers.js), para a demonstração ficar coerente
        // mesmo com VITE_USE_SAMPLE_DATA=false.
        dbContext.Users.AddRange(
            NewUser("3f6c1a52-8d1e-4b7a-9c0f-1a2b3c4d5e01", "admin", "admin@cyberprotech.lab", "Admin",
                "Conta administrativa do laboratório.", new DateTime(2026, 9, 1, 9, 0, 0, DateTimeKind.Utc), passwordHasher, seedPassword),
            NewUser("3f6c1a52-8d1e-4b7a-9c0f-1a2b3c4d5e02", "aluno01", "aluno01@cyberprotech.lab", "User",
                "Estudante de segurança da informação.", new DateTime(2026, 9, 2, 14, 20, 0, DateTimeKind.Utc), passwordHasher, seedPassword),
            NewUser("3f6c1a52-8d1e-4b7a-9c0f-1a2b3c4d5e03", "maria.souza", "maria.souza@cyberprotech.lab", "User",
                "Analista de suporte. Gosta de café e de logs bem escritos.", new DateTime(2026, 9, 3, 10, 5, 0, DateTimeKind.Utc), passwordHasher, seedPassword),
            NewUser("3f6c1a52-8d1e-4b7a-9c0f-1a2b3c4d5e04", "joao.lima", "joao.lima@cyberprotech.lab", "User",
                "Desenvolvedor front-end em treinamento.", new DateTime(2026, 9, 5, 16, 40, 0, DateTimeKind.Utc), passwordHasher, seedPassword),
            NewUser("3f6c1a52-8d1e-4b7a-9c0f-1a2b3c4d5e05", "carla.admin", "carla@cyberprotech.lab", "Admin",
                "Responsável pela gestão de acessos.", new DateTime(2026, 9, 8, 8, 30, 0, DateTimeKind.Utc), passwordHasher, seedPassword),
            NewUser("3f6c1a52-8d1e-4b7a-9c0f-1a2b3c4d5e06", "pedro.alves", "pedro.alves@cyberprotech.lab", "User",
                null, new DateTime(2026, 9, 12, 11, 15, 0, DateTimeKind.Utc), passwordHasher, seedPassword));

        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Seed aplicado: 6 usuários de exemplo criados.");
    }

    private static User NewUser(
        string id,
        string username,
        string email,
        string role,
        string? bio,
        DateTime createdAt,
        IPasswordHasher passwordHasher,
        string seedPassword) =>
        new()
        {
            Id = Guid.Parse(id),
            Username = username,
            Email = email,
            Role = role,
            Bio = bio,
            PasswordHash = passwordHasher.Hash(seedPassword),
            CreatedAt = createdAt,
        };
}
