using Application.Interfaces;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories;

/// <summary>
/// Implementação EF Core do repositório de usuários.
/// </summary>
public sealed class EfUserRepository(AppDbContext dbContext) : IUserRepository
{
    public async Task<IReadOnlyList<User>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await dbContext.Users
            .AsNoTracking()
            .OrderBy(u => u.Username)
            .ToListAsync(cancellationToken);

    public async Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

    public async Task<User?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken = default) =>
        await dbContext.Users
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

    public async Task<IReadOnlyList<User>> SearchAsync(string? search, CancellationToken cancellationToken = default)
    {
        var query = dbContext.Users.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            // O termo viaja como parâmetro (nunca concatenado no SQL), mas os
            // curingas do LIKE precisam ser escapados para que a busca por
            // "100%" não corresponda a qualquer coisa.
            var term = $"%{EscapeLikePattern(search.Trim())}%";

            query = query.Where(u =>
                EF.Functions.ILike(u.Username, term, LikeEscapeCharacter)
                || EF.Functions.ILike(u.Email, term, LikeEscapeCharacter));
        }

        return await query.OrderBy(u => u.Username).ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Busca por usuário, ignorando maiúsculas/minúsculas. É o método usado
    /// pelo login — por isso compara sem traduzir para o banco.
    /// </summary>
    public Task<User?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default) =>
        dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Username == Canonicalize(username), cancellationToken);

    public Task<bool> ExistsByUsernameOrEmailAsync(
        string username,
        string email,
        CancellationToken cancellationToken = default)
    {
        var normalizedUsername = Canonicalize(username);
        var normalizedEmail = Canonicalize(email);

        return dbContext.Users.AnyAsync(
            u => u.Username == normalizedUsername || u.Email == normalizedEmail,
            cancellationToken);
    }

    /// <remarks>
    /// Username e e-mail são canonicalizados para minúsculas na gravação. Como o
    /// índice único do PostgreSQL diferencia maiúsculas, normalizar na entrada é o
    /// que impede "Admin" e "admin" de virarem dois usuários distintos.
    /// </remarks>
    public async Task AddAsync(User user, CancellationToken cancellationToken = default)
    {
        user.Username = Canonicalize(user.Username);
        user.Email = Canonicalize(user.Email);

        await dbContext.Users.AddAsync(user, cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        dbContext.SaveChangesAsync(cancellationToken);

    public Task DeleteAsync(User user, CancellationToken cancellationToken = default)
    {
        dbContext.Users.Remove(user);
        return Task.CompletedTask;
    }

    private const string LikeEscapeCharacter = "\\";

    private static string EscapeLikePattern(string value) =>
        value.Replace(LikeEscapeCharacter, LikeEscapeCharacter + LikeEscapeCharacter)
             .Replace("%", LikeEscapeCharacter + "%")
             .Replace("_", LikeEscapeCharacter + "_");

    private static string Canonicalize(string value) =>
        value.Trim().ToLowerInvariant();
}
